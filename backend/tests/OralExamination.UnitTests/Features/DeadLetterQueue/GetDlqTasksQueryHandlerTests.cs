using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;
using OralExamination.Domain.Entities;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class GetDlqTasksQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new OralExamDbContext(options);
        DlqTestActors.Seed(context);
        return context;
    }

    [Fact(DisplayName = "1. GetDlqTasksQuery: Trả về toàn bộ danh sách các tác vụ DLQ sắp xếp giảm dần theo thời gian tạo")]
    public async Task Handle_ReturnsAllDlqTasks_OrderedByCreatedAtDescending()
    {
        // Arrange
        using var context = CreateDbContext();
        var task1 = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{\"answerId\":\"" + Guid.NewGuid() + "\"}",
            ErrorMessage = "Rate limit 429",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        var task2 = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{\"answerId\":\"" + Guid.NewGuid() + "\"}",
            ErrorMessage = "Timeout 504",
            RetryCount = 3,
            Status = "resolved",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ResolvedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        context.DeadLetterQueues.AddRange(task1, task2);
        await context.SaveChangesAsync();

        var handler = new GetDlqTasksQueryHandler(context);
        var query = new GetDlqTasksQuery(ActorUserId: DlqTestActors.Admin);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(task2.Id, "Tác vụ mới hơn phải xuất hiện đầu tiên");
        result.Value[1].Id.Should().Be(task1.Id);
    }

    [Fact(DisplayName = "2. GetDlqTasksQuery: Lọc chính xác các tác vụ DLQ theo Status")]
    public async Task Handle_WithStatusFilter_ReturnsFilteredTasks()
    {
        // Arrange
        using var context = CreateDbContext();
        var pendingTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{}",
            ErrorMessage = "Gemini overloaded",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var resolvedTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{}",
            ErrorMessage = "Whisper failed",
            RetryCount = 3,
            Status = "resolved",
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            ResolvedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        context.DeadLetterQueues.AddRange(pendingTask, resolvedTask);
        await context.SaveChangesAsync();

        var handler = new GetDlqTasksQueryHandler(context);
        var query = new GetDlqTasksQuery(Status: "pending", ActorUserId: DlqTestActors.Admin);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(pendingTask.Id);
        result.Value[0].Status.Should().Be("pending");
    }

    [Fact(DisplayName = "3. GetDlqTasksQuery: Trả về danh sách rỗng khi không có tác vụ nào")]
    public async Task Handle_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateDbContext();
        var handler = new GetDlqTasksQueryHandler(context);
        var query = new GetDlqTasksQuery(ActorUserId: DlqTestActors.Admin);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "4. GetDlqTasksQuery: Lọc chính xác các tác vụ DLQ theo FlowType và FlowTypes")]
    public async Task Handle_WithFlowTypeFilter_ReturnsFilteredTasks()
    {
        // Arrange
        using var context = CreateDbContext();
        var practiceTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        var mockTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "MockExamGrading",
            FlowType = "MOCK_EXAM",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Error",
            Status = "pending"
        };
        var officialTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = "{}",
            ErrorMessage = "Error",
            Status = "pending"
        };

        context.DeadLetterQueues.AddRange(practiceTask, mockTask, officialTask);
        await context.SaveChangesAsync();

        var handler = new GetDlqTasksQueryHandler(context);

        // Act 1: FlowTypes list contains PRACTICE and MOCK_EXAM
        var resultAdminFlows = await handler.Handle(new GetDlqTasksQuery(FlowTypes: new List<string> { "PRACTICE", "MOCK_EXAM" }, ActorUserId: DlqTestActors.Admin), CancellationToken.None);

        // Assert 1
        resultAdminFlows.IsSuccess.Should().BeTrue();
        resultAdminFlows.Value.Should().HaveCount(2);
        resultAdminFlows.Value.Select(x => x.FlowType).Should().Contain(new[] { "PRACTICE", "MOCK_EXAM" });

        // Act 2: FlowType = OFFICIAL_EXAM
        var resultOfficial = await handler.Handle(new GetDlqTasksQuery(FlowType: "OFFICIAL_EXAM", ActorUserId: DlqTestActors.Officer), CancellationToken.None);

        // Assert 2
        resultOfficial.IsSuccess.Should().BeTrue();
        resultOfficial.Value.Should().HaveCount(1);
        resultOfficial.Value[0].FlowType.Should().Be("OFFICIAL_EXAM");
    }

    [Fact(DisplayName = "5. GetDlqTasksQuery: Lọc chính xác các tác vụ DLQ theo ManagedByRole")]
    public async Task Handle_WithManagedByRoleFilter_ReturnsFilteredTasks()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        var examOfficerTask = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };

        context.DeadLetterQueues.AddRange(adminTask, examOfficerTask);
        await context.SaveChangesAsync();

        var handler = new GetDlqTasksQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetDlqTasksQuery(ManagedByRole: "examination_officer", ActorUserId: DlqTestActors.Officer), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ManagedByRole.Should().Be("examination_officer");
    }

    [Fact(DisplayName = "6. GetDlqTasksQuery: TaskType ưu tiên chuẩn hóa hơn FlowType cho dữ liệu legacy và bản ghi gắn nhãn lệch")]
    public async Task Handle_UsesCanonicalTaskTypePrecedence_ForLegacyAndMistaggedTasks()
    {
        using var context = CreateDbContext();
        var legacyInClass = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = "{}",
            ErrorMessage = "Legacy InClass",
            Status = "pending"
        };
        var mistaggedOfficial = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Mistagged Official",
            Status = "pending"
        };

        context.DeadLetterQueues.AddRange(legacyInClass, mistaggedOfficial);
        await context.SaveChangesAsync();

        var handler = new GetDlqTasksQueryHandler(context);
        var adminResult = await handler.Handle(new GetDlqTasksQuery(
            Status: "pending",
            ManagedByRole: "admin",
            FlowTypes: new List<string> { "PRACTICE", "MOCK_EXAM", "IN_CLASS_EXAM" },
            ActorUserId: DlqTestActors.Admin), CancellationToken.None);

        adminResult.IsSuccess.Should().BeTrue();
        adminResult.Value.Should().ContainSingle(x => x.Id == legacyInClass.Id);
        adminResult.Value.Should().NotContain(x => x.Id == mistaggedOfficial.Id);

        var inClassOnly = await handler.Handle(new GetDlqTasksQuery(
            Status: "pending",
            FlowType: "IN_CLASS_EXAM",
            ManagedByRole: "admin",
            ActorUserId: DlqTestActors.Admin), CancellationToken.None);

        inClassOnly.IsSuccess.Should().BeTrue();
        inClassOnly.Value.Should().ContainSingle(x => x.Id == legacyInClass.Id);
    }
}

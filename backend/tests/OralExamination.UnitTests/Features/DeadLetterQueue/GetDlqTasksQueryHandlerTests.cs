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
        return new OralExamDbContext(options);
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
        var query = new GetDlqTasksQuery();

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
        var query = new GetDlqTasksQuery(Status: "pending");

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
        var query = new GetDlqTasksQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Queries.GetDlqTasks;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Domain.Entities;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.DeadLetterQueue;

public class ResolveDlqTaskCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. ResolveDlqTaskCommand: Trả về Failure khi không tìm thấy tác vụ DLQ với Id đã cho")]
    public async Task Handle_WhenTaskNotFound_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var handler = new ResolveDlqTaskCommandHandler(context);
        var command = new ResolveDlqTaskCommand(Guid.NewGuid(), "Resolved");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy tác vụ DLQ");
    }

    [Fact(DisplayName = "2. ResolveDlqTaskCommand: Admin resolve tác vụ Practice/Mock thành công kèm ghi chú và ResolvedByUserId")]
    public async Task Handle_WhenPracticeTask_WithAdminRole_ResolvesSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Transient error",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var handler = new ResolveDlqTaskCommandHandler(context);
        var command = new ResolveDlqTaskCommand(dlqEntry.Id, "Đã xử lý thủ công", adminId, "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updated!.Status.Should().Be("resolved");
        updated.ResolvedAt.Should().NotBeNull();
        updated.ResolutionNotes.Should().Be("Đã xử lý thủ công");
        updated.ResolvedByUserId.Should().Be(adminId);
    }

    [Fact(DisplayName = "3. ResolveDlqTaskCommand: Khảo thí resolve tác vụ OfficialExam thành công")]
    public async Task Handle_WhenOfficialTask_WithExamOfficerRole_ResolvesSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var officerId = Guid.NewGuid();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = "{}",
            ErrorMessage = "Timeout error",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var handler = new ResolveDlqTaskCommandHandler(context);
        var command = new ResolveDlqTaskCommand(dlqEntry.Id, "Đã duyệt bài thi", officerId, "examination_officer");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await context.DeadLetterQueues.FindAsync(dlqEntry.Id);
        updated!.Status.Should().Be("resolved");
        updated.ResolvedAt.Should().NotBeNull();
        updated.ResolutionNotes.Should().Be("Đã duyệt bài thi");
        updated.ResolvedByUserId.Should().Be(officerId);
    }

    [Fact(DisplayName = "4. ResolveDlqTaskCommand: Non-admin resolve tác vụ Practice bị từ chối FORBIDDEN")]
    public async Task Handle_WhenPracticeTask_WithNonAdminRole_ReturnsForbiddenFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var handler = new ResolveDlqTaskCommandHandler(context);
        var command = new ResolveDlqTaskCommand(dlqEntry.Id, "Note", Guid.NewGuid(), "examination_officer");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
        result.Error.Should().Contain("Quản trị viên (Admin)");
    }

    [Fact(DisplayName = "5. ResolveDlqTaskCommand: Non-examination_officer resolve tác vụ Official bị từ chối FORBIDDEN")]
    public async Task Handle_WhenOfficialTask_WithNonExamOfficerRole_ReturnsForbiddenFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var dlqEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "OfficialExamGrading",
            FlowType = "OFFICIAL_EXAM",
            ManagedByRole = "examination_officer",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending"
        };
        context.DeadLetterQueues.Add(dlqEntry);
        await context.SaveChangesAsync();

        var handler = new ResolveDlqTaskCommandHandler(context);
        var command = new ResolveDlqTaskCommand(dlqEntry.Id, "Note", Guid.NewGuid(), "admin");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
        result.Error.Should().Contain("Cán bộ Khảo thí");
    }

    [Fact(DisplayName = "6. ResolveDlqTaskCommand: Phân định loại trừ lẫn nhau theo TaskType và xóa NextRetryAt = null khi resolved")]
    public async Task Handle_MutualExclusionByTaskType_And_ClearsNextRetryAt()
    {
        using var context = CreateDbContext();
        // Trường hợp bản ghi tạo với TaskType = PracticeGrading nhưng giữ mặc định FlowType = OFFICIAL_EXAM
        var practiceWithDefaultFlow = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            PayloadJson = "{}",
            ErrorMessage = "Timeout",
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(5)
        };
        context.DeadLetterQueues.Add(practiceWithDefaultFlow);
        await context.SaveChangesAsync();

        var handler = new ResolveDlqTaskCommandHandler(context);

        // Khảo thí không được phép resolve tác vụ có TaskType == PracticeGrading
        var officerAttempt = await handler.Handle(
            new ResolveDlqTaskCommand(practiceWithDefaultFlow.Id, "Thử resolve", Guid.NewGuid(), "examination_officer"),
            CancellationToken.None);
        officerAttempt.IsSuccess.Should().BeFalse();
        officerAttempt.Error.Should().Contain("FORBIDDEN");

        // Admin được phép resolve và NextRetryAt được xóa về null
        var adminAttempt = await handler.Handle(
            new ResolveDlqTaskCommand(practiceWithDefaultFlow.Id, "Admin resolve", Guid.NewGuid(), "admin"),
            CancellationToken.None);
        adminAttempt.IsSuccess.Should().BeTrue();

        var updated = await context.DeadLetterQueues.FindAsync(practiceWithDefaultFlow.Id);
        updated!.Status.Should().Be("resolved");
        updated.NextRetryAt.Should().BeNull();
    }

    [Fact(DisplayName = "7. GetDlqTasksQueryHandler chuẩn hóa Status và SubmitMockExamSession / OfficialExamDlqReplayWorker đồng bộ trạng thái MockExamGrading")]
    public async Task GetDlqTasks_StatusNormalization_And_MockExamDlqLifecycle_Should_Succeed()
    {
        using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem SWD392",
            TotalMaxScore = 10.0m,
            IsActive = true
        };
        var c1 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "TC1", MaxScore = 5m, Weight = 0.5m, OrderIndex = 1 };
        var c2 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "TC2", MaxScore = 5m, Weight = 0.5m, OrderIndex = 2 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi thi thử",
            Content = "Nội dung câu hỏi thi thử",
            SampleAnswer = new string('A', 60),
            Difficulty = "medium",
            BloomLevel = "Analyze",
            IsActive = true
        };

        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-25),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = "submitted"
        };
        var answer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            AnswerText = "Câu trả lời thi thử của sinh viên",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-5)
        };
        session.Answers.Add(answer);

        var dlqPending = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "MockExamGrading",
            CorrelationId = session.Id,
            FlowType = "MOCK_EXAM",
            ManagedByRole = "admin",
            PayloadJson = "{}",
            ErrorMessage = "Gemini 429",
            RetryCount = 1,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(4)
        };

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.AddRange(c1, c2);
        context.PracticeQuestions.Add(question);
        context.MockExamSessions.Add(session);
        context.DeadLetterQueues.Add(dlqPending);
        await context.SaveChangesAsync();

        // 1. GetDlqTasksQueryHandler lọc Status không phân biệt hoa thường & khoảng trắng
        var getDlqHandler = new GetDlqTasksQueryHandler(context);
        var queryRes = await getDlqHandler.Handle(new GetDlqTasksQuery(Status: "  PENDING  "), CancellationToken.None);
        queryRes.IsSuccess.Should().BeTrue();
        queryRes.Value.Should().ContainSingle(x => x.Id == dlqPending.Id);

        // 2. Khi session đang chờ backoff (NextRetryAt > now), OfficialExamDlqReplayWorker không quét bù làm tăng RetryCount trước hạn
        var aiMock = new Mock<IAiGradingService>();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(OralExamDbContext))).Returns(context);
        spMock.Setup(sp => sp.GetService(typeof(IAiGradingService))).Returns(aiMock.Object);
        var scopeMock = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        var scopeFactoryMock = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var workerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OfficialExamDlqReplayWorker>.Instance;
        var worker = new OfficialExamDlqReplayWorker(scopeFactoryMock.Object, workerLogger);

        await worker.ReplayDueAsync(CancellationToken.None);
        aiMock.Verify(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // 3. Khi SubmitMockExamSessionCommandHandler chấm thành công, tự động đóng DLQ pending thành resolved và xóa NextRetryAt = null
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.0m,
                ConfidenceScore = 0.9m,
                Feedback = "Tốt",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = c1.Id.ToString(), Score = 4.0m, Comment = "Tốt" },
                    new() { CriterionId = c2.Id.ToString(), Score = 4.0m, Comment = "Tốt" }
                }
            });

        var submitHandler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var submitRes = await submitHandler.Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);
        submitRes.IsSuccess.Should().BeTrue();

        var autoResolvedDlq = await context.DeadLetterQueues.FindAsync(dlqPending.Id);
        autoResolvedDlq!.Status.Should().Be("resolved");
        autoResolvedDlq.NextRetryAt.Should().BeNull();
        autoResolvedDlq.ResolvedAt.Should().NotBeNull();
        autoResolvedDlq.ResolutionNotes.Should().Be("Auto-resolved after successful mock exam grading.");
    }
}

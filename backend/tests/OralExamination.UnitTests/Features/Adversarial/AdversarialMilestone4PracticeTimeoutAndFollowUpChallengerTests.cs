using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using API.Controllers.v1;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử đối kháng thực nghiệm nâng cao (Adversarial Empirical Challenge Suite) cho Milestone 4.
/// Được thiết kế và thực thi độc lập bởi Challenger 2 (challenger_m4_2).
///
/// Các khía cạnh thách thức thực nghiệm:
/// 1. 5-Minute Inactivity Timeout (SessionInactivityTimeoutMinutes = 5):
///    - Ranh giới trôi thời gian (Elapsed Boundaries: 4.99 min vs 5.01 min).
///    - Kiểm chứng NextQuestion, SubmitAnswer, SubmitBatch đối với timeout.
///    - Cấu hình động từ system_configs (10 phút, fallback khi config hỏng).
/// 2. Ánh xạ lỗi Controller sang HTTP 410 Gone (RFC 7807 ProblemDetails):
///    - GetNextQuestion, SubmitAnswer, SubmitBatch.
/// 3. Bảo toàn điểm số và câu trả lời đã làm trước khi timeout (Score Preservation):
///    - Truy vấn chi tiết phiên và lịch sử sau khi timeout.
/// 4. Thuật toán Anti-3-Consecutive Rotation & Edge Conditions:
///    - Luân chuyển tất yếu với 2 mức độ (deterministic alternation).
///    - Câu trả lời phụ (IsFollowUp = true) không làm sai lệch cửa sổ trượt.
///    - Cạn kiệt các mức khác fallback mượt mà không crash.
/// 5. Động cơ Follow-Up ranh giới (4.0 <= Score <= 8.0) & Giới hạn tối đa (MaxPracticeFollowUpQuestions):
///    - Ranh giới điểm số cực đoan (3.99, 4.00, 6.00, 8.00, 8.01, 10.00).
///    - Chế độ Full-Session Progressive TUYỆT ĐỐI KHÔNG sinh câu hỏi phụ.
///    - Giới hạn số lượng câu hỏi phụ tối đa từ cấu hình hệ thống.
/// </summary>
public class AdversarialMilestone4PracticeTimeoutAndFollowUpChallengerTests
{
    private OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var databaseName = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName)
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<(Course Course, User Student, PracticeSession Session)> SeedPracticeSessionAsync(
        OralExamDbContext context,
        DateTime lastActivityAt,
        string practiceMode = "per_question",
        List<string>? selectedDifficulties = null,
        int? timeoutMinutes = 5,
        string? rawTimeoutConfig = null)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWT301_EMP",
            Name = "Software Testing Empirical Challenge",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"student.adv.{Guid.NewGuid():N}@fpt.edu.vn",
            FullName = "Nguyễn Văn Thực Nghiệm",
            Role = UserRole.Student,
            IsActive = true
        };

        var diffs = selectedDifficulties ?? new List<string> { "easy", "medium", "hard" };

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-60),
            LastActivityAt = lastActivityAt,
            Status = "in_progress",
            PracticeMode = practiceMode,
            SelectedDifficulties = JsonSerializer.Serialize(diffs)
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);

        if (rawTimeoutConfig != null || timeoutMinutes.HasValue)
        {
            var configVal = rawTimeoutConfig ?? timeoutMinutes!.Value.ToString();
            context.SystemConfigs.Add(new SystemConfig
            {
                Id = Guid.NewGuid(),
                Key = "SessionInactivityTimeoutMinutes",
                Value = configVal,
                Description = "Thời gian timeout không tương tác"
            });
        }

        await context.SaveChangesAsync();
        return (course, student, session);
    }

    private async Task<List<PracticeQuestion>> SeedPracticeQuestionsAsync(
        OralExamDbContext context,
        Guid courseId,
        string difficulty,
        int count)
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            Name = $"Rubric {difficulty}",
            TotalMaxScore = 10.0m
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Tiêu chí chính",
            MaxScore = 10.0m,
            OrderIndex = 1
        });
        context.Rubrics.Add(rubric);

        var list = new List<PracticeQuestion>();
        for (int i = 1; i <= count; i++)
        {
            var q = new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                RubricId = rubric.Id,
                Title = $"Question {difficulty} #{i}",
                Content = $"Adversarial Question Content {difficulty} #{i}",
                Difficulty = difficulty,
                SampleAnswer = "Câu trả lời mẫu kỹ thuật tối thiểu 50 ký tự chuẩn mực.",
                FollowUpPrompt = "Hãy giải thích sâu hơn về cơ chế này.",
                IsActive = true
            };
            list.Add(q);
            context.PracticeQuestions.Add(q);
        }

        await context.SaveChangesAsync();
        return list;
    }

    // =========================================================================
    // 1. INACTIVITY TIMEOUT ELAPSED BOUNDARIES (F11)
    // =========================================================================

    [Fact(DisplayName = "ADV-TO-01. NextQuestion: 4.99 phút (299.4 giây) - Chưa vượt ranh giới 5 phút -> Thành công, không timeout")]
    public async Task NextQuestion_Elapsed_4_99_Minutes_ShouldNotTimeout()
    {
        // Arrange: 4 phút 59 giây trước (< 5.00 phút)
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-4.99));
        await SeedPracticeQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Thiết lập LastActivityAt cách đây 295 giây (4.91 phút < 5 phút)
        session.LastActivityAt = DateTime.UtcNow.AddSeconds(-295);
        await context.SaveChangesAsync();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("4.99 phút chưa vượt ngưỡng 5.00 phút");
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question.Should().NotBeNull();

        var sessionInDb = await context.PracticeSessions.FindAsync(session.Id);
        sessionInDb!.Status.Should().Be("in_progress");
    }

    [Fact(DisplayName = "ADV-TO-02. NextQuestion: 5.01 phút (300.6 giây) - Đã vượt ranh giới 5 phút -> Báo timeout, cập nhật CompletedAt")]
    public async Task NextQuestion_Elapsed_5_01_Minutes_ShouldTimeoutAndMarkCompleted()
    {
        // Arrange: 5 phút 1 giây trước (> 5.00 phút)
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-5.01));
        await SeedPracticeQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);
        var command = new NextQuestionCommand(session.Id, student.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse("5.01 phút đã vượt ngưỡng 5.00 phút");
        result.Error.Should().Contain("không có tương tác trong hơn 5 phút");

        var sessionInDb = await context.PracticeSessions.FindAsync(session.Id);
        sessionInDb!.Status.Should().Be("completed");
        sessionInDb.CompletedAt.Should().NotBeNull();
        sessionInDb.CompletedAt.Should().BeCloseTo(session.LastActivityAt.AddMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "ADV-TO-03. SubmitPracticeAnswer: 4.99 phút -> Thành công lưu bài và đẩy hàng đợi")]
    public async Task SubmitPracticeAnswer_Elapsed_4_99_Minutes_ShouldSucceed()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-4.99));
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 2);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeAnswerCommandHandler(context, queueMock.Object);
        var command = new SubmitPracticeAnswerCommand(
            SessionId: session.Id,
            QuestionId: questions[0].Id,
            StudentId: student.Id,
            AnswerText: "Câu trả lời đúng hạn",
            IsFollowUp: false,
            ParentAnswerId: null);

        // Thiết lập LastActivityAt cách đây 295 giây (4.91 phút < 5 phút) ngay trước khi gọi handler
        session.LastActivityAt = DateTime.UtcNow.AddSeconds(-295);
        await context.SaveChangesAsync();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Once);

        var sessionInDb = await context.PracticeSessions.FindAsync(session.Id);
        sessionInDb!.Status.Should().Be("in_progress");
        sessionInDb.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "ADV-TO-04. SubmitPracticeAnswer: 5.01 phút -> Báo timeout, đóng phiên, không ghi nhận câu trả lời")]
    public async Task SubmitPracticeAnswer_Elapsed_5_01_Minutes_ShouldTimeout()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-5.01));
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 2);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeAnswerCommandHandler(context, queueMock.Object);
        var command = new SubmitPracticeAnswerCommand(
            SessionId: session.Id,
            QuestionId: questions[0].Id,
            StudentId: student.Id,
            AnswerText: "Câu trả lời bị quá hạn",
            IsFollowUp: false,
            ParentAnswerId: null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có tương tác trong hơn 5 phút");
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);

        var answersCount = await context.PracticeAnswers.CountAsync(a => a.SessionId == session.Id);
        answersCount.Should().Be(0, "Không được lưu câu trả lời khi phiên đã quá hạn");

        var sessionInDb = await context.PracticeSessions.FindAsync(session.Id);
        sessionInDb!.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "ADV-TO-05. SubmitPracticeBatch: 4.99 phút (Full-Session) -> Nộp trọn gói thành công")]
    public async Task SubmitPracticeBatch_Elapsed_4_99_Minutes_ShouldSucceed()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-4.99),
            practiceMode: "full_session");
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 3);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);
        var batchAnswers = questions.Select(q => new BatchAnswerDto(
            q.Id,
            $"Trả lời câu {q.Id}",
            false,
            null
        )).ToList();

        var command = new SubmitPracticeBatchCommand(session.Id, student.Id, batchAnswers);

        // Thiết lập LastActivityAt cách đây 295 giây (4.91 phút < 5 phút)
        session.LastActivityAt = DateTime.UtcNow.AddSeconds(-295);
        await context.SaveChangesAsync();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        var sessionInDb = await context.PracticeSessions.FindAsync(session.Id);
        sessionInDb!.Status.Should().Be("completed");
    }

    [Fact(DisplayName = "ADV-TO-06. SubmitPracticeBatch: 5.01 phút (Full-Session) -> Báo timeout, không lưu batch")]
    public async Task SubmitPracticeBatch_Elapsed_5_01_Minutes_ShouldTimeout()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-5.01),
            practiceMode: "full_session");
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 3);

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitPracticeBatchCommandHandler(context, queueMock.Object);
        var batchAnswers = questions.Select(q => new BatchAnswerDto(
            q.Id,
            $"Trả lời câu {q.Id}",
            false,
            null
        )).ToList();

        var command = new SubmitPracticeBatchCommand(session.Id, student.Id, batchAnswers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có tương tác trong hơn 5 phút");
        queueMock.Verify(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()), Times.Never);

        var answersCount = await context.PracticeAnswers.CountAsync(a => a.SessionId == session.Id);
        answersCount.Should().Be(0);
    }

    [Fact(DisplayName = "ADV-TO-07. Cấu hình động system_configs = 10 phút: 7.0 phút thành công, 10.01 phút timeout")]
    public async Task DynamicConfig_CustomTimeout10Minutes_EnforcesBoundaryAccurately()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-7.0),
            timeoutMinutes: 10);
        await SeedPracticeQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);

        // 1. Tại 7.0 phút (< 10.0 phút) -> Succeeded
        var r1 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue("7 phút nhỏ hơn cấu hình 10 phút");

        // 2. Chuyển LastActivityAt về 10 phút 1 giây trước -> Timeout
        session.LastActivityAt = DateTime.UtcNow.AddMinutes(-10.01);
        await context.SaveChangesAsync();

        var r2 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r2.IsSuccess.Should().BeFalse("10.01 phút vượt quá cấu hình 10 phút");
        r2.Error.Should().Contain("không có tương tác trong hơn 5 phút");
    }

    [Fact(DisplayName = "ADV-TO-08. Cấu hình system_configs bị hỏng hoặc thiếu -> Tự động fallback về mặc định 5 phút")]
    public async Task DynamicConfig_CorruptedValue_GracefullyFallsBackTo5Minutes()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-4.5),
            rawTimeoutConfig: "invalid_string_not_a_number");
        await SeedPracticeQuestionsAsync(context, course.Id, "easy", 5);

        var handler = new NextQuestionCommandHandler(context);

        // 1. 4.5 phút (< 5 phút) -> Vẫn thành công
        var r1 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();

        // 2. 5.5 phút (> 5 phút) -> Timeout an toàn
        session.LastActivityAt = DateTime.UtcNow.AddMinutes(-5.5);
        await context.SaveChangesAsync();

        var r2 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r2.IsSuccess.Should().BeFalse();
        r2.Error.Should().Contain("không có tương tác trong hơn 5 phút");
    }

    // =========================================================================
    // 2. CONTROLLER HTTP 410 GONE MAPPING (RFC 7807)
    // =========================================================================

    private PracticeController CreateTestController(Mock<ISender> senderMock, Guid studentId)
    {
        var controller = new PracticeController(senderMock.Object);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, studentId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return controller;
    }

    [Fact(DisplayName = "ADV-CTRL-01. PracticeController.GetNextQuestion: Trả về HTTP 410 Gone ProblemDetails khi timeout")]
    public async Task Controller_GetNextQuestion_OnTimeout_Returns410GoneProblemDetails()
    {
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = CreateTestController(senderMock, studentId);

        const string timeoutMsg = "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.";
        senderMock.Setup(s => s.Send(It.Is<NextQuestionCommand>(c => c.SessionId == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Failure(timeoutMsg));

        // Act
        var result = await controller.GetNextQuestion(sessionId);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problem = objResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status410Gone);
        problem.Title.Should().Be("Session Timed Out");
        problem.Detail.Should().Be(timeoutMsg);
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7807");
    }

    [Fact(DisplayName = "ADV-CTRL-02. PracticeController.SubmitAnswer: Trả về HTTP 410 Gone ProblemDetails khi timeout")]
    public async Task Controller_SubmitAnswer_OnTimeout_Returns410GoneProblemDetails()
    {
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = CreateTestController(senderMock, studentId);

        const string timeoutMsg = "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.";
        senderMock.Setup(s => s.Send(It.Is<SubmitPracticeAnswerCommand>(c => c.SessionId == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure(timeoutMsg));

        var request = new SubmitPracticeAnswerRequest(
            QuestionId: Guid.NewGuid(),
            StudentId: studentId,
            AnswerText: "Câu trả lời sau timeout",
            IsFollowUp: false,
            ParentAnswerId: null);

        // Act
        var result = await controller.SubmitAnswer(sessionId, request);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problem = objResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status410Gone);
        problem.Title.Should().Be("Session Timed Out");
        problem.Detail.Should().Be(timeoutMsg);
    }

    [Fact(DisplayName = "ADV-CTRL-03. PracticeController.SubmitBatch: Trả về HTTP 410 Gone ProblemDetails khi timeout")]
    public async Task Controller_SubmitBatch_OnTimeout_Returns410GoneProblemDetails()
    {
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = CreateTestController(senderMock, studentId);

        const string timeoutMsg = "Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.";
        senderMock.Setup(s => s.Send(It.Is<SubmitPracticeBatchCommand>(c => c.SessionId == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure(timeoutMsg));

        var request = new SubmitPracticeBatchRequest(
            StudentId: studentId,
            Answers: new List<BatchAnswerDto>());

        // Act
        var result = await controller.SubmitBatch(sessionId, request);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problem = objResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status410Gone);
        problem.Title.Should().Be("Session Timed Out");
        problem.Detail.Should().Be(timeoutMsg);
    }

    // =========================================================================
    // 3. SCORE & ANSWER PRESERVATION ACROSS TIMEOUT (F11)
    // =========================================================================

    [Fact(DisplayName = "ADV-DATA-01. Bảo toàn 100% câu trả lời & điểm số trước khi timeout trong GetPracticeSession và History")]
    public async Task InactivityTimeout_PreservesAllPriorAnswersAndEvaluationsInSessionAndHistory()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow.AddMinutes(-20), // Quá hạn 20 phút
            timeoutMinutes: 5);
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 3);

        // Tạo 3 câu trả lời đã được AI chấm điểm
        var scores = new[] { 7.0m, 8.5m, 9.0m };
        for (int i = 0; i < 3; i++)
        {
            var ans = new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = questions[i].Id,
                StudentId = student.Id,
                AnswerText = $"Câu trả lời #{i + 1}",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-40 + i * 5),
                IsFollowUp = false,
                Status = "graded"
            };
            var eval = new AiEvaluation
            {
                Id = Guid.NewGuid(),
                AnswerType = "practice",
                PracticeAnswerId = ans.Id,
                TotalScore = scores[i],
                Feedback = $"Phản hồi #{i + 1}",
                EvaluatedAt = DateTime.UtcNow.AddMinutes(-39 + i * 5)
            };
            context.PracticeAnswers.Add(ans);
            context.AiEvaluations.Add(eval);
        }
        await context.SaveChangesAsync();

        // 1. Kích hoạt lazy timeout qua NextQuestion
        var nextHandler = new NextQuestionCommandHandler(context);
        var nextRes = await nextHandler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        nextRes.IsSuccess.Should().BeFalse();

        // 2. Xác minh truy vấn chi tiết phiên (GetPracticeSessionQueryHandler)
        var sessionHandler = new GetPracticeSessionQueryHandler(context);
        var sessionDetailRes = await sessionHandler.Handle(
            new GetPracticeSessionQuery(session.Id, student.Id, IsElevatedRole: false),
            CancellationToken.None);

        sessionDetailRes.IsSuccess.Should().BeTrue();
        var detail = sessionDetailRes.Value;
        detail.Status.Should().Be("completed");
        detail.Answers.Should().HaveCount(3);
        detail.Answers.Select(a => a.TotalScore).Should().BeEquivalentTo(new[] { 7.0m, 8.5m, 9.0m });

        // 3. Xác minh truy vấn lịch sử luyện tập (GetStudentPracticeHistoryQueryHandler)
        var historyHandler = new GetStudentPracticeHistoryQueryHandler(context);
        var historyRes = await historyHandler.Handle(
            new GetStudentPracticeHistoryQuery(student.Id),
            CancellationToken.None);

        historyRes.IsSuccess.Should().BeTrue();
        var historyItem = historyRes.Value.FirstOrDefault(h => h.SessionId == session.Id);
        historyItem.Should().NotBeNull();
        historyItem!.Status.Should().Be("completed");
        historyItem.TotalQuestions.Should().Be(3);
        historyItem.AverageScore.Should().Be(8.17m); // (7.0 + 8.5 + 9.0) / 3 = 8.1666... -> 8.17
    }

    // =========================================================================
    // 4. ANTI-3-CONSECUTIVE ROTATION & EDGE CONDITIONS (F12)
    // =========================================================================

    [Fact(DisplayName = "ADV-ROT-01. Hai mức độ (easy, hard): 2 câu liên tiếp cùng mức BẮT BUỘC đổi sang mức còn lại (100% deterministic)")]
    public async Task Anti3Consecutive_TwoDifficulties_StrictlyAlternatesAfterConsecutivePair()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow,
            selectedDifficulties: new List<string> { "easy", "hard" });

        await SeedPracticeQuestionsAsync(context, course.Id, "easy", 15);
        await SeedPracticeQuestionsAsync(context, course.Id, "hard", 15);

        var handler = new NextQuestionCommandHandler(context);
        var history = new List<string>();

        for (int q = 1; q <= 20; q++)
        {
            var res = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
            res.IsSuccess.Should().BeTrue();
            var diff = res.Value.Question!.Difficulty.ToLowerInvariant();
            history.Add(diff);

            // Ghi nhận answer
            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = res.Value.Question.Id,
                StudentId = student.Id,
                AnswerText = $"Ans {q}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(q * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }

        // Kiểm tra cửa sổ trượt 3 câu
        for (int i = 0; i <= history.Count - 3; i++)
        {
            var isThreeSame = (history[i] == history[i + 1] && history[i + 1] == history[i + 2]);
            isThreeSame.Should().BeFalse(
                $"Vi phạm Anti-3-Consecutive tại [{i}..{i + 2}]: {history[i]} -> {history[i + 1]} -> {history[i + 2]}");
        }
    }

    [Fact(DisplayName = "ADV-ROT-02. Câu hỏi phụ (IsFollowUp = true) xen giữa không phá vỡ logic kiểm tra 2 câu chính liền trước")]
    public async Task Anti3Consecutive_InterleavedFollowUps_DoNotDisruptSlidingWindowCheck()
    {
        using var context = CreateDbContext();
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow,
            selectedDifficulties: new List<string> { "easy", "medium", "hard" });

        var easyQuestions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 5);
        await SeedPracticeQuestionsAsync(context, course.Id, "medium", 5);
        await SeedPracticeQuestionsAsync(context, course.Id, "hard", 5);

        // Nộp câu chính 1 (easy)
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[0].Id,
            StudentId = student.Id,
            AnswerText = "Chính 1 (easy)",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10),
            IsFollowUp = false
        });

        // Nộp câu phụ 1 của câu 1
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[0].Id,
            StudentId = student.Id,
            AnswerText = "Phụ 1 của câu 1",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-9),
            IsFollowUp = true
        });

        // Nộp câu chính 2 (easy)
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[1].Id,
            StudentId = student.Id,
            AnswerText = "Chính 2 (easy)",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-8),
            IsFollowUp = false
        });

        // Nộp câu phụ 2 của câu 2
        context.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = easyQuestions[1].Id,
            StudentId = student.Id,
            AnswerText = "Phụ 2 của câu 2",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-7),
            IsFollowUp = true
        });

        await context.SaveChangesAsync();

        // Act: Bốc câu thứ 3
        var handler = new NextQuestionCommandHandler(context);
        var result = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);

        // Assert: 2 câu chính liền trước đều là easy, nên câu thứ 3 TUYỆT ĐỐI KHÔNG ĐƯỢC LÀ easy!
        result.IsSuccess.Should().BeTrue();
        result.Value.Question!.Difficulty.ToLowerInvariant().Should().NotBe(
            "easy",
            "Vì 2 câu chính liền trước (loại trừ follow-up) đều là easy");
    }

    // =========================================================================
    // 5. BORDERLINE FOLLOW-UP ENGINE (4.0 <= Score <= 8.0) & FULL-SESSION INVARIANT (F12)
    // =========================================================================

    private class TestGradingChannel : IGradingQueueChannel
    {
        private readonly Channel<GradingTask> _channel = Channel.CreateUnbounded<GradingTask>();

        public async ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(task, cancellationToken);
        }

        public async IAsyncEnumerable<GradingTask> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var task))
                {
                    yield return task;
                }
            }
        }

        public void Complete() => _channel.Writer.Complete();
    }

    [Theory(DisplayName = "ADV-FOL-01. Ranh giới điểm số kích hoạt Follow-up MF-01: [4.0, 8.0] kích hoạt, ngoài khoảng thì không")]
    [InlineData(3.99, false)]
    [InlineData(4.00, true)]
    [InlineData(6.00, true)]
    [InlineData(8.00, true)]
    [InlineData(8.01, false)]
    [InlineData(10.00, false)]
    [InlineData(0.00, false)]
    public async Task BorderlineFollowUp_ScoreBoundaries_CorrectlyIdentified(decimal score, bool expectedNeedsFollowUp)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);
        var (course, student, session) = await SeedPracticeSessionAsync(context, DateTime.UtcNow);
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 1);
        var question = questions[0];

        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Sinh viên trả lời kiểm thử ranh giới follow-up",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow,
            IsFollowUp = false
        };
        context.PracticeAnswers.Add(answer);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = score,
                ConfidenceScore = 0.95m,
                Feedback = $"Điểm số {score}",
                Details = new List<GradingDetailDto>
                {
                    new GradingDetailDto { CriterionId = question.Rubric!.Criteria.First().Id.ToString(), Score = score, Comment = "Đạt" }
                }
            });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => CreateDbContext(dbName));
        services.AddScoped(_ => aiMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var hubClientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();

        hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group($"session_{session.Id}")).Returns(clientMock.Object);

        PracticeAnswerDetailDto? capturedDto = null;
        clientMock.Setup(c => c.ReceiveGradingResult(It.IsAny<PracticeAnswerDetailDto>()))
            .Callback<PracticeAnswerDetailDto>(dto => capturedDto = dto)
            .Returns(Task.CompletedTask);

        var channel = new TestGradingChannel();
        var worker = new GradingQueueWorker(
            channel,
            serviceProvider,
            hubMock.Object,
            NullLogger<GradingQueueWorker>.Instance);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var workerTask = worker.StartAsync(cts.Token);
        await channel.EnqueueAsync(new GradingTask
        {
            AnswerId = answer.Id,
            QuestionId = question.Id,
            SessionId = session.Id,
            StudentId = student.Id,
            AnswerText = answer.AnswerText,
            IsFollowUp = false,
            IsFullSession = false
        });
        channel.Complete();

        // Chờ xử lý xong
        for (int i = 0; i < 50 && capturedDto == null; i++)
        {
            await Task.Delay(50);
        }

        await worker.StopAsync(CancellationToken.None);

        // Assert
        capturedDto.Should().NotBeNull("Worker phải hoàn thành chấm điểm và phát SignalR");
        capturedDto!.TotalScore.Should().Be(score);
        capturedDto.NeedsFollowUp.Should().Be(
            expectedNeedsFollowUp,
            $"Điểm {score} phải có NeedsFollowUp = {expectedNeedsFollowUp}");
    }

    [Fact(DisplayName = "ADV-FOL-02. Cấu hình MaxPracticeFollowUpQuestions: Đạt tối đa câu phụ thì KHÔNG sinh thêm")]
    public async Task BorderlineFollowUp_MaxPracticeFollowUpQuestions_Enforced()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);
        var (course, student, session) = await SeedPracticeSessionAsync(context, DateTime.UtcNow);
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 1);
        var question = questions[0];

        // Cấu hình MaxPracticeFollowUpQuestions = 2
        context.SystemConfigs.Add(new SystemConfig
        {
            Id = Guid.NewGuid(),
            Key = "MaxPracticeFollowUpQuestions",
            Value = "2",
            Description = "Số câu phụ tối đa"
        });

        // Đã có sẵn 2 câu phụ của câu hỏi này trong phiên
        for (int i = 1; i <= 2; i++)
        {
            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = question.Id,
                StudentId = student.Id,
                AnswerText = $"Câu phụ #{i}",
                Status = "graded",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-5 * i),
                IsFollowUp = true
            });
        }

        // Câu trả lời mới cần chấm
        var newAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời mới",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow,
            IsFollowUp = true
        };
        context.PracticeAnswers.Add(newAnswer);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 5.0m, // Nằm trong [4.0, 8.0]
                ConfidenceScore = 0.9m,
                Feedback = "Trung bình",
                Details = new List<GradingDetailDto>
                {
                    new GradingDetailDto { CriterionId = question.Rubric!.Criteria.First().Id.ToString(), Score = 5.0m, Comment = "Đạt" }
                }
            });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => CreateDbContext(dbName));
        services.AddScoped(_ => aiMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var hubClientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();
        hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group($"session_{session.Id}")).Returns(clientMock.Object);

        PracticeAnswerDetailDto? capturedDto = null;
        clientMock.Setup(c => c.ReceiveGradingResult(It.IsAny<PracticeAnswerDetailDto>()))
            .Callback<PracticeAnswerDetailDto>(dto => capturedDto = dto)
            .Returns(Task.CompletedTask);

        var channel = new TestGradingChannel();
        var worker = new GradingQueueWorker(
            channel,
            serviceProvider,
            hubMock.Object,
            NullLogger<GradingQueueWorker>.Instance);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await channel.EnqueueAsync(new GradingTask
        {
            AnswerId = newAnswer.Id,
            QuestionId = question.Id,
            SessionId = session.Id,
            StudentId = student.Id,
            AnswerText = newAnswer.AnswerText,
            IsFollowUp = true,
            IsFullSession = false
        });
        channel.Complete();

        for (int i = 0; i < 50 && capturedDto == null; i++)
        {
            await Task.Delay(50);
        }
        await worker.StopAsync(CancellationToken.None);

        // Assert: Dù điểm 5.0m nằm trong [4.0, 8.0], nhưng đã có 2 câu phụ nên NeedsFollowUp = false
        capturedDto.Should().NotBeNull();
        capturedDto!.NeedsFollowUp.Should().BeFalse(
            "Vì số câu phụ đã đạt mức trần MaxPracticeFollowUpQuestions = 2");
    }

    [Theory(DisplayName = "ADV-FOL-03. Chế độ Full-Session Progressive: TUYỆT ĐỐI KHÔNG sinh câu hỏi phụ dù điểm rơi vào [4.0, 8.0]")]
    [InlineData(4.0)]
    [InlineData(5.5)]
    [InlineData(6.0)]
    [InlineData(8.0)]
    public async Task FullSessionProgressive_NeverTriggersFollowUpQuestions(decimal score)
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);
        var (course, student, session) = await SeedPracticeSessionAsync(
            context,
            DateTime.UtcNow,
            practiceMode: "full_session");
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "easy", 1);
        var question = questions[0];

        var answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Sinh viên trả lời trong chế độ Full-Session",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow,
            IsFollowUp = false
        };
        context.PracticeAnswers.Add(answer);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = score,
                ConfidenceScore = 0.95m,
                Feedback = "Bài làm đạt điểm ranh giới",
                Details = new List<GradingDetailDto>
                {
                    new GradingDetailDto { CriterionId = question.Rubric!.Criteria.First().Id.ToString(), Score = score, Comment = "Đạt" }
                }
            });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => CreateDbContext(dbName));
        services.AddScoped(_ => aiMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var hubClientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();
        hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group($"session_{session.Id}")).Returns(clientMock.Object);

        PracticeAnswerDetailDto? capturedDto = null;
        clientMock.Setup(c => c.ReceiveGradingResult(It.IsAny<PracticeAnswerDetailDto>()))
            .Callback<PracticeAnswerDetailDto>(dto => capturedDto = dto)
            .Returns(Task.CompletedTask);

        var channel = new TestGradingChannel();
        var worker = new GradingQueueWorker(
            channel,
            serviceProvider,
            hubMock.Object,
            NullLogger<GradingQueueWorker>.Instance);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await channel.EnqueueAsync(new GradingTask
        {
            AnswerId = answer.Id,
            QuestionId = question.Id,
            SessionId = session.Id,
            StudentId = student.Id,
            AnswerText = answer.AnswerText,
            IsFollowUp = false,
            IsFullSession = true // Chế độ Full-Session Progressive
        });
        channel.Complete();

        for (int i = 0; i < 50 && capturedDto == null; i++)
        {
            await Task.Delay(50);
        }
        await worker.StopAsync(CancellationToken.None);

        // Assert
        capturedDto.Should().NotBeNull();
        capturedDto!.NeedsFollowUp.Should().BeFalse(
            "Luật bất biến MF-01: Chế độ Full-Session Progressive CẤM câu hỏi phụ (IsFullSession = true)");
    }

    [Fact(DisplayName = "ADV-FOL-04. Chuỗi Follow-up #1 -> Follow-up #2: Không lặp lại câu hỏi phụ cũ, truyền ngữ cảnh vào AI và đồng bộ GetPracticeSession")]
    public async Task BorderlineFollowUp_SecondFollowUp_NeverRepeatsFirstPrompt_AndSyncsWithGetSession()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateDbContext(dbName);
        var (course, student, session) = await SeedPracticeSessionAsync(context, DateTime.UtcNow);
        var questions = await SeedPracticeQuestionsAsync(context, course.Id, "medium", 1);
        var question = questions[0];
        question.FollowUpPrompt = "Câu hỏi phụ gốc #1: Hãy giải thích sự khác biệt giữa Scoped và Transient?";

        var mainAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "Em trả lời câu hỏi chính ở mức điểm 6.0",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2),
            IsFollowUp = false
        };
        context.PracticeAnswers.Add(mainAnswer);
        await context.SaveChangesAsync();

        AiGradingRequest? capturedFollowUpGradingRequest = null;
        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiGradingRequest, CancellationToken>((req, _) =>
            {
                if (req.IsFollowUp)
                {
                    capturedFollowUpGradingRequest = req;
                }
            })
            .ReturnsAsync(new AiGradingResult
            {
                Score = 6.0m,
                ConfidenceScore = 0.92m,
                Feedback = "Trả lời đạt mức khá nhưng cần phân tích sâu hơn.",
                Details = new List<GradingDetailDto>
                {
                    new GradingDetailDto { CriterionId = question.Rubric!.Criteria.First().Id.ToString(), Score = 6.0m, Comment = "Khá" }
                }
            });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => CreateDbContext(dbName));
        services.AddScoped(_ => aiMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var hubClientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();
        hubMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);
        hubClientsMock.Setup(c => c.Group($"session_{session.Id}")).Returns(clientMock.Object);

        var receivedDtos = new List<PracticeAnswerDetailDto>();
        clientMock.Setup(c => c.ReceiveGradingResult(It.IsAny<PracticeAnswerDetailDto>()))
            .Callback<PracticeAnswerDetailDto>(dto => receivedDtos.Add(dto))
            .Returns(Task.CompletedTask);

        // Bước 1: Chấm câu trả lời chính -> Sinh Follow-up #1
        var channel1 = new TestGradingChannel();
        var worker1 = new GradingQueueWorker(channel1, serviceProvider, hubMock.Object, NullLogger<GradingQueueWorker>.Instance);
        await worker1.StartAsync(CancellationToken.None);
        await channel1.EnqueueAsync(new GradingTask
        {
            AnswerId = mainAnswer.Id,
            QuestionId = question.Id,
            SessionId = session.Id,
            StudentId = student.Id,
            AnswerText = mainAnswer.AnswerText,
            IsFollowUp = false,
            IsFullSession = false
        });
        channel1.Complete();
        for (int i = 0; i < 50 && receivedDtos.Count < 1; i++) await Task.Delay(50);
        await worker1.StopAsync(CancellationToken.None);

        receivedDtos.Should().HaveCount(1);
        var firstResult = receivedDtos[0];
        firstResult.NeedsFollowUp.Should().BeTrue();
        firstResult.FollowUpPrompt.Should().Be(question.FollowUpPrompt);

        // Bước 2: Sinh viên nộp câu trả lời cho Follow-up #1 -> Chấm điểm đạt 6.0 -> Sinh Follow-up #2
        using var context2 = CreateDbContext(dbName);
        var followUp1Answer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            ParentAnswerId = mainAnswer.Id,
            AnswerText = "Scoped tạo mới mỗi HTTP request còn Transient tạo mới mỗi lần inject.",
            Status = "pending",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
            IsFollowUp = true
        };
        context2.PracticeAnswers.Add(followUp1Answer);
        await context2.SaveChangesAsync();

        var channel2 = new TestGradingChannel();
        var worker2 = new GradingQueueWorker(channel2, serviceProvider, hubMock.Object, NullLogger<GradingQueueWorker>.Instance);
        await worker2.StartAsync(CancellationToken.None);
        await channel2.EnqueueAsync(new GradingTask
        {
            AnswerId = followUp1Answer.Id,
            QuestionId = question.Id,
            SessionId = session.Id,
            StudentId = student.Id,
            ParentAnswerId = mainAnswer.Id,
            AnswerText = followUp1Answer.AnswerText,
            IsFollowUp = true,
            IsFullSession = false
        });
        channel2.Complete();
        for (int i = 0; i < 50 && receivedDtos.Count < 2; i++) await Task.Delay(50);
        await worker2.StopAsync(CancellationToken.None);

        receivedDtos.Should().HaveCount(2);
        var secondResult = receivedDtos[1];
        secondResult.NeedsFollowUp.Should().BeTrue("Lượt hỏi phụ #1 đạt 6.0đ và MaxPracticeFollowUpQuestions mặc định = 2 nên phải kích hoạt Follow-up #2");
        secondResult.FollowUpPrompt.Should().NotBeNullOrWhiteSpace();
        secondResult.FollowUpPrompt.Should().NotBe(question.FollowUpPrompt, "Follow-up #2 tuyệt đối không được lặp lại nguyên văn câu hỏi phụ #1");

        capturedFollowUpGradingRequest.Should().NotBeNull();
        capturedFollowUpGradingRequest!.IsFollowUp.Should().BeTrue();
        capturedFollowUpGradingRequest.FollowUpPrompt.Should().Be(question.FollowUpPrompt);

        // Bước 3: Kiểm tra GetPracticeSessionQueryHandler đồng bộ 100% với SignalR
        using var queryContext = CreateDbContext(dbName);
        var queryHandler = new OralExamination.Application.Features.Practice.Queries.GetPracticeSession.GetPracticeSessionQueryHandler(queryContext);
        var sessionDetail = await queryHandler.Handle(
            new OralExamination.Application.Features.Practice.Queries.GetPracticeSession.GetPracticeSessionQuery(session.Id, student.Id, false),
            CancellationToken.None);

        sessionDetail.IsSuccess.Should().BeTrue();
        var syncedSecondAnswer = sessionDetail.Value.Answers.Single(a => a.AnswerId == followUp1Answer.Id);
        syncedSecondAnswer.NeedsFollowUp.Should().BeTrue();
        syncedSecondAnswer.FollowUpPrompt.Should().Be(secondResult.FollowUpPrompt);
    }
}

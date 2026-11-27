using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.MockExams.Commands;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.Common;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.MockExams.Queries.ResumeMockExamSession;
using OralExamination.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using OralExamination.Application.Features.Notifications.Commands.MarkNotificationRead;
using OralExamination.Application.Features.Notifications.DTOs;
using OralExamination.Application.Features.Notifications.Queries.GetNotifications;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using OralExamination.UnitTests.TestSupport;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class P2HardeningMf01Mf02AndCoreStabilityTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static ControllerContext CreateUserContext(Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // =========================================================================
    // 1. P2-01 — GetMockExamHistory Pagination Clamping & DoS Guard
    // =========================================================================

    [Fact(DisplayName = "P2-01: GetMockExamHistory kẹp an toàn PageIndex âm/overflow và PageSize <= 0 hoặc > 100")]
    public async Task P2_01_GetMockExamHistory_ClampsInvalidPaginationWithoutCrashOrMemoryDos()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET 8 Web API", Credits = 3 };
        context.Courses.Add(course);

        for (var i = 0; i < 105; i++)
        {
            context.MockExamSessions.Add(new MockExamSession
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseId = course.Id,
                StartedAt = DateTime.UtcNow.AddMinutes(-120 - i),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-90 - i),
                Status = "graded",
                TotalScore = 8.0m
            });
        }
        await context.SaveChangesAsync();

        var handler = new GetMockExamHistoryQueryHandler(context);

        // Negative PageIndex & 0 PageSize -> clamped to PageIndex=1, PageSize=20
        var negativeRes = await handler.Handle(
            new GetMockExamHistoryQuery(studentId, PageIndex: -5, PageSize: 0),
            CancellationToken.None);
        negativeRes.IsSuccess.Should().BeTrue();
        negativeRes.Value.PageIndex.Should().Be(1);
        negativeRes.Value.PageSize.Should().Be(20);
        negativeRes.Value.TotalCount.Should().Be(105);
        negativeRes.Value.TotalPages.Should().Be(6);
        negativeRes.Value.Items.Should().HaveCount(20);

        // int.MinValue boundary -> no overflow / divide-by-zero
        var minValueRes = await handler.Handle(
            new GetMockExamHistoryQuery(studentId, PageIndex: int.MinValue, PageSize: int.MinValue),
            CancellationToken.None);
        minValueRes.IsSuccess.Should().BeTrue();
        minValueRes.Value.PageIndex.Should().Be(1);
        minValueRes.Value.PageSize.Should().Be(20);
        minValueRes.Value.Items.Should().HaveCount(20);

        // Massive PageSize = 100_000 -> clamped to 100 (chặn Memory DoS)
        var hugePageSizeRes = await handler.Handle(
            new GetMockExamHistoryQuery(studentId, PageIndex: 1, PageSize: 100_000),
            CancellationToken.None);
        hugePageSizeRes.IsSuccess.Should().BeTrue();
        hugePageSizeRes.Value.PageIndex.Should().Be(1);
        hugePageSizeRes.Value.PageSize.Should().Be(100);
        hugePageSizeRes.Value.TotalPages.Should().Be(2);
        hugePageSizeRes.Value.Items.Should().HaveCount(100);

        // int.MaxValue PageIndex -> no negative Skip overflow
        var maxPageIndexRes = await handler.Handle(
            new GetMockExamHistoryQuery(studentId, PageIndex: int.MaxValue, PageSize: 50),
            CancellationToken.None);
        maxPageIndexRes.IsSuccess.Should().BeTrue();
        maxPageIndexRes.Value.Items.Should().BeEmpty();
    }

    // =========================================================================
    // 2. P2-02 & MF-02 — Infinite Pause Exploit, Paused 409, Slow STT Race & Controller
    // =========================================================================

    [Fact(DisplayName = "P2-02A: ResumeMockExam chặn exploit pause nhiều ngày vượt trần 15 phút và đóng ca hết giờ sạch")]
    public async Task P2_02_ResumeMockExam_MultiDayPauseExceedingCeiling_ExpiresCleanlyInsteadOfReopening()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            MockExamDurationMinutes = 30,
            IsActive = true
        };
        context.Courses.Add(course);

        var startedAt = DateTime.UtcNow.AddDays(-3);
        var sessionNoAnswers = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = startedAt,
            ExpiresAt = startedAt.AddMinutes(30),
            PausedAt = startedAt.AddMinutes(5), // Tạm dừng 3 ngày trước khi còn 25 phút
            Status = "in_progress"
        };

        var sessionWithAnswer = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = startedAt,
            ExpiresAt = startedAt.AddMinutes(30),
            PausedAt = startedAt.AddMinutes(10),
            Status = "in_progress"
        };
        sessionWithAnswer.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = sessionWithAnswer.Id,
            QuestionId = Guid.NewGuid(),
            OrderIndex = 1,
            AnswerText = "Đã trả lời trước khi bỏ máy 3 ngày.",
            Status = "answered"
        });

        context.MockExamSessions.AddRange(sessionNoAnswers, sessionWithAnswer);
        await context.SaveChangesAsync();

        var handler = new ResumeMockExamSessionQueryHandler(context);

        var resEmpty = await handler.Handle(new ResumeMockExamSessionQuery(sessionNoAnswers.Id, studentId), CancellationToken.None);
        resEmpty.IsSuccess.Should().BeFalse();
        resEmpty.Error.Should().StartWith("SESSION_EXPIRED:");

        var savedEmpty = await context.MockExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionNoAnswers.Id);
        savedEmpty.PausedAt.Should().BeNull();
        savedEmpty.ExpiresAt.Should().Be(startedAt.AddMinutes(30 + MockExamClockHelper.MaxPauseExtensionMinutes));
        savedEmpty.Status.Should().Be("graded");
        savedEmpty.TotalScore.Should().Be(0.0m);

        var resAnswered = await handler.Handle(new ResumeMockExamSessionQuery(sessionWithAnswer.Id, studentId), CancellationToken.None);
        resAnswered.IsSuccess.Should().BeFalse();
        resAnswered.Error.Should().StartWith("SESSION_EXPIRED:");

        var savedAnswered = await context.MockExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionWithAnswer.Id);
        savedAnswered.PausedAt.Should().BeNull();
        savedAnswered.ExpiresAt.Should().Be(startedAt.AddMinutes(30 + MockExamClockHelper.MaxPauseExtensionMinutes));
        savedAnswered.Status.Should().Be("submitted");
    }

    [Fact(DisplayName = "P2-02B: Pause-Resume nhiều lần bị kẹp đúng mốc trần StartedAt + Base + 15p và không phình DurationMinutes")]
    public async Task P2_02_ResumeMockExam_MultiplePauseCycles_ClampsAtCeiling_AndPreservesBaseDurationMinutes()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = ".NET 8",
            MockExamDurationMinutes = 30,
            IsActive = true
        };
        context.Courses.Add(course);

        var now = DateTime.UtcNow;
        var startedAt = now.AddMinutes(-25);
        var maxAllowedExpiresAt = startedAt.AddMinutes(30 + MockExamClockHelper.MaxPauseExtensionMinutes);

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = startedAt,
            ExpiresAt = startedAt.AddMinutes(30),
            PausedAt = now.AddMinutes(-10), // Chu kỳ 1: tạm dừng 10 phút
            Status = "in_progress"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var resumeHandler = new ResumeMockExamSessionQueryHandler(context);
        var pauseHandler = new PauseMockExamSessionCommandHandler(context);

        // Resume lần 1 (+10 phút -> ExpiresAt = StartedAt + 40m < trần 45m)
        var firstResume = await resumeHandler.Handle(new ResumeMockExamSessionQuery(session.Id, studentId), CancellationToken.None);
        firstResume.IsSuccess.Should().BeTrue();
        firstResume.Value.DurationMinutes.Should().Be(30, "DurationMinutes không được phình theo thời gian pause");
        firstResume.Value.ExpiresAt.Should().BeCloseTo(startedAt.AddMinutes(40), TimeSpan.FromSeconds(5));

        // Chu kỳ 2: giả lập tạm dừng thêm 10 phút nữa (tổng 20 phút > trần 15 phút)
        var tracked = await context.MockExamSessions.SingleAsync(s => s.Id == session.Id);
        tracked.PausedAt = DateTime.UtcNow.AddMinutes(-10);
        await context.SaveChangesAsync();

        var secondResume = await resumeHandler.Handle(new ResumeMockExamSessionQuery(session.Id, studentId), CancellationToken.None);
        secondResume.IsSuccess.Should().BeTrue();
        secondResume.Value.DurationMinutes.Should().Be(30);
        secondResume.Value.ExpiresAt.Should().Be(maxAllowedExpiresAt, "ExpiresAt tuyệt đối không vượt trần StartedAt + base + 15m");

        // Khi đã chạm trần cộng bù 15 phút, bấm pause thêm phải bị từ chối (CONFLICT -> 409)
        var thirdPause = await pauseHandler.Handle(new PauseMockExamSessionCommand(session.Id, studentId), CancellationToken.None);
        thirdPause.IsSuccess.Should().BeFalse();
        thirdPause.Error.Should().StartWith("CONFLICT:");

        // M1-MF02-02: Phân biệt NOT_FOUND / FORBIDDEN / CONFLICT (completed) / SESSION_EXPIRED
        var missingRes = await pauseHandler.Handle(new PauseMockExamSessionCommand(Guid.NewGuid(), studentId), CancellationToken.None);
        missingRes.IsSuccess.Should().BeFalse();
        missingRes.Error.Should().StartWith("NOT_FOUND:");

        var forbiddenRes = await pauseHandler.Handle(new PauseMockExamSessionCommand(session.Id, Guid.NewGuid()), CancellationToken.None);
        forbiddenRes.IsSuccess.Should().BeFalse();
        forbiddenRes.Error.Should().StartWith("FORBIDDEN:");

        var expiredSession = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = DateTime.UtcNow.AddHours(-3),
            ExpiresAt = DateTime.UtcNow.AddHours(-2),
            Status = "in_progress"
        };
        var completedSession = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = "graded"
        };
        context.MockExamSessions.AddRange(expiredSession, completedSession);
        await context.SaveChangesAsync();

        var expiredRes = await pauseHandler.Handle(new PauseMockExamSessionCommand(expiredSession.Id, studentId), CancellationToken.None);
        expiredRes.IsSuccess.Should().BeFalse();
        expiredRes.Error.Should().StartWith("SESSION_EXPIRED:");

        var completedRes = await pauseHandler.Handle(new PauseMockExamSessionCommand(completedSession.Id, studentId), CancellationToken.None);
        completedRes.IsSuccess.Should().BeFalse();
        completedRes.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "P2-02C: SubmitMockExamAnswer khi ca đang tạm dừng trả CONFLICT (HTTP 409) thay vì 410 Gone")]
    public async Task P2_02_SubmitMockExamAnswer_WhenPaused_ReturnsConflictPrefix_AndControllerMapsTo409()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        context.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = ".NET", IsActive = true });
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(25),
            PausedAt = DateTime.UtcNow.AddMinutes(-1),
            Status = "in_progress"
        };
        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionId,
            Status = "pending"
        });
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);
        using var audio = new MemoryStream(Encoding.UTF8.GetBytes("voice-payload"));

        var result = await handler.Handle(new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audio,
            FileName: "ans.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 15), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<SubmitMockExamAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var controller = new MockExamsController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(studentId, "student")
        };

        var actionResult = await controller.SubmitAnswer(
            session.Id,
            new SubmitMockExamAnswerFormRequest { QuestionId = questionId, TimeTakenSeconds = 15 },
            CancellationToken.None);

        var conflict = actionResult.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "P2-02D: Race SubmitMockExamAnswer chạy STT chậm không ghi đè vào ca thi đã nộp/chấm trong lúc chờ STT")]
    public async Task P2_02_SubmitMockExamAnswer_SlowSttRace_RejectsWriteIfSessionSubmittedDuringStt()
    {
        var dbName = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var answerId = Guid.NewGuid();

        using (var setup = CreateDbContext(dbName))
        {
            setup.Courses.Add(new Course { Id = courseId, Code = "SWD392", Name = "Architecture", IsActive = true });
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddMinutes(20),
                Status = "in_progress"
            };
            session.Answers.Add(new MockExamAnswer
            {
                Id = answerId,
                SessionId = sessionId,
                QuestionId = questionId,
                Status = "pending",
                AnswerText = null
            });
            setup.MockExamSessions.Add(session);
            await setup.SaveChangesAsync();
        }

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Mô phỏng request nộp toàn phiên chạy song song trên connection khác và hoàn tất chấm điểm trong khi STT đang chạy
                using var concurrentCtx = CreateDbContext(dbName);
                var dbSession = await concurrentCtx.MockExamSessions.SingleAsync(s => s.Id == sessionId);
                dbSession.Status = "graded";
                dbSession.TotalScore = 0.0m;
                dbSession.SubmittedAt = DateTime.UtcNow;
                await concurrentCtx.SaveChangesAsync();

                return new SpeechTranscriptionResult(
                    Text: "Câu trả lời đến muộn sau khi đã nộp bài",
                    RawText: "Câu trả lời đến muộn sau khi đã nộp bài",
                    Provider: "CloudflareWhisper",
                    UsedFallback: false,
                    ConfidenceScore: 0.95);
            });

        using var answerCtx = CreateDbContext(dbName);
        var handler = new SubmitMockExamAnswerCommandHandler(answerCtx, sttMock.Object);
        using var audio = new MemoryStream(Encoding.UTF8.GetBytes("slow-stt-audio-bytes"));

        var result = await handler.Handle(new SubmitMockExamAnswerCommand(
            SessionId: sessionId,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audio,
            FileName: "late.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 40), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");

        using var verifyCtx = CreateDbContext(dbName);
        var persistedAnswer = await verifyCtx.MockExamAnswers.AsNoTracking().SingleAsync(a => a.Id == answerId);
        persistedAnswer.AnswerText.Should().BeNull("Câu trả lời chạy STT chậm tuyệt đối không được ghi đè sau khi ca đã graded");
        persistedAnswer.Status.Should().Be("pending");
    }

    [Fact(DisplayName = "P2-02E: MockExamsController trả 429 ProblemDetails kèm Extensions[errorCode]=QUOTA_EXCEEDED và truyền CancellationToken")]
    public async Task P2_02_MockExamsController_Quota429IncludesErrorCodeExtension_AndForwardsCancellationToken()
    {
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<StartMockExamCommand>(), token))
            .ReturnsAsync(Result<StartMockExamResponse>.Failure("QUOTA_EXCEEDED:Đã hết 3/3 lượt thi thử hôm nay."));
        senderMock.Setup(s => s.Send(It.IsAny<GetMockExamQuotaQuery>(), token))
            .ReturnsAsync(Result<MockExamQuotaDto>.Success(new MockExamQuotaDto(courseId, studentId, DateOnly.FromDateTime(DateTime.UtcNow), 3, 0, 3, false)));
        senderMock.Setup(s => s.Send(It.IsAny<GetMockExamHistoryQuery>(), token))
            .ReturnsAsync(Result<MockExamHistoryListResponse>.Success(new MockExamHistoryListResponse([], 1, 10, 0, 0)));
        senderMock.Setup(s => s.Send(It.IsAny<SubmitMockExamSessionCommand>(), token))
            .ReturnsAsync(Result<MockExamScorecardDto>.Failure("Ca thi không tồn tại."));
        senderMock.Setup(s => s.Send(It.IsAny<GetMockExamSessionDetailQuery>(), token))
            .ReturnsAsync(Result<MockExamScorecardDto>.Failure("Không tìm thấy thông tin ca thi thử."));

        var controller = new MockExamsController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(studentId, "student")
        };

        var startRes = await controller.StartSession(new StartMockExamRequest(courseId, false), token);
        var obj = startRes.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        var problem = obj.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problem.Extensions.Should().ContainKey("errorCode");
        problem.Extensions["errorCode"].Should().Be("QUOTA_EXCEEDED");

        await controller.GetQuota(courseId, null, token);
        await controller.GetHistory(courseId, 1, 10, null, null, token);
        await controller.SubmitSession(sessionId, token);
        await controller.GetHistoryDetail(sessionId, null, token);

        senderMock.Verify(s => s.Send(It.IsAny<StartMockExamCommand>(), token), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<GetMockExamQuotaQuery>(), token), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<GetMockExamHistoryQuery>(), token), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<SubmitMockExamSessionCommand>(), token), Times.Once);
        senderMock.Verify(s => s.Send(It.IsAny<GetMockExamSessionDetailQuery>(), token), Times.Once);
    }

    // =========================================================================
    // 3. P2-03 — SubmitMockExamSession Serialization Without Static RAM & No Downgrade
    // =========================================================================

    [Fact(DisplayName = "P2-03A: SubmitMockExamSession bỏ ActiveGradingSessions tĩnh RAM, dùng ExecuteMockExamGradingAsync và không giữ lock khi gọi LLM")]
    public async Task P2_03_SubmitMockExamSession_UsesDbSerializationLockOutsideLlm_WithoutStaticRamDictionary()
    {
        // Đảm bảo không còn static ConcurrentDictionary ActiveGradingSessions trong handler
        var staticFields = typeof(SubmitMockExamSessionCommandHandler)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        staticFields.Should().NotContain(f => f.Name.Contains("ActiveGradingSessions", StringComparison.OrdinalIgnoreCase));

        using var innerContext = CreateDbContext();
        var spyContext = new LockTrackingDbContextSpy(innerContext);
        var studentId = Guid.NewGuid();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", Credits = 3 };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10.0m };
        var crit = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10.0m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain Clean Architecture",
            SampleAnswer = "Domain at the center",
            Difficulty = "easy",
            BloomLevel = "Understand"
        };
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            Status = "in_progress"
        };
        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            OrderIndex = 1,
            AnswerText = "Domain is independent of Infrastructure.",
            Status = "answered"
        });

        innerContext.Courses.Add(course);
        innerContext.Rubrics.Add(rubric);
        innerContext.RubricCriteria.Add(crit);
        innerContext.PracticeQuestions.Add(question);
        innerContext.MockExamSessions.Add(session);
        await innerContext.SaveChangesAsync();

        var lockHeldDuringLlm = false;
        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                lockHeldDuringLlm = spyContext.IsInsideMockExamGradingLock;
                return new AiGradingResult
                {
                    Score = 8.5m,
                    ConfidenceScore = 0.95m,
                    Feedback = "Tốt",
                    Details = [new() { CriterionId = crit.Id.ToString(), Score = 8.5m, Comment = "Đạt" }]
                };
            });

        var handler = new SubmitMockExamSessionCommandHandler(spyContext, aiMock.Object);
        var result = await handler.Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        spyContext.MockExamGradingLockCalls.Should().Be(2, "Phải gọi ExecuteMockExamGradingAsync cho Pha Claim và Pha Commit");
        lockHeldDuringLlm.Should().BeFalse("Tuyệt đối không được giữ PostgreSQL lock trong suốt lúc gọi LLM");
    }

    [Fact(DisplayName = "P2-03B: Lỗi AI tạm thời sau khi luồng khác đã commit graded tuyệt đối không hạ ngược trạng thái về submitted")]
    public async Task P2_03_SubmitMockExamSession_TransientFailureAfterConcurrentCommit_NeverDowngradesGradedSession()
    {
        var dbName = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        using (var setup = CreateDbContext(dbName))
        {
            var course = new Course { Id = courseId, Code = "SWD392", Name = "Architecture", Credits = 3 };
            var rubric = new Rubric { Id = rubricId, CourseId = courseId, Name = "R", TotalMaxScore = 10.0m };
            var crit = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubricId, CriterionName = "Core", MaxScore = 10.0m, Weight = 1.0m, OrderIndex = 1 };
            var question = new PracticeQuestion
            {
                Id = questionId,
                CourseId = courseId,
                RubricId = rubricId,
                Title = "Q1",
                Content = "Explain CQRS",
                SampleAnswer = "Separate read and write models",
                Difficulty = "medium",
                BloomLevel = "Understand"
            };
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                StartedAt = DateTime.UtcNow.AddMinutes(-15),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Status = "in_progress"
            };
            session.Answers.Add(new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                QuestionId = questionId,
                OrderIndex = 1,
                AnswerText = "CQRS separates commands and queries.",
                Status = "answered"
            });

            setup.Courses.Add(course);
            setup.Rubrics.Add(rubric);
            setup.RubricCriteria.Add(crit);
            setup.PracticeQuestions.Add(question);
            setup.MockExamSessions.Add(session);
            await setup.SaveChangesAsync();
        }

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AiGradingRequest, CancellationToken>(async (_, _) =>
            {
                // Luồng khác hoàn tất chấm và chốt "graded" trước khi luồng này gặp lỗi tạm thời
                using var otherCtx = CreateDbContext(dbName);
                var committed = await otherCtx.MockExamSessions.SingleAsync(s => s.Id == sessionId);
                committed.Status = "graded";
                committed.TotalScore = 9.0m;
                await otherCtx.SaveChangesAsync();

                throw new InvalidOperationException("Simulated transient 503 after concurrent commit");
            });

        using var slowCtx = CreateDbContext(dbName);
        var handler = new SubmitMockExamSessionCommandHandler(slowCtx, aiMock.Object);
        var result = await handler.Handle(new SubmitMockExamSessionCommand(sessionId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");

        using var verifyCtx = CreateDbContext(dbName);
        var finalSession = await verifyCtx.MockExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        finalSession.Status.Should().Be("graded", "Tuyệt đối không hạ ngược phiên đã graded về submitted");
        finalSession.TotalScore.Should().Be(9.0m);
        (await verifyCtx.DeadLetterQueues.CountAsync(q => q.CorrelationId == sessionId)).Should().Be(0);
    }

    // =========================================================================
    // 4. P2-04, P2-05 & MF-01 — NextQuestion Serialization, 409 vs 410, Per-Question Progressive & PracticeSession
    // =========================================================================

    [Fact(DisplayName = "P2-04: NextQuestionCommandHandler tuần tự hóa bốc câu hỏi qua ExecutePracticeSessionMutationAsync")]
    public async Task P2_04_NextQuestion_SerializesMutationViaExecutePracticeSessionMutationAsync()
    {
        using var innerContext = CreateDbContext();
        var spyContext = new LockTrackingDbContextSpy(innerContext);
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Testing", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.p204@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Unit testing vs Integration testing",
            Difficulty = "easy",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = PracticeSession.SerializeDifficulties(["easy"])
        };

        innerContext.Courses.Add(course);
        innerContext.Users.Add(student);
        innerContext.Rubrics.Add(rubric);
        innerContext.PracticeQuestions.Add(question);
        innerContext.PracticeSessions.Add(session);
        await innerContext.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(spyContext);
        var result = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasMoreQuestions.Should().BeTrue();
        result.Value.Question!.Id.Should().Be(question.Id);
        spyContext.PracticeMutationLockSessions.Should().ContainSingle().Which.Should().Be(session.Id);
    }

    [Fact(DisplayName = "P2-05: Phiên Practice đã completed quá 5 phút trả CONFLICT (409) trên NextQuestion/SubmitAnswer thay vì 410 Gone")]
    public async Task P2_05_CompletedPracticeSessionOlderThanTimeout_ReturnsConflict409InsteadOf410()
    {
        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWT301", Name = "Testing", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.p205@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10m };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain Boundary Value Analysis",
            Difficulty = "easy",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var completedSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            PracticeMode = "per_question",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-20),
            CompletedAt = DateTime.UtcNow.AddMinutes(-20),
            SelectedDifficulties = PracticeSession.SerializeDifficulties(["easy"])
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.PracticeSessions.Add(completedSession);
        await context.SaveChangesAsync();

        var nextRes = await new NextQuestionCommandHandler(context)
            .Handle(new NextQuestionCommand(completedSession.Id, student.Id), CancellationToken.None);
        nextRes.IsSuccess.Should().BeFalse();
        nextRes.Error.Should().StartWith("CONFLICT:");
        nextRes.Error.Should().NotContain("SESSION_TIMEOUT_410");

        var submitRes = await new SubmitPracticeAnswerCommandHandler(context, new Mock<IGradingQueueChannel>().Object)
            .Handle(new SubmitPracticeAnswerCommand(completedSession.Id, question.Id, student.Id, "Late answer", false, null), CancellationToken.None);
        submitRes.IsSuccess.Should().BeFalse();
        submitRes.Error.Should().StartWith("CONFLICT:");
        submitRes.Error.Should().NotContain("SESSION_TIMEOUT_410");

        // PracticeController.GetNextQuestion phải ánh xạ CONFLICT -> HTTP 409 Conflict
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<NextQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nextRes);
        var controller = new PracticeController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(student.Id, "student")
        };

        var httpRes = await controller.GetNextQuestion(completedSession.Id);
        var objRes = httpRes.Should().BeOfType<ObjectResult>().Subject;
        objRes.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "MF-01: Chặn tạo phiên practice mode=per_question với difficulty=progressive ở Validator, Handler và Controller")]
    public async Task MF01_StartPracticeSession_RejectsPerQuestionWithProgressive()
    {
        var validator = new StartPracticeSessionCommandValidator();
        var invalidCmd = new StartPracticeSessionCommand(
            StudentId: Guid.NewGuid(),
            CourseId: Guid.NewGuid(),
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: false,
            Topic: null,
            Difficulties: null,
            PracticeMode: "per_question");

        var validation = validator.Validate(invalidCmd);
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.ErrorMessage.Contains("VALIDATION:"));

        using var context = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET 8", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.prog@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        context.Courses.Add(course);
        context.Users.Add(student);
        await context.SaveChangesAsync();
        await StudentEnrollmentFixture.EnrollAsync(context, course.Id, student.Id);

        var handler = new StartPracticeSessionCommandHandler(context);
        var handlerRes = await handler.Handle(invalidCmd with { StudentId = student.Id, CourseId = course.Id }, CancellationToken.None);
        handlerRes.IsSuccess.Should().BeFalse();
        handlerRes.Error.Should().StartWith("VALIDATION:");
        (await context.PracticeSessions.CountAsync()).Should().Be(0);

        // Controller truyền PracticeMode vào StartPracticeSessionCommand và trả 400 BadRequest
        StartPracticeSessionCommand? captured = null;
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<StartPracticeSessionResponse>>, CancellationToken>((cmd, _) => captured = (StartPracticeSessionCommand)cmd)
            .ReturnsAsync(handlerRes);

        var controller = new PracticeController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(student.Id, "student")
        };

        var httpRes = await controller.StartSession(new StartPracticeSessionRequest(
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: false,
            PracticeMode: "per_question"));

        captured.Should().NotBeNull();
        captured!.PracticeMode.Should().Be("per_question");
        httpRes.Should().BeOfType<BadRequestObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "MF-01: PracticeSession xử lý JsonException tường minh trên JSON lỗi mà không crash và giữ tương thích qid:")]
    public void MF01_PracticeSession_HandlesMalformedJsonViaJsonExceptionAndPreservesLegacyQidCompatibility()
    {
        var corrupted = new PracticeSession
        {
            SelectedQuestionIdsJson = "{not-valid-json",
            SelectedDifficulties = "easy, hard"
        };

        corrupted.SelectedQuestionIds.Should().BeEmpty();
        corrupted.DifficultiesList.Should().Equal("easy", "hard");

        var legacyQid = Guid.NewGuid();
        var legacy = new PracticeSession
        {
            SelectedQuestionIdsJson = "[]",
            SelectedDifficulties = PracticeSession.SerializeDifficultiesAndQuestions(["medium"], [legacyQid])
        };

        legacy.DifficultiesList.Should().Equal("medium");
        legacy.SelectedQuestionIds.Should().ContainSingle().Which.Should().Be(legacyQid);
    }

    // =========================================================================
    // 5. Hardening 🟡 — Courses JSONB/404, AcademicClasses Enrolled Count, Notifications 403/404, TTS Byte Budget
    // =========================================================================

    [Theory(DisplayName = "Hardening 🟡: UpdateCourseConfiguration chặn MockExamBloomDistribution JSONB sai định dạng, key ngoài BloomLevel, trùng key, âm hoặc tổng = 0")]
    [InlineData("{invalid-json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("{\"Remember\": -1, \"Understand\": 2}")]
    [InlineData("{\"Remember\": \"high\"}")]
    [InlineData("{\"Remember\": 0, \"Understand\": 0}")]
    [InlineData("{\"UnknownLevel\": 2}")]
    [InlineData("{\"Remember\": 1, \"remember\": 2}")]
    public async Task Hardening_UpdateCourseConfiguration_RejectsInvalidBloomDistributionJsonb(string invalidBloomJson)
    {
        using var context = CreateDbContext();
        var headId = Guid.NewGuid();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            DepartmentHeadUserId = headId,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            MockExamBloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}",
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var result = await handler.Handle(new UpdateCourseConfigurationCommand(course.Id, 90, 2, true)
        {
            ActorRole = $"department_head:{headId}",
            MockExamBloomDistribution = invalidBloomJson
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");

        var persisted = await context.Courses.AsNoTracking().SingleAsync(c => c.Id == course.Id);
        persisted.MockExamBloomDistribution.Should().Be("{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}");
        persisted.TranscriptBufferSeconds.Should().Be(60, "Không được ghi nửa vời khi Bloom JSON không hợp lệ");
    }

    [Fact(DisplayName = "Hardening 🟡: CoursesController.UpdateConfiguration ánh xạ NOT_FOUND và 'không tồn tại' sang 404 ProblemDetails")]
    public async Task Hardening_CoursesController_UpdateConfiguration_MapsNotFoundTo404ProblemDetails()
    {
        var courseId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = new CoursesController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(headId, "department_head")
        };

        var request = new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = true
        };

        foreach (var error in new[] { "NOT_FOUND: Môn học không tồn tại trong hệ thống.", "Môn học không tồn tại trong hệ thống." })
        {
            senderMock.Setup(s => s.Send(It.IsAny<UpdateCourseConfigurationCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<CourseConfigurationDto>.Failure(error));

            var actionResult = await controller.UpdateConfiguration(courseId, request);
            var notFound = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            notFound.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status404NotFound);
        }
    }

    [Fact(DisplayName = "Hardening 🟡: GetAcademicClasses chỉ đếm sĩ số sinh viên có Status == 'enrolled', loại dropped và removed")]
    public async Task Hardening_GetAcademicClasses_CountsOnlyEnrolledStudents()
    {
        using var context = CreateDbContext();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin.cls@fpt.edu.vn", FullName = "Admin", Role = UserRole.Admin, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET 8", SemesterId = semester.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SE1801", CourseId = course.Id, SemesterId = semester.Id, MaxStudents = 35, IsActive = true };

        cls.Enrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = Guid.NewGuid(), Status = "enrolled" });
        cls.Enrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = Guid.NewGuid(), Status = "enrolled" });
        cls.Enrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = Guid.NewGuid(), Status = "dropped" });
        cls.Enrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = Guid.NewGuid(), Status = "removed" });

        context.Users.Add(admin);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Classes.Add(cls);
        await context.SaveChangesAsync();

        var handler = new GetAcademicClassesQueryHandler(context);
        var result = await handler.Handle(
            new GetAcademicClassesQuery(course.Id, semester.Id, true, null, admin.Id, UserRole.Admin),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].EnrolledStudentsCount.Should().Be(2);
    }

    [Fact(DisplayName = "Hardening 🟡: NotificationsController ánh xạ FORBIDDEN -> 403 và NOT_FOUND -> 404 RFC 7807 ProblemDetails")]
    public async Task Hardening_NotificationsController_MapsForbiddenAndNotFoundTo403And404()
    {
        var userId = Guid.NewGuid();
        var notiId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var controller = new NotificationsController(senderMock.Object)
        {
            ControllerContext = CreateUserContext(userId, "student")
        };

        senderMock.Setup(s => s.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("FORBIDDEN: Không thuộc về bạn."));
        var forbidden = (await controller.MarkRead(notiId, CancellationToken.None)).Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        forbidden.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status403Forbidden);

        senderMock.Setup(s => s.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("NOT_FOUND: Thông báo không tồn tại."));
        var notFound = (await controller.MarkRead(notiId, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        notFound.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(StatusCodes.Status404NotFound);

        senderMock.Setup(s => s.Send(It.IsAny<MarkAllNotificationsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("FORBIDDEN: Không được phép."));
        var markAllForbidden = (await controller.MarkAllRead(CancellationToken.None)).Should().BeOfType<ObjectResult>().Subject;
        markAllForbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "Hardening 🟡: ResilientTextToSpeechService ép trần RAM 64MB và đuổi mục cũ nhất khi vượt trần byte")]
    public async Task Hardening_ResilientTextToSpeechService_EnforcesByteBudgetAndEvictsOldestEntries()
    {
        ResilientTextToSpeechService.MaxSharedCacheTotalBytes.Should().Be(64L * 1024 * 1024);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Speech:MaxCacheTotalBytes"] = "10240" // 10 KB budget để kiểm chứng đuổi FIFO tức thì
            })
            .Build();

        var synthCalls = 0;
        var payload4Kb = new byte[4096];
        Array.Fill(payload4Kb, (byte)0x55);
        var payload12Kb = new byte[12288];
        Array.Fill(payload12Kb, (byte)0x77);

        using var httpClient = new HttpClient();
        var service = new ResilientTextToSpeechService(
            httpClient,
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, _, _, _) =>
            {
                Interlocked.Increment(ref synthCalls);
                return Task.FromResult(text.Contains("oversized", StringComparison.OrdinalIgnoreCase) ? payload12Kb : payload4Kb);
            });

        var r1 = await service.SynthesizeSpeechAsync("Câu hỏi thứ nhất về Clean Architecture", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        r1.FromCache.Should().BeFalse();
        service.CurrentCacheTotalBytes.Should().Be(4096);
        service.CurrentCacheEntryCount.Should().Be(1);

        var r2 = await service.SynthesizeSpeechAsync("Câu hỏi thứ hai về CQRS và MediatR", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        r2.FromCache.Should().BeFalse();
        service.CurrentCacheTotalBytes.Should().Be(8192);
        service.CurrentCacheEntryCount.Should().Be(2);

        // Mục thứ 3 (4KB) khiến tổng = 12KB > 10KB -> bắt buộc đuổi mục cũ nhất (r1)
        var r3 = await service.SynthesizeSpeechAsync("Câu hỏi thứ ba về PostgreSQL Indexing", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        r3.FromCache.Should().BeFalse();
        service.CurrentCacheTotalBytes.Should().Be(8192);
        service.CurrentCacheEntryCount.Should().Be(2);

        // r2 vẫn nằm trong cache (FromCache = true), còn r1 đã bị đuổi (FromCache = false)
        var r2Hit = await service.SynthesizeSpeechAsync("Câu hỏi thứ hai về CQRS và MediatR", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        r2Hit.FromCache.Should().BeTrue();

        var r1Miss = await service.SynthesizeSpeechAsync("Câu hỏi thứ nhất về Clean Architecture", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        r1Miss.FromCache.Should().BeFalse();
        service.CurrentCacheTotalBytes.Should().BeLessOrEqualTo(10240);

        // Payload đơn lẻ lớn hơn toàn bộ trần byte (12KB > 10KB) -> không lưu cache, giữ nguyên ngân sách RAM
        var oversized = await service.SynthesizeSpeechAsync("oversized audio payload test", ResilientTextToSpeechService.DefaultFemaleVoice, 1.0);
        oversized.FromCache.Should().BeFalse();
        oversized.AudioBytes.Length.Should().Be(12288);
        service.CurrentCacheTotalBytes.Should().BeLessOrEqualTo(10240);
        synthCalls.Should().Be(5);
    }

    // =========================================================================
    // 6. Remediation Panel A — A1 (ExamStructure Duration), A2 (Phase 3A Upsert + Stale Claim), A3 (History Lock)
    // =========================================================================

    [Fact(DisplayName = "Remediation A1: MF-02 (ExamSetId=null) dùng ExamStructure.DurationMinutes=60 (> Course=30) cho Pause, không cắt ExpiresAt khi F5 ở phút 46, và trả DurationMinutes=60 trên Scorecard/History")]
    public async Task Remediation_A1_MockExamClock_UsesActiveExamStructureDuration_NoTruncationOnF5_AndPreservesDurationOnScorecardAndHistory()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            MockExamDurationMinutes = 30,
            IsActive = true
        };
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Mock Structure 60m",
            TotalQuestions = 1,
            DurationMinutes = 60,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R-A1", TotalMaxScore = 10.0m };
        var crit = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10.0m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Explain Hexagonal Architecture",
            SampleAnswer = "Ports and Adapters isolate domain logic from infrastructure concerns.",
            Difficulty = "medium",
            BloomLevel = "Understand",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var now = DateTime.UtcNow;
        var startedAtMinute1 = now.AddMinutes(-1);
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ExamSetId = null,
            CourseId = course.Id,
            HasFollowUp = false,
            StartedAt = startedAtMinute1,
            ExpiresAt = startedAtMinute1.AddMinutes(60),
            Status = "in_progress"
        };
        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            OrderIndex = 1,
            AnswerText = "Hexagonal architecture separates core domain via ports and adapters.",
            Status = "answered"
        });

        context.Courses.Add(course);
        context.ExamStructures.Add(structure);
        context.Rubrics.Add(rubric);
        context.RubricCriteria.Add(crit);
        context.PracticeQuestions.Add(question);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        // 1. Pause ở phút thứ 1 phải thành công (không bị chặn bởi Course.MockExamDurationMinutes=30 + 15=45)
        var pauseHandler = new PauseMockExamSessionCommandHandler(context);
        var pauseResult = await pauseHandler.Handle(new PauseMockExamSessionCommand(session.Id, studentId), CancellationToken.None);
        pauseResult.IsSuccess.Should().BeTrue();
        pauseResult.Value.Should().BeGreaterThan(3400);

        // 2. Giả lập SV không pause mà chỉ F5/reload ở phút 46 (còn 14 phút): tuyệt đối không cắt ExpiresAt xuống 45p và không đóng sớm 410
        var startedAtMinute46 = DateTime.UtcNow.AddMinutes(-46);
        var expectedExpiresAt = startedAtMinute46.AddMinutes(60);
        var tracked = await context.MockExamSessions.SingleAsync(s => s.Id == session.Id);
        tracked.StartedAt = startedAtMinute46;
        tracked.ExpiresAt = expectedExpiresAt;
        tracked.PausedAt = null;
        await context.SaveChangesAsync();

        var resumeHandler = new ResumeMockExamSessionQueryHandler(context);
        var f5Res = await resumeHandler.Handle(new ResumeMockExamSessionQuery(session.Id, studentId), CancellationToken.None);
        f5Res.IsSuccess.Should().BeTrue(because: f5Res.Error);
        f5Res.Value.DurationMinutes.Should().Be(60);
        f5Res.Value.ExpiresAt.Should().Be(expectedExpiresAt, "F5/reload bình thường khi không pause tuyệt đối không được cắt xén ExpiresAt");

        // 3. Giả lập có cộng bù pause 10 phút (ExpiresAt = StartedAt + 70m <= trần 75m) -> Scorecard & History vẫn trả DurationMinutes = 60
        tracked = await context.MockExamSessions.SingleAsync(s => s.Id == session.Id);
        tracked.ExpiresAt = startedAtMinute46.AddMinutes(70);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.0m,
                ConfidenceScore = 0.92m,
                Feedback = "Tốt",
                Details = [new() { CriterionId = crit.Id.ToString(), Score = 8.0m, Comment = "Đạt" }]
            });

        var submitRes = await new SubmitMockExamSessionCommandHandler(context, aiMock.Object)
            .Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);
        submitRes.IsSuccess.Should().BeTrue(because: submitRes.Error);
        submitRes.Value.DurationMinutes.Should().Be(60);

        var historyRes = await new GetMockExamHistoryQueryHandler(context)
            .Handle(new GetMockExamHistoryQuery(studentId, course.Id, 1, 10), CancellationToken.None);
        historyRes.IsSuccess.Should().BeTrue(because: historyRes.Error);
        historyRes.Value.Items.Should().ContainSingle();
        historyRes.Value.Items[0].DurationMinutes.Should().Be(60);
    }

    [Fact(DisplayName = "Remediation A2: SubmitMockExamSession Pha 3A cập nhật tại chỗ AiEvaluation đã tồn tại theo MockExamAnswerId (không trùng 23505) và làm mới SubmittedAt khi claim stale")]
    public async Task Remediation_A2_SubmitMockExamSession_Phase3AUpsertsByMockExamAnswerId_AndRefreshesStaleClaimSubmittedAt()
    {
        var dbName = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var critId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var answerId = Guid.NewGuid();
        var preExistingEvalId = Guid.NewGuid();

        using (var setup = CreateDbContext(dbName))
        {
            var course = new Course { Id = courseId, Code = "SWD392", Name = "Architecture", Credits = 3, IsActive = true };
            var rubric = new Rubric { Id = rubricId, CourseId = courseId, Name = "R-A2", TotalMaxScore = 10.0m };
            var crit = new RubricCriterion { Id = critId, RubricId = rubricId, CriterionName = "Core", MaxScore = 10.0m, Weight = 1.0m, OrderIndex = 1 };
            var question = new PracticeQuestion
            {
                Id = questionId,
                CourseId = courseId,
                RubricId = rubricId,
                Title = "Q1",
                Content = "Explain CAP theorem",
                SampleAnswer = "Consistency, Availability, Partition tolerance trade-offs in distributed systems.",
                Difficulty = "hard",
                BloomLevel = "Analyze"
            };
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                StartedAt = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                // Giả lập phiên đang submitted bị kẹt quá 2 phút (stale claim)
                Status = "submitted",
                SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
                TotalScore = null
            };
            session.Answers.Add(new MockExamAnswer
            {
                Id = answerId,
                SessionId = sessionId,
                QuestionId = questionId,
                OrderIndex = 1,
                AnswerText = "Under network partition a distributed system trades off consistency and availability.",
                Status = "answered"
            });

            setup.Courses.Add(course);
            setup.Rubrics.Add(rubric);
            setup.RubricCriteria.Add(crit);
            setup.PracticeQuestions.Add(question);
            setup.MockExamSessions.Add(session);
            await setup.SaveChangesAsync();
        }

        Result<MockExamScorecardDto>? competingClaimResult = null;
        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns<AiGradingRequest, CancellationToken>(async (_, _) =>
            {
                // 1. Kiểm chứng stale claim refresh: luồng thứ 2 chạy song song ngay sau Pha 1 phải bị chặn CONFLICT (không thundering herd)
                using (var competingCtx = CreateDbContext(dbName))
                {
                    var competingHandler = new SubmitMockExamSessionCommandHandler(competingCtx, new Mock<IAiGradingService>().Object);
                    competingClaimResult = await competingHandler.Handle(
                        new SubmitMockExamSessionCommand(sessionId, studentId),
                        CancellationToken.None);
                }

                // 2. Mô phỏng partial-failure trước đó đã chèn sẵn 1 AiEvaluation cho cùng MockExamAnswerId trong DB trước khi Pha 3A chạy
                using (var partialCtx = CreateDbContext(dbName))
                {
                    partialCtx.AiEvaluations.Add(new AiEvaluation
                    {
                        Id = preExistingEvalId,
                        AnswerType = "mock_exam",
                        StudentId = studentId,
                        MockExamAnswerId = answerId,
                        TotalScore = 6.0m,
                        ConfidenceScore = 0.70m,
                        Feedback = "Bản chấm tạm trước partial failure",
                        CotTrace = "Old CoT",
                        EvaluatedAt = DateTime.UtcNow.AddMinutes(-3),
                        Details =
                        [
                            new AiEvaluationDetail
                            {
                                Id = Guid.NewGuid(),
                                EvaluationId = preExistingEvalId,
                                CriterionId = critId,
                                Score = 6.0m,
                                Comment = "Điểm tạm"
                            }
                        ]
                    });
                    await partialCtx.SaveChangesAsync();
                }

                return new AiGradingResult
                {
                    Score = 8.5m,
                    ConfidenceScore = 0.96m,
                    Feedback = "Bản chấm hoàn chỉnh sau Pha 3A",
                    CotTrace = "Updated CoT",
                    Details = [new() { CriterionId = critId.ToString(), Score = 8.5m, Comment = "Đầy đủ ý chính" }]
                };
            });

        using var handlerCtx = CreateDbContext(dbName);
        var result = await new SubmitMockExamSessionCommandHandler(handlerCtx, aiMock.Object)
            .Handle(new SubmitMockExamSessionCommand(sessionId, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value.TotalScore.Should().Be(8.5m);

        competingClaimResult.Should().NotBeNull();
        competingClaimResult!.IsSuccess.Should().BeFalse("Pha 1 phải làm mới SubmittedAt khi claim stale để chặn thundering herd");
        competingClaimResult.Error.Should().StartWith("CONFLICT:");

        using var verifyCtx = CreateDbContext(dbName);
        var evalsForAnswer = await verifyCtx.AiEvaluations
            .AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.MockExamAnswerId == answerId)
            .ToListAsync();
        evalsForAnswer.Should().ContainSingle("Pha 3A tuyệt đối không được tạo duplicate AiEvaluation cho cùng MockExamAnswerId");
        var persistedEval = evalsForAnswer[0];
        persistedEval.Id.Should().Be(preExistingEvalId);
        persistedEval.TotalScore.Should().Be(8.5m);
        persistedEval.ConfidenceScore.Should().Be(0.96m);
        persistedEval.Feedback.Should().Be("Bản chấm hoàn chỉnh sau Pha 3A");
        persistedEval.CotTrace.Should().Be("Updated CoT");
        persistedEval.Details.Should().ContainSingle();
        persistedEval.Details.Single().EvaluationId.Should().Be(preExistingEvalId);
        persistedEval.Details.Single().Score.Should().Be(8.5m);
        persistedEval.Details.Single().Comment.Should().Be("Đầy đủ ý chính");
    }

    [Fact(DisplayName = "Remediation A3: GetMockExamHistory bọc đóng ca hết hạn trong ExecuteMockExamGradingAsync và tuyệt đối không hạ ngược phiên đã graded về submitted")]
    public async Task Remediation_A3_GetMockExamHistory_UsesGradingLock_AndNeverDowngradesGradedSessionToSubmitted()
    {
        var dbName = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        using (var setup = CreateDbContext(dbName))
        {
            setup.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = ".NET 8", MockExamDurationMinutes = 30, IsActive = true });
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                StartedAt = DateTime.UtcNow.AddMinutes(-40),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
                Status = "in_progress"
            };
            session.Answers.Add(new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                QuestionId = Guid.NewGuid(),
                OrderIndex = 1,
                AnswerText = "Câu trả lời hợp lệ đã lưu",
                Status = "answered"
            });
            setup.MockExamSessions.Add(session);
            await setup.SaveChangesAsync();
        }

        using var historyCtx = CreateDbContext(dbName);
        var spyContext = new LockTrackingDbContextSpy(
            historyCtx,
            beforeMockGradingLock: async lockedSessionId =>
            {
                // Mô phỏng SubmitMockExamSession vừa hoàn tất chấm điểm và chốt "graded" trước khi GetMockExamHistory vào khóa
                using var concurrentCtx = CreateDbContext(dbName);
                var dbSession = await concurrentCtx.MockExamSessions.SingleAsync(s => s.Id == lockedSessionId);
                dbSession.Status = "graded";
                dbSession.TotalScore = 9.25m;
                dbSession.SubmittedAt = DateTime.UtcNow;
                await concurrentCtx.SaveChangesAsync();
            });

        var handler = new GetMockExamHistoryQueryHandler(spyContext);
        var historyRes = await handler.Handle(new GetMockExamHistoryQuery(studentId, courseId, 1, 10), CancellationToken.None);

        historyRes.IsSuccess.Should().BeTrue(because: historyRes.Error);
        spyContext.MockExamGradingLockCalls.Should().Be(1, "Đóng ca quá hạn trong GetMockExamHistory bắt buộc đi qua ExecuteMockExamGradingAsync");
        historyRes.Value.Items.Should().ContainSingle();
        historyRes.Value.Items[0].Status.Should().Be("graded");
        historyRes.Value.Items[0].TotalScore.Should().Be(9.25m);

        using var verifyCtx = CreateDbContext(dbName);
        var persisted = await verifyCtx.MockExamSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        persisted.Status.Should().Be("graded", "Tuyệt đối không được hạ ngược phiên đã graded về submitted");
        persisted.TotalScore.Should().Be(9.25m);
    }

    // =========================================================================
    // Helper: Delegating Spy cho IApplicationDbContext để xác minh lời gọi khóa
    // =========================================================================

    private sealed class LockTrackingDbContextSpy(
        OralExamDbContext inner,
        Func<Guid, Task>? beforeMockGradingLock = null) : IApplicationDbContext
    {
        private int _mockGradingDepth;

        public int MockExamGradingLockCalls { get; private set; }
        public bool IsInsideMockExamGradingLock => Volatile.Read(ref _mockGradingDepth) > 0;
        public List<Guid> PracticeMutationLockSessions { get; } = [];

        public DbSet<User> Users => inner.Users;
        public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
        public DbSet<Semester> Semesters => inner.Semesters;
        public DbSet<AcademicProgram> AcademicPrograms => inner.AcademicPrograms;
        public DbSet<Course> Courses => inner.Courses;
        public DbSet<CourseOffering> CourseOfferings => inner.CourseOfferings;
        public DbSet<CourseClo> CourseClos => inner.CourseClos;
        public DbSet<QuestionReviewRequest> QuestionReviewRequests => inner.QuestionReviewRequests;
        public DbSet<QuestionReviewRequestItem> QuestionReviewRequestItems => inner.QuestionReviewRequestItems;
        public DbSet<CourseLecturer> CourseLecturers => inner.CourseLecturers;
        public DbSet<Class> Classes => inner.Classes;
        public DbSet<ClassEnrollment> ClassEnrollments => inner.ClassEnrollments;
        public DbSet<Rubric> Rubrics => inner.Rubrics;
        public DbSet<RubricCriterion> RubricCriteria => inner.RubricCriteria;
        public DbSet<PracticeQuestion> PracticeQuestions => inner.PracticeQuestions;
        public DbSet<ExamQuestion> ExamQuestions => inner.ExamQuestions;
        public DbSet<PracticeSession> PracticeSessions => inner.PracticeSessions;
        public DbSet<PracticeAnswer> PracticeAnswers => inner.PracticeAnswers;
        public DbSet<AiEvaluation> AiEvaluations => inner.AiEvaluations;
        public DbSet<AiEvaluationDetail> AiEvaluationDetails => inner.AiEvaluationDetails;
        public DbSet<ExamStructure> ExamStructures => inner.ExamStructures;
        public DbSet<ExamSet> ExamSets => inner.ExamSets;
        public DbSet<ExamPreparation> ExamPreparations => inner.ExamPreparations;
        public DbSet<ExamAuthoringAssignment> ExamAuthoringAssignments => inner.ExamAuthoringAssignments;
        public DbSet<ExamStructureClo> ExamStructureClos => inner.ExamStructureClos;
        public DbSet<ExamSetRevision> ExamSetRevisions => inner.ExamSetRevisions;
        public DbSet<ExamSetQuestion> ExamSetQuestions => inner.ExamSetQuestions;
        public DbSet<MockExamQuota> MockExamQuotas => inner.MockExamQuotas;
        public DbSet<MockExamSession> MockExamSessions => inner.MockExamSessions;
        public DbSet<MockExamAnswer> MockExamAnswers => inner.MockExamAnswers;
        public DbSet<OfficialExamSession> OfficialExamSessions => inner.OfficialExamSessions;
        public DbSet<OfficialExamCandidate> OfficialExamCandidates => inner.OfficialExamCandidates;
        public DbSet<RealExamSessionShift> RealExamSessionShifts => inner.RealExamSessionShifts;
        public DbSet<StudentExamTicket> StudentExamTickets => inner.StudentExamTickets;
        public DbSet<ExamQuestionSubmission> ExamQuestionSubmissions => inner.ExamQuestionSubmissions;
        public DbSet<ExamAntiCheatLog> ExamAntiCheatLogs => inner.ExamAntiCheatLogs;
        public DbSet<LecturerAudit> LecturerAudits => inner.LecturerAudits;
        public DbSet<LecturerAuditDetail> LecturerAuditDetails => inner.LecturerAuditDetails;
        public DbSet<AppealRequest> AppealRequests => inner.AppealRequests;
        public DbSet<OralExamination.Domain.Entities.DeadLetterQueue> DeadLetterQueues => inner.DeadLetterQueues;
        public DbSet<GradingJob> GradingJobs => inner.GradingJobs;
        public DbSet<AuditLog> AuditLogs => inner.AuditLogs;
        public DbSet<SystemConfig> SystemConfigs => inner.SystemConfigs;
        public DbSet<InClassExamShift> InClassExamShifts => inner.InClassExamShifts;
        public DbSet<InClassShiftPassword> InClassShiftPasswords => inner.InClassShiftPasswords;
        public DbSet<InClassShiftParticipant> InClassShiftParticipants => inner.InClassShiftParticipants;
        public DbSet<InClassExamSubmission> InClassExamSubmissions => inner.InClassExamSubmissions;
        public DbSet<Notification> Notifications => inner.Notifications;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => inner.SaveChangesAsync(cancellationToken);

        public Task<T> ExecuteShiftAllocationAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> allocate, CancellationToken cancellationToken)
            => inner.ExecuteShiftAllocationAsync(shiftId, allocate, cancellationToken);

        public Task<T> ExecuteShiftMutationAsync<T>(Guid shiftId, bool isOfficial, Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken = default)
            => inner.ExecuteShiftMutationAsync(shiftId, isOfficial, mutation, cancellationToken);

        public Task<IAsyncDisposable?> BeginShiftAllocationLockScopeAsync(Guid shiftId, CancellationToken cancellationToken = default)
            => inner.BeginShiftAllocationLockScopeAsync(shiftId, cancellationToken);

        public Task<T> ExecuteTicketDecisionAsync<T>(Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default)
            => inner.ExecuteTicketDecisionAsync(ticketId, decision, cancellationToken);

        public Task<T> ExecuteMockExamStartAsync<T>(Guid studentId, Guid courseId, DateOnly quotaDate, Func<CancellationToken, Task<T>> start, CancellationToken cancellationToken = default)
            => inner.ExecuteMockExamStartAsync(studentId, courseId, quotaDate, start, cancellationToken);

        public Task<T> ExecuteInClassJoinAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> join, CancellationToken cancellationToken = default)
            => inner.ExecuteInClassJoinAsync(shiftId, join, cancellationToken);

        public Task<T> ExecuteClassEnrollmentAsync<T>(Guid classId, Func<CancellationToken, Task<T>> enrollment, CancellationToken cancellationToken = default)
            => inner.ExecuteClassEnrollmentAsync(classId, enrollment, cancellationToken);

        public Task<T> ExecuteInClassSubmissionAsync<T>(Guid shiftId, Guid studentId, Func<CancellationToken, Task<T>> submit, CancellationToken cancellationToken = default)
            => inner.ExecuteInClassSubmissionAsync(shiftId, studentId, submit, cancellationToken);

        public async Task<T> ExecuteMockExamGradingAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            MockExamGradingLockCalls++;
            if (beforeMockGradingLock is not null)
            {
                await beforeMockGradingLock(sessionId);
            }
            Interlocked.Increment(ref _mockGradingDepth);
            try
            {
                return await inner.ExecuteMockExamGradingAsync(sessionId, action, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _mockGradingDepth);
            }
        }

        public async Task<T> ExecutePracticeSessionMutationAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
        {
            PracticeMutationLockSessions.Add(sessionId);
            return await inner.ExecutePracticeSessionMutationAsync(sessionId, action, cancellationToken);
        }

        public Task<T> ExecuteAppealTransitionAsync<T>(Guid appealId, Func<CancellationToken, Task<T>> transition, CancellationToken cancellationToken = default)
            => inner.ExecuteAppealTransitionAsync(appealId, transition, cancellationToken);

        public Task<T> ExecuteAppealDecisionAsync<T>(Guid appealId, Guid ticketId, Func<CancellationToken, Task<T>> decision, CancellationToken cancellationToken = default)
            => inner.ExecuteAppealDecisionAsync(appealId, ticketId, decision, cancellationToken);

        public Task<T> ExecuteAppealAssignmentAsync<T>(IReadOnlyCollection<Guid> appealIds, IReadOnlyCollection<Guid> lecturerIds, Func<CancellationToken, Task<T>> assignment, CancellationToken cancellationToken = default)
            => inner.ExecuteAppealAssignmentAsync(appealIds, lecturerIds, assignment, cancellationToken);

        public Task<T> ExecuteBulkExamTicketImportAsync<T>(Guid shiftId, Func<CancellationToken, Task<T>> import, CancellationToken cancellationToken = default)
            => inner.ExecuteBulkExamTicketImportAsync(shiftId, import, cancellationToken);

        public Task<T> ExecuteOfficialSemesterBatchAsync<T>(Guid semesterId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => inner.ExecuteOfficialSemesterBatchAsync(semesterId, action, cancellationToken);

        public Task<T> ExecuteOfficialShiftScheduleBatchAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => inner.ExecuteOfficialShiftScheduleBatchAsync(action, cancellationToken);

        public Task<T> ExecuteOfficialSessionCandidatesAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
            => inner.ExecuteOfficialSessionCandidatesAsync(sessionId, action, cancellationToken);

        public Task<T> ExecuteOfficialSessionAllocationAsync<T>(Guid sessionId, Func<CancellationToken, Task<T>> allocation, CancellationToken cancellationToken = default)
            => inner.ExecuteOfficialSessionAllocationAsync(sessionId, allocation, cancellationToken);

        public Task<T> ExecuteRefreshTokenRotationAsync<T>(Func<CancellationToken, Task<T>> rotate, CancellationToken cancellationToken = default)
            => inner.ExecuteRefreshTokenRotationAsync(rotate, cancellationToken);

        public Task<IRefreshTokenLockScope> BeginRefreshTokenLockScopeAsync(string tokenHash, CancellationToken cancellationToken = default)
            => inner.BeginRefreshTokenLockScopeAsync(tokenHash, cancellationToken);
    }
}

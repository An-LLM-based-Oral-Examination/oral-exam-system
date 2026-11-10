using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using OralExamination.API.Middlewares;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// EMPIRICAL CHALLENGER TEST SUITE (M5 - Instance 2):
/// Kiểm chứng đối kháng thực nghiệm khả năng chịu tải, phòng thủ ngoại lệ và an toàn đồng thời:
/// 1. GetStudentDashboardQueryHandler:
///    - CSDL hoàn toàn rỗng / 0 sinh viên -> trả về Failure rõ ràng, không crash 500.
///    - Sinh viên có vé thi mồ côi (Shift = null, Submissions = null / rỗng, không điểm) -> không chia cho 0, không văng NullReferenceException.
///    - Cấu trúc phụ (AiEvaluationDetails) gặp lỗi schema drift -> suy thoái êm dịu (Graceful Degradation).
/// 2. GetPracticeSessionQueryHandler & SubmitMockExamAnswerCommandHandler:
///    - Xử lý phòng thủ null và ngoại lệ CSDL không để văng unhandled 500.
///    - Xử lý DbUpdateConcurrencyException và DbUpdateException trong SubmitMockExamAnswer.
/// 3. GlobalExceptionMiddleware:
///    - Ánh xạ PostgresException chuẩn RFC 7807:
///      * 23505 (UniqueViolation) -> 409 Conflict
///      * 23503 (ForeignKeyViolation) -> 400 Bad Request
///      * 23514 (CheckViolation) -> 400 Bad Request
///      * 42703 (UndefinedColumn) / 42P01 (UndefinedTable) -> 500 với type https://oralexam.io.vn/errors/database-schema
///    - Ánh xạ DbUpdateConcurrencyException -> 409 Conflict
///    - Ánh xạ KeyNotFoundException -> 404 Not Found
/// </summary>
public sealed class AdversarialMilestone5ResilienceAndConcurrencyChallengerTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static void SeedCourseAndStudent(OralExamDbContext context, Guid courseId, Guid studentId)
    {
        if (!context.Courses.Any(c => c.Id == courseId))
        {
            context.Courses.Add(new Course
            {
                Id = courseId,
                Code = "PRN231",
                Name = "Practice Course",
                Credits = 3,
                SemesterId = Guid.NewGuid(),
                IsActive = true
            });
        }

        if (!context.Users.Any(u => u.Id == studentId))
        {
            context.Users.Add(new User
            {
                Id = studentId,
                Email = $"student_{studentId:N}@fpt.edu.vn",
                FullName = "Sinh Viên Test",
                Role = UserRole.Student,
                IsActive = true
            });
        }
    }

    private class ConcurrencyThrowingDbContext : OralExamDbContext
    {
        public ConcurrencyThrowingDbContext(DbContextOptions<OralExamDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateConcurrencyException("Row has been modified by another concurrent transaction.");
    }

    private class DbUpdateThrowingDbContext : OralExamDbContext
    {
        public DbUpdateThrowingDbContext(DbContextOptions<OralExamDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Database constraint violation while updating table.");
    }

    private class GeneralExceptionThrowingDbContext : OralExamDbContext
    {
        public GeneralExceptionThrowingDbContext(DbContextOptions<OralExamDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("General unexpected persistence error.");
    }

    private static PostgresException CreatePostgresException(
        string sqlState,
        string message = "Database error occurred.",
        string tableName = "sample_table",
        string columnName = "sample_col")
    {
        var ctors = typeof(PostgresException).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var pars = ctor.GetParameters();
            if (pars.Length >= 4 && pars[3].Name == "sqlState")
            {
                var args = new object?[pars.Length];
                args[0] = message;
                args[1] = "ERROR";
                args[2] = "ERROR";
                args[3] = sqlState;
                for (int i = 4; i < pars.Length; i++)
                {
                    if (pars[i].Name == "tableName") args[i] = tableName;
                    else if (pars[i].Name == "columnName") args[i] = columnName;
                    else args[i] = pars[i].HasDefaultValue ? pars[i].DefaultValue : null;
                }
                return (PostgresException)ctor.Invoke(args);
            }
        }
        throw new InvalidOperationException("Could not find suitable PostgresException constructor.");
    }

    private static byte[] CreateValidWebmBytes()
    {
        var bytes = new byte[300];
        bytes[0] = 0x1A;
        bytes[1] = 0x45;
        bytes[2] = 0xDF;
        bytes[3] = 0xA3;
        return bytes;
    }

    #region Part 1: GetStudentDashboardQueryHandler Adversarial Resilience

    [Fact(DisplayName = "ADV-RES-01: CSDL hoàn toàn rỗng (0 bản ghi User) không ném unhandled exception")]
    public async Task Dashboard_EmptyDatabase_Should_ReturnFailureGracefullyWithout500()
    {
        using var context = CreateDbContext();
        var handler = new GetStudentDashboardQueryHandler(context, null);

        var randomStudentId = Guid.NewGuid();
        var result = await handler.Handle(new GetStudentDashboardQuery(randomStudentId), CancellationToken.None);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "ADV-RES-02: Vé thi mồ côi (Shift = null, Submissions = null) không chia cho 0 và không văng NullReferenceException")]
    public async Task Dashboard_OrphanTicket_NullShiftAndNullSubmissions_Should_NotDivideByZero()
    {
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "orphan_student@fpt.edu.vn",
            FullName = "Nguyễn Văn Mồ Côi",
            StudentCode = "SE199001",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student);

        var orphanTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = Guid.NewGuid(),
            Shift = null!,
            SeatNumber = 1,
            Status = ExamTicketStatus.Locked,
            IsLocked = true,
            Submissions = null!
        };
        context.StudentExamTickets.Add(orphanTicket);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.OfficialExamTickets.Should().NotBeNull();
    }

    [Fact(DisplayName = "ADV-RES-03: Vé thi có Submissions toàn điểm null không thực hiện phép chia và trả về FinalScore = null")]
    public async Task Dashboard_TicketWithAllNullScores_Should_ReturnNullFinalScore_WithoutDividingByZero()
    {
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "null_scores_student@fpt.edu.vn",
            FullName = "Trần Thị Điểm Null",
            StudentCode = "SE199002",
            Role = UserRole.Student,
            IsActive = true
        };
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Apps",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Course = course,
            ExamStructureId = Guid.NewGuid(),
            Title = "Ca thi Res",
            ExamDate = new DateOnly(2026, 10, 1)
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            ShiftName = "Shift 1",
            RoomLab = "Lab 101",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1)
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ShiftId = shift.Id,
            Shift = shift,
            SeatNumber = 10,
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };

        var sub1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = Guid.NewGuid(), FinalScore = null, AiScore = null };
        var sub2 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = Guid.NewGuid(), FinalScore = null, AiScore = null };
        ticket.Submissions = new List<ExamQuestionSubmission> { sub1, sub2 };

        context.Users.Add(student);
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.OfficialExamSessions.Add(session);
        context.RealExamSessionShifts.Add(shift);
        context.StudentExamTickets.Add(ticket);
        context.ExamQuestionSubmissions.AddRange(sub1, sub2);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ticketDto = result.Value!.OfficialExamTickets.Should().ContainSingle().Subject;
        ticketDto.FinalScore.Should().BeNull();
    }

    [Fact(DisplayName = "ADV-RES-04: Phiên luyện tập có Answers rỗng hoặc SelectedQuestionIds null không văng NullReferenceException")]
    public async Task Dashboard_PracticeSessionsWithNullCollections_Should_HandleGracefully()
    {
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "null_practice@fpt.edu.vn",
            FullName = "Lê Null Practice",
            StudentCode = "SE199003",
            Role = UserRole.Student,
            IsActive = true
        };
        var practiceSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = Guid.NewGuid(),
            PracticeMode = "mixed",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(30),
            SelectedQuestionIdsJson = "[]",
            Answers = null!
        };

        context.Users.Add(student);
        context.PracticeSessions.Add(practiceSession);
        await context.SaveChangesAsync();

        var handler = new GetStudentDashboardQueryHandler(context, null);

        var result = await handler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalPracticeSessions.Should().Be(1);
        result.Value.CompletedPracticeSessions.Should().Be(1);
    }

    #endregion

    #region Part 2: GetPracticeSessionQueryHandler & SubmitMockExamAnswerCommandHandler

    [Fact(DisplayName = "ADV-RES-05: GetPracticeSession với session không tồn tại trả về Failure không ném 500")]
    public async Task PracticeSession_NonExistentSession_Should_ReturnFailure()
    {
        using var context = CreateDbContext();
        var handler = new GetPracticeSessionQueryHandler(context, null);

        var result = await handler.Handle(new GetPracticeSessionQuery(Guid.NewGuid(), Guid.NewGuid(), false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "ADV-RES-06: GetPracticeSession với student không sở hữu và không elevated role trả về Failure")]
    public async Task PracticeSession_UnauthorizedStudent_Should_ReturnFailure()
    {
        using var context = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        SeedCourseAndStudent(context, courseId, ownerId);

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = ownerId,
            CourseId = courseId,
            PracticeMode = "on_demand",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context, null);

        var result = await handler.Handle(new GetPracticeSessionQuery(session.Id, callerId, false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có quyền truy cập");
    }

    [Fact(DisplayName = "ADV-RES-07: GetPracticeSession với session có các collection null hoàn toàn không văng NullReferenceException")]
    public async Task PracticeSession_WithNullCollections_Should_ReturnSuccess()
    {
        using var context = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        SeedCourseAndStudent(context, courseId, ownerId);

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = ownerId,
            CourseId = courseId,
            PracticeMode = "full_session",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            CompletedAt = DateTime.UtcNow,
            SelectedQuestionIdsJson = "[]",
            Answers = null!
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var handler = new GetPracticeSessionQueryHandler(context, null);

        var result = await handler.Handle(new GetPracticeSessionQuery(session.Id, ownerId, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Questions.Should().BeEmpty();
        result.Value.Answers.Should().BeEmpty();
        result.Value.CourseCode.Should().Be("PRN231");
    }

    [Fact(DisplayName = "ADV-RES-08: SubmitMockExamAnswer với session không tồn tại trả về Failure không ném unhandled 500")]
    public async Task SubmitMockExamAnswer_NonExistentSession_Should_ReturnFailure()
    {
        using var context = CreateDbContext();
        var sttMock = new Mock<ISpeechToTextService>();
        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object, null, null);

        var command = new SubmitMockExamAnswerCommand(
            SessionId: Guid.NewGuid(),
            StudentId: Guid.NewGuid(),
            QuestionId: Guid.NewGuid(),
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 30,
            DirectTranscript: "Direct answer text"
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "ADV-RES-09: SubmitMockExamAnswer khi xảy ra DbUpdateConcurrencyException trả về Failure CONFLICT")]
    public async Task SubmitMockExamAnswer_ConcurrencyException_Should_ReturnConflictFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        // Seed data using standard context
        using (var seedContext = new OralExamDbContext(options))
        {
            SeedCourseAndStudent(seedContext, courseId, studentId);
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                Status = "in_progress",
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                HasFollowUp = false,
                Answers = new List<MockExamAnswer>()
            };
            var answer = new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                QuestionId = questionId,
                OrderIndex = 1,
                Status = "pending"
            };
            session.Answers.Add(answer);
            seedContext.MockExamSessions.Add(session);
            await seedContext.SaveChangesAsync();
        }

        // Execute handler using ConcurrencyThrowingDbContext
        using var throwingContext = new ConcurrencyThrowingDbContext(options);
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("Nội dung trả lời kiểm thử.", "Nội dung trả lời kiểm thử.", "Whisper", false, null, 0.95));
        sttMock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        var loggerMock = new Mock<ILogger<SubmitMockExamAnswerCommandHandler>>();
        var handler = new SubmitMockExamAnswerCommandHandler(throwingContext, sttMock.Object, null, loggerMock.Object);

        using var audioStream = new MemoryStream(CreateValidWebmBytes());
        var command = new SubmitMockExamAnswerCommand(
            SessionId: sessionId,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audioStream,
            FileName: "answer.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 45
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT");
    }

    [Fact(DisplayName = "ADV-RES-10: SubmitMockExamAnswer khi xảy ra DbUpdateException trả về Failure rõ nghĩa")]
    public async Task SubmitMockExamAnswer_DbUpdateException_Should_ReturnFailureGracefully()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        using (var seedContext = new OralExamDbContext(options))
        {
            SeedCourseAndStudent(seedContext, courseId, studentId);
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                Status = "in_progress",
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                HasFollowUp = false,
                Answers = new List<MockExamAnswer>()
            };
            var answer = new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                QuestionId = questionId,
                OrderIndex = 1,
                Status = "pending"
            };
            session.Answers.Add(answer);
            seedContext.MockExamSessions.Add(session);
            await seedContext.SaveChangesAsync();
        }

        using var throwingContext = new DbUpdateThrowingDbContext(options);
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("Nội dung trả lời kiểm thử.", "Nội dung trả lời kiểm thử.", "Whisper", false, null, 0.95));
        sttMock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        var loggerMock = new Mock<ILogger<SubmitMockExamAnswerCommandHandler>>();
        var handler = new SubmitMockExamAnswerCommandHandler(throwingContext, sttMock.Object, null, loggerMock.Object);

        using var audioStream = new MemoryStream(CreateValidWebmBytes());
        var command = new SubmitMockExamAnswerCommand(
            SessionId: sessionId,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audioStream,
            FileName: "answer.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 30
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("lỗi cập nhật cơ sở dữ liệu");
    }

    [Fact(DisplayName = "ADV-RES-10B: SubmitMockExamAnswer khi xảy ra General Exception trả về Failure an toàn")]
    public async Task SubmitMockExamAnswer_GeneralException_Should_ReturnFailureSafely()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        using (var seedContext = new OralExamDbContext(options))
        {
            SeedCourseAndStudent(seedContext, courseId, studentId);
            var session = new MockExamSession
            {
                Id = sessionId,
                StudentId = studentId,
                CourseId = courseId,
                Status = "in_progress",
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                HasFollowUp = false,
                Answers = new List<MockExamAnswer>()
            };
            var answer = new MockExamAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                QuestionId = questionId,
                OrderIndex = 1,
                Status = "pending"
            };
            session.Answers.Add(answer);
            seedContext.MockExamSessions.Add(session);
            await seedContext.SaveChangesAsync();
        }

        using var throwingContext = new GeneralExceptionThrowingDbContext(options);
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("Nội dung trả lời kiểm thử.", "Nội dung trả lời kiểm thử.", "Whisper", false, null, 0.95));
        sttMock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        var loggerMock = new Mock<ILogger<SubmitMockExamAnswerCommandHandler>>();
        var handler = new SubmitMockExamAnswerCommandHandler(throwingContext, sttMock.Object, null, loggerMock.Object);

        using var audioStream = new MemoryStream(CreateValidWebmBytes());
        var command = new SubmitMockExamAnswerCommand(
            SessionId: sessionId,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: audioStream,
            FileName: "answer.webm",
            ContentType: "audio/webm",
            TimeTakenSeconds: 30
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Đã xảy ra lỗi khi lưu kết quả bài thi.");
    }

    #endregion

    #region Part 3: GlobalExceptionMiddleware RFC 7807 Error Mapping

    private static async Task<(int StatusCode, ProblemDetails Problem)> ExecuteMiddlewareWithExceptionAsync(
        Exception exception,
        bool isDevelopment = false)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/test-endpoint";

        RequestDelegate next = _ => throw exception;
        var loggerMock = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(isDevelopment ? Environments.Development : Environments.Production);

        var middleware = new GlobalExceptionMiddleware(next, loggerMock.Object, envMock.Object);

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(responseText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return (context.Response.StatusCode, problem!);
    }

    [Fact(DisplayName = "ADV-RES-11: Middleware ánh xạ PostgresException 23505 (UniqueViolation) thành 409 Conflict")]
    public async Task Middleware_Postgres_23505_Should_Return_409Conflict()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.UniqueViolation, "duplicate key value violates unique constraint");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Dữ liệu đã tồn tại.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.8");
    }

    [Fact(DisplayName = "ADV-RES-12: Middleware ánh xạ PostgresException 23503 (ForeignKeyViolation) thành 400 BadRequest")]
    public async Task Middleware_Postgres_23503_Should_Return_400BadRequest()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.ForeignKeyViolation, "insert or update on table violates foreign key constraint");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Dữ liệu tham chiếu không hợp lệ.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact(DisplayName = "ADV-RES-13: Middleware ánh xạ PostgresException 23514 (CheckViolation) thành 400 BadRequest")]
    public async Task Middleware_Postgres_23514_Should_Return_400BadRequest()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.CheckViolation, "new row for relation violates check constraint");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Vi phạm ràng buộc dữ liệu.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact(DisplayName = "ADV-RES-14: Middleware ánh xạ PostgresException 42703 (UndefinedColumn) thành 500 với database-schema type")]
    public async Task Middleware_Postgres_42703_Should_Return_500_With_DatabaseSchemaType()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.UndefinedColumn, "column e.student_id does not exist", tableName: "ai_evaluations", columnName: "student_id");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("Lỗi cấu trúc cơ sở dữ liệu.");
        problem.Type.Should().Be("https://oralexam.io.vn/errors/database-schema");
    }

    [Fact(DisplayName = "ADV-RES-15: Middleware ánh xạ PostgresException 42P01 (UndefinedTable) thành 500 với database-schema type")]
    public async Task Middleware_Postgres_42P01_Should_Return_500_With_DatabaseSchemaType()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.UndefinedTable, "relation practice_sessions does not exist", tableName: "practice_sessions");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(pgEx);

        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Title.Should().Be("Lỗi cấu trúc cơ sở dữ liệu.");
        problem.Type.Should().Be("https://oralexam.io.vn/errors/database-schema");
    }

    [Fact(DisplayName = "ADV-RES-16: Middleware ánh xạ DbUpdateException bọc Postgres 23505 thành 409 Conflict")]
    public async Task Middleware_DbUpdateException_WrappingPostgres23505_Should_Return_409Conflict()
    {
        var pgEx = CreatePostgresException(PostgresErrorCodes.UniqueViolation, "duplicate key value");
        var dbUpdateEx = new DbUpdateException("An error occurred while saving", pgEx);

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(dbUpdateEx);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Dữ liệu đã tồn tại.");
    }

    [Fact(DisplayName = "ADV-RES-17: Middleware ánh xạ DbUpdateConcurrencyException thành 409 Conflict")]
    public async Task Middleware_DbUpdateConcurrencyException_Should_Return_409Conflict()
    {
        var concurrencyEx = new DbUpdateConcurrencyException("Row has been modified by another transaction.");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(concurrencyEx);

        statusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Xung đột dữ liệu đồng thời.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.8");
    }

    [Fact(DisplayName = "ADV-RES-18: Middleware ánh xạ KeyNotFoundException thành 404 NotFound")]
    public async Task Middleware_KeyNotFoundException_Should_Return_404NotFound()
    {
        var knfEx = new KeyNotFoundException("Không tìm thấy ca thi với định danh chỉ định.");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(knfEx);

        statusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Không tìm thấy tài nguyên.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.4");
        problem.Detail.Should().Contain("Không tìm thấy ca thi");
    }

    [Fact(DisplayName = "ADV-RES-19: Middleware ánh xạ BadHttpRequestException thành 400 BadRequest")]
    public async Task Middleware_BadHttpRequestException_Should_Return_400BadRequest()
    {
        var badHttpEx = new BadHttpRequestException("Invalid JSON payload format.");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(badHttpEx);

        statusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().Be("Yêu cầu không hợp lệ.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact(DisplayName = "ADV-RES-20: Middleware ánh xạ UnauthorizedAccessException thành 403 Forbidden")]
    public async Task Middleware_UnauthorizedAccessException_Should_Return_403Forbidden()
    {
        var unauthEx = new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(unauthEx);

        statusCode.Should().Be(StatusCodes.Status403Forbidden);
        problem.Status.Should().Be(StatusCodes.Status403Forbidden);
        problem.Title.Should().Be("Truy cập bị từ chối.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.3");
    }

    [Fact(DisplayName = "ADV-RES-21: Middleware ánh xạ OperationCanceledException thành 499 Client Closed Request")]
    public async Task Middleware_OperationCanceledException_Should_Return_499ClientClosed()
    {
        var cancelEx = new OperationCanceledException("The operation was canceled.");

        var (statusCode, problem) = await ExecuteMiddlewareWithExceptionAsync(cancelEx);

        statusCode.Should().Be(499);
        problem.Status.Should().Be(499);
        problem.Title.Should().Be("Thao tác đã bị hủy.");
    }

    #endregion
}

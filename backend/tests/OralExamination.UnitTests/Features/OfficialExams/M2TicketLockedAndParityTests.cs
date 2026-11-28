using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.KioskHeartbeat;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class M2TicketLockedAndParityTests
{
    private const string BoundDeviceId = "BYOD-M2-LOCKED-DEVICE-0001";
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"m2-parity-{Guid.NewGuid():N}")
            .Options;
        return new OralExamDbContext(options);
    }

    private static ClaimsPrincipal CreateStudentPrincipal(Guid studentId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, studentId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Student),
            new Claim("role", UserRole.Student)
        ], "Test"));

    private static async Task<(User Student, RealExamSessionShift Shift, StudentExamTicket Ticket, ExamQuestion Question, ExamQuestionSubmission Submission, string RawToken, string QuestionAudioKey, string TicketAudioKey)> SeedTicketAsync(
        OralExamDbContext db,
        string ticketStatus,
        bool isLocked)
    {
        var now = DateTime.UtcNow;
        var officer = new User { Id = Guid.NewGuid(), Email = "eo-m2@fpt.edu.vn", FullName = "EO M2", Role = UserRole.ExaminationOfficer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv-m2@fpt.edu.vn", FullName = "SV M2", StudentCode = "SE189001", Role = UserRole.Student, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", Credits = 3, SemesterId = semester.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, CreatedBy = officer.Id, Name = "Struct", IsActive = true };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            SemesterId = semester.Id,
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            ExamAttemptType = OfficialExamAttemptType.FinalExam,
            Title = "FE M2",
            ExamDate = DateOnly.FromDateTime(now),
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            Status = "scheduled",
            CreatedBy = officer.Id
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca M2",
            RoomLab = "Lab 301",
            StartTime = now.AddMinutes(-15),
            EndTime = now.AddMinutes(45),
            ProctorUserId = officer.Id,
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true,
            MaxSeats = 24
        };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric M2", TotalMaxScore = 10m, IsActive = true };
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Clean Architecture",
            Content = "Explain Clean Architecture layers",
            SampleAnswer = new string('A', 60),
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SessionId = session.Id,
            SetCode = "SET-M2-01",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var examSetQuestion = new ExamSetQuestion
        {
            Id = Guid.NewGuid(),
            ExamSetId = examSet.Id,
            ExamQuestionId = question.Id,
            OrderIndex = 1
        };

        var rawToken = OfficialExamDeviceGuard.GenerateSessionToken();
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ticketStatus,
            IsLocked = isLocked,
            DeviceId = BoundDeviceId,
            DeviceLockedAt = now.AddMinutes(-10),
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawToken)
        };

        var questionAudioKey = OfficialExamAudioObjectKey.CreateForQuestion(
            shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!, examSetQuestion.OrderIndex, 0);
        var ticketAudioKey = OfficialExamAudioObjectKey.Create(
            shift.Id, ticket.Id, ticket.SeatNumber, student.StudentCode!);

        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            FollowUpIndex = 0,
            TranscriptWhisper = "Clean Architecture isolates domain rules from infrastructure concerns.",
            AudioObjectKey = questionAudioKey,
            AudioR2Url = questionAudioKey,
            AudioHashSha256 = ValidSha256,
            GradingStatus = "pending",
            SubmittedAt = now.AddMinutes(-2)
        };

        db.Users.AddRange(officer, student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.Add(question);
        db.ExamSets.Add(examSet);
        db.ExamSetQuestions.Add(examSetQuestion);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(submission);
        await db.SaveChangesAsync();

        return (student, shift, ticket, question, submission, rawToken, questionAudioKey, ticketAudioKey);
    }

    [Theory(DisplayName = "M2-01: 6 handlers OfficialExams trả OfficialExamErrors.TicketLocked khi Status==LOCKED hoặc IsLocked==true")]
    [InlineData(ExamTicketStatus.Locked, false)]
    [InlineData(ExamTicketStatus.InProgress, true)]
    [InlineData(ExamTicketStatus.Locked, true)]
    public async Task SixOfficialExamHandlers_ReturnTicketLocked_WhenStatusLockedOrIsLocked(string ticketStatus, bool isLocked)
    {
        await using var db = CreateDbContext();
        var (student, _, ticket, question, submission, rawToken, questionAudioKey, ticketAudioKey) =
            await SeedTicketAsync(db, ticketStatus, isLocked);

        var storageMock = new Mock<IStorageService>(MockBehavior.Strict);

        var submitQuestionRes = await new SubmitOfficialQuestionAnswerCommandHandler(db, storageMock.Object).Handle(
            new SubmitOfficialQuestionAnswerCommand(
                student.Id, ticket.Id, question.Id, 0, null,
                questionAudioKey, ValidSha256,
                "Initial", "Initial", null, 30, false,
                BoundDeviceId, rawToken),
            CancellationToken.None);
        submitQuestionRes.IsSuccess.Should().BeFalse();
        submitQuestionRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var questionAuthRes = await new CreateQuestionAudioUploadAuthorizationCommandHandler(db, storageMock.Object).Handle(
            new CreateQuestionAudioUploadAuthorizationCommand(
                student.Id, ticket.Id, question.Id, 0, null, BoundDeviceId, rawToken, DateTime.UtcNow),
            CancellationToken.None);
        questionAuthRes.IsSuccess.Should().BeFalse();
        questionAuthRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var followUpRes = await new GenerateOfficialExamFollowUpCommandHandler(db).Handle(
            new GenerateOfficialExamFollowUpCommand(
                student.Id, ticket.Id, submission.Id, BoundDeviceId, rawToken),
            CancellationToken.None);
        followUpRes.IsSuccess.Should().BeFalse();
        followUpRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var ticketAuthRes = await new CreateAudioUploadAuthorizationCommandHandler(db, storageMock.Object).Handle(
            new CreateAudioUploadAuthorizationCommand(
                student.Id, ticket.Id, "10.0.0.1", DateTime.UtcNow, BoundDeviceId, rawToken),
            CancellationToken.None);
        ticketAuthRes.IsSuccess.Should().BeFalse();
        ticketAuthRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var sealRes = await new SealOfficialExamAudioCommandHandler(db, storageMock.Object).Handle(
            new SealOfficialExamAudioCommand(
                student.Id, ticket.Id, "10.0.0.1", ticketAudioKey, ValidSha256, BoundDeviceId, rawToken),
            CancellationToken.None);
        sealRes.IsSuccess.Should().BeFalse();
        sealRes.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var checkInRes = await new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator()).Handle(
            new CheckInExamTicketCommand(
                student.Id, ticket.Id, "10.0.0.1", DateTime.UtcNow, BoundDeviceId, "1.0.0"),
            CancellationToken.None);
        checkInRes.IsSuccess.Should().BeFalse();
        checkInRes.Error.Should().Be(OfficialExamErrors.TicketLocked);
    }

    [Fact(DisplayName = "M2-02: OfficialExamsController và OfficialExamKioskController ánh xạ TICKET_LOCKED sang 409 với type ticket-locked")]
    public async Task OfficialExamsAndKioskControllers_MapTicketLocked_To409WithTicketLockedType()
    {
        var studentId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var principal = CreateStudentPrincipal(studentId);

        var workflowSender = new Mock<ISender>();
        workflowSender.Setup(s => s.Send(It.IsAny<CheckInExamTicketCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExamTicketResponse>.Failure(OfficialExamErrors.TicketLocked));

        var officialController = new OfficialExamsController(workflowSender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };

        var workflowResult = await officialController.CheckInTicket(
            ticketId,
            CancellationToken.None,
            new CheckInExamTicketRequest { DeviceId = BoundDeviceId });

        var workflowObject = workflowResult.Should().BeOfType<ObjectResult>().Subject;
        workflowObject.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        workflowObject.Value.Should().BeOfType<ProblemDetails>()
            .Which.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/ticket-locked");

        var kioskSender = new Mock<ISender>();
        kioskSender.Setup(s => s.Send(It.IsAny<KioskHeartbeatCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<KioskHeartbeatResponse>.Failure(OfficialExamErrors.TicketLocked));

        var kioskController = new OfficialExamKioskController(kioskSender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };

        var kioskResult = await kioskController.Heartbeat(
            ticketId,
            new KioskHeartbeatRequest { DeviceId = BoundDeviceId, AudioLevelDb = 65 },
            CancellationToken.None);

        var kioskObject = kioskResult.Should().BeOfType<ObjectResult>().Subject;
        kioskObject.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        kioskObject.Value.Should().BeOfType<ProblemDetails>()
            .Which.Type.Should().Be("https://oralexam.fpt.edu.vn/errors/ticket-locked");
    }

    [Fact(DisplayName = "M2-03: GradingQueueWorker đặt ManagedByRole = admin khi cập nhật bản ghi PracticeGrading DLQ sẵn có")]
    public async Task GradingQueueWorker_UpdatesExistingPracticeDlq_SetsManagedByRoleAdmin()
    {
        var dbName = $"m2-dlq-role-{Guid.NewGuid():N}";
        var student = new User { Id = Guid.NewGuid(), Email = "sv-dlq@fpt.edu.vn", FullName = "SV DLQ", StudentCode = "SE189002", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric DLQ", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Core", MaxScore = 10m, Weight = 1.0m, OrderIndex = 1 };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Explain CQRS",
            Content = "Explain CQRS",
            SampleAnswer = new string('B', 60),
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var session = new PracticeSession { Id = Guid.NewGuid(), StudentId = student.Id, CourseId = course.Id, PracticeMode = "per_question", Status = "in_progress" };
        var answer = new PracticeAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = question.Id, StudentId = student.Id, AnswerText = "CQRS separates commands and queries.", Status = "pending" };
        var existingDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            FlowType = "PRACTICE",
            ManagedByRole = "lecturer",
            CorrelationId = answer.Id,
            PayloadJson = "{}",
            ErrorMessage = "Previous failure",
            RetryCount = 1,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Gemini 503"));

        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<OralExamDbContext>());
        services.AddSingleton(aiMock.Object);

        await using var provider = services.BuildServiceProvider();
        await using (var seedDb = provider.CreateScope().ServiceProvider.GetRequiredService<OralExamDbContext>())
        {
            seedDb.Users.Add(student);
            seedDb.Courses.Add(course);
            seedDb.Rubrics.Add(rubric);
            seedDb.RubricCriteria.Add(criterion);
            seedDb.PracticeQuestions.Add(question);
            seedDb.PracticeSessions.Add(session);
            seedDb.PracticeAnswers.Add(answer);
            seedDb.DeadLetterQueues.Add(existingDlq);
            await seedDb.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GradingQueue:RetryDelayMilliseconds"] = "0",
                ["GradingQueue:MaxDegreeOfParallelism"] = "1"
            })
            .Build();

        var hubMock = new Mock<IHubContext<PracticeHub, IPracticeClient>>();
        var clientsMock = new Mock<IHubClients<IPracticeClient>>();
        var clientMock = new Mock<IPracticeClient>();
        clientMock.Setup(c => c.ReceiveGradingError(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        clientsMock.Setup(c => c.Group($"session_{session.Id}")).Returns(clientMock.Object);
        hubMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);

        var channel = new SingleQueueTestChannel();
        var worker = new GradingQueueWorker(channel, provider, hubMock.Object, NullLogger<GradingQueueWorker>.Instance, config);

        await worker.StartAsync(CancellationToken.None);
        await channel.EnqueueAsync(new GradingTask
        {
            AnswerId = answer.Id,
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = answer.AnswerText
        });
        channel.Complete();

        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(30));
        }
        await worker.StopAsync(CancellationToken.None);

        await using var verifyDb = provider.CreateScope().ServiceProvider.GetRequiredService<OralExamDbContext>();
        var updatedDlq = await verifyDb.DeadLetterQueues.AsNoTracking().SingleAsync(d => d.Id == existingDlq.Id);
        updatedDlq.ManagedByRole.Should().Be("admin");
        updatedDlq.RetryCount.Should().Be(4);
    }

    [Fact(DisplayName = "M2-04: GetMyInClassShifts ẩn MyFinalScore khi sinh viên có trạng thái ABSENT dù ca đã FINALIZED")]
    public async Task GetMyInClassShifts_HidesMyFinalScore_WhenParticipantIsAbsentOnFinalizedShift()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-m2@fpt.edu.vn", FullName = "GV M2", Role = UserRole.Lecturer, IsActive = true };
        var presentStudent = new User { Id = Guid.NewGuid(), Email = "sv-present@fpt.edu.vn", FullName = "SV Present", StudentCode = "SE189011", Role = UserRole.Student, IsActive = true };
        var absentStudent = new User { Id = Guid.NewGuid(), Email = "sv-absent@fpt.edu.vn", FullName = "SV Absent", StudentCode = "SE189012", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SWD392 - SE1801", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Finalized M2",
            TotpSecretKey = "SECRET_M2",
            Status = "FINALIZED",
            IsLocked = true,
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            FinalizedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        db.Users.AddRange(lecturer, presentStudent, absentStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = presentStudent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = absentStudent.Id, Status = "enrolled" });
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = presentStudent.Id,
                Status = "FINALIZED",
                TotalAiScore = 8.0m,
                TotalFinalScore = 8.5m,
                JoinedAt = DateTime.UtcNow.AddHours(-2)
            },
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = absentStudent.Id,
                Status = "ABSENT",
                TotalAiScore = 0m,
                TotalFinalScore = 0m,
                JoinedAt = DateTime.UtcNow.AddHours(-2)
            });
        await db.SaveChangesAsync();

        var handler = new GetMyInClassShiftsQueryHandler(db);
        var presentRes = await handler.Handle(new GetMyInClassShiftsQuery(presentStudent.Id), CancellationToken.None);
        presentRes.IsSuccess.Should().BeTrue();
        presentRes.Value.Single(s => s.Id == shift.Id).MyFinalScore.Should().Be(8.5m);

        var absentRes = await handler.Handle(new GetMyInClassShiftsQuery(absentStudent.Id), CancellationToken.None);
        absentRes.IsSuccess.Should().BeTrue();
        absentRes.Value.Single(s => s.Id == shift.Id).MyFinalScore.Should().BeNull();
    }

    [Fact(DisplayName = "M2-05: SpeechController, StorageController và MockExamsController đồng bộ RequestSizeLimit 26 MiB và chặn file > 25 MiB (413)")]
    public async Task AudioUploadEndpoints_Enforce26MiBRequestSizeLimit_AndMockExamsRejectsOver25MiB()
    {
        const long expectedLimit = 26 * 1024 * 1024;

        var speechLimit = typeof(SpeechController).GetMethod(nameof(SpeechController.TranscribeSpeech))!
            .GetCustomAttributesData()
            .SingleOrDefault(a => a.AttributeType == typeof(RequestSizeLimitAttribute));
        speechLimit.Should().NotBeNull();
        ((long)speechLimit!.ConstructorArguments[0].Value!).Should().Be(expectedLimit);

        var storageLimit = typeof(StorageController).GetMethod(nameof(StorageController.UploadAudio))!
            .GetCustomAttributesData()
            .SingleOrDefault(a => a.AttributeType == typeof(RequestSizeLimitAttribute));
        storageLimit.Should().NotBeNull();
        ((long)storageLimit!.ConstructorArguments[0].Value!).Should().Be(expectedLimit);

        var mockLimit = typeof(MockExamsController).GetMethod(nameof(MockExamsController.SubmitAnswer))!
            .GetCustomAttributesData()
            .SingleOrDefault(a => a.AttributeType == typeof(RequestSizeLimitAttribute));
        mockLimit.Should().NotBeNull();
        ((long)mockLimit!.ConstructorArguments[0].Value!).Should().Be(expectedLimit);

        var studentId = Guid.NewGuid();
        var senderMock = new Mock<ISender>(MockBehavior.Strict);
        var controller = new MockExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateStudentPrincipal(studentId) }
            }
        };

        var oversizeFileMock = new Mock<IFormFile>();
        oversizeFileMock.SetupGet(f => f.Length).Returns(25 * 1024 * 1024 + 1);

        var result = await controller.SubmitAnswer(
            Guid.NewGuid(),
            new SubmitMockExamAnswerFormRequest
            {
                QuestionId = Guid.NewGuid(),
                File = oversizeFileMock.Object,
                TimeTakenSeconds = 10
            },
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        senderMock.Verify(s => s.Send(It.IsAny<SubmitMockExamAnswerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "M2-06 (Probe 1A): Concurrent lock khi AI trả FollowUpPrompt=null trả TicketLocked và không sửa GradingJob.NextAttemptAt")]
    public async Task GenerateFollowUp_ConcurrentLockWithNullAiPrompt_ReturnsTicketLocked_AndLeavesDeferredJobUnmutated()
    {
        await using var db = CreateDbContext();
        var (student, _, ticket, question, submission, rawToken, _, _) =
            await SeedTicketAsync(db, ExamTicketStatus.InProgress, isLocked: false);

        var rubric = await db.Rubrics.SingleAsync(r => r.CourseId == question.CourseId);
        db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Layer separation",
            Description = "Describe dependency inversion",
            MaxScore = 10m,
            Weight = 1.0m,
            OrderIndex = 1
        });
        var deferredAt = DateTime.UtcNow.AddSeconds(120);
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = "pending",
            NextAttemptAt = deferredAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.GradingJobs.Add(job);
        await db.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                var trackedTicket = await db.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id, ct);
                trackedTicket.Status = ExamTicketStatus.Locked;
                trackedTicket.IsLocked = true;
                await db.SaveChangesAsync(ct);

                return new AiGradingResult
                {
                    Score = 9.0m,
                    ConfidenceScore = 0.95m,
                    Feedback = "Complete answer",
                    FollowUpPrompt = null
                };
            });

        var handler = new GenerateOfficialExamFollowUpCommandHandler(db, aiMock.Object);
        var res = await handler.Handle(
            new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, submission.Id, BoundDeviceId, rawToken),
            CancellationToken.None);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var jobAfter = await db.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        jobAfter.NextAttemptAt.Should().Be(deferredAt);

        var subAfter = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
        subAfter.GeneratedFollowUpPrompt.Should().BeNull();
        subAfter.GradingStatus.Should().Be("pending");
    }

    [Fact(DisplayName = "M2-07 (Probe 1A): Concurrent lock khi STT trả refusal/blank trả TicketLocked và không sửa GradingJob hoặc Submission")]
    public async Task GenerateFollowUp_ConcurrentLockDuringSttRefusalPath_ReturnsTicketLocked_AndLeavesStateUnmutated()
    {
        await using var db = CreateDbContext();
        var (student, _, ticket, question, submission, rawToken, questionAudioKey, _) =
            await SeedTicketAsync(db, ExamTicketStatus.InProgress, isLocked: false);

        var session = await db.OfficialExamSessions.SingleAsync();
        session.ExamInputMode = ExamInputMode.VoiceOnly;
        submission.TranscriptWhisper = string.Empty;
        submission.GradingStatus = "pending";
        var deferredAt = DateTime.UtcNow.AddSeconds(120);
        var job = new GradingJob
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            Status = "pending",
            NextAttemptAt = deferredAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.GradingJobs.Add(job);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.VerifySha256Async(questionAudioKey, ValidSha256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storageMock.Setup(s => s.OpenReadStreamAsync(questionAudioKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3, 4 }));

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream _, string _, string? _, CancellationToken ct) =>
            {
                var trackedTicket = await db.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id, ct);
                trackedTicket.Status = ExamTicketStatus.Locked;
                trackedTicket.IsLocked = true;
                await db.SaveChangesAsync(ct);

                return new SpeechTranscriptionResult(
                    Text: "em không biết",
                    RawText: "em không biết",
                    Provider: "Whisper",
                    UsedFallback: false,
                    TimestampsJson: null,
                    ConfidenceScore: 0.9);
            });

        var handler = new GenerateOfficialExamFollowUpCommandHandler(db, storageService: storageMock.Object, speechToTextService: sttMock.Object);
        var res = await handler.Handle(
            new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, submission.Id, BoundDeviceId, rawToken),
            CancellationToken.None);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be(OfficialExamErrors.TicketLocked);

        var jobAfter = await db.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        jobAfter.NextAttemptAt.Should().Be(deferredAt);

        var subAfter = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id);
        subAfter.TranscriptWhisper.Should().BeEmpty();
        subAfter.GradingStatus.Should().Be("pending");
    }

    [Fact(DisplayName = "M2-08 (Probe 1B): Concurrent lock trong vòng lặp polling JobInFlight trả TicketLocked thay vì 200 OK")]
    public async Task GenerateFollowUp_ConcurrentLockDuringJobInFlightPolling_ReturnsTicketLocked()
    {
        var dbName = $"m2-probe1b-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Guid studentId;
        Guid ticketId;
        Guid submissionId;
        string rawToken;

        await using (var seedDb = new OralExamDbContext(options))
        {
            var seeded = await SeedTicketAsync(seedDb, ExamTicketStatus.InProgress, isLocked: false);
            studentId = seeded.Student.Id;
            ticketId = seeded.Ticket.Id;
            submissionId = seeded.Submission.Id;
            rawToken = seeded.RawToken;

            seedDb.GradingJobs.Add(new GradingJob
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                Status = "processing",
                LockedUntil = DateTime.UtcNow.AddMinutes(2),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = new LockAfterPreClaimDbContext(options, ticketId, submissionId);
        var handler = new GenerateOfficialExamFollowUpCommandHandler(db);

        var res = await handler.Handle(
            new GenerateOfficialExamFollowUpCommand(studentId, ticketId, submissionId, BoundDeviceId, rawToken),
            CancellationToken.None);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be(OfficialExamErrors.TicketLocked);
    }

    [Fact(DisplayName = "M2-09 (Probe 1D): GetMyWorkstationTicket và GetOfficialExamTicketState ẩn Questions/FollowUps khi IN_PROGRESS + IsLocked=true")]
    public async Task TicketQueries_InProgressWithIsLockedTrue_SetIsKioskLocked_AndHideQuestionsAndFollowUps()
    {
        await using var db = CreateDbContext();
        var (student, _, ticket, question, submission, rawToken, _, _) =
            await SeedTicketAsync(db, ExamTicketStatus.InProgress, isLocked: true);

        submission.GeneratedFollowUpPrompt = "Hidden follow-up prompt";
        submission.AudioVerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var wsRes = await new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator())
            .Handle(
                new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", DateTime.UtcNow, BoundDeviceId, rawToken),
                CancellationToken.None);

        wsRes.IsSuccess.Should().BeTrue();
        wsRes.Value.IsKioskLocked.Should().BeTrue();
        wsRes.Value.ExamSetCode.Should().BeNull();
        wsRes.Value.Questions.Should().BeNull();
        wsRes.Value.FollowUps.Should().BeEmpty();

        var stateRes = await new GetOfficialExamTicketStateQueryHandler(db)
            .Handle(
                new GetOfficialExamTicketStateQuery(student.Id, ticket.Id, BoundDeviceId, rawToken),
                CancellationToken.None);

        stateRes.IsSuccess.Should().BeTrue();
        stateRes.Value.IsKioskLocked.Should().BeTrue();
        stateRes.Value.FollowUps.Should().BeEmpty();
    }

    [Fact(DisplayName = "M2-10 (Probe 3B-2): GetInClassShiftById ẩn TotalAiScore/TotalFinalScore khi participant có trạng thái ABSENT dù ca FINALIZED")]
    public async Task GetInClassShiftById_HidesParticipantScores_WhenParticipantIsAbsentOnFinalizedShift()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv-m2-detail@fpt.edu.vn", FullName = "GV M2", Role = UserRole.Lecturer, IsActive = true };
        var presentStudent = new User { Id = Guid.NewGuid(), Email = "sv-present-detail@fpt.edu.vn", FullName = "SV Present", StudentCode = "SE189021", Role = UserRole.Student, IsActive = true };
        var absentStudent = new User { Id = Guid.NewGuid(), Email = "sv-absent-detail@fpt.edu.vn", FullName = "SV Absent", StudentCode = "SE189022", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1802", Name = "SWD392 - SE1802", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Finalized M2 Detail",
            TotpSecretKey = "SECRET_M2_DETAIL",
            Status = "FINALIZED",
            IsLocked = true,
            TotalQuestionsPerStudent = 1,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            FinalizedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        db.Users.AddRange(lecturer, presentStudent, absentStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = presentStudent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = absentStudent.Id, Status = "enrolled" });
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = presentStudent.Id,
                Status = "FINALIZED",
                TotalAiScore = 8.0m,
                TotalFinalScore = 8.5m,
                JoinedAt = DateTime.UtcNow.AddHours(-2)
            },
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = absentStudent.Id,
                Status = "ABSENT",
                TotalAiScore = 0m,
                TotalFinalScore = 0m,
                JoinedAt = DateTime.UtcNow.AddHours(-2)
            });
        await db.SaveChangesAsync();

        var handler = new GetInClassShiftByIdQueryHandler(db);

        var absentView = await handler.Handle(new GetInClassShiftByIdQuery(absentStudent.Id, shift.Id), CancellationToken.None);
        absentView.IsSuccess.Should().BeTrue();
        var absentSelf = absentView.Value.Participants.Single(p => p.StudentId == absentStudent.Id);
        absentSelf.TotalAiScore.Should().BeNull();
        absentSelf.TotalFinalScore.Should().BeNull();

        var lecturerView = await handler.Handle(new GetInClassShiftByIdQuery(lecturer.Id, shift.Id), CancellationToken.None);
        lecturerView.IsSuccess.Should().BeTrue();
        var lecturerAbsentRow = lecturerView.Value.Participants.Single(p => p.StudentId == absentStudent.Id);
        lecturerAbsentRow.TotalAiScore.Should().BeNull();
        lecturerAbsentRow.TotalFinalScore.Should().BeNull();
        var lecturerPresentRow = lecturerView.Value.Participants.Single(p => p.StudentId == presentStudent.Id);
        lecturerPresentRow.TotalAiScore.Should().Be(8.0m);
        lecturerPresentRow.TotalFinalScore.Should().Be(8.5m);
    }

    private sealed class LockAfterPreClaimDbContext(
        DbContextOptions<OralExamDbContext> options,
        Guid ticketId,
        Guid submissionId)
        : OralExamDbContext(options), IApplicationDbContext
    {
        private bool _lockedAfterPreClaim;

        async Task<T> IApplicationDbContext.ExecuteTicketDecisionAsync<T>(
            Guid requestedTicketId,
            Func<CancellationToken, Task<T>> decision,
            CancellationToken cancellationToken)
        {
            var result = await base.ExecuteTicketDecisionAsync(requestedTicketId, decision, cancellationToken);
            if (!_lockedAfterPreClaim && requestedTicketId == ticketId)
            {
                _lockedAfterPreClaim = true;
                var trackedTicket = await StudentExamTickets.SingleAsync(t => t.Id == ticketId, cancellationToken);
                trackedTicket.Status = ExamTicketStatus.Locked;
                trackedTicket.IsLocked = true;
                var trackedSubmission = await ExamQuestionSubmissions.SingleAsync(s => s.Id == submissionId, cancellationToken);
                trackedSubmission.GeneratedFollowUpPrompt = "Concurrent prompt after lock";
                await SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class SingleQueueTestChannel : IGradingQueueChannel
    {
        private readonly Channel<GradingTask> _channel = Channel.CreateUnbounded<GradingTask>();

        public int Count => _channel.Reader.Count;
        public int Capacity => 1000;

        public ValueTask EnqueueAsync(GradingTask task, CancellationToken cancellationToken = default) =>
            _channel.Writer.WriteAsync(task, cancellationToken);

        public void Complete() => _channel.Writer.TryComplete();

        public IAsyncEnumerable<GradingTask> ReadAllAsync(CancellationToken cancellationToken = default) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

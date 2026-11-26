using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.API.Configuration;
using API.Controllers;
using API.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.Commands.ImportUsers;
using OralExamination.Application.Features.Admin.Commands.UpdateUser;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Auth.Commands.GoogleLogin;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Application.Features.Auth.Commands.SignOut;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;
using OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;
using OralExamination.Application.Features.OfficialExams.Commands.CreateQuestionAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.GenerateOfficialExamFollowUp;
using OralExamination.Application.Features.OfficialExams.Commands.LogAntiCheatEvent;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;
using OralExamination.Application.Features.OfficialExams.Commands.UnlockExamTicketDevice;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamTicketState;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Identity;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features;

public sealed class PostPr42HardeningRegressionTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    // =========================================================================
    // ĐỢT A — ADMIN BULK USER IMPORT & DB SCHEMA / RECONCILER PARITY
    // =========================================================================

    [Fact(DisplayName = "DotA-01: Model EF mở rộng users.username lên 50 + thêm index active SetCode; UpdateUser cho SV <= 50 nhưng vẫn chặn staff > 32")]
    public void DotA_01_UsersUsernameColumnMaxLength50_ExamSetCodeIndex_AndUpdateUserValidator()
    {
        using var db = CreateDbContext();

        var userEntity = db.Model.FindEntityType(typeof(User))!;
        userEntity.FindProperty(nameof(User.Username))!.GetMaxLength().Should().Be(StudentUsername.MaxLength);

        var examSetEntity = db.Model.FindEntityType(typeof(ExamSet))!;
        examSetEntity.GetIndexes().Should().Contain(idx =>
            idx.GetDatabaseName() == "uq_exam_sets_active_course_set_code"
            && idx.IsUnique
            && idx.GetFilter() == "is_active = true");

        var validator = new UpdateUserCommandValidator();

        var longStudentUsername = new string('a', 45);
        var studentCmd = new UpdateUserCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "longstudent@fpt.edu.vn",
            "Nguyễn Quang Thành Long",
            longStudentUsername,
            "SE180123");
        validator.Validate(studentCmd).IsValid.Should().BeTrue();

        var staffOver32Cmd = new UpdateUserCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "staff33@fpt.edu.vn",
            "Giảng Viên Test",
            new string('u', 33),
            null);
        validator.Validate(staffOver32Cmd).IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "DotA-02: AdminUserImportEvaluator chặn tiền tố công thức Excel trong FullName (=, +, -, @)")]
    [InlineData("=SUM(1,1)")]
    [InlineData("+CMD|' /C calc'!A0")]
    [InlineData("-1+1")]
    [InlineData("@HYPERLINK(\"http://evil\")")]
    public async Task DotA_02_AdminUserImportPreview_RejectsExcelFormulaPrefixInFullName(string maliciousName)
    {
        await using var db = CreateDbContext();
        var csv = $"Email,FullName,Role,StudentCode\nsv.formula@fpt.edu.vn,\"{maliciousName.Replace("\"", "\"\"")}\",student,SE180999\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var handler = new PreviewAdminUsersImportQueryHandler(db, new AdminUserSpreadsheetService());
        var result = await handler.Handle(
            new PreviewAdminUsersImportQuery(stream, "users.csv", stream.Length),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValidCount.Should().Be(0);
        result.Value.InvalidCount.Should().Be(1);
        result.Value.Rows.Should().ContainSingle(r =>
            !r.IsValid && r.ErrorMessage != null && r.ErrorMessage.Contains("không được bắt đầu"));
    }

    [Fact(DisplayName = "DotA-03: AbuseProtection đặt ExpensiveRequestSeconds >= 135 và định tuyến import + mock-exam answers vào bucket upload")]
    public void DotA_03_AbuseProtection_RoutesImportAndMockExamAnswerToUpload_AndExpensiveTimeoutExceedsPolly()
    {
        var defaults = new AbuseProtectionOptions();
        defaults.ExpensiveRequestSeconds.Should().BeGreaterThanOrEqualTo(135);

        static HttpContext BuildPost(string path)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Path = path;
            return ctx;
        }

        AbuseProtection.Category(BuildPost("/api/v1/admin/users/import/commit")).Should().Be("upload");
        AbuseProtection.Category(BuildPost("/api/v1/admin/users/import/preview")).Should().Be("upload");
        AbuseProtection.Category(BuildPost("/api/v1/official-exams/sessions/00000000-0000-0000-0000-000000000001/candidates/import/commit"))
            .Should().Be("upload");
        AbuseProtection.Category(BuildPost("/api/v1/mock-exams/sessions/00000000-0000-0000-0000-000000000001/answers"))
            .Should().Be("upload");
    }

    [Fact(DisplayName = "DotA-04: PostgresSchemaReconciler có đủ grades_published_at, paused_at, varchar(50), FK parity và an toàn composite format")]
    public void DotA_04_PostgresSchemaReconciler_HasParityAndCompositeFormatSafety()
    {
        var postSql = PostgresSchemaReconciler.GetPostMigrationSql();

        postSql.Should().Contain("grades_published_at");
        postSql.Should().Contain("paused_at");
        postSql.Should().Contain("character varying(50)");
        postSql.Should().Contain("uq_exam_sets_active_course_set_code");
        postSql.Should().Contain("ix_question_review_request_items_reviewed_by");
        postSql.Should().Contain("REFERENCES users(id) ON DELETE RESTRICT");
        postSql.Should().Contain("REFERENCES exam_question_submissions(id) ON DELETE SET NULL");

        Action format = () => _ = string.Format(CultureInfo.InvariantCulture, postSql, Array.Empty<object>());
        format.Should().NotThrow();
    }

    // =========================================================================
    // ĐỢT B — P0-01 BYOD, P1-07 CHECK-IN, MF-04 CAPACITY, MF-05 FOLLOW-UP
    // =========================================================================

    [Fact(DisplayName = "DotB-01 (P0-01): Vé IN_PROGRESS sau khi Giám thị điểm danh (DeviceId=null) bị chặn đọc đề, FollowUps và toàn bộ 7 hành động thi đến khi SV Check-in gắn thiết bị")]
    public async Task DotB_01_P001_UnboundInProgressTicket_BlockedUntilStudentChecksInWithDevice()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.p001@fpt.edu.vn",
            FullName = "SV P0-01",
            StudentCode = "SE181111",
            Role = UserRole.Student,
            IsActive = true
        };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var question = new ExamQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Q1 Secret",
            Content = "Explain CAP theorem",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var examSet = new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SetCode = "P001-SET",
            Status = ExamSetStatus.HandedOver,
            IsActive = true
        };
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = structure.Id,
            Title = "FE PRN231",
            ExamDate = DateOnly.FromDateTime(now),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            Status = "in_progress"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "Lab 101",
            StartTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(50),
            Status = ExamShiftWindow.InProgress,
            IsExamPublished = true
        };
        // Trạng thái ngay sau khi Giám thị điểm danh: IN_PROGRESS nhưng chưa gắn DeviceId/SessionToken!
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = examSet.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.InProgress,
            DeviceId = null,
            DeviceSessionTokenHash = null,
            DeviceUnlockCount = 0
        };
        var existingSub = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = question.Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            FollowUpPrompt = "Follow-up secret?",
            TranscriptWhisper = "Initial answer",
            GradingStatus = "pending",
            SubmittedAt = now
        };

        db.Users.Add(student);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamQuestions.Add(question);
        db.ExamSets.Add(examSet);
        db.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSet.Id, ExamQuestionId = question.Id, OrderIndex = 1 });
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(existingSub);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IStorageService>();
        var notifierMock = new Mock<IAntiCheatNotifier>();
        var dummySha = new string('a', 64);
        var dummyKey = OfficialExamAudioObjectKey.Create(shift.Id, ticket.Id, 1, student.StudentCode);

        // 1. Queries MUST hide questions and follow-ups when ticket has no bound device/session
        var wsBefore = await new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now), CancellationToken.None);
        wsBefore.IsSuccess.Should().BeTrue();
        wsBefore.Value.Questions.Should().BeNull();
        wsBefore.Value.FollowUps.Should().BeNullOrEmpty();

        var stateBefore = await new GetOfficialExamTicketStateQueryHandler(db)
            .Handle(new GetOfficialExamTicketStateQuery(student.Id, ticket.Id), CancellationToken.None);
        stateBefore.IsSuccess.Should().BeTrue();
        stateBefore.Value.FollowUps.Should().BeNullOrEmpty();

        // 2. All 6 exam command handlers + student anti-cheat MUST reject with DeviceMismatch
        (await new CreateAudioUploadAuthorizationCommandHandler(db, storageMock.Object)
            .Handle(new CreateAudioUploadAuthorizationCommand(student.Id, ticket.Id, "10.0.0.1", now), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new CreateQuestionAudioUploadAuthorizationCommandHandler(db, storageMock.Object)
            .Handle(new CreateQuestionAudioUploadAuthorizationCommand(student.Id, ticket.Id, question.Id, 0, null, null, null, now), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new SealOfficialExamAudioCommandHandler(db, storageMock.Object)
            .Handle(new SealOfficialExamAudioCommand(student.Id, ticket.Id, "10.0.0.1", dummyKey, dummySha), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new SubmitOfficialQuestionAnswerCommandHandler(db, storageMock.Object)
            .Handle(new SubmitOfficialQuestionAnswerCommand(student.Id, ticket.Id, question.Id, 0, null, dummyKey, dummySha, "init", "init", "[]", 15, false, null, null), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new GenerateOfficialExamFollowUpCommandHandler(db)
            .Handle(new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, existingSub.Id, null, null), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new SubmitOfficialExamCommandHandler(db)
            .Handle(new SubmitOfficialExamCommand(student.Id, ticket.Id, "10.0.0.1", [new OfficialExamAnswerSubmissionRequest(question.Id, "ans", "[]", 10)]), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        (await new LogAntiCheatEventCommandHandler(db, notifierMock.Object)
            .Handle(new LogAntiCheatEventCommand(student.Id, ticket.Id, shift.Id, student.Id, AntiCheatEventType.WindowBlur, AntiCheatSeverity.Low, "blur"), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.DeviceMismatch);

        // 3. Sau khi SV thực hiện CheckInExamTicket với X-Device-Id -> mở đề & chỉ mở khi kèm đúng token
        const string laptopA = "BYOD-P001-LAPTOP-A001";
        var checkInRes = await new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator())
            .Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.1", now, laptopA, "1.0.0"), CancellationToken.None);
        checkInRes.IsSuccess.Should().BeTrue(checkInRes.Error);
        checkInRes.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
        var activeToken = checkInRes.Value.ActiveSessionToken!;

        var wsValid = await new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now, laptopA, activeToken), CancellationToken.None);
        wsValid.IsSuccess.Should().BeTrue();
        wsValid.Value.Questions.Should().NotBeNull().And.ContainSingle(q => q.QuestionId == question.Id);

        var wsWrongToken = await new GetMyWorkstationTicketQueryHandler(db, new GetMyWorkstationTicketQueryValidator())
            .Handle(new GetMyWorkstationTicketQuery(student.Id, "10.0.0.1", now, laptopA, "wrong-session-token"), CancellationToken.None);
        wsWrongToken.IsSuccess.Should().BeFalse();
        wsWrongToken.Error.Should().Be(OfficialExamErrors.ActiveSessionConflict);
    }

    [Fact(DisplayName = "DotB-02 (P1-07): CheckInExamTicket phân biệt AuditLog KIOSK_DEVICE_BOUND lần đầu và KIOSK_DEVICE_REBOUND sau khi Khảo thí mở khóa thiết bị")]
    public async Task DotB_02_P107_CheckInExamTicket_LogsBoundThenReboundAuditAction()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var student = new User { Id = Guid.NewGuid(), Email = "sv.rebound@fpt.edu.vn", FullName = "SV Rebound", StudentCode = "SE181222", Role = UserRole.Student, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.rebound@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWP391", Name = "Capstone", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "RB-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE", ExamDate = DateOnly.FromDateTime(now) };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = now.AddMinutes(-5), EndTime = now.AddMinutes(55), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, ExamSetId = examSet.Id, SeatNumber = 1, Status = ExamTicketStatus.Scheduled };

        db.Users.AddRange(student, officer);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamSets.Add(examSet);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var checkInHandler = new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator());
        var first = await checkInHandler.Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.1", now, "BYOD-LAPTOP-FIRST-001"), CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Error);

        var unlock = await new UnlockExamTicketDeviceCommandHandler(db)
            .Handle(new UnlockExamTicketDeviceCommand(officer.Id, ticket.Id, "Laptop bị tắt nguồn đột ngột, chuyển máy dự phòng."), CancellationToken.None);
        unlock.IsSuccess.Should().BeTrue(unlock.Error);

        var second = await checkInHandler.Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.2", now.AddMinutes(1), "BYOD-LAPTOP-SECOND-02"), CancellationToken.None);
        second.IsSuccess.Should().BeTrue(second.Error);

        var auditActions = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityId == ticket.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.Action)
            .ToListAsync();

        auditActions.Should().Contain("KIOSK_DEVICE_BOUND");
        auditActions.Should().Contain("KIOSK_DEVICE_REBOUND");
    }

    [Fact(DisplayName = "DotB-03 (MF-04 Capacity Parity): Phòng MaxSeats=30 được ước tính 30 chỗ và AutoAllocate chia đúng 30 SV không lệch 24 vs 40")]
    public async Task DotB_03_Mf04CapacityParity_EstimateAndAutoAllocateAgreeOnMaxSeats30()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var officer = new User { Id = Guid.NewGuid(), Email = "eo.cap@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", SemesterId = semester.Id, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, Title = "FE", ExamDate = DateOnly.FromDateTime(now.AddDays(1)), ExamAttemptType = OfficialExamAttemptType.FinalExam };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 - Lab 30",
            RoomLab = "Lab 30",
            MaxSeats = 30,
            StartTime = now.AddDays(1),
            EndTime = now.AddDays(1).AddHours(2),
            Status = "scheduled"
        };

        db.Users.Add(officer);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);

        for (var i = 1; i <= 30; i++)
        {
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"sv.cap{i:D2}@fpt.edu.vn",
                FullName = $"SV {i}",
                StudentCode = $"SE18{i:D4}",
                Role = UserRole.Student,
                IsActive = true
            };
            db.Users.Add(student);
            db.OfficialExamCandidates.Add(new OfficialExamCandidate
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                StudentId = student.Id,
                ImportedBy = officer.Id,
                ImportedAt = now
            });
        }

        await db.SaveChangesAsync();

        var estimate = await new GetOfficialExamEnrollmentEstimateQueryHandler(db)
            .Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        estimate.IsSuccess.Should().BeTrue(estimate.Error);
        estimate.Value.CurrentTotalCapacity.Should().Be(30);
        estimate.Value.CurrentAvailableSeats.Should().Be(30);

        var allocated = await new AutoAllocateSessionStudentsCommandHandler(db)
            .Handle(new AutoAllocateSessionStudentsCommand(officer.Id, session.Id, null, false, false), CancellationToken.None);
        allocated.IsSuccess.Should().BeTrue(allocated.Error);
        allocated.Value.NewlyAllocatedCount.Should().Be(30);
    }

    [Fact(DisplayName = "DotB-04 (MF-05 Multi-Question Follow-up): Hoàn thành max follow-up ở Câu 1 KHÔNG khóa sớm participant sang SUBMITTED khi Câu 2 vẫn còn lượt follow-up")]
    public async Task DotB_04_Mf05MultiQuestionFollowUp_DoesNotLockParticipantPrematurely()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.fu@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.fu@fpt.edu.vn", FullName = "SV", StudentCode = "SE182001", Role = UserRole.Student, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", SemesterId = semester.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "Class 1", JoinCode = "JOIN01", CourseId = course.Id, SemesterId = semester.Id, LecturerId = lecturer.Id, IsActive = true };
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Explain DI", Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var q2 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q2", Content = "Explain EF Core", Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Progress Test 2Q",
            StartTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(50),
            ExamDurationMinutes = 40,
            TotalQuestionsPerStudent = 2,
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = q1.Id,
            AssignedQuestionIdsJson = $"[\"{q1.Id}\",\"{q2.Id}\"]",
            Status = "IN_PROGRESS",
            JoinedAt = now.AddMinutes(-5)
        };

        db.Users.AddRange(lecturer, student);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student.Id, Status = "enrolled", EnrolledAt = now.AddDays(-1) });
        db.PracticeQuestions.AddRange(q1, q2);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        await db.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        var handler = new SubmitInClassShiftAnswerCommandHandler(db, queueMock.Object, new SubmitInClassShiftAnswerCommandValidator());

        // Nộp Q1 main & Q2 main -> vẫn IN_PROGRESS
        var rQ1 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q1.Id, "Dependency Injection giúp giảm coupling."), CancellationToken.None);
        var rQ2 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q2.Id, "EF Core là ORM cho .NET."), CancellationToken.None);
        rQ1.IsSuccess.Should().BeTrue(rQ1.Error);
        rQ2.IsSuccess.Should().BeTrue(rQ2.Error);
        (await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id)).Status.Should().Be("IN_PROGRESS");

        // Giả lập AI đã sinh FollowUpPrompt cho cả Q1 và Q2
        var subQ1 = await db.InClassExamSubmissions.SingleAsync(s => s.Id == rQ1.Value.SubmissionId);
        var subQ2 = await db.InClassExamSubmissions.SingleAsync(s => s.Id == rQ2.Value.SubmissionId);
        subQ1.FollowUpPrompt = "Phân biệt Scoped và Transient?";
        subQ2.FollowUpPrompt = "Phân biệt AsNoTracking và Tracking?";
        await db.SaveChangesAsync();

        // Nộp đủ 2 lượt follow-up cho Q1 -> Q2 CHƯA trả lời follow-up nên participant PHẢI giữ IN_PROGRESS!
        var rQ1Fu1 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q1.Id, "Scoped sống theo request.", true, subQ1.Id, 1), CancellationToken.None);
        rQ1Fu1.IsSuccess.Should().BeTrue(rQ1Fu1.Error);
        var subQ1Fu1 = await db.InClassExamSubmissions.SingleAsync(s => s.Id == rQ1Fu1.Value.SubmissionId);
        subQ1Fu1.FollowUpPrompt = "Singleton inject Scoped gây lỗi gì?";
        await db.SaveChangesAsync();

        var rQ1Fu2 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q1.Id, "Captive dependency.", true, subQ1Fu1.Id, 2), CancellationToken.None);
        rQ1Fu2.IsSuccess.Should().BeTrue(rQ1Fu2.Error);
        (await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id)).Status
            .Should().Be("IN_PROGRESS", "Câu 2 vẫn còn lượt follow-up chưa trả lời nên tuyệt đối không khóa sớm sang SUBMITTED");

        // Nộp tiếp 2 lượt follow-up cho Q2 -> lúc này cả Q1 & Q2 đều đạt trần max follow-up -> chuyển SUBMITTED!
        var rQ2Fu1 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q2.Id, "AsNoTracking không theo dõi change tracker.", true, subQ2.Id, 1), CancellationToken.None);
        rQ2Fu1.IsSuccess.Should().BeTrue(rQ2Fu1.Error);
        var subQ2Fu1 = await db.InClassExamSubmissions.SingleAsync(s => s.Id == rQ2Fu1.Value.SubmissionId);
        subQ2Fu1.FollowUpPrompt = "Khi nào dùng SplitQuery?";
        await db.SaveChangesAsync();

        var rQ2Fu2 = await handler.Handle(new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, q2.Id, "Khi Include nhiều collection tránh Cartesian explosion.", true, subQ2Fu1.Id, 2), CancellationToken.None);
        rQ2Fu2.IsSuccess.Should().BeTrue(rQ2Fu2.Error);
        (await db.InClassShiftParticipants.AsNoTracking().SingleAsync(p => p.Id == participant.Id)).Status.Should().Be("SUBMITTED");
    }

    // =========================================================================
    // ĐỢT C — AUTH, RBAC, MOCK DURATION, COURSE OFFERING, APPEALS, MF-05 GET
    // =========================================================================

    [Fact(DisplayName = "DotC-01: PasswordLoginCommandValidator chấp nhận email > 50 ký tự (tới 255) và SignOut tăng AuthVersion")]
    public async Task DotC_01_PasswordLoginLongEmail_AndSignOutBumpsAuthVersion()
    {
        var loginValidator = new PasswordLoginCommandValidator();
        var longEmail = "very.long.student.email.address.for.testing.login.2026@fpt.edu.vn";
        longEmail.Length.Should().BeGreaterThan(50);
        loginValidator.Validate(new PasswordLoginCommand(longEmail, "ValidPass#123")).IsValid.Should().BeTrue();

        await using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "signout@fpt.edu.vn", FullName = "SO", Role = UserRole.Student, IsActive = true, AuthVersion = 3 };
        const string rawRefresh = "raw-refresh-token-for-signout-test-1234567890";
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = GoogleLoginCommandHandler.HashToken(rawRefresh),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        var signOutRes = await new SignOutCommandHandler(db, new SignOutCommandValidator())
            .Handle(new SignOutCommand(rawRefresh), CancellationToken.None);
        signOutRes.IsSuccess.Should().BeTrue();

        var reloadedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        reloadedUser.AuthVersion.Should().Be(4, "SignOut phải tăng AuthVersion để thu hồi tức thì access token cũ");
        (await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).RevokedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "DotC-02: SpeechController và StorageController loại trừ vai trò proctor")]
    public void DotC_02_SpeechAndStorageControllers_ExcludeProctorRole()
    {
        foreach (var controllerType in new[] { typeof(SpeechController), typeof(StorageController) })
        {
            var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
            authorize.Should().NotBeNull();
            authorize!.Roles.Should().NotBeNullOrWhiteSpace();
            var roles = authorize.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            roles.Should().NotContain(UserRole.Proctor);
            roles.Should().Contain(UserRole.Student);
            roles.Should().Contain(UserRole.Lecturer);
            roles.Should().Contain(UserRole.ExaminationOfficer);
        }
    }

    [Fact(DisplayName = "DotC-03: GetMockExamSessionDetail không phình DurationMinutes sau khi tạm dừng và tiếp tục")]
    public async Task DotC_03_GetMockExamSessionDetail_PreservesBaseDurationAfterPauseResume()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var student = new User { Id = Guid.NewGuid(), Email = "sv.mock@fpt.edu.vn", FullName = "SV", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", MockExamDurationMinutes = 15, IsActive = true };
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            HasFollowUp = false,
            StartedAt = now.AddMinutes(-35),
            ExpiresAt = now.AddMinutes(-5), // 30 phút wall-clock do cộng bù 15 phút pause
            SubmittedAt = now.AddMinutes(-6),
            Status = "graded",
            TotalScore = 8.0m
        };
        db.Users.Add(student);
        db.Courses.Add(course);
        db.MockExamSessions.Add(session);
        await db.SaveChangesAsync();

        var detail = await new GetMockExamSessionDetailQueryHandler(db)
            .Handle(new GetMockExamSessionDetailQuery(session.Id, student.Id, IsElevatedRole: false, AttemptLazyGrading: false), CancellationToken.None);

        detail.IsSuccess.Should().BeTrue(detail.Error);
        detail.Value.DurationMinutes.Should().Be(15);
    }

    [Fact(DisplayName = "DotC-04: CreateOfficialExamSession hỗ trợ truyền SemesterId cho CourseOffering đa học kỳ và chặn học kỳ chưa mở offering")]
    public async Task DotC_04_CreateOfficialExamSession_SupportsMultiSemesterOffering()
    {
        await using var db = CreateDbContext();

        var officer = new User { Id = Guid.NewGuid(), Email = "eo.sem@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var semA = new Semester { Id = Guid.NewGuid(), Code = "SU26", Name = "Summer 2026", StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 8, 31), IsActive = true };
        var semB = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var semC = new Semester { Id = Guid.NewGuid(), Code = "SP27", Name = "Spring 2027", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 4, 30), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", SemesterId = semA.Id, IsActive = true };
        var offeringB = new CourseOffering { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semB.Id, IsEnabled = true };

        db.Users.Add(officer);
        db.Semesters.AddRange(semA, semB, semC);
        db.Courses.Add(course);
        db.CourseOfferings.Add(offeringB);
        await db.SaveChangesAsync();

        var handler = new CreateOfficialExamSessionCommandHandler(db, new CreateOfficialExamSessionCommandValidator());

        var validB = await handler.Handle(new CreateOfficialExamSessionCommand(
            officer.Id, course.Id, null, "FE FA26", new DateOnly(2026, 11, 20), false, 2, ExamInputMode.VoiceOnly, 60, OfficialExamAttemptType.FinalExam, semB.Id), CancellationToken.None);
        validB.IsSuccess.Should().BeTrue(validB.Error);
        validB.Value.SemesterId.Should().Be(semB.Id);

        var invalidC = await handler.Handle(new CreateOfficialExamSessionCommand(
            officer.Id, course.Id, null, "FE SP27", new DateOnly(2027, 3, 20), false, 2, ExamInputMode.VoiceOnly, 60, OfficialExamAttemptType.FinalExam, semC.Id), CancellationToken.None);
        invalidC.IsSuccess.Should().BeFalse();
        invalidC.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "DotC-05: CreateAppeal & BatchAssignAppeals trả đúng tiền tố NOT_FOUND:/FORBIDDEN: cho RFC 7807")]
    public async Task DotC_05_Appeals_ReturnStructuredPrefixes()
    {
        await using var db = CreateDbContext();
        var student1 = new User { Id = Guid.NewGuid(), Email = "sv1.ap@fpt.edu.vn", FullName = "SV1", Role = UserRole.Student, IsActive = true };
        var student2 = new User { Id = Guid.NewGuid(), Email = "sv2.ap@fpt.edu.vn", FullName = "SV2", Role = UserRole.Student, IsActive = true };
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.ap@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = Guid.NewGuid(), Title = "FE Appeal", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = DateTime.UtcNow.AddHours(-3), EndTime = DateTime.UtcNow.AddHours(-1), Status = "completed" };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student1.Id, SeatNumber = 1, Status = ExamTicketStatus.Published, IsLocked = true };
        db.Users.AddRange(student1, student2, officer);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var createHandler = new CreateAppealCommandHandler(db);
        var notFound = await createHandler.Handle(new CreateAppealCommand(student1.Id, Guid.NewGuid(), null, "Lý do phúc khảo hợp lệ."), CancellationToken.None);
        notFound.IsSuccess.Should().BeFalse();
        notFound.Error.Should().StartWith("NOT_FOUND:");

        var forbidden = await createHandler.Handle(new CreateAppealCommand(student2.Id, ticket.Id, null, "Lý do phúc khảo hợp lệ."), CancellationToken.None);
        forbidden.IsSuccess.Should().BeFalse();
        forbidden.Error.Should().StartWith("FORBIDDEN:");

        var batchHandler = new BatchAssignAppealsCommandHandler(db, new BatchAssignAppealsCommandValidator());
        var missingAppeal = await batchHandler.Handle(
            new BatchAssignAppealsCommand(officer.Id, [new AppealAssignmentItemDto { AppealId = Guid.NewGuid(), LecturerId = Guid.NewGuid() }]),
            CancellationToken.None);
        missingAppeal.IsSuccess.Should().BeFalse();
        missingAppeal.Error.Should().StartWith("NOT_FOUND:");
    }

    [Fact(DisplayName = "DotC-06: Admin UpdateCourse không ghi đè MockExamDurationMinutes và MockExamTotalQuestions của Trưởng Bộ Môn")]
    public async Task DotC_06_AdminUpdateCourse_PreservesDepartmentHeadMockExamConfig()
    {
        await using var db = CreateDbContext();
        var sem = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "PRN",
            Credits = 3,
            SemesterId = sem.Id,
            MaxMockExamsPerDay = 3,
            MockExamDurationMinutes = 45,
            MockExamTotalQuestions = 8,
            IsActive = true
        };
        db.Semesters.Add(sem);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var result = await new UpdateCourseCommandHandler(db).Handle(new UpdateCourseCommand(
            CourseId: course.Id,
            Code: "PRN231",
            Name: "PRN Updated",
            Credits: 3,
            MaxMockExamsPerDay: 6,
            MockExamDurationMinutes: 20,
            MockExamTotalQuestions: 5,
            IsActive: true,
            SemesterId: sem.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.MaxMockExamsPerDay.Should().Be(6);
        result.Value.MockExamDurationMinutes.Should().Be(45);
        result.Value.MockExamTotalQuestions.Should().Be(8);
    }

    [Fact(DisplayName = "DotC-07: GetMyInClassShifts không ghi DB trên GET, chiếu trạng thái CLOSED cho ca hết giờ và kẹp ExamEndsAt <= EndTime")]
    public async Task DotC_07_GetMyInClassShifts_PureReadAndClampedExamEndsAt()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.my@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.my@fpt.edu.vn", FullName = "SV", StudentCode = "SE183001", Role = UserRole.Student, IsActive = true };
        var sem = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", SemesterId = sem.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1802", Name = "Class 2", JoinCode = "JOIN02", CourseId = course.Id, SemesterId = sem.Id, LecturerId = lecturer.Id, IsActive = true };

        var expiredShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca hết hạn",
            StartTime = now.AddHours(-2),
            EndTime = now.AddMinutes(-10),
            ExamDurationMinutes = 30,
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "IN_PROGRESS"
        };
        var activeShiftEndTime = now.AddMinutes(5);
        var activeShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đang thi sắp đóng",
            StartTime = now.AddMinutes(-20),
            EndTime = activeShiftEndTime,
            ExamDurationMinutes = 45, // JoinedAt + 45m > EndTime -> phải kẹp về activeShiftEndTime!
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "IN_PROGRESS"
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = activeShift.Id,
            StudentId = student.Id,
            Status = "IN_PROGRESS",
            JoinedAt = now.AddMinutes(-2)
        };

        db.Users.AddRange(lecturer, student);
        db.Semesters.Add(sem);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.Add(new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = student.Id, Status = "enrolled", EnrolledAt = now.AddDays(-1) });
        db.InClassExamShifts.AddRange(expiredShift, activeShift);
        db.InClassShiftParticipants.Add(participant);
        await db.SaveChangesAsync();

        var result = await new GetMyInClassShiftsQueryHandler(db)
            .Handle(new GetMyInClassShiftsQuery(student.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var expiredDto = result.Value.Single(s => s.Id == expiredShift.Id);
        expiredDto.Status.Should().Be("CLOSED");

        // Xác nhận GET không đột biến DB
        (await db.InClassExamShifts.AsNoTracking().SingleAsync(s => s.Id == expiredShift.Id)).Status.Should().Be("IN_PROGRESS");

        var activeDto = result.Value.Single(s => s.Id == activeShift.Id);
        activeDto.MyExamEndsAt.Should().NotBeNull();
        activeDto.MyExamEndsAt!.Value.Should().BeOnOrBefore(activeShiftEndTime.AddSeconds(1));
    }

    [Fact(DisplayName = "DotC-08: GetNextInClassFollowUp dừng ngay không gọi lặp AI khi bài đã AI_GRADED và không sinh FollowUpPrompt")]
    public async Task DotC_08_GetNextInClassFollowUp_ShortCircuitsWithoutCallingAiWhenAlreadyGradedWithNoPrompt()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.sc@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.sc@fpt.edu.vn", FullName = "SV", StudentCode = "SE184001", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1803", Name = "Class 3", JoinCode = "JOIN03", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var question = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = Guid.NewGuid(), Title = "Q", Content = "Content", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Follow-up",
            StartTime = now.AddMinutes(-10),
            EndTime = now.AddMinutes(50),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "IN_PROGRESS"
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            Status = "IN_PROGRESS",
            JoinedAt = now.AddMinutes(-5)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Câu trả lời rất đầy đủ đạt 9.5 điểm.",
            Status = "AI_GRADED",
            AiScore = 9.5m,
            FollowUpPrompt = null,
            SubmittedAt = now.AddMinutes(-1)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.PracticeQuestions.Add(question);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        var res = await new GetNextInClassFollowUpQueryHandler(db, aiMock.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, submission.Id), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.HasMoreFollowUp.Should().BeFalse();
        aiMock.Verify(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // RED-TEAM REMEDIATION REGRESSION TESTS (RT-01 .. RT-06)
    // =========================================================================

    [Fact(DisplayName = "RT-01: SignOut thu hồi toàn bộ RefreshToken còn hiệu lực của cùng user khi tăng AuthVersion")]
    public async Task RT_01_SignOut_RevokesAllActiveSiblingRefreshTokensOfUser()
    {
        await using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "rt01@fpt.edu.vn", FullName = "RT01", Role = UserRole.Student, IsActive = true, AuthVersion = 1 };
        const string rawTokenA = "raw-refresh-token-device-a-1234567890abcdef";
        const string rawTokenB = "raw-refresh-token-device-b-1234567890abcdef";
        var tokenA = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = GoogleLoginCommandHandler.HashToken(rawTokenA), ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow };
        var tokenB = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = GoogleLoginCommandHandler.HashToken(rawTokenB), ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow };

        db.Users.Add(user);
        db.RefreshTokens.AddRange(tokenA, tokenB);
        await db.SaveChangesAsync();

        var res = await new SignOutCommandHandler(db, new SignOutCommandValidator())
            .Handle(new SignOutCommand(rawTokenA), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).AuthVersion.Should().Be(2);
        (await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == tokenA.Id)).RevokedAt.Should().NotBeNull();
        (await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == tokenB.Id)).RevokedAt.Should().NotBeNull(
            "SignOut phải thu hồi cả các RefreshToken anh em để tránh split-brain với AuthVersion");
    }

    [Fact(DisplayName = "RT-02: AdminUserImportEvaluator chặn Excel formula trên Email, Username và khoảng trắng Unicode (NBSP); SafeExcelText thoát công thức NBSP")]
    public async Task RT_02_AdminUserImport_RejectsFormulaInjectionAcrossEmailUsernameAndNbsp()
    {
        await using var db = CreateDbContext();
        var csv = string.Join("\n",
            "Email,FullName,Role,StudentCode,Username",
            "=1+1@fpt.edu.vn,Nguyen Van An,lecturer,,an_nv",
            "+cmd@fpt.edu.vn,Nguyen Van Binh,lecturer,,binh_nv",
            "staff.dash@fpt.edu.vn,Tran Van Cuong,lecturer,,-cuongtv",
            "nbsp.formula@fpt.edu.vn,\"\u00A0=SUM(1,1)\",lecturer,,nbspuser") + "\n";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var res = await new PreviewAdminUsersImportQueryHandler(db, new AdminUserSpreadsheetService())
            .Handle(new PreviewAdminUsersImportQuery(stream, "formula-vectors.csv", stream.Length), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.ValidCount.Should().Be(0);
        res.Value.InvalidCount.Should().Be(4);
        res.Value.Rows.Should().OnlyContain(r => !r.IsValid);

        var spreadsheetService = new AdminUserSpreadsheetService();
        var workbook = spreadsheetService.GenerateResultWorkbook(
            createdRows: [],
            skippedRows:
            [
                new AdminUserImportSkippedRowDto
                {
                    RowNumber = 2,
                    Email = "\u00A0=CMD|' /C calc'!A0",
                    FullName = "\uFEFF+1+1",
                    Role = "lecturer",
                    Reason = "Formula injection"
                }
            ],
            DateTime.UtcNow);
        using var wbStream = new MemoryStream(workbook.Content);
        using var package = new OfficeOpenXml.ExcelPackage(wbStream);
        var skippedSheet = package.Workbook.Worksheets["DongBoQua"];
        skippedSheet.Should().NotBeNull();
        skippedSheet!.Cells[2, 2].Text.Should().StartWith("'");
        skippedSheet.Cells[2, 3].Text.Should().StartWith("'");
    }

    [Fact(DisplayName = "RT-03: CheckInExamTicket khóa chặt chỉ vai trò student (chặn lecturer/department_head trước existence oracle) và tự cấp ExamSetId khi rebind vé IN_PROGRESS chưa có đề")]
    public async Task RT_03_CheckInExamTicket_StudentOnlyAndResolvesMissingExamSetOnInProgressBind()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var student = new User { Id = Guid.NewGuid(), Email = "sv.rt03@fpt.edu.vn", FullName = "SV RT03", StudentCode = "SE185001", Role = UserRole.Student, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.rt03@fpt.edu.vn", FullName = "GV RT03", Role = UserRole.Lecturer, IsActive = true };
        var deptHead = new User { Id = Guid.NewGuid(), Email = "tbm.rt03@fpt.edu.vn", FullName = "TBM RT03", Role = UserRole.DepartmentHead, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "S", IsActive = true };
        var examSet = new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SetCode = "RT03-SET-01", Status = ExamSetStatus.HandedOver, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE RT03", ExamDate = DateOnly.FromDateTime(now) };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = now.AddMinutes(-5), EndTime = now.AddMinutes(55), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        // Proctor đã điểm danh -> IN_PROGRESS nhưng chưa gắn ExamSetId lẫn DeviceId
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, ExamSetId = null, SeatNumber = 1, Status = ExamTicketStatus.InProgress };

        db.Users.AddRange(student, lecturer, deptHead);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.ExamSets.Add(examSet);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var checkInAttr = typeof(OfficialExamsController).GetMethod(nameof(OfficialExamsController.CheckInTicket))!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        checkInAttr!.Roles.Should().Be(UserRole.Student);

        var handler = new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator());

        // Lecturer & DepartmentHead phải bị chặn Forbidden ngay cả khi ticketId không tồn tại (chống existence oracle)
        var lecRes = await handler.Handle(new CheckInExamTicketCommand(lecturer.Id, Guid.NewGuid(), "10.0.0.1", now, "BYOD-RT03-LAPTOP-0001"), CancellationToken.None);
        lecRes.IsSuccess.Should().BeFalse();
        lecRes.Error.Should().Be(OfficialExamErrors.Forbidden);

        var dhRes = await handler.Handle(new CheckInExamTicketCommand(deptHead.Id, Guid.NewGuid(), "10.0.0.1", now, "BYOD-RT03-LAPTOP-0001"), CancellationToken.None);
        dhRes.IsSuccess.Should().BeFalse();
        dhRes.Error.Should().Be(OfficialExamErrors.Forbidden);

        // Student check-in vé IN_PROGRESS chưa có ExamSetId -> phải bind thiết bị VÀ cấp ExamSetId
        var svRes = await handler.Handle(new CheckInExamTicketCommand(student.Id, ticket.Id, "10.0.0.1", now, "BYOD-RT03-LAPTOP-0001"), CancellationToken.None);
        svRes.IsSuccess.Should().BeTrue(svRes.Error);
        svRes.Value.ExamSetId.Should().Be(examSet.Id);
        svRes.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "RT-04: GetNextInClassFollowUp chống TOCTOU khi Worker chấm xong AI_GRADED giữa lúc đang gọi LLM và không nuốt OperationCanceledException")]
    public async Task RT_04_GetNextInClassFollowUp_PreventsToctouOverwriteAndPropagatesCancellation()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.rt04@fpt.edu.vn", FullName = "GV", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.rt04@fpt.edu.vn", FullName = "SV", StudentCode = "SE185004", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1804", Name = "Class 4", JoinCode = "JOIN04", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Kiến thức cốt lõi", Description = "Đúng bản chất", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var question = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q", Content = "Giải thích Middleware Pipeline trong ASP.NET Core", SampleAnswer = "Middleware xử lý chuỗi request/response theo thứ tự đăng ký trong Program.cs.", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var shift = new InClassExamShift { Id = Guid.NewGuid(), ClassId = cls.Id, LecturerId = lecturer.Id, ShiftName = "Ca RT04", StartTime = now.AddMinutes(-10), EndTime = now.AddMinutes(50), HasFollowUp = true, MaxFollowUpQuestions = 2, TotpSecretKey = "12345678901234567890123456789012", Status = "IN_PROGRESS" };
        var participant = new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, AssignedQuestionId = question.Id, Status = "IN_PROGRESS", JoinedAt = now.AddMinutes(-5) };
        var submission = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, QuestionId = question.Id, Transcript = "Middleware tạo thành pipeline xử lý HTTP request theo thứ tự.", Status = "SUBMITTED", SubmittedAt = now.AddMinutes(-1) };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(criterion);
        db.PracticeQuestions.Add(question);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.Add(submission);
        await db.SaveChangesAsync();

        // 1. Mô phỏng Worker hoàn tất chấm điểm AI_GRADED (không sinh câu phụ) ngay trong lúc LLM call đang chạy
        var toctouAi = new Mock<IAiGradingService>();
        toctouAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                var tracked = await db.InClassExamSubmissions.SingleAsync(s => s.Id == submission.Id, ct);
                tracked.Status = "AI_GRADED";
                tracked.AiScore = 9.2m;
                await db.SaveChangesAsync(ct);
                return new AiGradingResult { Score = 7.0m, FollowUpPrompt = "Vì sao UseCors phải đặt trước UseStaticFiles?" };
            });

        var res = await new GetNextInClassFollowUpQueryHandler(db, toctouAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, submission.Id), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.HasMoreFollowUp.Should().BeFalse();
        (await db.InClassExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id)).FollowUpPrompt.Should().BeNull();

        // 2. OperationCanceledException KHÔNG bị nuốt
        var sub2 = new InClassExamSubmission { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, QuestionId = question.Id, Transcript = "Middleware thứ hai.", Status = "SUBMITTED", SubmittedAt = now };
        db.InClassExamSubmissions.Add(sub2);
        await db.SaveChangesAsync();

        var cancelAi = new Mock<IAiGradingService>();
        cancelAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        Func<Task> act = () => new GetNextInClassFollowUpQueryHandler(db, cancelAi.Object)
            .Handle(new GetNextInClassFollowUpQuery(student.Id, shift.Id, sub2.Id), CancellationToken.None);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "RT-05: GenerateOfficialExamFollowUp kiểm tra lại trạng thái vé & khóa thiết bị trong ExecuteTicketDecisionAsync để chặn TOCTOU khi vé bị LOCKED giữa chừng")]
    public async Task RT_05_GenerateOfficialExamFollowUp_RevalidatesTicketInsideLockAgainstToctou()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        const string laptopA = "BYOD-RT05-LAPTOP-A001";
        var rawToken = OfficialExamDeviceGuard.GenerateSessionToken();
        var student = new User { Id = Guid.NewGuid(), Email = "sv.rt05@fpt.edu.vn", FullName = "SV RT05", StudentCode = "SE185005", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Chuẩn ACID", Description = "Đúng định nghĩa", MaxScore = 10m, Weight = 1m, OrderIndex = 1 };
        var question = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "ACID", Content = "Giải thích tính chất ACID của giao dịch CSDL", SampleAnswer = "ACID gồm Atomicity, Consistency, Isolation và Durability đảm bảo toàn vẹn dữ liệu.", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, Title = "FE RT05", ExamDate = DateOnly.FromDateTime(now), HasFollowUp = true, MaxFollowUpQuestions = 2 };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", StartTime = now.AddMinutes(-10), EndTime = now.AddMinutes(50), Status = ExamShiftWindow.InProgress, IsExamPublished = true };
        var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.InProgress, DeviceId = laptopA, DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(rawToken) };
        var submission = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = question.Id, TranscriptWhisper = "ACID đảm bảo tính nguyên tử và nhất quán cho giao dịch cơ sở dữ liệu.", GradingStatus = "pending", SubmittedAt = now };

        db.Users.Add(student);
        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(criterion);
        db.ExamQuestions.Add(question);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var toctouAi = new Mock<IAiGradingService>();
        toctouAi.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (AiGradingRequest _, CancellationToken ct) =>
            {
                var trackedTicket = await db.StudentExamTickets.SingleAsync(t => t.Id == ticket.Id, ct);
                trackedTicket.Status = ExamTicketStatus.Locked;
                trackedTicket.IsLocked = true;
                await db.SaveChangesAsync(ct);
                return new AiGradingResult { Score = 6.0m, FollowUpPrompt = "Hãy phân biệt Isolation mức Read Committed và Serializable?" };
            });

        var res = await new GenerateOfficialExamFollowUpCommandHandler(db, toctouAi.Object)
            .Handle(new GenerateOfficialExamFollowUpCommand(student.Id, ticket.Id, submission.Id, laptopA, rawToken), CancellationToken.None);

        res.IsSuccess.Should().BeFalse();
        res.Error.Should().Be(OfficialExamErrors.TicketNotInProgress);
        (await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == submission.Id)).GeneratedFollowUpPrompt.Should().BeNull();
    }

    [Fact(DisplayName = "RT-06: CreateOfficialExamSession chặn học kỳ ngưng hoạt động & offering bị tắt; Estimate bỏ qua ca cancelled; TicketState yêu cầu X-Device-Id khi vé đã bind thiết bị")]
    public async Task RT_06_SessionSemesterOfferingGuard_CancelledShiftEstimate_AndTicketStateDeviceIdEnforcement()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var officer = new User { Id = Guid.NewGuid(), Email = "eo.rt06@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.rt06@fpt.edu.vn", FullName = "SV", StudentCode = "SE185006", Role = UserRole.Student, IsActive = true };
        var inactiveSem = new Semester { Id = Guid.NewGuid(), Code = "OLD25", Name = "Old 2025", StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 4, 30), IsActive = false };
        var activeSem = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var courseInactiveSem = new Course { Id = Guid.NewGuid(), Code = "OLD101", Name = "Old", SemesterId = inactiveSem.Id, IsActive = true };
        var courseDisabledOff = new Course { Id = Guid.NewGuid(), Code = "DIS101", Name = "Disabled", SemesterId = activeSem.Id, IsActive = true };
        var disabledOffering = new CourseOffering { Id = Guid.NewGuid(), CourseId = courseDisabledOff.Id, SemesterId = activeSem.Id, IsEnabled = false };

        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseDisabledOff.Id, SemesterId = activeSem.Id, Title = "FE Est", ExamDate = DateOnly.FromDateTime(now) };
        var cancelledShift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Cancelled Room", RoomLab = "Lab X", MaxSeats = 24, StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(2), Status = "cancelled" };
        var activeShift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Active Room", RoomLab = "Lab 1", MaxSeats = 24, StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(2), Status = "scheduled" };
        var boundTicketNoSessionHash = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = activeShift.Id, StudentId = student.Id, SeatNumber = 1, Status = ExamTicketStatus.InProgress, DeviceId = "BYOD-RT06-BOUND-0001", DeviceSessionTokenHash = null };

        db.Users.AddRange(officer, student);
        db.Semesters.AddRange(inactiveSem, activeSem);
        db.Courses.AddRange(courseInactiveSem, courseDisabledOff);
        db.CourseOfferings.Add(disabledOffering);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.AddRange(cancelledShift, activeShift);
        db.StudentExamTickets.Add(boundTicketNoSessionHash);
        await db.SaveChangesAsync();

        var sessionHandler = new CreateOfficialExamSessionCommandHandler(db, new CreateOfficialExamSessionCommandValidator());
        var inactiveRes = await sessionHandler.Handle(new CreateOfficialExamSessionCommand(
            officer.Id, courseInactiveSem.Id, null, "FE Old", new DateOnly(2026, 11, 20), false, 2, ExamInputMode.VoiceOnly, 60, OfficialExamAttemptType.FinalExam, null), CancellationToken.None);
        inactiveRes.IsSuccess.Should().BeFalse();
        inactiveRes.Error.Should().StartWith("UNPROCESSABLE:");

        var disabledRes = await sessionHandler.Handle(new CreateOfficialExamSessionCommand(
            officer.Id, courseDisabledOff.Id, null, "FE Dis", new DateOnly(2026, 11, 20), false, 2, ExamInputMode.VoiceOnly, 60, OfficialExamAttemptType.FinalExam, null), CancellationToken.None);
        disabledRes.IsSuccess.Should().BeFalse();
        disabledRes.Error.Should().StartWith("UNPROCESSABLE:");

        var est = await new GetOfficialExamEnrollmentEstimateQueryHandler(db)
            .Handle(new GetOfficialExamEnrollmentEstimateQuery(officer.Id, session.Id), CancellationToken.None);
        est.IsSuccess.Should().BeTrue(est.Error);
        est.Value.CurrentShiftRoomCount.Should().Be(1, "ca cancelled không được tính vào số phòng và sức chứa hiện tại");
        est.Value.CurrentTotalCapacity.Should().Be(24);

        var stateNoDevice = await new GetOfficialExamTicketStateQueryHandler(db)
            .Handle(new GetOfficialExamTicketStateQuery(student.Id, boundTicketNoSessionHash.Id, DeviceId: null, KioskSessionToken: null), CancellationToken.None);
        stateNoDevice.IsSuccess.Should().BeFalse();
        stateNoDevice.Error.Should().Be(OfficialExamErrors.DeviceMismatch);
    }
}

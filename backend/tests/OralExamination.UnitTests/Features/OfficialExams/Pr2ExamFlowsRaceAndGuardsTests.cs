using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;
using OralExamination.Application.Features.OfficialExams.Commands.BatchOpenExamShifts;
using OralExamination.Application.Features.OfficialExams.Commands.CheckInExamTicket;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;
using OralExamination.Application.Features.Proctor.Commands.UpdateTicketAttendance;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class Pr2ExamFlowsRaceAndGuardsTests
{
    private static OralExamDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new OralExamDbContext(options);
    }

    private static byte[] CreateValidWebmHeaderBytes(int length = 300)
    {
        var bytes = new byte[length];
        bytes[0] = 0x1A;
        bytes[1] = 0x45;
        bytes[2] = 0xDF;
        bytes[3] = 0xA3;
        return bytes;
    }

    [Fact(DisplayName = "PR2-2.1: MF-05 chặn SELECTED_CLOS có số CLO > số câu/SV (422) và AllocateBalancedQuestions không tự fallback sang kho chung")]
    public async Task Pr2_2_1_InClassShift_RejectsSelectedClosGreaterThanTotalQuestions_AndPoolAllocatorReturnsEmpty()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv.pr2@fpt.edu.vn", FullName = "GV PR2", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SE1801", CourseId = course.Id, LecturerId = lecturer.Id, IsActive = true };
        var clo1 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO1", Description = "CLO 1", OrderIndex = 1, IsActive = true };
        var clo2 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO2", Description = "CLO 2", OrderIndex = 2, IsActive = true };
        var clo3 = new CourseClo { Id = Guid.NewGuid(), CourseId = course.Id, CloCode = "CLO3", Description = "CLO 3", OrderIndex = 3, IsActive = true };

        db.Users.Add(lecturer);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.CourseClos.AddRange(clo1, clo2, clo3);
        db.PracticeQuestions.AddRange(
            new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo1.Id, Title = "Q1", Content = "C1", SampleAnswer = new string('a', 60), Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true },
            new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo2.Id, Title = "Q2", Content = "C2", SampleAnswer = new string('b', 60), Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true },
            new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, CloId = clo3.Id, Title = "Q3", Content = "C3", SampleAnswer = new string('c', 60), Difficulty = "easy", ApprovalStatus = ApprovalStatus.Approved, IsActive = true }
        );
        await db.SaveChangesAsync();

        var handler = new CreateInClassShiftCommandHandler(db, new CreateInClassShiftCommandValidator());
        var createResult = await handler.Handle(new CreateInClassShiftCommand(
            lecturer.Id,
            cls.Id,
            "Ca lỗi 3 CLO / 2 câu",
            DateTime.UtcNow.AddMinutes(5),
            DateTime.UtcNow.AddMinutes(50),
            QuestionPoolMode: InClassShiftPoolConfig.SelectedClos,
            TotalQuestionsPerStudent: 2,
            SelectedDifficulties: ["easy"],
            SelectedCloIds: [clo1.Id, clo2.Id, clo3.Id]), CancellationToken.None);

        createResult.IsSuccess.Should().BeFalse();
        InClassShiftErrors.IsUnprocessablePoolError(createResult.Error).Should().BeTrue();
        createResult.Error.Should().Contain("Số CLO đã chọn (3) không được lớn hơn số câu hỏi mỗi sinh viên (2)");

        var candidates = new List<PoolCandidateQuestion>
        {
            new(Guid.NewGuid(), clo1.Id, "easy"),
            new(Guid.NewGuid(), clo2.Id, "easy"),
            new(Guid.NewGuid(), clo3.Id, "easy")
        };

        var allocated = InClassShiftPoolConfig.AllocateBalancedQuestions(
            candidates,
            poolMode: InClassShiftPoolConfig.SelectedClos,
            selectedCloIds: [clo1.Id, clo2.Id, clo3.Id],
            targetCount: 2,
            usageCounts: new Dictionary<Guid, int>());

        allocated.Should().BeEmpty("SELECTED_CLOS phải trả rỗng khi số CLO vượt số câu/SV thay vì lặng lẽ bỏ lọc CLO");
    }

    [Fact(DisplayName = "PR2-2.2A: OfficialExamGradingWorker thăng hạng vé SUBMITTED -> AI_GRADED dưới khóa ticket khi câu còn lại đã audited và tự phục hồi khi replay")]
    public async Task Pr2_2_2_OfficialGradingWorker_PromotesTicketToAiGraded_WhenSiblingAlreadyAudited_AndSelfHealsAlreadyGradedSubmission()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));

        var gradingMock = new Mock<IAiGradingService>();
        var criterionId = Guid.NewGuid();
        gradingMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.5m,
                ConfidenceScore = 0.91m,
                Feedback = "Phân tích rõ ràng và chính xác.",
                Details = [new GradingDetailDto { CriterionId = criterionId.ToString(), Score = 8.5m, Comment = "Tốt" }]
            });
        services.AddSingleton(gradingMock.Object);
        await using var provider = services.BuildServiceProvider();

        Guid ticketId;
        Guid sub1Id;
        Guid job1Id;
        Guid sub2Id;
        Guid job2Id;

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", IsActive = true };
            var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
            rubric.Criteria.Add(new RubricCriterion { Id = criterionId, RubricId = rubric.Id, CriterionName = "Hiểu bài", MaxScore = 10m, OrderIndex = 1 });
            var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Clean Arch", SampleAnswer = new string('x', 60), Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
            var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "CQRS", SampleAnswer = new string('y', 60), Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
            var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "Ca 1", RoomLab = "Lab 1", Status = "in_progress", IsExamPublished = true };
            var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), ExamSetId = Guid.NewGuid(), SeatNumber = 1, Status = ExamTicketStatus.Submitted, SubmittedAt = DateTime.UtcNow };
            var sub1 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, TranscriptWhisper = "Giải thích Clean Architecture đầy đủ", GradingStatus = "pending", SubmittedAt = DateTime.UtcNow };
            var sub2 = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, TranscriptWhisper = "Đã hậu kiểm xong", GradingStatus = "audited", AiScore = 8.0m, SubmittedAt = DateTime.UtcNow };
            var job1 = new GradingJob { Id = Guid.NewGuid(), SubmissionId = sub1.Id, Status = "pending", AttemptCount = 0, NextAttemptAt = DateTime.UtcNow };
            var job2 = new GradingJob { Id = Guid.NewGuid(), SubmissionId = sub2.Id, Status = "pending", AttemptCount = 0, NextAttemptAt = DateTime.UtcNow };

            db.Courses.Add(course);
            db.Rubrics.Add(rubric);
            db.ExamQuestions.AddRange(q1, q2);
            db.RealExamSessionShifts.Add(shift);
            db.StudentExamTickets.Add(ticket);
            db.ExamQuestionSubmissions.AddRange(sub1, sub2);
            db.GradingJobs.AddRange(job1, job2);
            await db.SaveChangesAsync();

            ticketId = ticket.Id;
            sub1Id = sub1.Id;
            job1Id = job1.Id;
            sub2Id = sub2.Id;
            job2Id = job2.Id;
        }

        var queueMock = new Mock<IOfficialExamGradingQueue>();
        var worker = new OfficialExamGradingWorker(
            queueMock.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);

        await worker.ProcessAsync(new OfficialExamGradingTask(job1Id, sub1Id), CancellationToken.None);

        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var ticket = await db.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
            var sub1 = await db.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == sub1Id);
            var job1 = await db.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job1Id);

            sub1.GradingStatus.Should().Be("ai_graded");
            sub1.AiScore.Should().Be(8.5m);
            job1.Status.Should().Be("completed");
            ticket.Status.Should().Be(ExamTicketStatus.AiGraded, "câu sibling ở trạng thái audited phải được tính là đã hoàn tất để thăng hạng vé");

            // Mô phỏng trường hợp vé bị kẹt ở SUBMITTED dù evaluation đã tồn tại -> replay job1 tự phục hồi vé sang AI_GRADED
            var trackedTicket = await db.StudentExamTickets.SingleAsync(t => t.Id == ticketId);
            var trackedSub2 = await db.ExamQuestionSubmissions.SingleAsync(s => s.Id == sub2Id);
            trackedTicket.Status = ExamTicketStatus.Submitted;
            trackedSub2.GradingStatus = "ai_graded";
            var replayJob = new GradingJob { Id = Guid.NewGuid(), SubmissionId = sub1Id, Status = "pending", AttemptCount = 0, NextAttemptAt = DateTime.UtcNow };
            db.GradingJobs.Add(replayJob);
            await db.SaveChangesAsync();
            job2Id = replayJob.Id;
        }

        await worker.ProcessAsync(new OfficialExamGradingTask(job2Id, sub1Id), CancellationToken.None);

        using (var replayVerifyScope = provider.CreateScope())
        {
            var db = replayVerifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var ticket = await db.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
            var replayJob = await db.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == job2Id);
            ticket.Status.Should().Be(ExamTicketStatus.AiGraded);
            replayJob.Status.Should().Be("completed");
        }
    }

    [Fact(DisplayName = "PR2-2.2B: SubmitOfficialExam per-turn finalize không hạ ngược vé AI_GRADED và giữ nguyên SubmittedAt khi gọi lặp")]
    public async Task Pr2_2_2_SubmitOfficialExam_PerTurnFinalize_NeverDowngradesAiGradedTicket_AndPromotesWhenAllGraded()
    {
        await using var db = CreateDbContext();
        var studentId = Guid.NewGuid();
        var examSetId = Guid.NewGuid();
        var q1Id = Guid.NewGuid();
        const string deviceId = "KIOSK-PR2-SUBMIT-0001";
        const string token = "SESSION-TOKEN-PR2-SUBMIT-0001";

        db.ExamSetQuestions.Add(new ExamSetQuestion { Id = Guid.NewGuid(), ExamSetId = examSetId, ExamQuestionId = q1Id, OrderIndex = 1 });
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = studentId,
            ExamSetId = examSetId,
            SeatNumber = 1,
            DeviceId = deviceId,
            DeviceSessionTokenHash = OfficialExamDeviceGuard.HashSessionToken(token),
            Status = ExamTicketStatus.InProgress
        };
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = q1Id,
            IsFollowUp = false,
            TranscriptWhisper = "Đã trả lời và đã được worker chấm xong",
            GradingStatus = "ai_graded",
            AiScore = 9.0m,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var handler = new SubmitOfficialExamCommandHandler(db);
        var firstFinalize = await handler.Handle(
            new SubmitOfficialExamCommand(studentId, ticket.Id, "10.0.0.1", null, deviceId, token),
            CancellationToken.None);

        firstFinalize.IsSuccess.Should().BeTrue();
        firstFinalize.Value.Status.Should().Be(ExamTicketStatus.AiGraded);
        var savedTicket = await db.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
        savedTicket.Status.Should().Be(ExamTicketStatus.AiGraded);
        savedTicket.SubmittedAt.Should().NotBeNull();
        var originalSubmittedAt = savedTicket.SubmittedAt!.Value;

        var secondFinalize = await handler.Handle(
            new SubmitOfficialExamCommand(studentId, ticket.Id, "10.0.0.1", null, deviceId, token),
            CancellationToken.None);

        secondFinalize.IsSuccess.Should().BeTrue();
        secondFinalize.Value.Status.Should().Be(ExamTicketStatus.AiGraded);
        secondFinalize.Value.SubmittedAt.Should().Be(originalSubmittedAt);
    }

    [Fact(DisplayName = "PR2-2.3A: CheckInExamTicket bắt buộc X-Device-Id hợp lệ với sinh viên chủ vé")]
    public async Task Pr2_2_3_CheckInExamTicket_RequiresDeviceId_ForStudentOwner()
    {
        await using var db = CreateDbContext();
        var student = new User { Id = Guid.NewGuid(), Email = "sv.dev@fpt.edu.vn", FullName = "SV Device", Role = UserRole.Student, IsActive = true };
        var now = new DateTime(2026, 11, 24, 8, 30, 0, DateTimeKind.Utc);
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1",
            RoomLab = "Lab 1",
            StartTime = now.AddMinutes(-30),
            EndTime = now.AddMinutes(90),
            Status = ExamShiftWindow.Scheduled,
            IsExamPublished = true,
            MaxSeats = 24
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = Guid.NewGuid(),
            SeatNumber = 1,
            Status = ExamTicketStatus.Scheduled
        };
        db.Users.Add(student);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new CheckInExamTicketCommandHandler(db, new CheckInExamTicketCommandValidator());
        var missingDevice = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "192.168.1.50", now, DeviceId: null),
            CancellationToken.None);

        missingDevice.IsSuccess.Should().BeFalse();
        missingDevice.Error.Should().Be(OfficialExamErrors.DeviceIdInvalid);

        const string validDeviceId = "DESKTOP-KIOSK-PR2-0001";
        var withDevice = await handler.Handle(
            new CheckInExamTicketCommand(student.Id, ticket.Id, "192.168.1.50", now, DeviceId: validDeviceId),
            CancellationToken.None);

        withDevice.IsSuccess.Should().BeTrue();
        withDevice.Value.DeviceId.Should().Be(validDeviceId);
        withDevice.Value.ActiveSessionToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "PR2-2.3B: Proctor UpdateTicketAttendance chặn điểm danh khi chưa phát đề hoặc chưa gán ExamSet và không biến đổi SeatNumber")]
    public async Task Pr2_2_3_ProctorAttendance_RequiresPublishedShiftAndAssignedExamSet_WithoutMutatingSeatOnFailure()
    {
        await using var db = CreateDbContext();
        var proctor = new User { Id = Guid.NewGuid(), Email = "proctor.pr2@fpt.edu.vn", FullName = "Proctor PR2", Role = UserRole.Proctor, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "student.pr2@fpt.edu.vn", FullName = "Student PR2", Role = UserRole.Student, IsActive = true };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca 1",
            RoomLab = "Lab 202",
            ProctorUserId = proctor.Id,
            IsExamPublished = false,
            Status = ExamShiftWindow.Scheduled,
            MaxSeats = 24
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            ExamSetId = null,
            SeatNumber = 2,
            Status = ExamTicketStatus.Scheduled
        };
        db.Users.AddRange(proctor, student);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new UpdateTicketAttendanceCommandHandler(db);

        // 1. Chưa xuất bản đề -> chặn CONFLICT và không đổi SeatNumber
        var unpublishedRes = await handler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket.Id, IsPresent: true, SeatNumber: 8),
            CancellationToken.None);
        unpublishedRes.IsSuccess.Should().BeFalse();
        unpublishedRes.Error.Should().StartWith("CONFLICT:");
        ticket.SeatNumber.Should().Be(2);

        // 2. Đã xuất bản đề nhưng vé chưa gán ExamSet -> chặn CONFLICT và không đổi SeatNumber
        shift.IsExamPublished = true;
        await db.SaveChangesAsync();

        var noSetRes = await handler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket.Id, IsPresent: true, SeatNumber: 8),
            CancellationToken.None);
        noSetRes.IsSuccess.Should().BeFalse();
        noSetRes.Error.Should().StartWith("CONFLICT:");
        ticket.SeatNumber.Should().Be(2);

        // 3. Đã gán ExamSet và đã xuất bản đề -> thành công
        ticket.ExamSetId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var okRes = await handler.Handle(
            new UpdateTicketAttendanceCommand(proctor.Id, ticket.Id, IsPresent: true, SeatNumber: 8),
            CancellationToken.None);
        okRes.IsSuccess.Should().BeTrue();
        okRes.Value.SeatNumber.Should().Be(8);
        okRes.Value.Status.Should().Be(ExamTicketStatus.InProgress);
    }

    [Fact(DisplayName = "PR2-2.3C: ImportExamRoster cho phép bỏ trống IP hoặc trùng IP NAT dùng chung nhưng vẫn chặn IP sai định dạng")]
    public async Task Pr2_2_3_ImportExamRoster_AllowsMissingAndDuplicateSharedNatIps_WhileRejectingMalformedIp()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.pr2@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var sv1 = new User { Id = Guid.NewGuid(), Email = "sv1.ip@fpt.edu.vn", FullName = "SV 1", StudentCode = "SE189001", Role = UserRole.Student, IsActive = true };
        var sv2 = new User { Id = Guid.NewGuid(), Email = "sv2.ip@fpt.edu.vn", FullName = "SV 2", StudentCode = "SE189002", Role = UserRole.Student, IsActive = true };
        var sv3 = new User { Id = Guid.NewGuid(), Email = "sv3.ip@fpt.edu.vn", FullName = "SV 3", StudentCode = "SE189003", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE", ExamDate = new DateOnly(2026, 12, 1), Status = "scheduled" };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", MaxSeats = 24, Status = "scheduled" };

        db.Users.AddRange(officer, sv1, sv2, sv3);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.ExamSets.AddRange(
            new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SessionId = session.Id, SetCode = "SET-1", Status = ExamSetStatus.HandedOver, IsActive = true },
            new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SessionId = session.Id, SetCode = "SET-2", Status = ExamSetStatus.HandedOver, IsActive = true },
            new ExamSet { Id = Guid.NewGuid(), CourseId = course.Id, StructureId = structure.Id, SessionId = session.Id, SetCode = "SET-3", Status = ExamSetStatus.HandedOver, IsActive = true }
        );
        await db.SaveChangesAsync();

        var previewHandler = new PreviewExamRosterImportQueryHandler(db);
        var invalidIpPreview = await previewHandler.Handle(new PreviewExamRosterImportQuery(
            officer.Id,
            shift.Id,
            [new BulkExamTicketImportRow(2, "SE189001", 1, "999.999.1.1")]), CancellationToken.None);

        invalidIpPreview.IsSuccess.Should().BeTrue();
        invalidIpPreview.Value.CanCommit.Should().BeFalse();
        invalidIpPreview.Value.Issues.Should().Contain(i => i.Message == OfficialExamErrors.IpInvalid);

        var commitHandler = new CommitExamRosterImportCommandHandler(db);
        var commitRes = await commitHandler.Handle(new CommitExamRosterImportCommand(
            officer.Id,
            shift.Id,
            [
                new BulkExamTicketImportRow(2, "SE189001", 1, string.Empty),
                new BulkExamTicketImportRow(3, "SE189002", 2, "10.24.0.1"),
                new BulkExamTicketImportRow(4, "SE189003", 3, "10.24.0.1")
            ]), CancellationToken.None);

        commitRes.IsSuccess.Should().BeTrue(because: commitRes.Error);
        commitRes.Value.Tickets.Should().HaveCount(3);
        commitRes.Value.Tickets.Single(t => t.SeatNumber == 1).IpAddress.Should().BeNull();
        commitRes.Value.Tickets.Where(t => t.SeatNumber is 2 or 3).Select(t => t.IpAddress).Should().AllBeEquivalentTo("10.24.0.1");
    }

    [Fact(DisplayName = "PR2-2.4: BatchOpenExamShifts chấp nhận phòng 30..40 ghế, chặn 41 ghế và AutoAllocate phân bổ đủ 30 SV vào phòng 30 ghế")]
    public async Task Pr2_2_4_BatchOpenShiftsAndAutoAllocate_SupportRoomsUpTo40Seats_WhileRejecting41()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.cap@fpt.edu.vn", FullName = "EO Cap", Role = UserRole.ExaminationOfficer, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", SemesterId = semester.Id, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, Title = "FE SWD", ExamDate = new DateOnly(2026, 12, 10), Status = "scheduled" };

        db.Users.Add(officer);
        db.Semesters.Add(semester);
        db.Courses.Add(course);
        db.OfficialExamSessions.Add(session);

        for (var i = 1; i <= 30; i++)
        {
            var sv = new User { Id = Guid.NewGuid(), Email = $"sv{i:D2}.cap@fpt.edu.vn", FullName = $"SV {i}", StudentCode = $"SE1900{i:D2}", Role = UserRole.Student, IsActive = true };
            db.Users.Add(sv);
            db.OfficialExamCandidates.Add(new OfficialExamCandidate { Id = Guid.NewGuid(), SessionId = session.Id, StudentId = sv.Id, ImportedBy = officer.Id, ImportedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();

        var openHandler = new BatchOpenExamShiftsCommandHandler(db);
        var start = new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var reject41 = await openHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca quá trần", start, end,
            [new BatchOpenExamShiftRoomDto { RoomLab = "LAB-401", MaxSeats = 41 }]), CancellationToken.None);
        reject41.IsSuccess.Should().BeFalse();
        reject41.Error.Should().StartWith("UNPROCESSABLE:");

        var open30 = await openHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca phòng lớn", start, end,
            [new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 30 }]), CancellationToken.None);
        open30.IsSuccess.Should().BeTrue(because: open30.Error);
        open30.Value.TotalCapacityCreated.Should().Be(30);

        var open40Next = await openHandler.Handle(new BatchOpenExamShiftsCommand(
            officer.Id, session.Id, "Ca nối tiếp 40 ghế", end, end.AddHours(2),
            [new BatchOpenExamShiftRoomDto { RoomLab = "LAB-301", MaxSeats = 40 }]), CancellationToken.None);
        open40Next.IsSuccess.Should().BeTrue(because: open40Next.Error);
        open40Next.Value.TotalCapacityCreated.Should().Be(40);

        var allocateRes = await new AutoAllocateSessionStudentsCommandHandler(db)
            .Handle(new AutoAllocateSessionStudentsCommand(officer.Id, session.Id), CancellationToken.None);
        allocateRes.IsSuccess.Should().BeTrue(because: allocateRes.Error);
        allocateRes.Value.NewlyAllocatedCount.Should().Be(30);

        var firstShiftId = open30.Value.Shifts.Single().Id;
        var assignedInFirstShift = await db.StudentExamTickets.CountAsync(t => t.ShiftId == firstShiftId);
        assignedInFirstShift.Should().Be(30, "phòng 30 ghế phải nhận đủ 30 thí sinh thay vì bị cắt cứng ở 24 ghế");
    }

    [Fact(DisplayName = "PR2-2.5A: MF-02 Voice-First Gate chặn byte giả khi STT lỗi nhưng vẫn cho fallback DirectTranscript với WebM container thật")]
    public async Task Pr2_2_5_MockExamVoiceFirstGate_BlocksFakeAudioBytesOnSttOutage_AndAllowsRealWebmFallback()
    {
        await using var db = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        db.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "PRN", IsActive = true });
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(28),
            Status = "in_progress"
        };
        var answer = new MockExamAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = questionId, Status = "pending" };
        session.Answers.Add(answer);
        db.MockExamSessions.Add(session);
        await db.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Upstream STT timeout"));
        sttMock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        var handler = new SubmitMockExamAnswerCommandHandler(db, sttMock.Object);

        using var fakeStream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-real-audio-file"));
        var fakeResult = await handler.Handle(new SubmitMockExamAnswerCommand(
            session.Id, studentId, questionId, fakeStream, "fake.webm", "audio/webm", 25,
            DirectTranscript: "Chèn text gian lận khi không có audio thật"), CancellationToken.None);

        fakeResult.IsSuccess.Should().BeFalse();
        fakeResult.Error.Should().Contain("VOICE_FIRST_GATE");

        using var validWebmStream = new MemoryStream(CreateValidWebmHeaderBytes());
        var validFallbackResult = await handler.Handle(new SubmitMockExamAnswerCommand(
            session.Id, studentId, questionId, validWebmStream, "real.webm", "audio/webm", 25,
            DirectTranscript: "Em trình bày Dependency Inversion trong Clean Architecture"), CancellationToken.None);

        validFallbackResult.IsSuccess.Should().BeTrue(because: validFallbackResult.Error);
        validFallbackResult.Value.Transcript.Should().Be("Em trình bày Dependency Inversion trong Clean Architecture");
    }

    [Fact(DisplayName = "PR2-2.5B: MF-02 Follow-up Q2=A bảo toàn điểm gốc khi yếu hơn và cộng điểm khi trả lời phụ tốt hơn")]
    public async Task Pr2_2_5_MockExamFollowUp_Q2A_NeverLowersMainScore_WhileHigherFollowUpImprovesScore()
    {
        await using var db = CreateDbContext();
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
        var c1 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C1", MaxScore = 5m, OrderIndex = 1 };
        var c2 = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "C2", MaxScore = 5m, OrderIndex = 2 };
        rubric.Criteria.Add(c1);
        rubric.Criteria.Add(c2);
        var q1 = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Rubric = rubric, Title = "Q1", Content = "Clean Arch", SampleAnswer = new string('a', 60), Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
        var studentId = Guid.NewGuid();
        var session = new MockExamSession { Id = Guid.NewGuid(), StudentId = studentId, CourseId = course.Id, HasFollowUp = true, StartedAt = DateTime.UtcNow.AddMinutes(-10), ExpiresAt = DateTime.UtcNow.AddMinutes(20), Status = "in_progress" };
        var mainAns = new MockExamAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = q1.Id, OrderIndex = 1, IsFollowUp = false, AnswerText = "Trả lời chính 5.0", Status = "answered", AnsweredAt = DateTime.UtcNow.AddMinutes(-5) };
        var followUpAns = new MockExamAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = q1.Id, ParentAnswerId = mainAns.Id, OrderIndex = 1, IsFollowUp = true, PromptText = "Phân tích sâu hơn?", AnswerText = "Trả lời phụ 9.0", Status = "answered", AnsweredAt = DateTime.UtcNow.AddMinutes(-2) };
        session.Answers.Add(mainAns);
        session.Answers.Add(followUpAns);

        db.Courses.Add(course);
        db.Rubrics.Add(rubric);
        db.PracticeQuestions.Add(q1);
        db.MockExamSessions.Add(session);
        await db.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.SetupSequence(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 5.0m,
                ConfidenceScore = 0.90m,
                Feedback = "Câu gốc đạt 5.0",
                Details = [new() { CriterionId = c1.Id.ToString(), Score = 2.5m }, new() { CriterionId = c2.Id.ToString(), Score = 2.5m }]
            })
            .ReturnsAsync(new AiGradingResult
            {
                Score = 9.0m,
                ConfidenceScore = 0.90m,
                Feedback = "Câu phụ xuất sắc 9.0",
                Details = [new() { CriterionId = c1.Id.ToString(), Score = 4.5m }, new() { CriterionId = c2.Id.ToString(), Score = 4.5m }]
            });

        var res = await new SubmitMockExamSessionCommandHandler(db, aiMock.Object)
            .Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.TotalScore.Should().BeGreaterThan(5.0m);
        res.Value.TotalScore.Should().BeLessOrEqualTo(9.0m);
    }

    [Fact(DisplayName = "PR2-2.5C: ExamQuestion bản nháp tạo ra với IsActive=false, ghép được vào ExamSet và kích hoạt IsActive=true khi TBM duyệt")]
    public async Task Pr2_2_5_CreateDraftExamQuestion_StartsInactive_CanJoinExamSet_AndActivatesOnExamSetApproval()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.pr2@fpt.edu.vn", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.pr2@fpt.edu.vn", FullName = "Lecturer", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct PR2", IsActive = true };

        db.Users.AddRange(head, lecturer);
        db.Courses.Add(course);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        await db.SaveChangesAsync();

        var draftRes = await new CreateDraftQuestionCommandHandler(db).Handle(new CreateDraftQuestionCommand(
            CourseId: course.Id,
            LecturerId: lecturer.Id,
            Title: "Câu hỏi nháp thi thật",
            Content: "Trình bày cơ chế Middleware Pipeline trong ASP.NET Core 8?",
            SampleAnswer: "Middleware Pipeline xử lý HTTP request theo chuỗi delegate nối tiếp nhau từ UseCors đến endpoint.",
            KeyPoints: ["Middleware", "RequestDelegate"],
            Difficulty: "medium",
            BloomLevel: "Understand",
            Source: "manual",
            UsageScope: "exam",
            HasFollowUp: false,
            FollowUpPrompt: null,
            Rubric: new RubricDraftDto("Rubric Draft", "Desc", 10.0m,
            [
                new RubricCriterionDraftDto("Khái niệm", 5.0m, 0.5m, "Understand", "Đúng khái niệm", 1),
                new RubricCriterionDraftDto("Thứ tự pipeline", 5.0m, 0.5m, "Apply", "Đúng thứ tự", 2)
            ])), CancellationToken.None);

        draftRes.IsSuccess.Should().BeTrue(because: draftRes.Error);
        var question = await db.ExamQuestions.SingleAsync(q => q.Id == draftRes.Value);
        question.IsActive.Should().BeFalse("câu hỏi thi thật ở trạng thái DRAFT không được kích hoạt sớm");
        question.ApprovalStatus.Should().Be(ApprovalStatus.Draft);

        var setRes = await new CreateExamSetCommandHandler(db).Handle(new CreateExamSetCommand(
            StructureId: structure.Id,
            CourseId: course.Id,
            SessionId: null,
            SetCode: "SET-PR2-01",
            QuestionIds: [question.Id],
            ActorUserId: lecturer.Id,
            ActorRole: UserRole.Lecturer), CancellationToken.None);
        setRes.IsSuccess.Should().BeTrue(because: setRes.Error);

        var approveRes = await new ApproveExamSetCommandHandler(db).Handle(new ApproveExamSetCommand(
            ExamSetId: setRes.Value.Id,
            ReviewNotes: "Duyệt đề PR2",
            ActorUserId: head.Id,
            ActorRole: UserRole.DepartmentHead), CancellationToken.None);
        approveRes.IsSuccess.Should().BeTrue(because: approveRes.Error);

        var approvedQuestion = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == question.Id);
        approvedQuestion.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        approvedQuestion.IsActive.Should().BeTrue("khi Trưởng BM duyệt bộ đề, câu hỏi bên trong phải được kích hoạt");
    }

    [Fact(DisplayName = "PR2-2.5D: MockExamsController trả về 403 ProblemDetails khi lệch ?studentId= và 401 khi chưa xác thực trên cả 3 endpoint")]
    public async Task Pr2_2_5_MockExamsController_Returns403ProblemDetailsOnMismatchedStudentId_And401WhenUnauthenticated()
    {
        var senderMock = new Mock<ISender>();
        var currentStudentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var studentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, currentStudentId.ToString()),
            new Claim(ClaimTypes.Role, UserRole.Student)
        ], "TestAuth"));

        var controller = new MockExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = studentPrincipal } }
        };

        var quota403 = (await controller.GetQuota(courseId, otherStudentId)).Should().BeOfType<ObjectResult>().Subject;
        quota403.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        quota403.Value.Should().BeOfType<ProblemDetails>();

        var history403 = (await controller.GetHistory(courseId, studentId: otherStudentId)).Should().BeOfType<ObjectResult>().Subject;
        history403.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        history403.Value.Should().BeOfType<ProblemDetails>();

        var detail403 = (await controller.GetHistoryDetail(sessionId, otherStudentId)).Should().BeOfType<ObjectResult>().Subject;
        detail403.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        detail403.Value.Should().BeOfType<ProblemDetails>();

        var unauthController = new MockExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } }
        };
        var quota401 = (await unauthController.GetQuota(courseId, null)).Should().BeOfType<UnauthorizedObjectResult>().Subject;
        quota401.Value.Should().BeOfType<ProblemDetails>();
        var history401 = (await unauthController.GetHistory(courseId)).Should().BeOfType<UnauthorizedObjectResult>().Subject;
        history401.Value.Should().BeOfType<ProblemDetails>();
        var detail401 = (await unauthController.GetHistoryDetail(sessionId)).Should().BeOfType<UnauthorizedObjectResult>().Subject;
        detail401.Value.Should().BeOfType<ProblemDetails>();

        senderMock.Verify(s => s.Send(It.IsAny<IRequest<It.IsAnyType>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "PR2-2.5E: Thông báo quá hạn phiên luyện tập tự động nội suy đúng số phút cấu hình trong system_configs")]
    public async Task Pr2_2_5_PracticeSessionTimeoutMessage_InterpolatesConfiguredMinutes()
    {
        await using var db = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        db.SystemConfigs.Add(new SystemConfig
        {
            Key = "SessionInactivityTimeoutMinutes",
            Value = "12",
            Description = "Dynamic 12 minutes",
            UpdatedAt = DateTime.UtcNow
        });
        db.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "PRN", IsActive = true });
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-13)
        };
        var batchSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            PracticeMode = "full_session",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-13)
        };
        var answerSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-13)
        };
        db.PracticeSessions.AddRange(session, batchSession, answerSession);
        await db.SaveChangesAsync();

        var nextRes = await new NextQuestionCommandHandler(db)
            .Handle(new NextQuestionCommand(session.Id, studentId), CancellationToken.None);
        nextRes.IsSuccess.Should().BeFalse();
        nextRes.Error.Should().Contain("trong hơn 12 phút");

        var batchRes = await new SubmitPracticeBatchCommandHandler(db, new Mock<IGradingQueueChannel>().Object)
            .Handle(new SubmitPracticeBatchCommand(batchSession.Id, studentId, []), CancellationToken.None);
        batchRes.IsSuccess.Should().BeFalse();
        batchRes.Error.Should().Contain("trong hơn 12 phút");

        var answerRes = await new SubmitPracticeAnswerCommandHandler(db, new Mock<IGradingQueueChannel>().Object)
            .Handle(new SubmitPracticeAnswerCommand(answerSession.Id, Guid.NewGuid(), studentId, "Trả lời test timeout", false, null), CancellationToken.None);
        answerRes.IsSuccess.Should().BeFalse();
        answerRes.Error.Should().Contain("trong hơn 12 phút");
    }

    [Fact(DisplayName = "PR2-2.2C: OfficialExamGradingWorker bảo vệ kết quả audited, tự phục hồi trạng thái transcribed đã có AiEvaluation và MarkFailedAsync không hạ ngược bài đã chấm")]
    public async Task Pr2_2_2_OfficialGradingWorker_ProtectsAuditedSubmission_SelfHealsStaleTranscribed_AndMarkFailedNeverDowngradesTerminal()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var gradingMock = new Mock<IAiGradingService>();
        services.AddSingleton(gradingMock.Object);
        await using var provider = services.BuildServiceProvider();

        Guid ticketId;
        Guid auditedSubId;
        Guid auditedJobId;
        Guid staleSubId;
        Guid staleJobId;

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", IsActive = true };
            var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };
            var critId = Guid.NewGuid();
            rubric.Criteria.Add(new RubricCriterion { Id = critId, RubricId = rubric.Id, CriterionName = "C1", MaxScore = 10m, OrderIndex = 1 });
            var q1 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q1", Content = "Q1", SampleAnswer = new string('a', 60), Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
            var q2 = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Q2", Content = "Q2", SampleAnswer = new string('b', 60), Difficulty = "medium", ApprovalStatus = ApprovalStatus.Approved, IsActive = true };
            var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), ShiftName = "Ca 1", RoomLab = "Lab 1", Status = "in_progress", IsExamPublished = true };
            var ticket = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = Guid.NewGuid(), ExamSetId = Guid.NewGuid(), SeatNumber = 1, Status = ExamTicketStatus.Submitted, SubmittedAt = DateTime.UtcNow };

            // Bài 1 đã được Giám khảo hậu kiểm (audited) dù chưa có AiEvaluation
            var auditedSub = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q1.Id, TranscriptWhisper = "Nội dung câu 1", GradingStatus = "audited", AiScore = 9.0m, SubmittedAt = DateTime.UtcNow };
            var auditedJob = new GradingJob { Id = Guid.NewGuid(), SubmissionId = auditedSub.Id, Status = "pending", AttemptCount = 0, NextAttemptAt = DateTime.UtcNow };

            // Bài 2 bị lệch trạng thái thành "transcribed" nhưng đã có AiEvaluation trong DB
            var staleSub = new ExamQuestionSubmission { Id = Guid.NewGuid(), TicketId = ticket.Id, ExamQuestionId = q2.Id, TranscriptWhisper = "Nội dung câu 2", GradingStatus = "transcribed", AiScore = 7.5m, SubmittedAt = DateTime.UtcNow };
            var staleJob = new GradingJob { Id = Guid.NewGuid(), SubmissionId = staleSub.Id, Status = "pending", AttemptCount = 0, NextAttemptAt = DateTime.UtcNow };

            db.Courses.Add(course);
            db.Rubrics.Add(rubric);
            db.ExamQuestions.AddRange(q1, q2);
            db.RealExamSessionShifts.Add(shift);
            db.StudentExamTickets.Add(ticket);
            db.ExamQuestionSubmissions.AddRange(auditedSub, staleSub);
            db.AiEvaluations.Add(new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "exam", ExamSubmissionId = staleSub.Id, TotalScore = 7.5m, ConfidenceScore = 0.90m, EvaluatedAt = DateTime.UtcNow });
            db.GradingJobs.AddRange(auditedJob, staleJob);
            await db.SaveChangesAsync();

            ticketId = ticket.Id;
            auditedSubId = auditedSub.Id;
            auditedJobId = auditedJob.Id;
            staleSubId = staleSub.Id;
            staleJobId = staleJob.Id;
        }

        var worker = new OfficialExamGradingWorker(
            new Mock<IOfficialExamGradingQueue>().Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfficialExamGradingWorker>.Instance);

        // 1. ProcessAsync trên bài đã audited -> không gọi AI, không ghi đè "audited"
        await worker.ProcessAsync(new OfficialExamGradingTask(auditedJobId, auditedSubId), CancellationToken.None);
        gradingMock.Verify(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        // 2. ProcessAsync trên bài bị kẹt "transcribed" nhưng đã có AiEvaluation -> tự phục hồi sang "ai_graded" và thăng hạng vé sang AI_GRADED
        await worker.ProcessAsync(new OfficialExamGradingTask(staleJobId, staleSubId), CancellationToken.None);

        // 3. Mô phỏng job song song gặp lỗi muộn sau khi bài đã audited -> MarkFailedAsync KHÔNG được hạ ngược về "failed" hay tạo DLQ
        using (var prepFailScope = provider.CreateScope())
        {
            var prepDb = prepFailScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
            var jobToFail = await prepDb.GradingJobs.SingleAsync(j => j.Id == auditedJobId);
            jobToFail.Status = "processing";
            jobToFail.AttemptCount = 1;
            await prepDb.SaveChangesAsync();
        }

        await worker.MarkFailedAsync(new OfficialExamGradingTask(auditedJobId, auditedSubId), new TimeoutException("Late failure"), CancellationToken.None);

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var finalAudited = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == auditedSubId);
        var finalStale = await verifyDb.ExamQuestionSubmissions.AsNoTracking().SingleAsync(s => s.Id == staleSubId);
        var finalTicket = await verifyDb.StudentExamTickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        var finalFailedJob = await verifyDb.GradingJobs.AsNoTracking().SingleAsync(j => j.Id == auditedJobId);

        finalAudited.GradingStatus.Should().Be("audited");
        finalAudited.AiScore.Should().Be(9.0m);
        finalStale.GradingStatus.Should().Be("ai_graded");
        finalTicket.Status.Should().Be(ExamTicketStatus.AiGraded);
        finalFailedJob.Status.Should().Be("completed");
        (await verifyDb.DeadLetterQueues.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "PR2-2.5F: CreateExamSet chặn câu Rejected/NeedsRevision/IsDeleted/trùng ID/khác môn và ApproveExamSet không hồi sinh câu đã xóa mềm hoặc bị từ chối")]
    public async Task Pr2_2_5_ExamSetCreationAndApproval_BlocksRejectedNeedsRevisionDeletedDuplicates_AndNeverResurrects()
    {
        await using var db = CreateDbContext();
        var head = new User { Id = Guid.NewGuid(), Email = "head.neg@fpt.edu.vn", FullName = "Head Neg", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "lec.neg@fpt.edu.vn", FullName = "Lec Neg", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", DepartmentHeadUserId = head.Id, IsActive = true };
        var otherCourse = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", DepartmentHeadUserId = head.Id, IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10m, IsActive = true };

        // Kịch bản nguy hiểm: câu Rejected/NeedsRevision nhưng có IsActive=true (mặc định DB cũ)
        var rejectedActive = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Rejected", Content = "R", ApprovalStatus = ApprovalStatus.Rejected, IsActive = true, IsDeleted = false };
        var needsRevActive = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "NeedsRev", Content = "N", ApprovalStatus = ApprovalStatus.NeedsRevision, IsActive = true, IsDeleted = false };
        var deletedDraft = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "Deleted", Content = "D", ApprovalStatus = ApprovalStatus.Draft, IsActive = false, IsDeleted = true };
        var foreignApproved = new ExamQuestion { Id = Guid.NewGuid(), CourseId = otherCourse.Id, RubricId = rubric.Id, Title = "Foreign", Content = "F", ApprovalStatus = ApprovalStatus.Approved, IsActive = true, IsDeleted = false };
        var validDraft = new ExamQuestion { Id = Guid.NewGuid(), CourseId = course.Id, RubricId = rubric.Id, Title = "ValidDraft", Content = "V", SampleAnswer = new string('s', 60), ApprovalStatus = ApprovalStatus.Draft, IsActive = false, IsDeleted = false };

        db.Users.AddRange(head, lecturer);
        db.Courses.AddRange(course, otherCourse);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = course.Id, LecturerUserId = lecturer.Id, IsActive = true });
        db.ExamStructures.Add(structure);
        db.Rubrics.Add(rubric);
        db.ExamQuestions.AddRange(rejectedActive, needsRevActive, deletedDraft, foreignApproved, validDraft);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);

        (await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-REJ", [rejectedActive.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-REV", [needsRevActive.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-DEL", [deletedDraft.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-FOR", [foreignApproved.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None)).IsSuccess.Should().BeFalse();
        (await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-DUP", [validDraft.Id, validDraft.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None)).IsSuccess.Should().BeFalse();

        // Tạo bộ đề hợp lệ với câu nháp, sau đó câu nháp bị xóa mềm trước khi TBM duyệt bộ đề -> ApproveExamSet phải chặn và KHÔNG hồi sinh
        var createdSet = await createHandler.Handle(new CreateExamSetCommand(structure.Id, course.Id, null, "SET-VALID", [validDraft.Id], lecturer.Id, UserRole.Lecturer), CancellationToken.None);
        createdSet.IsSuccess.Should().BeTrue(because: createdSet.Error);

        validDraft.IsDeleted = true;
        await db.SaveChangesAsync();

        var approveAfterDelete = await new ApproveExamSetCommandHandler(db).Handle(
            new ApproveExamSetCommand(createdSet.Value.Id, "Duyệt", head.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        approveAfterDelete.IsSuccess.Should().BeFalse();
        approveAfterDelete.Error.Should().StartWith("CONFLICT:");

        var qReloaded = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == validDraft.Id);
        qReloaded.IsDeleted.Should().BeTrue();
        qReloaded.IsActive.Should().BeFalse();
        qReloaded.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
    }

    [Fact(DisplayName = "PR2-2.5G: Voice-First Gate chặn 4xx từ STT dù có WebM magic bytes và chặn byte rác dù transcript đã ở cache verified")]
    public async Task Pr2_2_5_MockExamVoiceFirstGate_Rejects4xxSttErrorAndFakeBytesWithCachedVerifiedTranscript()
    {
        await using var db = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        db.Courses.Add(new Course { Id = courseId, Code = "PRN231", Name = "PRN", IsActive = true });
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(28),
            Status = "in_progress"
        };
        session.Answers.Add(new MockExamAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = questionId, Status = "pending" });
        db.MockExamSessions.Add(session);
        await db.SaveChangesAsync();

        // 1. 4xx HttpRequestException (audio hỏng / giả mạo container) -> KHÔNG cho fallback DirectTranscript
        var stt400Mock = new Mock<ISpeechToTextService>();
        stt400Mock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Malformed WebM payload", null, HttpStatusCode.BadRequest));
        stt400Mock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        using var corruptWebm = new MemoryStream(CreateValidWebmHeaderBytes());
        var res400 = await new SubmitMockExamAnswerCommandHandler(db, stt400Mock.Object).Handle(
            new SubmitMockExamAnswerCommand(session.Id, studentId, questionId, corruptWebm, "bad.webm", "audio/webm", 20, DirectTranscript: "Văn bản chèn qua lỗi 400"),
            CancellationToken.None);
        res400.IsSuccess.Should().BeFalse();
        res400.Error.Should().Contain("Lỗi khi bóc băng âm thanh");

        // 2. Transcript đã đăng ký vào cache verified + 1 byte rác không có WebM header -> KHÔNG được hit cache
        const string cachedText = "Clean Architecture tách biệt tầng Domain khỏi Infrastructure và API";
        TranscriptHelper.RegisterVerifiedTranscript(cachedText);

        var sttEmptyMock = new Mock<ISpeechToTextService>();
        sttEmptyMock.Setup(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(string.Empty, string.Empty, "CloudflareWhisper", false, ConfidenceScore: 0.0));
        sttEmptyMock.Setup(x => x.NormalizeTranscript(It.IsAny<string>())).Returns<string>(s => s);

        using var junkStream = new MemoryStream([0x41]);
        var resCachedJunk = await new SubmitMockExamAnswerCommandHandler(db, sttEmptyMock.Object).Handle(
            new SubmitMockExamAnswerCommand(session.Id, studentId, questionId, junkStream, "junk.webm", "audio/webm", 20, DirectTranscript: cachedText),
            CancellationToken.None);
        resCachedJunk.IsSuccess.Should().BeFalse();
        sttEmptyMock.Verify(x => x.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "PR2-2.3D: ImportExamRoster phát hiện trùng MSSV không phân biệt hoa/thường và loại trừ bộ đề mẫu IsSampleSet")]
    public async Task Pr2_2_3_ImportExamRoster_DetectsCaseInsensitiveDuplicateStudentCode_AndExcludesSampleSet()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo.dup@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var sv1 = new User { Id = Guid.NewGuid(), Email = "sv.dup@fpt.edu.vn", FullName = "SV Dup", StudentCode = "SE189901", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN", IsActive = true };
        var structure = new ExamStructure { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Struct", IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, ExamStructureId = structure.Id, Title = "FE", ExamDate = new DateOnly(2026, 12, 1), Status = "scheduled" };
        var shift = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = session.Id, ShiftName = "Ca 1", RoomLab = "Lab 1", MaxSeats = 24, Status = "scheduled" };

        db.Users.AddRange(officer, sv1);
        db.Courses.Add(course);
        db.ExamStructures.Add(structure);
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        // Chỉ có 1 bộ đề nhưng là Đề Mẫu (IsSampleSet = true) -> không được tính vào bộ đề phát thi
        db.ExamSets.Add(new ExamSet
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StructureId = structure.Id,
            SessionId = session.Id,
            SetCode = "SAMPLE-01",
            Status = ExamSetStatus.HandedOver,
            IsSampleSet = true,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var previewHandler = new PreviewExamRosterImportQueryHandler(db);
        var dupCasePreview = await previewHandler.Handle(new PreviewExamRosterImportQuery(
            officer.Id,
            shift.Id,
            [
                new BulkExamTicketImportRow(2, "se189901", 1, string.Empty),
                new BulkExamTicketImportRow(3, "SE189901", 2, string.Empty)
            ]), CancellationToken.None);

        dupCasePreview.IsSuccess.Should().BeTrue();
        dupCasePreview.Value.CanCommit.Should().BeFalse();
        dupCasePreview.Value.Issues.Should().Contain(i => i.Message.Contains("MSSV bị lặp"));

        var sampleOnlyPreview = await previewHandler.Handle(new PreviewExamRosterImportQuery(
            officer.Id,
            shift.Id,
            [new BulkExamTicketImportRow(2, "SE189901", 1, string.Empty)]), CancellationToken.None);

        sampleOnlyPreview.IsSuccess.Should().BeTrue();
        sampleOnlyPreview.Value.CanCommit.Should().BeFalse();
        sampleOnlyPreview.Value.AvailableHandedOverSetCount.Should().Be(0, "Đề mẫu IsSampleSet=true tuyệt đối không được tính vào nguồn bộ đề phát cho thí sinh");
    }
}

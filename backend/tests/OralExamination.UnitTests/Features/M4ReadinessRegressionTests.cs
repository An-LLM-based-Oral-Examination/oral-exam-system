using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using OfficeOpenXml;
using OralExamination.API.Configuration;
using OralExamination.API.Hubs;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.DeleteInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.MarkInClassParticipantAbsent;
using OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.ExportInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using OralExamination.Application.Features.Speech.Commands.TranscribeSpeech;
using OralExamination.Application.Features.Speech.Queries.SynthesizeSpeech;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using OralExamination.Infrastructure.Reporting;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features;

public sealed class M4ReadinessRegressionTests
{
    private static OralExamDbContext CreateDbContext(bool withLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"M4Regression_{Guid.NewGuid():N}");
        if (withLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }

        return new OralExamDbContext(builder.Options);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static byte[] CreateValidWebmBytes(int totalBytes = 64)
    {
        var bytes = new byte[Math.Max(16, totalBytes)];
        bytes[0] = 0x1A;
        bytes[1] = 0x45;
        bytes[2] = 0xDF;
        bytes[3] = 0xA3;
        return bytes;
    }

    private sealed class NonSeekableReadStream(byte[] payload) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = payload.Length - _offset;
            if (remaining <= 0)
            {
                return 0;
            }

            var toCopy = Math.Min(count, remaining);
            Buffer.BlockCopy(payload, _offset, buffer, offset, toCopy);
            _offset += toCopy;
            return toCopy;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // =========================================================================
    // ITEM 25: MF-05 AdjustScore QuestionTitle/Content + SignalR + SubmitAnswer 202
    // =========================================================================

    [Fact(DisplayName = "M4-25-01: AdjustInClassSubmissionScore trả về QuestionTitle/QuestionContent và đẩy SignalR staff payload đầy đủ nhưng ẩn điểm student")]
    public async Task Item25_AdjustScore_PopulatesQuestionTitleAndContent_AndBroadcastsCorrectSignalRPayloads()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv25@fpt.edu.vn", FullName = "GV 25", Role = UserRole.Lecturer, IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv25@fpt.edu.vn", FullName = "SV 25", StudentCode = "SE250001", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1801", Name = "SE1801", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Nguyên lý Dependency Inversion",
            Content = "Giải thích DIP trong Clean Architecture.",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca 25",
            TotpSecretKey = "secret-25",
            Status = "CLOSED",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = JsonSerializer.Serialize(new[] { question.Id }),
            Status = "GRADED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Module cấp cao không phụ thuộc module cấp thấp.",
            AiScore = 7.0m,
            FinalScore = 7.0m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-20)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.PracticeQuestions.Add(question);
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var handler = new AdjustInClassSubmissionScoreCommandHandler(db, new AdjustInClassSubmissionScoreCommandValidator());
        var result = await handler.Handle(
            new AdjustInClassSubmissionScoreCommand(lecturer.Id, submission.Id, 8.5m, "Trả lời đúng trọng tâm DIP.", shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.QuestionTitle.Should().Be(question.Title);
        result.Value.QuestionContent.Should().Be(question.Content);
        result.Value.FinalScore.Should().Be(8.5m);

        // Verify InClassShiftsController emits QuestionTitle/QuestionContent to StaffGroupName and redacts StudentGroupName
        object? staffPayload = null;
        object? studentPayload = null;

        var staffClientMock = new Mock<IInClassShiftClient>();
        staffClientMock
            .Setup(c => c.InClassSubmissionGraded(It.IsAny<object>()))
            .Callback<object>(p => staffPayload = p)
            .Returns(Task.CompletedTask);

        var studentClientMock = new Mock<IInClassShiftClient>();
        studentClientMock
            .Setup(c => c.InClassSubmissionGraded(It.IsAny<object>()))
            .Callback<object>(p => studentPayload = p)
            .Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients<IInClassShiftClient>>();
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StaffGroupName(shift.Id))).Returns(staffClientMock.Object);
        hubClientsMock.Setup(c => c.Group(InClassShiftHub.StudentGroupName(shift.Id, student.Id))).Returns(studentClientMock.Object);

        var hubMock = new Mock<IHubContext<InClassShiftHub, IInClassShiftClient>>();
        hubMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<AdjustInClassSubmissionScoreCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var controller = new InClassShiftsController(senderMock.Object, hubMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(lecturer.Id, UserRole.Lecturer) }
            }
        };

        var actionResult = await controller.AdjustSubmissionScore(
            shift.Id,
            submission.Id,
            new AdjustSubmissionScoreRequest(8.5m, "Trả lời đúng trọng tâm DIP."),
            CancellationToken.None);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var okDto = ok.Value.Should().BeOfType<InClassExamSubmissionResponse>().Subject;
        okDto.QuestionTitle.Should().Be(question.Title);
        okDto.QuestionContent.Should().Be(question.Content);

        var staffDto = staffPayload.Should().BeOfType<InClassExamSubmissionResponse>().Subject;
        staffDto.QuestionTitle.Should().Be(question.Title);
        staffDto.QuestionContent.Should().Be(question.Content);

        var studentJson = JsonSerializer.Serialize(studentPayload);
        studentJson.Should().Contain("AUDITED");
        studentJson.Should().NotContain("8.5");
        studentJson.Should().NotContain(question.Title);
    }

    // =========================================================================
    // ITEM 26: MF-05 CourseName, MyFinalScore, Ongoing Update, ABSENT, Excel Export
    // =========================================================================

    [Fact(DisplayName = "M4-26-01: CreateInClassShift và GetInClassShiftsByClass điền đủ CourseName và MyFinalScore (ẩn khi ABSENT hoặc chưa FINALIZED)")]
    public async Task Item26_CreateAndByClass_PopulateCourseName_AndMyFinalScoreCorrectly()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv26a@fpt.edu.vn", FullName = "GV 26A", Role = UserRole.Lecturer, IsActive = true };
        var studentPresent = new User { Id = Guid.NewGuid(), Email = "sv26p@fpt.edu.vn", FullName = "SV Present", StudentCode = "SE260001", Role = UserRole.Student, IsActive = true };
        var studentAbsent = new User { Id = Guid.NewGuid(), Email = "sv26a@fpt.edu.vn", FullName = "SV Absent", StudentCode = "SE260002", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Software Architecture and Design", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1826", Name = "SWD392-SE1826", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };
        var q = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "CQRS",
            Content = "Explain CQRS",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        db.Users.AddRange(lecturer, studentPresent, studentAbsent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.PracticeQuestions.Add(q);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentPresent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = studentAbsent.Id, Status = "enrolled" });
        await db.SaveChangesAsync();

        // 1. CreateInClassShift populates CourseName
        var createHandler = new CreateInClassShiftCommandHandler(db, new CreateInClassShiftCommandValidator());
        var createRes = await createHandler.Handle(
            new CreateInClassShiftCommand(
                lecturer.Id,
                cls.Id,
                "Ca thi 26",
                DateTime.UtcNow.AddMinutes(5),
                DateTime.UtcNow.AddMinutes(45),
                ExamDurationMinutes: 20),
            CancellationToken.None);

        createRes.IsSuccess.Should().BeTrue(createRes.Error);
        createRes.Value.CourseCode.Should().Be("SWD392");
        createRes.Value.CourseName.Should().Be("Software Architecture and Design");
        createRes.Value.MyFinalScore.Should().BeNull();

        // 2. Finalized shift returns MyFinalScore for present student, null for ABSENT student and null for lecturer
        var shift = await db.InClassExamShifts.FirstAsync(s => s.Id == createRes.Value.Id);
        shift.Status = "FINALIZED";
        shift.IsLocked = true;
        shift.FinalizedAt = DateTime.UtcNow;
        db.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = studentPresent.Id,
                Status = "FINALIZED",
                TotalFinalScore = 9.25m,
                JoinedAt = DateTime.UtcNow.AddMinutes(-20)
            },
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = studentAbsent.Id,
                Status = "ABSENT",
                TotalFinalScore = null,
                JoinedAt = DateTime.UtcNow.AddMinutes(-20),
                FinishedAt = DateTime.UtcNow.AddMinutes(-20)
            });

        // Open shift (not FINALIZED) must hide score
        var openShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca chưa chốt",
            TotpSecretKey = "secret-open",
            Status = "CLOSED",
            IsLocked = false,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddMinutes(-10)
        };
        db.InClassExamShifts.Add(openShift);
        db.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = openShift.Id,
            StudentId = studentPresent.Id,
            Status = "GRADED",
            TotalFinalScore = 8.0m,
            JoinedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var byClassHandler = new GetInClassShiftsByClassQueryHandler(db);

        var presentRes = await byClassHandler.Handle(new GetInClassShiftsByClassQuery(studentPresent.Id, cls.Id), CancellationToken.None);
        presentRes.IsSuccess.Should().BeTrue();
        presentRes.Value.Should().HaveCount(2);
        presentRes.Value.Should().OnlyContain(s => s.CourseName == "Software Architecture and Design");
        presentRes.Value.Single(s => s.Id == shift.Id).MyFinalScore.Should().Be(9.25m);
        presentRes.Value.Single(s => s.Id == openShift.Id).MyFinalScore.Should().BeNull();

        var absentRes = await byClassHandler.Handle(new GetInClassShiftsByClassQuery(studentAbsent.Id, cls.Id), CancellationToken.None);
        absentRes.IsSuccess.Should().BeTrue();
        absentRes.Value.Single(s => s.Id == shift.Id).MyFinalScore.Should().BeNull();

        var lecturerRes = await byClassHandler.Handle(new GetInClassShiftsByClassQuery(lecturer.Id, cls.Id), CancellationToken.None);
        lecturerRes.IsSuccess.Should().BeTrue();
        lecturerRes.Value.Should().OnlyContain(s => s.CourseName == "Software Architecture and Design" && s.MyFinalScore == null);
    }

    [Fact(DisplayName = "M4-26-02: UpdateInClassShift cho phép sửa ca SCHEDULED đang diễn ra, chặn lùi StartTime và giữ Delete chặn AlreadyStarted")]
    public async Task Item26_UpdateInClassShift_AllowsOngoingScheduledShift_WhileRejectingBackdate_AndKeepingDeleteGuard()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv26u@fpt.edu.vn", FullName = "GV 26U", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1827", Name = "SE1827", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };
        var originalStart = DateTime.UtcNow.AddMinutes(-5);
        var originalEnd = DateTime.UtcNow.AddMinutes(35);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đang chạy",
            TotpSecretKey = "secret-ongoing",
            Status = "SCHEDULED",
            StartTime = originalStart,
            EndTime = originalEnd,
            ExamDurationMinutes = 20
        };
        var q = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Q1",
            Content = "Content",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        db.Users.Add(lecturer);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.InClassExamShifts.Add(shift);
        db.PracticeQuestions.Add(q);
        await db.SaveChangesAsync();

        var updateHandler = new UpdateInClassShiftCommandHandler(db);

        // 1. Backdating start < shift.StartTime is rejected
        var backdateRes = await updateHandler.Handle(
            new UpdateInClassShiftCommand(lecturer.Id, shift.Id, "Backdate", originalStart.AddMinutes(-1), originalEnd.AddMinutes(10)),
            CancellationToken.None);
        backdateRes.IsSuccess.Should().BeFalse();
        shift.ShiftName.Should().Be("Ca đang chạy");

        // 2. EndTime in the past is rejected
        var pastEndRes = await updateHandler.Handle(
            new UpdateInClassShiftCommand(lecturer.Id, shift.Id, "PastEnd", originalStart, DateTime.UtcNow.AddMinutes(-1)),
            CancellationToken.None);
        pastEndRes.IsSuccess.Should().BeFalse();

        // 3. Keeping original StartTime (ongoing) and extending EndTime succeeds
        var validOngoingRes = await updateHandler.Handle(
            new UpdateInClassShiftCommand(lecturer.Id, shift.Id, "Ca đã gia hạn", originalStart, DateTime.UtcNow.AddMinutes(55)),
            CancellationToken.None);
        validOngoingRes.IsSuccess.Should().BeTrue(validOngoingRes.Error);
        shift.ShiftName.Should().Be("Ca đã gia hạn");

        // 4. DeleteInClassShift still rejects AlreadyStarted for ongoing shift
        var deleteRes = await new DeleteInClassShiftCommandHandler(db)
            .Handle(new DeleteInClassShiftCommand(lecturer.Id, shift.Id), CancellationToken.None);
        deleteRes.IsSuccess.Should().BeFalse();
        deleteRes.Error.Should().Be(ShiftMutationErrors.AlreadyStarted);
    }

    [Fact(DisplayName = "M4-26-03: MarkInClassParticipantAbsent xử lý đủ happy-path, idempotent, 409 SCHEDULED/đã nộp, 403 locked/unauthorized và 404")]
    public async Task Item26_MarkInClassParticipantAbsent_EnforcesAllGuardsAndIdempotency()
    {
        await using var db = CreateDbContext();
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv26ab@fpt.edu.vn", FullName = "GV Absent", Role = UserRole.Lecturer, IsActive = true };
        var otherLecturer = new User { Id = Guid.NewGuid(), Email = "gv26other@fpt.edu.vn", FullName = "GV Other", Role = UserRole.Lecturer, IsActive = true };
        var noShowStudent = new User { Id = Guid.NewGuid(), Email = "sv1@fpt.edu.vn", FullName = "No Show", StudentCode = "SE261001", Role = UserRole.Student, IsActive = true };
        var submittedStudent = new User { Id = Guid.NewGuid(), Email = "sv2@fpt.edu.vn", FullName = "Submitted", StudentCode = "SE261002", Role = UserRole.Student, IsActive = true };
        var outsiderStudent = new User { Id = Guid.NewGuid(), Email = "sv3@fpt.edu.vn", FullName = "Outsider", StudentCode = "SE261003", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1828", Name = "SE1828", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };

        var openShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca mở",
            TotpSecretKey = "secret-1",
            Status = "IN_PROGRESS",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            EndTime = DateTime.UtcNow.AddMinutes(30)
        };
        var scheduledShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca chờ",
            TotpSecretKey = "secret-2",
            Status = "SCHEDULED",
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2)
        };
        var lockedShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đã chốt",
            TotpSecretKey = "secret-3",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1)
        };

        db.Users.AddRange(lecturer, otherLecturer, noShowStudent, submittedStudent, outsiderStudent);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = noShowStudent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = submittedStudent.Id, Status = "enrolled" });
        db.InClassExamShifts.AddRange(openShift, scheduledShift, lockedShift);
        db.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = openShift.Id,
            StudentId = submittedStudent.Id,
            Status = "JOINED",
            JoinedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        db.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = openShift.Id,
            StudentId = submittedStudent.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Đã nộp bài",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var handler = new MarkInClassParticipantAbsentCommandHandler(db);

        // Unauthorized lecturer -> LecturerNotAuthorized
        var unauth = await handler.Handle(new MarkInClassParticipantAbsentCommand(otherLecturer.Id, openShift.Id, noShowStudent.Id), CancellationToken.None);
        unauth.IsSuccess.Should().BeFalse();
        unauth.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);

        // Locked shift -> ShiftLocked
        var locked = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, lockedShift.Id, noShowStudent.Id), CancellationToken.None);
        locked.IsSuccess.Should().BeFalse();
        locked.Error.Should().Be(InClassShiftErrors.ShiftLocked);

        // SCHEDULED shift -> CONFLICT:
        var scheduled = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, scheduledShift.Id, noShowStudent.Id), CancellationToken.None);
        scheduled.IsSuccess.Should().BeFalse();
        scheduled.Error.Should().StartWith("CONFLICT:");

        // Not enrolled -> NOT_FOUND:
        var outsider = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, openShift.Id, outsiderStudent.Id), CancellationToken.None);
        outsider.IsSuccess.Should().BeFalse();
        outsider.Error.Should().StartWith("NOT_FOUND:");

        // Already has submission -> CONFLICT:
        var hasSub = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, openShift.Id, submittedStudent.Id), CancellationToken.None);
        hasSub.IsSuccess.Should().BeFalse();
        hasSub.Error.Should().StartWith("CONFLICT:");

        // Happy path -> creates ABSENT participant
        var ok1 = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, openShift.Id, noShowStudent.Id), CancellationToken.None);
        ok1.IsSuccess.Should().BeTrue(ok1.Error);
        ok1.Value.Status.Should().Be("ABSENT");
        ok1.Value.StudentCode.Should().Be("SE261001");

        // Idempotent repeat -> succeeds without duplicate
        var ok2 = await handler.Handle(new MarkInClassParticipantAbsentCommand(lecturer.Id, openShift.Id, noShowStudent.Id), CancellationToken.None);
        ok2.IsSuccess.Should().BeTrue(ok2.Error);
        ok2.Value.Status.Should().Be("ABSENT");
        (await db.InClassShiftParticipants.CountAsync(p => p.ShiftId == openShift.Id && p.StudentId == noShowStudent.Id)).Should().Be(1);
    }

    [Fact(DisplayName = "M4-26-04: ExportInClassShiftGrades xuất file .xlsx hợp lệ qua EPPlus gồm cả SV đã chấm, ABSENT, NOT_JOINED và chống formula injection")]
    public async Task Item26_ExportInClassShiftGrades_ProducesSanitizedXlsxWithAllEnrolledAndAbsentStudents()
    {
        await using var db = CreateDbContext();
        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var lecturer = new User { Id = Guid.NewGuid(), Email = "gv26ex@fpt.edu.vn", FullName = "GV Export", Role = UserRole.Lecturer, IsActive = true };
        var otherLecturer = new User { Id = Guid.NewGuid(), Email = "gv26ex2@fpt.edu.vn", FullName = "GV Other", Role = UserRole.Lecturer, IsActive = true };
        var svGraded = new User { Id = Guid.NewGuid(), Email = "sv1@fpt.edu.vn", FullName = "=CMD|'/C calc'!A0", StudentCode = "SE262001", Role = UserRole.Student, IsActive = true };
        var svAbsent = new User { Id = Guid.NewGuid(), Email = "sv2@fpt.edu.vn", FullName = "SV Vắng", StudentCode = "SE262002", Role = UserRole.Student, IsActive = true };
        var svNotJoined = new User { Id = Guid.NewGuid(), Email = "sv3@fpt.edu.vn", FullName = "SV Chưa Vào", StudentCode = "SE262003", Role = UserRole.Student, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "Web API", SemesterId = semester.Id, Semester = semester, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1829", Name = "PRN231-SE1829", CourseId = course.Id, Course = course, LecturerId = lecturer.Id, IsActive = true };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            Class = cls,
            LecturerId = lecturer.Id,
            Lecturer = lecturer,
            ShiftName = "Ca Export 1",
            TotpSecretKey = "secret-export",
            Status = "FINALIZED",
            IsLocked = true,
            FinalizedAt = DateTime.UtcNow,
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1)
        };

        db.Semesters.Add(semester);
        db.Users.AddRange(lecturer, otherLecturer, svGraded, svAbsent, svNotJoined);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = svGraded.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = svAbsent.Id, Status = "enrolled" },
            new ClassEnrollment { Id = Guid.NewGuid(), ClassId = cls.Id, StudentId = svNotJoined.Id, Status = "enrolled" });
        db.InClassExamShifts.Add(shift);
        db.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = svGraded.Id,
                Status = "FINALIZED",
                TotalAiScore = 8.0m,
                TotalFinalScore = 8.75m,
                JoinedAt = DateTime.UtcNow.AddMinutes(-90),
                FinishedAt = DateTime.UtcNow.AddMinutes(-70)
            },
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = svAbsent.Id,
                Status = "ABSENT",
                JoinedAt = DateTime.UtcNow.AddMinutes(-65),
                FinishedAt = DateTime.UtcNow.AddMinutes(-65)
            });
        db.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = svGraded.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Trả lời tốt",
            AiScore = 8.0m,
            FinalScore = 8.75m,
            LecturerNotes = "+100% đạt chuẩn",
            Status = "AUDITED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-75),
            GradedAt = DateTime.UtcNow.AddMinutes(-68)
        });
        await db.SaveChangesAsync();

        var exporter = new InClassShiftReportExporter();
        var handler = new ExportInClassShiftGradesQueryHandler(db, exporter);

        // Unauthorized lecturer rejected
        var denied = await handler.Handle(new ExportInClassShiftGradesQuery(otherLecturer.Id, shift.Id), CancellationToken.None);
        denied.IsSuccess.Should().BeFalse();
        denied.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);

        // Valid export
        var res = await handler.Handle(new ExportInClassShiftGradesQuery(lecturer.Id, shift.Id), CancellationToken.None);
        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        res.Value.FileName.Should().EndWith(".xlsx");
        res.Value.FileContents.Should().NotBeEmpty();

        using var ms = new MemoryStream(res.Value.FileContents);
        using var pkg = new ExcelPackage(ms);
        var ws = pkg.Workbook.Worksheets[0];
        ws.Cells[9, 2].Text.Should().Be("SE262001");
        ws.Cells[9, 3].Text.Should().Be("'=CMD|'/C calc'!A0");
        ws.Cells[9, 5].Text.Should().Be("FINALIZED");
        Convert.ToDecimal(ws.Cells[9, 7].Value).Should().Be(8.75m);
        ws.Cells[9, 8].Text.Should().Be("'+100% đạt chuẩn");

        ws.Cells[10, 2].Text.Should().Be("SE262002");
        ws.Cells[10, 5].Text.Should().Be("ABSENT");
        ws.Cells[10, 7].Value.Should().BeNull();

        ws.Cells[11, 2].Text.Should().Be("SE262003");
        ws.Cells[11, 5].Text.Should().Be("NOT_JOINED");

        // Controller returns FileContentResult
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<ExportInClassShiftGradesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(res);
        var controller = new InClassShiftsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(lecturer.Id, UserRole.Lecturer) }
            }
        };
        var httpRes = await controller.ExportShiftGrades(shift.Id, CancellationToken.None);
        var fileResult = httpRes.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // =========================================================================
    // ITEM 27: DLQ 409, Speech/Storage 413/502/504, CORS, OfficialExamGradingWorker
    // =========================================================================

    [Fact(DisplayName = "M4-27-01: DeadLetterQueueController trả 409 Conflict cho lỗi CONFLICT và giữ 400 cho resolved")]
    public async Task Item27_DeadLetterQueueController_MapsConflictTo409_WhilePreserving400OnResolved()
    {
        var senderMock = new Mock<ISender>();
        var controller = new DeadLetterQueueController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(Guid.NewGuid(), UserRole.Admin) }
            }
        };

        var taskId = Guid.NewGuid();
        senderMock
            .Setup(s => s.Send(It.IsAny<ReplayDlqTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure("CONFLICT: Tác vụ DLQ này vừa được đưa lại vào hàng đợi chấm điểm."));

        var replayConflict = await controller.ReplayAdminDlqTask(taskId);
        var conflictObj = replayConflict.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictObj.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflictObj.Value.Should().BeOfType<ProblemDetails>().Which.Status.Should().Be(409);

        senderMock
            .Setup(s => s.Send(It.IsAny<ResolveDlqTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure("CONFLICT: Đang xử lý đồng thời."));

        var resolveConflict = await controller.ResolveAdminDlqTask(taskId);
        resolveConflict.Should().BeOfType<ConflictObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "M4-27-02: SpeechController & StorageController trả 413 (>25MB seekable/non-seekable), 504 (Timeout) và 502 (Upstream Error)")]
    public async Task Item27_SpeechAndStorage_Return413OnOversize_And504Or502OnUpstreamFailures()
    {
        // 1. SpeechController >25MB seekable -> 413 PAYLOAD_TOO_LARGE
        var senderMock = new Mock<ISender>();
        var speechController = new SpeechController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(Guid.NewGuid(), UserRole.Student) }
            }
        };

        var bigFileMock = new Mock<IFormFile>();
        bigFileMock.Setup(f => f.Length).Returns(26L * 1024 * 1024);
        bigFileMock.Setup(f => f.FileName).Returns("big.webm");
        var speech413 = await speechController.TranscribeSpeech(bigFileMock.Object);
        var speech413Obj = speech413.Should().BeOfType<ObjectResult>().Subject;
        speech413Obj.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        speech413Obj.Value.Should().BeOfType<ProblemDetails>().Which.Extensions["errorCode"].Should().Be("PAYLOAD_TOO_LARGE");

        // 2. Non-seekable stream >25MB on SpeechController & StorageController -> 413
        var oversizedPayload = new byte[(25 * 1024 * 1024) + 128];
        oversizedPayload[0] = 0x1A;
        oversizedPayload[1] = 0x45;
        oversizedPayload[2] = 0xDF;
        oversizedPayload[3] = 0xA3;

        var streamFileMock = new Mock<IFormFile>();
        streamFileMock.Setup(f => f.Length).Returns(1024); // Claimed small Content-Length, actual non-seekable stream exceeds 25MB
        streamFileMock.Setup(f => f.FileName).Returns("streamed.webm");
        streamFileMock.Setup(f => f.ContentType).Returns("audio/webm");
        streamFileMock.Setup(f => f.OpenReadStream()).Returns(() => new NonSeekableReadStream(oversizedPayload));

        var speechStream413 = await speechController.TranscribeSpeech(streamFileMock.Object);
        speechStream413.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);

        var storageController = new StorageController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(Guid.NewGuid(), UserRole.Student) }
            }
        };
        var storageStream413 = await storageController.UploadAudio(streamFileMock.Object);
        storageStream413.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);

        // 3. Real Handlers + Controllers: Upstream Timeout -> 504, Upstream HttpRequestException/WebSocketException -> 502, Caller Abort -> Propagates
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cả Phương án 1 và Phương án 2 đều thất bại", new TimeoutException("Whisper timed out")));

        var sttHandler = new TranscribeSpeechCommandHandler(sttMock.Object);
        var sttTimeoutRes = await sttHandler.Handle(new TranscribeSpeechCommand(new MemoryStream(CreateValidWebmBytes()), "a.webm"), CancellationToken.None);
        sttTimeoutRes.IsSuccess.Should().BeFalse();
        sttTimeoutRes.Error.Should().StartWith("GATEWAY_TIMEOUT:");

        senderMock.Setup(s => s.Send(It.IsAny<TranscribeSpeechCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sttTimeoutRes);
        var stt504 = await speechController.TranscribeSpeech(new FormFile(new MemoryStream(CreateValidWebmBytes()), 0, 64, "file", "a.webm"));
        stt504.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status504GatewayTimeout);

        // TTS upstream WebSocketException wrapped in InvalidOperationException -> 502 BadGateway
        var ttsMock = new Mock<ITextToSpeechService>();
        ttsMock.Setup(t => t.SynthesizeSpeechAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cả Phương án 1 và Phương án 2 đều thất bại", new WebSocketException("Edge WS failed")));

        var ttsHandler = new SynthesizeSpeechQueryHandler(ttsMock.Object);
        var tts502Res = await ttsHandler.Handle(new SynthesizeSpeechQuery("Xin chào"), CancellationToken.None);
        tts502Res.IsSuccess.Should().BeFalse();
        tts502Res.Error.Should().StartWith("BAD_GATEWAY:");

        senderMock.Setup(s => s.Send(It.IsAny<SynthesizeSpeechQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tts502Res);
        var ttsHttp502 = await speechController.SynthesizeSpeechPost(new SynthesizeSpeechHttpRequest("Xin chào"));
        ttsHttp502.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        // UploadAudioPracticeCommandHandler HttpRequestException -> 502; TimeoutException -> 504
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Upstream 500", null, HttpStatusCode.InternalServerError));
        var uploadHandler = new UploadAudioPracticeCommandHandler(sttMock.Object);
        var upload502Res = await uploadHandler.Handle(
            new UploadAudioPracticeCommand(new MemoryStream(CreateValidWebmBytes()), "a.webm", "audio/webm"),
            CancellationToken.None);
        upload502Res.IsSuccess.Should().BeFalse();
        upload502Res.Error.Should().StartWith("BAD_GATEWAY:");

        senderMock.Setup(s => s.Send(It.IsAny<UploadAudioPracticeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload502Res);
        var storage502 = await storageController.UploadAudio(new FormFile(new MemoryStream(CreateValidWebmBytes()), 0, 64, "file", "a.webm"));
        storage502.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);

        // Caller cancellation propagates
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(callerCts.Token));
        Func<Task> cancelledAct = () => sttHandler.Handle(new TranscribeSpeechCommand(new MemoryStream(CreateValidWebmBytes()), "a.webm"), callerCts.Token);
        await cancelledAct.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "M4-27-03: CORS Policy expose đủ X-TTS-Rate và X-TTS-Cache")]
    public void Item27_CorsPolicy_ExposesTtsRateAndCacheHeaders()
    {
        var services = new ServiceCollection();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://oralexam.io.vn" })
            .Build();
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        services.AddOralExamCors(cfg, env.Object);
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IOptions<CorsOptions>>().Value.GetPolicy(OralExamCorsExtensions.PolicyName);
        policy.Should().NotBeNull();
        policy!.ExposedHeaders.Should().Contain(["X-TTS-Rate", "X-TTS-Cache", "X-TTS-Provider", "X-TTS-Voice", "X-TTS-Fallback"]);
    }

    [Fact(DisplayName = "M4-27-04: OfficialExamGradingWorker gán tường minh FlowType = OFFICIAL_EXAM và ManagedByRole = examination_officer trên cả 2 nhánh tạo DLQ")]
    public async Task Item27_OfficialExamGradingWorker_ExplicitlySetsDlqFlowTypeAndRoleOnBothFailurePaths()
    {
        await using var db = CreateDbContext();
        var jobWithSubmissionId = Guid.NewGuid();
        var jobWithoutSubmissionId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        db.GradingJobs.AddRange(
            new GradingJob
            {
                Id = jobWithSubmissionId,
                SubmissionId = submissionId,
                Status = "processing",
                AttemptCount = 1
            },
            new GradingJob
            {
                Id = jobWithoutSubmissionId,
                SubmissionId = Guid.NewGuid(),
                Status = "processing",
                AttemptCount = 1
            });

        db.ExamQuestionSubmissions.Add(new ExamQuestionSubmission
        {
            Id = submissionId,
            TicketId = Guid.NewGuid(),
            ExamQuestionId = Guid.NewGuid(),
            GradingStatus = "submitted",
            SubmittedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var markFailedCore = typeof(OfficialExamGradingWorker).GetMethod(
            "MarkFailedCoreAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        markFailedCore.Should().NotBeNull();

        // Branch 1: submission != null
        await (Task)markFailedCore!.Invoke(
            null,
            [db, new OfficialExamGradingTask(jobWithSubmissionId, submissionId), null, new InvalidOperationException("Gemini failure 1"), CancellationToken.None])!;

        // Branch 2: submission == null fallback
        await (Task)markFailedCore.Invoke(
            null,
            [db, new OfficialExamGradingTask(jobWithoutSubmissionId, Guid.NewGuid()), null, new InvalidOperationException("Gemini failure 2"), CancellationToken.None])!;

        var dlqs = await db.DeadLetterQueues.AsNoTracking().ToListAsync();
        dlqs.Should().HaveCount(2);
        dlqs.Should().OnlyContain(d => d.FlowType == "OFFICIAL_EXAM" && d.ManagedByRole == "examination_officer");
    }

    // =========================================================================
    // M1 & M3 POLISH REGRESSION TESTS
    // =========================================================================

    [Fact(DisplayName = "M4-M1-01: GeminiAiGradingService.RedactStudentIdentifiers ẩn email, MSSV, SĐT và tên bằng GeneratedRegex")]
    public void M1Polish_GeminiAiGradingService_RedactStudentIdentifiers_RedactsPiiCleanly()
    {
        var method = typeof(GeminiAiGradingService).GetMethod(
            "RedactStudentIdentifiers",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var input = "Em tên là Nguyễn Văn An, mã số SE180123, email an.nv@fpt.edu.vn, SĐT 0912345678 trả lời về SOLID.";
        var redacted = (string)method!.Invoke(null, [input])!;

        redacted.Should().Contain("[NAME]");
        redacted.Should().Contain("[MSSV]");
        redacted.Should().Contain("[EMAIL]");
        redacted.Should().Contain("[PHONE]");
        redacted.Should().NotContain("SE180123");
        redacted.Should().NotContain("an.nv@fpt.edu.vn");
        redacted.Should().NotContain("0912345678");
    }

    [Fact(DisplayName = "M4-M1-02: ResetClassPasswordCommandHandler dùng AcademicCourseOwnership.IsOwnedBy khi AcademicProgram.DepartmentHeadUserId == Guid.Empty")]
    public async Task M1Polish_ResetClassPassword_UsesAcademicCourseOwnershipFallbackAndRejectsEmptyActor()
    {
        await using var db = CreateDbContext();
        var legacyDeptHead = new User { Id = Guid.NewGuid(), Email = "tbm@fpt.edu.vn", FullName = "TBM", Role = UserRole.DepartmentHead, IsActive = true };
        var program = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = Guid.Empty };
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", AcademicProgramId = program.Id, AcademicProgram = program, DepartmentHeadUserId = legacyDeptHead.Id, IsActive = true };
        var cls = new Class { Id = Guid.NewGuid(), Code = "SE1830", Name = "SE1830", CourseId = course.Id, Course = course, JoinCode = "JOIN30", IsActive = true };

        db.Users.Add(legacyDeptHead);
        db.AcademicPrograms.Add(program);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        await db.SaveChangesAsync();

        var handler = new ResetClassPasswordCommandHandler(db);

        // Legacy DepartmentHead fallback works when AcademicProgram.DepartmentHeadUserId is Guid.Empty
        var okRes = await handler.Handle(new ResetClassPasswordCommand(cls.Id, legacyDeptHead.Id, "NewPass@2026"), CancellationToken.None);
        okRes.IsSuccess.Should().BeTrue(okRes.Error);

        // Guid.Empty actor is rejected (never matches empty DepartmentHeadUserId)
        var emptyActorRes = await handler.Handle(new ResetClassPasswordCommand(cls.Id, Guid.Empty, "NewPass@2026"), CancellationToken.None);
        emptyActorRes.IsSuccess.Should().BeFalse();
    }

    [Fact(DisplayName = "M4-M3-01: PadesPdfDigitalSigner từ chối NotBefore tương lai và bọc IOException/UnauthorizedAccessException thành PdfSigningCertificateException (503)")]
    public void M3Polish_PadesPdfDigitalSigner_RejectsFutureNotBefore_AndWrapsIoExceptionAsPdfSigningCertificateException()
    {
        // 1. Future NotBefore -> PdfSigningCertificateException
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Future Cert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var futureCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddYears(1));
        var futureBase64 = Convert.ToBase64String(futureCert.Export(X509ContentType.Pfx, "pass"));

        var futureConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["PdfSigning:RequireCertificate"] = "true",
                ["PdfSigning:CertificateBase64"] = futureBase64,
                ["PdfSigning:CertificatePassword"] = "pass"
            })
            .Build();

        using var futureSigner = new PadesPdfDigitalSigner(futureConfig);
        Action actFuture = () => _ = futureSigner.GetActiveCertificateThumbprint();
        actFuture.Should().Throw<PdfSigningCertificateException>().WithMessage("*chưa đến thời hạn hiệu lực*");

        // 2. Existing but exclusively locked/unreadable .pfx -> IOException wrapped in PdfSigningCertificateException (503, not 403)
        var tempDir = Path.Combine(AppContext.BaseDirectory, $"pades_lock_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempPfx = Path.Combine(tempDir, "locked.pfx");
        try
        {
            File.WriteAllBytes(tempPfx, futureCert.Export(X509ContentType.Pfx, "pass"));
            using var exclusiveLock = new FileStream(tempPfx, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var lockedPathConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["PdfSigning:RequireCertificate"] = "true",
                    ["PdfSigning:CertificatePath"] = tempPfx,
                    ["PdfSigning:CertificatePassword"] = "pass"
                })
                .Build();

            using var lockedSigner = new PadesPdfDigitalSigner(lockedPathConfig);
            Action actLocked = () => _ = lockedSigner.GetActiveCertificateThumbprint();
            actLocked.Should().Throw<PdfSigningCertificateException>().WithMessage("*FAIL CLOSED*");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort temp cleanup
            }
        }
    }

    [Fact(DisplayName = "M4-M3-02: GetAppealAllocationProposal đặt ExceedsStandardQuota = true khi một môn hết quota 24 dù tổng toàn hệ thống còn dư")]
    public async Task M3Polish_GetAppealAllocationProposal_SetsExceedsStandardQuotaTrue_OnPerCourseSaturation()
    {
        await using var db = CreateDbContext();
        var officer = new User { Id = Guid.NewGuid(), Email = "eo_m4@fpt.edu.vn", FullName = "EO", Role = UserRole.ExaminationOfficer, IsActive = true };
        var studentA = new User { Id = Guid.NewGuid(), Email = "sva@fpt.edu.vn", StudentCode = "SE900001", FullName = "SV A", Role = UserRole.Student, IsActive = true };
        var lecturerA = new User { Id = Guid.NewGuid(), Email = "gva@fpt.edu.vn", FullName = "GV Môn A", Role = UserRole.Lecturer, IsActive = true };
        var lecturerB = new User { Id = Guid.NewGuid(), Email = "gvb@fpt.edu.vn", FullName = "GV Môn B", Role = UserRole.Lecturer, IsActive = true };

        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", IsActive = true };

        var sessionA = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseA.Id, Course = courseA, Title = "FE A", ExamDate = new DateOnly(2026, 11, 20), Status = "published" };
        var sessionB = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseB.Id, Course = courseB, Title = "FE B", ExamDate = new DateOnly(2026, 11, 20), Status = "published" };
        var shiftA = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionA.Id, Session = sessionA, ShiftName = "Ca A", RoomLab = "Lab 1", ExaminerUserId = Guid.NewGuid(), Status = "completed" };
        var shiftB = new RealExamSessionShift { Id = Guid.NewGuid(), SessionId = sessionB.Id, Session = sessionB, ShiftName = "Ca B", RoomLab = "Lab 2", ExaminerUserId = Guid.NewGuid(), Status = "completed" };

        var ticketA = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftA.Id, Shift = shiftA, StudentId = studentA.Id, SeatNumber = 1, Status = ExamTicketStatus.Published, GradesPublishedAt = DateTime.UtcNow, IsLocked = true };
        var ticketB = new StudentExamTicket { Id = Guid.NewGuid(), ShiftId = shiftB.Id, Shift = shiftB, StudentId = studentA.Id, SeatNumber = 2, Status = ExamTicketStatus.Published, GradesPublishedAt = DateTime.UtcNow, IsLocked = true };

        db.Users.AddRange(officer, studentA, lecturerA, lecturerB);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = courseA.Id, LecturerUserId = lecturerA.Id, AssignedByUserId = officer.Id, IsActive = true },
            new CourseLecturer { CourseId = courseB.Id, LecturerUserId = lecturerB.Id, AssignedByUserId = officer.Id, IsActive = true });
        db.OfficialExamSessions.AddRange(sessionA, sessionB);
        db.RealExamSessionShifts.AddRange(shiftA, shiftB);
        db.StudentExamTickets.AddRange(ticketA, ticketB);

        // Lecturer A already has 24 active appeals (quota = 0), while Lecturer B has 0 active appeals (quota = 24)
        for (var i = 0; i < 24; i++)
        {
            db.AppealRequests.Add(new AppealRequest
            {
                Id = Guid.NewGuid(),
                TicketId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                SessionId = sessionA.Id,
                Reason = $"Active {i}",
                Status = AppealStatus.AssignedToLecturer,
                AssignedTo = lecturerA.Id
            });
        }

        var appealA = new AppealRequest { Id = Guid.NewGuid(), TicketId = ticketA.Id, Ticket = ticketA, StudentId = studentA.Id, Student = studentA, SessionId = sessionA.Id, Session = sessionA, Reason = "Phúc khảo A", Status = AppealStatus.SubmittedToExamOffice };
        var appealB = new AppealRequest { Id = Guid.NewGuid(), TicketId = ticketB.Id, Ticket = ticketB, StudentId = studentA.Id, Student = studentA, SessionId = sessionB.Id, Session = sessionB, Reason = "Phúc khảo B", Status = AppealStatus.SubmittedToExamOffice };
        db.AppealRequests.AddRange(appealA, appealB);
        await db.SaveChangesAsync();

        var res = await new GetAppealAllocationProposalQueryHandler(db)
            .Handle(new GetAppealAllocationProposalQuery(SessionId: null, officer.Id, officer.Role), CancellationToken.None);

        res.IsSuccess.Should().BeTrue(res.Error);
        res.Value.TotalAppeals.Should().Be(2);
        res.Value.Items.Single(i => i.AppealId == appealA.Id).ProposedLecturerId.Should().BeNull();
        res.Value.Items.Single(i => i.AppealId == appealB.Id).ProposedLecturerId.Should().Be(lecturerB.Id);
        res.Value.ExceedsStandardQuota.Should().BeTrue("môn A đã hết quota 24 nên đơn của môn A không thể phân bổ dù môn B còn dư quota");
    }
}

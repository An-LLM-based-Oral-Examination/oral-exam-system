using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.CloseDueInClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.CloseInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.ExpirePersonalInClassExams;
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Commands.FinishStudentInClassExam;
using OralExamination.Application.Features.InClassShifts.Commands.ResumeInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
using OralExamination.Application.Features.InClassShifts.Queries.GetMyInClassShifts;
using OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public class InClassShiftTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static async Task<(User Lecturer, User Student, User OtherStudent, Course Course, Class Class)>
        SeedBaseDataAsync(OralExamDbContext context)
    {
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fe.edu.vn",
            FullName = "Giảng viên SWP391",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fe.edu.vn",
            FullName = "Sinh viên Nguyễn Văn A",
            StudentCode = "SE170001",
            Role = UserRole.Student,
            IsActive = true
        };

        var otherStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "otherstudent@fe.edu.vn",
            FullName = "Sinh viên Ngoài Lớp",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Software Development Project",
            IsActive = true
        };

        var classEntity = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1701",
            Name = "Lớp KTPM 1701",
            CourseId = course.Id,
            LecturerId = lecturer.Id,
            IsActive = true
        };

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            StudentId = student.Id,
            Status = "enrolled"
        };

        context.Users.AddRange(lecturer, student, otherStudent);
        context.Courses.Add(course);
        context.Classes.Add(classEntity);
        context.ClassEnrollments.Add(enrollment);
        await context.SaveChangesAsync();

        return (lecturer, student, otherStudent, course, classEntity);
    }

    [Fact]
    public async Task CreateInClassShift_Success_GeneratesValidSecretKeyAndScheduledStatus()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, _, _, _, classEntity) = await SeedBaseDataAsync(context);
        var validator = new CreateInClassShiftCommandValidator();
        var handler = new CreateInClassShiftCommandHandler(context, validator);

        var startTime = DateTime.UtcNow.AddMinutes(5);
        var endTime = startTime.AddMinutes(45);
        var command = new CreateInClassShiftCommand(lecturer.Id, classEntity.Id, "Kiểm tra miệng Sprint 1", startTime, endTime);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ShiftName.Should().Be("Kiểm tra miệng Sprint 1");
        result.Value.Status.Should().Be("SCHEDULED");
        result.Value.IsLocked.Should().BeFalse();

        var createdShift = await context.InClassExamShifts.FirstOrDefaultAsync(s => s.Id == result.Value.Id);
        createdShift.Should().NotBeNull();
        createdShift!.TotpSecretKey.Should().NotBeNullOrWhiteSpace();
        createdShift.TotpSecretKey.Length.Should().Be(64); // 32 bytes hex
    }

    [Fact]
    public async Task CreateInClassShift_InvalidTimeRange_FailsValidation()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var validator = new CreateInClassShiftCommandValidator();
        var handler = new CreateInClassShiftCommandHandler(context, validator);

        var startTime = DateTime.UtcNow.AddMinutes(30);
        var endTime = DateTime.UtcNow.AddMinutes(10); // End < Start
        var command = new CreateInClassShiftCommand(lecturer.Id, classEntity.Id, "Ca thi lỗi thời gian", startTime, endTime);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.InvalidTimeRange);
    }

    [Fact]
    public async Task CreateInClassShift_InactiveCourse_ReturnsClassNotFound()
    {
        using var context = CreateDbContext();
        var (lecturer, _, _, course, classEntity) = await SeedBaseDataAsync(context);
        course.IsActive = false;
        await context.SaveChangesAsync();

        var handler = new CreateInClassShiftCommandHandler(context, new CreateInClassShiftCommandValidator());
        var startTime = DateTime.UtcNow.AddMinutes(5);
        var result = await handler.Handle(
            new CreateInClassShiftCommand(
                lecturer.Id,
                classEntity.Id,
                "Ca không được tạo trên môn đã khóa",
                startTime,
                startTime.AddMinutes(30)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.ClassNotFound);
        context.InClassExamShifts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentPassword_Success_ReturnsValidTotpAndSavesAuditRecord()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, _, _, _, classEntity) = await SeedBaseDataAsync(context);
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        mockPasswordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed_otp");

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi kiểm tra kiến thức OOP",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new GetInClassShiftCurrentPasswordQueryHandler(context, mockPasswordHasher.Object);
        var query = new GetInClassShiftCurrentPasswordQuery(lecturer.Id, shift.Id);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CurrentPassword.Should().HaveLength(6);
        result.Value.RemainingSeconds.Should().BeInRange(1, 300);

        // Verify record in InClassShiftPasswords
        var savedPassword = await context.InClassShiftPasswords.FirstOrDefaultAsync(p => p.ShiftId == shift.Id);
        savedPassword.Should().NotBeNull();
        savedPassword!.PlainCode.Should().Be(result.Value.CurrentPassword);
        savedPassword.PasswordHash.Should().Be("hashed_otp");
    }

    [Fact]
    public async Task EnterInClassShift_CorrectOtp_SuccessAndAdvancesStatusToInProgress()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Clean Architecture",
            Content = "Trình bày vai trò của Domain Layer trong Clean Architecture?",
            SampleAnswer = "Domain Layer chứa Entities, Value Objects, Enums và quy tắc nghiệp vụ cốt lõi độc lập.",
            KeyPoints = "[\"Entities\", \"Domain Rules\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Vấn đáp Kiến trúc Clean Architecture",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = secret,
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        string validCurrentOtp = TotpPasswordHelper.GenerateCode(secret);
        var validator = new EnterInClassShiftCommandValidator();
        var handler = new EnterInClassShiftCommandHandler(context, validator, new Mock<IGradingQueueChannel>().Object);

        var command = new EnterInClassShiftCommand(student.Id, shift.Id, validCurrentOtp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("JOINED");

        var participant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(p => p.ShiftId == shift.Id && p.StudentId == student.Id);
        participant.Should().NotBeNull();
        participant!.Status.Should().Be("JOINED");

        var updatedShift = await context.InClassExamShifts.FindAsync(shift.Id);
        updatedShift!.Status.Should().Be("IN_PROGRESS");
    }

    [Fact]
    public async Task EnterInClassShift_WrongOtp_ReturnsInvalidOtpError()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Clean Architecture",
            Content = "Trình bày vai trò của Domain Layer trong Clean Architecture?",
            SampleAnswer = "Domain Layer chứa Entities, Value Objects, Enums và quy tắc nghiệp vụ cốt lõi độc lập.",
            KeyPoints = "[\"Entities\", \"Domain Rules\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Vấn đáp bảo mật",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = secret,
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var validator = new EnterInClassShiftCommandValidator();
        var handler = new EnterInClassShiftCommandHandler(context, validator, new Mock<IGradingQueueChannel>().Object);

        var command = new EnterInClassShiftCommand(student.Id, shift.Id, "000000"); // wrong OTP

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.InvalidOtp);
    }

    [Fact]
    public async Task EnterInClassShift_StudentNotEnrolled_ReturnsStudentNotEnrolledError()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, _, otherStudent, _, classEntity) = await SeedBaseDataAsync(context);

        var secret = TotpPasswordHelper.GenerateSecretKey(32);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi lớp SE1701",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = secret,
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        string validOtp = TotpPasswordHelper.GenerateCode(secret);
        var validator = new EnterInClassShiftCommandValidator();
        var handler = new EnterInClassShiftCommandHandler(context, validator, new Mock<IGradingQueueChannel>().Object);

        var command = new EnterInClassShiftCommand(otherStudent.Id, shift.Id, validOtp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.StudentNotEnrolled);
    }

    [Fact]
    public async Task GetMyInClassShifts_HidesShiftWhenCourseIsInactive()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);
        context.InClassExamShifts.Add(new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca chỉ hiển thị khi môn còn hoạt động",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        });
        await context.SaveChangesAsync();

        var handler = new GetMyInClassShiftsQueryHandler(context);
        var activeResult = await handler.Handle(
            new GetMyInClassShiftsQuery(student.Id),
            CancellationToken.None);
        activeResult.Value.Should().ContainSingle();

        course.IsActive = false;
        await context.SaveChangesAsync();
        var inactiveResult = await handler.Handle(
            new GetMyInClassShiftsQuery(student.Id),
            CancellationToken.None);
        inactiveResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitInClassShiftAnswer_Success_EnqueuesGradingAndDoesNotStoreAudio()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var questionId = Guid.NewGuid();
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = questionId,
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Singleton Pattern",
            Content = "Trình bày bản chất của Singleton Pattern?",
            SampleAnswer = "Singleton đảm bảo một lớp chỉ có duy nhất một instance trong suốt vòng đời ứng dụng.",
            KeyPoints = "[\"Single Instance\", \"Global Access\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi Design Patterns",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            Status = "JOINED"
        };
        context.InClassShiftParticipants.Add(participant);
        await context.SaveChangesAsync();

        var mockQueueChannel = new Mock<IGradingQueueChannel>();
        GradingTask? enqueuedTask = null;
        mockQueueChannel.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Callback<GradingTask, CancellationToken>((t, _) => enqueuedTask = t)
            .Returns(ValueTask.CompletedTask);

        var validator = new SubmitInClassShiftAnswerCommandValidator();
        var handler = new SubmitInClassShiftAnswerCommandHandler(context, mockQueueChannel.Object, validator);

        var command = new SubmitInClassShiftAnswerCommand(
            student.Id,
            shift.Id,
            questionId,
            "Mô hình Singleton đảm bảo một lớp chỉ có duy nhất một instance trong toàn bộ ứng dụng."
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("SUBMITTED");

        // Verify entity persisted in DB
        var submission = await context.InClassExamSubmissions.FirstOrDefaultAsync(s => s.Id == result.Value.SubmissionId);
        submission.Should().NotBeNull();
        submission!.Transcript.Should().Contain("Singleton");
        submission.Status.Should().Be("SUBMITTED");

        // Verify participant updated to SUBMITTED
        var updatedParticipant = await context.InClassShiftParticipants.FindAsync(participant.Id);
        updatedParticipant!.Status.Should().Be("SUBMITTED");

        // Verify task pushed to channel with InClassSubmissionId
        enqueuedTask.Should().NotBeNull();
        enqueuedTask!.InClassSubmissionId.Should().Be(submission.Id);
        enqueuedTask.TaskType.Should().Be("InClass");
        enqueuedTask.AnswerText.Should().Contain("Singleton");
    }

    [Fact]
    public async Task AdjustInClassSubmissionScore_Success_UpdatesScoreAndSetsStatusAudited()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var questionId = Guid.NewGuid();
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = questionId,
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Singleton Pattern",
            Content = "Trình bày bản chất của Singleton Pattern?",
            SampleAnswer = "Singleton đảm bảo một lớp chỉ có duy nhất một instance trong suốt vòng đời ứng dụng.",
            KeyPoints = "[\"Single Instance\", \"Global Access\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi chấm hậu kiểm",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Em xin trả lời về Factory Method...",
            AiScore = 7.50m,
            AiFeedback = "Câu trả lời đúng ý nhưng chưa nêu nhược điểm",
            FinalScore = 7.50m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var validator = new AdjustInClassSubmissionScoreCommandValidator();
        var handler = new AdjustInClassSubmissionScoreCommandHandler(context, validator);

        var command = new AdjustInClassSubmissionScoreCommand(
            lecturer.Id,
            submission.Id,
            8.50m,
            "Cộng 1 điểm vì sinh viên giải thích rất tốt về Open/Closed Principle"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FinalScore.Should().Be(8.50m);
        result.Value.Status.Should().Be("AUDITED");
        result.Value.LecturerNotes.Should().Contain("Cộng 1 điểm");

        var updatedSubmission = await context.InClassExamSubmissions.FindAsync(submission.Id);
        updatedSubmission!.FinalScore.Should().Be(8.50m);
        updatedSubmission.Status.Should().Be("AUDITED");
    }

    [Fact]
    public async Task FinalizeInClassShiftGrades_Success_LocksShiftAndActivatesOneWayLock()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi chốt điểm quá trình",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = Guid.NewGuid(),
            Status = "SUBMITTED"
        };
        context.InClassShiftParticipants.Add(participant);
        context.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = participant.StudentId,
            QuestionId = participant.AssignedQuestionId.Value,
            Transcript = "Bài đã được AI chấm xong.",
            AiScore = 8.25m,
            FinalScore = 8.25m,
            Status = "AI_GRADED"
        });
        await context.SaveChangesAsync();

        var handler = new FinalizeInClassShiftGradesCommandHandler(context);
        var command = new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("FINALIZED");
        result.Value.IsLocked.Should().BeTrue();
        result.Value.FinalizedAt.Should().NotBeNull();

        var lockedShift = await context.InClassExamShifts.FindAsync(shift.Id);
        lockedShift!.Status.Should().Be("FINALIZED");
        lockedShift.IsLocked.Should().BeTrue();
        (await context.InClassShiftParticipants.FindAsync(participant.Id))!.Status.Should().Be("FINALIZED");
    }

    [Fact]
    public async Task FinalizeInClassShiftGrades_WhenShiftOpenOrSubmissionPending_DoesNotLock()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca chưa đủ điều kiện chốt",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = Guid.NewGuid(),
            Status = "SUBMITTED"
        });
        context.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài đang chờ chấm.",
            Status = "SUBMITTED"
        });
        await context.SaveChangesAsync();

        var handler = new FinalizeInClassShiftGradesCommandHandler(context);
        var openResult = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id),
            CancellationToken.None);

        openResult.IsSuccess.Should().BeFalse();
        (await context.InClassExamShifts.FindAsync(shift.Id))!.IsLocked.Should().BeFalse();

        shift.Status = "CLOSED";
        await context.SaveChangesAsync();
        var pendingResult = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id),
            CancellationToken.None);

        pendingResult.IsSuccess.Should().BeFalse();
        (await context.InClassExamShifts.FindAsync(shift.Id))!.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Queries_GetById_Submissions_ByClass_WorkAsExpected()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var questionId = Guid.NewGuid();
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = questionId,
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi Singleton Pattern",
            Content = "Trình bày bản chất của Singleton Pattern?",
            SampleAnswer = "Singleton đảm bảo một lớp chỉ có duy nhất một instance trong suốt vòng đời ứng dụng.",
            KeyPoints = "[\"Single Instance\", \"Global Access\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi truy vấn tổng hợp",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            Status = "JOINED"
        };
        context.InClassShiftParticipants.Add(participant);

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài nộp truy vấn",
            Status = "SUBMITTED"
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        // 1. Test GetInClassShiftByIdQuery
        var getByIdHandler = new GetInClassShiftByIdQueryHandler(context);
        var detailResult = await getByIdHandler.Handle(new GetInClassShiftByIdQuery(lecturer.Id, shift.Id), CancellationToken.None);
        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value.Participants.Should().HaveCount(1);
        detailResult.Value.Submissions.Should().HaveCount(1);

        // 2. Test GetInClassShiftSubmissionsQuery
        var getSubmissionsHandler = new GetInClassShiftSubmissionsQueryHandler(context);
        var subsResult = await getSubmissionsHandler.Handle(new GetInClassShiftSubmissionsQuery(lecturer.Id, shift.Id), CancellationToken.None);
        subsResult.IsSuccess.Should().BeTrue();
        subsResult.Value.Should().HaveCount(1);

        // 3. Test GetInClassShiftsByClassQuery
        var getByClassHandler = new GetInClassShiftsByClassQueryHandler(context);
        var byClassResult = await getByClassHandler.Handle(new GetInClassShiftsByClassQuery(lecturer.Id, classEntity.Id), CancellationToken.None);
        byClassResult.IsSuccess.Should().BeTrue();
        byClassResult.Value.Should().HaveCount(1);

        // 4. Test GetStudentInClassSubmissionQuery
        var getStudentSubHandler = new GetStudentInClassSubmissionQueryHandler(context);
        var studentSubResult = await getStudentSubHandler.Handle(new GetStudentInClassSubmissionQuery(student.Id, shift.Id), CancellationToken.None);
        studentSubResult.IsSuccess.Should().BeTrue();
        studentSubResult.Value.Transcript.Should().Be("Bài nộp truy vấn");
    }

    [Fact]
    public async Task EnterInClassShift_AutoAssignsApprovedQuestion_AndEnforcesQuestionMatchAndNoDuplicateSubmit()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, otherStudent, course, classEntity) = await SeedBaseDataAsync(context);

        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            StudentId = otherStudent.Id,
            Status = "enrolled"
        });

        var q1 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi 1: Phân biệt Stack và Heap",
            Content = "Trình bày sự khác biệt giữa vùng nhớ Stack và vùng nhớ Heap?",
            SampleAnswer = "Stack lưu biến cục bộ và khung hàm tự giải phóng, còn Heap lưu dữ liệu cấp phát động.",
            KeyPoints = "[\"Stack\", \"Heap\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var q2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi 2: Nguyên lý SOLID",
            Content = "Giải thích nguyên lý Dependency Inversion trong SOLID?",
            SampleAnswer = "Module cấp cao không phụ thuộc vào module cấp thấp mà cả hai phụ thuộc vào abstraction.",
            KeyPoints = "[\"DIP\", \"Abstraction\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Apply,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var otherCourseQuestion = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi môn khác",
            Content = "Nội dung câu hỏi môn khác",
            SampleAnswer = "Câu trả lời mẫu cho câu hỏi thuộc môn học khác dùng để kiểm thử chặn gian lận.",
            KeyPoints = "[\"Other\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        context.PracticeQuestions.AddRange(q1, q2, otherCourseQuestion);

        var secretKey = TotpPasswordHelper.GenerateSecretKey(32);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi tự động bốc câu hỏi",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = secretKey,
            Status = "IN_PROGRESS",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        string otp = TotpPasswordHelper.GenerateCode(secretKey);
        var enterHandler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), new Mock<IGradingQueueChannel>().Object);

        // Act 1: Student 1 enters shift -> auto-assigned a question
        var enterRes1 = await enterHandler.Handle(new EnterInClassShiftCommand(student.Id, shift.Id, otp), CancellationToken.None);
        enterRes1.IsSuccess.Should().BeTrue();
        enterRes1.Value.AssignedQuestionId.Should().NotBeNull();
        enterRes1.Value.AssignedQuestion.Should().NotBeNull();

        // Re-entering returns the exact same assigned question (idempotent)
        var reEnterRes1 = await enterHandler.Handle(new EnterInClassShiftCommand(student.Id, shift.Id, otp), CancellationToken.None);
        reEnterRes1.IsSuccess.Should().BeTrue();
        reEnterRes1.Value.AssignedQuestionId.Should().Be(enterRes1.Value.AssignedQuestionId);

        // Act 2: Student 2 enters shift -> anti-collision picks the other question
        var enterRes2 = await enterHandler.Handle(new EnterInClassShiftCommand(otherStudent.Id, shift.Id, otp), CancellationToken.None);
        enterRes2.IsSuccess.Should().BeTrue();
        enterRes2.Value.AssignedQuestionId.Should().NotBeNull();
        enterRes2.Value.AssignedQuestionId!.Value.Should().NotBe(enterRes1.Value.AssignedQuestionId!.Value);

        // Act 3: Student 1 tries to submit with wrong question ID -> blocked by QuestionMismatch
        var mockQueue = new Mock<IGradingQueueChannel>();
        mockQueue.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var submitHandler = new SubmitInClassShiftAnswerCommandHandler(context, mockQueue.Object, new SubmitInClassShiftAnswerCommandValidator());

        var mismatchRes = await submitHandler.Handle(
            new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, otherCourseQuestion.Id, "Câu trả lời gian lận đổi câu hỏi"),
            CancellationToken.None);
        mismatchRes.IsSuccess.Should().BeFalse();
        mismatchRes.Error.Should().Be(InClassShiftErrors.QuestionMismatch);

        // Act 4: Student 1 submits with assigned question -> succeeds
        var validSubRes = await submitHandler.Handle(
            new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, enterRes1.Value.AssignedQuestionId!.Value, "Em trả lời đúng câu hỏi được giao."),
            CancellationToken.None);
        validSubRes.IsSuccess.Should().BeTrue();

        // Act 5: Retry cùng payload phải idempotent để không kẹt bài khi client mất response.
        var retryRes = await submitHandler.Handle(
            new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, enterRes1.Value.AssignedQuestionId!.Value, "Em trả lời đúng câu hỏi được giao."),
            CancellationToken.None);
        retryRes.IsSuccess.Should().BeTrue();
        retryRes.Value.SubmissionId.Should().Be(validSubRes.Value.SubmissionId);

        // Nội dung khác vẫn bị chặn để không sửa bài đã nộp.
        var duplicateRes = await submitHandler.Handle(
            new SubmitInClassShiftAnswerCommand(student.Id, shift.Id, enterRes1.Value.AssignedQuestionId!.Value, "Em nộp lần 2."),
            CancellationToken.None);
        duplicateRes.IsSuccess.Should().BeFalse();
        duplicateRes.Error.Should().Be(InClassShiftErrors.DuplicateSubmission);
    }

    [Fact]
    public async Task GetInClassShiftById_ForStudent_IsolatesOwnRecordsAndMasksUnfinalizedScores()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, otherStudent, _, classEntity) = await SeedBaseDataAsync(context);

        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            StudentId = otherStudent.Id,
            Status = "enrolled"
        });

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi kiểm thử BOLA",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);

        context.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id, Status = "SUBMITTED" },
            new InClassShiftParticipant { Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = otherStudent.Id, Status = "SUBMITTED" }
        );

        context.InClassExamSubmissions.AddRange(
            new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = student.Id,
                QuestionId = Guid.NewGuid(),
                Transcript = "Bài làm của sinh viên A",
                AiScore = 8.50m,
                AiFeedback = "Nhận xét AI cho A",
                FinalScore = 8.50m,
                LecturerNotes = "Ghi chú nháp của GV",
                Status = "AI_GRADED"
            },
            new InClassExamSubmission
            {
                Id = Guid.NewGuid(),
                ShiftId = shift.Id,
                StudentId = otherStudent.Id,
                QuestionId = Guid.NewGuid(),
                Transcript = "Bài làm của sinh viên B",
                AiScore = 9.00m,
                AiFeedback = "Nhận xét AI cho B",
                FinalScore = 9.00m,
                Status = "AI_GRADED"
            }
        );
        await context.SaveChangesAsync();

        var getByIdHandler = new GetInClassShiftByIdQueryHandler(context);
        var getMySubHandler = new GetStudentInClassSubmissionQueryHandler(context);

        // Act 1: Student queries before shift is finalized -> sees only own record, and scores are masked (null)
        var studentDetailBefore = await getByIdHandler.Handle(new GetInClassShiftByIdQuery(student.Id, shift.Id), CancellationToken.None);
        studentDetailBefore.IsSuccess.Should().BeTrue();
        studentDetailBefore.Value.Participants.Should().ContainSingle(p => p.StudentId == student.Id);
        studentDetailBefore.Value.Submissions.Should().ContainSingle(s => s.StudentId == student.Id);
        studentDetailBefore.Value.Submissions[0].AiScore.Should().BeNull();
        studentDetailBefore.Value.Submissions[0].FinalScore.Should().BeNull();
        studentDetailBefore.Value.Submissions[0].LecturerNotes.Should().BeNull();

        var mySubBefore = await getMySubHandler.Handle(new GetStudentInClassSubmissionQuery(student.Id, shift.Id), CancellationToken.None);
        mySubBefore.IsSuccess.Should().BeTrue();
        mySubBefore.Value.FinalScore.Should().BeNull();

        // Act 2: Finalize shift -> Student can now see finalized score
        shift.Status = "CLOSED";
        await context.SaveChangesAsync();
        var finalizeHandler = new FinalizeInClassShiftGradesCommandHandler(context);
        (await finalizeHandler.Handle(new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var mySubAfter = await getMySubHandler.Handle(new GetStudentInClassSubmissionQuery(student.Id, shift.Id), CancellationToken.None);
        mySubAfter.IsSuccess.Should().BeTrue();
        mySubAfter.Value.FinalScore.Should().Be(8.50m);
        mySubAfter.Value.AiScore.Should().Be(8.50m);
    }

    [Fact]
    public async Task InClassShift_SelectedPool_MultiQuestion_FollowUp_And_AutoPublishGrades_WorksEndToEnd()
    {
        // Arrange
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var q1 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu 1: Kiến trúc Microservices",
            Content = "So sánh Microservices và Monolith?",
            SampleAnswer = "Microservices chia nhỏ theo bounded context, triển khai độc lập nhưng phức tạp vận hành.",
            KeyPoints = "[\"Bounded Context\", \"Independent Deployment\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Analyze,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var q2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu 2: Event-Driven Architecture",
            Content = "Khi nào nên dùng Outbox Pattern?",
            SampleAnswer = "Dùng Outbox Pattern để đảm bảo tính nhất quán nguyên tử giữa ghi DB và phát sự kiện.",
            KeyPoints = "[\"Outbox\", \"Atomicity\"]",
            Difficulty = QuestionDifficulty.Hard,
            BloomLevel = BloomLevel.Evaluate,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        var excludedQ3 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu 3: Không nằm trong SELECTED_POOL",
            Content = "Câu hỏi này không nằm trong danh sách chọn của giảng viên",
            SampleAnswer = "Câu trả lời mẫu cho câu hỏi ngoài tập SELECTED_POOL của ca thi trên lớp.",
            KeyPoints = "[\"Excluded\"]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Remember,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };

        context.PracticeQuestions.AddRange(q1, q2, excludedQ3);
        await context.SaveChangesAsync();

        // 1. Giảng viên tạo ca thi với SELECTED_POOL (2 câu), HasFollowUp = true (tối đa 2 câu phụ), IsAutoPublishGrades = true
        var createHandler = new CreateInClassShiftCommandHandler(context, new CreateInClassShiftCommandValidator());
        var createRes = await createHandler.Handle(new CreateInClassShiftCommand(
            lecturer.Id,
            classEntity.Id,
            "Kiểm tra vấn đáp Giữa kỳ (2 câu + Follow-up)",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(45),
            Description: "Kiểm tra kiến trúc phân tán",
            QuestionPoolMode: "SELECTED_POOL",
            SelectedQuestionIds: new List<Guid> { q1.Id, q2.Id },
            TotalQuestionsPerStudent: 2,
            HasFollowUp: true,
            MaxFollowUpQuestions: 2,
            IsAutoPublishGrades: true
        ), CancellationToken.None);

        createRes.IsSuccess.Should().BeTrue();
        createRes.Value.QuestionPoolMode.Should().Be("SELECTED_POOL");
        createRes.Value.TotalQuestionsPerStudent.Should().Be(2);
        createRes.Value.HasFollowUp.Should().BeTrue();
        createRes.Value.MaxFollowUpQuestions.Should().Be(2);
        createRes.Value.IsAutoPublishGrades.Should().BeTrue();

        var shiftEntity = await context.InClassExamShifts.FindAsync(createRes.Value.Id);
        shiftEntity!.Status = "IN_PROGRESS";
        await context.SaveChangesAsync();
        string otp = TotpPasswordHelper.GenerateCode(shiftEntity.TotpSecretKey);

        // 2. Sinh viên vào ca thi -> nhận đúng 2 câu hỏi trong SELECTED_POOL (không dính excludedQ3)
        var enterHandler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), new Mock<IGradingQueueChannel>().Object);
        var enterRes = await enterHandler.Handle(new EnterInClassShiftCommand(student.Id, shiftEntity.Id, otp), CancellationToken.None);
        enterRes.IsSuccess.Should().BeTrue();
        enterRes.Value.AssignedQuestionIds.Should().NotBeNull();
        enterRes.Value.AssignedQuestionIds.Should().BeEquivalentTo(new[] { q1.Id, q2.Id });
        enterRes.Value.AssignedQuestions.Should().HaveCount(2);

        // 3. Sinh viên nộp câu trả lời chính cho Câu 1 và Câu 2
        var mockQueue = new Mock<IGradingQueueChannel>();
        mockQueue.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var submitHandler = new SubmitInClassShiftAnswerCommandHandler(context, mockQueue.Object, new SubmitInClassShiftAnswerCommandValidator());

        var subQ1 = await submitHandler.Handle(new SubmitInClassShiftAnswerCommand(
            student.Id, shiftEntity.Id, q1.Id, "Microservices chia nhỏ theo Bounded Context."
        ), CancellationToken.None);
        subQ1.IsSuccess.Should().BeTrue();

        var subQ2 = await submitHandler.Handle(new SubmitInClassShiftAnswerCommand(
            student.Id, shiftEntity.Id, q2.Id, "Outbox Pattern lưu sự kiện vào cùng transaction DB."
        ), CancellationToken.None);
        subQ2.IsSuccess.Should().BeTrue();

        // 4. Giả lập AI chấm xong Câu 1 và sinh câu hỏi phụ FollowUpPrompt
        var subQ1Entity = await context.InClassExamSubmissions.FindAsync(subQ1.Value.SubmissionId);
        subQ1Entity!.AiScore = 8.0m;
        subQ1Entity.FinalScore = 8.0m;
        subQ1Entity.AiFeedback = "Trả lời tốt, cần làm rõ giao tiếp bất đồng bộ.";
        subQ1Entity.FollowUpPrompt = "Làm thế nào xử lý lỗi phân tán khi gọi giữa 2 Microservices?";
        subQ1Entity.Status = "AI_GRADED";
        await context.SaveChangesAsync();

        // 5. Vì IsAutoPublishGrades = true, sinh viên xem được ngay điểm AI và câu hỏi phụ theo thời gian thực (Parallel Real-time Publishing)
        var getByIdHandler = new GetInClassShiftByIdQueryHandler(context);
        var studentView = await getByIdHandler.Handle(new GetInClassShiftByIdQuery(student.Id, shiftEntity.Id), CancellationToken.None);
        studentView.IsSuccess.Should().BeTrue();
        var q1Dto = studentView.Value.Submissions.First(s => s.Id == subQ1.Value.SubmissionId);
        q1Dto.AiScore.Should().Be(8.0m);
        q1Dto.FinalScore.Should().Be(8.0m);
        q1Dto.FollowUpPrompt.Should().Contain("2 Microservices");

        // 6. Sinh viên nộp câu trả lời Follow-up thứ 1 cho Câu 1
        var subFollowUp1 = await submitHandler.Handle(new SubmitInClassShiftAnswerCommand(
            student.Id,
            shiftEntity.Id,
            q1.Id,
            "Em dùng Saga Pattern và Circuit Breaker với Polly.",
            IsFollowUp: true,
            ParentSubmissionId: subQ1.Value.SubmissionId,
            FollowUpIndex: 1
        ), CancellationToken.None);
        subFollowUp1.IsSuccess.Should().BeTrue();
        subFollowUp1.Value.IsFollowUp.Should().BeTrue();
        subFollowUp1.Value.FollowUpIndex.Should().Be(1);
    }

    [Fact]
    public async Task SubmitFollowUp_RejectsForeignParentAndNonContiguousIndex()
    {
        using var context = CreateDbContext();
        var (lecturer, student, otherStudent, course, classEntity) = await SeedBaseDataAsync(context);
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            StudentId = otherStudent.Id,
            Status = "enrolled"
        });
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hỏi bảo vệ chuỗi follow-up",
            Content = "Giải thích nguyên tắc least privilege.",
            SampleAnswer = "Chỉ cấp đúng quyền tối thiểu cần thiết cho từng danh tính và từng thao tác.",
            KeyPoints = "[\"least privilege\"]",
            Difficulty = QuestionDifficulty.Medium,
            BloomLevel = BloomLevel.Analyze,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca kiểm tra follow-up",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            HasFollowUp = true,
            MaxFollowUpQuestions = 1
        };
        context.PracticeQuestions.Add(question);
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.AddRange(
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = student.Id,
                Status = "IN_PROGRESS", AssignedQuestionId = question.Id
            },
            new InClassShiftParticipant
            {
                Id = Guid.NewGuid(), ShiftId = shift.Id, StudentId = otherStudent.Id,
                Status = "IN_PROGRESS", AssignedQuestionId = question.Id
            });
        var foreignParent = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = otherStudent.Id,
            QuestionId = question.Id,
            Transcript = "Bài chính của sinh viên khác",
            FollowUpPrompt = "Hãy nêu một ví dụ.",
            Status = "AI_GRADED"
        };
        var ownParent = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Bài chính của sinh viên hiện tại",
            FollowUpPrompt = "Hãy nêu một ví dụ.",
            Status = "AI_GRADED"
        };
        context.InClassExamSubmissions.AddRange(foreignParent, ownParent);
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var handler = new SubmitInClassShiftAnswerCommandHandler(
            context, queue.Object, new SubmitInClassShiftAnswerCommandValidator());

        var foreignResult = await handler.Handle(new SubmitInClassShiftAnswerCommand(
            student.Id, shift.Id, question.Id, "Câu trả lời phụ đánh cắp parent",
            IsFollowUp: true, ParentSubmissionId: foreignParent.Id, FollowUpIndex: 1), CancellationToken.None);
        foreignResult.IsSuccess.Should().BeFalse();
        foreignResult.Error.Should().Be(InClassShiftErrors.InvalidFollowUpParent);

        var skippedIndexResult = await handler.Handle(new SubmitInClassShiftAnswerCommand(
            student.Id, shift.Id, question.Id, "Câu trả lời phụ nhảy chỉ số",
            IsFollowUp: true, ParentSubmissionId: ownParent.Id, FollowUpIndex: 2), CancellationToken.None);
        skippedIndexResult.IsSuccess.Should().BeFalse();
        skippedIndexResult.Error.Should().Be(InClassShiftErrors.InvalidFollowUpIndex);
    }

    [Fact]
    public async Task CloseInClassShift_Manual_ClosesOpenShift()
    {
        using var context = CreateDbContext();
        var (lecturer, _, _, _, classEntity) = await SeedBaseDataAsync(context);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đóng thủ công",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new CloseInClassShiftCommandHandler(context, new Mock<IGradingQueueChannel>().Object);
        var result = await handler.Handle(new CloseInClassShiftCommand(lecturer.Id, shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("CLOSED");
        (await context.InClassExamShifts.FindAsync(shift.Id))!.Status.Should().Be("CLOSED");
    }

    [Fact]
    public async Task CloseDueInClassShifts_ClosesShiftWhenEndTimePassed_AndKeepsFinalized()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var due = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca tới giờ",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-1),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        var finalized = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca đã chốt",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-1),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "FINALIZED",
            IsLocked = true
        };
        context.InClassExamShifts.AddRange(due, finalized);
        await context.SaveChangesAsync();

        var closed = await new CloseDueInClassShiftsCommandHandler(context, new Mock<IGradingQueueChannel>().Object)
            .Handle(new CloseDueInClassShiftsCommand(), CancellationToken.None);

        closed.Should().Be(1);
        (await context.InClassExamShifts.FindAsync(due.Id))!.Status.Should().Be("CLOSED");
        (await context.InClassExamShifts.FindAsync(finalized.Id))!.Status.Should().Be("FINALIZED");

        var enter = await new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator(), new Mock<IGradingQueueChannel>().Object)
            .Handle(new EnterInClassShiftCommand(student.Id, due.Id, "000000"), CancellationToken.None);
        enter.IsSuccess.Should().BeFalse();
        enter.Error.Should().Be(InClassShiftErrors.ShiftClosed);
    }

    [Fact]
    public async Task CloseInClassShift_SubmitsUnansweredQuestionsForAiGrading()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var questionId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca còn bài chưa nộp",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = questionId,
            AssignedQuestionIdsJson = $"[\"{questionId}\"]",
            Status = "JOINED"
        });
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        var handler = new CloseInClassShiftCommandHandler(context, queue.Object);
        var result = await handler.Handle(new CloseInClassShiftCommand(lecturer.Id, shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var submission = await context.InClassExamSubmissions.SingleAsync(item => item.ShiftId == shift.Id);
        submission.StudentId.Should().Be(student.Id);
        submission.QuestionId.Should().Be(questionId);
        submission.Transcript.Should().BeEmpty();
        submission.Status.Should().Be("SUBMITTED");
        (await context.InClassShiftParticipants.SingleAsync()).Status.Should().Be("SUBMITTED");
        queue.Verify(channel => channel.EnqueueAsync(
            It.Is<GradingTask>(task => task.InClassSubmissionId == submission.Id && task.TaskType == "InClass"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpirePersonalInClassExams_WhenExamDurationElapsed_SubmitsAndEnqueuesAi()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var questionId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca hết giờ cá nhân",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExamDurationMinutes = 10,
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = questionId,
            AssignedQuestionIdsJson = $"[\"{questionId}\"]",
            JoinedAt = DateTime.UtcNow.AddMinutes(-15),
            Status = "JOINED"
        });
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        var created = await new ExpirePersonalInClassExamsCommandHandler(context, queue.Object)
            .Handle(new ExpirePersonalInClassExamsCommand(), CancellationToken.None);

        created.Should().Be(1);
        var participant = await context.InClassShiftParticipants.SingleAsync();
        participant.Status.Should().Be("SUBMITTED");
        var submission = await context.InClassExamSubmissions.SingleAsync();
        submission.QuestionId.Should().Be(questionId);
        submission.Transcript.Should().BeEmpty();
        queue.Verify(channel => channel.EnqueueAsync(
            It.Is<GradingTask>(task => task.InClassSubmissionId == submission.Id && task.TaskType == "InClass"),
            It.IsAny<CancellationToken>()), Times.Once);
        (await context.InClassExamShifts.FindAsync(shift.Id))!.Status.Should().Be("IN_PROGRESS");
    }

    [Fact]
    public async Task ResumeInClassShift_WhenTimeRemaining_ReturnsQuestionsAndRemainingSeconds()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);
        var questionId = Guid.NewGuid();
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = questionId,
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu resume",
            Content = "Nội dung câu resume",
            SampleAnswer = "Mẫu",
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca resume",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(40),
            ExamDurationMinutes = 20,
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = questionId,
            AssignedQuestionIdsJson = $"[\"{questionId}\"]",
            JoinedAt = DateTime.UtcNow.AddMinutes(-3),
            Status = "JOINED"
        });
        await context.SaveChangesAsync();

        var result = await new ResumeInClassShiftCommandHandler(context, new Mock<IGradingQueueChannel>().Object)
            .Handle(new ResumeInClassShiftCommand(student.Id, shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TimedOut.Should().BeFalse();
        result.Value.Status.Should().Be("JOINED");
        result.Value.RemainingSeconds.Should().BeGreaterThan(0);
        result.Value.AssignedQuestions.Should().ContainSingle(q => q.QuestionId == questionId);
        (await context.InClassShiftParticipants.SingleAsync()).Status.Should().Be("JOINED");
    }

    [Fact]
    public async Task ResumeInClassShift_WhenPersonalTimeExpired_FinalizesAndReportsTimedOut()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);
        var questionId = Guid.NewGuid();
        context.PracticeQuestions.Add(new PracticeQuestion
        {
            Id = questionId,
            CourseId = course.Id,
            RubricId = Guid.NewGuid(),
            Title = "Câu hết giờ",
            Content = "Nội dung",
            SampleAnswer = "Mẫu",
            KeyPoints = "[]",
            Difficulty = QuestionDifficulty.Easy,
            BloomLevel = BloomLevel.Understand,
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        });
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca resume hết giờ",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExamDurationMinutes = 5,
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = questionId,
            AssignedQuestionIdsJson = $"[\"{questionId}\"]",
            JoinedAt = DateTime.UtcNow.AddMinutes(-20),
            Status = "JOINED"
        });
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        var result = await new ResumeInClassShiftCommandHandler(context, queue.Object)
            .Handle(new ResumeInClassShiftCommand(student.Id, shift.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TimedOut.Should().BeTrue();
        result.Value.Status.Should().Be("SUBMITTED");
        result.Value.RemainingSeconds.Should().Be(0);
        queue.Verify(channel => channel.EnqueueAsync(
            It.IsAny<GradingTask>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "InClassShiftAutoSubmit: SubmitMissingAsync và SubmitExpiredPersonalExamsAsync lọc sạch terminal participants")]
    public async Task AutoSubmit_Should_FilterTerminalStatuses_Cleanly()
    {
        using var context = CreateDbContext();
        var gradingQueueMock = new Mock<IGradingQueueChannel>();
        var shiftId = Guid.NewGuid();

        var shift = new InClassExamShift
        {
            Id = shiftId,
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca test auto submit",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExamDurationMinutes = 5,
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);

        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            Status = "SUBMITTED"
        });
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            StudentId = Guid.NewGuid(),
            Status = "JOINED"
        });
        await context.SaveChangesAsync();

        var missingAction = () => InClassShiftAutoSubmit.SubmitMissingAsync(
            context, gradingQueueMock.Object, CancellationToken.None, shiftId);
        await missingAction.Should().NotThrowAsync();

        var expiredAction = () => InClassShiftAutoSubmit.SubmitExpiredPersonalExamsAsync(
            context, gradingQueueMock.Object, CancellationToken.None, shiftId);
        await expiredAction.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "MF-05: Finish phải tạo submission còn thiếu trước khi đánh dấu đã nộp")]
    public async Task FinishStudentExam_CreatesMissingMainSubmissionsBeforeCompleting()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var questionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca kiểm tra finish an toàn",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExamDurationMinutes = 20,
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = questionIds[0],
            AssignedQuestionIdsJson = $"[\"{questionIds[0]}\",\"{questionIds[1]}\"]",
            JoinedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(participant);
        context.InClassExamSubmissions.Add(new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = questionIds[0],
            Transcript = "Câu trả lời đã lưu",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(channel => channel.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var handler = new FinishStudentInClassExamCommandHandler(context, queue.Object);

        var result = await handler.Handle(
            new FinishStudentInClassExamCommand(student.Id, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        participant.Status.Should().Be("SUBMITTED");
        var submissions = await context.InClassExamSubmissions
            .Where(item => item.ShiftId == shift.Id && item.StudentId == student.Id && !item.IsFollowUp)
            .ToListAsync();
        submissions.Should().HaveCount(2);
        submissions.Single(item => item.QuestionId == questionIds[1]).Transcript.Should().BeEmpty();
        queue.Verify(channel => channel.EnqueueAsync(
            It.Is<GradingTask>(task => task.QuestionId == questionIds[1]),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "MF-05: Auto-submit phải phục hồi participant SUBMITTED nhưng chưa có submission")]
    public async Task AutoSubmit_RepairsSubmittedParticipantWithoutSubmissions()
    {
        using var context = CreateDbContext();
        var questionId = Guid.NewGuid();
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = Guid.NewGuid(),
            LecturerId = Guid.NewGuid(),
            ShiftName = "Ca dữ liệu lỗi cần phục hồi",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-1),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED",
            IsLocked = false
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = Guid.NewGuid(),
            AssignedQuestionId = questionId,
            AssignedQuestionIdsJson = $"[\"{questionId}\"]",
            Status = "SUBMITTED"
        });
        await context.SaveChangesAsync();

        var queue = new Mock<IGradingQueueChannel>();
        queue.Setup(channel => channel.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var created = await InClassShiftAutoSubmit.SubmitMissingAsync(
            context, queue.Object, CancellationToken.None, shift.Id);

        created.Should().Be(1);
        (await context.InClassExamSubmissions.SingleAsync()).QuestionId.Should().Be(questionId);
        queue.Verify(channel => channel.EnqueueAsync(
            It.Is<GradingTask>(task => task.QuestionId == questionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "MF-05: submissionsCount phải dựa trên bài thực tế, không dựa trên cờ SUBMITTED")]
    public async Task GetShift_SubmissionsCountDoesNotCountParticipantWithoutSubmission()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, _, classEntity) = await SeedBaseDataAsync(context);
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca kiểm tra thống kê",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(10),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);
        context.InClassShiftParticipants.Add(new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            Status = "SUBMITTED"
        });
        await context.SaveChangesAsync();

        var result = await new GetInClassShiftsByClassQueryHandler(context)
            .Handle(new GetInClassShiftsByClassQuery(lecturer.Id, classEntity.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().SubmissionsCount.Should().Be(0);
    }

    [Fact(DisplayName = "F-001: FinalizeInClassShiftGrades cấm Admin và Trưởng BM khác môn chốt điểm")]
    public async Task FinalizeInClassShiftGrades_WhenCalledByAdminOrUnassignedDeptHead_ReturnsLecturerNotAuthorized()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Admin User",
            Role = UserRole.Admin,
            IsActive = true
        };
        var foreignHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "foreignhead@fpt.edu.vn",
            FullName = "Foreign Dept Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.Users.AddRange(admin, foreignHead);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz 1",
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(-10),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED"
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new FinalizeInClassShiftGradesCommandHandler(context);

        // Act & Assert 1: Admin không được phép finalize
        var adminResult = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(admin.Id, shift.Id),
            CancellationToken.None);
        adminResult.IsSuccess.Should().BeFalse();
        adminResult.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);

        // Act & Assert 2: Trưởng BM khác môn không được phép finalize
        var foreignHeadResult = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(foreignHead.Id, shift.Id),
            CancellationToken.None);
        foreignHeadResult.IsSuccess.Should().BeFalse();
        foreignHeadResult.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
    }

    [Fact(DisplayName = "F-001: FinalizeInClassShiftGrades cấm Trưởng BM chốt điểm ca thi trong lớp")]
    public async Task FinalizeInClassShiftGrades_WhenCalledByDeptHead_ReturnsLecturerNotAuthorized()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var courseHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "coursehead@fpt.edu.vn",
            FullName = "Course Dept Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.Users.Add(courseHead);
        course.DepartmentHeadUserId = courseHead.Id;

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz Finalize",
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(-10),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED"
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new FinalizeInClassShiftGradesCommandHandler(context);
        var result = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(courseHead.Id, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
    }

    [Fact(DisplayName = "F-001: FinalizeInClassShiftGrades cho phép Giảng viên phụ trách ca chốt điểm")]
    public async Task FinalizeInClassShiftGrades_WhenCalledByAssignedLecturer_Succeeds()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz Finalize Lecturer",
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(-10),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED"
        };
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new FinalizeInClassShiftGradesCommandHandler(context);
        var result = await handler.Handle(
            new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        shift.Status.Should().Be("FINALIZED");
        shift.IsLocked.Should().BeTrue();
    }

    [Fact(DisplayName = "F-005: AdjustInClassSubmissionScore tính điểm tổng thống nhất với Finalize khi có follow-up")]
    public async Task AdjustInClassSubmissionScore_WhenFollowUpPresent_TotalFinalScoreMatchesFinalizeScore()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Khái niệm DI trong .NET",
            Content = "Khái niệm DI trong .NET?",
            SampleAnswer = "Dependency Injection là design pattern...",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.PracticeQuestions.Add(question);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz Scoring Test",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED"
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = $"[\"{question.Id}\"]",
            Status = "SUBMITTED"
        };
        context.InClassShiftParticipants.Add(participant);

        // Câu chính: điểm 4.0
        var mainSub = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            Transcript = "Câu trả lời chính",
            AiScore = 4.0m,
            FinalScore = 4.0m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-20),
            GradedAt = DateTime.UtcNow.AddMinutes(-19)
        };
        // Câu phụ: điểm 8.0
        var fuSub = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            IsFollowUp = true,
            FollowUpIndex = 1,
            ParentSubmissionId = mainSub.Id,
            Transcript = "Câu trả lời phụ mở rộng",
            AiScore = 8.0m,
            FinalScore = 8.0m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-15),
            GradedAt = DateTime.UtcNow.AddMinutes(-14)
        };
        context.InClassExamSubmissions.AddRange(mainSub, fuSub);
        await context.SaveChangesAsync();

        // Giảng viên hậu kiểm nâng điểm câu phụ lên 9.0
        var adjustValidator = new AdjustInClassSubmissionScoreCommandValidator();
        var adjustHandler = new AdjustInClassSubmissionScoreCommandHandler(context, adjustValidator);
        var adjustResult = await adjustHandler.Handle(
            new AdjustInClassSubmissionScoreCommand(lecturer.Id, fuSub.Id, 9.0m, "Hậu kiểm câu phụ"),
            CancellationToken.None);

        adjustResult.IsSuccess.Should().BeTrue();

        // participant.TotalFinalScore sau khi Adjust phải là Max(4.0, 9.0) = 9.0 (thay vì trung bình cộng (4+9)/2 = 6.5)
        var updatedParticipant = await context.InClassShiftParticipants.FindAsync(participant.Id);
        updatedParticipant!.TotalFinalScore.Should().Be(9.0m);

        // Finalize ca thi cũng phải giữ nguyên 9.0
        var finalizeHandler = new FinalizeInClassShiftGradesCommandHandler(context);
        var finalizeResult = await finalizeHandler.Handle(
            new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id),
            CancellationToken.None);

        finalizeResult.IsSuccess.Should().BeTrue();
        var finalizedParticipant = await context.InClassShiftParticipants.FindAsync(participant.Id);
        finalizedParticipant!.TotalFinalScore.Should().Be(9.0m, "Điểm finalize phải khớp 100% với điểm sau khi hậu kiểm");
    }

    [Fact(DisplayName = "F-007: AdjustInClassSubmissionScore cấm Admin và Trưởng BM điều chỉnh điểm")]
    public async Task AdjustInClassSubmissionScore_WhenCalledByAdminOrDeptHead_ReturnsLecturerNotAuthorized()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin2@fpt.edu.vn",
            FullName = "Admin User 2",
            Role = UserRole.Admin,
            IsActive = true
        };
        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "depthead2@fpt.edu.vn",
            FullName = "Dept Head 2",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.Users.AddRange(admin, deptHead);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Câu hỏi F-007",
            Content = "Nội dung...",
            SampleAnswer = "Trả lời...",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.PracticeQuestions.Add(question);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz F007",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "CLOSED"
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = $"[\"{question.Id}\"]",
            Status = "SUBMITTED"
        };
        context.InClassShiftParticipants.Add(participant);

        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            IsFollowUp = false,
            FollowUpIndex = 0,
            Transcript = "Câu trả lời sinh viên",
            AiScore = 5.0m,
            FinalScore = 5.0m,
            Status = "AI_GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-20),
            GradedAt = DateTime.UtcNow.AddMinutes(-19)
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();

        var adjustValidator = new AdjustInClassSubmissionScoreCommandValidator();
        var adjustHandler = new AdjustInClassSubmissionScoreCommandHandler(context, adjustValidator);

        // Admin cố tình sửa điểm -> Bị chặn
        var adminResult = await adjustHandler.Handle(
            new AdjustInClassSubmissionScoreCommand(admin.Id, submission.Id, 8.0m, "Admin sửa điểm"),
            CancellationToken.None);
        adminResult.IsSuccess.Should().BeFalse();
        adminResult.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);

        // Trưởng BM cố tình sửa điểm -> Bị chặn
        var deptHeadResult = await adjustHandler.Handle(
            new AdjustInClassSubmissionScoreCommand(deptHead.Id, submission.Id, 8.0m, "DeptHead sửa điểm"),
            CancellationToken.None);
        deptHeadResult.IsSuccess.Should().BeFalse();
        deptHeadResult.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
    }

    [Fact(DisplayName = "F-004: SubmitInClassShiftAnswer nộp lại cùng nội dung transcript trả về kết quả thành công idempotent")]
    public async Task SubmitInClassShiftAnswer_WhenIdempotentResubmit_SucceedsWithoutConflict()
    {
        using var context = CreateDbContext();
        var (lecturer, student, _, course, classEntity) = await SeedBaseDataAsync(context);

        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Câu hỏi nộp bài",
            Content = "Nội dung câu hỏi...",
            SampleAnswer = "Câu trả lời mẫu...",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        context.PracticeQuestions.Add(question);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "PRN231 Quiz Submit Test",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS"
        };
        context.InClassExamShifts.Add(shift);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            AssignedQuestionIdsJson = $"[\"{question.Id}\"]",
            Status = "IN_PROGRESS"
        };
        context.InClassShiftParticipants.Add(participant);
        await context.SaveChangesAsync();

        var queueMock = new Mock<IGradingQueueChannel>();
        queueMock.Setup(q => q.EnqueueAsync(It.IsAny<GradingTask>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var validator = new SubmitInClassShiftAnswerCommandValidator();
        var handler = new SubmitInClassShiftAnswerCommandHandler(context, queueMock.Object, validator);

        var command = new SubmitInClassShiftAnswerCommand(
            student.Id,
            shift.Id,
            question.Id,
            "Em xin trình bày về Dependency Injection trong ASP.NET Core.",
            false,
            null,
            0);

        // Lần 1: nộp thành công
        var result1 = await handler.Handle(command, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Lần 2: nộp lại cùng nội dung transcript (idempotent replay)
        var result2 = await handler.Handle(command, CancellationToken.None);
        result2.IsSuccess.Should().BeTrue();
        result2.Value!.SubmissionId.Should().Be(result1.Value!.SubmissionId);
    }
}

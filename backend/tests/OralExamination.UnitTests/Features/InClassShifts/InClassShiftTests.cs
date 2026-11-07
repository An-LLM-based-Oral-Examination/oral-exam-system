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
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
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
        var (lecturer, _, _, _, classEntity) = await SeedBaseDataAsync(context);
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
        var handler = new EnterInClassShiftCommandHandler(context, validator);

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
        var handler = new EnterInClassShiftCommandHandler(context, validator);

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
        var handler = new EnterInClassShiftCommandHandler(context, validator);

        var command = new EnterInClassShiftCommand(otherStudent.Id, shift.Id, validOtp);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(InClassShiftErrors.StudentNotEnrolled);
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
        var (lecturer, _, _, _, classEntity) = await SeedBaseDataAsync(context);

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = classEntity.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca thi chốt điểm quá trình",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMinutes(30),
            TotpSecretKey = TotpPasswordHelper.GenerateSecretKey(32),
            Status = "IN_PROGRESS",
            IsLocked = false,
            IsAutoPublishGrades = false
        };
        context.InClassExamShifts.Add(shift);
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
        var enterHandler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator());

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

        // Act 5: Student 1 tries to submit a second time -> blocked by DuplicateSubmission
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
        var enterHandler = new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator());
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

        var enter = await new EnterInClassShiftCommandHandler(context, new EnterInClassShiftCommandValidator())
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
}

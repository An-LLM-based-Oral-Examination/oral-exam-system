using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OralExamination.API.Workers;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;
using OralExamination.Application.Features.AcademicHierarchy.Queries.SearchAvailableClasses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using DlqEntity = OralExamination.Domain.Entities.DeadLetterQueue;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 1 (Empirical Challenger M1_1 - Adversarial Edge-Case Verifier).
/// Kiểm thử các kịch bản biên khắc nghiệt cho:
/// 1. Tra cứu lớp: Keyword rỗng, keyword ký tự đặc biệt, lọc lệch CourseId & SemesterId, LecturerId không dạy lớp nào.
/// 2. Bảo mật danh sách sinh viên: EnrolledStudents bắt buộc rỗng [] đối với role student ở mọi trạng thái/casing.
/// 3. Sĩ số và khóa lớp: Chặn join khi đầy sĩ số MaxStudents, IsJoinEnabled = false, AllowSelfEnrollment = false.
/// 4. DLQ Replay: OfficialExamDlqReplayWorker tuyệt đối không bốc lại bản ghi DLQ có status abandoned hoặc đang backoff.
/// </summary>
public class AdversarialMilestone1EdgeCaseVerificationTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static (Semester semester, Course course, User lecturer, User student) SeedBaseData(OralExamDbContext context)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true
        };

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Assigned Lecturer",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Enrolled Student",
            StudentCode = "SE170001",
            Role = UserRole.Student,
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform .NET",
            SemesterId = semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(lecturer, student);
        context.Courses.Add(course);
        context.SaveChanges();

        return (semester, course, lecturer, student);
    }

    // =========================================================================
    // SECTION 1: TRA CỨU LỚP (CLASSES SEARCH ADVERSARIAL EDGE CASES)
    // =========================================================================

    [Theory(DisplayName = "ADV-SEARCH-01: Tra cứu lớp với Keyword rỗng, khoảng trắng hoặc null không làm crash và trả về đầy đủ lớp đang mở")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public async Task SearchAvailableClasses_EmptyOrWhitespaceKeyword_DoesNotCrash_AndReturnsAllMatchingClasses(string? emptyKeyword)
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _) = SeedBaseData(context);

        var class1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-01",
            Name = "Lớp PRN231 SE01",
            JoinCode = "CODE01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(class1);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(Keyword: emptyKeyword),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.ClassCode == "PRN231-01");
    }

    [Theory(DisplayName = "ADV-SEARCH-02: Tra cứu lớp với Keyword chứa ký tự đặc biệt, SQL injection, XSS payload không làm lỗi hệ thống")]
    [InlineData("!@#$%^&*()_+{}|:\"<>?~`-=[]\\;',./")]
    [InlineData("' OR '1'='1' --")]
    [InlineData("'; DROP TABLE classes; --")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("%_%")]
    public async Task SearchAvailableClasses_SpecialCharactersKeyword_ReturnsEmptySafelyWithoutException(string specialKeyword)
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _) = SeedBaseData(context);

        var class1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-01",
            Name = "Lớp PRN231 SE01",
            JoinCode = "CODE01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(class1);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(Keyword: specialKeyword),
            CancellationToken.None);

        // Assert: Không crash, trả về danh sách rỗng an toàn vì không khớp ký tự đặc biệt
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "ADV-SEARCH-03: Lọc kết hợp CourseId và SemesterId không khớp lớp nào trả về danh sách rỗng")]
    public async Task SearchAvailableClasses_MismatchedCourseAndSemester_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _) = SeedBaseData(context);

        var otherSemester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "SP27",
            Name = "Spring 2027",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 4, 30),
            IsActive = true
        };
        context.Semesters.Add(otherSemester);

        var class1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-FA26",
            Name = "Lớp PRN231 FA26",
            JoinCode = "FA26CODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(class1);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act: Tìm theo course.Id nhưng kết hợp với otherSemester.Id (không có lớp nào khớp)
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(CourseId: course.Id, SemesterId: otherSemester.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "ADV-SEARCH-04: Lọc theo LecturerId không dạy lớp nào trả về danh sách rỗng")]
    public async Task SearchAvailableClasses_UnassignedLecturerId_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _) = SeedBaseData(context);

        var unassignedLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "no.classes@fpt.edu.vn",
            FullName = "Lecturer Without Classes",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(unassignedLecturer);

        var class1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-01",
            Name = "Lớp PRN231 SE01",
            JoinCode = "CODE01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id, // Gán cho lecturer khác
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(class1);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(LecturerId: unassignedLecturer.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // =========================================================================
    // SECTION 2: BẢO MẬT DANH SÁCH SINH VIÊN (STUDENT ROSTER PRIVACY)
    // =========================================================================

    [Theory(DisplayName = "ADV-ROSTER-01: Sinh viên (dù đã ghi danh hay chưa, mọi cách viết hoa/thường role) tuyệt đối không xem được EnrolledStudents")]
    [InlineData("student", true)]
    [InlineData("STUDENT", true)]
    [InlineData("Student", true)]
    [InlineData(" student ", true)]
    [InlineData("student", false)]
    [InlineData("STUDENT", false)]
    public async Task GetClassDetail_StudentRole_AlwaysReceivesEmptyEnrolledStudentsRoster(string studentRole, bool isEnrolled)
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, student1) = SeedBaseData(context);

        var student2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "student2@fpt.edu.vn",
            FullName = "Peer Student Two",
            StudentCode = "SE170002",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(student2);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-PRIVACY",
            Name = "Lớp Kiểm Thử Quyền Riêng Tư",
            JoinCode = "PRIVACY1",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(targetClass);

        // Đưa student2 vào lớp
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student2.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });

        if (isEnrolled)
        {
            context.ClassEnrollments.Add(new ClassEnrollment
            {
                Id = Guid.NewGuid(),
                ClassId = targetClass.Id,
                StudentId = student1.Id,
                Status = "enrolled",
                EnrolledAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var handler = new GetClassDetailQueryHandler(context);

        // Act
        var query = new GetClassDetailQuery(targetClass.Id, student1.Id, studentRole);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // Bắt buộc EnrolledStudents phải rỗng 100% để bảo vệ thông tin sinh viên cùng lớp
        result.Value.EnrolledStudents.Should().BeEmpty("Sinh viên không được phép xem thông tin danh sách sinh viên cùng lớp");
    }

    // =========================================================================
    // SECTION 3: SĨ SỐ VÀ KHÓA LỚP (CLASS CAPACITY & LOCK CONTROLS)
    // =========================================================================

    [Fact(DisplayName = "ADV-CAPACITY-01: Sinh viên bị chặn khi lớp đã đạt số lượng tối đa MaxStudents")]
    public async Task JoinClass_WhenClassIsAtMaxCapacity_ReturnsFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, student1) = SeedBaseData(context);

        var candidateStudent = new User
        {
            Id = Guid.NewGuid(),
            Email = "candidate@fpt.edu.vn",
            FullName = "Candidate Student",
            StudentCode = "SE170099",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Users.Add(candidateStudent);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-FULL",
            Name = "Lớp Đầy Sĩ Số",
            JoinCode = "FULLCODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 1, // Sĩ số tối đa là 1
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(targetClass);

        // Đã có 1 sinh viên enroll (đủ 1/1)
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var passwordHasher = new Pbkdf2PasswordHasher();
        var handler = new JoinClassCommandHandler(context, passwordHasher, new JoinClassCommandValidator());

        // Act: Sinh viên khác cố gắng tham gia
        var command = new JoinClassCommand(candidateStudent.Id, targetClass.JoinCode, null, targetClass.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Lớp học đã đạt số lượng sinh viên tối đa.");
    }

    [Fact(DisplayName = "ADV-LOCK-02: Sinh viên bị chặn khi IsJoinEnabled = false")]
    public async Task JoinClass_WhenIsJoinEnabledIsFalse_ReturnsJoinDisabledFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-LOCKED",
            Name = "Lớp Khóa Tính Năng Tham Gia",
            JoinCode = "LOCKCODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = false, // Khóa tham gia
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var passwordHasher = new Pbkdf2PasswordHasher();
        var handler = new JoinClassCommandHandler(context, passwordHasher, new JoinClassCommandValidator());

        // Act
        var command = new JoinClassCommand(student.Id, targetClass.JoinCode, null, targetClass.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("JOIN_DISABLED: Lớp học hiện đang khóa tính năng tham gia.");
    }

    [Fact(DisplayName = "ADV-ENROLL-03: Sinh viên bị chặn khi AllowSelfEnrollment = false")]
    public async Task JoinClass_WhenAllowSelfEnrollmentIsFalse_ReturnsSelfEnrollmentDisabledFailure()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "PRN231-NOSELF",
            Name = "Lớp Không Cho Tự Ghi Danh",
            JoinCode = "NOSELF01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = false, // Không cho tự ghi danh
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var passwordHasher = new Pbkdf2PasswordHasher();
        var handler = new JoinClassCommandHandler(context, passwordHasher, new JoinClassCommandValidator());

        // Act
        var command = new JoinClassCommand(student.Id, targetClass.JoinCode, null, targetClass.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Lớp học không cho phép sinh viên tự ghi danh.");
    }

    // =========================================================================
    // SECTION 4: DLQ REPLAY VÀ BẢN GHI ABANDONED / BACKOFF
    // =========================================================================

    [Fact(DisplayName = "ADV-DLQ-01: OfficialExamDlqReplayWorker tuyệt đối không bốc lại bản ghi DLQ có status 'abandoned'")]
    public async Task OfficialExamDlqReplayWorker_NeverReplays_AbandonedDlqEntries()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<OralExamDbContext>();

        var submissionId = Guid.NewGuid();

        var abandonedEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = $"{{\"InClassSubmissionId\":\"{submissionId}\"}}",
            ErrorMessage = "Max retry attempts exceeded",
            RetryCount = 9,
            Status = "abandoned", // Đã bị abandoned
            NextRetryAt = null,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        context.DeadLetterQueues.Add(abandonedEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Channel TUYỆT ĐỐI KHÔNG nhận task cho DLQ abandoned
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.IsAny<GradingTask>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Trạng thái DLQ vẫn giữ nguyên abandoned
        var checkDlq = await context.DeadLetterQueues.FindAsync(abandonedEntry.Id);
        checkDlq!.Status.Should().Be("abandoned");
    }

    [Fact(DisplayName = "ADV-DLQ-02: OfficialExamDlqReplayWorker tuyệt đối không bốc lại bản ghi DLQ đang trong thời gian backoff (NextRetryAt > now)")]
    public async Task OfficialExamDlqReplayWorker_NeverReplays_DlqEntriesCurrentlyInBackoff()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<OralExamDbContext>();

        var submissionId = Guid.NewGuid();

        var backoffEntry = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = $"{{\"InClassSubmissionId\":\"{submissionId}\"}}",
            ErrorMessage = "Transient rate limit error",
            RetryCount = 2,
            Status = "pending",
            NextRetryAt = DateTime.UtcNow.AddMinutes(10), // Đang backoff 10 phút nữa
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        context.DeadLetterQueues.Add(backoffEntry);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Channel TUYỆT ĐỐI KHÔNG nhận task vì đang trong backoff
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.IsAny<GradingTask>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "ADV-DLQ-03: InClassExamSubmission bị kẹt nhưng có DLQ abandoned bị loại trừ khỏi luồng quét tự động (Chống Infinite Loop)")]
    public async Task OfficialExamDlqReplayWorker_ExcludesStuckSubmission_WhenAssociatedDlqIsAbandoned()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<OralExamDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = provider.GetRequiredService<OralExamDbContext>();

        var submissionId = Guid.NewGuid();

        // Submission bị kẹt 10 phút
        var stuckSubmission = new InClassExamSubmission
        {
            Id = submissionId,
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Transcript = "Bài thi bị lỗi mô hình.",
            Status = "SUBMITTED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.InClassExamSubmissions.Add(stuckSubmission);

        // Có DLQ abandoned liên kết
        var abandonedDlq = new DlqEntity
        {
            Id = Guid.NewGuid(),
            TaskType = "InClassGrading",
            FlowType = "IN_CLASS_EXAM",
            ManagedByRole = "lecturer",
            CorrelationId = submissionId,
            PayloadJson = "{}",
            ErrorMessage = "Permanently abandoned",
            RetryCount = 9,
            Status = "abandoned",
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        context.DeadLetterQueues.Add(abandonedDlq);
        await context.SaveChangesAsync();

        var queueChannelMock = new Mock<IGradingQueueChannel>();
        var worker = new OfficialExamDlqReplayWorker(
            scopeFactory,
            NullLogger<OfficialExamDlqReplayWorker>.Instance,
            queueChannelMock.Object);

        // Act
        await worker.ReplayDueAsync(CancellationToken.None);

        // Assert: Bị loại trừ, không đẩy vào queue
        queueChannelMock.Verify(q => q.EnqueueAsync(
            It.Is<GradingTask>(t => t.InClassSubmissionId == submissionId),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

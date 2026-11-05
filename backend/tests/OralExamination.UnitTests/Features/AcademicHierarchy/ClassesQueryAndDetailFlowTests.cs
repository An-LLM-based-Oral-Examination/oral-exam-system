using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetClassDetail;
using OralExamination.Application.Features.AcademicHierarchy.Queries.SearchAvailableClasses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public class ClassesQueryAndDetailFlowTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static (Semester semester, Course course, User lecturer1, User lecturer2, User student1, User student2) SeedBaseData(OralExamDbContext context)
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

        var lecturer1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer1@fpt.edu.vn",
            FullName = "Lecturer One",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var lecturer2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer2@fpt.edu.vn",
            FullName = "Lecturer Two",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var student1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "student1@fpt.edu.vn",
            FullName = "Student One",
            StudentCode = "SE180001",
            Role = UserRole.Student,
            IsActive = true
        };

        var student2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "student2@fpt.edu.vn",
            FullName = "Student Two",
            StudentCode = "SE180002",
            Role = UserRole.Student,
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Cross-Platform .NET Development",
            SemesterId = semester.Id,
            DepartmentHeadUserId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(lecturer1, lecturer2, student1, student2);
        context.Courses.Add(course);
        context.SaveChanges();

        return (semester, course, lecturer1, lecturer2, student1, student2);
    }

    [Fact(DisplayName = "1. SearchAvailableClassesQuery: Lọc chính xác theo từ khóa (Code, Name, JoinCode)")]
    public async Task SearchAvailableClasses_FilterByKeyword_MatchesCodeNameAndJoinCode()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer1, _, student1, _) = SeedBaseData(context);

        var class1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp PRN231 SE1801 sáng Thứ 2",
            JoinCode = "JOIN1801",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer1.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var class2 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1802-NET",
            Name = "Lớp PRN231 SE1802 chiều Thứ 4",
            JoinCode = "JOIN1802",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer1.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.AddRange(class1, class2);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act 1: Search by Code
        var resultByCode = await handler.Handle(
            new SearchAvailableClassesQuery(Keyword: "1801"),
            CancellationToken.None);

        // Act 2: Search by Name
        var resultByName = await handler.Handle(
            new SearchAvailableClassesQuery(Keyword: "chiều Thứ 4"),
            CancellationToken.None);

        // Act 3: Search by JoinCode
        var resultByJoinCode = await handler.Handle(
            new SearchAvailableClassesQuery(Keyword: "JOIN1802"),
            CancellationToken.None);

        // Assert
        resultByCode.IsSuccess.Should().BeTrue();
        resultByCode.Value.Should().ContainSingle(c => c.ClassCode == "SE1801-NET");

        resultByName.IsSuccess.Should().BeTrue();
        resultByName.Value.Should().ContainSingle(c => c.ClassCode == "SE1802-NET");

        resultByJoinCode.IsSuccess.Should().BeTrue();
        resultByJoinCode.Value.Should().ContainSingle(c => c.ClassCode == "SE1802-NET");
    }

    [Fact(DisplayName = "2. SearchAvailableClassesQuery: Lọc theo CourseId và SemesterId")]
    public async Task SearchAvailableClasses_FilterByCourseIdAndSemesterId_ReturnsOnlyMatchingClasses()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester1, course1, lecturer, _, _, _) = SeedBaseData(context);

        var semester2 = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "SP27",
            Name = "Spring 2027",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 4, 30),
            IsActive = true
        };

        var course2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Software Project",
            SemesterId = semester1.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Semesters.Add(semester2);
        context.Courses.Add(course2);

        var classInTarget = new Class
        {
            Id = Guid.NewGuid(),
            Code = "TARGET-CLASS",
            Name = "Target Class PRN231 FA26",
            JoinCode = "TARGET01",
            CourseId = course1.Id,
            SemesterId = semester1.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classOtherSemester = new Class
        {
            Id = Guid.NewGuid(),
            Code = "OTHER-SEM",
            Name = "Other Semester Class",
            JoinCode = "OTHER01",
            CourseId = course1.Id,
            SemesterId = semester2.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classOtherCourse = new Class
        {
            Id = Guid.NewGuid(),
            Code = "OTHER-COURSE",
            Name = "Other Course Class",
            JoinCode = "OTHER02",
            CourseId = course2.Id,
            SemesterId = semester1.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.AddRange(classInTarget, classOtherSemester, classOtherCourse);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(CourseId: course1.Id, SemesterId: semester1.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().ClassCode.Should().Be("TARGET-CLASS");
    }

    [Fact(DisplayName = "3. SearchAvailableClassesQuery: Lọc theo LecturerId trả về đúng danh sách lớp của giảng viên")]
    public async Task SearchAvailableClasses_FilterByLecturerId_ReturnsOnlyLecturerClasses()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer1, lecturer2, _, _) = SeedBaseData(context);

        var classLecturer1 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-GV1",
            Name = "Lớp do GV1 phụ trách",
            JoinCode = "GV1CODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer1.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var classLecturer2 = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-GV2",
            Name = "Lớp do GV2 phụ trách",
            JoinCode = "GV2CODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer2.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.AddRange(classLecturer1, classLecturer2);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(LecturerId: lecturer1.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().ClassCode.Should().Be("CLASS-GV1");
        result.Value.First().LecturerName.Should().Be("Lecturer One");
    }

    [Fact(DisplayName = "4. SearchAvailableClassesQuery: Cờ OnlyAvailableToJoin = true loại bỏ lớp đầy sĩ số, lớp khóa join, và lớp đã ghi danh")]
    public async Task SearchAvailableClasses_OnlyAvailableToJoin_FiltersOutFullAndEnrolledClasses()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _, student1, student2) = SeedBaseData(context);

        // Lớp 1: Đã đầy sĩ số (Max 1, Enrolled 1)
        var classFull = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-FULL",
            Name = "Lớp đã đầy",
            JoinCode = "FULL01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 1,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var enrollmentFull = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classFull.Id,
            StudentId = student2.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        };

        // Lớp 2: Khóa tính năng tham gia
        var classLocked = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-LOCKED",
            Name = "Lớp bị khóa join",
            JoinCode = "LOCK01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = false,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Lớp 3: Sinh viên student1 đã tham gia trước đó
        var classAlreadyEnrolled = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-ALREADY",
            Name = "Lớp sinh viên đã tham gia",
            JoinCode = "ALREADY1",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var enrollmentStudent1 = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = classAlreadyEnrolled.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        };

        // Lớp 4: Lớp hợp lệ, còn chỗ để tham gia
        var classAvailable = new Class
        {
            Id = Guid.NewGuid(),
            Code = "CLASS-OPEN",
            Name = "Lớp đang mở tự do",
            JoinCode = "OPEN01",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.AddRange(classFull, classLocked, classAlreadyEnrolled, classAvailable);
        context.ClassEnrollments.AddRange(enrollmentFull, enrollmentStudent1);
        await context.SaveChangesAsync();

        var handler = new SearchAvailableClassesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new SearchAvailableClassesQuery(CurrentStudentId: student1.Id, OnlyAvailableToJoin: true),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().ClassCode.Should().Be("CLASS-OPEN");
        result.Value.First().CanJoin.Should().BeTrue();
        result.Value.First().IsAlreadyEnrolled.Should().BeFalse();
    }

    [Fact(DisplayName = "5. GetClassDetailQuery: Role student (đã enroll) thấy ca thi MF-05, ẩn danh sách sinh viên EnrolledStudents")]
    public async Task GetClassDetail_As_Student_Enrolled_Sees_Shifts_And_Hides_EnrolledStudents()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _, student1, student2) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp PRN231 SE1801",
            JoinCode = "JOIN1801",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var enrollment1 = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        };

        var enrollment2 = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student2.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        };

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            ShiftName = "Progress Test 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.Add(targetClass);
        context.ClassEnrollments.AddRange(enrollment1, enrollment2);
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new GetClassDetailQueryHandler(context);

        // Act: Student 1 đã enroll gọi xem chi tiết
        var query = new GetClassDetailQuery(targetClass.Id, student1.Id, "student");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsAlreadyEnrolled.Should().BeTrue();
        result.Value.EnrolledCount.Should().Be(2);

        // Sinh viên xem được danh sách ca thi MF-05
        result.Value.Shifts.Should().ContainSingle();
        result.Value.Shifts.First().ShiftName.Should().Be("Progress Test 1");

        // Tuyệt đối ẩn danh sách sinh viên khác (bảo vệ thông tin cá nhân)
        result.Value.EnrolledStudents.Should().BeEmpty();
    }

    [Fact(DisplayName = "6. GetClassDetailQuery: Role student (chưa enroll) không thấy ca thi MF-05 và không thấy danh sách sinh viên")]
    public async Task GetClassDetail_As_Student_NotEnrolled_Hides_Shifts_And_EnrolledStudents()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _, student1, student2) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp PRN231 SE1801",
            JoinCode = "JOIN1801",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            ShiftName = "Progress Test 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.Add(targetClass);
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new GetClassDetailQueryHandler(context);

        // Act: Student 1 chưa ghi danh gọi xem chi tiết
        var query = new GetClassDetailQuery(targetClass.Id, student1.Id, "student");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsAlreadyEnrolled.Should().BeFalse();

        // Ẩn cả ca thi lẫn danh sách sinh viên
        result.Value.Shifts.Should().BeEmpty();
        result.Value.EnrolledStudents.Should().BeEmpty();
    }

    [Fact(DisplayName = "7. GetClassDetailQuery: Role lecturer phụ trách thấy đầy đủ danh sách sinh viên và ca thi")]
    public async Task GetClassDetail_As_Owner_Lecturer_Sees_Both_EnrolledStudents_And_Shifts()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer, _, student1, student2) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp PRN231 SE1801",
            JoinCode = "JOIN1801",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var enrollment1 = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student1.Id,
            Status = "enrolled",
            EnrolledAt = DateTime.UtcNow
        };

        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            ShiftName = "Progress Test 1",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            TotpSecretKey = "12345678901234567890123456789012",
            Status = "SCHEDULED",
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.Add(targetClass);
        context.ClassEnrollments.Add(enrollment1);
        context.InClassExamShifts.Add(shift);
        await context.SaveChangesAsync();

        var handler = new GetClassDetailQueryHandler(context);

        // Act: Giảng viên phụ trách gọi xem chi tiết
        var query = new GetClassDetailQuery(targetClass.Id, lecturer.Id, "lecturer");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Thấy cả ca thi và danh sách sinh viên
        result.Value.Shifts.Should().ContainSingle(s => s.ShiftName == "Progress Test 1");
        result.Value.EnrolledStudents.Should().ContainSingle(e => e.FullName == "Student One");
    }

    [Fact(DisplayName = "8. GetClassDetailQuery: Role lecturer không phụ trách bị chặn 403 Forbidden")]
    public async Task GetClassDetail_As_NonOwner_Lecturer_Returns_403_Forbidden()
    {
        // Arrange
        using var context = CreateDbContext();
        var (semester, course, lecturer1, lecturer2, _, _) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp của GV 1",
            JoinCode = "JOIN1801",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer1.Id,
            MaxStudents = 30,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var handler = new GetClassDetailQueryHandler(context);

        // Act: Giảng viên 2 (không phụ trách) gọi xem chi tiết lớp của Giảng viên 1
        var query = new GetClassDetailQuery(targetClass.Id, lecturer2.Id, "lecturer");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN: Giảng viên chỉ được phép xem chi tiết lớp học phần do chính mình phụ trách.");
    }

    [Fact(DisplayName = "9. GetClassDetailQuery: Trả về NOT_FOUND khi ID lớp không tồn tại")]
    public async Task GetClassDetail_WhenClassNotFound_Returns_NotFound()
    {
        // Arrange
        using var context = CreateDbContext();
        var handler = new GetClassDetailQueryHandler(context);

        // Act
        var query = new GetClassDetailQuery(Guid.NewGuid(), Guid.NewGuid(), "admin");
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NOT_FOUND: Không tìm thấy lớp học phần.");
    }
}

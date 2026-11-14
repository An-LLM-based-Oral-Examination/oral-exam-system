using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.ResetClassPassword;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseLecturers;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerClasses;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetStudentMyClasses;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.CreateCourseClo;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public class AcademicHierarchyTests
{
    private readonly IPasswordHasher _passwordHasher = new Pbkdf2PasswordHasher();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static (Semester semester, Course course, User deptHead, User lecturer, User student) SeedBaseData(OralExamDbContext context)
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

        var deptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fpt.edu.vn",
            FullName = "Department Head User",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };

        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Lecturer User",
            Role = UserRole.Lecturer,
            IsActive = true
        };

        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Student User",
            StudentCode = "SE180001",
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
            IsActive = true
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(deptHead, lecturer, student);
        context.Courses.Add(course);
        context.SaveChanges();

        return (semester, course, deptHead, lecturer, student);
    }

    #region 1. AssignDepartmentHead Tests

    [Fact(DisplayName = "1.1. Admin gán Trưởng BM thành công -> Cập nhật DepartmentHeadUserId")]
    public async Task AssignDepartmentHead_With_Valid_Data_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, _, _) = SeedBaseData(context);

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(course.Id, deptHead.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.DepartmentHeadUserId.Should().Be(deptHead.Id);
        result.Value.DepartmentHeadName.Should().Be(deptHead.FullName);

        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.DepartmentHeadUserId.Should().Be(deptHead.Id);
    }

    [Fact(DisplayName = "1.2. Admin gán Trưởng BM thất bại khi Course không tồn tại")]
    public async Task AssignDepartmentHead_Course_Not_Found_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (_, _, deptHead, _, _) = SeedBaseData(context);

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(Guid.NewGuid(), deptHead.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy môn học");
    }

    [Fact(DisplayName = "1.3. Admin gán Trưởng BM thất bại khi User không có role department_head")]
    public async Task AssignDepartmentHead_With_Invalid_Role_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (_, course, _, _, student) = SeedBaseData(context);

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(course.Id, student.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có vai trò Trưởng bộ môn");
    }

    [Fact(DisplayName = "1.4. Validator chặn CourseId hoặc DepartmentHeadUserId rỗng")]
    public async Task AssignDepartmentHead_Validation_Failure()
    {
        await using var context = CreateDbContext();
        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(Guid.Empty, Guid.Empty);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 2. AssignLecturersToCourse Tests

    [Fact(DisplayName = "2.1. Trưởng BM phân công danh sách Giảng viên thành công -> Lưu CourseLecturer")]
    public async Task AssignLecturersToCourse_With_Valid_Lecturers_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        var lecturer2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer2@fpt.edu.vn",
            FullName = "Lecturer User 2",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(lecturer2);
        await context.SaveChangesAsync();

        var validator = new AssignLecturersToCourseCommandValidator();
        var handler = new AssignLecturersToCourseCommandHandler(context, validator);

        var command = new AssignLecturersToCourseCommand(
            course.Id,
            new List<Guid> { lecturer.Id, lecturer2.Id },
            deptHead.Id,
            semester.Id,
            "department_head");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AssignedCount.Should().Be(2);
        result.Value.Lecturers.Should().HaveCount(2);

        var assigned = await context.CourseLecturers
            .Where(cl => cl.CourseId == course.Id)
            .ToListAsync();

        assigned.Should().HaveCount(2);
        assigned.Should().OnlyContain(cl => cl.IsActive);
    }

    [Fact(DisplayName = "2.2. Trưởng BM phân công thất bại khi danh sách có role không phải lecturer")]
    public async Task AssignLecturersToCourse_With_Non_Lecturer_Role_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, student) = SeedBaseData(context);

        var validator = new AssignLecturersToCourseCommandValidator();
        var handler = new AssignLecturersToCourseCommandHandler(context, validator);

        var command = new AssignLecturersToCourseCommand(
            course.Id,
            new List<Guid> { lecturer.Id, student.Id }, // student is not lecturer
            deptHead.Id,
            semester.Id,
            "department_head");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("không có vai trò Giảng viên");
    }

    [Fact(DisplayName = "2.3. Trưởng BM phân công thất bại khi User ID không tồn tại")]
    public async Task AssignLecturersToCourse_User_Not_Found_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, _, _) = SeedBaseData(context);

        var validator = new AssignLecturersToCourseCommandValidator();
        var handler = new AssignLecturersToCourseCommandHandler(context, validator);

        var command = new AssignLecturersToCourseCommand(
            course.Id,
            new List<Guid> { Guid.NewGuid() },
            deptHead.Id,
            semester.Id,
            "department_head");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Một số giảng viên không tồn tại");
    }

    [Fact(DisplayName = "2.4. Phân công lại giảng viên đã được gán trước đó -> Kích hoạt lại IsActive = true")]
    public async Task AssignLecturersToCourse_Reactivate_Existing_Assignment()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        var existingCl = new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = deptHead.Id,
            SemesterId = semester.Id,
            IsActive = false, // previously deactivated
            AssignedAt = DateTime.UtcNow.AddDays(-10),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };
        context.CourseLecturers.Add(existingCl);
        await context.SaveChangesAsync();

        var validator = new AssignLecturersToCourseCommandValidator();
        var handler = new AssignLecturersToCourseCommandHandler(context, validator);

        var command = new AssignLecturersToCourseCommand(
            course.Id,
            new List<Guid> { lecturer.Id },
            deptHead.Id,
            semester.Id,
            "department_head");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedCount.Should().Be(1);

        var reloaded = await context.CourseLecturers.FindAsync(existingCl.Id);
        reloaded!.IsActive.Should().BeTrue();
    }

    #endregion

    #region 3. CreateClass Tests

    [Fact(DisplayName = "3.1. Giảng viên tạo lớp học có JoinCode & Password băm thành công")]
    public async Task CreateClass_With_JoinCode_And_Password_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        context.CourseLecturers.Add(new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = deptHead.Id,
            SemesterId = semester.Id,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var validator = new CreateClassCommandValidator();
        var handler = new CreateClassCommandHandler(context, _passwordHasher, validator);

        var command = new CreateClassCommand(
            Code: "SE1802-NET",
            Name: "Lớp SE1802 .NET Web API",
            CourseId: course.Id,
            SemesterId: semester.Id,
            LecturerId: lecturer.Id,
            JoinCode: "JOIN1802",
            Password: "SecurePassword123!",
            MaxStudents: 30,
            AllowSelfEnrollment: true,
            IsJoinEnabled: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Code.Should().Be("SE1802-NET");
        result.Value.Name.Should().Be("Lớp SE1802 .NET Web API");
        result.Value.JoinCode.Should().Be("JOIN1802");
        result.Value.HasPassword.Should().BeTrue();

        var savedClass = await context.Classes.FindAsync(result.Value.Id);
        savedClass.Should().NotBeNull();
        savedClass!.JoinPasswordHash.Should().NotBeNull();
        savedClass.JoinPasswordHash.Should().NotBe("SecurePassword123!");
        _passwordHasher.Verify("SecurePassword123!", savedClass.JoinPasswordHash!).Should().BeTrue();
    }

    [Fact(DisplayName = "3.2. Giảng viên tạo lớp không truyền JoinCode -> Sinh JoinCode tự động")]
    public async Task CreateClass_Without_JoinCode_Should_Auto_Generate()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        context.CourseLecturers.Add(new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = deptHead.Id,
            SemesterId = semester.Id,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var validator = new CreateClassCommandValidator();
        var handler = new CreateClassCommandHandler(context, _passwordHasher, validator);

        var command = new CreateClassCommand(
            Code: "SE1803-NET",
            Name: "Lớp SE1803 .NET",
            CourseId: course.Id,
            SemesterId: semester.Id,
            LecturerId: lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.JoinCode.Should().NotBeNullOrWhiteSpace();
        result.Value.JoinCode.Length.Should().Be(8);
        result.Value.HasPassword.Should().BeFalse();
    }

    [Fact(DisplayName = "3.3. Giảng viên tạo lớp trùng mã Code -> Báo lỗi đã tồn tại")]
    public async Task CreateClass_Duplicate_Code_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        context.CourseLecturers.Add(new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = deptHead.Id,
            SemesterId = semester.Id,
            IsActive = true
        });

        var existingClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801-NET",
            Name = "Lớp SE1801-NET",
            JoinCode = "SE1801NET",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id
        };
        context.Classes.Add(existingClass);
        await context.SaveChangesAsync();

        var validator = new CreateClassCommandValidator();
        var handler = new CreateClassCommandHandler(context, _passwordHasher, validator);

        var command = new CreateClassCommand(
            Code: "se1801-net", // duplicate case-insensitive
            Name: "Trùng mã lớp",
            CourseId: course.Id,
            SemesterId: semester.Id,
            LecturerId: lecturer.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Mã lớp học đã tồn tại");
    }

    [Fact(DisplayName = "3.4. Giảng viên chưa được Trưởng BM phân công vào course_lecturers gọi CreateClass -> Bị chặn FORBIDDEN")]
    public async Task CreateClass_Unassigned_Lecturer_Should_Return_Forbidden()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, _) = SeedBaseData(context);

        var validator = new CreateClassCommandValidator();
        var handler = new CreateClassCommandHandler(context, _passwordHasher, validator);

        var command = new CreateClassCommand(
            Code: "SE1804-UNASSIGNED",
            Name: "Lớp chưa được phân công",
            CourseId: course.Id,
            SemesterId: semester.Id,
            LecturerId: lecturer.Id,
            ActorRole: "lecturer");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
    }

    [Fact(DisplayName = "3.5. ClassesController.CreateClass chặn 403 Forbidden khi giảng viên cố tình truyền LecturerId của giảng viên khác")]
    public async Task ClassesController_CreateClass_Impersonating_Another_Lecturer_Should_Return_403()
    {
        var senderMock = new Mock<ISender>();
        var controller = new ClassesController(senderMock.Object);

        var callerLecturerId = Guid.NewGuid();
        var otherLecturerId = Guid.NewGuid();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, callerLecturerId.ToString()),
                    new Claim(ClaimTypes.Role, "lecturer")
                ], "TestAuth"))
            }
        };

        var request = new CreateClassRequest(
            Code: "SE1899-IMP",
            Name: "Lớp mạo danh",
            CourseId: Guid.NewGuid(),
            SemesterId: Guid.NewGuid(),
            LecturerId: otherLecturerId);

        var actionResult = await controller.CreateClass(request);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        senderMock.Verify(s => s.Send(It.IsAny<CreateClassCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 4. JoinClass Tests

    [Fact(DisplayName = "4.1. Sinh viên join lớp đúng code & password thành công -> JoinedViaCode = true")]
    public async Task JoinClass_With_Correct_Code_And_Password_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var passwordHash = _passwordHasher.Hash("Secret123!");
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1804-NET",
            Name = "Lớp SE1804-NET",
            JoinCode = "SE1804CODE",
            JoinPasswordHash = passwordHash,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "se1804code", "Secret123!");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ClassId.Should().Be(targetClass.Id);
        result.Value.Status.Should().Be("enrolled");

        var enrollment = await context.ClassEnrollments
            .FirstOrDefaultAsync(e => e.ClassId == targetClass.Id && e.StudentId == student.Id);

        enrollment.Should().NotBeNull();
        enrollment!.JoinedViaCode.Should().BeTrue();
        enrollment.JoinMethod.Should().Be("PASSWORD");
        enrollment.Status.Should().Be("enrolled");
    }

    [Fact(DisplayName = "4.2. Sinh viên join lớp sai password -> Báo lỗi INVALID_PASSWORD")]
    public async Task JoinClass_With_Wrong_Password_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var passwordHash = _passwordHasher.Hash("Secret123!");
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1805-NET",
            Name = "Lớp SE1805-NET",
            JoinCode = "SE1805CODE",
            JoinPasswordHash = passwordHash,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "SE1805CODE", "WrongPassword!");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("INVALID_PASSWORD");
    }

    [Fact(DisplayName = "4.3. Sinh viên join lớp bị khóa is_join_enabled = false -> Báo lỗi JOIN_DISABLED")]
    public async Task JoinClass_When_Join_Disabled_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1806-NET",
            Name = "Lớp SE1806-NET",
            JoinCode = "SE1806CODE",
            IsJoinEnabled = false, // Lock join
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "SE1806CODE", null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("JOIN_DISABLED");
    }

    [Fact(DisplayName = "4.4. Sinh viên đã ghi danh cố join lại -> Báo lỗi ALREADY_ENROLLED")]
    public async Task JoinClass_Already_Enrolled_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1807-NET",
            Name = "Lớp SE1807-NET",
            JoinCode = "SE1807CODE",
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(targetClass);

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student.Id,
            Status = "enrolled",
            JoinedViaCode = true,
            JoinMethod = "PASSWORD"
        };
        context.ClassEnrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "SE1807CODE", null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ALREADY_ENROLLED");
    }

    [Fact(DisplayName = "4.5. Sinh viên join lớp không tồn tại -> Báo lỗi NOT_FOUND")]
    public async Task JoinClass_Not_Found_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, student) = SeedBaseData(context);

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "NONEXISTENT", null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NOT_FOUND");
    }

    [Fact(DisplayName = "4.6. Sinh viên join lớp không đặt mật khẩu -> Thành công")]
    public async Task JoinClass_Without_Password_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1808-NET",
            Name = "Lớp SE1808-NET",
            JoinCode = "SE1808CODE",
            JoinPasswordHash = null, // No password
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var validator = new JoinClassCommandValidator();
        var handler = new JoinClassCommandHandler(context, _passwordHasher, validator);

        var command = new JoinClassCommand(student.Id, "SE1808CODE", null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClassId.Should().Be(targetClass.Id);
    }

    #endregion

    #region 5. Queries Tests

    [Fact(DisplayName = "5.1. GetCourseLecturers trả về danh sách giảng viên của môn học")]
    public async Task GetCourseLecturers_Should_Return_List()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);

        var cl = new CourseLecturer
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            LecturerUserId = lecturer.Id,
            AssignedByUserId = deptHead.Id,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.CourseLecturers.Add(cl);
        await context.SaveChangesAsync();

        var handler = new GetCourseLecturersQueryHandler(context);
        var query = new GetCourseLecturersQuery(course.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].LecturerId.Should().Be(lecturer.Id);
        result.Value[0].FullName.Should().Be(lecturer.FullName);
    }

    [Fact(DisplayName = "5.2. GetStudentMyClasses trả về danh sách lớp học của sinh viên")]
    public async Task GetStudentMyClasses_Should_Return_List()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1809-NET",
            Name = "Lớp SE1809-NET",
            JoinCode = "SE1809CODE",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id
        };
        context.Classes.Add(targetClass);

        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student.Id,
            Status = "enrolled",
            JoinedViaCode = true,
            JoinMethod = "PASSWORD"
        };
        context.ClassEnrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var handler = new GetStudentMyClassesQueryHandler(context);
        var query = new GetStudentMyClassesQuery(student.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ClassId.Should().Be(targetClass.Id);
        result.Value[0].ClassCode.Should().Be("SE1809-NET");

        course.IsActive = false;
        await context.SaveChangesAsync();
        var afterCourseDisabled = await handler.Handle(query, CancellationToken.None);
        afterCourseDisabled.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "5.3. GetLecturerClasses ẩn lớp thuộc môn đã ngừng hoạt động")]
    public async Task GetLecturerClasses_Should_Hide_Inactive_Academic_Context()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, _) = SeedBaseData(context);
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1810-CSD",
            Name = "Lớp SE1810-CSD",
            JoinCode = "SE1810CSD",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            IsActive = true
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var handler = new GetLecturerClassesQueryHandler(context);
        var activeResult = await handler.Handle(
            new GetLecturerClassesQuery(LecturerId: lecturer.Id),
            CancellationToken.None);
        activeResult.Value.Should().ContainSingle(item => item.ClassId == targetClass.Id);

        course.IsActive = false;
        await context.SaveChangesAsync();
        var inactiveResult = await handler.Handle(
            new GetLecturerClassesQuery(LecturerId: lecturer.Id),
            CancellationToken.None);
        inactiveResult.Value.Should().BeEmpty();
    }

    #endregion

    #region 6. Controller HTTP Action Tests

    [Fact(DisplayName = "6.1. ClassesController.CreateClass trả về 201 Created")]
    public async Task ClassesController_CreateClass_Should_Return_201()
    {
        var senderMock = new Mock<ISender>();
        var controller = new ClassesController(senderMock.Object);

        var responseDto = new CreateClassResponseDto(
            Guid.NewGuid(), "SE1810-NET", "Lớp SE1810", "JOIN1810",
            Guid.NewGuid(), "PRN231", "Cross-Platform .NET",
            Guid.NewGuid(), "FA26",
            Guid.NewGuid(), "Lecturer Name",
            35, true, true, true, DateTime.UtcNow);

        senderMock
            .Setup(s => s.Send(It.IsAny<CreateClassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateClassResponseDto>.Success(responseDto));

        var request = new CreateClassRequest("SE1810-NET", "Lớp SE1810", Guid.NewGuid(), Guid.NewGuid());
        var actionResult = await controller.CreateClass(request);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact(DisplayName = "6.2. ClassesController.JoinClass trả về 200 OK khi thành công")]
    public async Task ClassesController_JoinClass_Should_Return_200()
    {
        var senderMock = new Mock<ISender>();
        var controller = new ClassesController(senderMock.Object);

        var studentId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, studentId.ToString()),
                    new Claim(ClaimTypes.Role, "student")
                ], "TestAuth"))
            }
        };

        var responseDto = new JoinClassResponseDto(
            Guid.NewGuid(), "SE1810-NET", "Lớp SE1810",
            Guid.NewGuid(), "PRN231", "Cross-Platform .NET",
            Guid.NewGuid(), "FA26",
            Guid.NewGuid(), "Lecturer Name",
            DateTime.UtcNow, "enrolled", "PASSWORD");

        senderMock
            .Setup(s => s.Send(It.IsAny<JoinClassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinClassResponseDto>.Success(responseDto));

        var request = new JoinClassRequest("JOIN1810", "Password123!");
        var actionResult = await controller.JoinClass(request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "6.3. ClassesController.JoinClass trả về 400 BadRequest khi sai mật khẩu")]
    public async Task ClassesController_JoinClass_InvalidPassword_Should_Return_400()
    {
        var senderMock = new Mock<ISender>();
        var controller = new ClassesController(senderMock.Object);

        var studentId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, studentId.ToString()),
                    new Claim(ClaimTypes.Role, "student")
                ], "TestAuth"))
            }
        };

        senderMock
            .Setup(s => s.Send(It.IsAny<JoinClassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinClassResponseDto>.Failure("INVALID_PASSWORD: Mật khẩu tham gia lớp học không chính xác."));

        var request = new JoinClassRequest("JOIN1810", "WrongPass");
        var actionResult = await controller.JoinClass(request);

        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "6.4. CoursesController.AssignDepartmentHead trả về 200 OK")]
    public async Task CoursesController_AssignDepartmentHead_Should_Return_200()
    {
        var senderMock = new Mock<ISender>();
        var controller = new CoursesController(senderMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "admin")
                ], "TestAuth"))
            }
        };

        var courseId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var responseDto = new AssignDepartmentHeadResponseDto(
            courseId, "PRN231", "Cross-Platform .NET",
            deptHeadId, "Dept Head Name", "head@fpt.edu.vn");

        senderMock
            .Setup(s => s.Send(It.IsAny<AssignDepartmentHeadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssignDepartmentHeadResponseDto>.Success(responseDto));

        var request = new AssignDepartmentHeadRequest(deptHeadId);
        var actionResult = await controller.AssignDepartmentHead(courseId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "6.5. CoursesController.AssignLecturers trả về 200 OK")]
    public async Task CoursesController_AssignLecturers_Should_Return_200()
    {
        var senderMock = new Mock<ISender>();
        var controller = new CoursesController(senderMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "department_head")
                ], "TestAuth"))
            }
        };

        var courseId = Guid.NewGuid();
        var responseDto = new AssignLecturersToCourseResponseDto(
            courseId, "PRN231", "Cross-Platform .NET", 1, new List<CourseLecturerDto>());

        senderMock
            .Setup(s => s.Send(It.IsAny<AssignLecturersToCourseCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AssignLecturersToCourseResponseDto>.Success(responseDto));

        var request = new AssignLecturersToCourseRequest(new List<Guid> { Guid.NewGuid() });
        var actionResult = await controller.AssignLecturers(courseId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    #endregion

    #region 7. R5 Hardening: CreateCourse, UpdateCourse, ResetClassPassword, BOLA & Inactive User Guards

    [Fact(DisplayName = "7.1. Admin tạo môn học mới (CreateCourse) thành công, chuẩn hóa mã viết hoa và gán Trưởng BM")]
    public async Task CreateCourse_With_Valid_Data_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _) = SeedBaseData(context);

        var handler = new CreateCourseCommandHandler(context);
        var command = new CreateCourseCommand(
            Code: "  swd392  ",
            Name: "Software Architecture and Design",
            Credits: 3,
            Description: "Kiến trúc phần mềm",
            DepartmentHeadUserId: deptHead.Id,
            MaxMockExamsPerDay: 4,
            MockExamDurationMinutes: 25,
            MockExamTotalQuestions: 5,
            IsActive: true,
            SemesterId: semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("SWD392");
        result.Value.Name.Should().Be("Software Architecture and Design");
        result.Value.MaxMockExamsPerDay.Should().Be(4);

        var createdInDb = await context.Courses.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        createdInDb.Should().NotBeNull();
        createdInDb!.DepartmentHeadUserId.Should().Be(deptHead.Id);
    }

    [Fact(DisplayName = "7.2. Admin tạo môn học trùng mã Code -> Bị chặn CONFLICT")]
    public async Task CreateCourse_With_Duplicate_Code_Should_Fail()
    {
        await using var context = CreateDbContext();
        SeedBaseData(context);

        var handler = new CreateCourseCommandHandler(context);
        var command = new CreateCourseCommand(
            Code: "prn231",
            Name: "Duplicate Course");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("CONFLICT:");
    }

    [Fact(DisplayName = "7.3. Chặn gán Trưởng bộ môn có IsActive = false trong AssignDepartmentHead và CreateCourse")]
    public async Task AssignOrCreateCourse_With_Inactive_DepartmentHead_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, _, _) = SeedBaseData(context);
        deptHead.IsActive = false;
        await context.SaveChangesAsync();

        var assignHandler = new AssignDepartmentHeadCommandHandler(context, new AssignDepartmentHeadCommandValidator());
        var assignResult = await assignHandler.Handle(
            new AssignDepartmentHeadCommand(course.Id, deptHead.Id, "admin"),
            CancellationToken.None);

        assignResult.IsSuccess.Should().BeFalse();
        assignResult.Error.Should().Contain("đang bị khóa hoặc vô hiệu hóa");

        var createCourseHandler = new CreateCourseCommandHandler(context);
        var createResult = await createCourseHandler.Handle(
            new CreateCourseCommand("SWR302", "Software Requirements", DepartmentHeadUserId: deptHead.Id),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeFalse();
        createResult.Error.Should().Contain("đang bị khóa hoặc vô hiệu hóa");
    }

    [Fact(DisplayName = "7.4. Admin cập nhật môn học (UpdateCourse) thành công")]
    public async Task UpdateCourse_With_Valid_Data_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, _, _) = SeedBaseData(context);

        var handler = new UpdateCourseCommandHandler(context);
        var command = new UpdateCourseCommand(
            CourseId: course.Id,
            Code: "prn231-new",
            Name: "Advanced .NET Microservices",
            Credits: 4,
            Description: "Updated description",
            DepartmentHeadUserId: deptHead.Id,
            MaxMockExamsPerDay: 5,
            MockExamDurationMinutes: 30,
            MockExamTotalQuestions: 6,
            IsActive: true,
            SemesterId: semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("PRN231-NEW");
        result.Value.Credits.Should().Be(4);
        result.Value.MaxMockExamsPerDay.Should().Be(5);
    }

    [Fact(DisplayName = "7.5. Giảng viên phụ trách lớp đổi mật khẩu và JoinCode (ResetClassPassword) thành công")]
    public async Task ResetClassPassword_By_Assigned_Lecturer_Should_Succeed()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, _) = SeedBaseData(context);

        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1820",
            Name = "Lớp SE1820",
            JoinCode = "OLDJOIN1",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            IsJoinEnabled = true
        };
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var handler = new ResetClassPasswordCommandHandler(context, _passwordHasher);
        var command = new ResetClassPasswordCommand(
            ClassId: targetClass.Id,
            ActorUserId: lecturer.Id,
            NewPassword: "NewSecretPassword99!",
            NewJoinCode: "newjoin2",
            IsJoinEnabled: false,
            ActorRole: UserRole.Lecturer);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.JoinCode.Should().Be("NEWJOIN2");
        result.Value.HasPassword.Should().BeTrue();
        result.Value.IsJoinEnabled.Should().BeFalse();

        var updatedClass = await context.Classes.FindAsync(targetClass.Id);
        _passwordHasher.Verify("NewSecretPassword99!", updatedClass!.JoinPasswordHash!).Should().BeTrue();
    }

    [Fact(DisplayName = "7.6. Chặn BOLA trên ResetClassPassword khi Giảng viên khác hoặc Trưởng BM môn khác gọi")]
    public async Task ResetClassPassword_By_Other_Lecturer_Or_Other_DeptHead_Should_Fail_Forbidden()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead1, lecturer1, _) = SeedBaseData(context);
        course.DepartmentHeadUserId = deptHead1.Id;

        var otherLecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "other.lecturer@fpt.edu.vn",
            FullName = "Other Lecturer",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var otherDeptHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "other.head@fpt.edu.vn",
            FullName = "Other Dept Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1821",
            Name = "Lớp SE1821",
            JoinCode = "JOIN1821",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer1.Id,
            IsJoinEnabled = true
        };
        context.Users.AddRange(otherLecturer, otherDeptHead);
        context.Classes.Add(targetClass);
        await context.SaveChangesAsync();

        var handler = new ResetClassPasswordCommandHandler(context, _passwordHasher);

        var otherLecResult = await handler.Handle(
            new ResetClassPasswordCommand(targetClass.Id, otherLecturer.Id, "Pass1234!", ActorRole: UserRole.Lecturer),
            CancellationToken.None);
        otherLecResult.IsSuccess.Should().BeFalse();
        otherLecResult.Error.Should().StartWith("FORBIDDEN:");

        var otherHeadResult = await handler.Handle(
            new ResetClassPasswordCommand(targetClass.Id, otherDeptHead.Id, "Pass1234!", ActorRole: UserRole.DepartmentHead),
            CancellationToken.None);
        otherHeadResult.IsSuccess.Should().BeFalse();
        otherHeadResult.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "7.7. Chặn BOLA giữa 2 Trưởng bộ môn khác môn trên AssignLecturers, CreateClass, UpdateCourseConfig và CreateCourseClo")]
    public async Task DepartmentHead_BOLA_Across_Different_Courses_Should_Be_Blocked()
    {
        await using var context = CreateDbContext();
        var (semester, courseA, deptHeadA, lecturer, _) = SeedBaseData(context);
        courseA.DepartmentHeadUserId = deptHeadA.Id;

        var deptHeadB = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.b@fpt.edu.vn",
            FullName = "Department Head B",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.Users.Add(deptHeadB);
        await context.SaveChangesAsync();

        // 1. AssignLecturersToCourse bởi deptHeadB vào courseA của deptHeadA
        var assignLecturersHandler = new AssignLecturersToCourseCommandHandler(context, new AssignLecturersToCourseCommandValidator());
        var assignRes = await assignLecturersHandler.Handle(
            new AssignLecturersToCourseCommand(courseA.Id, new List<Guid> { lecturer.Id }, deptHeadB.Id, semester.Id, UserRole.DepartmentHead),
            CancellationToken.None);
        assignRes.IsSuccess.Should().BeFalse();
        assignRes.Error.Should().StartWith("FORBIDDEN:");

        // 2. CreateClass bởi deptHeadB vào courseA của deptHeadA
        var createClassHandler = new CreateClassCommandHandler(context, _passwordHasher, new CreateClassCommandValidator());
        var createClassRes = await createClassHandler.Handle(
            new CreateClassCommand("SE1890-BOLA", "Lớp BOLA", courseA.Id, semester.Id, deptHeadB.Id, ActorRole: $"department_head:{deptHeadB.Id}"),
            CancellationToken.None);
        createClassRes.IsSuccess.Should().BeFalse();
        createClassRes.Error.Should().StartWith("FORBIDDEN:");

        // 3. UpdateCourseConfiguration bởi deptHeadB vào courseA của deptHeadA
        var updateConfigHandler = new UpdateCourseConfigurationCommandHandler(context);
        var updateConfigRes = await updateConfigHandler.Handle(
            new UpdateCourseConfigurationCommand(courseA.Id, 60, 2, true) { ActorRole = $"department_head:{deptHeadB.Id}" },
            CancellationToken.None);
        updateConfigRes.IsSuccess.Should().BeFalse();
        updateConfigRes.Error.Should().StartWith("FORBIDDEN:");

        // 4. CreateCourseClo bởi deptHeadB vào courseA của deptHeadA
        var createCloHandler = new CreateCourseCloCommandHandler(context);
        var createCloRes = await createCloHandler.Handle(
            new CreateCourseCloCommand(courseA.Id, "CLO9", "Mô tả CLO BOLA", BloomLevel.Apply, 1, $"department_head:{deptHeadB.Id}"),
            CancellationToken.None);
        createCloRes.IsSuccess.Should().BeFalse();
        createCloRes.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "7.8. Chặn phân công giảng viên bị khóa (IsActive = false) trong AssignLecturersToCourse")]
    public async Task AssignLecturersToCourse_With_Inactive_Lecturer_Should_Fail()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, lecturer, _) = SeedBaseData(context);
        course.DepartmentHeadUserId = deptHead.Id;
        lecturer.IsActive = false;
        await context.SaveChangesAsync();

        var handler = new AssignLecturersToCourseCommandHandler(context, new AssignLecturersToCourseCommandValidator());
        var command = new AssignLecturersToCourseCommand(
            course.Id,
            new List<Guid> { lecturer.Id },
            deptHead.Id,
            semester.Id,
            UserRole.DepartmentHead);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đang bị khóa hoặc vô hiệu hóa");
    }

    [Fact(DisplayName = "7.9. Chặn sinh viên có IsActive = false tham gia lớp học và phân biệt JoinMethod INVITATION / PASSWORD")]
    public async Task JoinClass_With_Inactive_Student_Should_Fail_And_JoinMethod_Should_Reflect_Password()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer, student) = SeedBaseData(context);
        student.IsActive = false;

        var openClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1830",
            Name = "Lớp Mở Không Mật Khẩu",
            JoinCode = "OPEN1830",
            JoinPasswordHash = null,
            IsJoinEnabled = true,
            AllowSelfEnrollment = true,
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = 35
        };
        context.Classes.Add(openClass);
        await context.SaveChangesAsync();

        var handler = new JoinClassCommandHandler(context, _passwordHasher, new JoinClassCommandValidator());

        var inactiveResult = await handler.Handle(new JoinClassCommand(student.Id, "OPEN1830"), CancellationToken.None);
        inactiveResult.IsSuccess.Should().BeFalse();
        inactiveResult.Error.Should().StartWith("FORBIDDEN:");

        student.IsActive = true;
        await context.SaveChangesAsync();

        var activeResult = await handler.Handle(new JoinClassCommand(student.Id, "OPEN1830"), CancellationToken.None);
        activeResult.IsSuccess.Should().BeTrue();
        activeResult.Value.JoinMethod.Should().Be("INVITATION");
    }

    [Fact(DisplayName = "7.10. GetAcademicClassesQuery lọc chính xác theo LecturerId và trả về JoinCode, IsJoinEnabled, HasPassword")]
    public async Task GetAcademicClasses_Filter_By_LecturerId_Should_Return_Matching_Classes()
    {
        await using var context = CreateDbContext();
        var (semester, course, _, lecturer1, _) = SeedBaseData(context);

        var lecturer2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "lec2@fpt.edu.vn",
            FullName = "Lecturer Two",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        context.Users.Add(lecturer2);

        context.Classes.AddRange(
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "SE1841",
                Name = "Lớp GV 1",
                JoinCode = "JOIN1841",
                JoinPasswordHash = _passwordHasher.Hash("Pass1!"),
                IsJoinEnabled = true,
                CourseId = course.Id,
                SemesterId = semester.Id,
                LecturerId = lecturer1.Id,
                IsActive = true
            },
            new Class
            {
                Id = Guid.NewGuid(),
                Code = "SE1842",
                Name = "Lớp GV 2",
                JoinCode = "JOIN1842",
                JoinPasswordHash = null,
                IsJoinEnabled = false,
                CourseId = course.Id,
                SemesterId = semester.Id,
                LecturerId = lecturer2.Id,
                IsActive = true
            });
        await context.SaveChangesAsync();

        var handler = new GetAcademicClassesQueryHandler(context);
        var result = await handler.Handle(
            new GetAcademicClassesQuery(course.Id, semester.Id, true, lecturer1.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Code.Should().Be("SE1841");
        result.Value[0].JoinCode.Should().Be("JOIN1841");
        result.Value[0].HasPassword.Should().BeTrue();
        result.Value[0].IsJoinEnabled.Should().BeTrue();
    }

    #endregion
}

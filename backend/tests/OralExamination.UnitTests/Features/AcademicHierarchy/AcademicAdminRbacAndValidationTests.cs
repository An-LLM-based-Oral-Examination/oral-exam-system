using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignDepartmentHead;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.Courses.Commands.CreateCourse;
using OralExamination.Application.Features.Courses.Commands.UpdateCourse;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Semesters.Commands.CreateSemester;
using OralExamination.Application.Features.Semesters.Commands.UpdateSemester;
using OralExamination.Application.Features.Semesters.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public class AcademicAdminRbacAndValidationTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private static (Semester semester, Course course, User deptHead, User lecturer, User student, User admin) SeedAcademicBase(OralExamDbContext context)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
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

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@fpt.edu.vn",
            FullName = "Admin User",
            Role = UserRole.Admin,
            IsActive = true
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            DepartmentHeadUserId = deptHead.Id,
            IsActive = true,
            MaxMockExamsPerDay = 3,
            MockExamDurationMinutes = 30,
            MockExamTotalQuestions = 5
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(deptHead, lecturer, student, admin);
        context.Courses.Add(course);
        context.SaveChanges();

        return (semester, course, deptHead, lecturer, student, admin);
    }

    #region 1. Semester Command Tests

    [Fact(DisplayName = "1.1. CreateSemester thành công với mã chuẩn hóa in hoa và ngày hợp lệ")]
    public async Task CreateSemester_Success_With_Valid_Data()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new CreateSemesterCommandHandler(context);
        var command = new CreateSemesterCommand(
            admin.Id,
            "sp27",
            "Spring 2027",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 4, 30),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("SP27");
        result.Value.Name.Should().Be("Spring 2027");

        var created = await context.Semesters.FirstOrDefaultAsync(s => s.Code == "SP27");
        created.Should().NotBeNull();
        created!.StartDate.Should().Be(new DateOnly(2027, 1, 1));
        created.EndDate.Should().Be(new DateOnly(2027, 4, 30));

        // Audit log created
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "SEMESTER_CREATED" && a.EntityId == created.Id);
        audit.Should().NotBeNull();
    }

    [Fact(DisplayName = "1.2. CreateSemester thất bại khi mã hoặc tên để trống")]
    public async Task CreateSemester_Fails_When_Code_Or_Name_Empty()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new CreateSemesterCommandHandler(context);
        var command = new CreateSemesterCommand(
            admin.Id,
            "  ",
            "",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 4, 30),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("VALIDATION");
    }

    [Fact(DisplayName = "1.3. CreateSemester thất bại khi EndDate <= StartDate (ck_semesters_dates)")]
    public async Task CreateSemester_Fails_When_EndDate_Before_StartDate()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new CreateSemesterCommandHandler(context);

        // 1.3a: EndDate < StartDate
        var commandBefore = new CreateSemesterCommand(
            admin.Id,
            "SU27",
            "Summer 2027",
            new DateOnly(2027, 8, 31),
            new DateOnly(2027, 5, 1),
            true);

        var resultBefore = await handler.Handle(commandBefore, CancellationToken.None);
        resultBefore.IsSuccess.Should().BeFalse();
        resultBefore.Error.Should().Contain("VALIDATION: Ngày kết thúc học kỳ");

        // 1.3b: EndDate == StartDate (T4A-F006)
        var commandEqual = new CreateSemesterCommand(
            admin.Id,
            "SU27",
            "Summer 2027",
            new DateOnly(2027, 5, 1),
            new DateOnly(2027, 5, 1),
            true);

        var resultEqual = await handler.Handle(commandEqual, CancellationToken.None);
        resultEqual.IsSuccess.Should().BeFalse();
        resultEqual.Error.Should().Contain("VALIDATION: Ngày kết thúc học kỳ");
    }

    [Fact(DisplayName = "1.3c. UpdateSemester thất bại khi EndDate <= StartDate (T4A-F006)")]
    public async Task UpdateSemester_Fails_When_EndDate_Less_Than_Or_Equal_To_StartDate()
    {
        await using var context = CreateDbContext();
        var (semester, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new UpdateSemesterCommandHandler(context);
        var command = new UpdateSemesterCommand(
            admin.Id,
            semester.Id,
            semester.Code,
            semester.Name,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1), // Equal
            true);

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("VALIDATION: Ngày kết thúc học kỳ");
    }

    [Fact(DisplayName = "1.4. CreateSemester thất bại khi mã học kỳ đã tồn tại (không phân biệt hoa thường)")]
    public async Task CreateSemester_Fails_When_Duplicate_Code()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new CreateSemesterCommandHandler(context);
        var command = new CreateSemesterCommand(
            admin.Id,
            "fa26", // Already exists in seed
            "Duplicate Fall 2026",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 12, 31),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT");
    }

    [Fact(DisplayName = "1.5. UpdateSemester thành công khi dữ liệu hợp lệ")]
    public async Task UpdateSemester_Success_With_Valid_Data()
    {
        await using var context = CreateDbContext();
        var (semester, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new UpdateSemesterCommandHandler(context);
        var command = new UpdateSemesterCommand(
            admin.Id,
            semester.Id,
            "FA26_UPDATED",
            "Fall 2026 Revised",
            new DateOnly(2026, 9, 5),
            new DateOnly(2027, 1, 10),
            false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("FA26_UPDATED");
        result.Value.Name.Should().Be("Fall 2026 Revised");
        result.Value.IsActive.Should().BeFalse();

        var updated = await context.Semesters.FindAsync(semester.Id);
        updated!.Code.Should().Be("FA26_UPDATED");
        updated.IsActive.Should().BeFalse();
    }

    [Fact(DisplayName = "1.6. UpdateSemester thất bại khi không tìm thấy học kỳ")]
    public async Task UpdateSemester_Fails_When_NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, _, _, _, admin) = SeedAcademicBase(context);

        var handler = new UpdateSemesterCommandHandler(context);
        var command = new UpdateSemesterCommand(
            admin.Id,
            Guid.NewGuid(), // Non-existent ID
            "FA99",
            "Future 99",
            new DateOnly(2099, 1, 1),
            new DateOnly(2099, 5, 1),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NOT_FOUND");
    }

    [Fact(DisplayName = "1.7. UpdateSemester thất bại khi mã học kỳ trùng với học kỳ khác")]
    public async Task UpdateSemester_Fails_When_Duplicate_Code_With_Another_Semester()
    {
        await using var context = CreateDbContext();
        var (semester, _, _, _, _, admin) = SeedAcademicBase(context);

        // Add a second semester
        var second = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "SP26",
            Name = "Spring 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            IsActive = true
        };
        context.Semesters.Add(second);
        await context.SaveChangesAsync();

        var handler = new UpdateSemesterCommandHandler(context);
        // Attempt to rename second to FA26
        var command = new UpdateSemesterCommand(
            admin.Id,
            second.Id,
            "fa26",
            "Spring 2026",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("CONFLICT");
    }

    #endregion

    #region 2. Course Command Tests

    [Fact(DisplayName = "2.1. CreateCourse thành công với mã in hoa, số tín chỉ hợp lệ và Trưởng bộ môn")]
    public async Task CreateCourse_Success_With_DepartmentHead()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new CreateCourseCommandValidator();
        var handler = new CreateCourseCommandHandler(context, validator);

        var command = new CreateCourseCommand(
            "swd392",
            "Software Architecture and Design",
            3,
            "Advanced software design patterns",
            deptHead.Id,
            3,
            30,
            5,
            true,
            semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("SWD392");
        result.Value.DepartmentHeadUserId.Should().Be(deptHead.Id);

        var created = await context.Courses.FirstOrDefaultAsync(c => c.Code == "SWD392");
        created.Should().NotBeNull();
        created!.Credits.Should().Be(3);
    }

    [Fact(DisplayName = "2.2. CreateCourse thất bại khi số tín chỉ <= 0")]
    public async Task CreateCourse_Fails_When_Credits_Zero_Or_Negative()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new CreateCourseCommandValidator();
        var handler = new CreateCourseCommandHandler(context, validator);

        var command = new CreateCourseCommand(
            "SWR302",
            "Software Requirements",
            0, // Invalid: credits must be >= 1
            "Requirement engineering",
            deptHead.Id,
            3,
            30,
            5,
            true,
            semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Số tín chỉ (Credits) phải nằm trong khoảng từ 1 đến 10");
    }

    [Fact(DisplayName = "2.3. CreateCourse thất bại khi mã môn đã tồn tại")]
    public async Task CreateCourse_Fails_When_Duplicate_Code()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new CreateCourseCommandValidator();
        var handler = new CreateCourseCommandHandler(context, validator);

        var command = new CreateCourseCommand(
            "prn231", // Already exists in seed
            "Duplicate PRN231",
            3,
            null,
            deptHead.Id,
            3,
            30,
            5,
            true,
            semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã tồn tại trong hệ thống");
    }

    [Fact(DisplayName = "2.4. CreateCourse thất bại khi Trưởng bộ môn bị khóa tài khoản")]
    public async Task CreateCourse_Fails_When_DepartmentHead_Inactive()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _, _) = SeedAcademicBase(context);

        // Lock deptHead
        deptHead.IsActive = false;
        await context.SaveChangesAsync();

        var validator = new CreateCourseCommandValidator();
        var handler = new CreateCourseCommandHandler(context, validator);

        var command = new CreateCourseCommand(
            "PRJ301",
            "Java Web Development",
            3,
            null,
            deptHead.Id,
            3,
            30,
            5,
            true,
            semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Tài khoản Trưởng bộ môn đang bị khóa");
    }

    [Fact(DisplayName = "2.5. UpdateCourse thành công khi cập nhật hợp lệ")]
    public async Task UpdateCourse_Success_With_Valid_Data()
    {
        await using var context = CreateDbContext();
        var (semester, course, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new UpdateCourseCommandValidator();
        var handler = new UpdateCourseCommandHandler(context, validator);

        var command = new UpdateCourseCommand(
            course.Id,
            "PRN231_REV",
            "Updated PRN231 Name",
            4,
            "Updated Description",
            deptHead.Id,
            4,
            45,
            6,
            true,
            semester.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("PRN231_REV");
        result.Value.Credits.Should().Be(4);
        result.Value.MaxMockExamsPerDay.Should().Be(4);
    }

    [Fact(DisplayName = "2.6. UpdateCourse thất bại khi không tìm thấy môn học")]
    public async Task UpdateCourse_Fails_When_NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new UpdateCourseCommandValidator();
        var handler = new UpdateCourseCommandHandler(context, validator);

        var command = new UpdateCourseCommand(
            Guid.NewGuid(),
            "NOT_EXIST",
            "Non Existent",
            3,
            null,
            deptHead.Id,
            3,
            30,
            5,
            true,
            null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy môn học");
    }

    [Fact(DisplayName = "2.7. Vòng đời môn học Active -> Inactive -> Active và kiểm tra DepartmentHeadName (T4A-F003, T4A-F004)")]
    public async Task Course_Lifecycle_Active_Inactive_Active_And_DepartmentHeadName()
    {
        await using var context = CreateDbContext();
        var (semester, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var createValidator = new CreateCourseCommandValidator();
        var createHandler = new CreateCourseCommandHandler(context, createValidator);
        var updateValidator = new UpdateCourseCommandValidator();
        var updateHandler = new UpdateCourseCommandHandler(context, updateValidator);
        var getCoursesHandler = new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQueryHandler(context);

        // 1. Create active course
        var createCmd = new CreateCourseCommand(
            "PRN234",
            "Advanced Cloud Architecture",
            3,
            "Cloud Native Patterns",
            deptHead.Id,
            3,
            30,
            5,
            true, // Active
            semester.Id);

        var createRes = await createHandler.Handle(createCmd, CancellationToken.None);
        createRes.IsSuccess.Should().BeTrue();
        createRes.Value!.DepartmentHeadName.Should().Be("Department Head User", "T4A-F004: CreateCourse must project DepartmentHeadName");

        var courseId = createRes.Value.Id;

        // Query active courses: both includeInactive=true and includeInactive=false return it
        var listPublic1 = await getCoursesHandler.Handle(new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQuery(false), CancellationToken.None);
        listPublic1.Value!.Should().Contain(c => c.Id == courseId);
        listPublic1.Value!.First(c => c.Id == courseId).DepartmentHeadName.Should().Be("Department Head User");

        // 2. Update to Inactive (active -> inactive)
        var updateToInactiveCmd = new UpdateCourseCommand(
            courseId,
            "PRN234",
            "Advanced Cloud Architecture",
            3,
            "Cloud Native Patterns",
            deptHead.Id,
            3,
            30,
            5,
            false, // Inactive
            semester.Id);

        var updateRes1 = await updateHandler.Handle(updateToInactiveCmd, CancellationToken.None);
        updateRes1.IsSuccess.Should().BeTrue();
        updateRes1.Value!.IsActive.Should().BeFalse();
        updateRes1.Value.DepartmentHeadName.Should().Be("Department Head User");

        // Query public (includeInactive=false) MUST HIDE the inactive course
        var listPublic2 = await getCoursesHandler.Handle(new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQuery(false), CancellationToken.None);
        listPublic2.Value!.Should().NotContain(c => c.Id == courseId, "T4A-F003: Public/student query must NOT return inactive courses");

        // Query admin (includeInactive=true) MUST RETURN the inactive course
        var listAdmin = await getCoursesHandler.Handle(new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQuery(true), CancellationToken.None);
        listAdmin.Value!.Should().Contain(c => c.Id == courseId, "T4A-F003: Admin query with includeInactive=true must return inactive courses");

        // 3. Update back to Active (inactive -> active)
        var updateToActiveCmd = new UpdateCourseCommand(
            courseId,
            "PRN234",
            "Advanced Cloud Architecture",
            3,
            "Cloud Native Patterns",
            deptHead.Id,
            3,
            30,
            5,
            true, // Active again
            semester.Id);

        var updateRes2 = await updateHandler.Handle(updateToActiveCmd, CancellationToken.None);
        updateRes2.IsSuccess.Should().BeTrue();
        updateRes2.Value!.IsActive.Should().BeTrue();

        // Query public (includeInactive=false) MUST SHOW the course again
        var listPublic3 = await getCoursesHandler.Handle(new OralExamination.Application.Features.Courses.Queries.GetCourses.GetCoursesQuery(false), CancellationToken.None);
        listPublic3.Value!.Should().Contain(c => c.Id == courseId, "T4A-F003: Once reactivated, course must appear in public query again");
    }

    #endregion

    #region 3. Assign Department Head Command Tests

    [Fact(DisplayName = "3.1. AssignDepartmentHead thành công khi User hợp lệ và có role department_head")]
    public async Task AssignDepartmentHead_Success_With_Valid_User()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, _, _, _) = SeedAcademicBase(context);

        // Create a new active department head
        var newHead = new User
        {
            Id = Guid.NewGuid(),
            Email = "newhead@fpt.edu.vn",
            FullName = "New Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.Users.Add(newHead);
        await context.SaveChangesAsync();

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(course.Id, newHead.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DepartmentHeadUserId.Should().Be(newHead.Id);
        result.Value.DepartmentHeadName.Should().Be("New Head");

        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.DepartmentHeadUserId.Should().Be(newHead.Id);
    }

    [Fact(DisplayName = "3.2. AssignDepartmentHead thất bại khi User có role không phải department_head (admin, student, lecturer, proctor, examination_officer) (T4A-F001)")]
    public async Task AssignDepartmentHead_Fails_When_User_Not_DepartmentHead()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, lecturer, student, admin) = SeedAcademicBase(context);

        var proctor = new User
        {
            Id = Guid.NewGuid(),
            Email = "proctor@fpt.edu.vn",
            FullName = "Proctor User",
            Role = UserRole.Proctor,
            IsActive = true
        };
        var officer = new User
        {
            Id = Guid.NewGuid(),
            Email = "officer@fpt.edu.vn",
            FullName = "Examination Officer User",
            Role = UserRole.ExaminationOfficer,
            IsActive = true
        };
        context.Users.AddRange(proctor, officer);
        await context.SaveChangesAsync();

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var nonDeptHeadUsers = new[] { admin, student, lecturer, proctor, officer };
        foreach (var nonDeptHead in nonDeptHeadUsers)
        {
            var cmd = new AssignDepartmentHeadCommand(course.Id, nonDeptHead.Id, "admin");
            var res = await handler.Handle(cmd, CancellationToken.None);

            res.IsSuccess.Should().BeFalse($"role '{nonDeptHead.Role}' must not be assigned as Department Head");
            res.Error.Should().Contain("không có vai trò Trưởng bộ môn");
        }
    }

    [Fact(DisplayName = "3.2b. AssignDepartmentHead thất bại khi Actor không có role admin (T4A-F001)")]
    public async Task AssignDepartmentHead_Fails_When_Actor_Not_Admin()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var nonAdminRoles = new[] { "student", "lecturer", "department_head", "proctor", "examination_officer", null };
        foreach (var role in nonAdminRoles)
        {
            var cmd = new AssignDepartmentHeadCommand(course.Id, deptHead.Id, role);
            var res = await handler.Handle(cmd, CancellationToken.None);

            res.IsSuccess.Should().BeFalse($"actor role '{role}' must be rejected with FORBIDDEN");
            res.Error.Should().Contain("FORBIDDEN");
        }
    }

    [Fact(DisplayName = "3.3. AssignDepartmentHead thất bại khi User bị khóa tài khoản")]
    public async Task AssignDepartmentHead_Fails_When_User_Inactive()
    {
        await using var context = CreateDbContext();
        var (_, course, deptHead, _, _, _) = SeedAcademicBase(context);

        deptHead.IsActive = false;
        await context.SaveChangesAsync();

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(course.Id, deptHead.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Tài khoản Trưởng bộ môn đang bị khóa hoặc vô hiệu hóa");
    }

    [Fact(DisplayName = "3.4. AssignDepartmentHead thất bại khi môn học không tồn tại")]
    public async Task AssignDepartmentHead_Fails_When_Course_NotFound()
    {
        await using var context = CreateDbContext();
        var (_, _, deptHead, _, _, _) = SeedAcademicBase(context);

        var validator = new AssignDepartmentHeadCommandValidator();
        var handler = new AssignDepartmentHeadCommandHandler(context, validator);

        var command = new AssignDepartmentHeadCommand(Guid.NewGuid(), deptHead.Id, "admin");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không tìm thấy môn học");
    }

    #endregion

    #region 4. Negative RBAC Attribute & Route Verification

    [Fact(DisplayName = "4.1. SemestersController CreateSemester và UpdateSemester có Authorize(Roles = 'admin')")]
    public void SemestersController_Actions_Must_Be_Restricted_To_Admin()
    {
        var controllerType = typeof(SemestersController);

        var createMethod = controllerType.GetMethod(nameof(SemestersController.CreateSemester));
        createMethod.Should().NotBeNull();
        var createAuth = createMethod!.GetCustomAttribute<AuthorizeAttribute>();
        createAuth.Should().NotBeNull();
        createAuth!.Roles.Should().Be("admin");

        var updateMethod = controllerType.GetMethod(nameof(SemestersController.UpdateSemester));
        updateMethod.Should().NotBeNull();
        var updateAuth = updateMethod!.GetCustomAttribute<AuthorizeAttribute>();
        updateAuth.Should().NotBeNull();
        updateAuth!.Roles.Should().Be("admin");
    }

    [Fact(DisplayName = "4.2. CoursesController CreateCourse, UpdateCourse và AssignDepartmentHead có Authorize(Roles = 'admin')")]
    public void CoursesController_AdminActions_Must_Be_Restricted_To_Admin()
    {
        var controllerType = typeof(CoursesController);

        var createMethod = controllerType.GetMethod(nameof(CoursesController.CreateCourse));
        createMethod.Should().NotBeNull();
        var createAuth = createMethod!.GetCustomAttribute<AuthorizeAttribute>();
        createAuth.Should().NotBeNull();
        createAuth!.Roles.Should().Be("admin");

        var updateMethod = controllerType.GetMethod(nameof(CoursesController.UpdateCourse));
        updateMethod.Should().NotBeNull();
        var updateAuth = updateMethod!.GetCustomAttribute<AuthorizeAttribute>();
        updateAuth.Should().NotBeNull();
        updateAuth!.Roles.Should().Be("admin");

        var assignMethod = controllerType.GetMethod(nameof(CoursesController.AssignDepartmentHead));
        assignMethod.Should().NotBeNull();
        var assignAuth = assignMethod!.GetCustomAttribute<AuthorizeAttribute>();
        assignAuth.Should().NotBeNull();
        assignAuth!.Roles.Should().Be("admin");
    }

    [Fact(DisplayName = "4.3. SemestersController TUYỆT ĐỐI KHÔNG có phương thức DELETE (Cấm xóa học kỳ)")]
    public void SemestersController_Must_Not_Have_Delete_Action()
    {
        var controllerType = typeof(SemestersController);
        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var deleteMethods = methods.Where(m =>
            m.GetCustomAttributes<HttpDeleteAttribute>().Any() ||
            m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));

        deleteMethods.Should().BeEmpty("Học kỳ là dữ liệu nền tảng bất biến trong PostgreSQL, cấm xóa (ON DELETE RESTRICT)");
    }

    #endregion
}

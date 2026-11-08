using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy.Commands.AssignClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Commands.DropClassStudent;
using OralExamination.Application.Features.AcademicHierarchy.Queries.LookupClassStudent;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public class ClassRosterCommandTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static (Class targetClass, User lecturer, User student) Seed(OralExamDbContext context, int maxStudents = 2, bool joinEnabled = false)
    {
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            IsActive = true,
        };
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Nguyen Van A",
            Role = UserRole.Lecturer,
            IsActive = true,
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Tran Thi B",
            StudentCode = "SE181863",
            Role = UserRole.Student,
            IsActive = true,
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CSD201",
            Name = "Cấu trúc dữ liệu",
            SemesterId = semester.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var targetClass = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1802",
            Name = "SE1802",
            JoinCode = "SE1802",
            CourseId = course.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            MaxStudents = maxStudents,
            IsJoinEnabled = joinEnabled,
            AllowSelfEnrollment = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.Semesters.Add(semester);
        context.Users.AddRange(lecturer, student);
        context.Courses.Add(course);
        context.Classes.Add(targetClass);
        context.SaveChanges();
        return (targetClass, lecturer, student);
    }

    [Fact]
    public async Task Lookup_ExactCode_ReturnsOneStudent()
    {
        using var context = CreateDbContext();
        Seed(context);
        var result = await new LookupClassStudentQueryHandler(context)
            .Handle(new LookupClassStudentQuery("se181863"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StudentCode.Should().Be("SE181863");
        result.Value.FullName.Should().Be("Tran Thi B");
    }

    [Fact]
    public async Task Lookup_PartialName_DoesNotSearch()
    {
        using var context = CreateDbContext();
        Seed(context);
        var result = await new LookupClassStudentQueryHandler(context)
            .Handle(new LookupClassStudentQuery("Tran"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Nhập đủ mã sinh viên hoặc email");
    }

    [Fact]
    public async Task Assign_WhenJoinLocked_AddsStudent()
    {
        using var context = CreateDbContext();
        var (targetClass, lecturer, student) = Seed(context, joinEnabled: false);

        var result = await new AssignClassStudentCommandHandler(context).Handle(
            new AssignClassStudentCommand(targetClass.Id, student.Id, lecturer.Id, UserRole.Lecturer),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.JoinMethod.Should().Be("assigned");
        var stored = await context.ClassEnrollments.SingleAsync();
        stored.Status.Should().Be("enrolled");
    }

    [Fact]
    public async Task Assign_WhenAlreadyEnrolled_Conflicts()
    {
        using var context = CreateDbContext();
        var (targetClass, lecturer, student) = Seed(context);
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            ClassId = targetClass.Id,
            StudentId = student.Id,
            Status = "enrolled",
            JoinMethod = "password",
        });
        await context.SaveChangesAsync();

        var result = await new AssignClassStudentCommandHandler(context).Handle(
            new AssignClassStudentCommand(targetClass.Id, student.Id, lecturer.Id, UserRole.Lecturer),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã có trong lớp");
    }

    [Fact]
    public async Task Drop_ThenAssign_ReusesEnrollment()
    {
        using var context = CreateDbContext();
        var (targetClass, lecturer, student) = Seed(context);
        var enrollment = new ClassEnrollment
        {
            Id = Guid.NewGuid(),
            ClassId = targetClass.Id,
            StudentId = student.Id,
            Status = "enrolled",
            JoinMethod = "password",
        };
        context.ClassEnrollments.Add(enrollment);
        await context.SaveChangesAsync();

        var dropped = await new DropClassStudentCommandHandler(context).Handle(
            new DropClassStudentCommand(targetClass.Id, student.Id, lecturer.Id, UserRole.Lecturer),
            default);
        dropped.IsSuccess.Should().BeTrue();

        var added = await new AssignClassStudentCommandHandler(context).Handle(
            new AssignClassStudentCommand(targetClass.Id, student.Id, lecturer.Id, UserRole.Lecturer),
            default);

        added.IsSuccess.Should().BeTrue();
        var rows = await context.ClassEnrollments.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(enrollment.Id);
        rows[0].Status.Should().Be("enrolled");
        rows[0].JoinMethod.Should().Be("assigned");
    }
}

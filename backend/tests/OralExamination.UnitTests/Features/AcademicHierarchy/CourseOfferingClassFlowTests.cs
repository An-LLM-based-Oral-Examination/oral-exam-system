using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy.Commands.CreateClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Authentication;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public sealed class CourseOfferingClassFlowTests
{
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();

    [Fact]
    public async Task DepartmentHead_Should_Sync_Five_Lecturers_Then_Disable_Omitted_Assignment()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedAsync(context, 5);
        var handler = SyncHandler(context);

        var first = await handler.Handle(new(fixture.Head.Id, fixture.Offering.Id,
            fixture.Lecturers.Select(x => x.Id).ToArray()), default);
        var second = await handler.Handle(new(fixture.Head.Id, fixture.Offering.Id,
            fixture.Lecturers.Take(4).Select(x => x.Id).ToArray()), default);

        first.IsSuccess.Should().BeTrue();
        first.Value.AssignedCount.Should().Be(5);
        second.IsSuccess.Should().BeTrue();
        second.Value.AssignedCount.Should().Be(4);
        second.Value.DisabledCount.Should().Be(1);
        context.CourseLecturers.Should().HaveCount(5);
        context.CourseLecturers.Count(x => x.IsActive).Should().Be(4);
        context.CourseLecturers.Should().OnlyContain(x => x.CourseOfferingId == fixture.Offering.Id);
    }

    [Fact]
    public async Task Other_Program_Head_Should_Not_Sync_Lecturers()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedAsync(context, 1);
        var otherHead = User(UserRole.DepartmentHead, "other-head@fpt.edu.vn");
        context.Users.Add(otherHead);
        await context.SaveChangesAsync();

        var result = await SyncHandler(context).Handle(new(otherHead.Id, fixture.Offering.Id,
            new[] { fixture.Lecturers[0].Id }), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
        context.CourseLecturers.Should().BeEmpty();
    }

    [Fact]
    public async Task Assigned_Lecturer_Should_See_Offering_And_Create_Hashed_Password_Class()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedAsync(context, 1);
        await AssignAsync(context, fixture, fixture.Lecturers[0]);

        var offerings = await new GetLecturerCourseOfferingsQueryHandler(context)
            .Handle(new(fixture.Lecturers[0].Id), default);
        var created = await CreateAsync(context, fixture, fixture.Lecturers[0]);

        offerings.Value.Should().ContainSingle(x => x.CourseOfferingId == fixture.Offering.Id);
        created.IsSuccess.Should().BeTrue();
        created.Value.CourseOfferingId.Should().Be(fixture.Offering.Id);
        var saved = await context.Classes.SingleAsync();
        saved.JoinPasswordHash.Should().NotBe(Password);
        _passwordHasher.Verify(Password, saved.JoinPasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task Unassigned_Lecturer_Should_Not_Create_Offering_Class()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedAsync(context, 2);
        await AssignAsync(context, fixture, fixture.Lecturers[0]);

        var result = await CreateAsync(context, fixture, fixture.Lecturers[1]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
        context.Classes.Should().BeEmpty();
    }

    [Fact]
    public async Task Join_Should_Require_Password_And_Block_Duplicate_Or_Disabled_Offering()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedAsync(context, 1);
        await AssignAsync(context, fixture, fixture.Lecturers[0]);
        var created = await CreateAsync(context, fixture, fixture.Lecturers[0]);
        var handler = new JoinClassCommandHandler(context, _passwordHasher, new JoinClassCommandValidator());

        var wrong = await handler.Handle(new(fixture.Student.Id, created.Value.JoinCode, "wrong-password"), default);
        var joined = await handler.Handle(new(fixture.Student.Id, created.Value.JoinCode, Password), default);
        var duplicate = await handler.Handle(new(fixture.Student.Id, created.Value.JoinCode, Password), default);

        wrong.IsSuccess.Should().BeFalse();
        joined.IsSuccess.Should().BeTrue();
        joined.Value.CourseOfferingId.Should().Be(fixture.Offering.Id);
        duplicate.IsSuccess.Should().BeFalse();
        context.ClassEnrollments.Should().ContainSingle();

        var secondStudent = User(UserRole.Student, "second-student@fpt.edu.vn");
        context.Users.Add(secondStudent);
        fixture.Offering.IsEnabled = false;
        await context.SaveChangesAsync();
        var disabled = await handler.Handle(new(secondStudent.Id, created.Value.JoinCode, Password), default);
        disabled.IsSuccess.Should().BeFalse();
        disabled.Error.Should().StartWith("JOIN_DISABLED:");
    }

    private const string Password = "FixedPassword123!";

    private async Task<OralExamination.Application.Common.Models.Result<
        OralExamination.Application.Features.AcademicHierarchy.DTOs.CreateClassResponseDto>> CreateAsync(
        OralExamDbContext context, Fixture fixture, User lecturer)
    {
        var handler = new CreateClassCommandHandler(context, _passwordHasher, new CreateClassCommandValidator());
        return await handler.Handle(new(
            "CSD201-SE01", "CSD201 Class SE01", Guid.Empty, Guid.Empty, lecturer.Id,
            "CSD201S1", Password, ActorRole: UserRole.Lecturer,
            CourseOfferingId: fixture.Offering.Id, ActorUserId: lecturer.Id), default);
    }

    private static async Task AssignAsync(OralExamDbContext context, Fixture fixture, User lecturer)
    {
        var result = await SyncHandler(context).Handle(new(fixture.Head.Id, fixture.Offering.Id,
            new[] { lecturer.Id }), default);
        result.IsSuccess.Should().BeTrue();
    }

    private static SyncCourseOfferingLecturersCommandHandler SyncHandler(OralExamDbContext context) =>
        new(context, new SyncCourseOfferingLecturersCommandValidator());

    private static async Task<Fixture> SeedAsync(OralExamDbContext context, int lecturerCount)
    {
        var head = User(UserRole.DepartmentHead, "head.se@fpt.edu.vn");
        var program = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "Software Engineering", DepartmentHeadUserId = head.Id, IsActive = true };
        var semester = new Semester { Id = Guid.NewGuid(), Code = "SP27", Name = "Spring 2027", StartDate = new(2027, 1, 1), EndDate = new(2027, 4, 30), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "CSD201", Name = "Data Structures and Algorithms", AcademicProgramId = program.Id, DepartmentHeadUserId = head.Id, SemesterId = semester.Id, IsActive = true };
        var offering = new CourseOffering { Id = Guid.NewGuid(), CourseId = course.Id, SemesterId = semester.Id, IsEnabled = true };
        var lecturers = Enumerable.Range(1, lecturerCount).Select(i => User(UserRole.Lecturer, $"lecturer{i}@fpt.edu.vn")).ToArray();
        var student = User(UserRole.Student, "student@fpt.edu.vn");
        context.AddRange(head, program, semester, course, offering, student);
        context.Users.AddRange(lecturers);
        await context.SaveChangesAsync();
        return new(head, offering, lecturers, student);
    }

    private static User User(string role, string email) => new() { Id = Guid.NewGuid(), Email = email, FullName = email, Role = role, IsActive = true };

    private static OralExamDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record Fixture(User Head, CourseOffering Offering, User[] Lecturers, User Student);
}

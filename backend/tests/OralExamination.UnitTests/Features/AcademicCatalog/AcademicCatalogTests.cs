using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;
using OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;
using OralExamination.Application.Features.AcademicCatalog.Queries.GetSemesterCourseCatalog;
using OralExamination.Application.Features.Semesters.Commands.CreateSemester;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicCatalog;

public sealed class AcademicCatalogTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public async Task Create_Semester_Should_Copy_All_Active_Catalog_Courses_As_Selected()
    {
        await using var context = CreateDbContext();
        var program = NewProgram();
        context.AddRange(program, NewCourse("CSD201", program.Id), NewCourse("PRN231", program.Id));
        await context.SaveChangesAsync();

        var createResult = await new CreateSemesterCommandHandler(context).Handle(
            new CreateSemesterCommand(
                Guid.NewGuid(),
                "SP27",
                "Spring 2027",
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 4, 30)),
            CancellationToken.None);

        var handler = new GetSemesterCourseCatalogQueryHandler(context);
        var result = await handler.Handle(
            new GetSemesterCourseCatalogQuery(createResult.Value.Id),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.CoursesCount.Should().Be(2);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(course => course.IsSelected && course.CourseOfferingId != null);
    }

    [Fact]
    public async Task Sync_Should_Create_Selected_And_Disable_Unselected_Without_Deleting()
    {
        await using var context = CreateDbContext();
        var program = NewProgram();
        var semester = NewSemester("SP27");
        var csd = NewCourse("CSD201", program.Id);
        var prn = NewCourse("PRN231", program.Id);
        context.AddRange(program, semester, csd, prn);
        await context.SaveChangesAsync();

        var handler = new SyncSemesterCourseOfferingsCommandHandler(
            context,
            new SyncSemesterCourseOfferingsCommandValidator());

        var first = await handler.Handle(
            new SyncSemesterCourseOfferingsCommand(Guid.NewGuid(), semester.Id, new[] { csd.Id, prn.Id }),
            CancellationToken.None);
        var second = await handler.Handle(
            new SyncSemesterCourseOfferingsCommand(Guid.NewGuid(), semester.Id, new[] { csd.Id }),
            CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        first.Value.CreatedCount.Should().Be(2);
        second.IsSuccess.Should().BeTrue();
        second.Value.DisabledCount.Should().Be(1);

        var offerings = await context.CourseOfferings
            .Where(o => o.SemesterId == semester.Id)
            .OrderBy(o => o.CourseId)
            .ToListAsync();
        offerings.Should().HaveCount(2);
        offerings.Single(o => o.CourseId == csd.Id).IsEnabled.Should().BeTrue();
        offerings.Single(o => o.CourseId == prn.Id).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Sync_Should_Reject_Unknown_Or_Inactive_Course()
    {
        await using var context = CreateDbContext();
        var semester = NewSemester("SP27");
        context.Semesters.Add(semester);
        await context.SaveChangesAsync();

        var handler = new SyncSemesterCourseOfferingsCommandHandler(
            context,
            new SyncSemesterCourseOfferingsCommandValidator());
        var result = await handler.Handle(
            new SyncSemesterCourseOfferingsCommand(Guid.NewGuid(), semester.Id, new[] { Guid.NewGuid() }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("VALIDATION:");
        context.CourseOfferings.Should().BeEmpty();
    }

    [Fact]
    public async Task Assign_Program_Head_Should_Propagate_To_Legacy_Courses()
    {
        await using var context = CreateDbContext();
        var program = NewProgram();
        var course = NewCourse("CSD201", program.Id);
        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.se@fpt.edu.vn",
            FullName = "SE Department Head",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        context.AddRange(program, course, head);
        await context.SaveChangesAsync();

        var handler = new AssignProgramDepartmentHeadCommandHandler(
            context,
            new AssignProgramDepartmentHeadCommandValidator());
        var result = await handler.Handle(
            new AssignProgramDepartmentHeadCommand(Guid.NewGuid(), program.Id, head.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DepartmentHeadUserId.Should().Be(head.Id);
        (await context.Courses.FindAsync(course.Id))!.DepartmentHeadUserId.Should().Be(head.Id);
    }

    private static AcademicProgram NewProgram() => new()
    {
        Id = Guid.NewGuid(),
        Code = "SE",
        Name = "Software Engineering",
        IsActive = true
    };

    private static Semester NewSemester(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        StartDate = new DateOnly(2027, 1, 1),
        EndDate = new DateOnly(2027, 4, 30),
        IsActive = true
    };

    private static Course NewCourse(string code, Guid programId) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        AcademicProgramId = programId,
        SemesterId = Guid.NewGuid(),
        IsActive = true
    };
}

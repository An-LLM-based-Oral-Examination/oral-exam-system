using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetLecturerCourseOfferings;
using OralExamination.Application.Features.AcademicHierarchy.Queries.GetCourseOfferingLecturers;
using OralExamination.Application.Features.AcademicHierarchy.Queries.SearchOfferingLecturers;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public sealed class Task23LecturerAssignmentTests
{
    [Fact]
    public async Task Sync_ReusesAssignmentFromDuplicateOfferingInsteadOfInsertingSameCourseSemesterLecturer()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        var duplicate = new CourseOffering { Course = offering.Course, Semester = offering.Semester, IsEnabled = true };
        db.CourseOfferings.Add(duplicate);
        var assignment = new CourseLecturer
        {
            CourseId = offering.CourseId, SemesterId = offering.SemesterId,
            CourseOfferingId = duplicate.Id, LecturerUserId = lecturer.Id,
            AssignedByUserId = head.Id, IsActive = false
        };
        db.CourseLecturers.Add(assignment);
        await db.SaveChangesAsync();

        (await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default)).IsSuccess.Should().BeTrue();
        db.CourseLecturers.Should().ContainSingle();
        assignment.CourseOfferingId.Should().Be(offering.Id);
        assignment.IsActive.Should().BeTrue();
        (await Sync(db).Handle(new(head.Id, offering.Id, []), default)).IsSuccess.Should().BeTrue();
        assignment.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Sync_AllowsLegacyCourseOwnerWhenProgramHasNoOwner()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        offering.Course.AcademicProgram!.DepartmentHeadUserId = null;
        await db.SaveChangesAsync();
        var result = await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HeadOfferings_ProgramOwnerTakesPrecedenceOverLegacyOwner()
    {
        await using var db = Context();
        var (head, offering, _) = await Seed(db);
        offering.Course.AcademicProgram!.DepartmentHeadUserId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var result = await new GetLecturerCourseOfferingsQueryHandler(db).Handle(new(head.Id), default);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HeadOfferings_RejectsInactiveActor()
    {
        await using var db = Context();
        var (head, _, _) = await Seed(db);
        head.IsActive = false;
        await db.SaveChangesAsync();
        var result = await new GetLecturerCourseOfferingsQueryHandler(db).Handle(new(head.Id), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task Sync_DuplicateAndRetryDoNotCreateDuplicateAssignments_AndRemovalIsAudited()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        (await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id, lecturer.Id]), default)).IsSuccess.Should().BeTrue();
        (await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default)).IsSuccess.Should().BeTrue();
        var removed = await Sync(db).Handle(new(head.Id, offering.Id, []), default);
        removed.IsSuccess.Should().BeTrue();
        removed.Value.DisabledCount.Should().Be(1);
        db.CourseLecturers.Should().ContainSingle().Which.IsActive.Should().BeFalse();
        db.AuditLogs.Should().Contain(x => x.Action == "COURSE_OFFERING_LECTURERS_SYNCED" && x.UserId == head.Id && x.OldValues != null);
    }

    [Fact]
    public async Task Sync_RejectsInactiveLecturerWithoutChangingAssignments()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        lecturer.IsActive = false;
        await db.SaveChangesAsync();
        var result = await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default);
        result.IsSuccess.Should().BeFalse();
        db.CourseLecturers.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminCannotBypassDepartmentHeadOnlyAssignmentInHandler()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        head.Role = UserRole.Admin;
        await db.SaveChangesAsync();
        (await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default)).Error.Should().StartWith("FORBIDDEN:");
        var legacy = new OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse.AssignLecturersToCourseCommandHandler(db,
            new OralExamination.Application.Features.AcademicHierarchy.Commands.AssignLecturersToCourse.AssignLecturersToCourseCommandValidator());
        var result = await legacy.Handle(new(offering.CourseId, [lecturer.Id], head.Id, offering.SemesterId, UserRole.Admin), default);
        result.Error.Should().StartWith("FORBIDDEN:");
        db.CourseLecturers.Should().BeEmpty();
    }

    private static SyncCourseOfferingLecturersCommandHandler Sync(OralExamDbContext db) => new(db, new SyncCourseOfferingLecturersCommandValidator());

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Lecturer)]
    [InlineData(UserRole.Proctor)]
    [InlineData(UserRole.ExaminationOfficer)]
    [InlineData(UserRole.Admin)]
    public async Task Directory_OnlyOwningDepartmentHeadCanSearch(string role)
    {
        await using var db = Context();
        var (head, offering, _) = await Seed(db);
        head.Role = role;
        await db.SaveChangesAsync();
        var result = await new SearchOfferingLecturersQueryHandler(db).Handle(new(head.Id, offering.Id, "lecturer"), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task Directory_IsBoundedActiveLecturersOnly_AndRejectsUnownedCourse()
    {
        await using var db = Context();
        var (head, offering, _) = await Seed(db);
        db.Users.AddRange(Enumerable.Range(0, 25).Select(i => new User { Email = $"match{i}@example.test", FullName = $"Match {i:D2}", Role = UserRole.Lecturer, IsActive = true }));
        db.Users.Add(new User { Email = "inactive@example.test", FullName = "Match inactive", Role = UserRole.Lecturer, IsActive = false });
        db.Users.Add(new User { Email = "student@example.test", FullName = "Match student", Role = UserRole.Student, IsActive = true });
        await db.SaveChangesAsync();
        var handler = new SearchOfferingLecturersQueryHandler(db);
        var result = await handler.Handle(new(head.Id, offering.Id, "match"), default);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(20);
        result.Value.Should().NotContain(x => x.Email == "inactive@example.test" || x.Email == "student@example.test");
        (await handler.Handle(new(head.Id, offering.Id, "m"), default)).IsSuccess.Should().BeFalse();
        offering.Course.AcademicProgram!.DepartmentHeadUserId = null;
        offering.Course.DepartmentHeadUserId = null;
        await db.SaveChangesAsync();
        (await handler.Handle(new(head.Id, offering.Id, "match"), default)).Error.Should().StartWith("FORBIDDEN:");
        (await Sync(db).Handle(new(head.Id, offering.Id, []), default)).Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact]
    public async Task AssignmentRead_RequiresOwnerOrAssignedLecturer_AndStudentCannotReadEmails()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        var handler = new GetCourseOfferingLecturersQueryHandler(db);
        (await handler.Handle(new(offering.Id, ActorUserId: lecturer.Id), default)).Error.Should().StartWith("FORBIDDEN:");
        await Sync(db).Handle(new(head.Id, offering.Id, [lecturer.Id]), default);
        (await handler.Handle(new(offering.Id, ActorUserId: head.Id), default)).Value.Should().ContainSingle();
        (await handler.Handle(new(offering.Id, ActorUserId: lecturer.Id), default)).Value.Should().ContainSingle();
        var other = new User { Email = "otherhead@example.test", FullName = "Other", Role = UserRole.DepartmentHead, IsActive = true };
        db.Users.Add(other);
        await db.SaveChangesAsync();
        (await handler.Handle(new(offering.Id, ActorUserId: other.Id), default)).Error.Should().StartWith("FORBIDDEN:");
        await Sync(db).Handle(new(head.Id, offering.Id, []), default);
        (await new GetLecturerCourseOfferingsQueryHandler(db).Handle(new(lecturer.Id), default)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Offerings_FilterBySemesterAndAssignment_WithoutGlobalFallback()
    {
        await using var db = Context();
        var (head, offering, lecturer) = await Seed(db);
        var handler = new GetLecturerCourseOfferingsQueryHandler(db);
        (await handler.Handle(new(lecturer.Id), default)).Value.Should().BeEmpty();
        (await handler.Handle(new(head.Id, SemesterId: Guid.NewGuid()), default)).Value.Should().BeEmpty();
        (await handler.Handle(new(head.Id, SemesterId: offering.SemesterId), default)).Value.Should().ContainSingle();
    }

    private static OralExamDbContext Context() => new(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(User Head, CourseOffering Offering, User Lecturer)> Seed(OralExamDbContext db)
    {
        var head = new User { Email = "head@example.test", FullName = "Head", Role = UserRole.DepartmentHead, IsActive = true };
        var lecturer = new User { Email = "lecturer@example.test", FullName = "Active lecturer", Role = UserRole.Lecturer, IsActive = true };
        var program = new AcademicProgram { Code = "SE", Name = "SE", DepartmentHeadUserId = head.Id };
        var semester = new Semester { Code = "FA26", Name = "Fall", IsActive = true };
        var course = new Course { Code = "PRN", Name = "Programming", SemesterId = semester.Id, AcademicProgram = program, DepartmentHeadUserId = head.Id, IsActive = true };
        var offering = new CourseOffering { Course = course, Semester = semester, IsEnabled = true };
        db.AddRange(head, lecturer, program, semester, course, offering);
        await db.SaveChangesAsync();
        return (head, offering, lecturer);
    }
}

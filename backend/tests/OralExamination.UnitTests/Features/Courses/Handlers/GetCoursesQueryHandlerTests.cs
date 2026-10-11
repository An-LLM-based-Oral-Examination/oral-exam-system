using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Courses.Queries.GetCourses;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses.Handlers;

public class GetCoursesQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "GetCoursesQuery: Trả về danh sách môn học IsActive = true được sắp xếp theo Course Code")]
    public async Task Handle_Should_Return_Only_Active_Courses()
    {
        using var context = CreateDbContext();
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var activeCourse1 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = true,
            IsActive = true
        };

        var activeCourse2 = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 90,
            MaxFollowUpQuestions = 3,
            HasFollowUp = true,
            IsActive = true
        };

        var inactiveCourse = new Course
        {
            Id = Guid.NewGuid(),
            Code = "OLD101",
            Name = "Legacy Course",
            Credits = 3,
            SemesterId = semester.Id,
            IsActive = false
        };

        context.Semesters.Add(semester);
        context.Courses.AddRange(activeCourse1, activeCourse2, inactiveCourse);
        await context.SaveChangesAsync();

        var handler = new GetCoursesQueryHandler(context);
        var query = new GetCoursesQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(c => c.Code).Should().ContainInOrder("PRN231", "SWD392");
    }
}

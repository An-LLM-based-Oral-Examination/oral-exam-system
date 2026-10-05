using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses.Handlers;

public class GetCourseConfigurationQueryHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. GetCourseConfigurationQuery trả về cấu hình chi tiết khi CourseId tồn tại")]
    public async Task Handle_Should_Return_Course_Config_When_Found()
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

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Architecture",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 75,
            MaxFollowUpQuestions = 4,
            HasFollowUp = true
        };

        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new GetCourseConfigurationQueryHandler(context);
        var query = new GetCourseConfigurationQuery(course.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.TranscriptBufferSeconds.Should().Be(75);
        result.Value.MaxFollowUpQuestions.Should().Be(4);
        result.Value.HasFollowUp.Should().BeTrue();
    }

    [Fact(DisplayName = "2. GetCourseConfigurationQuery trả về Failure khi CourseId không tồn tại")]
    public async Task Handle_Should_Fail_When_Course_Not_Found()
    {
        using var context = CreateDbContext();

        var handler = new GetCourseConfigurationQueryHandler(context);
        var query = new GetCourseConfigurationQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }
}

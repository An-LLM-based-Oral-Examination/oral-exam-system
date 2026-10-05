using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses.Handlers;

public class UpdateCourseConfigurationCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<Course> SeedCourseAsync(OralExamDbContext context)
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

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture & Design",
            Credits = 3,
            SemesterId = semester.Id,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            HasFollowUp = false,
            IsActive = true
        };

        context.Semesters.Add(semester);
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        return course;
    }

    [Fact(DisplayName = "1. Handle cập nhật cấu hình động môn học thành công")]
    public async Task Handle_Should_Update_Course_Configuration_Successfully()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 90,
            MaxFollowUpQuestions: 3,
            HasFollowUp: true
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CourseId.Should().Be(course.Id);
        result.Value.TranscriptBufferSeconds.Should().Be(90);
        result.Value.MaxFollowUpQuestions.Should().Be(3);
        result.Value.HasFollowUp.Should().BeTrue();

        // Kiểm tra database lưu trữ
        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.TranscriptBufferSeconds.Should().Be(90);
        updatedCourse.MaxFollowUpQuestions.Should().Be(3);
        updatedCourse.HasFollowUp.Should().BeTrue();
    }

    [Fact(DisplayName = "2. Handle trả về Failure khi CourseId không tồn tại")]
    public async Task Handle_Should_Fail_When_Course_Not_Found()
    {
        using var context = CreateDbContext();
        await SeedCourseAsync(context);

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: Guid.NewGuid(),
            TranscriptBufferSeconds: 120,
            MaxFollowUpQuestions: 2,
            HasFollowUp: false
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }

    [Fact(DisplayName = "3. Handle giữ nguyên giá trị HasFollowUp cũ khi command truyền HasFollowUp là null")]
    public async Task Handle_Should_Keep_Existing_HasFollowUp_When_Null()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);
        course.HasFollowUp = true;
        await context.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 45,
            MaxFollowUpQuestions: 2,
            HasFollowUp: null
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasFollowUp.Should().BeTrue();

        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.HasFollowUp.Should().BeTrue();
        updatedCourse.TranscriptBufferSeconds.Should().Be(45);
        updatedCourse.MaxFollowUpQuestions.Should().Be(2);
    }
}

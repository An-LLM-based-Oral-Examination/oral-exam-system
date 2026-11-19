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
    [Theory]
    [InlineData("{\"easy\":1,\"medium\":2,\"hard\":1}", true)]
    [InlineData("{\"easy\":0,\"medium\":0,\"hard\":0}", false)]
    [InlineData("{\"easy\":-1,\"medium\":2,\"hard\":1}", false)]
    [InlineData("{\"Apply\":3}", false)]
    [InlineData("invalid", false)]
    public async Task Handle_DifficultyDistributionDeterminesQuestionCount(string distribution, bool valid)
    {
        using var context = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var course = await SeedCourseAsync(context, ownerId);
        var originalCount = course.MockExamTotalQuestions;
        var command = new UpdateCourseConfigurationCommand(course.Id, 60, 2, true)
        {
            ActorRole = $"department_head:{ownerId}",
            MockExamDifficultyDistribution = distribution,
            MockExamTotalQuestions = 99
        };
        var result = await new UpdateCourseConfigurationCommandHandler(context).Handle(command, default);
        result.IsSuccess.Should().Be(valid);
        course.MockExamTotalQuestions.Should().Be(valid ? 4 : originalCount);
        if (valid) course.MockExamDifficultyDistribution.Should().NotBeNull();
        else course.MockExamDifficultyDistribution.Should().BeNull();
    }

    [Theory]
    [InlineData("program", true)]
    [InlineData("legacy", false)]
    [InlineData("other", false)]
    [InlineData("missing", false)]
    [InlineData("missing_id", false)]
    public async Task Handle_UsesProgramOwnership(string actor, bool allowed)
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);
        var programOwner = Guid.NewGuid();
        var legacyOwner = Guid.NewGuid();
        course.DepartmentHeadUserId = legacyOwner;
        course.AcademicProgram = new AcademicProgram
        {
            Code = "SE", Name = "Software Engineering", DepartmentHeadUserId = programOwner
        };
        context.AcademicPrograms.Add(course.AcademicProgram);
        await context.SaveChangesAsync();
        var id = actor == "program" ? programOwner : actor == "legacy" ? legacyOwner : Guid.NewGuid();
        var command = new UpdateCourseConfigurationCommand(course.Id, 90, 2, true)
        {
            ActorRole = actor switch
            {
                "missing" => null,
                "missing_id" => "department_head",
                _ => $"department_head:{id}"
            }
        };

        var result = await new UpdateCourseConfigurationCommandHandler(context).Handle(command, default);
        result.IsSuccess.Should().Be(allowed);
        course.TranscriptBufferSeconds.Should().Be(allowed ? 90 : 60);
    }

    [Fact]
    public async Task Handle_LegacyOwnerCanConfigureButCannotChangeQuota()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);
        var owner = Guid.NewGuid();
        course.DepartmentHeadUserId = owner;
        await context.SaveChangesAsync();
        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(course.Id, 90, 2, true)
        {
            ActorRole = $"department_head:{owner}"
        };
        (await handler.Handle(command, default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(command with { MaxMockExamsPerDay = 5 }, default)).IsSuccess.Should().BeFalse();
        course.MaxMockExamsPerDay.Should().Be(3);
    }

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private async Task<Course> SeedCourseAsync(OralExamDbContext context, Guid? departmentHeadUserId = null)
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
            DepartmentHeadUserId = departmentHeadUserId,
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
        var ownerId = Guid.NewGuid();
        var course = await SeedCourseAsync(context, ownerId);

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 90,
            MaxFollowUpQuestions: 3,
            HasFollowUp: true
        ) { ActorRole = $"department_head:{ownerId}" };

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
        ) { ActorRole = $"department_head:{Guid.NewGuid()}" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Môn học không tồn tại");
    }

    [Fact(DisplayName = "3. Handle giữ nguyên giá trị HasFollowUp cũ khi command truyền HasFollowUp là null")]
    public async Task Handle_Should_Keep_Existing_HasFollowUp_When_Null()
    {
        using var context = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var course = await SeedCourseAsync(context, ownerId);
        course.HasFollowUp = true;
        await context.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 45,
            MaxFollowUpQuestions: 2,
            HasFollowUp: null
        ) { ActorRole = $"department_head:{ownerId}" };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasFollowUp.Should().BeTrue();

        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.HasFollowUp.Should().BeTrue();
        updatedCourse.TranscriptBufferSeconds.Should().Be(45);
        updatedCourse.MaxFollowUpQuestions.Should().Be(2);
    }

    [Fact(DisplayName = "4. Handle cập nhật MaxMockExamsPerDay khi Quản trị viên (Admin) cấu hình hạn ngạch thi thử")]
    public async Task Handle_Should_Update_MaxMockExamsPerDay_When_Provided()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);

        var handler = new UpdateCourseConfigurationCommandHandler(context);
        var command = new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: course.TranscriptBufferSeconds,
            MaxFollowUpQuestions: course.MaxFollowUpQuestions,
            HasFollowUp: course.HasFollowUp,
            ExamInputMode: course.ExamInputMode
        )
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = 5
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MaxMockExamsPerDay.Should().Be(5);

        var updatedCourse = await context.Courses.FindAsync(course.Id);
        updatedCourse!.MaxMockExamsPerDay.Should().Be(5);
    }

    [Fact(DisplayName = "5. Admin bị chặn FORBIDDEN khi thay đổi tham số học thuật hoặc không truyền MaxMockExamsPerDay")]
    public async Task Handle_Should_Forbid_Admin_From_Mutating_Academic_Fields_Or_Omitting_Quota()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);
        var handler = new UpdateCourseConfigurationCommandHandler(context);

        var missingQuota = await handler.Handle(new UpdateCourseConfigurationCommand(
            course.Id,
            course.TranscriptBufferSeconds,
            course.MaxFollowUpQuestions,
            course.HasFollowUp)
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = null
        }, CancellationToken.None);

        missingQuota.IsSuccess.Should().BeFalse();
        missingQuota.Error.Should().StartWith("FORBIDDEN:");

        var mutateAcademic = await handler.Handle(new UpdateCourseConfigurationCommand(
            course.Id,
            90,
            course.MaxFollowUpQuestions,
            true,
            "VoiceOnly")
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = 5
        }, CancellationToken.None);

        mutateAcademic.IsSuccess.Should().BeFalse();
        mutateAcademic.Error.Should().StartWith("FORBIDDEN:");

        var persisted = await context.Courses.FindAsync(course.Id);
        persisted!.TranscriptBufferSeconds.Should().Be(60);
        persisted.HasFollowUp.Should().BeFalse();
        persisted.MaxMockExamsPerDay.Should().Be(3);
    }

    [Fact(DisplayName = "6. Admin cập nhật MaxMockExamsPerDay thành công với payload tối giản, ExamInputMode/Difficulty rỗng và JSON đảo thứ tự/khoảng trắng")]
    public async Task Handle_Should_Allow_Admin_QuotaUpdate_With_MinimalPayload_EmptyNullEquivalent_And_SemanticJson()
    {
        using var context = CreateDbContext();
        var course = await SeedCourseAsync(context);
        course.MockExamDifficultyDistribution = "{\"easy\":1,\"medium\":2,\"hard\":1}";
        course.MockExamBloomDistribution = "{\"Remember\": 1, \"Understand\": 2, \"Apply\": 2}";
        await context.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(context);

        // 1. Minimal payload + reordered Difficulty keys + minified/reordered Bloom JSON
        var reorderedResult = await handler.Handle(new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 0,
            MaxFollowUpQuestions: 0,
            HasFollowUp: null,
            ExamInputMode: "")
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = 5,
            MockExamDifficultyDistribution = "{ \"hard\": 1, \"medium\": 2, \"easy\": 1 }",
            MockExamBloomDistribution = "{\"Apply\":2,\"Remember\":1,\"Understand\":2}"
        }, CancellationToken.None);

        reorderedResult.IsSuccess.Should().BeTrue();
        reorderedResult.Value.MaxMockExamsPerDay.Should().Be(5);
        reorderedResult.Value.TranscriptBufferSeconds.Should().Be(60);
        reorderedResult.Value.MaxFollowUpQuestions.Should().Be(1);
        reorderedResult.Value.ExamInputMode.Should().Be(course.ExamInputMode);

        // 2. Empty string DifficultyDistribution when DB has null -> succeeds without overwriting null
        course.MockExamDifficultyDistribution = null;
        await context.SaveChangesAsync();

        var emptyOnNullResult = await handler.Handle(new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 0,
            MaxFollowUpQuestions: 0,
            HasFollowUp: null,
            ExamInputMode: "")
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = 8,
            MockExamDifficultyDistribution = "",
            MockExamBloomDistribution = "{\"Remember\":1,\"Understand\":2,\"Apply\":2}"
        }, CancellationToken.None);

        emptyOnNullResult.IsSuccess.Should().BeTrue();
        emptyOnNullResult.Value.MaxMockExamsPerDay.Should().Be(8);
        emptyOnNullResult.Value.MockExamDifficultyDistribution.Should().BeNull();

        // 3. Semantic value change in Bloom JSON still returns FORBIDDEN
        var semanticDiffResult = await handler.Handle(new UpdateCourseConfigurationCommand(
            CourseId: course.Id,
            TranscriptBufferSeconds: 0,
            MaxFollowUpQuestions: 0,
            HasFollowUp: null,
            ExamInputMode: "")
        {
            ActorRole = "admin",
            MaxMockExamsPerDay = 8,
            MockExamBloomDistribution = "{\"Remember\":2,\"Understand\":2,\"Apply\":1}"
        }, CancellationToken.None);

        semanticDiffResult.IsSuccess.Should().BeFalse();
        semanticDiffResult.Error.Should().StartWith("FORBIDDEN:");
    }
}

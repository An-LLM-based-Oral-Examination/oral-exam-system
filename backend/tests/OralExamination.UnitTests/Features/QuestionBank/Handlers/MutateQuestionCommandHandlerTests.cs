using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Handlers;

public sealed class MutateQuestionCommandHandlerTests
{
    private static OralExamDbContext CreateContext() => new(
        new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(User Lecturer, User Head, Course Course, PracticeQuestion Question)> SeedAsync(
        OralExamDbContext context, string status = ApprovalStatus.Draft)
    {
        var lecturer = new User { Id = Guid.NewGuid(), Role = UserRole.Lecturer,
            Email = $"lecturer-{Guid.NewGuid():N}@fpt.edu.vn", FullName = "Lecturer", IsActive = true };
        var head = new User { Id = Guid.NewGuid(), Role = UserRole.DepartmentHead,
            Email = $"head-{Guid.NewGuid():N}@fpt.edu.vn", FullName = "Head", IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "CSD201", Name = "Data Structures",
            Credits = 3, DepartmentHeadUserId = head.Id, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id,
            Name = "Original", TotalMaxScore = 10m, IsActive = true };
        rubric.Criteria.Add(new RubricCriterion { CriterionName = "A", MaxScore = 5m,
            Weight = 0.5m, BloomLevel = BloomLevel.Understand, OrderIndex = 1 });
        rubric.Criteria.Add(new RubricCriterion { CriterionName = "B", MaxScore = 5m,
            Weight = 0.5m, BloomLevel = BloomLevel.Apply, OrderIndex = 2 });
        var question = new PracticeQuestion { Id = Guid.NewGuid(), CourseId = course.Id,
            Rubric = rubric, Title = "Original", Content = "Original content",
            SampleAnswer = new string('A', 60), SubmittedBy = lecturer.Id,
            ApprovalStatus = status, IsActive = status == ApprovalStatus.Approved };
        context.Users.AddRange(lecturer, head);
        context.Courses.Add(course);
        context.CourseLecturers.Add(new CourseLecturer { CourseId = course.Id,
            LecturerUserId = lecturer.Id, AssignedByUserId = head.Id });
        context.PracticeQuestions.Add(question);
        await context.SaveChangesAsync();
        return (lecturer, head, course, question);
    }

    private static UpdateQuestionDraftDto Draft(decimal secondScore = 6m) => new(
        "Updated title", "Updated content", new string('B', 60),
        ["Dependency Injection"], QuestionDifficulty.Medium, BloomLevel.Apply,
        new RubricDraftDto("Updated rubric", null, 10m,
        [new RubricCriterionDraftDto("Criterion 1", 10m - secondScore, 0.4m, BloomLevel.Understand, "A"),
         new RubricCriterionDraftDto("Criterion 2", secondScore, 0.6m, BloomLevel.Apply, "B")]),
        null);

    [Fact]
    public async Task LecturerUpdatesOwnDraft_WithNewTenPointRubric()
    {
        await using var context = CreateContext();
        var (lecturer, _, _, question) = await SeedAsync(context);
        var oldRubricId = question.RubricId;

        var result = await new MutateQuestionCommandHandler(context).Handle(
            new MutateQuestionCommand(question.Id, lecturer.Id, lecturer.Role,
                QuestionMutationKind.Update, Draft()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        question.Title.Should().Be("Updated title");
        question.IsActive.Should().BeFalse();
        question.ApprovalStatus.Should().Be(ApprovalStatus.Draft);
        question.RubricId.Should().NotBe(oldRubricId);
        question.Rubric.Criteria.Sum(c => c.MaxScore).Should().Be(10m);
    }

    [Fact]
    public async Task InvalidRubricAndOtherLecturer_CannotChangeDraft()
    {
        await using var context = CreateContext();
        var (lecturer, _, _, question) = await SeedAsync(context);
        var handler = new MutateQuestionCommandHandler(context);
        var badRubric = await handler.Handle(new MutateQuestionCommand(question.Id, lecturer.Id,
            lecturer.Role, QuestionMutationKind.Update, Draft(5.5m) with
            { Rubric = Draft().Rubric with { Criteria = [new RubricCriterionDraftDto("Only", 9m, 1m, BloomLevel.Apply, "invalid")] } }),
            CancellationToken.None);
        var otherLecturer = await handler.Handle(new MutateQuestionCommand(question.Id, Guid.NewGuid(),
            UserRole.Lecturer, QuestionMutationKind.SoftDelete), CancellationToken.None);

        badRubric.IsSuccess.Should().BeFalse();
        otherLecturer.IsSuccess.Should().BeFalse();
        question.IsDeleted.Should().BeFalse();
        question.Title.Should().Be("Original");
    }

    [Fact]
    public async Task SoftDeleteHidesUnusedDraft_AndCannotDeleteApprovedQuestion()
    {
        await using var context = CreateContext();
        var (lecturer, _, _, question) = await SeedAsync(context);
        var handler = new MutateQuestionCommandHandler(context);

        var removed = await handler.Handle(new MutateQuestionCommand(question.Id, lecturer.Id,
            lecturer.Role, QuestionMutationKind.SoftDelete), CancellationToken.None);
        var replay = await handler.Handle(new MutateQuestionCommand(question.Id, lecturer.Id,
            lecturer.Role, QuestionMutationKind.SoftDelete), CancellationToken.None);

        removed.IsSuccess.Should().BeTrue();
        question.IsDeleted.Should().BeTrue();
        question.IsActive.Should().BeFalse();
        replay.IsSuccess.Should().BeFalse();
        replay.Error.Should().StartWith("NOT_FOUND:");
        var detail = await new GetQuestionByIdQueryHandler(context).Handle(
            new GetQuestionByIdQuery(question.Id, lecturer.Id, lecturer.Role), CancellationToken.None);
        detail.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnlyCourseHeadCanToggleApprovedQuestion()
    {
        await using var context = CreateContext();
        var (lecturer, head, _, question) = await SeedAsync(context, ApprovalStatus.Approved);
        var handler = new MutateQuestionCommandHandler(context);

        var denied = await handler.Handle(new MutateQuestionCommand(question.Id, lecturer.Id,
            lecturer.Role, QuestionMutationKind.SetActive, Active: false), CancellationToken.None);
        var allowed = await handler.Handle(new MutateQuestionCommand(question.Id, head.Id,
            head.Role, QuestionMutationKind.SetActive, Active: false), CancellationToken.None);
        var delete = await handler.Handle(new MutateQuestionCommand(question.Id, head.Id,
            head.Role, QuestionMutationKind.SoftDelete), CancellationToken.None);

        denied.IsSuccess.Should().BeFalse();
        allowed.IsSuccess.Should().BeTrue();
        question.IsActive.Should().BeFalse();
        delete.IsSuccess.Should().BeFalse();
        question.IsDeleted.Should().BeFalse();
    }
}

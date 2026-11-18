using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using OralExamination.Application.Features.QuestionBank.ReviewRequests;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank;

public sealed class QuestionReviewRequestTests
{
    private static OralExamDbContext Context() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(User Head, User Lecturer, Course Course, PracticeQuestion[] Questions)> Seed(OralExamDbContext db)
    {
        var head = new User { FullName = "Head", Email = "head@test.local", Role = "department_head" };
        var lecturer = new User { FullName = "Lecturer", Email = "lecturer@test.local", Role = "lecturer" };
        var course = new Course
        {
            Code = "SE", Name = "Software Engineering",
            AcademicProgram = new AcademicProgram { Code = "SE", Name = "SE", DepartmentHeadUserId = head.Id }
        };
        db.Users.AddRange(head, lecturer);
        db.Courses.Add(course);
        var questions = Enumerable.Range(1, 3).Select(index => new PracticeQuestion
        {
            Course = course, CourseId = course.Id, Title = $"Question {index}", Content = "Question content",
            SampleAnswer = new string('a', 60), SubmittedBy = lecturer.Id,
            ApprovalStatus = "DRAFT", Source = "manual",
            Rubric = new Rubric
            {
                CourseId = course.Id, Name = "Rubric", Criteria = new List<RubricCriterion>
                {
                    new() { CriterionName = "Content", MaxScore = 5, Weight = 0.5m, OrderIndex = 1 },
                    new() { CriterionName = "Reasoning", MaxScore = 5, Weight = 0.5m, OrderIndex = 2 }
                }
            }
        }).ToArray();
        db.PracticeQuestions.AddRange(questions);
        await db.SaveChangesAsync();
        return (head, lecturer, course, questions);
    }

    private static Task<OralExamination.Application.Common.Models.Result<OralExamination.Application.Features.QuestionBank.DTOs.BatchSubmitQuestionsForReviewResponseDto>>
        Submit(OralExamDbContext db, User lecturer, Course course, IEnumerable<PracticeQuestion> questions) =>
        new BatchSubmitQuestionsForReviewCommandHandler(db).Handle(
            new(course.Id, lecturer.Id, questions.Select(q => q.Id).ToList(), "Submission note"), default);

    [Fact]
    public async Task Submission_CreatesSnapshotAndRejectsDuplicatePendingSubmission()
    {
        await using var db = Context();
        var (head, lecturer, course, questions) = await Seed(db);
        (await Submit(db, lecturer, course, questions)).IsSuccess.Should().BeTrue();
        var service = new QuestionReviewRequestService(db);
        var page = await service.List(head.Id, null, null, null, 1, default);
        page.Items.Should().ContainSingle();
        page.Items[0].Pending.Should().Be(3);
        var detail = await service.Detail(head.Id, page.Items[0].Id, default);
        detail.Value.Items.Should().OnlyContain(i => i.Question != null && i.Question.Criteria.Count == 2);
        (await Submit(db, lecturer, course, questions)).IsSuccess.Should().BeFalse();
        (await db.QuestionReviewRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Search_MatchesOnlyLecturerNameOrEmail()
    {
        await using var db = Context();
        var (head, lecturer, course, questions) = await Seed(db);
        await Submit(db, lecturer, course, questions);
        var service = new QuestionReviewRequestService(db);

        (await service.List(head.Id, null, null, " LECTURER ", 1, default)).Items.Should().ContainSingle();
        (await service.List(head.Id, null, null, lecturer.Email.ToUpperInvariant(), 1, default)).Items.Should().ContainSingle();
        (await service.List(head.Id, null, null, course.Name, 1, default)).Items.Should().BeEmpty();
        (await service.List(head.Id, null, null, course.Code, 1, default)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Decisions_ArePerItem_AndResubmissionPreservesPreviousSnapshot()
    {
        await using var db = Context();
        var (head, lecturer, course, questions) = await Seed(db);
        await Submit(db, lecturer, course, questions);
        var service = new QuestionReviewRequestService(db);
        var request = await db.QuestionReviewRequests.Include(r => r.Items).SingleAsync();
        var first = request.Items.First(i => i.QuestionId == questions[0].Id);
        var second = request.Items.First(i => i.QuestionId == questions[1].Id);
        var third = request.Items.First(i => i.QuestionId == questions[2].Id);
        (await service.Decide(head.Id, request.Id, new([first.Id], "APPROVED", null, 0), default)).IsSuccess.Should().BeTrue();
        (await service.List(head.Id, null, "IN_PROGRESS", null, 1, default)).Items.Should().ContainSingle();
        (await service.Decide(head.Id, request.Id, new([second.Id], "NEEDS_REVISION", "Please improve reasoning.", 1), default)).IsSuccess.Should().BeTrue();
        (await service.Decide(head.Id, request.Id, new([third.Id], "REJECTED", "Question is out of scope.", 2), default)).IsSuccess.Should().BeTrue();
        (await service.List(head.Id, null, "COMPLETED", null, 1, default)).Items.Should().ContainSingle();
        questions[1].Title = "Revised question";
        await db.SaveChangesAsync();
        (await Submit(db, lecturer, course, [questions[1]])).IsSuccess.Should().BeTrue();
        var old = await service.Detail(head.Id, request.Id, default);
        old.Value.Items.Single(i => i.Id == second.Id).Question!.Title.Should().Be("Question 2");
        old.Value.Items.Single(i => i.Id == second.Id).ReviewNotes.Should().Be("Please improve reasoning.");
        (await db.QuestionReviewRequests.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task BatchApprove_ValidatesAllItemsBeforeUpdating_AndEnforcesOwnershipAndVersion()
    {
        await using var db = Context();
        var (head, lecturer, course, questions) = await Seed(db);
        await Submit(db, lecturer, course, questions);
        var other = new User { Role = "department_head", Email = "other@test.local", FullName = "Other" };
        db.Users.Add(other);
        await db.SaveChangesAsync();
        var request = await db.QuestionReviewRequests.Include(r => r.Items).SingleAsync();
        var service = new QuestionReviewRequestService(db);
        var input = new ReviewRequestDecision(request.Items.Select(i => i.Id).ToList(), "APPROVED", null, 0);
        (await service.List(other.Id, null, null, null, 1, default)).Items.Should().BeEmpty();
        (await service.Detail(other.Id, request.Id, default)).IsSuccess.Should().BeFalse();
        (await service.Decide(other.Id, request.Id, input, default)).IsSuccess.Should().BeFalse();
        (await service.Decide(head.Id, request.Id, input with { Version = 5 }, default)).IsSuccess.Should().BeFalse();
        (await service.Decide(head.Id, request.Id, input with { Decision = "REJECTED", Notes = "" }, default)).IsSuccess.Should().BeFalse();
        questions[1].SampleAnswer = "Changed";
        (await service.Decide(head.Id, request.Id, input, default)).IsSuccess.Should().BeFalse();
        request.Items.Should().OnlyContain(i => i.Decision == "PENDING");
        questions.Should().OnlyContain(q => q.ApprovalStatus == "SUBMITTED_FOR_REVIEW");
    }

    [Fact]
    public async Task BatchApprove_AllPendingAndRetryAreAtomicAndIdempotent()
    {
        await using var db = Context();
        var (head, lecturer, course, questions) = await Seed(db);
        await Submit(db, lecturer, course, questions);
        var request = await db.QuestionReviewRequests.Include(r => r.Items).SingleAsync();
        var input = new ReviewRequestDecision(request.Items.Select(i => i.Id).ToList(), "APPROVED", null, 0);
        var service = new QuestionReviewRequestService(db);
        (await service.Decide(head.Id, request.Id, input, default)).IsSuccess.Should().BeTrue();
        (await service.Decide(head.Id, request.Id, input, default)).IsSuccess.Should().BeTrue();
        request.Version.Should().Be(1);
        questions.Should().OnlyContain(q => q.ApprovalStatus == "APPROVED" && q.ApprovedBy == head.Id);
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.QuestionBank.Commands.MutateQuestion;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class QuestionSoftDeletePostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task DeletedDraftPersistsPhysically_ButDisappearsFromAuthorizedList()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var seed = await TestData.AcademicAsync(context);
        context.CourseLecturers.Add(new CourseLecturer
        {
            CourseId = seed.Course.Id, LecturerUserId = seed.Lecturer.Id,
            AssignedByUserId = seed.Lecturer.Id
        });
        var question = new PracticeQuestion
        {
            CourseId = seed.Course.Id, RubricId = seed.Rubric.Id,
            Title = "Draft question", Content = "Explain data structures.",
            SampleAnswer = new string('A', 60), SubmittedBy = seed.Lecturer.Id,
            ApprovalStatus = ApprovalStatus.Draft, IsActive = false
        };
        context.PracticeQuestions.Add(question);
        await context.SaveChangesAsync();

        var list = new GetQuestionsQuery(CourseId: seed.Course.Id, UsageScope: "practice",
            ActorId: seed.Lecturer.Id, ActorRole: UserRole.Lecturer);
        var before = await new GetQuestionsQueryHandler(context).Handle(list, CancellationToken.None);
        before.IsSuccess.Should().BeTrue();
        before.Value.Items.Should().ContainSingle(item => item.Id == question.Id);

        var result = await new MutateQuestionCommandHandler(context).Handle(
            new MutateQuestionCommand(question.Id, seed.Lecturer.Id, UserRole.Lecturer,
                QuestionMutationKind.SoftDelete), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        await using var verify = database.CreateContext();
        var persisted = await verify.PracticeQuestions.SingleAsync(q => q.Id == question.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();
        var after = await new GetQuestionsQueryHandler(verify).Handle(list, CancellationToken.None);
        after.IsSuccess.Should().BeTrue();
        after.Value.Items.Should().BeEmpty();
    }
}

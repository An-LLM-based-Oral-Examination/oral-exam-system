using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class AiEvaluationConstraintTests(PostgresFixture fixture)
{
    [Fact]
    public async Task OfficialSubmission_SecondEvaluationRejectedByPartialUniqueIndex_OriginalRemains()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var official = await TestData.OfficialAsync(context);
        var submission = AddOfficialSubmission(context, official);
        var original = new AiEvaluation
        {
            AnswerType = "exam", ExamSubmission = submission, StudentId = official.Academic.Student.Id,
            TotalScore = 8m, Feedback = "Original persisted evaluation"
        };
        context.AiEvaluations.Add(original);
        await context.SaveChangesAsync();
        context.AiEvaluations.Add(new AiEvaluation
        {
            AnswerType = "exam", ExamSubmissionId = submission.Id, StudentId = official.Academic.Student.Id,
            TotalScore = 3m, Feedback = "Duplicate evaluation must not replace original"
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("uq_ai_evaluations_exam_submission");
        await using var verify = database.CreateContext();
        var persisted = await verify.AiEvaluations.Where(item => item.ExamSubmissionId == submission.Id).SingleAsync();
        persisted.Id.Should().Be(original.Id);
        persisted.TotalScore.Should().Be(8m);
        persisted.Feedback.Should().Be("Original persisted evaluation");
    }

    [Fact]
    public async Task PartialUniqueIndex_HasNonNullPredicate_AndAllowsTwoValidPracticeEvaluations()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var answers = await AddPracticeAnswersAsync(context, academic);
        context.AiEvaluations.AddRange(answers.Select(answer => new AiEvaluation
        {
            AnswerType = "practice", PracticeAnswerId = answer.Id, StudentId = academic.Student.Id,
            TotalScore = 7m
        }));

        await context.SaveChangesAsync();

        var definition = await database.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'ai_evaluations' AND indexname = 'uq_ai_evaluations_exam_submission'");
        definition.Should().Contain("CREATE UNIQUE INDEX");
        definition.Should().MatchRegex(@"WHERE\s+\(exam_submission_id IS NOT NULL\)");
        await using var verify = database.CreateContext();
        var answerIds = answers.Select(answer => answer.Id).ToArray();
        var persisted = await verify.AiEvaluations
            .Where(item => item.PracticeAnswerId.HasValue && answerIds.Contains(item.PracticeAnswerId.Value))
            .ToListAsync();
        persisted.Should().HaveCount(2);
        persisted.Should().OnlyContain(item => item.AnswerType == "practice" && item.ExamSubmissionId == null);
        (await verify.PracticeAnswers.CountAsync(answer => answerIds.Contains(answer.Id))).Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task XorConstraint_RejectsWrongFlowReferenceOrTwoValidReferences(bool includeOfficialReference)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var official = await TestData.OfficialAsync(context);
        var submission = AddOfficialSubmission(context, official);
        var answers = await AddPracticeAnswersAsync(context, official.Academic);
        // Every non-null reference points to a real persisted entity. Failure must
        // come from flow/reference XOR, not a nonexistent FK or invalid type value.
        var rejected = new AiEvaluation
        {
            AnswerType = "exam", StudentId = official.Academic.Student.Id,
            PracticeAnswerId = answers[0].Id,
            ExamSubmissionId = includeOfficialReference ? submission.Id : null,
            TotalScore = 5m
        };
        context.AiEvaluations.Add(rejected);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        postgres.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        postgres.ConstraintName.Should().Be("ck_ai_evaluations_xor");
        await using var verify = database.CreateContext();
        (await verify.AiEvaluations.AnyAsync(item => item.Id == rejected.Id)).Should().BeFalse();
        (await verify.ExamQuestionSubmissions.AnyAsync(item => item.Id == submission.Id)).Should().BeTrue();
        (await verify.PracticeAnswers.AnyAsync(item => item.Id == answers[0].Id)).Should().BeTrue();
    }

    private static ExamQuestionSubmission AddOfficialSubmission(OralExamDbContext context, OfficialData official)
    {
        var submission = new ExamQuestionSubmission
        {
            TicketId = official.Ticket.Id, ExamQuestionId = official.Question.Id,
            TranscriptWhisper = "Integration answer for evaluation constraints", GradingStatus = "pending"
        };
        context.ExamQuestionSubmissions.Add(submission);
        return submission;
    }

    private static async Task<PracticeAnswer[]> AddPracticeAnswersAsync(OralExamDbContext context, AcademicData academic)
    {
        var question = new PracticeQuestion
        {
            CourseId = academic.Course.Id, RubricId = academic.Rubric.Id, CloId = academic.Clo.Id,
            Title = "Practice constraint fixture", Content = "Explain atomicity."
        };
        var session = new PracticeSession { CourseId = academic.Course.Id, StudentId = academic.Student.Id };
        var answers = Enumerable.Range(1, 2).Select(index => new PracticeAnswer
        {
            Session = session, Question = question, StudentId = academic.Student.Id,
            OrderIndex = index, AnswerText = $"Integration practice answer {index}"
        }).ToArray();
        context.PracticeAnswers.AddRange(answers);
        await context.SaveChangesAsync();
        return answers;
    }
}

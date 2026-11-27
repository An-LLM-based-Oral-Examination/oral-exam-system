using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class PaperPairSimilarityTests
{
    private sealed class Scenario : IAsyncDisposable
    {
        public OralExamDbContext Db { get; } = new(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Guid CourseId { get; } = Guid.NewGuid();
        public Semester Semester { get; } = new() { Code = "FA26", Name = "Fall", StartDate = new(2026, 9, 1), EndDate = new(2026, 12, 31) };
        public ExamSet Add(string code, string[] titles, string status = ExamSetStatus.Official, Semester? semester = null,
            Guid? courseId = null, bool sample = false, bool active = true, string sessionStatus = "completed")
        {
            semester ??= Semester;
            if (Db.Entry(semester).State == EntityState.Detached) Db.Semesters.Add(semester);
            var session = new OfficialExamSession { CourseId = courseId ?? CourseId, Semester = semester, SemesterId = semester.Id, Title = code + " exam", Status = sessionStatus };
            var set = new ExamSet { CourseId = session.CourseId, StructureId = Guid.NewGuid(), Session = session, SessionId = session.Id,
                SetCode = code, Status = status, IsSampleSet = sample, IsActive = active };
            set.ExamSetQuestions = titles.Select((title, i) => {
                var q = new ExamQuestion { CourseId = set.CourseId, Title = title, Content = title, RubricId = Guid.NewGuid() };
                return new ExamSetQuestion { ExamSet = set, ExamSetId = set.Id, ExamQuestion = q, ExamQuestionId = q.Id, OrderIndex = i + 1 };
            }).ToList();
            Db.ExamSets.Add(set);
            return set;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task DoesNotCombineBestQuestionsFromDifferentPapers()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha", "beta"], ExamSetStatus.Draft);
        s.Add("A", ["alpha", "gamma"]);
        s.Add("B", ["delta", "beta"]);
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.SimilarityPercent.Should().BeLessThan(100);
        report.Questions.SelectMany(q => q.Matches).Select(m => m.ExamSetId).Distinct().Should().ContainSingle();
        report.Questions.SelectMany(q => q.Matches).Should().OnlyContain(m => m.ExamSetId == report.ComparedPaper!.ExamSetId);
    }

    [Fact]
    public async Task ReorderingDoesNotChangeScoreAndEachQuestionIsMatchedOnce()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha", "beta", "gamma"], ExamSetStatus.Draft);
        var other = s.Add("OTHER", ["gamma", "alpha", "beta"]);
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.SimilarityPercent.Should().Be(100);
        report.ComparedPaper!.ExamSetId.Should().Be(other.Id);
        report.Questions.SelectMany(q => q.Matches).Select(m => m.QuestionId).Should().OnlyHaveUniqueItems();
        report.Questions.Select(q => q.Matches.Single().OrderIndex).Should().Equal(2, 3, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DifferentLengthsUseLongerDenominatorAndNeverReuseAQuestion(bool currentLonger)
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", currentLonger ? ["alpha", "alpha"] : ["alpha"], ExamSetStatus.Draft);
        s.Add("OTHER", currentLonger ? ["alpha"] : ["alpha", "alpha"]);
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.SimilarityPercent.Should().Be(50);
        report.Questions.SelectMany(q => q.Matches).Should().ContainSingle();
    }

    [Fact]
    public async Task MatchingOptimizesTotalInsteadOfTakingGreedyBestForFirstQuestion()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha beta", "alpha"], ExamSetStatus.Draft);
        s.Add("OTHER", ["alpha", "beta"]);
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.Questions[0].Matches.Single().Title.Should().Be("beta");
        report.Questions[1].Percent.Should().Be(100);
        report.SimilarityPercent.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task ScopeExcludesPendingSamplesInactiveOtherCoursesOldAndCancelledSessions()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha"], ExamSetStatus.Draft);
        var previous = new Semester { Code = "SU26", Name = "Summer", StartDate = new(2026, 5, 1), EndDate = new(2026, 8, 31) };
        var old = new Semester { Code = "SP26", Name = "Spring", StartDate = new(2026, 1, 1), EndDate = new(2026, 4, 30) };
        var cancelled = new Semester { Code = "CANCEL", Name = "Cancelled", StartDate = new(2026, 8, 2), EndDate = new(2026, 8, 31) };
        var expected = s.Add("PREVIOUS", ["beta"], semester: previous);
        s.Add("OLD", ["alpha"], semester: old);
        s.Add("CANCEL", ["alpha"], semester: cancelled, sessionStatus: "cancelled");
        s.Add("PENDING", ["alpha"], ExamSetStatus.SubmittedForReview);
        s.Add("SAMPLE", ["alpha"], sample: true);
        s.Add("INACTIVE", ["alpha"], active: false);
        s.Add("FOREIGN", ["alpha"], courseId: Guid.NewGuid());
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.ComparedPaper!.ExamSetId.Should().Be(expected.Id);
        report.SimilarityPercent.Should().BeLessThan(100);
    }

    [Fact]
    public async Task NoComparisonReturnsZeroAndNoComparedPaper()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha"], ExamSetStatus.Draft);
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.ComparedPaper.Should().BeNull(); report.SimilarityPercent.Should().Be(0);
        report.Questions.Single().Matches.Should().BeEmpty();
    }

    [Theory]
    [InlineData("alpha beta", "alpha beta", "unrelated", "different", 100)]
    [InlineData("alpha", "xyz", "same answer", "same answer", 0)]
    [InlineData("alpha beta gamma delta", "alpha beta gamma epsilon", "same answer", "same answer", -1)]
    [InlineData("alpha beta gamma delta", "alpha beta gamma epsilon", "xyz", "uvw", -2)]
    public async Task ContentDecidesClearCasesAndAnswersRefineAmbiguousCases(
        string left, string right, string leftAnswer, string rightAnswer, int expected)
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", [left], ExamSetStatus.Draft);
        var other = s.Add("OTHER", [right]);
        current.ExamSetQuestions.Single().ExamQuestion.SampleAnswer = leftAnswer;
        other.ExamSetQuestions.Single().ExamQuestion.SampleAnswer = rightAnswer;
        // Titles and source identity must not contribute to the score.
        current.ExamSetQuestions.Single().ExamQuestion.Title = "one title";
        other.ExamSetQuestions.Single().ExamQuestion.Title = "another title";
        other.ExamSetQuestions.Single().SourceQuestionId = current.ExamSetQuestions.Single().ExamQuestionId;
        await s.Db.SaveChangesAsync();
        var content = TrigramSimilarityCalculator.CalculateSimilarity(left, right);
        if (expected < 0) content.Should().BeInRange(0.50m, 0.8499m);
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.SimilarityPercent.Should().Be(expected < 0
            ? TrigramSimilarityCalculator.ToSimilarityPercent(content * 0.70m + (expected == -1 ? 0.30m : 0m))
            : expected);
    }

    [Fact]
    public async Task MissingAnswerKeepsContentScoreAndTitleAloneDoesNotMatch()
    {
        await using var s = new Scenario();
        var current = s.Add("CURRENT", ["alpha beta gamma delta"], ExamSetStatus.Draft);
        var other = s.Add("OTHER", ["alpha beta gamma epsilon"]);
        other.ExamSetQuestions.Single().ExamQuestion.SampleAnswer = "answer";
        await s.Db.SaveChangesAsync();
        var report = await ExamSimilarityRules.Report(s.Db, current.Id, default);
        report.SimilarityPercent.Should().Be(TrigramSimilarityCalculator.ToSimilarityPercent(
            TrigramSimilarityCalculator.CalculateSimilarity("alpha beta gamma delta", "alpha beta gamma epsilon")));
        current.ExamSetQuestions.Single().ExamQuestion.Content = "";
        other.ExamSetQuestions.Single().ExamQuestion.Content = "";
        other.ExamSetQuestions.Single().ExamQuestion.Title = current.ExamSetQuestions.Single().ExamQuestion.Title;
        await s.Db.SaveChangesAsync();
        (await ExamSimilarityRules.Report(s.Db, current.Id, default)).SimilarityPercent.Should().Be(0);
    }
}

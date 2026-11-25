using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class DuplicateQuestionAndExamSetTests
{
    private sealed class Scenario : IAsyncDisposable
    {
        public OralExamDbContext Db { get; } = new(new DbContextOptionsBuilder<OralExamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User Head { get; } = new() { Email = "head@test.local", FullName = "Head", Role = UserRole.DepartmentHead };
        public User Author { get; } = new() { Email = "author@test.local", FullName = "Author", Role = UserRole.Lecturer };
        public User Officer { get; } = new() { Email = "officer@test.local", FullName = "Officer", Role = UserRole.ExaminationOfficer };
        public Course Course { get; private set; } = null!;
        public OfficialExamSession Session { get; private set; } = null!;
        public CourseClo Clo { get; private set; } = null!;
        public ExamQuestion Source { get; private set; } = null!;
        public Guid StructureId { get; private set; }
        public Guid AssignmentId { get; private set; }
        public ExamPreparationCommandHandler Workflow => new(Db);
        public async Task Initialize(int quota = 1)
        {
            Db.Users.AddRange(Head, Author, Officer);
            var semester = new Semester { Code = "FA26", Name = "Fall", StartDate = new(2026,9,1), EndDate = new(2026,12,31) };
            Db.Semesters.Add(semester);
            Course = new Course { Code = "PRN231", Name = "Development", SemesterId = semester.Id, DepartmentHeadUserId = Head.Id };
            Db.Courses.Add(Course);
            Clo = new CourseClo { CourseId = Course.Id, CloCode = "CLO1", Description = "Analyze services", BloomLevel = "Analyze" };
            Db.CourseClos.Add(Clo);
            Db.CourseLecturers.Add(new CourseLecturer { CourseId = Course.Id, SemesterId = semester.Id, LecturerUserId = Author.Id, AssignedByUserId = Head.Id });
            Session = new OfficialExamSession { CourseId = Course.Id, SemesterId = semester.Id, Title = "Final", ExamDate = new(2026,12,20) };
            Db.OfficialExamSessions.Add(Session);
            var rubric = new Rubric { CourseId = Course.Id, Name = "Rubric", Description = "Complete grading", Criteria = [
                new RubricCriterion { CriterionName = "Concept", Description = "Explain the concept", MaxScore = 6, Weight = .6m },
                new RubricCriterion { CriterionName = "Example", Description = "Give examples", MaxScore = 4, Weight = .4m, OrderIndex = 2 }] };
            Source = new ExamQuestion { CourseId = Course.Id, CloId = Clo.Id, Title = "Question", Content = "Explain dependency injection",
                SampleAnswer = "A complete model answer with practical examples and a clear explanation.", Rubric = rubric, RubricId = rubric.Id,
                Difficulty = "medium", BloomLevel = "Apply", SubmittedBy = Author.Id, IsActive = false };
            Db.ExamQuestions.Add(Source);
            await Db.SaveChangesAsync();
            (await Workflow.Handle(new(Session.Id, Officer.Id, Officer.Role, PreparationAction.Request,
                Request: new(quota, DateTime.UtcNow.AddDays(10))), default)).IsSuccess.Should().BeTrue();
            (await Workflow.Handle(new(Session.Id, Head.Id, Head.Role, PreparationAction.Configure,
                Blueprint: new("Blueprint", 1, 30, 0, 1, 0, [Clo.Id])), default)).IsSuccess.Should().BeTrue();
            StructureId = Session.ExamStructureId!.Value;
            (await Workflow.Handle(new(Session.Id, Head.Id, Head.Role, PreparationAction.Assign,
                Assignment: new(StructureId, Author.Id, Math.Max(2, quota), DateTime.UtcNow.AddDays(5))), default)).IsSuccess.Should().BeTrue();
            AssignmentId = await Db.ExamAuthoringAssignments.Select(x => x.Id).SingleAsync();
        }
        public async Task<ExamSet> Create(string code = "SET01", List<Guid>? ids = null)
        {
            var result = await new CreateExamSetCommandHandler(Db).Handle(new(StructureId, Course.Id, Session.Id, code, ids ?? [Source.Id], Author.Id, Author.Role, AssignmentId), default);
            result.IsSuccess.Should().BeTrue(result.Error);
            return await Db.ExamSets.Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).ThenInclude(x => x.Rubric).ThenInclude(x => x.Criteria).SingleAsync(x => x.Id == result.Value.Id);
        }
        public Task<OralExamination.Application.Common.Models.Result> Submit(ExamSet s) => new SubmitExamSetForReviewCommandHandler(Db).Handle(new(s.Id, Author.Id, Author.Role), default);
        public async Task Approve(ExamSet s)
        {
            (await Submit(s)).IsSuccess.Should().BeTrue();
            var result = await new ApproveExamSetCommandHandler(Db).Handle(new(s.Id, "Approved", Head.Id, Head.Role), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task FullWorkflow_PreservesDraftsRevisionsAndBatches()
    {
        await using var s = new Scenario(); await s.Initialize(2);
        var one = await s.Create(); var two = await s.Create("SET02");
        one.SessionId.Should().Be(s.Session.Id);
        one.ExamSetQuestions.Single().ExamQuestionId.Should().NotBe(s.Source.Id);
        (await s.Submit(one)).IsSuccess.Should().BeTrue();
        (await s.Workflow.Handle(new(one.Id, s.Head.Id, s.Head.Role, PreparationAction.RequestChanges, Review: new("Clarify the answer")), default)).IsSuccess.Should().BeTrue();
        (await s.Submit(one)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(one.Id, s.Author.Id, s.Author.Role, PreparationAction.SaveDraft, Draft: new([s.Source.Id])), default)).IsSuccess.Should().BeTrue();
        one.Status.Should().Be(ExamSetStatus.Draft); one.ReviewNotes.Should().BeNull();
        await s.Approve(one);
        var insufficient = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([one.Id])), default);
        insufficient.IsSuccess.Should().BeFalse(); one.Status.Should().Be(ExamSetStatus.Official);
        await s.Approve(two);
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([one.Id, two.Id])), default)).IsSuccess.Should().BeTrue();
        one.Status.Should().Be(ExamSetStatus.HandedOver); two.Status.Should().Be(ExamSetStatus.HandedOver);
        (await s.Db.ExamPreparations.SingleAsync()).HandedOverAt.Should().NotBeNull();
        var events = await s.Db.ExamSetRevisions.Where(x => x.ExamSetId == one.Id).OrderBy(x => x.Sequence).ToListAsync();
        events.Select(x => x.Action).Should().ContainInOrder("CREATED", "SUBMITTED_FOR_REVIEW", "NEEDS_REVISION", "DRAFT_SAVED", "SUBMITTED_FOR_REVIEW", "OFFICIAL", "HANDED_OVER");
        events.Should().Contain(x => x.Notes == "Clarify the answer");
        events.Last().Snapshot.Should().Contain("Complete grading").And.Contain("Explain the concept");
    }

    [Fact]
    public async Task DraftCanBeEmpty_ButSubmissionNeedsCompleteBlueprint()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create(ids: []);
        (await s.Submit(set)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(set.Id, s.Author.Id, s.Author.Role, PreparationAction.SaveDraft, Draft: new([s.Source.Id])), default)).IsSuccess.Should().BeTrue();
        (await s.Submit(set)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("difficulty")]
    [InlineData("answer")]
    [InlineData("rubric")]
    [InlineData("clo")]
    public async Task InvalidCompositionCannotSubmit(string invalid)
    {
        await using var s = new Scenario(); await s.Initialize();
        if (invalid == "difficulty") s.Source.Difficulty = "hard";
        if (invalid == "answer") s.Source.SampleAnswer = "";
        if (invalid == "rubric") s.Source.Rubric.Criteria.First().MaxScore = 3;
        if (invalid == "clo") s.Source.CloId = null;
        await s.Db.SaveChangesAsync();
        var set = await s.Create();
        (await s.Submit(set)).IsSuccess.Should().BeFalse(); set.Status.Should().Be(ExamSetStatus.Draft);
    }

    [Fact]
    public async Task BloomIsIndependentOfDifficultyAndCloTarget()
    {
        await using var s = new Scenario(); await s.Initialize();
        var set = await s.Create();
        set.ExamSetQuestions.Single().ExamQuestion.BloomLevel.Should().Be("Apply");
        s.Clo.BloomLevel.Should().Be("Analyze");
        (await s.Submit(set)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CopiesAndRubricsRemainUnchangedWhenBankChanges_AndCopiesAreProtected()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        var copy = set.ExamSetQuestions.Single().ExamQuestion;
        s.Source.Title = "Edited bank title"; s.Source.Rubric.Name = "Edited rubric";
        await s.Db.SaveChangesAsync();
        copy.Title.Should().Be("Question"); copy.Rubric.Name.Should().Be("Rubric");
        copy.Content = "Unexpected mutation";
        await s.Db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
    }

    [Fact]
    public async Task CannotApproveDraft_OrResubmitRejectedWithoutSaving()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        (await new ApproveExamSetCommandHandler(s.Db).Handle(new(set.Id, null, s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeFalse();
        (await s.Submit(set)).IsSuccess.Should().BeTrue();
        (await s.Workflow.Handle(new(set.Id, s.Head.Id, s.Head.Role, PreparationAction.Reject, Review: new("Wrong scope")), default)).IsSuccess.Should().BeTrue();
        (await s.Submit(set)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(set.Id, s.Author.Id, s.Author.Role, PreparationAction.SaveDraft, Draft: new([s.Source.Id])), default)).IsSuccess.Should().BeTrue();
        (await s.Submit(set)).IsSuccess.Should().BeTrue(); set.ReviewNotes.Should().BeNull();
    }

    [Fact]
    public async Task AuthoringIsScopedToAssignment_AndAllowsExtraDrafts()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        var foreign = new User { Email = "foreign@test.local", FullName = "Foreign", Role = UserRole.Lecturer }; s.Db.Users.Add(foreign); await s.Db.SaveChangesAsync();
        (await new SubmitExamSetForReviewCommandHandler(s.Db).Handle(new(set.Id, foreign.Id, foreign.Role), default)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(set.Id, foreign.Id, foreign.Role, PreparationAction.SaveDraft, Draft: new([])), default)).IsSuccess.Should().BeFalse();
        (await new GetExamPreparationQueryHandler(s.Db).Handle(new(null, foreign.Id, foreign.Role, set.Id), default)).IsSuccess.Should().BeFalse();
        await s.Create("SET02");
        (await new CreateExamSetCommandHandler(s.Db).Handle(new(s.StructureId, s.Course.Id, s.Session.Id, "SET03", [], s.Author.Id, s.Author.Role, s.AssignmentId), default)).IsSuccess.Should().BeTrue();
        (await new CreateExamSetCommandHandler(s.Db).Handle(new(s.StructureId, s.Course.Id, null, "SET04", [], s.Author.Id, s.Author.Role), default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task BatchSubmission_RequiresQuota_AndPublishesAllSelectedDraftsAtomically()
    {
        await using var s = new Scenario(); await s.Initialize();
        var one = await s.Create();
        var handler = new SubmitExamAssignmentCommandHandler(s.Db);
        var insufficient = await handler.Handle(new(s.AssignmentId, [one.Id], s.Author.Id, s.Author.Role), default);
        insufficient.IsSuccess.Should().BeFalse();
        one.Status.Should().Be(ExamSetStatus.Draft);
        var incomplete = await s.Create("EMPTY", []);
        (await handler.Handle(new(s.AssignmentId, [one.Id, incomplete.Id], s.Author.Id, s.Author.Role), default)).IsSuccess.Should().BeFalse();
        one.Status.Should().Be(ExamSetStatus.Draft);
        var two = await s.Create("SET02");
        var success = await handler.Handle(new(s.AssignmentId, [one.Id, two.Id], s.Author.Id, s.Author.Role), default);
        success.IsSuccess.Should().BeTrue(success.Error);
        one.Status.Should().Be(ExamSetStatus.SubmittedForReview);
        two.Status.Should().Be(ExamSetStatus.SubmittedForReview);
        incomplete.Status.Should().Be(ExamSetStatus.Draft);
        (await handler.Handle(new(s.AssignmentId, [one.Id, two.Id], s.Author.Id, s.Author.Role), default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task BatchSubmission_RejectsForeignActor_AndAllowsResubmissionWithAlreadyApprovedCredit()
    {
        await using var s = new Scenario(); await s.Initialize(); var one=await s.Create(); var two=await s.Create("SET02");
        var handler = new SubmitExamAssignmentCommandHandler(s.Db);
        (await handler.Handle(new(s.AssignmentId, [one.Id,two.Id], s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeFalse();
        await s.Approve(one);
        (await handler.Handle(new(s.AssignmentId,[two.Id],s.Author.Id,s.Author.Role),default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BankOriginIsStable_WhenSourceChangesAndTheDraftIsSavedAgain()
    {
        await using var s = new Scenario(); await s.Initialize();
        s.Source.ApprovalStatus=ApprovalStatus.Approved; s.Source.IsActive=true; await s.Db.SaveChangesAsync();
        var paper=await s.Create();
        var member=paper.ExamSetQuestions.Single();
        member.IsBankQuestion.Should().BeTrue();
        s.Source.ApprovalStatus=ApprovalStatus.Rejected; await s.Db.SaveChangesAsync();
        var saved=await s.Workflow.Handle(new(paper.Id,s.Author.Id,s.Author.Role,PreparationAction.SaveDraft,Draft:new([member.ExamQuestionId])),default);
        saved.IsSuccess.Should().BeTrue(saved.Error);
        paper.ExamSetQuestions.Single().IsBankQuestion.Should().BeTrue();
    }

    [Fact]
    public async Task BlueprintVersionsDoNotChangeExistingAssignments()
    {
        await using var s = new Scenario(); await s.Initialize(); var original = s.StructureId;
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.Configure,
            Blueprint: new("Version two", 2, 40, 1, 1, 0, [s.Clo.Id])), default)).IsSuccess.Should().BeTrue();
        s.Session.ExamStructureId.Should().NotBe(original);
        (await s.Db.ExamAuthoringAssignments.SingleAsync()).StructureId.Should().Be(original);
        var set = await s.Create(); (await s.Submit(set)).IsSuccess.Should().BeTrue();
        (await new ApproveExamSetCommandHandler(s.Db).Handle(new(set.Id, null, s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeTrue();
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([set.Id])), default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateScopeIncludesAllCurrentEventsAndNearestPreviousSemesterOnly()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        var previous = new Semester { Code = "SU26", Name = "Summer", StartDate = new(2026,5,1), EndDate = new(2026,8,31) };
        var old = new Semester { Code = "SP26", Name = "Spring", StartDate = new(2026,1,1), EndDate = new(2026,4,30) };
        s.Db.Semesters.AddRange(previous, old);
        foreach (var (code, semester, status, sample) in new[] {
            ("CURRENT_FE", s.Session.SemesterId, ExamSetStatus.Official, false),
            ("CURRENT_RE", s.Session.SemesterId, ExamSetStatus.HandedOver, false),
            ("PREVIOUS", previous.Id, ExamSetStatus.Official, false),
            ("OLD", old.Id, ExamSetStatus.Official, false),
            ("PENDING", s.Session.SemesterId, ExamSetStatus.SubmittedForReview, false),
            ("SAMPLE", s.Session.SemesterId, ExamSetStatus.Official, true) })
        {
            var session = new OfficialExamSession { CourseId = s.Course.Id, SemesterId = semester, Title = code, ExamAttemptType = code == "CURRENT_RE" ? "RE" : "FE" };
            s.Db.OfficialExamSessions.Add(session);
            s.Db.ExamSets.Add(new ExamSet { CourseId = s.Course.Id, StructureId = s.StructureId, SessionId = session.Id, SetCode = code, Status = status, IsSampleSet = sample,
                ExamSetQuestions = [new ExamSetQuestion { ExamQuestionId = s.Source.Id, SourceQuestionId = s.Source.Id }] });
        }
        await s.Db.SaveChangesAsync();
        var duplicates = await ExamPaperRules.Duplicates(s.Db, set, default);
        duplicates.Select(x => x.SetCode).Should().BeEquivalentTo("CURRENT_FE", "CURRENT_RE", "PREVIOUS");
        duplicates.Should().Contain(x => x.SemesterCode == "SU26" && x.SessionTitle == "PREVIOUS");
        await s.Approve(set); set.Status.Should().Be(ExamSetStatus.Official);
    }

    [Fact]
    public async Task HistoryAndHandedOverPaperAreImmutable()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create(); await s.Approve(set);
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([set.Id])), default)).IsSuccess.Should().BeTrue();
        (await s.Workflow.Handle(new(set.Id, s.Author.Id, s.Author.Role, PreparationAction.SaveDraft, Draft: new([])), default)).IsSuccess.Should().BeFalse();
        set.SetCode = "MUTATED";
        await s.Db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
    }
    [Fact]
    public async Task PrivatePaperQuestionsAndRubricsAreExcludedFromBankEndpoints()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        var copy = set.ExamSetQuestions.Single().ExamQuestion;
        var rubrics = await new OralExamination.Application.Features.QuestionBank.Queries.GetRubrics.GetRubricsQueryHandler(s.Db)
            .Handle(new(s.Course.Id, ActorUserId: s.Author.Id, ActorRole: s.Author.Role), default);
        rubrics.IsSuccess.Should().BeTrue();
        rubrics.Value.Should().NotContain(x => x.Id == copy.RubricId);
        var questions = await new OralExamination.Application.Features.QuestionBank.Queries.GetQuestions.GetQuestionsQueryHandler(s.Db)
            .Handle(new(CourseId: s.Course.Id, ActorId: s.Author.Id, ActorRole: s.Author.Role, UsageScope: "exam"), default);
        questions.IsSuccess.Should().BeTrue();
        questions.Value.Items.Should().NotContain(x => x.Id == copy.Id);
        var history = await new GetExamPreparationQueryHandler(s.Db).Handle(new(null, s.Author.Id, s.Author.Role, set.Id), default);
        history.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MixedSessionDeliveryFailsWithoutChangingApprovedPapers()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create(); await s.Approve(set);
        var otherSession = new OfficialExamSession { CourseId = s.Course.Id, SemesterId = s.Session.SemesterId, Title = "Retake", ExamAttemptType = "RE" };
        s.Db.OfficialExamSessions.Add(otherSession);
        s.Db.ExamSets.Add(new ExamSet { CourseId = s.Course.Id, StructureId = s.StructureId, SessionId = otherSession.Id, SetCode = "FOREIGN", Status = ExamSetStatus.Official });
        await s.Db.SaveChangesAsync();
        var other = await s.Db.ExamSets.SingleAsync(x => x.SetCode == "FOREIGN");
        var result = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([set.Id, other.Id])), default);
        result.IsSuccess.Should().BeFalse(); set.Status.Should().Be(ExamSetStatus.Official);
        (await s.Db.ExamPreparations.SingleAsync()).HandedOverAt.Should().BeNull();
    }

    [Fact]
    public async Task StartedSessionCannotChangeItsPapersOrBlueprint()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        s.Session.Status = "in_progress"; await s.Db.SaveChangesAsync();
        (await s.Submit(set)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.Configure,
            Blueprint: new("Changed", 1, 30, 0, 1, 0, [s.Clo.Id])), default)).IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public async Task ApprovalNotesRespectDatabaseLimit(int length, bool success)
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        (await s.Submit(set)).IsSuccess.Should().BeTrue();
        var result = await new ApproveExamSetCommandHandler(s.Db).Handle(new(set.Id, new string('x', length), s.Head.Id, s.Head.Role), default);
        result.IsSuccess.Should().Be(success);
        set.Status.Should().Be(success ? ExamSetStatus.Official : ExamSetStatus.SubmittedForReview);
    }

    [Fact]
    public async Task CompletedPreparationRejectsFurtherDecisionsOnExtraPapers()
    {
        await using var s = new Scenario(); await s.Initialize();
        var selected = await s.Create(); var extra = await s.Create("EXTRA");
        (await s.Submit(extra)).IsSuccess.Should().BeTrue(); await s.Approve(selected);
        (await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.HandOver, Delivery: new([selected.Id])), default)).IsSuccess.Should().BeTrue();
        (await new ApproveExamSetCommandHandler(s.Db).Handle(new(extra.Id, null, s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(extra.Id, s.Head.Id, s.Head.Role, PreparationAction.RequestChanges, Review: new("Review again")), default)).IsSuccess.Should().BeFalse();
        extra.Status.Should().Be(ExamSetStatus.SubmittedForReview);
    }

    [Fact]
    public async Task SavedHistoryCannotBeRewritten()
    {
        await using var s = new Scenario(); await s.Initialize(); await s.Create();
        (await s.Db.ExamSetRevisions.SingleAsync()).Snapshot = "{}";
        await s.Db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
    }

    [Fact]
    public async Task PrivateRubricCriteriaCannotBeRewritten()
    {
        await using var s = new Scenario(); await s.Initialize(); var set = await s.Create();
        set.ExamSetQuestions.Single().ExamQuestion.Rubric.Criteria.First().Description = "Unexpected edit";
        await s.Db.Invoking(x => x.SaveChangesAsync()).Should().ThrowAsync<OneWayLockException>();
    }

}

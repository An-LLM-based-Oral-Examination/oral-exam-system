using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.Queries;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSetById;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public class DuplicateQuestionAndExamSetTests
{
    [Fact]
    public async Task BankAvailabilityIncludesOwnDraftsInThisSessionAndExcludesTheOpenDraftOnly()
    {
        await using var s = new Scenario(); await s.Initialize(2);
        s.Source.IsActive = true; s.Source.ApprovalStatus = ApprovalStatus.Approved;
        var open = await s.Create("OPEN");
        var own = await s.Create("OWN");
        ExamSet Add(string code) {
            var set = new ExamSet { SetCode = code, CourseId = s.Course.Id, StructureId = s.StructureId,
                SessionId = s.Session.Id, CreatedBy = s.Author.Id, Status = ExamSetStatus.Draft };
            set.ExamSetQuestions.Add(new() { ExamSetId = set.Id, ExamSet = set, ExamQuestionId = s.Source.Id,
                ExamQuestion = s.Source, SourceQuestionId = s.Source.Id, OrderIndex = 1 });
            s.Db.ExamSets.Add(set); return set;
        }
        Add("FOREIGN").CreatedBy = Guid.NewGuid();
        Add("OTHER-SESSION").SessionId = Guid.NewGuid();
        Add("INACTIVE").IsActive = false;
        Add("SAMPLE").IsSampleSet = true;
        Add("REJECTED").Status = ExamSetStatus.Rejected;
        await s.Db.SaveChangesAsync();
        var query = new OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions.GetAvailableExamSetQuestionsQueryHandler(s.Db);
        var result = await query.Handle(new(s.StructureId, s.Author.Id, s.Author.Role,
            SessionId: s.Session.Id, ExcludeExamSetId: open.Id), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value.Items.Single(x => x.QuestionId == s.Source.Id);
        row.UsedCount.Should().Be(1); row.CanSelect.Should().BeFalse();
        (await ExamReuseRules.Counts(s.Db, s.Session.Id, default)).Should().BeEmpty();
        var withoutExclusion = await query.Handle(new(s.StructureId, s.Author.Id, s.Author.Role, SessionId: s.Session.Id), default);
        withoutExclusion.Value.Items.Single(x => x.QuestionId == s.Source.Id).UsedCount.Should().Be(2);
        (await s.Submit(own)).IsSuccess.Should().BeTrue();
        // The exclusion only applies to drafts; submitted use cannot be hidden by its ID.
        var committed = await query.Handle(new(s.StructureId, s.Author.Id, s.Author.Role,
            SessionId: s.Session.Id, ExcludeExamSetId: own.Id), default);
        committed.Value.Items.Single(x => x.QuestionId == s.Source.Id).UsedCount.Should().Be(2);
    }

    [Fact]
    public async Task HeadOnlySeesSubmittedPapers_AndLosesAccessWhileAuthorEditsDraft()
    {
        await using var s = new Scenario(); await s.Initialize();
        var paper = await s.Create();
        var list = new GetExamSetsQueryHandler(s.Db);
        var detail = new GetExamSetByIdQueryHandler(s.Db);
        async Task AssertHidden()
        {
            var hidden = await list.Handle(new(s.Course.Id, s.Session.Id, ActorUserId: s.Head.Id, ActorRole: s.Head.Role, IncludeInactive: true), default);
            hidden.IsSuccess.Should().BeTrue(); hidden.Value.Should().BeEmpty();
            (await detail.Handle(new(paper.Id, s.Head.Id, s.Head.Role, true), default)).Error.Should().Be(OfficialExamErrors.Forbidden);
            foreach (var kind in new[] { "similarity", "bank" })
                (await new ExamPaperAnalysisQueryHandler(s.Db).Handle(new(paper.Id, s.Head.Id, s.Head.Role, kind), default)).IsSuccess.Should().BeFalse();
            foreach (var duplicates in new[] { false, true })
                (await new GetExamPreparationQueryHandler(s.Db).Handle(new(null, s.Head.Id, s.Head.Role, paper.Id, duplicates), default)).IsSuccess.Should().BeFalse();
            (await detail.Handle(new(paper.Id, s.Author.Id, s.Author.Role), default)).IsSuccess.Should().BeTrue();
        }
        await AssertHidden();
        (await s.Submit(paper)).IsSuccess.Should().BeTrue();
        (await detail.Handle(new(paper.Id, s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeTrue();
        var visible = await list.Handle(new(s.Course.Id, s.Session.Id, ActorUserId: s.Head.Id, ActorRole: s.Head.Role), default);
        visible.Value.Should().ContainSingle(x => x.Id == paper.Id);
        (await s.Workflow.Handle(new(paper.Id, s.Head.Id, s.Head.Role, PreparationAction.RequestChanges, Review: new("Revise answer")), default)).IsSuccess.Should().BeTrue();
        (await detail.Handle(new(paper.Id, s.Head.Id, s.Head.Role), default)).IsSuccess.Should().BeTrue();
        (await s.Workflow.Handle(new(paper.Id, s.Author.Id, s.Author.Role, PreparationAction.SaveDraft, Draft: new([s.Source.Id])), default)).IsSuccess.Should().BeTrue();
        await AssertHidden();
    }
    [Fact]
    public async Task ReuseQuota_CountsBatchAndReleasesWhenChangesRequested()
    {
        await using var s = new Scenario(); await s.Initialize(2);
        s.Source.IsActive = true; s.Source.ApprovalStatus = ApprovalStatus.Approved; await s.Db.SaveChangesAsync();
        var one = await s.Create(); var two = await s.Create("REUSE02");
        var batch = await new SubmitExamAssignmentCommandHandler(s.Db).Handle(new(s.AssignmentId, [one.Id, two.Id], s.Author.Id, s.Author.Role), default);
        batch.IsSuccess.Should().BeFalse(); batch.Error.Should().Contain("vượt giới hạn");
        batch.Error.Should().Contain(one.SetCode).And.Contain(two.SetCode);
        one.Status.Should().Be(ExamSetStatus.Draft); two.Status.Should().Be(ExamSetStatus.Draft);
        (await s.Submit(one)).IsSuccess.Should().BeTrue();
        (await s.Submit(two)).IsSuccess.Should().BeFalse();
        (await s.Workflow.Handle(new(one.Id, s.Head.Id, s.Head.Role, PreparationAction.RequestChanges, Review: new("Change answer")), default)).IsSuccess.Should().BeTrue();
        (await s.Submit(two)).IsSuccess.Should().BeTrue();
        (await ExamReuseRules.Counts(s.Db, s.Session.Id, default))[s.Source.Id].Should().Be(1);
        var available = await new OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions.GetAvailableExamSetQuestionsQueryHandler(s.Db)
            .Handle(new(s.StructureId, s.Author.Id, s.Author.Role, SessionId: s.Session.Id), default);
        available.IsSuccess.Should().BeTrue(available.Error);
        var row = available.Value.Items.Single(x => x.QuestionId == s.Source.Id);
        row.CanSelect.Should().BeFalse(); row.UsedCount.Should().Be(1); row.MaxUses.Should().Be(1);
    }

    [Fact]
    public async Task Similarity_RecognizesSameSourceAndSimilarContentAcrossDifferentStructures()
    {
        await using var s = new Scenario(); await s.Initialize(10);
        var first = await s.Create(); await s.Approve(first);
        var same = await s.Create("SAME");
        var sameReport = await ExamSimilarityRules.Report(s.Db, same.Id, default);
        sameReport.SimilarityPercent.Should().Be(100);
        sameReport.Questions.Single().Matches.First().SameSource.Should().BeTrue();
        var otherId = await s.NewSource();
        var other = await s.Db.ExamQuestions.SingleAsync(q => q.Id == otherId);
        other.Title = s.Source.Title; other.Content = s.Source.Content; await s.Db.SaveChangesAsync();
        var differentSource = await s.Create("TEXT", [otherId]);
        var report = await ExamSimilarityRules.Report(s.Db, differentSource.Id, default);
        report.Questions.Single().Percent.Should().Be(100);
        report.Questions.Single().Matches.First().SameSource.Should().BeFalse();
        var denied = await new ExamPaperAnalysisQueryHandler(s.Db).Handle(new(differentSource.Id, s.Author.Id, s.Author.Role, "similarity"), default);
        denied.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Capacity_PreviewsShortageAndDoesNotPersistConfiguration()
    {
        await using var s = new Scenario(); await s.Initialize(10);
        s.Source.IsActive = true; s.Source.ApprovalStatus = ApprovalStatus.Approved; await s.Db.SaveChangesAsync();
        var result = await new ExamPaperAnalysisQueryHandler(s.Db).Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, "capacity",
            new("Preview", 1, 30, 0, 1, 0, [s.Clo.Id], 30)), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        var capacity = (ExamCapacityReport)result.Value;
        capacity.MaxUses.Should().Be(3);
        capacity.AdditionalQuestionsRequired.Should().Be(3);
        capacity.Warning.Should().Contain("thiếu ít nhất 3 câu hỏi");
        System.Text.Json.JsonSerializer.Serialize(capacity).Should().NotContain("Difficulties");
        capacity.SuggestedReusePercent.Should().BeNull();
        var mixed = await new ExamPaperAnalysisQueryHandler(s.Db).Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, "capacity",
            new("Preview", 3, 30, 1, 1, 1, [s.Clo.Id], 30)), default);
        ((ExamCapacityReport)mixed.Value).AdditionalQuestionsRequired.Should().Be(11);
        (await s.Db.ExamPreparations.SingleAsync()).ReusePercent.Should().Be(30);
    }

    [Fact]
    public async Task Promotion_IsIdempotentCopiesFullRubricAndPreservesCanonicalSource()
    {
        await using var s = new Scenario(); await s.Initialize();
        var paper = await s.Create(); await s.Approve(paper);
        var item = paper.ExamSetQuestions.Single();
        (await PaperBankRules.Candidates(s.Db, paper.Id, default)).Should().ContainSingle();
        var handler = new PromotePaperQuestionsCommandHandler(s.Db);
        var command = new PromotePaperQuestionsCommand(paper.Id, [item.ExamQuestionId], s.Head.Id, s.Head.Role);
        (await handler.Handle(command, default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(command, default)).IsSuccess.Should().BeTrue();
        var bank = await s.Db.ExamQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria).SingleAsync(q => q.OriginQuestionId == s.Source.Id);
        bank.IsPaperCopy.Should().BeFalse(); bank.IsActive.Should().BeTrue();
        bank.RubricId.Should().NotBe(item.ExamQuestion.RubricId); bank.Rubric.Criteria.Should().HaveCount(2);
        bank.SampleAnswer.Should().Be(item.ExamQuestion.SampleAnswer);
        var next = await s.Create("BANK", [bank.Id]);
        next.ExamSetQuestions.Single().SourceQuestionId.Should().Be(s.Source.Id);
        (await PaperBankRules.Candidates(s.Db, paper.Id, default)).Should().BeEmpty();
        (await s.Submit(next)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ReuseConfiguration_RejectsReductionBelowOccupiedUsage()
    {
        await using var s = new Scenario(); await s.Initialize(10);
        var first = await s.Create(); var second = await s.Create("SECOND");
        (await s.Submit(first)).IsSuccess.Should().BeTrue(); (await s.Submit(second)).IsSuccess.Should().BeTrue();
        var reduction = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.Configure,
            Blueprint: new("New", 1, 30, 0, 1, 0, [s.Clo.Id], 10)), default);
        reduction.IsSuccess.Should().BeFalse();
        (await s.Db.ExamPreparations.SingleAsync()).ReusePercent.Should().Be(30);
        var original = s.Session.ExamStructureId;
        var updated = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.UpdateReuse, ReusePercent: 40), default);
        updated.IsSuccess.Should().BeTrue(updated.Error);
        s.Session.ExamStructureId.Should().Be(original);
        (await s.Db.ExamPreparations.SingleAsync()).ReusePercent.Should().Be(40);
    }

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
        public async Task<Guid> NewSource()
        {
            var q = new ExamQuestion { CourseId = Source.CourseId, CloId = Source.CloId, Title = "Another question", Content = "Explain service lifetimes",
                SampleAnswer = Source.SampleAnswer, RubricId = Source.RubricId, Difficulty = Source.Difficulty, BloomLevel = Source.BloomLevel,
                SubmittedBy = Author.Id, IsActive = false };
            Db.ExamQuestions.Add(q); await Db.SaveChangesAsync(); return q.Id;
        }
        public async Task Approve(ExamSet s)
        {
            (await Submit(s)).IsSuccess.Should().BeTrue();
            var result = await new ApproveExamSetCommandHandler(Db).Handle(new(s.Id, "Approved", Head.Id, Head.Role), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task BatchAssignment_ValidatesAllRowsBeforeSaving_AndPreservesExistingAssignments()
    {
        await using var s = new Scenario(); await s.Initialize();
        var second = new User { Email = "second@test.local", FullName = "Second", Role = UserRole.Lecturer };
        var third = new User { Email = "third@test.local", FullName = "Third", Role = UserRole.Lecturer };
        s.Db.Users.AddRange(second, third);
        foreach (var user in new[] { second, third })
            s.Db.CourseLecturers.Add(new CourseLecturer { CourseId = s.Course.Id, SemesterId = s.Session.SemesterId, LecturerUserId = user.Id, AssignedByUserId = s.Head.Id });
        await s.Db.SaveChangesAsync();
        var invalid = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.AssignBatch,
            Assignments: new(s.StructureId, [new(second.Id, 2), new(Guid.NewGuid(), 1)], DateTime.UtcNow.AddDays(3))), default);
        invalid.IsSuccess.Should().BeFalse();
        (await s.Db.ExamAuthoringAssignments.CountAsync()).Should().Be(1);
        var valid = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.AssignBatch,
            Assignments: new(s.StructureId, [new(second.Id, 2), new(third.Id, 3)], DateTime.UtcNow.AddDays(3), "Shared notes")), default);
        valid.IsSuccess.Should().BeTrue(valid.Error);
        (await s.Db.ExamAuthoringAssignments.CountAsync()).Should().Be(3);
        var repeated = await s.Workflow.Handle(new(s.Session.Id, s.Head.Id, s.Head.Role, PreparationAction.AssignBatch,
            Assignments: new(s.StructureId, [new(second.Id, 4)], DateTime.UtcNow.AddDays(3))), default);
        repeated.IsSuccess.Should().BeFalse();
        (await s.Db.ExamAuthoringAssignments.SingleAsync(x => x.LecturerId == second.Id)).SetCount.Should().Be(2);
    }

    [Fact]
    public async Task FullWorkflow_PreservesDraftsRevisionsAndBatches()
    {
        await using var s = new Scenario(); await s.Initialize(2);
        var one = await s.Create(); var two = await s.Create("SET02", [await s.NewSource()]);
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
        var two = await s.Create("SET02", [await s.NewSource()]);
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
        await using var s = new Scenario(); await s.Initialize(); var one=await s.Create(); var two=await s.Create("SET02", [await s.NewSource()]);
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
        var similarity = await ExamSimilarityRules.Report(s.Db, set.Id, default);
        similarity.ComparedPaper!.SetCode.Should().Be("CURRENT_FE");
        similarity.Questions.Single().Matches.Should().ContainSingle();
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
        var selected = await s.Create(); var extra = await s.Create("EXTRA", [await s.NewSource()]);
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

    [Fact]
    public async Task LegacyMutationRoutesCannotBypassAssignedPaperHistory()
    {
        await using var s = new Scenario(); await s.Initialize(); var paper = await s.Create();
        var update = await new OralExamination.Application.Features.OfficialExams.Commands.UpdateExamSet.UpdateExamSetCommandHandler(s.Db)
            .Handle(new(paper.Id, "CHANGED", [s.Source.Id], s.Author.Id, s.Author.Role), default);
        update.IsSuccess.Should().BeFalse(); paper.SetCode.Should().Be("SET01");
        (await s.Submit(paper)).IsSuccess.Should().BeTrue();
        var revisions = await s.Db.ExamSetRevisions.CountAsync();
        var returned = await new OralExamination.Application.Features.OfficialExams.Commands.ReturnExamSetForRevision.ReturnExamSetForRevisionCommandHandler(s.Db)
            .Handle(new(paper.Id, "Revise explanation", s.Head.Id, s.Head.Role), default);
        returned.IsSuccess.Should().BeFalse(); paper.Status.Should().Be(ExamSetStatus.SubmittedForReview);
        (await s.Db.ExamSetRevisions.CountAsync()).Should().Be(revisions);
    }

    [Fact]
    public async Task MissingAssignmentCannotFallBackToLegacyCreationForConfiguredSession()
    {
        await using var s = new Scenario(); await s.Initialize();
        var legacyStructure = new ExamStructure { CourseId = s.Course.Id, Name = "Legacy" };
        s.Db.ExamStructures.Add(legacyStructure); await s.Db.SaveChangesAsync();
        var result = await new CreateExamSetCommandHandler(s.Db).Handle(new(legacyStructure.Id, s.Course.Id,
            s.Session.Id, "BYPASS", [s.Source.Id], s.Author.Id, s.Author.Role), default);
        result.IsSuccess.Should().BeFalse();
        (await s.Db.ExamSets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StructureReadContractPreservesDifficultyCloAndNewLegacyFields()
    {
        await using var s = new Scenario(); await s.Initialize();
        var result = await new OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures.GetExamStructuresQueryHandler(s.Db)
            .Handle(new(CourseId:s.Course.Id, ActorUserId:s.Head.Id, ActorRole:s.Head.Role), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value.Single();
        dto.EasyCount.Should().Be(0); dto.MediumCount.Should().Be(1); dto.HardCount.Should().Be(0);
        dto.CloIds.Should().ContainSingle().Which.Should().Be(s.Clo.Id);
        dto.RequiredSetCount.Should().Be(1); dto.SlotDefinitions.Should().Be("[]");
    }

}

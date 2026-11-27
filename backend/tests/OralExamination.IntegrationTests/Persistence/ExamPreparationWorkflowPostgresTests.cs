using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.Commands.ApproveExamSet;
using OralExamination.Application.Features.OfficialExams.Queries;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class ExamPreparationWorkflowPostgresTests(PostgresFixture fixture)
{
    private sealed record Seed(Guid SessionId, Guid CourseId, Guid StructureId, Guid AssignmentId, Guid QuestionId, Guid HeadId, Guid AuthorId);
    private static async Task<Seed> Prepare(OralExamDbContext db, int count)
    {
        var a = await TestData.AcademicAsync(db);
        var suffix = Guid.NewGuid().ToString("N");
        var head = new User { Email = suffix + "@head.example.test", FullName = "Head", Role = UserRole.DepartmentHead };
        var officer = new User { Email = suffix + "@officer.example.test", FullName = "Officer", Role = UserRole.ExaminationOfficer };
        db.Users.AddRange(head, officer);
        a.Course.DepartmentHeadUserId = head.Id;
        db.CourseLecturers.Add(new CourseLecturer { CourseId = a.Course.Id, SemesterId = a.Course.SemesterId,
            LecturerUserId = a.Lecturer.Id, AssignedByUserId = head.Id });
        db.RubricCriteria.AddRange(new RubricCriterion { RubricId = a.Rubric.Id, CriterionName = "Concept", MaxScore = 6, Weight = .6m },
            new RubricCriterion { RubricId = a.Rubric.Id, CriterionName = "Example", MaxScore = 4, Weight = .4m, OrderIndex = 2 });
        var question = new ExamQuestion { CourseId = a.Course.Id, CloId = a.Clo.Id, RubricId = a.Rubric.Id, Title = "Question", Content = "Explain transactions",
            SampleAnswer = "Explain the transaction boundary and give a complete example.", SubmittedBy = a.Lecturer.Id, Difficulty = "medium", BloomLevel = "Apply", IsActive = false };
        db.ExamQuestions.Add(question);
        var session = new OfficialExamSession { CourseId = a.Course.Id, SemesterId = a.Course.SemesterId, Title = "Final", ExamDate = new(2026,12,20) };
        db.OfficialExamSessions.Add(session);
        await db.SaveChangesAsync();
        var flow = new ExamPreparationCommandHandler(db);
        var request = await flow.Handle(new(session.Id, officer.Id, officer.Role, PreparationAction.Request, Request: new(count, DateTime.UtcNow.AddDays(10))), default);
        request.IsSuccess.Should().BeTrue(request.Error);
        var blueprint = await flow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.Configure, Blueprint: new("Blueprint", 1, 30, 0, 1, 0, [a.Clo.Id])), default);
        blueprint.IsSuccess.Should().BeTrue(blueprint.Error);
        var assigned = await flow.Handle(new(session.Id, head.Id, head.Role, PreparationAction.Assign,
            Assignment: new(session.ExamStructureId!.Value, a.Lecturer.Id, count, DateTime.UtcNow.AddDays(5))), default);
        assigned.IsSuccess.Should().BeTrue(assigned.Error);
        var assignment = await db.ExamAuthoringAssignments.SingleAsync(x => x.Preparation.SessionId == session.Id);
        return new(session.Id, a.Course.Id, assignment.StructureId, assignment.Id, question.Id, head.Id, a.Lecturer.Id);
    }

    [Fact]
    public async Task MigrationAndCompleteWorkflow_WorkOnPostgres_WithQueryableProgressAndHistory()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var db = database.CreateContext();
        var s = await Prepare(db, 2);
        var clo = await db.CourseClos.SingleAsync(x => x.CourseId == s.CourseId);
        var capacity = await new ExamPaperAnalysisQueryHandler(db).Handle(new(s.SessionId, s.HeadId, UserRole.DepartmentHead, "capacity",
            new("Preview", 1, 30, 0, 1, 0, [clo.Id], 30)), default);
        capacity.IsSuccess.Should().BeTrue(capacity.Error);
        var paperIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var questionId = s.QuestionId;
            if (i > 0)
            {
                var source = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.Id == s.QuestionId);
                var other = new ExamQuestion { CourseId = source.CourseId, CloId = source.CloId, RubricId = source.RubricId,
                    Title = source.Title, Content = source.Content, SampleAnswer = source.SampleAnswer, Difficulty = source.Difficulty,
                    BloomLevel = source.BloomLevel, SubmittedBy = s.AuthorId, IsActive = false };
                db.ExamQuestions.Add(other); await db.SaveChangesAsync(); questionId = other.Id;
            }
            var created = await new CreateExamSetCommandHandler(db).Handle(new(s.StructureId, s.CourseId, s.SessionId,
                "SET" + i, [questionId], s.AuthorId, UserRole.Lecturer, s.AssignmentId), default);
            created.IsSuccess.Should().BeTrue(created.Error);
            var id = created.Value.Id; paperIds.Add(id);
            (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(id, s.AuthorId, UserRole.Lecturer), default)).IsSuccess.Should().BeTrue();
            if (i == 0)
            {
                var flow = new ExamPreparationCommandHandler(db);
                (await flow.Handle(new(id, s.HeadId, UserRole.DepartmentHead, PreparationAction.RequestChanges, Review: new("Clarify the explanation")), default)).IsSuccess.Should().BeTrue();
                (await flow.Handle(new(id, s.AuthorId, UserRole.Lecturer, PreparationAction.SaveDraft, Draft: new([s.QuestionId])), default)).IsSuccess.Should().BeTrue();
                (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(id, s.AuthorId, UserRole.Lecturer), default)).IsSuccess.Should().BeTrue();
            }
            var approved = await new ApproveExamSetCommandHandler(db).Handle(new(id, "Approved", s.HeadId, UserRole.DepartmentHead), default);
            approved.IsSuccess.Should().BeTrue(approved.Error);
            approved.Value.HasDuplicates.Should().BeFalse();
            approved.Value.Similarity!.SimilarityPercent.Should().Be(i > 0 ? 100 : 0);
            var candidate = approved.Value.BankCandidates.Should().ContainSingle().Which;
            var promotion = new PromotePaperQuestionsCommandHandler(db);
            var promote = new PromotePaperQuestionsCommand(id, [candidate.QuestionId], s.HeadId, UserRole.DepartmentHead);
            (await promotion.Handle(promote, default)).IsSuccess.Should().BeTrue();
            (await promotion.Handle(promote, default)).IsSuccess.Should().BeTrue();
            var bank = await db.ExamQuestions.AsNoTracking().SingleAsync(q => q.OriginQuestionId == questionId);
            bank.IsPaperCopy.Should().BeFalse(); bank.IsActive.Should().BeTrue();
        }
        var delivery = await new ExamPreparationCommandHandler(db).Handle(new(s.SessionId, s.HeadId, UserRole.DepartmentHead,
            PreparationAction.HandOver, Delivery: new(paperIds)), default);
        delivery.IsSuccess.Should().BeTrue(delivery.Error);
        var progress = await new GetExamPreparationQueryHandler(db).Handle(new(s.SessionId, s.HeadId, UserRole.DepartmentHead), default);
        progress.IsSuccess.Should().BeTrue(progress.Error);
        (await new GetExamPreparationQueryHandler(db).Handle(new(null, s.AuthorId, UserRole.Lecturer, paperIds[0], Detail: true), default)).IsSuccess.Should().BeTrue();
        (await db.ExamSetRevisions.CountAsync(x => x.ExamSetId == paperIds[0])).Should().Be(7);
        (await db.ExamSets.CountAsync(x => x.SessionId == s.SessionId && x.Status == ExamSetStatus.HandedOver)).Should().Be(2);
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentDraftCreation_AllowsExtraSets_AndBatchSubmissionIsAtomic()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Seed s;
        await using (var seed = database.CreateContext()) s = await Prepare(seed, 1);
        async Task<bool> Create(string code)
        {
            await using var db = new OralExamDbContext(new DbContextOptionsBuilder<OralExamDbContext>()
                .UseNpgsql(database.ConnectionString, o => { o.EnableRetryOnFailure(); o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); }).Options);
            var result = await new CreateExamSetCommandHandler(db).Handle(new(s.StructureId, s.CourseId, s.SessionId,
                code, [s.QuestionId], s.AuthorId, UserRole.Lecturer, s.AssignmentId), default);
            return result.IsSuccess;
        }
        var results = await Task.WhenAll(Create("A"), Create("B"));
        results.Count(x => x).Should().Be(2);
        await using var verify = database.CreateContext();
        (await verify.ExamSets.CountAsync(x => x.AssignmentId == s.AssignmentId)).Should().Be(2);
        var ids = await verify.ExamSets.Where(x=>x.AssignmentId==s.AssignmentId).Select(x=>x.Id).ToListAsync();
        var submitted = await new SubmitExamAssignmentCommandHandler(verify).Handle(new(s.AssignmentId,ids,s.AuthorId,UserRole.Lecturer),default);
        submitted.IsSuccess.Should().BeFalse(); submitted.Error.Should().Contain("vượt giới hạn");
        (await verify.ExamSets.CountAsync(x=>x.AssignmentId==s.AssignmentId&&x.Status==ExamSetStatus.SubmittedForReview)).Should().Be(0);
        async Task<bool> Submit(Guid id)
        {
            await using var db = database.CreateContext();
            return (await new SubmitExamSetForReviewCommandHandler(db).Handle(new(id, s.AuthorId, UserRole.Lecturer), default)).IsSuccess;
        }
        var submissions = await Task.WhenAll(ids.Select(Submit));
        submissions.Count(x => x).Should().Be(1);
        (await verify.ExamSets.AsNoTracking().CountAsync(x=>x.AssignmentId==s.AssignmentId&&x.Status==ExamSetStatus.SubmittedForReview)).Should().Be(1);
        (await verify.ExamSetRevisions.CountAsync()).Should().Be(3);
    }
}

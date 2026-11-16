using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;
using OralExamination.Application.Features.QuestionBank.Queries.GetQuestions;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class QuestionApprovalPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Migration_LeavesLegacyApprovalTimeUnknown_AndCanRollBackAndReapply()
    {
        await using var database = await fixture.CreateDatabaseAsync(migrate: false);
        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261115090635_AddOfficialShiftCapacity");
        var count = await database.ScalarAsync<long>("SELECT count(*) FROM exam_questions");
        count.Should().BeGreaterThan(0);
        await context.Database.MigrateAsync();
        (await database.ScalarAsync<long>("SELECT count(*) FROM exam_questions WHERE approved_at IS NULL"))
            .Should().Be(count);
        (await database.ScalarAsync<long>("SELECT count(*) FROM practice_questions WHERE approved_at IS NOT NULL"))
            .Should().Be(0);
        await migrator.MigrateAsync("20261115090635_AddOfficialShiftCapacity");
        (await database.ScalarAsync<long>("SELECT count(*) FROM exam_questions")).Should().Be(count);
        await context.Database.MigrateAsync();
        (await database.ScalarAsync<long>("SELECT count(*) FROM pg_indexes WHERE indexname IN ('ix_exam_questions_approver_status_time', 'ix_practice_questions_approver_status_time')"))
            .Should().Be(2);
    }

    [Theory]
    [InlineData("exam")]
    [InlineData("practice")]
    public async Task Review_PersistsUtcApprovalTime_AndDetailReturnsIt(string scope)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var head = new User { Email = "head@example.test", FullName = "Head", Role = "department_head" };
        context.Users.Add(head);
        context.AddRange(
            new RubricCriterion { Rubric = academic.Rubric, CriterionName = "A", MaxScore = 5, Weight = .5m, OrderIndex = 1 },
            new RubricCriterion { Rubric = academic.Rubric, CriterionName = "B", MaxScore = 5, Weight = .5m, OrderIndex = 2 });
        var id = Guid.NewGuid();
        if (scope == "exam")
            context.ExamQuestions.Add(new ExamQuestion { Id = id, CourseId = academic.Course.Id, Rubric = academic.Rubric,
                Title = "Review", Content = "Question", SampleAnswer = new string('a', 60),
                ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = academic.Lecturer.Id });
        else
            context.PracticeQuestions.Add(new PracticeQuestion { Id = id, CourseId = academic.Course.Id, Rubric = academic.Rubric,
                Title = "Review", Content = "Question", SampleAnswer = new string('a', 60),
                ApprovalStatus = ApprovalStatus.SubmittedForReview, SubmittedBy = academic.Lecturer.Id, IsActive = false });
        await context.SaveChangesAsync();

        var before = DateTime.UtcNow;
        var result = await new ReviewQuestionDecisionCommandHandler(context).Handle(
            new(id, head.Id, ApprovalStatus.Approved, "Accepted", scope), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        context.ChangeTracker.Clear();
        var detail = await new GetQuestionByIdQueryHandler(context).Handle(new(id), default);
        detail.IsSuccess.Should().BeTrue();
        detail.Value.ApprovedBy.Should().Be(head.Id);
        detail.Value.ApprovedAt.Should().NotBeNull();
        detail.Value.ApprovedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        detail.Value.ApprovedAt.Value.Should().BeOnOrAfter(before.AddMilliseconds(-1)).And.BeOnOrBefore(DateTime.UtcNow);
        detail.Value.ApprovedAt.Value.Should().BeCloseTo(result.Value.ReviewedAt, TimeSpan.FromMilliseconds(1));
        detail.Value.IsActive.Should().BeTrue();

        var list = await new GetQuestionsQueryHandler(context).Handle(new(ApprovedBy: head.Id, UsageScope: scope), default);
        list.Value.Items.Should().ContainSingle().Which.ApprovedAt.Should().Be(detail.Value.ApprovedAt);
    }

    [Fact]
    public async Task ApprovedList_FiltersBothBanks_PaginatesByApprovalTime_AndKeepsLegacyNullLast()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var head = new User { Email = "head@example.test", FullName = "Head", Role = "department_head" };
        var other = new User { Email = "other@example.test", FullName = "Other", Role = "department_head" };
        context.AddRange(head, other);
        var now = DateTime.UtcNow;
        var recent = new PracticeQuestion { CourseId = academic.Course.Id, Rubric = academic.Rubric, Title = "Recent", Content = "Question",
            ApprovedBy = head.Id, ApprovedAt = now, ApprovalStatus = ApprovalStatus.Approved, CreatedAt = now.AddDays(-10) };
        var older = new ExamQuestion { CourseId = academic.Course.Id, Rubric = academic.Rubric, Title = "Older", Content = "Question",
            ApprovedBy = head.Id, ApprovedAt = now.AddDays(-1), ApprovalStatus = ApprovalStatus.Approved };
        var legacy = new ExamQuestion { CourseId = academic.Course.Id, Rubric = academic.Rubric, Title = "Legacy", Content = "Question",
            ApprovedBy = head.Id, ApprovalStatus = ApprovalStatus.Approved };
        context.AddRange(recent, older, legacy,
            new ExamQuestion { CourseId = academic.Course.Id, Rubric = academic.Rubric, Title = "Other", Content = "Question",
                ApprovedBy = other.Id, ApprovedAt = now, ApprovalStatus = ApprovalStatus.Approved },
            new PracticeQuestion { CourseId = academic.Course.Id, Rubric = academic.Rubric, Title = "Rejected", Content = "Question",
                ApprovedBy = head.Id, ApprovedAt = now, ApprovalStatus = ApprovalStatus.Rejected });
        await context.SaveChangesAsync();
        var handler = new GetQuestionsQueryHandler(context);
        var first = await handler.Handle(new(ApprovedBy: head.Id, PageSize: 2), default);
        first.IsSuccess.Should().BeTrue(first.Error);
        first.Value.TotalCount.Should().Be(3);
        first.Value.Items.Select(x => x.Id).Should().Equal(recent.Id, older.Id);
        first.Value.Items.Select(x => x.UsageScope).Should().Equal("practice", "exam");
        var second = await handler.Handle(new(ApprovedBy: head.Id, PageSize: 2, PageNumber: 2), default);
        second.Value.Items.Should().ContainSingle().Which.Id.Should().Be(legacy.Id);
        second.Value.Items[0].ApprovedAt.Should().BeNull();
        var filtered = await handler.Handle(new(ApprovedBy: head.Id, UsageScope: "practice", Search: "Recent", CourseId: academic.Course.Id), default);
        filtered.Value.Items.Should().ContainSingle().Which.Id.Should().Be(recent.Id);
        var exam = await handler.Handle(new(ApprovedBy: head.Id, UsageScope: "exam"), default);
        exam.Value.Items.Select(x => x.Id).Should().Equal(older.Id, legacy.Id);
        (await handler.Handle(new(ApprovedBy: head.Id, UsageScope: "both"), default)).IsSuccess.Should().BeFalse();
    }
}

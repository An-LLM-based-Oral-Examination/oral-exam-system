using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.ReviewAppealDecision;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class AppealWorkflowPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ConcurrentAssignments_ForLastLecturerSlot_CommitOnlyOne()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid officerId;
        Guid lecturerId;
        Guid firstAppealId;
        Guid secondAppealId;

        await using (var seed = database.CreateContext())
        {
            var data = await TestData.OfficialAsync(seed, locked: true);
            lecturerId = data.Academic.Lecturer.Id;
            var officer = new User
            {
                Email = $"{Guid.NewGuid():N}@officer.example.test",
                FullName = "Appeal Officer",
                Role = UserRole.ExaminationOfficer,
                IsActive = true
            };
            seed.Users.Add(officer);
            officerId = officer.Id;

            for (var index = 0; index < 23; index++)
            {
                seed.AppealRequests.Add(NewAppeal(data, lecturerId, AppealStatus.AssignedToLecturer, index));
            }

            var first = NewAppeal(data, officerId, AppealStatus.SubmittedToExamOffice, 24);
            var second = NewAppeal(data, officerId, AppealStatus.SubmittedToExamOffice, 25);
            firstAppealId = first.Id;
            secondAppealId = second.Id;
            seed.AppealRequests.AddRange(first, second);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var results = await Task.WhenAll(
            new AssignAppealLecturerCommandHandler(firstContext).Handle(
                new AssignAppealLecturerCommand(firstAppealId, officerId, lecturerId), default),
            new AssignAppealLecturerCommandHandler(secondContext).Handle(
                new AssignAppealLecturerCommand(secondAppealId, officerId, lecturerId), default));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => !result.IsSuccess && result.Error.StartsWith("CONFLICT:"))
            .Should().Be(1);

        await using var verify = database.CreateContext();
        (await verify.AppealRequests.CountAsync(appeal => appeal.AssignedTo == lecturerId &&
            (appeal.Status == AppealStatus.AssignedToLecturer || appeal.Status == AppealStatus.RegradedByLecturer)))
            .Should().Be(24);
        (await verify.AuditLogs.CountAsync(log => log.Action == "APPEAL_STATUS_CHANGED" &&
            (log.EntityId == firstAppealId || log.EntityId == secondAppealId))).Should().Be(1);
        (await verify.Notifications.CountAsync(notification => notification.UserId == lecturerId &&
            notification.Type == "APPEAL_ASSIGNED" &&
            (notification.RelatedEntityId == firstAppealId || notification.RelatedEntityId == secondAppealId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentApprovals_ForDifferentSubmissions_KeepTicketAuditAggregateConsistent()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid officerId;
        Guid firstAppealId;
        Guid secondAppealId;
        Guid ticketId;

        await using (var seed = database.CreateContext())
        {
            var data = await TestData.OfficialAsync(seed, locked: true);
            ticketId = data.Ticket.Id;
            var officer = new User
            {
                Email = $"{Guid.NewGuid():N}@officer.example.test",
                FullName = "Decision Officer",
                Role = UserRole.ExaminationOfficer,
                IsActive = true
            };
            var firstSubmission = new ExamQuestionSubmission
            {
                TicketId = ticketId,
                ExamQuestionId = data.Question.Id,
                AiScore = 5m,
                FinalScore = 5m,
                GradingStatus = "audited"
            };
            var secondQuestion = new ExamQuestion
            {
                CourseId = data.Academic.Course.Id,
                RubricId = data.Academic.Rubric.Id,
                CloId = data.Academic.Clo.Id,
                StructureId = data.Question.StructureId,
                Title = "Second integration question",
                Content = "Explain isolation.",
                KeyPoints = "[]"
            };
            var secondSubmission = new ExamQuestionSubmission
            {
                TicketId = ticketId,
                ExamQuestion = secondQuestion,
                AiScore = 5m,
                FinalScore = 5m,
                GradingStatus = "audited"
            };
            seed.AddRange(officer, firstSubmission, secondSubmission,
                new LecturerAudit
                {
                    TicketId = ticketId,
                    LecturerId = data.Academic.Lecturer.Id,
                    OriginalAiScore = 5m,
                    AuditedScore = 5m,
                    OverrideReason = "Initial locked grade.",
                    IsLocked = true
                });
            await seed.SaveChangesAsync();
            officerId = officer.Id;

            var first = NewAppeal(data, data.Academic.Lecturer.Id, AppealStatus.RegradedByLecturer, 1);
            first.SubmissionId = firstSubmission.Id;
            first.ProposedScore = 7m;
            var second = NewAppeal(data, data.Academic.Lecturer.Id, AppealStatus.RegradedByLecturer, 2);
            second.SubmissionId = secondSubmission.Id;
            second.ProposedScore = 9m;
            firstAppealId = first.Id;
            secondAppealId = second.Id;
            seed.AddRange(first, second);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var decisions = await Task.WhenAll(
            new ReviewAppealDecisionCommandHandler(firstContext).Handle(
                new ReviewAppealDecisionCommand(firstAppealId, officerId, AppealStatus.Approved, 7m, "Approve first independent regrade."), default),
            new ReviewAppealDecisionCommandHandler(secondContext).Handle(
                new ReviewAppealDecisionCommand(secondAppealId, officerId, AppealStatus.Approved, 9m, "Approve second independent regrade."), default));

        decisions.Should().OnlyContain(decision => decision.IsSuccess);

        await using var verify = database.CreateContext();
        (await verify.ExamQuestionSubmissions.Where(submission => submission.TicketId == ticketId)
            .OrderBy(submission => submission.FinalScore).Select(submission => submission.FinalScore).ToListAsync())
            .Should().Equal(7m, 9m);
        (await verify.LecturerAudits.SingleAsync(audit => audit.TicketId == ticketId)).AuditedScore.Should().Be(8m);
        (await verify.AuditLogs.CountAsync(log => log.Action == "APPEAL_STATUS_CHANGED" &&
            (log.EntityId == firstAppealId || log.EntityId == secondAppealId))).Should().Be(2);
    }

    private static AppealRequest NewAppeal(OfficialData data, Guid assignedTo, string status, int index) => new()
    {
        TicketId = data.Ticket.Id,
        StudentId = data.Academic.Student.Id,
        SessionId = data.Ticket.Shift.SessionId,
        Reason = $"Integration appeal {index}",
        Status = status,
        AssignedTo = assignedTo,
        OriginalScore = 5m,
        CreatedAt = DateTime.UtcNow.AddMinutes(-index),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-index)
    };
}

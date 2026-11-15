using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Persistence;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

/// <summary>
/// Exercises the registered SaveChanges interceptor against migrated PostgreSQL.
/// This is not a claim that raw SQL/ExecuteUpdate is protected by that interceptor.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class OneWayLockPostgresTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LockedTicket_UpdateOrDeleteIsRejected_AndOriginalPersists(bool asyncSave, bool delete)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid ticketId;
        await using (var seedContext = database.CreateContext())
        {
            ticketId = (await TestData.OfficialAsync(seedContext, locked: true)).Ticket.Id;
        }

        await using (var writeContext = database.CreateContext())
        {
            var ticket = await writeContext.StudentExamTickets.SingleAsync(item => item.Id == ticketId);
            if (delete) writeContext.StudentExamTickets.Remove(ticket);
            else ticket.SeatNumber = 2;

            await AssertRejectedAsync(writeContext, asyncSave);
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.StudentExamTickets.SingleAsync(item => item.Id == ticketId);
        persisted.IsLocked.Should().BeTrue();
        persisted.SeatNumber.Should().Be(1);
        persisted.Status.Should().Be("LOCKED");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LockedAudit_UpdateOrDeleteIsRejected_AndScorePersists(bool asyncSave, bool delete)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid auditId;
        await using (var seedContext = database.CreateContext())
        {
            var official = await TestData.OfficialAsync(seedContext, locked: true);
            var audit = new LecturerAudit
            {
                TicketId = official.Ticket.Id, LecturerId = official.Academic.Lecturer.Id,
                OriginalAiScore = 7m, AuditedScore = 7m, OverrideReason = "Original audit", IsLocked = true
            };
            seedContext.LecturerAudits.Add(audit);
            await seedContext.SaveChangesAsync();
            auditId = audit.Id;
        }

        await using (var writeContext = database.CreateContext())
        {
            var audit = await writeContext.LecturerAudits.SingleAsync(item => item.Id == auditId);
            if (delete) writeContext.LecturerAudits.Remove(audit);
            else audit.AuditedScore = 9m;

            await AssertRejectedAsync(writeContext, asyncSave);
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.LecturerAudits.SingleAsync(item => item.Id == auditId);
        persisted.IsLocked.Should().BeTrue();
        persisted.AuditedScore.Should().Be(7m);
        persisted.OverrideReason.Should().Be("Original audit");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LockedInClassShift_UpdateOrDeleteIsRejected_AndAggregatePersists(bool asyncSave, bool delete)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        InClassData data;
        await using (var seedContext = database.CreateContext())
        {
            data = await SeedInClassAsync(seedContext);
        }

        await using (var writeContext = database.CreateContext())
        {
            var shift = await writeContext.InClassExamShifts.SingleAsync(item => item.Id == data.ShiftId);
            if (delete) writeContext.InClassExamShifts.Remove(shift);
            else shift.Description = "Unauthorized change";

            await AssertRejectedAsync(writeContext, asyncSave);
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.InClassExamShifts.SingleAsync(item => item.Id == data.ShiftId);
        persisted.IsLocked.Should().BeTrue();
        persisted.Status.Should().Be("FINALIZED");
        persisted.Description.Should().Be("Original locked shift");
        (await verify.InClassExamSubmissions.AnyAsync(item => item.Id == data.SubmissionId)).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SubmissionOfLockedInClassShift_UpdateOrDeleteIsRejected_WithoutLoadingParent(bool asyncSave, bool delete)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        InClassData data;
        await using (var seedContext = database.CreateContext())
        {
            data = await SeedInClassAsync(seedContext);
        }

        await using (var writeContext = database.CreateContext())
        {
            var submission = await writeContext.InClassExamSubmissions.SingleAsync(item => item.Id == data.SubmissionId);
            writeContext.ChangeTracker.Entries<InClassExamShift>().Should().BeEmpty();
            if (delete) writeContext.InClassExamSubmissions.Remove(submission);
            else submission.FinalScore = 9m;

            await AssertRejectedAsync(writeContext, asyncSave);
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.InClassExamSubmissions.SingleAsync(item => item.Id == data.SubmissionId);
        persisted.FinalScore.Should().Be(7m);
        persisted.Transcript.Should().Be("Original submitted answer");
        (await verify.InClassExamShifts.SingleAsync(item => item.Id == data.ShiftId)).IsLocked.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LockedTicket_PendingToAcknowledgedIsAllowed_WithoutUnlockingOrChangingGrade(bool asyncSave)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid ticketId;
        await using (var seedContext = database.CreateContext())
        {
            ticketId = (await TestData.OfficialAsync(seedContext, locked: true)).Ticket.Id;
        }

        await using (var writeContext = database.CreateContext())
        {
            var ticket = await writeContext.StudentExamTickets.SingleAsync(item => item.Id == ticketId);
            ticket.StudentAcknowledgementStatus.Should().Be("PENDING");
            ticket.StudentAcknowledgementStatus = "ACKNOWLEDGED";
            if (asyncSave) await writeContext.SaveChangesAsync();
            else writeContext.SaveChanges();
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.StudentExamTickets.SingleAsync(item => item.Id == ticketId);
        persisted.StudentAcknowledgementStatus.Should().Be("ACKNOWLEDGED");
        persisted.IsLocked.Should().BeTrue();
        persisted.Status.Should().Be("LOCKED");
        persisted.SeatNumber.Should().Be(1);
    }

    private static async Task AssertRejectedAsync(OralExamDbContext context, bool asyncSave)
    {
        if (asyncSave)
            await Assert.ThrowsAsync<OneWayLockException>(() => context.SaveChangesAsync());
        else
            Assert.Throws<OneWayLockException>(() => context.SaveChanges());
    }

    private static async Task<InClassData> SeedInClassAsync(OralExamDbContext context)
    {
        var academic = await TestData.AcademicAsync(context);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var examClass = new Class
        {
            Code = "IT-LOCK-" + suffix, Name = "Lock test class", JoinCode = "LOCK" + suffix,
            CourseId = academic.Course.Id, SemesterId = academic.Course.SemesterId,
            LecturerId = academic.Lecturer.Id
        };
        var question = new PracticeQuestion
        {
            CourseId = academic.Course.Id, RubricId = academic.Rubric.Id, CloId = academic.Clo.Id,
            Title = "Integration lock question", Content = "Explain the lock boundary."
        };
        var shift = new InClassExamShift
        {
            Class = examClass, LecturerId = academic.Lecturer.Id, ShiftName = "Locked MF05 shift",
            Description = "Original locked shift", TotpSecretKey = "INTEGRATION-LOCK-TEST",
            StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow,
            Status = "FINALIZED", IsLocked = true, FinalizedAt = DateTime.UtcNow
        };
        var submission = new InClassExamSubmission
        {
            Shift = shift, StudentId = academic.Student.Id, Question = question,
            Transcript = "Original submitted answer", Status = "AUDITED", AiScore = 7m, FinalScore = 7m
        };
        context.InClassExamSubmissions.Add(submission);
        await context.SaveChangesAsync();
        return new(shift.Id, submission.Id);
    }

    private sealed record InClassData(Guid ShiftId, Guid SubmissionId);
}

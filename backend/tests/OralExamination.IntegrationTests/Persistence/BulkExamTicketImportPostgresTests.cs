using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class BulkExamTicketImportPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Commit_WhenAuditInsertFails_RollsBackEveryPreparedTicket()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid officerId;
        Guid shiftId;
        Guid studentId;

        await using (var seed = database.CreateContext())
        {
            var data = await TestData.OfficialAsync(seed);
            shiftId = data.Ticket.ShiftId;
            var officer = new User { Email = $"{Guid.NewGuid():N}@officer.test", FullName = "Import officer", Role = UserRole.ExaminationOfficer, IsActive = true };
            var student = new User { Email = $"{Guid.NewGuid():N}@student.test", FullName = "Rollback student", StudentCode = "SEIMPROLL", Role = UserRole.Student, IsActive = true };
            var session = await seed.OfficialExamSessions.SingleAsync(item => item.Id == data.Ticket.Shift.SessionId);
            var structureId = session.ExamStructureId ?? throw new InvalidOperationException("Seeded session must have an exam structure.");
            seed.AddRange(officer, student, new ExamSet
            {
                CourseId = data.Academic.Course.Id,
                StructureId = structureId,
                SessionId = session.Id,
                SetCode = "BULK-ROLLBACK",
                Status = ExamSetStatus.HandedOver,
                IsActive = true,
                HandedOverAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
            officerId = officer.Id;
            studentId = student.Id;
        }

        await database.ExecuteAsync("""
            CREATE OR REPLACE FUNCTION fail_bulk_ticket_import_audit() RETURNS trigger AS $$
            BEGIN
                IF NEW.action = 'BULK_EXAM_TICKETS_IMPORTED' THEN
                    RAISE EXCEPTION 'forced bulk import audit failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER fail_bulk_ticket_import_audit_trigger
            BEFORE INSERT ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION fail_bulk_ticket_import_audit();
            """);

        await using (var context = database.CreateContext())
        {
            var command = new CommitExamRosterImportCommand(officerId, shiftId,
                [new BulkExamTicketImportRow(2, "SEIMPROLL", 21, "10.20.0.21")]);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new CommitExamRosterImportCommandHandler(context).Handle(command, default));
        }

        await using var verify = database.CreateContext();
        (await verify.StudentExamTickets.CountAsync(ticket => ticket.ShiftId == shiftId && ticket.StudentId == studentId))
            .Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentCommits_SerializeShiftAndPersistExactlyOneWholeBatch()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        Guid officerId;
        Guid shiftId;
        Guid firstStudentId;
        Guid secondStudentId;

        await using (var seed = database.CreateContext())
        {
            var data = await TestData.OfficialAsync(seed);
            shiftId = data.Ticket.ShiftId;
            var officer = new User { Email = $"{Guid.NewGuid():N}@officer.test", FullName = "Import officer", Role = UserRole.ExaminationOfficer, IsActive = true };
            var firstStudent = new User { Email = $"{Guid.NewGuid():N}@student.test", FullName = "Import student 1", StudentCode = "SEIMP001", Role = UserRole.Student, IsActive = true };
            var secondStudent = new User { Email = $"{Guid.NewGuid():N}@student.test", FullName = "Import student 2", StudentCode = "SEIMP002", Role = UserRole.Student, IsActive = true };
            var session = await seed.OfficialExamSessions.SingleAsync(item => item.Id == data.Ticket.Shift.SessionId);
            var structureId = session.ExamStructureId ?? throw new InvalidOperationException("Seeded session must have an exam structure.");
            var sets = Enumerable.Range(1, 2).Select(index => new ExamSet
            {
                CourseId = data.Academic.Course.Id,
                StructureId = structureId,
                SessionId = session.Id,
                SetCode = $"BULK-HANDED-{index}",
                Status = ExamSetStatus.HandedOver,
                IsActive = true,
                HandedOverAt = DateTime.UtcNow.AddMinutes(-index)
            }).ToList();
            seed.AddRange(officer, firstStudent, secondStudent);
            seed.ExamSets.AddRange(sets);
            await seed.SaveChangesAsync();
            officerId = officer.Id;
            firstStudentId = firstStudent.Id;
            secondStudentId = secondStudent.Id;
        }

        var rows = new[]
        {
            new BulkExamTicketImportRow(2, "SEIMP001", 11, "10.20.0.11"),
            new BulkExamTicketImportRow(3, "SEIMP002", 12, "10.20.0.12")
        };
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var results = await Task.WhenAll(
            new CommitExamRosterImportCommandHandler(firstContext).Handle(
                new CommitExamRosterImportCommand(officerId, shiftId, rows), default),
            new CommitExamRosterImportCommandHandler(secondContext).Handle(
                new CommitExamRosterImportCommand(officerId, shiftId, rows), default));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => !result.IsSuccess && result.Error.StartsWith("CONFLICT:")).Should().Be(1);

        await using var verify = database.CreateContext();
        var imported = await verify.StudentExamTickets
            .Where(ticket => ticket.ShiftId == shiftId && (ticket.StudentId == firstStudentId || ticket.StudentId == secondStudentId))
            .ToListAsync();
        imported.Should().HaveCount(2);
        imported.Select(ticket => ticket.ExamSetId).Should().OnlyHaveUniqueItems();
        (await verify.AuditLogs.CountAsync(log => log.Action == "BULK_EXAM_TICKETS_IMPORTED" && log.EntityId == shiftId))
            .Should().Be(1);
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.ImportExamRoster;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class ImportExamRosterCommandHandlerTests
{
    private static OralExamDbContext CreateContext() => new(new DbContextOptionsBuilder<OralExamDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task<(User Officer, RealExamSessionShift Shift)> SeedAsync(
        OralExamDbContext context, int handedOverSetCount)
    {
        var officer = new User { Email = "officer@import.test", FullName = "Officer", Role = UserRole.ExaminationOfficer, IsActive = true };
        var course = new Course { Code = "IMP101", Name = "Import test course" };
        var structure = new ExamStructure { Course = course, Name = "Import structure", TotalQuestions = 1, CreatedBy = officer.Id };
        var session = new OfficialExamSession { Course = course, ExamStructure = structure, Title = "Import session", ExamDate = new DateOnly(2026, 11, 15) };
        var shift = new RealExamSessionShift { Session = session, ShiftName = "Import shift", RoomLab = "L-101", StartTime = DateTime.UtcNow.AddHours(1), EndTime = DateTime.UtcNow.AddHours(2) };
        context.AddRange(officer, shift);

        for (var index = 0; index < handedOverSetCount; index++)
        {
            context.ExamSets.Add(new ExamSet
            {
                Course = course,
                Structure = structure,
                Session = session,
                SetCode = $"HANDOVER-{index + 1}",
                Status = ExamSetStatus.HandedOver,
                IsActive = true,
                HandedOverAt = DateTime.UtcNow.AddMinutes(-index)
            });
        }
        await context.SaveChangesAsync();
        return (officer, shift);
    }

    private static async Task AddStudentsAsync(OralExamDbContext context, params string[] codes)
    {
        context.Users.AddRange(codes.Select((code, index) => new User
        {
            Email = $"{code.ToLowerInvariant()}@student.test",
            FullName = $"Student {index + 1}",
            StudentCode = code,
            Role = UserRole.Student,
            IsActive = true
        }));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_WhenHandedOverSetsAreInsufficient_ReportsWholeBatchConflict()
    {
        await using var context = CreateContext();
        var (officer, shift) = await SeedAsync(context, handedOverSetCount: 1);
        await AddStudentsAsync(context, "SE100001", "SE100002");
        var rows = new[]
        {
            new BulkExamTicketImportRow(2, "SE100001", 1, "10.0.0.1"),
            new BulkExamTicketImportRow(3, "SE100002", 2, "10.0.0.2")
        };

        var result = await new PreviewExamRosterImportQueryHandler(context)
            .Handle(new PreviewExamRosterImportQuery(officer.Id, shift.Id, rows), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CanCommit.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(issue => issue.Message.Contains("Thiếu bộ đề HANDED_OVER"));

        var commit = await new CommitExamRosterImportCommandHandler(context)
            .Handle(new CommitExamRosterImportCommand(officer.Id, shift.Id, rows), default);
        commit.IsSuccess.Should().BeFalse();
        (await context.StudentExamTickets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Commit_WhenPreviewIsValid_CreatesEveryTicketWithDistinctHandedOverSetsAndAudit()
    {
        await using var context = CreateContext();
        var (officer, shift) = await SeedAsync(context, handedOverSetCount: 2);
        await AddStudentsAsync(context, "SE100001", "SE100002");
        var rows = new[]
        {
            new BulkExamTicketImportRow(2, "SE100001", 1, "10.0.0.1"),
            new BulkExamTicketImportRow(3, "SE100002", 2, "10.0.0.2")
        };

        var result = await new CommitExamRosterImportCommandHandler(context)
            .Handle(new CommitExamRosterImportCommand(officer.Id, shift.Id, rows), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedTicketCount.Should().Be(2);
        (await context.StudentExamTickets.Where(ticket => ticket.ShiftId == shift.Id).ToListAsync())
            .Select(ticket => ticket.ExamSetId).Should().OnlyHaveUniqueItems();
        (await context.ExamSets.Where(set => result.Value.Tickets.Select(ticket => ticket.ExamSetId).Contains(set.Id))
            .Select(set => set.Status).ToListAsync()).Should().OnlyContain(status => status == ExamSetStatus.HandedOver);
        (await context.AuditLogs.CountAsync(log => log.Action == "BULK_EXAM_TICKETS_IMPORTED" && log.EntityId == shift.Id)).Should().Be(1);
    }
}

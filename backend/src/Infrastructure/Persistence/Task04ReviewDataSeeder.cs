using Microsoft.EntityFrameworkCore;
using OralExamination.Domain.Enums;

namespace OralExamination.Infrastructure.Persistence;

/// <summary>
/// Supplies one isolated browser-review fixture for Task 04.
/// The API composition root invokes it only for Development with an explicit flag.
/// </summary>
public static class Task04ReviewDataSeeder
{
    public static async Task SeedAsync(
        OralExamDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ticket = await context.StudentExamTickets
            .FirstOrDefaultAsync(
                item => item.Id == DevelopmentDataSeeder.TicketMaiId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Task 04 review fixture requires the development seed ticket for student SE170002.");

        // Start inside the appeal window with no appeal for this ticket. The student
        // must create the first transition through the real API/UI.
        if (!ticket.IsLocked)
        {
            ticket.Status = ExamTicketStatus.Published;
            ticket.IsLocked = true;
            ticket.GradesPublishedAt = DateTime.UtcNow.AddHours(-1);
            ticket.StudentAcknowledgementStatus = StudentAcknowledgementStatus.Pending;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Common;

internal static class OfficialExamReportQuery
{
    public static Task<RealExamSessionShift?> LoadShiftAsync(
        IApplicationDbContext context,
        Guid shiftId,
        CancellationToken cancellationToken) =>
        context.RealExamSessionShifts
            .AsNoTracking()
            .Include(shift => shift.Session)
                .ThenInclude(session => session.Course)
                    .ThenInclude(course => course.Semester)
            .Include(shift => shift.ProctorUser)
            .Include(shift => shift.StudentExamTickets)
                .ThenInclude(ticket => ticket.Student)
            .Include(shift => shift.StudentExamTickets)
                .ThenInclude(ticket => ticket.LecturerAudit)
            .Include(shift => shift.StudentExamTickets)
                .ThenInclude(ticket => ticket.Submissions)
                    .ThenInclude(submission => submission.AiEvaluations)
            .FirstOrDefaultAsync(shift => shift.Id == shiftId, cancellationToken);

    public static bool IsPublishedAndLocked(RealExamSessionShift shift) =>
        shift.StudentExamTickets.Count > 0
        && shift.StudentExamTickets.All(ticket =>
            ticket.IsLocked
            && ticket.Status is ExamTicketStatus.Published or ExamTicketStatus.Locked);
}

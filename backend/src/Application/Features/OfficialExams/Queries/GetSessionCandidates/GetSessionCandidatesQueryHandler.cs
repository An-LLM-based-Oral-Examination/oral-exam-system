using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetSessionCandidates;

public sealed class GetSessionCandidatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSessionCandidatesQuery, Result<SessionCandidatesPageResponse>>
{
    public async Task<Result<SessionCandidatesPageResponse>> Handle(
        GetSessionCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveEnrollmentEstimateRoleAsync(
                context, request.ActorUserId, cancellationToken))
        {
            return Result<SessionCandidatesPageResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<SessionCandidatesPageResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == request.SessionId)
            .Select(s => new { s.Id, s.CourseId, s.ExamAttemptType })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<SessionCandidatesPageResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (!await OfficialExamAuthorization.CanReadEnrollmentEstimateAsync(
                context, request.ActorUserId, session.CourseId, cancellationToken))
        {
            return Result<SessionCandidatesPageResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var candidates = await context.OfficialExamCandidates
            .AsNoTracking()
            .Where(c => c.SessionId == session.Id)
            .Select(c => new
            {
                c.StudentId,
                c.ImportedAt
            })
            .ToListAsync(cancellationToken);

        var studentIds = candidates.Select(c => c.StudentId).Distinct().ToList();

        var usersById = studentIds.Count == 0
            ? new Dictionary<Guid, (string StudentCode, string FullName, string Email)>()
            : (await context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.StudentCode, u.FullName, u.Email })
                .ToListAsync(cancellationToken))
                .ToDictionary(
                    u => u.Id,
                    u => (StudentCode: u.StudentCode ?? string.Empty, FullName: u.FullName ?? string.Empty, Email: u.Email ?? string.Empty));

        var shifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(sh => sh.SessionId == session.Id)
            .Select(sh => new { sh.Id, sh.RoomLab })
            .ToListAsync(cancellationToken);

        var shiftRoomById = shifts.ToDictionary(sh => sh.Id, sh => sh.RoomLab);
        var shiftIds = shifts.Select(sh => sh.Id).ToList();

        var ticketsByStudent = shiftIds.Count == 0 || studentIds.Count == 0
            ? new Dictionary<Guid, (Guid TicketId, Guid ShiftId, int SeatNumber, string Status)>()
            : (await context.StudentExamTickets
                .AsNoTracking()
                .Where(t => shiftIds.Contains(t.ShiftId) && studentIds.Contains(t.StudentId))
                .Select(t => new { t.Id, t.ShiftId, t.StudentId, t.SeatNumber, t.Status })
                .ToListAsync(cancellationToken))
                .GroupBy(t => t.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var first = g.First();
                        return (TicketId: first.Id, ShiftId: first.ShiftId, SeatNumber: first.SeatNumber, Status: first.Status);
                    });

        var allItems = candidates
            .Select(c =>
            {
                usersById.TryGetValue(c.StudentId, out var user);
                var hasTicket = ticketsByStudent.TryGetValue(c.StudentId, out var ticket);
                string? roomLab = hasTicket && shiftRoomById.TryGetValue(ticket.ShiftId, out var room) ? room : null;

                return new SessionCandidateItemDto(
                    c.StudentId,
                    user.StudentCode ?? string.Empty,
                    user.FullName ?? string.Empty,
                    user.Email ?? string.Empty,
                    c.ImportedAt,
                    hasTicket,
                    hasTicket ? ticket.TicketId : null,
                    hasTicket ? ticket.ShiftId : null,
                    roomLab,
                    hasTicket ? ticket.SeatNumber : null,
                    hasTicket ? ticket.Status : null);
            })
            .OrderBy(item => item.StudentCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalAllocatedCount = allItems.Count(item => item.IsAllocated);
        var totalUnallocatedCount = allItems.Count - totalAllocatedCount;

        IEnumerable<SessionCandidateItemDto> filtered = allItems;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(item =>
                item.StudentCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.RoomLab is not null && item.RoomLab.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (request.AllocatedOnly.HasValue)
        {
            filtered = filtered.Where(item => item.IsAllocated == request.AllocatedOnly.Value);
        }

        var filteredList = filtered.ToList();
        var pagedItems = filteredList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<SessionCandidatesPageResponse>.Success(new SessionCandidatesPageResponse(
            session.Id,
            session.ExamAttemptType,
            filteredList.Count,
            totalAllocatedCount,
            totalUnallocatedCount,
            pageNumber,
            pageSize,
            pagedItems));
    }
}

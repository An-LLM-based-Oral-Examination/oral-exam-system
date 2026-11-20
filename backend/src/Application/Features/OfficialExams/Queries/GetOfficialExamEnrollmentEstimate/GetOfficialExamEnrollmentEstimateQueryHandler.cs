using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamEnrollmentEstimate;

public sealed class GetOfficialExamEnrollmentEstimateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamEnrollmentEstimateQuery, Result<OfficialExamEnrollmentEstimateDto>>
{
    public async Task<Result<OfficialExamEnrollmentEstimateDto>> Handle(
        GetOfficialExamEnrollmentEstimateQuery request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveEnrollmentEstimateRoleAsync(
                context, request.ActorUserId, cancellationToken))
        {
            return Result<OfficialExamEnrollmentEstimateDto>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<OfficialExamEnrollmentEstimateDto>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(item => item.Id == request.SessionId)
            .Select(item => new
            {
                item.Id,
                item.CourseId,
                CourseCode = item.Course.Code,
                CourseName = item.Course.Name,
                ResolvedSemesterId = item.SemesterId ?? item.Course.SemesterId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<OfficialExamEnrollmentEstimateDto>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (!await OfficialExamAuthorization.CanReadEnrollmentEstimateAsync(
                context, request.ActorUserId, session.CourseId, cancellationToken))
        {
            return Result<OfficialExamEnrollmentEstimateDto>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        var semester = await context.Semesters
            .AsNoTracking()
            .Where(item => item.Id == session.ResolvedSemesterId)
            .Select(item => new { item.Id, item.Code })
            .FirstOrDefaultAsync(cancellationToken);

        var semesterCode = semester?.Code ?? string.Empty;

        var eligibleStudentIds = await OfficialExamSchedulingHelper.GetEligibleEnrolledStudentIdsAsync(
            context, session.CourseId, session.ResolvedSemesterId, cancellationToken);

        var shifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(shift => shift.SessionId == session.Id && shift.Status.ToLower() != "cancelled")
            .Select(shift => new
            {
                shift.Id,
                shift.MaxSeats,
                shift.Status,
                shift.IsExamPublished
            })
            .ToListAsync(cancellationToken);

        var activeShiftIds = shifts.Select(shift => shift.Id).ToList();

        var existingTickets = activeShiftIds.Count == 0
            ? []
            : await context.StudentExamTickets
                .AsNoTracking()
                .Where(ticket => activeShiftIds.Contains(ticket.ShiftId))
                .Select(ticket => new { ticket.ShiftId, ticket.StudentId, ticket.SeatNumber })
                .ToListAsync(cancellationToken);

        var allocatedStudentIdSet = existingTickets
            .Select(ticket => ticket.StudentId)
            .ToHashSet();

        var eligibleStudentCount = eligibleStudentIds.Count;
        var allocatedStudentCount = eligibleStudentIds.Count(allocatedStudentIdSet.Contains);
        var unallocatedStudentCount = Math.Max(0, eligibleStudentCount - allocatedStudentCount);

        var ticketsByShift = existingTickets
            .GroupBy(ticket => ticket.ShiftId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var currentShiftRoomCount = shifts.Count;
        var currentTotalCapacity = shifts.Sum(shift => Math.Min(OfficialExamRules.DefaultShiftCapacity, shift.MaxSeats));

        var currentAvailableSeats = 0;
        foreach (var shift in shifts)
        {
            if (!string.Equals(shift.Status, "scheduled", StringComparison.OrdinalIgnoreCase) || shift.IsExamPublished)
            {
                continue;
            }

            var effectiveRoomCap = Math.Min(OfficialExamRules.DefaultShiftCapacity, shift.MaxSeats);
            var shiftTickets = ticketsByShift.GetValueOrDefault(shift.Id);
            var occupiedCount = shiftTickets?.Count ?? 0;
            var occupiedSeatsInStandardRange = shiftTickets is null
                ? 0
                : shiftTickets.Select(t => t.SeatNumber).Where(seat => seat >= 1 && seat <= effectiveRoomCap).Distinct().Count();
            var freeStandardSeats = Math.Max(0, effectiveRoomCap - occupiedSeatsInStandardRange);
            currentAvailableSeats += Math.Min(Math.Max(0, effectiveRoomCap - occupiedCount), freeStandardSeats);
        }

        var handedOverExamSetCount = await context.ExamSets
            .AsNoTracking()
            .CountAsync(set =>
                set.IsActive
                && set.Status == ExamSetStatus.HandedOver
                && set.CourseId == session.CourseId
                && (!set.SessionId.HasValue || set.SessionId == session.Id),
                cancellationToken);

        var missingSeatsForAdditionalRooms = Math.Max(0, unallocatedStudentCount - currentAvailableSeats);

        return Result<OfficialExamEnrollmentEstimateDto>.Success(new OfficialExamEnrollmentEstimateDto(
            session.Id,
            session.CourseId,
            session.CourseCode,
            session.CourseName,
            session.ResolvedSemesterId,
            semesterCode,
            eligibleStudentCount,
            allocatedStudentCount,
            unallocatedStudentCount,
            OfficialExamRules.DefaultShiftCapacity,
            OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(eligibleStudentCount),
            OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(missingSeatsForAdditionalRooms),
            currentShiftRoomCount,
            currentTotalCapacity,
            currentAvailableSeats,
            handedOverExamSetCount));
    }
}

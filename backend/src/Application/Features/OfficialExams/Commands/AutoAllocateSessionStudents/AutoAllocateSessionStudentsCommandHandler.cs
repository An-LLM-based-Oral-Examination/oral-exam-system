using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.AutoAllocateSessionStudents;

public sealed class AutoAllocateSessionStudentsCommandHandler(
    IApplicationDbContext context,
    IOfficialExamRandomGenerator? randomGenerator = null)
    : IRequestHandler<AutoAllocateSessionStudentsCommand, Result<AutoAllocateSessionStudentsResponse>>
{
    private readonly IOfficialExamRandomGenerator _randomGenerator =
        randomGenerator ?? CryptoOfficialExamRandomGenerator.Instance;

    public async Task<Result<AutoAllocateSessionStudentsResponse>> Handle(
        AutoAllocateSessionStudentsCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerOrAdminAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (request.ShiftIds is not null && request.ShiftIds.Any(id => id == Guid.Empty))
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure("BAD_REQUEST: Danh sách shiftIds chứa mã ca thi không hợp lệ.");
        }

        return await context.ExecuteOfficialSessionAllocationAsync(
            request.SessionId,
            ct => AllocateLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result<AutoAllocateSessionStudentsResponse>> AllocateLockedAsync(
        AutoAllocateSessionStudentsCommand request,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(item => item.Id == request.SessionId)
            .Select(item => new
            {
                item.Id,
                item.CourseId,
                item.Status,
                ResolvedSemesterId = item.SemesterId ?? item.Course.SemesterId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (string.Equals(session.Status, "concluded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure(
                "CONFLICT: Không thể phân bổ sinh viên cho kỳ thi đã kết thúc hoặc đã hủy.");
        }

        var eligibleStudentIds = await OfficialExamSchedulingHelper.GetEligibleEnrolledStudentIdsAsync(
            context, session.CourseId, session.ResolvedSemesterId, cancellationToken);

        var allActiveSessionShifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(shift => shift.SessionId == session.Id && shift.Status.ToLower() != "cancelled")
            .OrderBy(shift => shift.StartTime)
            .ThenBy(shift => shift.EndTime)
            .ThenBy(shift => shift.RoomLab)
            .ToListAsync(cancellationToken);

        var allActiveShiftIds = allActiveSessionShifts.Select(shift => shift.Id).ToList();
        var existingSessionTickets = allActiveShiftIds.Count == 0
            ? []
            : await context.StudentExamTickets
                .AsNoTracking()
                .Where(ticket => allActiveShiftIds.Contains(ticket.ShiftId))
                .Select(ticket => new
                {
                    ticket.ShiftId,
                    ticket.StudentId,
                    ticket.SeatNumber,
                    ticket.ExamSetId
                })
                .ToListAsync(cancellationToken);

        var alreadyAllocatedStudentSet = existingSessionTickets
            .Select(ticket => ticket.StudentId)
            .ToHashSet();

        var totalEligibleStudents = eligibleStudentIds.Count;
        var previouslyAllocatedCount = eligibleStudentIds.Count(alreadyAllocatedStudentSet.Contains);
        var unallocatedStudents = eligibleStudentIds
            .Where(studentId => !alreadyAllocatedStudentSet.Contains(studentId))
            .ToList();

        if (unallocatedStudents.Count == 0)
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure(
                "UNPROCESSABLE: Không có sinh viên đang ghi danh hợp lệ nào chưa được phân bổ phòng thi trong kỳ thi này.");
        }

        var targetShiftsResult = FilterTargetShifts(allActiveSessionShifts, request.ShiftIds);
        if (!targetShiftsResult.IsSuccess)
        {
            return Result<AutoAllocateSessionStudentsResponse>.Failure(targetShiftsResult.Error);
        }

        var targetShifts = targetShiftsResult.Value;
        var ticketsByShift = existingSessionTickets
            .GroupBy(ticket => ticket.ShiftId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var roomSlots = new List<ShiftRoomAllocationSlot>(targetShifts.Count);
        foreach (var shift in targetShifts)
        {
            var effectiveMaxSeats = Math.Min(OfficialExamRules.DefaultShiftCapacity, shift.MaxSeats);
            var roomTickets = ticketsByShift.GetValueOrDefault(shift.Id);
            var occupiedSeats = roomTickets is null
                ? new HashSet<int>()
                : roomTickets.Select(item => item.SeatNumber).ToHashSet();
            var usedExamSetIds = roomTickets is null
                ? new HashSet<Guid>()
                : roomTickets.Where(item => item.ExamSetId.HasValue).Select(item => item.ExamSetId!.Value).ToHashSet();

            var availableSeats = Enumerable.Range(1, effectiveMaxSeats)
                .Where(seat => !occupiedSeats.Contains(seat))
                .ToList();

            roomSlots.Add(new ShiftRoomAllocationSlot
            {
                ShiftId = shift.Id,
                RoomLab = shift.RoomLab,
                MaxSeats = shift.MaxSeats,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                AssignedBefore = roomTickets?.Count ?? 0,
                AvailableSeatNumbers = availableSeats,
                ExistingExamSetIds = usedExamSetIds
            });
        }

        var totalAvailableSeats = roomSlots.Sum(slot => slot.AvailableCapacity);
        if (roomSlots.Count == 0 || totalAvailableSeats < unallocatedStudents.Count)
        {
            var missingSeats = unallocatedStudents.Count - totalAvailableSeats;
            var neededRooms = OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(missingSeats);
            return Result<AutoAllocateSessionStudentsResponse>.Failure(
                $"UNPROCESSABLE: Tổng số ghế trống khả dụng ({totalAvailableSeats} ghế) không đủ để xếp {unallocatedStudents.Count} sinh viên chưa có phòng (thiếu {missingSeats} ghế, cần mở thêm tối thiểu {neededRooms} phòng chuẩn tối đa {OfficialExamRules.DefaultShiftCapacity} SV/phòng).");
        }

        OfficialExamSchedulingHelper.ShuffleInPlace(unallocatedStudents, _randomGenerator);
        var groupPlans = OfficialExamSchedulingHelper.AllocateAcrossShiftGroups(roomSlots, unallocatedStudents.Count);

        var candidateExamSets = new List<Guid>();
        if (request.AssignHandedOverExamSets)
        {
            candidateExamSets = await context.ExamSets
                .AsNoTracking()
                .Where(set =>
                    set.IsActive
                    && set.Status == ExamSetStatus.HandedOver
                    && set.CourseId == session.CourseId
                    && (!set.SessionId.HasValue || set.SessionId == session.Id))
                .OrderBy(set => set.HandedOverAt)
                .ThenBy(set => set.Id)
                .Select(set => set.Id)
                .ToListAsync(cancellationToken);

            foreach (var group in groupPlans)
            {
                foreach (var room in group.Rooms.Where(item => item.AssignedNow > 0))
                {
                    var availableSetsInRoomCount = candidateExamSets.Count(setId => !room.ExistingExamSetIds.Contains(setId));
                    if (availableSetsInRoomCount < room.AssignedNow)
                    {
                        return Result<AutoAllocateSessionStudentsResponse>.Failure(
                            $"UNPROCESSABLE: Không đủ bộ đề HANDED_OVER khả dụng cho phòng '{room.RoomLab}': cần cấp {room.AssignedNow} bộ đề không trùng lặp, hiện chỉ có {availableSetsInRoomCount} bộ đề khả dụng.");
                    }
                }
            }
        }

        if (!request.DryRun)
        {
            var nowUtc = DateTime.UtcNow;
            var newTickets = new List<StudentExamTicket>(unallocatedStudents.Count);
            var studentCursor = 0;

            foreach (var group in groupPlans)
            {
                foreach (var room in group.Rooms)
                {
                    if (room.AssignedNow <= 0)
                    {
                        continue;
                    }

                    List<Guid>? shuffledRoomExamSets = null;
                    if (request.AssignHandedOverExamSets)
                    {
                        shuffledRoomExamSets = candidateExamSets
                            .Where(setId => !room.ExistingExamSetIds.Contains(setId))
                            .ToList();
                        OfficialExamSchedulingHelper.ShuffleInPlace(shuffledRoomExamSets, _randomGenerator);
                    }

                    for (var index = 0; index < room.AssignedNow; index++)
                    {
                        var studentId = unallocatedStudents[studentCursor++];
                        var seatNumber = room.AvailableSeatNumbers[index];
                        var examSetId = shuffledRoomExamSets is not null ? shuffledRoomExamSets[index] : (Guid?)null;

                        newTickets.Add(new StudentExamTicket
                        {
                            Id = Guid.NewGuid(),
                            ShiftId = room.ShiftId,
                            StudentId = studentId,
                            ExamSetId = examSetId,
                            SeatNumber = seatNumber,
                            IpAddress = null,
                            Status = ExamTicketStatus.Scheduled,
                            StudentAcknowledgementStatus = StudentAcknowledgementStatus.Pending,
                            IsLocked = false,
                            CheckedInAt = nowUtc
                        });
                    }
                }
            }

            context.StudentExamTickets.AddRange(newTickets);
            context.AuditLogs.Add(new AuditLog
            {
                UserId = request.ActorUserId,
                Action = "OFFICIAL_SESSION_STUDENTS_AUTO_ALLOCATED",
                EntityName = nameof(OfficialExamSession),
                EntityId = session.Id,
                NewValues = JsonSerializer.Serialize(new
                {
                    sessionId = session.Id,
                    newlyAllocatedCount = newTickets.Count,
                    assignHandedOverExamSets = request.AssignHandedOverExamSets,
                    rooms = groupPlans
                        .SelectMany(group => group.Rooms)
                        .Where(room => room.AssignedNow > 0)
                        .Select(room => new { room.ShiftId, room.RoomLab, room.AssignedBefore, room.AssignedNow, room.TotalAfter })
                        .ToArray()
                }),
                CreatedAt = nowUtc
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        var responseGroups = groupPlans
            .Select(group => new AutoAllocatedShiftGroupDto(
                group.StartTime,
                group.EndTime,
                group.IsFinalBalancedShift,
                group.Rooms.Sum(room => room.AssignedNow),
                group.Rooms
                    .Select(room => new AutoAllocatedRoomDto(
                        room.ShiftId,
                        room.RoomLab,
                        Math.Min(OfficialExamRules.DefaultShiftCapacity, room.MaxSeats),
                        room.AssignedBefore,
                        room.AssignedNow,
                        room.TotalAfter))
                    .ToList()))
            .ToList();

        return Result<AutoAllocateSessionStudentsResponse>.Success(new AutoAllocateSessionStudentsResponse(
            session.Id,
            request.DryRun,
            totalEligibleStudents,
            previouslyAllocatedCount,
            unallocatedStudents.Count,
            responseGroups));
    }

    private static Result<List<RealExamSessionShift>> FilterTargetShifts(
        IReadOnlyList<RealExamSessionShift> allActiveSessionShifts,
        IReadOnlyList<Guid>? requestedShiftIds)
    {
        if (requestedShiftIds is null || requestedShiftIds.Count == 0)
        {
            var eligibleShifts = allActiveSessionShifts
                .Where(shift =>
                    string.Equals(shift.Status, "scheduled", StringComparison.OrdinalIgnoreCase)
                    && !shift.IsExamPublished)
                .ToList();
            return Result<List<RealExamSessionShift>>.Success(eligibleShifts);
        }

        var requestedSet = requestedShiftIds.ToHashSet();
        var shiftMap = allActiveSessionShifts.ToDictionary(shift => shift.Id);

        foreach (var shiftId in requestedSet)
        {
            if (!shiftMap.TryGetValue(shiftId, out var found))
            {
                return Result<List<RealExamSessionShift>>.Failure(
                    $"NOT_FOUND: Ca thi '{shiftId}' không tồn tại hoặc không thuộc kỳ thi này.");
            }

            if (!string.Equals(found.Status, "scheduled", StringComparison.OrdinalIgnoreCase) || found.IsExamPublished)
            {
                return Result<List<RealExamSessionShift>>.Failure(
                    $"CONFLICT: Phòng thi '{found.RoomLab}' không còn ở trạng thái scheduled hoặc đã xuất bản đề thi nên không thể phân bổ tự động.");
            }
        }

        var filtered = allActiveSessionShifts
            .Where(shift => requestedSet.Contains(shift.Id))
            .ToList();
        return Result<List<RealExamSessionShift>>.Success(filtered);
    }
}

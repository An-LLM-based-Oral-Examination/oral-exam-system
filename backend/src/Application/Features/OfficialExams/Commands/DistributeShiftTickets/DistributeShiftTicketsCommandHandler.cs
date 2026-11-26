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

namespace OralExamination.Application.Features.OfficialExams.Commands.DistributeShiftTickets;

public sealed class DistributeShiftTicketsCommandHandler(
    IApplicationDbContext context,
    IOfficialExamRandomGenerator? randomGenerator = null)
    : IRequestHandler<DistributeShiftTicketsCommand, Result<DistributeShiftTicketsResponse>>
{
    private readonly IOfficialExamRandomGenerator _randomGenerator =
        randomGenerator ?? CryptoOfficialExamRandomGenerator.Instance;

    public async Task<Result<DistributeShiftTicketsResponse>> Handle(
        DistributeShiftTicketsCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorRole is not null
            && !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.Ordinal))
        {
            return Result<DistributeShiftTicketsResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<DistributeShiftTicketsResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (request.ShiftId == Guid.Empty)
        {
            return Result<DistributeShiftTicketsResponse>.Failure("Mã ca thi không được để trống.");
        }

        return await context.ExecuteShiftMutationAsync(request.ShiftId, true, async ct =>
        {
            var shift = await context.RealExamSessionShifts
                .Include(s => s.Session)
                .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

            if (shift is null)
            {
                return Result<DistributeShiftTicketsResponse>.Failure(OfficialExamErrors.ShiftNotFound);
            }

            if (shift.Status is ExamShiftWindow.Completed or ExamShiftWindow.Cancelled)
            {
                return Result<DistributeShiftTicketsResponse>.Failure("CONFLICT: Không thể phát đề cho ca thi đã kết thúc hoặc bị hủy.");
            }

            var tickets = await context.StudentExamTickets
                .Include(t => t.Student)
                .Where(t => t.ShiftId == request.ShiftId)
                .OrderBy(t => t.SeatNumber)
                .ToListAsync(ct);

            var unsetTickets = tickets.Where(t => !t.ExamSetId.HasValue).ToList();
            if (unsetTickets.Count == 0)
            {
                return Result<DistributeShiftTicketsResponse>.Success(new DistributeShiftTicketsResponse(
                    shift.Id,
                    0,
                    "Tất cả vé thi trong ca thi đã được gán bộ đề.",
                    Array.Empty<DistributeShiftTicketsItemDto>()));
            }

            var usedExamSetIds = tickets
                .Where(t => t.ExamSetId.HasValue)
                .Select(t => t.ExamSetId!.Value)
                .ToHashSet();

            var candidateExamSetsQuery = context.ExamSets
                .Where(set => set.IsActive
                    && !set.IsSampleSet
                    && set.Status == ExamSetStatus.HandedOver
                    && set.CourseId == shift.Session.CourseId
                    && (!set.SessionId.HasValue || set.SessionId == shift.SessionId)
                    && !usedExamSetIds.Contains(set.Id));

            if (shift.Session.ExamStructureId.HasValue && shift.Session.ExamStructureId.Value != Guid.Empty)
            {
                candidateExamSetsQuery = candidateExamSetsQuery.Where(
                    set => set.StructureId == shift.Session.ExamStructureId.Value);
            }

            var candidateExamSets = await candidateExamSetsQuery
                .OrderBy(set => set.HandedOverAt)
                .ThenBy(set => set.Id)
                .ToListAsync(ct);

            if (candidateExamSets.Count < unsetTickets.Count)
            {
                return Result<DistributeShiftTicketsResponse>.Failure(
                    $"UNPROCESSABLE: Không đủ bộ đề HANDED_OVER khả dụng cho ca thi: cần cấp {unsetTickets.Count} bộ đề không trùng lặp, hiện chỉ có {candidateExamSets.Count} bộ đề khả dụng.");
            }

            if (request.AllowRandomShuffle)
            {
                OfficialExamSchedulingHelper.ShuffleInPlace(candidateExamSets, _randomGenerator);
            }

            var distributedItems = new List<DistributeShiftTicketsItemDto>(unsetTickets.Count);
            for (var i = 0; i < unsetTickets.Count; i++)
            {
                var ticket = unsetTickets[i];
                var examSet = candidateExamSets[i];
                ticket.ExamSetId = examSet.Id;
                distributedItems.Add(new DistributeShiftTicketsItemDto(
                    ticket.Id,
                    ticket.Student?.StudentCode ?? string.Empty,
                    ticket.SeatNumber,
                    examSet.Id));
            }

            context.AuditLogs.Add(new AuditLog
            {
                UserId = request.ActorUserId,
                Action = "OFFICIAL_SHIFT_TICKETS_DISTRIBUTED",
                EntityName = nameof(RealExamSessionShift),
                EntityId = shift.Id,
                NewValues = JsonSerializer.Serialize(new
                {
                    shiftId = shift.Id,
                    totalDistributed = distributedItems.Count,
                    allowRandomShuffle = request.AllowRandomShuffle,
                    distributed = distributedItems.Select(d => new { d.TicketId, d.SeatNumber, d.ExamSetId }).ToArray()
                }),
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(ct);

            return Result<DistributeShiftTicketsResponse>.Success(new DistributeShiftTicketsResponse(
                shift.Id,
                distributedItems.Count,
                "Đã phân bổ thành công bộ đề thi từ kho HANDED_OVER, đảm bảo không trùng lặp đề trong ca thi.",
                distributedItems));
        }, cancellationToken);
    }
}

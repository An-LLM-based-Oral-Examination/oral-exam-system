using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShiftStatus;

public sealed class UpdateExamShiftStatusCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateExamShiftStatusCommand, Result<ExamShiftResponse>>
{
    public async Task<Result<ExamShiftResponse>> Handle(
        UpdateExamShiftStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorRole is not null
            && !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.Ordinal)
            && !string.Equals(request.ActorRole, UserRole.Admin, StringComparison.Ordinal))
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!await OfficialExamAuthorization.IsActiveOfficerOrAdminAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (request.ShiftId == Guid.Empty)
        {
            return Result<ExamShiftResponse>.Failure("Mã ca thi không được để trống.");
        }

        var targetStatus = request.Status?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(targetStatus))
        {
            return Result<ExamShiftResponse>.Failure("Trạng thái ca thi không được để trống.");
        }

        if (targetStatus is not (ExamShiftWindow.Scheduled or ExamShiftWindow.InProgress or ExamShiftWindow.Completed or ExamShiftWindow.Cancelled))
        {
            return Result<ExamShiftResponse>.Failure($"BAD_REQUEST: Trạng thái ca thi '{request.Status}' không hợp lệ. Chỉ chấp nhận: scheduled, in_progress, completed, cancelled.");
        }

        return await context.ExecuteShiftMutationAsync(request.ShiftId, true, async ct =>
        {
            var shift = await context.RealExamSessionShifts
                .Include(s => s.ProctorUser)
                .Include(s => s.ExaminerUser)
                .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

            if (shift is null)
            {
                return Result<ExamShiftResponse>.Failure(OfficialExamErrors.ShiftNotFound);
            }

            var currentStatus = shift.Status?.Trim().ToLowerInvariant() ?? ExamShiftWindow.Scheduled;

            if (string.Equals(currentStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Result<ExamShiftResponse>.Success(ToResponse(shift));
            }

            if (currentStatus == ExamShiftWindow.Completed)
            {
                return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã hoàn thành (completed), không thể chuyển đổi trạng thái.");
            }

            if (currentStatus == ExamShiftWindow.Cancelled)
            {
                return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã bị hủy (cancelled), không thể chuyển đổi trạng thái.");
            }

            if (targetStatus == ExamShiftWindow.InProgress)
            {
                if (currentStatus != ExamShiftWindow.Scheduled)
                {
                    return Result<ExamShiftResponse>.Failure($"CONFLICT: Không thể chuyển ca thi từ trạng thái '{currentStatus}' sang '{ExamShiftWindow.InProgress}'.");
                }

                shift.IsExamPublished = true;
                shift.PublishedAt ??= DateTime.UtcNow;
                shift.Status = ExamShiftWindow.InProgress;
            }
            else if (targetStatus == ExamShiftWindow.Completed)
            {
                if (!shift.IsExamPublished)
                {
                    shift.IsExamPublished = true;
                    shift.PublishedAt ??= DateTime.UtcNow;
                }
                shift.Status = ExamShiftWindow.Completed;
            }
            else if (targetStatus == ExamShiftWindow.Cancelled)
            {
                var hasSubmissions = await context.StudentExamTickets
                    .AsNoTracking()
                    .Where(t => t.ShiftId == shift.Id)
                    .AnyAsync(t => t.Submissions.Any()
                        || t.Status == ExamTicketStatus.Submitted
                        || t.Status == ExamTicketStatus.AiGraded
                        || t.Status == ExamTicketStatus.Audited
                        || t.Status == ExamTicketStatus.Published, ct);

                if (hasSubmissions)
                {
                    return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã có bài làm của thí sinh, không thể hủy.");
                }

                shift.Status = ExamShiftWindow.Cancelled;
            }
            else if (targetStatus == ExamShiftWindow.Scheduled)
            {
                return Result<ExamShiftResponse>.Failure($"CONFLICT: Không thể chuyển ca thi từ trạng thái '{currentStatus}' về '{ExamShiftWindow.Scheduled}'.");
            }

            context.AuditLogs.Add(new AuditLog
            {
                UserId = request.ActorUserId,
                Action = "OFFICIAL_EXAM_SHIFT_STATUS_UPDATED",
                EntityName = nameof(RealExamSessionShift),
                EntityId = shift.Id,
                OldValues = JsonSerializer.Serialize(new { status = currentStatus, isExamPublished = shift.IsExamPublished }),
                NewValues = JsonSerializer.Serialize(new { status = shift.Status, isExamPublished = shift.IsExamPublished, publishedAt = shift.PublishedAt }),
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(ct);

            return Result<ExamShiftResponse>.Success(ToResponse(shift));
        }, cancellationToken);
    }

    private static ExamShiftResponse ToResponse(RealExamSessionShift shift) =>
        new(
            shift.Id,
            shift.SessionId,
            shift.ShiftName,
            shift.RoomLab,
            shift.StartTime,
            shift.EndTime,
            shift.ProctorUserId,
            shift.Status,
            shift.ExaminerUserId,
            shift.ExaminerUser?.FullName,
            shift.ProctorUser?.FullName,
            shift.MaxSeats);
}

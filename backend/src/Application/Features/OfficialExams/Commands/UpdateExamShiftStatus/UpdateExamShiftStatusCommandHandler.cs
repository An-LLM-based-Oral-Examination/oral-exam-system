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
            && !string.Equals(request.ActorRole, UserRole.ExaminationOfficer, StringComparison.Ordinal))
        {
            return Result<ExamShiftResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
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

            var oldStatus = shift.Status?.Trim().ToLowerInvariant() ?? ExamShiftWindow.Scheduled;
            var oldIsExamPublished = shift.IsExamPublished;

            if (string.Equals(oldStatus, targetStatus, StringComparison.OrdinalIgnoreCase))
            {
                return Result<ExamShiftResponse>.Success(await BuildShiftResponseAsync(shift, ct));
            }

            if (oldStatus == ExamShiftWindow.Completed)
            {
                return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã hoàn thành (completed), không thể chuyển đổi trạng thái.");
            }

            if (oldStatus == ExamShiftWindow.Cancelled)
            {
                return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã bị hủy (cancelled), không thể chuyển đổi trạng thái.");
            }

            if (targetStatus == ExamShiftWindow.InProgress)
            {
                if (oldStatus != ExamShiftWindow.Scheduled)
                {
                    return Result<ExamShiftResponse>.Failure($"CONFLICT: Không thể chuyển ca thi từ trạng thái '{oldStatus}' sang '{ExamShiftWindow.InProgress}'.");
                }

                if (!shift.IsExamPublished)
                {
                    var hasUnassignedTicket = await context.StudentExamTickets
                        .AsNoTracking()
                        .AnyAsync(t => t.ShiftId == shift.Id && t.ExamSetId == null, ct);

                    if (hasUnassignedTicket)
                    {
                        return Result<ExamShiftResponse>.Failure("CONFLICT: Còn thí sinh chưa được phân bổ bộ đề thi. Vui lòng chạy phát đề trước khi mở ca thi.");
                    }
                }

                shift.IsExamPublished = true;
                shift.PublishedAt ??= DateTime.UtcNow;
                shift.Status = ExamShiftWindow.InProgress;
            }
            else if (targetStatus == ExamShiftWindow.Completed)
            {
                shift.Status = ExamShiftWindow.Completed;
            }
            else if (targetStatus == ExamShiftWindow.Cancelled)
            {
                var hasActiveOrSubmittedTickets = await context.StudentExamTickets
                    .AsNoTracking()
                    .Where(t => t.ShiftId == shift.Id)
                    .AnyAsync(t => t.Submissions.Any()
                        || t.IsLocked
                        || t.Status == ExamTicketStatus.InProgress
                        || t.Status == ExamTicketStatus.Submitted
                        || t.Status == ExamTicketStatus.AiGraded
                        || t.Status == ExamTicketStatus.Audited
                        || t.Status == ExamTicketStatus.Published
                        || t.Status == ExamTicketStatus.Locked, ct);

                if (hasActiveOrSubmittedTickets)
                {
                    return Result<ExamShiftResponse>.Failure("CONFLICT: Ca thi đã có thí sinh đang thi, đã nộp bài hoặc vé đã khóa, không thể hủy.");
                }

                shift.Status = ExamShiftWindow.Cancelled;
            }
            else if (targetStatus == ExamShiftWindow.Scheduled)
            {
                return Result<ExamShiftResponse>.Failure($"CONFLICT: Không thể chuyển ca thi từ trạng thái '{oldStatus}' về '{ExamShiftWindow.Scheduled}'.");
            }

            context.AuditLogs.Add(new AuditLog
            {
                UserId = request.ActorUserId,
                Action = "OFFICIAL_EXAM_SHIFT_STATUS_UPDATED",
                EntityName = nameof(RealExamSessionShift),
                EntityId = shift.Id,
                OldValues = JsonSerializer.Serialize(new { status = oldStatus, isExamPublished = oldIsExamPublished }),
                NewValues = JsonSerializer.Serialize(new { status = shift.Status, isExamPublished = shift.IsExamPublished, publishedAt = shift.PublishedAt }),
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync(ct);

            return Result<ExamShiftResponse>.Success(await BuildShiftResponseAsync(shift, ct));
        }, cancellationToken);
    }

    private async Task<ExamShiftResponse> BuildShiftResponseAsync(RealExamSessionShift shift, CancellationToken ct)
    {
        var ticketStats = await context.StudentExamTickets
            .AsNoTracking()
            .Where(t => t.ShiftId == shift.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AssignedTicketsCount = g.Count(),
                IsGradesPublished = g.Any(t => t.Status == ExamTicketStatus.Published),
                GradesPublishedAt = g.Where(t => t.GradesPublishedAt != null).Max(t => t.GradesPublishedAt)
            })
            .FirstOrDefaultAsync(ct);

        var assignedTicketsCount = ticketStats?.AssignedTicketsCount ?? 0;
        var isGradesPublished = ticketStats?.IsGradesPublished ?? false;
        var gradesPublishedAt = ticketStats?.GradesPublishedAt;
        var remainingSeats = Math.Max(0, shift.MaxSeats - assignedTicketsCount);
        var attemptTypeRaw = shift.Session?.ExamAttemptType;
        if (string.IsNullOrWhiteSpace(attemptTypeRaw))
        {
            attemptTypeRaw = await context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => s.Id == shift.SessionId)
                .Select(s => s.ExamAttemptType)
                .FirstOrDefaultAsync(ct);
        }

        var attemptType = string.IsNullOrWhiteSpace(attemptTypeRaw)
            ? OfficialExamAttemptType.FinalExam
            : attemptTypeRaw;

        return new ExamShiftResponse(
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
            shift.MaxSeats,
            shift.IsExamPublished,
            isGradesPublished,
            gradesPublishedAt,
            shift.CertificateHash,
            attemptType,
            assignedTicketsCount,
            remainingSeats);
    }
}

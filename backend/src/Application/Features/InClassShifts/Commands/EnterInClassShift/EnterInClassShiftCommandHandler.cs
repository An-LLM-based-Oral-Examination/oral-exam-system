using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Common.Security;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;

public sealed class EnterInClassShiftCommandHandler(
    IApplicationDbContext context,
    IValidator<EnterInClassShiftCommand> validator)
    : IRequestHandler<EnterInClassShiftCommand, Result<JoinInClassShiftResponse>>
{
    private const int GracePeriodSeconds = 45;

    public async Task<Result<JoinInClassShiftResponse>> Handle(
        EnterInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<JoinInClassShiftResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftLocked);
        }

        if (shift.Status == "CANCELLED")
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ShiftCancelled);
        }

        bool isEnrolled = await context.ClassEnrollments
            .AsNoTracking()
            .AnyAsync(
                e => e.ClassId == shift.ClassId && e.StudentId == request.StudentId && e.Status == "enrolled",
                cancellationToken);

        if (!isEnrolled)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.StudentNotEnrolled);
        }

        DateTime utcNow = DateTime.UtcNow;
        string normalizedOtp = request.OtpCode.Trim();

        // 1. Kiểm tra theo thuật toán TOTP 5 phút (kèm Grace Period 60s ở cửa sổ liền kề)
        bool isValidOtp = TotpPasswordHelper.ValidateCode(
            shift.TotpSecretKey,
            normalizedOtp,
            utcNow,
            gracePeriodSeconds: GracePeriodSeconds);

        // 2. Fallback đối chiếu với bản ghi mật khẩu lưu trong DB (hỗ trợ cả mật khẩu liền kề còn trong Grace Period)
        if (!isValidOtp)
        {
            DateTime graceCutoff = utcNow.AddSeconds(-GracePeriodSeconds);
            isValidOtp = await context.InClassShiftPasswords
                .AsNoTracking()
                .AnyAsync(
                    p => p.ShiftId == shift.Id &&
                         p.PlainCode == normalizedOtp &&
                         p.ValidFrom <= utcNow &&
                         p.ValidTo >= graceCutoff,
                    cancellationToken);
        }

        if (!isValidOtp)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.InvalidOtp);
        }

        int targetQuestionCount = Math.Clamp(shift.TotalQuestionsPerStudent <= 0 ? 1 : shift.TotalQuestionsPerStudent, 1, 10);

        var existingParticipant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                cancellationToken);

        if (existingParticipant is not null)
        {
            var existingIds = ParseGuidList(existingParticipant.AssignedQuestionIdsJson);
            if (existingIds.Count == 0 && existingParticipant.AssignedQuestionId.HasValue)
            {
                existingIds.Add(existingParticipant.AssignedQuestionId.Value);
            }

            if (existingIds.Count == 0)
            {
                existingIds = await AllocateQuestionsForStudentAsync(shift, targetQuestionCount, cancellationToken);
                if (existingIds.Count < targetQuestionCount)
                {
                    return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.InsufficientQuestionPool);
                }

                existingParticipant.AssignedQuestionId = existingIds[0];
                existingParticipant.AssignedQuestionIdsJson = JsonSerializer.Serialize(existingIds);
                await context.SaveChangesAsync(cancellationToken);
            }

            var existingDtos = await LoadQuestionDtosAsync(existingIds, cancellationToken);
            return Result<JoinInClassShiftResponse>.Success(new JoinInClassShiftResponse(
                shift.Id,
                existingParticipant.StudentId,
                shift.ShiftName,
                existingParticipant.Status,
                existingParticipant.JoinedAt,
                existingParticipant.AssignedQuestionId,
                existingDtos.FirstOrDefault(),
                existingIds,
                existingDtos,
                targetQuestionCount,
                shift.HasFollowUp,
                shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0
            ));
        }

        var assignedQuestionIds = await AllocateQuestionsForStudentAsync(shift, targetQuestionCount, cancellationToken);
        if (assignedQuestionIds.Count < targetQuestionCount)
        {
            return Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.InsufficientQuestionPool);
        }

        if (shift.Status == "SCHEDULED")
        {
            shift.Status = "IN_PROGRESS";
            shift.UpdatedAt = utcNow;
        }

        var assignedQuestionDtos = await LoadQuestionDtosAsync(assignedQuestionIds, cancellationToken);

        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = request.StudentId,
            AssignedQuestionId = assignedQuestionIds[0],
            AssignedQuestionIdsJson = JsonSerializer.Serialize(assignedQuestionIds),
            JoinedAt = utcNow,
            Status = "JOINED"
        };

        context.InClassShiftParticipants.Add(participant);
        await context.SaveChangesAsync(cancellationToken);

        var response = new JoinInClassShiftResponse(
            shift.Id,
            participant.StudentId,
            shift.ShiftName,
            participant.Status,
            participant.JoinedAt,
            participant.AssignedQuestionId,
            assignedQuestionDtos.FirstOrDefault(),
            assignedQuestionIds,
            assignedQuestionDtos,
            targetQuestionCount,
            shift.HasFollowUp,
            shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0
        );

        return Result<JoinInClassShiftResponse>.Success(response);
    }

    /// <summary>
    /// Phân bổ bộ N câu hỏi từ kho câu hỏi luyện tập (PracticeQuestions) đã APPROVED của môn học (cô lập R8),
    /// hỗ trợ chế độ ALL_COURSE_BANK / SELECTED_POOL và thuật toán Anti-Collision ưu tiên câu hỏi ít xuất hiện nhất trong ca thi.
    /// </summary>
    private async Task<List<Guid>> AllocateQuestionsForStudentAsync(
        InClassExamShift shift,
        int targetCount,
        CancellationToken cancellationToken)
    {
        var query = context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == shift.Class.CourseId &&
                        q.ApprovalStatus == ApprovalStatus.Approved &&
                        q.IsActive);

        if (string.Equals(shift.QuestionPoolMode, "SELECTED_POOL", StringComparison.OrdinalIgnoreCase))
        {
            var poolIds = ParseGuidList(shift.SelectedQuestionIdsJson);
            if (poolIds.Count > 0)
            {
                query = query.Where(q => poolIds.Contains(q.Id));
            }
        }

        var candidateIds = await query
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count < targetCount)
        {
            return new List<Guid>();
        }

        var existingParticipants = await context.InClassShiftParticipants
            .AsNoTracking()
            .Where(p => p.ShiftId == shift.Id)
            .Select(p => new { p.AssignedQuestionId, p.AssignedQuestionIdsJson })
            .ToListAsync(cancellationToken);

        var usageCounts = candidateIds.ToDictionary(id => id, _ => 0);

        foreach (var p in existingParticipants)
        {
            var ids = ParseGuidList(p.AssignedQuestionIdsJson);
            if (ids.Count == 0 && p.AssignedQuestionId.HasValue)
            {
                ids.Add(p.AssignedQuestionId.Value);
            }

            foreach (var id in ids.Distinct())
            {
                if (usageCounts.ContainsKey(id))
                {
                    usageCounts[id]++;
                }
            }
        }

        return candidateIds
            .OrderBy(id => usageCounts[id])
            .ThenBy(_ => Random.Shared.Next())
            .Take(Math.Min(targetCount, candidateIds.Count))
            .ToList();
    }

    private async Task<List<InClassAssignedQuestionDto>> LoadQuestionDtosAsync(
        IReadOnlyList<Guid> questionIds,
        CancellationToken cancellationToken)
    {
        if (questionIds.Count == 0)
        {
            return new List<InClassAssignedQuestionDto>();
        }

        var rawQuestions = await context.PracticeQuestions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .Select(q => new InClassAssignedQuestionDto(
                q.Id,
                q.Title,
                q.Content,
                q.Difficulty.ToString().ToLowerInvariant(),
                q.BloomLevel.ToString().ToLowerInvariant()))
            .ToListAsync(cancellationToken);

        var dict = rawQuestions.ToDictionary(q => q.QuestionId);
        var ordered = new List<InClassAssignedQuestionDto>(questionIds.Count);
        foreach (var id in questionIds)
        {
            if (dict.TryGetValue(id, out var dto))
            {
                ordered.Add(dto);
            }
        }

        return ordered;
    }

    internal static List<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<Guid>>(json);
            return parsed?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }
}

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
    IValidator<EnterInClassShiftCommand> validator,
    IGradingQueueChannel gradingQueue)
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

        var outcome = await context.ExecuteInClassJoinAsync(
            request.ShiftId,
            ct => HandleSerialized(request, ct),
            cancellationToken);

        // Queue publication is deliberately outside the retryable database transaction.
        if (outcome.ExpiredParticipant is not null)
        {
            await InClassShiftAutoSubmit.FinalizeParticipantAsync(
                context,
                gradingQueue,
                outcome.ExpiredParticipant,
                cancellationToken);
        }

        return outcome.Result;
    }

    private async Task<JoinMutationOutcome> HandleSerialized(
        EnterInClassShiftCommand request,
        CancellationToken cancellationToken)
    {
        var shift = await context.InClassExamShifts
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
            .Include(s => s.Class)
                .ThenInclude(c => c.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Failure(InClassShiftErrors.ClassNotFound);
        }

        if (InClassShiftLifecycle.TryCloseIfDue(shift, DateTime.UtcNow))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (shift.Status is "CLOSED" or "ENDED")
        {
            return Failure(InClassShiftErrors.ShiftClosed);
        }

        if (shift.IsLocked || shift.Status == "FINALIZED")
        {
            return Failure(InClassShiftErrors.ShiftLocked);
        }

        if (shift.Status != "IN_PROGRESS")
        {
            return Failure(InClassShiftErrors.ShiftNotOpen);
        }

        bool isEnrolled = await context.ClassEnrollments
            .AsNoTracking()
            .AnyAsync(
                e => e.ClassId == shift.ClassId && e.StudentId == request.StudentId && e.Status == "enrolled",
                cancellationToken);

        if (!isEnrolled)
        {
            return Failure(InClassShiftErrors.StudentNotEnrolled);
        }

        DateTime utcNow = DateTime.UtcNow;
        string normalizedOtp = request.OtpCode.Trim();

        // 1. Kiểm tra theo thuật toán TOTP 5 phút (kèm Grace Period 60s ở cửa sổ liền kề)
        bool isValidOtp = TotpPasswordHelper.ValidateCode(
            shift.TotpSecretKey,
            normalizedOtp,
            utcNow,
            gracePeriodSeconds: GracePeriodSeconds);

        // 2. Fallback đối chiếu với bản ghi mật khẩu lưu trong DB (ưu tiên cột 3NF grace_until, fallback ValidTo + 45s cho dữ liệu cũ)
        if (!isValidOtp)
        {
            DateTime graceCutoff = utcNow.AddSeconds(-GracePeriodSeconds);
            isValidOtp = await context.InClassShiftPasswords
                .AsNoTracking()
                .AnyAsync(
                    p => p.ShiftId == shift.Id &&
                         p.PlainCode == normalizedOtp &&
                         p.ValidFrom <= utcNow &&
                         ((p.GraceUntil.HasValue && p.GraceUntil.Value >= utcNow) ||
                          (!p.GraceUntil.HasValue && p.ValidTo >= graceCutoff)),
                    cancellationToken);
        }

        if (!isValidOtp)
        {
            return Failure(InClassShiftErrors.InvalidOtp);
        }

        int targetQuestionCount = Math.Clamp(shift.TotalQuestionsPerStudent <= 0 ? 1 : shift.TotalQuestionsPerStudent, 1, 10);

        var existingParticipant = await context.InClassShiftParticipants
            .FirstOrDefaultAsync(
                p => p.ShiftId == shift.Id && p.StudentId == request.StudentId,
                cancellationToken);

        if (existingParticipant is not null)
        {
            if (IsCompletedParticipation(existingParticipant.Status))
            {
                return Failure(InClassShiftErrors.AlreadyParticipated);
            }

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
                    return Failure(InClassShiftErrors.InsufficientQuestionPool);
                }

                existingParticipant.AssignedQuestionId = existingIds[0];
                existingParticipant.AssignedQuestionIdsJson = JsonSerializer.Serialize(existingIds);
                await context.SaveChangesAsync(cancellationToken);
            }

            if (InClassShiftLifecycle.IsPersonalExamTimeExpired(existingParticipant, shift, utcNow))
            {
                return new JoinMutationOutcome(
                    Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.ExamTimeExpired),
                    existingParticipant);
            }

            var existingDtos = await LoadQuestionDtosAsync(existingIds, cancellationToken);
            var existingEndsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(existingParticipant, shift);
            return Success(new JoinInClassShiftResponse(
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
                shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0,
                existingEndsAt,
                InClassShiftLifecycle.GetRemainingExamSeconds(existingEndsAt, utcNow)
            ));
        }

        var assignedQuestionIds = await AllocateQuestionsForStudentAsync(shift, targetQuestionCount, cancellationToken);
        if (assignedQuestionIds.Count < targetQuestionCount)
        {
            return Failure(InClassShiftErrors.InsufficientQuestionPool);
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

        var examEndsAt = InClassShiftLifecycle.GetPersonalExamEndsAt(participant, shift);
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
            shift.HasFollowUp ? shift.MaxFollowUpQuestions : 0,
            examEndsAt,
            InClassShiftLifecycle.GetRemainingExamSeconds(examEndsAt, utcNow)
        );

        return Success(response);
    }

    private static JoinMutationOutcome Failure(string error) =>
        new(Result<JoinInClassShiftResponse>.Failure(error));

    private static JoinMutationOutcome Success(JoinInClassShiftResponse response) =>
        new(Result<JoinInClassShiftResponse>.Success(response));

    private sealed record JoinMutationOutcome(
        Result<JoinInClassShiftResponse> Result,
        InClassShiftParticipant? ExpiredParticipant = null);

    /// <summary>
    /// Phân bổ bộ N câu hỏi từ kho câu hỏi luyện tập (PracticeQuestions) đã APPROVED của môn học (cô lập R8),
    /// hỗ trợ chế độ ALL_COURSE_BANK / SELECTED_CLOS (cùng tương thích ngược SELECTED_POOL cho ca cũ),
    /// cân bằng giữa các CLO & độ khó đã chọn và ưu tiên câu hỏi ít xuất hiện nhất trong ca thi (Anti-Collision).
    /// </summary>
    private async Task<List<Guid>> AllocateQuestionsForStudentAsync(
        InClassExamShift shift,
        int targetCount,
        CancellationToken cancellationToken)
    {
        var baseQuery = context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.CourseId == shift.Class.CourseId &&
                        q.ApprovalStatus == ApprovalStatus.Approved &&
                        q.IsActive &&
                        !q.IsDeleted);

        var mode = (shift.QuestionPoolMode ?? InClassShiftPoolConfig.AllCourseBank).Trim().ToUpperInvariant();
        var selectedCloIds = new List<Guid>();

        if (mode == InClassShiftPoolConfig.SelectedPoolLegacy)
        {
            var poolIds = ParseGuidList(shift.SelectedQuestionIdsJson);
            if (poolIds.Count == 0)
            {
                return new List<Guid>();
            }

            baseQuery = baseQuery.Where(q => poolIds.Contains(q.Id));
        }
        else if (mode == InClassShiftPoolConfig.SelectedClos)
        {
            selectedCloIds = InClassShiftPoolConfig.ParseGuidList(shift.SelectedCloIdsJson);
            if (selectedCloIds.Count == 0)
            {
                return new List<Guid>();
            }

            var activeCloIds = await context.CourseClos
                .AsNoTracking()
                .Where(c =>
                    c.CourseId == shift.Class.CourseId &&
                    c.IsActive &&
                    selectedCloIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (activeCloIds.Count != selectedCloIds.Count)
            {
                return new List<Guid>();
            }

            baseQuery = baseQuery.Where(q => q.CloId.HasValue && activeCloIds.Contains(q.CloId.Value));
        }

        var rawQuestions = await baseQuery
            .Select(q => new
            {
                q.Id,
                q.CloId,
                q.Difficulty
            })
            .ToListAsync(cancellationToken);

        var allowedDiffs = mode == InClassShiftPoolConfig.SelectedPoolLegacy
            ? InClassShiftPoolConfig.CanonicalDifficulties.ToHashSet(StringComparer.Ordinal)
            : InClassShiftPoolConfig.ParseStoredDifficulties(shift.SelectedDifficultiesJson)
                .ToHashSet(StringComparer.Ordinal);

        var candidates = new List<PoolCandidateQuestion>(rawQuestions.Count);
        foreach (var q in rawQuestions)
        {
            if (InClassShiftPoolConfig.TryNormalizeDifficulty(q.Difficulty, out var canonicalDiff) &&
                allowedDiffs.Contains(canonicalDiff))
            {
                candidates.Add(new PoolCandidateQuestion(q.Id, q.CloId, canonicalDiff));
            }
        }

        if (candidates.Count < targetCount)
        {
            return new List<Guid>();
        }

        var existingParticipants = await context.InClassShiftParticipants
            .AsNoTracking()
            .Where(p => p.ShiftId == shift.Id)
            .Select(p => new { p.AssignedQuestionId, p.AssignedQuestionIdsJson })
            .ToListAsync(cancellationToken);

        var usageCounts = candidates.ToDictionary(q => q.Id, _ => 0);

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

        return InClassShiftPoolConfig.AllocateBalancedQuestions(
            candidates,
            mode,
            selectedCloIds,
            targetCount,
            usageCounts);
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
                q.BloomLevel.ToString().ToLowerInvariant(),
                q.ImageUrl,
                q.ImageDescription))
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

    internal static bool IsCompletedParticipation(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return status.Equals("SUBMITTED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("GRADED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("FINALIZED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
    }
}

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.UpdateOfficialExamSession;

public sealed class UpdateOfficialExamSessionCommandHandler(
    IApplicationDbContext context,
    IValidator<UpdateOfficialExamSessionCommand>? validator = null)
    : IRequestHandler<UpdateOfficialExamSessionCommand, Result<OfficialExamSessionResponse>>
{
    private readonly IValidator<UpdateOfficialExamSessionCommand> _validator =
        validator ?? new UpdateOfficialExamSessionCommandValidator();

    public async Task<Result<OfficialExamSessionResponse>> Handle(
        UpdateOfficialExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(
                context,
                request.ActorUserId,
                request.ActorRole,
                UserRole.ExaminationOfficer,
                cancellationToken))
        {
            return Result<OfficialExamSessionResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<OfficialExamSessionResponse>.Failure($"BAD_REQUEST: {validation.Errors[0].ErrorMessage}");
        }

        return await context.ExecuteOfficialSessionCandidatesAsync(
            request.SessionId,
            ct => HandleLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result<OfficialExamSessionResponse>> HandleLockedAsync(
        UpdateOfficialExamSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await context.OfficialExamSessions
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            return Result<OfficialExamSessionResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (!string.Equals(session.Status, "scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "CONFLICT: Chỉ có thể cập nhật kỳ thi đang ở trạng thái scheduled.");
        }

        var hasShifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .AnyAsync(sh => sh.SessionId == session.Id, cancellationToken);

        if (hasShifts)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "CONFLICT: Kỳ thi đã có ca thi phòng Lab, không thể chỉnh sửa thông tin kỳ thi.");
        }

        var hasTickets = await context.StudentExamTickets
            .AsNoTracking()
            .AnyAsync(t => t.Shift.SessionId == session.Id, cancellationToken);

        if (hasTickets)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "CONFLICT: Kỳ thi đã có vé thi được cấp, không thể chỉnh sửa thông tin kỳ thi.");
        }

        Guid? resolvedStructureId = request.ExamStructureId.HasValue && request.ExamStructureId.Value != Guid.Empty
            ? request.ExamStructureId.Value
            : null;

        if (resolvedStructureId.HasValue)
        {
            var structureMatchesCourse = await context.ExamStructures
                .AsNoTracking()
                .AnyAsync(
                    structure => structure.Id == resolvedStructureId.Value
                        && structure.CourseId == session.CourseId
                        && structure.IsActive,
                    cancellationToken);

            if (!structureMatchesCourse)
            {
                return Result<OfficialExamSessionResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.StructureNotFound}");
            }
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            title = session.Title,
            examDate = session.ExamDate,
            examStructureId = session.ExamStructureId,
            examInputMode = session.ExamInputMode,
            transcriptBufferSeconds = session.TranscriptBufferSeconds
        });

        session.Title = request.Title!.Trim();
        session.ExamDate = request.ExamDate;
        session.ExamStructureId = resolvedStructureId;
        session.ExamInputMode = request.ExamInputMode!;
        session.TranscriptBufferSeconds = request.TranscriptBufferSeconds;

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "OFFICIAL_EXAM_SESSION_UPDATED",
            EntityName = nameof(OfficialExamSession),
            EntityId = session.Id,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new
            {
                title = session.Title,
                examDate = session.ExamDate,
                examStructureId = session.ExamStructureId,
                examInputMode = session.ExamInputMode,
                transcriptBufferSeconds = session.TranscriptBufferSeconds
            }),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        var candidateCount = await context.OfficialExamCandidates
            .AsNoTracking()
            .CountAsync(c => c.SessionId == session.Id, cancellationToken);

        var resolvedSemesterId = session.SemesterId != Guid.Empty
            ? session.SemesterId
            : session.Course?.SemesterId;

        var attemptType = string.IsNullOrWhiteSpace(session.ExamAttemptType)
            ? OfficialExamAttemptType.FinalExam
            : session.ExamAttemptType;

        return Result<OfficialExamSessionResponse>.Success(new OfficialExamSessionResponse(
            session.Id,
            session.CourseId,
            session.ExamStructureId,
            session.Title,
            session.ExamDate,
            session.HasFollowUp,
            session.MaxFollowUpQuestions,
            session.ExamInputMode,
            session.TranscriptBufferSeconds,
            session.Status,
            resolvedSemesterId,
            attemptType,
            session.Course?.Code,
            session.Course?.Name,
            candidateCount,
            0));
    }
}

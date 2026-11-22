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

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

public sealed class CommitSessionCandidateImportCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CommitSessionCandidateImportCommand, Result<CandidateRosterImportCommitResponse>>
{
    public async Task<Result<CandidateRosterImportCommitResponse>> Handle(
        CommitSessionCandidateImportCommand request,
        CancellationToken cancellationToken)
    {
        if (!await OfficialExamAuthorization.IsActiveOfficerAsync(context, request.ActorUserId, cancellationToken))
        {
            return Result<CandidateRosterImportCommitResponse>.Failure($"FORBIDDEN: {OfficialExamErrors.Forbidden}");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Result<CandidateRosterImportCommitResponse>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        return await context.ExecuteOfficialSessionCandidatesAsync(
            request.SessionId,
            ct => CommitLockedAsync(request, ct),
            cancellationToken);
    }

    private async Task<Result<CandidateRosterImportCommitResponse>> CommitLockedAsync(
        CommitSessionCandidateImportCommand request,
        CancellationToken cancellationToken)
    {
        var evaluationResult = await CandidateRosterImportEvaluator.EvaluateAsync(
            context,
            request.SessionId,
            request.Mode,
            request.Rows,
            cancellationToken);

        if (!evaluationResult.IsSuccess)
        {
            return Result<CandidateRosterImportCommitResponse>.Failure(evaluationResult.Error);
        }

        var eval = evaluationResult.Value;
        if (!eval.Preview.CanCommit)
        {
            return Result<CandidateRosterImportCommitResponse>.Failure(
                $"UNPROCESSABLE: Danh sách thí sinh có {eval.Preview.InvalidCount} dòng không hợp lệ. Vui lòng sửa toàn bộ lỗi trước khi xác nhận nhập.");
        }

        var nowUtc = DateTime.UtcNow;
        IReadOnlyList<Guid> toInsert;

        if (eval.Mode == CandidateRosterImportMode.Replace)
        {
            var existingCandidates = await context.OfficialExamCandidates
                .Where(c => c.SessionId == eval.SessionId)
                .ToListAsync(cancellationToken);

            if (existingCandidates.Count > 0)
            {
                context.OfficialExamCandidates.RemoveRange(existingCandidates);
            }

            toInsert = eval.ValidStudentIds;
        }
        else
        {
            toInsert = eval.NetNewStudentIds;
        }

        if (toInsert.Count > 0)
        {
            var newEntities = toInsert.Select(studentId => new OfficialExamCandidate
            {
                Id = Guid.NewGuid(),
                SessionId = eval.SessionId,
                StudentId = studentId,
                ImportedBy = request.ActorUserId,
                ImportedAt = nowUtc
            });
            context.OfficialExamCandidates.AddRange(newEntities);
        }

        var totalCandidateCount = eval.Preview.ProjectedTotalCandidates;
        var recommendedTotalRooms = OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(totalCandidateCount);

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId,
            Action = "OFFICIAL_SESSION_CANDIDATES_IMPORTED",
            EntityName = nameof(OfficialExamSession),
            EntityId = eval.SessionId,
            NewValues = JsonSerializer.Serialize(new
            {
                sessionId = eval.SessionId,
                examAttemptType = eval.ExamAttemptType,
                mode = eval.Mode,
                importedCount = toInsert.Count,
                totalCandidateCount,
                duplicateCount = eval.Preview.DuplicateCount
            }),
            CreatedAt = nowUtc
        });

        await context.SaveChangesAsync(cancellationToken);

        return Result<CandidateRosterImportCommitResponse>.Success(new CandidateRosterImportCommitResponse(
            eval.SessionId,
            eval.ExamAttemptType,
            eval.Mode,
            toInsert.Count,
            totalCandidateCount,
            recommendedTotalRooms,
            nowUtc));
    }
}

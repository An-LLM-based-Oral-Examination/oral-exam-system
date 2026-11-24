using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetAvailableExamSetQuestions;

public sealed class GetAvailableExamSetQuestionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAvailableExamSetQuestionsQuery, Result<AvailableExamSetQuestionsResponseDto>>
{
    private const string YellowWarningText = "Đã có người chọn, đang chờ duyệt";

    private static readonly string[] AllowedRoles =
    [
        UserRole.Lecturer,
        UserRole.DepartmentHead
    ];

    public async Task<Result<AvailableExamSetQuestionsResponseDto>> Handle(
        GetAvailableExamSetQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<AvailableExamSetQuestionsResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<AvailableExamSetQuestionsResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null
            || !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase)
            || !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<AvailableExamSetQuestionsResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        var structure = await context.ExamStructures
            .AsNoTracking()
            .Include(s => s.Course)
                .ThenInclude(c => c.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == request.StructureId, cancellationToken);

        if (structure is null || structure.Course is null || !structure.Course.IsActive)
        {
            return Result<AvailableExamSetQuestionsResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy cấu trúc đề thi hoặc môn học đã ngừng hoạt động.");
        }

        var isLecturer = string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var canAccessCourse = isLecturer
            ? await OfficialExamAuthorization.CanLecturerAccessCourseAsync(
                context, structure.CourseId, actor.Id, cancellationToken)
            : AcademicCourseOwnership.IsOwnedBy(structure.Course, actor.Id);

        if (!canAccessCourse)
        {
            return Result<AvailableExamSetQuestionsResponseDto>.Failure(OfficialExamErrors.Forbidden);
        }

        // 1. Ẩn toàn bộ câu hỏi thuộc Đề mẫu đang hoạt động của môn học
        var sampleQuestionIds = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                esq.ExamSet.CourseId == structure.CourseId
                && esq.ExamSet.IsSampleSet
                && esq.ExamSet.IsActive
                && esq.ExamSet.Status != ExamSetStatus.Rejected)
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 2. Ẩn toàn bộ câu hỏi đã được duyệt trong bộ đề OFFICIAL / HANDED_OVER của cùng cấu trúc đề (cùng đợt thi)
        var approvedInStructureIds = await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                esq.ExamSet.StructureId == structure.Id
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet
                && (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver))
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 3. Với Giảng viên: ẩn câu hỏi chính GV đó đã chọn ở các bộ đề trước (khác REJECTED) trong cùng cấu trúc
        List<Guid> ownSelectedIds = [];
        if (isLecturer)
        {
            ownSelectedIds = await context.ExamSetQuestions
                .AsNoTracking()
                .Where(esq =>
                    esq.ExamSet.StructureId == structure.Id
                    && esq.ExamSet.IsActive
                    && !esq.ExamSet.IsSampleSet
                    && esq.ExamSet.CreatedBy == actor.Id
                    && esq.ExamSet.Status != ExamSetStatus.Rejected)
                .Select(esq => esq.ExamQuestionId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var hiddenIds = new HashSet<Guid>(sampleQuestionIds);
        hiddenIds.UnionWith(approvedInStructureIds);
        hiddenIds.UnionWith(ownSelectedIds);

        // 4. Nhãn vàng ẩn danh: câu đang nằm trong bộ đề SUBMITTED_FOR_REVIEW của GV khác trong cùng cấu trúc
        var yellowPendingIds = (await context.ExamSetQuestions
            .AsNoTracking()
            .Where(esq =>
                esq.ExamSet.StructureId == structure.Id
                && esq.ExamSet.IsActive
                && !esq.ExamSet.IsSampleSet
                && esq.ExamSet.Status == ExamSetStatus.SubmittedForReview
                && (!esq.ExamSet.CreatedBy.HasValue || esq.ExamSet.CreatedBy.Value != actor.Id))
            .Select(esq => esq.ExamQuestionId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var questionQuery = context.ExamQuestions
            .AsNoTracking()
            .Where(q =>
                q.CourseId == structure.CourseId
                && !q.IsDeleted
                && q.BankCurationStatus != BankCurationStatus.ExcludedFromBank
                && (q.BankCurationStatus == BankCurationStatus.BankReady
                    || q.BankCurationStatus == BankCurationStatus.CuratedIntoBank
                    || (q.BankCurationStatus == BankCurationStatus.PendingCuration && q.SubmittedBy == actor.Id))
                && ((q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive)
                    || (q.SubmittedBy == actor.Id
                        && (q.ApprovalStatus == ApprovalStatus.Draft || q.ApprovalStatus == ApprovalStatus.SubmittedForReview))));

        if (request.CloId.HasValue && request.CloId.Value != Guid.Empty)
        {
            questionQuery = questionQuery.Where(q => q.CloId == request.CloId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            var normalizedDifficulty = request.Difficulty.Trim().ToLowerInvariant();
            questionQuery = questionQuery.Where(q => q.Difficulty.ToLower() == normalizedDifficulty);
        }

        if (!string.IsNullOrWhiteSpace(request.BloomLevel))
        {
            var normalizedBloom = request.BloomLevel.Trim().ToLowerInvariant();
            questionQuery = questionQuery.Where(q => q.BloomLevel.ToLower() == normalizedBloom);
        }

        var rawQuestions = await questionQuery
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.Title,
                q.Content,
                q.Difficulty,
                q.BloomLevel,
                q.CloId,
                CloCode = q.Clo != null ? q.Clo.CloCode : null,
                q.RubricId,
                q.Source,
                q.ApprovalStatus,
                q.BankCurationStatus,
                q.SubmittedBy,
                q.ImageUrl,
                q.ImageDescription,
                q.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        var items = new List<AvailableExamSetQuestionDto>(rawQuestions.Count);
        foreach (var q in rawQuestions)
        {
            if (hiddenIds.Contains(q.Id))
            {
                continue;
            }

            if (search is not null
                && !q.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !q.Content.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isYellow = yellowPendingIds.Contains(q.Id);
            items.Add(new AvailableExamSetQuestionDto
            {
                QuestionId = q.Id,
                Title = q.Title,
                Content = q.Content,
                Difficulty = q.Difficulty,
                BloomLevel = q.BloomLevel,
                CloId = q.CloId,
                CloCode = q.CloCode,
                RubricId = q.RubricId,
                Source = q.Source,
                ApprovalStatus = q.ApprovalStatus,
                BankCurationStatus = q.BankCurationStatus,
                IsOwnQuestion = q.SubmittedBy == actor.Id,
                IsYellowPendingByOtherLecturer = isYellow,
                YellowWarningLabel = isYellow ? YellowWarningText : null,
                ImageUrl = q.ImageUrl,
                ImageDescription = q.ImageDescription,
                CreatedAt = q.CreatedAt
            });
        }

        return Result<AvailableExamSetQuestionsResponseDto>.Success(new AvailableExamSetQuestionsResponseDto
        {
            StructureId = structure.Id,
            CourseId = structure.CourseId,
            CourseCode = structure.Course.Code,
            RequiredSetCount = structure.RequiredSetCount,
            YellowThresholdPercent = 70,
            TotalAvailable = items.Count,
            YellowCount = items.Count(i => i.IsYellowPendingByOtherLecturer),
            Items = items
        });
    }
}

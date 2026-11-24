using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Commands.UpdateRubric;

public sealed class UpdateRubricCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateRubricCommand, Result<CreateRubricResponseDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<CreateRubricResponseDto>> Handle(
        UpdateRubricCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Bạn không có quyền cập nhật Barem Rubric.");
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            (!string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Chỉ Giảng viên hoặc Trưởng bộ môn đang hoạt động mới được cập nhật Barem Rubric.");
        }

        var rubric = await _context.Rubrics
            .Include(r => r.Criteria)
            .FirstOrDefaultAsync(r => r.Id == request.RubricId && r.IsActive, cancellationToken);

        if (rubric is null)
        {
            return Result<CreateRubricResponseDto>.Failure("NOT_FOUND: Không tìm thấy Barem Rubric đang hoạt động.");
        }

        var canAccessCourse = await QuestionCourseAccess
            .AllowedCourseIds(_context, actor.Role, actor.Id)
            .AnyAsync(id => id == rubric.CourseId, cancellationToken);

        if (!canAccessCourse)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "FORBIDDEN: Bạn không phụ trách hoặc không được phân công giảng dạy môn học này.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "VALIDATION: Tên Barem Rubric không được để trống và tối đa 200 ký tự.");
        }

        if (request.Criteria is null || request.Criteria.Count < 2)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Barem Rubric phải có tối thiểu 2 tiêu chí đánh giá con.");
        }

        if (request.Criteria.Any(c => string.IsNullOrWhiteSpace(c.CriterionName)))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "VALIDATION: Tên từng tiêu chí đánh giá con không được để trống.");
        }

        if (request.Criteria.Any(c => c.CriterionName.Trim().Length > 200))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "VALIDATION: Tên từng tiêu chí đánh giá con không được vượt quá 200 ký tự.");
        }

        if (request.Criteria.Any(c => c.MaxScore <= 0m))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Điểm tối đa của từng tiêu chí con phải lớn hơn 0.");
        }

        if (request.Criteria.Any(c => decimal.Round(c.MaxScore, 2) != c.MaxScore))
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Điểm tối đa của từng tiêu chí con chỉ chấp nhận tối đa 2 chữ số thập phân (numeric(4,2)).");
        }

        var canonicalCriterionBlooms = new string?[request.Criteria.Count];
        for (var i = 0; i < request.Criteria.Count; i++)
        {
            var rawBloom = request.Criteria[i].BloomLevel;
            if (string.IsNullOrWhiteSpace(rawBloom))
            {
                canonicalCriterionBlooms[i] = null;
                continue;
            }

            var trimmedBloom = rawBloom.Trim();
            var matchedBloom = BloomLevel.All.FirstOrDefault(b =>
                string.Equals(b, trimmedBloom, StringComparison.OrdinalIgnoreCase));
            if (matchedBloom is null || trimmedBloom.Length > 20)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    $"VALIDATION: Mức độ nhận thức Bloom '{trimmedBloom}' không hợp lệ. Chỉ chấp nhận: {string.Join(", ", BloomLevel.All)}.");
            }

            canonicalCriterionBlooms[i] = matchedBloom;
        }

        var totalMaxScore = request.Criteria.Sum(c => c.MaxScore);
        if (totalMaxScore != 10.00m)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "INVARIANT: Tổng điểm các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm.");
        }

        // 1. Chặn chỉnh sửa nếu rubric gắn với ExamQuestion nằm trong ExamSet OFFICIAL/HANDED_OVER hoặc đã có bài nộp thi thật
        var rubricExamQuestions = await _context.ExamQuestions
            .AsNoTracking()
            .Where(q => q.RubricId == rubric.Id)
            .Select(q => new { q.Id, q.IsDeleted, q.ApprovalStatus })
            .ToListAsync(cancellationToken);

        var activeExamQuestionIds = rubricExamQuestions
            .Where(q => !q.IsDeleted)
            .Select(q => q.Id)
            .ToList();
        if (activeExamQuestionIds.Count > 0)
        {
            var usedInOfficialOrHandedOverSet = await _context.ExamSetQuestions
                .AsNoTracking()
                .AnyAsync(esq =>
                    activeExamQuestionIds.Contains(esq.ExamQuestionId)
                    && (esq.ExamSet.Status == ExamSetStatus.Official || esq.ExamSet.Status == ExamSetStatus.HandedOver),
                    cancellationToken);

            if (usedInOfficialOrHandedOverSet)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    "CONFLICT: Không thể chỉnh sửa Barem Rubric vì câu hỏi tham chiếu đang nằm trong bộ đề chính thức (OFFICIAL) hoặc đã bàn giao (HANDED_OVER).");
            }
        }

        var allExamQuestionIds = rubricExamQuestions.Select(q => q.Id).ToList();
        if (allExamQuestionIds.Count > 0)
        {
            var hasOfficialExamSubmissions = await _context.ExamQuestionSubmissions
                .AsNoTracking()
                .AnyAsync(sub => allExamQuestionIds.Contains(sub.ExamQuestionId), cancellationToken);

            if (hasOfficialExamSubmissions)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    "CONFLICT: Không thể chỉnh sửa Barem Rubric vì đã phát sinh bài thi thật đã nộp hoặc đã khóa điểm sử dụng Barem này.");
            }
        }

        var existingCriteria = rubric.Criteria.ToList();
        var existingCriterionIds = existingCriteria.Select(c => c.Id).ToList();
        if (existingCriterionIds.Count > 0)
        {
            var hasEvaluationReferences = await _context.AiEvaluationDetails
                .AsNoTracking()
                .AnyAsync(d => existingCriterionIds.Contains(d.CriterionId), cancellationToken)
                || await _context.LecturerAuditDetails
                    .AsNoTracking()
                    .AnyAsync(d => existingCriterionIds.Contains(d.CriterionId), cancellationToken);

            if (hasEvaluationReferences)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    "CONFLICT: Không thể chỉnh sửa tiêu chí của Barem Rubric vì đã phát sinh dữ liệu chấm điểm hoặc hậu kiểm.");
            }
        }

        // 2. Chặn chỉnh sửa nếu rubric gắn với PracticeQuestion đã có bài làm MF-01 / MF-02 / MF-05
        var rubricPracticeQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Where(q => q.RubricId == rubric.Id)
            .Select(q => new { q.Id, q.IsDeleted, q.ApprovalStatus })
            .ToListAsync(cancellationToken);

        var allPracticeQuestionIds = rubricPracticeQuestions.Select(q => q.Id).ToList();
        if (allPracticeQuestionIds.Count > 0)
        {
            var hasPracticeSubmissions = await _context.PracticeAnswers
                    .AsNoTracking()
                    .AnyAsync(a => allPracticeQuestionIds.Contains(a.QuestionId), cancellationToken)
                || await _context.MockExamAnswers
                    .AsNoTracking()
                    .AnyAsync(a => allPracticeQuestionIds.Contains(a.QuestionId), cancellationToken)
                || await _context.InClassExamSubmissions
                    .AsNoTracking()
                    .AnyAsync(s => allPracticeQuestionIds.Contains(s.QuestionId), cancellationToken);

            if (hasPracticeSubmissions)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    "CONFLICT: Không thể chỉnh sửa Barem Rubric vì câu hỏi tham chiếu đã phát sinh bài làm luyện tập, thi thử hoặc kiểm tra trên lớp.");
            }
        }

        // 3. Chống GV lách quy trình TBM duyệt barem (Taboo #4): chặn GV sửa rubric đã gắn câu hỏi APPROVED chưa xóa
        if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            var hasApprovedQuestion = rubricExamQuestions.Any(q => !q.IsDeleted && q.ApprovalStatus == ApprovalStatus.Approved)
                || rubricPracticeQuestions.Any(q => !q.IsDeleted && q.ApprovalStatus == ApprovalStatus.Approved);
            if (hasApprovedQuestion)
            {
                return Result<CreateRubricResponseDto>.Failure(
                    "CONFLICT: Giảng viên không được chỉnh sửa Barem Rubric đang gắn với câu hỏi đã được Trưởng Bộ Môn phê duyệt (APPROVED).");
            }
        }

        var now = DateTime.UtcNow;
        var oldName = rubric.Name;

        if (existingCriteria.Count > 0)
        {
            _context.RubricCriteria.RemoveRange(existingCriteria);
            rubric.Criteria.Clear();
        }

        rubric.Name = request.Name.Trim();
        rubric.Description = request.Description;
        rubric.TotalMaxScore = 10.00m;

        var newCriteria = new List<RubricCriterion>(request.Criteria.Count);
        for (var idx = 0; idx < request.Criteria.Count; idx++)
        {
            var c = request.Criteria[idx];
            var normalizedWeight = c.Weight;
            if (normalizedWeight > 1.00m)
            {
                normalizedWeight = Math.Round(normalizedWeight / 100m, 2, MidpointRounding.AwayFromZero);
            }
            if (normalizedWeight <= 0m || normalizedWeight > 1.00m)
            {
                normalizedWeight = Math.Clamp(Math.Round(c.MaxScore / 10m, 2, MidpointRounding.AwayFromZero), 0.01m, 1.00m);
            }

            var criterion = new RubricCriterion
            {
                Id = Guid.NewGuid(),
                RubricId = rubric.Id,
                CriterionName = c.CriterionName.Trim(),
                Description = c.Description,
                MaxScore = c.MaxScore,
                Weight = normalizedWeight,
                BloomLevel = canonicalCriterionBlooms[idx],
                OrderIndex = c.OrderIndex > 0 ? c.OrderIndex : idx + 1,
                CreatedAt = now
            };

            _context.RubricCriteria.Add(criterion);
            newCriteria.Add(criterion);
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "UPDATE_RUBRIC",
            EntityName = "rubrics",
            EntityId = rubric.Id,
            OldValues = oldName,
            NewValues = rubric.Name,
            CreatedAt = now
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "CONFLICT: Barem Rubric đã bị thay đổi đồng thời bởi thao tác khác. Vui lòng tải lại dữ liệu và thử lại.");
        }
        catch (DbUpdateException)
        {
            return Result<CreateRubricResponseDto>.Failure(
                "CONFLICT: Không thể cập nhật Barem Rubric do xung đột ràng buộc tham chiếu dữ liệu trong CSDL.");
        }

        var criteriaDto = newCriteria
            .OrderBy(c => c.OrderIndex)
            .Select(c => new RubricCriterionDetailDto(
                c.Id,
                c.CriterionName,
                c.Description,
                c.MaxScore,
                c.Weight,
                c.BloomLevel,
                c.OrderIndex))
            .ToList();

        return Result<CreateRubricResponseDto>.Success(new CreateRubricResponseDto(
            rubric.Id,
            rubric.CourseId,
            rubric.Name,
            rubric.Description,
            rubric.TotalMaxScore,
            criteriaDto.Count,
            rubric.CreatedAt,
            criteriaDto));
    }
}

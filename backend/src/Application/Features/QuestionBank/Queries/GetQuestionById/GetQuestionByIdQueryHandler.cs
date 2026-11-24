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
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.Queries.GetQuestionById;

public sealed class GetQuestionByIdQueryHandler : IRequestHandler<GetQuestionByIdQuery, Result<QuestionDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetQuestionByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<QuestionDetailDto>> Handle(GetQuestionByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
            return Result<QuestionDetailDto>.Failure("FORBIDDEN: Thiếu định danh người tra cứu.");
        if (!string.Equals(request.ActorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.ActorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            return Result<QuestionDetailDto>.Failure("FORBIDDEN: Không có quyền tra cứu câu hỏi.");

        var isLecturer = string.Equals(request.ActorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase);
        var allowedCourseIds = QuestionCourseAccess.AllowedCourseIds(_context, request.ActorRole, request.ActorId);
        var question = await _context.ExamQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Clo)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Include(q => q.SubmittedByUser)
            .Include(q => q.ApprovedByUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == request.Id && !q.IsDeleted && allowedCourseIds.Contains(q.CourseId), cancellationToken);

        var practiceQuestion = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Clo)
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Include(q => q.SubmittedByUser)
            .Include(q => q.ApprovedByUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == request.Id && !q.IsDeleted && allowedCourseIds.Contains(q.CourseId), cancellationToken);

        if (question != null)
        {
            var isOwnQuestion = question.SubmittedBy == request.ActorId;
            if (isLecturer && !isOwnQuestion)
            {
                var isBankVisible =
                    question.ApprovalStatus == ApprovalStatus.Approved &&
                    question.IsActive &&
                    (question.BankCurationStatus == BankCurationStatus.BankReady ||
                     question.BankCurationStatus == BankCurationStatus.CuratedIntoBank);

                if (!isBankVisible)
                {
                    return Result<QuestionDetailDto>.Failure(
                        "FORBIDDEN: Bạn không có quyền xem câu hỏi riêng tư hoặc chưa vào ngân hàng của giảng viên khác.");
                }

                var isInActiveSampleSet = await _context.ExamSetQuestions
                    .AsNoTracking()
                    .AnyAsync(esq =>
                        esq.ExamQuestionId == question.Id &&
                        esq.ExamSet.IsSampleSet &&
                        esq.ExamSet.IsActive &&
                        esq.ExamSet.Status != ExamSetStatus.Rejected,
                        cancellationToken);

                if (isInActiveSampleSet)
                {
                    return Result<QuestionDetailDto>.Failure(
                        "FORBIDDEN: Không được phép xem câu hỏi thuộc bộ đề mẫu (Sample Set) đang hoạt động.");
                }
            }

            return Result<QuestionDetailDto>.Success(
                MapExamQuestionToDetailDto(question, "exam", maskAuthor: isLecturer && !isOwnQuestion));
        }

        if (practiceQuestion != null)
        {
            var isOwnQuestion = practiceQuestion.SubmittedBy == request.ActorId;
            if (isLecturer && !isOwnQuestion &&
                !(practiceQuestion.ApprovalStatus == ApprovalStatus.Approved && practiceQuestion.IsActive))
            {
                return Result<QuestionDetailDto>.Failure(
                    "FORBIDDEN: Bạn không có quyền xem câu hỏi luyện tập chưa được phê duyệt của giảng viên khác.");
            }

            return Result<QuestionDetailDto>.Success(
                MapPracticeQuestionToDetailDto(practiceQuestion, "practice", maskAuthor: isLecturer && !isOwnQuestion));
        }

        return Result<QuestionDetailDto>.Failure("Không tìm thấy câu hỏi với mã tương ứng.");
    }

    private static QuestionDetailDto MapExamQuestionToDetailDto(ExamQuestion question, string usageScope, bool maskAuthor)
    {
        var keyPoints = ParseKeyPoints(question.KeyPoints);
        var rubricDto = MapRubricDto(question.Rubric);

        return new QuestionDetailDto(
            question.Id,
            question.CourseId,
            question.Course.Code,
            question.RubricId,
            question.Title,
            question.Content,
            question.SampleAnswer,
            keyPoints,
            question.Difficulty,
            question.BloomLevel,
            question.Source,
            question.ApprovalStatus,
            maskAuthor ? null : question.SubmittedBy,
            maskAuthor ? null : question.SubmittedByUser?.FullName,
            question.ApprovedBy,
            question.ApprovedByUser?.FullName,
            question.ReviewNotes,
            question.IsActive,
            question.CreatedAt,
            rubricDto,
            question.ImageUrl,
            question.ImageDescription,
            question.CloId,
            question.Clo?.CloCode,
            question.StructureId,
            usageScope,
            question.ApprovedAt
        );
    }

    private static QuestionDetailDto MapPracticeQuestionToDetailDto(PracticeQuestion question, string usageScope, bool maskAuthor)
    {
        var keyPoints = ParseKeyPoints(question.KeyPoints);
        var rubricDto = MapRubricDto(question.Rubric);

        return new QuestionDetailDto(
            question.Id,
            question.CourseId,
            question.Course.Code,
            question.RubricId,
            question.Title,
            question.Content,
            question.SampleAnswer,
            keyPoints,
            question.Difficulty,
            question.BloomLevel,
            question.Source,
            question.ApprovalStatus,
            maskAuthor ? null : question.SubmittedBy,
            maskAuthor ? null : question.SubmittedByUser?.FullName,
            question.ApprovedBy,
            question.ApprovedByUser?.FullName,
            question.ReviewNotes,
            question.IsActive,
            question.CreatedAt,
            rubricDto,
            question.ImageUrl,
            question.ImageDescription,
            question.CloId,
            question.Clo?.CloCode,
            null,
            usageScope,
            question.ApprovedAt
        );
    }

    private static List<string> ParseKeyPoints(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static RubricDetailDto MapRubricDto(Rubric? rubric)
    {
        var criteriaDto = rubric?.Criteria
            .OrderBy(c => c.OrderIndex)
            .Select(c => new RubricCriterionDetailDto(
                c.Id,
                c.CriterionName,
                c.Description,
                c.MaxScore,
                c.Weight,
                c.BloomLevel,
                c.OrderIndex
            ))
            .ToList() ?? new List<RubricCriterionDetailDto>();

        return new RubricDetailDto(
            rubric?.Id ?? Guid.Empty,
            rubric?.Name ?? string.Empty,
            rubric?.Description,
            rubric?.TotalMaxScore ?? 10.00m,
            criteriaDto
        );
    }
}

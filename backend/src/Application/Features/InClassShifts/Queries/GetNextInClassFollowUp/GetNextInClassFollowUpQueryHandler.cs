using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts.Queries.GetNextInClassFollowUp;

public sealed class GetNextInClassFollowUpQueryHandler(
    IApplicationDbContext context,
    IAiGradingService? aiGradingService = null)
    : IRequestHandler<GetNextInClassFollowUpQuery, Result<InClassNextFollowUpResponse>>
{
    public async Task<Result<InClassNextFollowUpResponse>> Handle(
        GetNextInClassFollowUpQuery request,
        CancellationToken cancellationToken)
    {
        var submission = await context.InClassExamSubmissions
            .Include(s => s.Shift)
                .ThenInclude(sh => sh.Class)
            .Include(s => s.ParentSubmission)
            .FirstOrDefaultAsync(
                s => s.Id == request.SubmissionId && s.ShiftId == request.ShiftId && s.StudentId == request.StudentUserId,
                cancellationToken);

        if (submission?.Shift is null)
        {
            return Result<InClassNextFollowUpResponse>.Failure(InClassShiftErrors.SubmissionNotFound);
        }

        var shift = submission.Shift;
        if (!shift.HasFollowUp || shift.MaxFollowUpQuestions <= 0 || submission.FollowUpIndex >= shift.MaxFollowUpQuestions)
        {
            return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
                HasMoreFollowUp: false,
                FollowUpIndex: submission.FollowUpIndex,
                MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
                ParentSubmissionId: submission.Id,
                QuestionId: submission.QuestionId,
                FollowUpPrompt: null));
        }

        int nextFollowUpIndex = submission.FollowUpIndex + 1;

        if (string.IsNullOrWhiteSpace(submission.FollowUpPrompt))
        {
            var question = await context.PracticeQuestions
                .AsNoTracking()
                .Include(q => q.Course)
                .Include(q => q.Rubric)
                    .ThenInclude(r => r!.Criteria)
                .FirstOrDefaultAsync(q => q.Id == submission.QuestionId, cancellationToken);

            var priorSubmissions = await context.InClassExamSubmissions
                .AsNoTracking()
                .Where(s => s.ShiftId == request.ShiftId && s.StudentId == request.StudentUserId && s.QuestionId == submission.QuestionId)
                .OrderBy(s => s.FollowUpIndex)
                .ToListAsync(cancellationToken);

            var previousFollowUpPrompts = priorSubmissions
                .Select(s => s.FollowUpPrompt)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orderedCriteria = question?.Rubric?.Criteria?
                .OrderBy(c => c.OrderIndex)
                .ToList() ?? new List<RubricCriterion>();

            string? aiFollowUpPrompt = null;

            if (aiGradingService is not null && question is not null && orderedCriteria.Count > 0)
            {
                try
                {
                    var criteriaContext = orderedCriteria
                        .Select(c => new AiGradingCriterion
                        {
                            Id = c.Id.ToString(),
                            Name = c.CriterionName,
                            Description = c.Description ?? string.Empty,
                            MaxScore = c.MaxScore,
                            Weight = c.Weight
                        })
                        .ToList();

                    string? answeredFollowUpPrompt = submission.IsFollowUp
                        ? (!string.IsNullOrWhiteSpace(submission.ParentSubmission?.FollowUpPrompt)
                            ? submission.ParentSubmission!.FollowUpPrompt!.Trim()
                            : previousFollowUpPrompts.LastOrDefault())
                        : null;

                    var aiRequest = new AiGradingRequest
                    {
                        QuestionContent = question.Content,
                        SampleAnswer = question.SampleAnswer ?? string.Empty,
                        CourseCode = question.Course?.Code,
                        Transcript = submission.Transcript,
                        Criteria = criteriaContext,
                        QuestionImageUrl = question.ImageUrl,
                        QuestionImageDescription = question.ImageDescription,
                        IsFollowUp = submission.IsFollowUp,
                        FollowUpPrompt = answeredFollowUpPrompt,
                        FollowUpIndex = submission.FollowUpIndex,
                        OriginalAnswerTranscript = priorSubmissions.FirstOrDefault(s => !s.IsFollowUp)?.Transcript ?? submission.Transcript,
                        PreviousFollowUpPrompts = previousFollowUpPrompts,
                        ThrowOnTransientFailure = false
                    };

                    var gradingResult = await aiGradingService.GradeAnswerAsync(aiRequest, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(gradingResult.FollowUpPrompt))
                    {
                        aiFollowUpPrompt = gradingResult.FollowUpPrompt.Trim();
                    }
                }
                catch
                {
                    // Fallback về bộ xây dựng câu hỏi theo ngữ cảnh rubric bên dưới nếu kết nối LLM bị gián đoạn tạm thời
                }
            }

            if (string.IsNullOrWhiteSpace(aiFollowUpPrompt))
            {
                var targetCriterion = orderedCriteria.Count > 0
                    ? orderedCriteria[(nextFollowUpIndex - 1) % orderedCriteria.Count]
                    : null;

                aiFollowUpPrompt = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                    question?.Course?.Code,
                    question?.Course?.Name,
                    null,
                    nextFollowUpIndex,
                    targetCriterion?.CriterionName,
                    targetCriterion?.Description,
                    orderedCriteria.Select(c => c.CriterionName),
                    question?.Content,
                    question?.Title,
                    submission.Transcript,
                    priorSubmissions.FirstOrDefault(s => !s.IsFollowUp)?.Transcript ?? submission.Transcript,
                    previousFollowUpPrompts);
            }

            submission.FollowUpPrompt = !string.IsNullOrWhiteSpace(aiFollowUpPrompt)
                ? aiFollowUpPrompt
                : $"Câu hỏi phụ #{nextFollowUpIndex}: Hãy phân tích sâu hơn tình huống thực tế và cách xử lý ngoại lệ của kiến thức vừa trình bày?";

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result<InClassNextFollowUpResponse>.Success(new InClassNextFollowUpResponse(
            HasMoreFollowUp: true,
            FollowUpIndex: nextFollowUpIndex,
            MaxFollowUpQuestions: shift.MaxFollowUpQuestions,
            ParentSubmissionId: submission.Id,
            QuestionId: submission.QuestionId,
            FollowUpPrompt: submission.FollowUpPrompt));
    }
}

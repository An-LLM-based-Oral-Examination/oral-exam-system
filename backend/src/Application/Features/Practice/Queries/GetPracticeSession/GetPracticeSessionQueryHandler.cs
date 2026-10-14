using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Features.Practice.Queries.GetPracticeSession;

public sealed class GetPracticeSessionQueryHandler : IRequestHandler<GetPracticeSessionQuery, Result<PracticeSessionDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPracticeSessionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PracticeSessionDetailDto>> Handle(GetPracticeSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.PracticeSessions
            .AsNoTracking()
            .Include(s => s.Course)
            .Include(s => s.Student)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
                        .ThenInclude(d => d.Criterion)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<PracticeSessionDetailDto>.Failure("Phiên luyện tập không tồn tại.");
        }

        // Lấy danh sách câu hỏi thuộc Course
        var courseQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == session.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        var questions = courseQuestions
            .Select(q => new PracticeQuestionDto(
                q.Id,
                q.Content,
                q.Rubric != null ? q.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList() : new System.Collections.Generic.List<string>()
            ))
            .ToList();

        var questionFollowUpMap = courseQuestions.ToDictionary(q => q.Id, q => q.FollowUpPrompt);

        var maxFollowUpConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
        int maxAllowedFollowUps = (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMaxFollowUps))
            ? parsedMaxFollowUps
            : 2;

        var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);

        var answerDtos = session.Answers
            .OrderBy(a => a.SubmittedAt)
            .Select(a =>
            {
                var evaluation = a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                bool isBorderline = !isFullSession &&
                                    evaluation != null &&
                                    evaluation.TotalScore >= 4.0m &&
                                    evaluation.TotalScore <= 8.0m;

                int followUpsCountForQuestion = session.Answers.Count(x => x.QuestionId == a.QuestionId && x.IsFollowUp);
                bool hasChildFollowUp = session.Answers.Any(x => x.ParentAnswerId == a.Id);
                bool needsFollowUp = isBorderline && !hasChildFollowUp && followUpsCountForQuestion < maxAllowedFollowUps;

                string? followUpPrompt = null;
                if (needsFollowUp)
                {
                    questionFollowUpMap.TryGetValue(a.QuestionId, out var rawPrompt);
                    followUpPrompt = !string.IsNullOrWhiteSpace(rawPrompt)
                        ? rawPrompt
                        : "Hãy làm rõ hơn luận điểm kỹ thuật và ví dụ thực tế liên quan đến câu trả lời của bạn.";
                }

                return new PracticeAnswerDetailDto
                {
                    AnswerId = a.Id,
                    QuestionId = a.QuestionId,
                    AnswerText = a.AnswerText,
                    IsFollowUp = a.IsFollowUp,
                    ParentAnswerId = a.ParentAnswerId,
                    Status = a.Status,
                    SubmittedAt = a.SubmittedAt,
                    TotalScore = evaluation?.TotalScore,
                    Feedback = evaluation?.Feedback,
                    ConfidenceScore = evaluation?.ConfidenceScore,
                    IsSuspicious = evaluation != null ? evaluation.ConfidenceScore < 0.70m : null,
                    NeedsFollowUp = needsFollowUp,
                    FollowUpPrompt = followUpPrompt,
                    CriteriaScores = evaluation?.Details.Select(d => new PracticeEvaluationDetailDto
                    {
                        CriterionId = d.CriterionId,
                        CriterionName = d.Criterion != null ? d.Criterion.CriterionName : string.Empty,
                        Score = d.Score,
                        Comment = d.Comment
                    }).ToList() ?? new()
                };
            }).ToList();

        var result = new PracticeSessionDetailDto
        {
            SessionId = session.Id,
            CourseId = session.CourseId,
            CourseCode = session.Course.Code,
            CourseName = session.Course.Name,
            StudentId = session.StudentId,
            StudentName = session.Student != null ? session.Student.FullName : string.Empty,
            PracticeMode = session.PracticeMode,
            Status = session.Status,
            TranscriptBufferSeconds = session.Course.TranscriptBufferSeconds,
            StartedAt = session.StartedAt,
            EndedAt = session.CompletedAt,
            Questions = questions,
            Answers = answerDtos
        };

        return Result<PracticeSessionDetailDto>.Success(result);
    }
}

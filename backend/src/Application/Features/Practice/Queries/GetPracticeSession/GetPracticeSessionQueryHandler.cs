using System;
using System.Collections.Generic;
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

        if (!request.IsElevatedRole && request.RequestingUserId.HasValue && request.RequestingUserId.Value != Guid.Empty && session.StudentId != request.RequestingUserId.Value)
        {
            return Result<PracticeSessionDetailDto>.Failure("Bạn không có quyền truy cập phiên luyện tập này.");
        }

        // Lấy danh sách câu hỏi thuộc Course để tra cứu Rubric, Criteria và FollowUpPrompt
        var courseQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == session.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);

        var sessionAnswers = session.Answers ?? Array.Empty<Domain.Entities.PracticeAnswer>();

        List<PracticeQuestionDto> questions;
        if (sessionAnswers.Any())
        {
            var answeredQuestionIds = sessionAnswers
                .OrderBy(a => a.SubmittedAt)
                .Select(a => a.QuestionId)
                .Distinct()
                .ToList();

            var questionById = courseQuestions.ToDictionary(q => q.Id);
            questions = answeredQuestionIds
                .Where(id => questionById.ContainsKey(id))
                .Select(id =>
                {
                    var q = questionById[id];
                    return new PracticeQuestionDto(
                        q.Id,
                        q.Content,
                        q.Rubric != null
                            ? q.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList()
                            : new List<string>(),
                        q.ImageUrl,
                        q.ImageDescription
                    );
                })
                .ToList();
        }
        else
        {
            IEnumerable<Domain.Entities.PracticeQuestion> filteredQuestions = courseQuestions;
            var selectedDiffs = session.DifficultiesList
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedDiffs.Count > 0)
            {
                filteredQuestions = filteredQuestions.Where(q =>
                    !string.IsNullOrWhiteSpace(q.Difficulty) &&
                    selectedDiffs.Contains(q.Difficulty.Trim().ToLowerInvariant()));
            }

            if (!isFullSession)
            {
                filteredQuestions = filteredQuestions.Take(1);
            }

            questions = filteredQuestions
                .Select(q => new PracticeQuestionDto(
                    q.Id,
                    q.Content,
                    q.Rubric != null
                        ? q.Rubric.Criteria.Select(c => c.Description ?? string.Empty).ToList()
                        : new List<string>(),
                    q.ImageUrl,
                    q.ImageDescription
                ))
                .ToList();
        }

        var questionFollowUpMap = courseQuestions.ToDictionary(q => q.Id, q => q.FollowUpPrompt);

        var maxFollowUpConfig = await _context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
        int maxAllowedFollowUps = (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMaxFollowUps))
            ? parsedMaxFollowUps
            : 2;

        var answerDtos = sessionAnswers
            .OrderBy(a => a.SubmittedAt)
            .Select(a =>
            {
                var evaluation = a.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                bool isBorderline = !isFullSession &&
                                    evaluation != null &&
                                    evaluation.TotalScore >= 4.0m &&
                                    evaluation.TotalScore <= 8.0m;

                int followUpsCountForQuestion = sessionAnswers.Count(x => x.QuestionId == a.QuestionId && x.IsFollowUp);
                bool hasChildFollowUp = sessionAnswers.Any(x => x.ParentAnswerId == a.Id);
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
                    IsSuspicious = evaluation != null
                        ? (evaluation.ConfidenceScore < 0.70m ||
                           evaluation.TotalScore == 0.0m ||
                           (!string.IsNullOrWhiteSpace(evaluation.CotTrace) &&
                            evaluation.CotTrace.Contains("[NON_ANSWER_GUARD]", StringComparison.OrdinalIgnoreCase)))
                        : null,
                    NeedsFollowUp = needsFollowUp,
                    FollowUpPrompt = followUpPrompt,
                    CriteriaScores = evaluation?.Details
                        .OrderBy(d => d.Criterion != null ? d.Criterion.OrderIndex : 0)
                        .Select(d => new PracticeEvaluationDetailDto
                        {
                            CriterionId = d.CriterionId,
                            CriterionName = d.Criterion != null ? d.Criterion.CriterionName : string.Empty,
                            Score = d.Score,
                            Comment = d.Comment
                        }).ToList() ?? new()
                };
            }).ToList();

        var sysBufferStr = await _context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "TranscriptBufferSeconds")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var bufferSeconds = int.TryParse(sysBufferStr, out var parsedBuffer)
            ? Math.Clamp(parsedBuffer, 10, 300)
            : (session.Course != null ? Math.Clamp(session.Course.TranscriptBufferSeconds, 10, 300) : 60);

        var result = new PracticeSessionDetailDto
        {
            SessionId = session.Id,
            CourseId = session.CourseId,
            CourseCode = session.Course != null ? session.Course.Code : string.Empty,
            CourseName = session.Course != null ? session.Course.Name : string.Empty,
            StudentId = session.StudentId,
            StudentName = session.Student != null ? session.Student.FullName : string.Empty,
            PracticeMode = session.PracticeMode,
            Status = session.Status,
            TranscriptBufferSeconds = bufferSeconds,
            StartedAt = session.StartedAt,
            EndedAt = session.CompletedAt,
            Questions = questions,
            Answers = answerDtos
        };

        return Result<PracticeSessionDetailDto>.Success(result);
    }
}

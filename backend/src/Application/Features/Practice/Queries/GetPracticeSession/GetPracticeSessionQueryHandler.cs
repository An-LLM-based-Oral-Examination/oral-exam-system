using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
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

        if (string.Equals(session.Status, "in_progress", StringComparison.OrdinalIgnoreCase))
        {
            int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(_context, cancellationToken);
            await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(_context, session, timeoutMinutes, cancellationToken);
        }

        // Lấy danh sách câu hỏi thuộc Course để tra cứu Rubric, Criteria và chủ đề câu hỏi
        var courseQuestions = await _context.PracticeQuestions
            .AsNoTracking()
            .Include(q => q.Rubric)
                .ThenInclude(r => r.Criteria)
            .Where(q => q.CourseId == session.CourseId && q.IsActive)
            .ToListAsync(cancellationToken);

        var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);

        var sessionAnswers = session.Answers ?? Array.Empty<Domain.Entities.PracticeAnswer>();

        var orderedQuestionIds = new List<Guid>(session.SelectedQuestionIds);
        foreach (var answeredQid in sessionAnswers.OrderBy(a => a.SubmittedAt).Select(a => a.QuestionId))
        {
            if (answeredQid != Guid.Empty && !orderedQuestionIds.Contains(answeredQid))
            {
                orderedQuestionIds.Add(answeredQid);
            }
        }

        var questionById = courseQuestions.ToDictionary(q => q.Id);

        List<PracticeQuestionDto> questions;
        if (orderedQuestionIds.Count > 0)
        {
            questions = orderedQuestionIds
                .Where(id => questionById.ContainsKey(id))
                .Select(id =>
                {
                    var q = questionById[id];
                    return new PracticeQuestionDto(
                        q.Id,
                        q.Content,
                        q.Rubric != null
                            ? q.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList()
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
                        ? q.Rubric.Criteria.OrderBy(c => c.OrderIndex).Select(c => c.Description ?? string.Empty).ToList()
                        : new List<string>(),
                    q.ImageUrl,
                    q.ImageDescription
                ))
                .ToList();
        }

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

                int followUpsCountForQuestion = sessionAnswers.Count(x => x.QuestionId == a.QuestionId && x.IsFollowUp && x.SubmittedAt <= a.SubmittedAt);
                bool hasChildFollowUp = sessionAnswers.Any(x =>
                    x.Id != a.Id &&
                    (x.ParentAnswerId == a.Id ||
                     (x.QuestionId == a.QuestionId && x.SubmittedAt > a.SubmittedAt)));
                bool needsFollowUp = isBorderline && !hasChildFollowUp && followUpsCountForQuestion < maxAllowedFollowUps;

                string? followUpPrompt = null;
                if (needsFollowUp)
                {
                    try
                    {
                        var persistedFollowUp = FollowUpPromptBuilder.TryExtractFollowUpFromCot(evaluation?.CotTrace);
                        if (!string.IsNullOrWhiteSpace(persistedFollowUp))
                        {
                            followUpPrompt = persistedFollowUp;
                        }
                        else
                        {
                            questionById.TryGetValue(a.QuestionId, out var qEntity);
                            var mainAnsText = sessionAnswers
                                .Where(x => x.QuestionId == a.QuestionId && !x.IsFollowUp)
                                .OrderBy(x => x.SubmittedAt)
                                .Select(x => x.AnswerText)
                                .FirstOrDefault();
                            var prevPrompts = sessionAnswers
                                .Where(x => x.QuestionId == a.QuestionId && x.SubmittedAt < a.SubmittedAt)
                                .SelectMany(x => x.AiEvaluations.OrderByDescending(e => e.EvaluatedAt).Take(1))
                                .Select(e => FollowUpPromptBuilder.TryExtractFollowUpFromCot(e.CotTrace))
                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                .Select(p => p!.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            var weakestDetail = evaluation?.Details
                                .OrderBy(d => d.Criterion != null && d.Criterion.MaxScore > 0m ? d.Score / d.Criterion.MaxScore : d.Score)
                                .FirstOrDefault();
                            int nextFollowUpSequence = followUpsCountForQuestion + 1;
                            var candidatePrompt = FollowUpPromptBuilder.ResolveFollowUpPrompt(
                                session.Course?.Code,
                                null,
                                null,
                                nextFollowUpSequence,
                                weakestDetail?.Criterion?.CriterionName,
                                weakestDetail?.Criterion?.Description,
                                evaluation?.Details?.Select(d => d.Criterion?.CriterionName ?? string.Empty),
                                qEntity?.Content,
                                null,
                                a.AnswerText,
                                mainAnsText,
                                prevPrompts);

                            if (!string.IsNullOrWhiteSpace(candidatePrompt))
                            {
                                followUpPrompt = candidatePrompt;
                            }
                            else
                            {
                                needsFollowUp = false;
                                followUpPrompt = null;
                            }
                        }
                    }
                    catch
                    {
                        needsFollowUp = false;
                        followUpPrompt = null;
                    }
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
                        .Select(d =>
                        {
                            var critName = d.Criterion?.CriterionName;
                            var critMaxScore = d.Criterion?.MaxScore ?? 0m;
                            if ((string.IsNullOrWhiteSpace(critName) || critMaxScore <= 0m) &&
                                questionById.TryGetValue(a.QuestionId, out var qRef))
                            {
                                var matchedCrit = qRef.Rubric?.Criteria.FirstOrDefault(c => c.Id == d.CriterionId);
                                if (string.IsNullOrWhiteSpace(critName))
                                {
                                    critName = matchedCrit?.CriterionName;
                                }

                                if (critMaxScore <= 0m && matchedCrit != null)
                                {
                                    critMaxScore = matchedCrit.MaxScore;
                                }
                            }

                            return new PracticeEvaluationDetailDto
                            {
                                CriterionId = d.CriterionId,
                                CriterionName = critName ?? string.Empty,
                                Score = d.Score,
                                MaxScore = critMaxScore > 0m ? critMaxScore : 10.0m,
                                Comment = d.Comment
                            };
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
            : (session.Course != null && session.Course.TranscriptBufferSeconds > 0
                ? Math.Clamp(session.Course.TranscriptBufferSeconds, 10, 300)
                : 60);

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

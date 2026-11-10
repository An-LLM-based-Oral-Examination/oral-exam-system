using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Common;
using OralExamination.Application.Features.Practice.DTOs;

namespace OralExamination.Application.Features.Practice.Queries.GetPracticeSession;

public sealed class GetPracticeSessionQueryHandler(
    IApplicationDbContext context,
    ILogger<GetPracticeSessionQueryHandler>? logger = null)
    : IRequestHandler<GetPracticeSessionQuery, Result<PracticeSessionDetailDto>>
{
    public async Task<Result<PracticeSessionDetailDto>> Handle(GetPracticeSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await context.PracticeSessions
            .Include(s => s.Course)
            .Include(s => s.Student)
            .Include(s => s.Answers)
                .ThenInclude(a => a.AiEvaluations)
                    .ThenInclude(e => e.Details)
                        .ThenInclude(d => d.Criterion)
            .AsSplitQuery()
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
            try
            {
                int timeoutMinutes = await PracticeSessionTimeoutHelper.GetTimeoutMinutesAsync(context, cancellationToken);
                await PracticeSessionTimeoutHelper.TryAutoCloseIfTimedOutAsync(context, session, timeoutMinutes, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Lỗi xảy ra trong quá trình tự động đóng phiên luyện tập hết hạn {SessionId}. Tiếp tục trả về thông tin phiên.", session.Id);
            }
        }

        var isFullSession = string.Equals(session.PracticeMode, "full_session", StringComparison.OrdinalIgnoreCase);

        var sessionAnswers = session.Answers ?? Array.Empty<Domain.Entities.PracticeAnswer>();

        var orderedQuestionIds = new List<Guid>(session.SelectedQuestionIds ?? new List<Guid>());
        foreach (var answeredQid in sessionAnswers
                     .OrderBy(a => a.OrderIndex > 0 ? a.OrderIndex : int.MaxValue)
                     .ThenBy(a => a.SubmittedAt)
                     .Select(a => a.QuestionId))
        {
            if (answeredQid != Guid.Empty && !orderedQuestionIds.Contains(answeredQid))
            {
                orderedQuestionIds.Add(answeredQid);
            }
        }

        // Lấy danh sách câu hỏi thuộc phiên (nếu đã có danh sách ID) hoặc fallback theo Course để tra cứu Rubric, Criteria và chủ đề câu hỏi
        var courseQuestions = orderedQuestionIds.Count > 0
            ? await context.PracticeQuestions
                .AsNoTracking()
                .Include(q => q.Rubric)
                    .ThenInclude(r => r.Criteria)
                .Where(q => orderedQuestionIds.Contains(q.Id))
                .ToListAsync(cancellationToken)
            : await context.PracticeQuestions
                .AsNoTracking()
                .Include(q => q.Rubric)
                    .ThenInclude(r => r.Criteria)
                .Where(q => q.CourseId == session.CourseId && q.IsActive)
                .ToListAsync(cancellationToken);

        var questionById = courseQuestions?.ToDictionary(q => q.Id) ?? new Dictionary<Guid, Domain.Entities.PracticeQuestion>();

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
            IEnumerable<Domain.Entities.PracticeQuestion> filteredQuestions = courseQuestions ?? new List<Domain.Entities.PracticeQuestion>();
            var selectedDiffs = (session.DifficultiesList ?? Enumerable.Empty<string>())
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

        var maxFollowUpConfig = await context.SystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "MaxPracticeFollowUpQuestions", cancellationToken);
        int maxAllowedFollowUps = (maxFollowUpConfig != null && int.TryParse(maxFollowUpConfig.Value, out var parsedMaxFollowUps))
            ? parsedMaxFollowUps
            : 2;

        var answerDtos = sessionAnswers
            .OrderBy(a => a.OrderIndex > 0 ? a.OrderIndex : int.MaxValue)
            .ThenBy(a => a.IsFollowUp ? 1 : 0)
            .ThenBy(a => a.SubmittedAt)
            .Select(a =>
            {
                var evaluation = (a.AiEvaluations ?? Enumerable.Empty<Domain.Entities.AiEvaluation>())
                    .OrderByDescending(e => e.EvaluatedAt)
                    .FirstOrDefault();
                bool isBorderline = !isFullSession &&
                                    evaluation != null &&
                                    evaluation.TotalScore >= 4.0m &&
                                    evaluation.TotalScore <= 8.0m;

                int followUpsCountForQuestion = sessionAnswers.Count(x => x.QuestionId == a.QuestionId && x.IsFollowUp && x.SubmittedAt <= a.SubmittedAt);
                bool hasChildFollowUp = sessionAnswers.Any(x =>
                    x.Id != a.Id &&
                    (x.ParentAnswerId == a.Id ||
                     (x.QuestionId == a.QuestionId && x.SubmittedAt > a.SubmittedAt)));
                bool hasProgressedToLaterQuestion = !isFullSession &&
                    (sessionAnswers.Any(x => x.QuestionId != a.QuestionId && x.SubmittedAt > a.SubmittedAt) ||
                     (session.SelectedQuestionIds != null &&
                      session.SelectedQuestionIds.Count > 0 &&
                      session.SelectedQuestionIds[^1] != a.QuestionId &&
                      session.SelectedQuestionIds.Contains(a.QuestionId)));
                bool needsFollowUp = isBorderline && !hasChildFollowUp && !hasProgressedToLaterQuestion && followUpsCountForQuestion < maxAllowedFollowUps;

                string? followUpPrompt = FollowUpPromptBuilder.ResolveEvaluationFollowUpPrompt(evaluation);
                if (needsFollowUp)
                {
                    try
                    {
                        var persistedFollowUp = followUpPrompt;
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
                                .SelectMany(x => (x.AiEvaluations ?? Enumerable.Empty<Domain.Entities.AiEvaluation>()).OrderByDescending(e => e.EvaluatedAt).Take(1))
                                .Select(FollowUpPromptBuilder.ResolveEvaluationFollowUpPrompt)
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
                    PromptText = a.PromptText,
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
                    CriteriaScores = evaluation?.Details != null
                        ? evaluation.Details
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
                            }).ToList()
                        : new List<PracticeEvaluationDetailDto>()
                };
            }).ToList();

        var sysBufferStr = await context.SystemConfigs
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

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;

public class ReplayDlqTaskCommandHandler : IRequestHandler<ReplayDlqTaskCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGradingQueueChannel _gradingQueueChannel;
    private readonly IAiGradingService? _aiGradingService;

    public ReplayDlqTaskCommandHandler(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueueChannel,
        IAiGradingService? aiGradingService = null)
    {
        _context = context;
        _gradingQueueChannel = gradingQueueChannel;
        _aiGradingService = aiGradingService;
    }

    public async Task<Result<Unit>> Handle(ReplayDlqTaskCommand request, CancellationToken cancellationToken)
    {
        var actor = await DlqAuthorization.ActorAsync(_context, request.ActorUserId, request.ActorRole, true, cancellationToken);
        if (actor is null) return Result<Unit>.Failure(DlqAuthorization.Forbidden);

        var dlqItem = await _context.DeadLetterQueues
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (dlqItem == null)
        {
            return Result<Unit>.Failure("Không tìm thấy tác vụ DLQ với Id đã cho.");
        }

        if (!await DlqAuthorization.CanAccessAsync(_context, actor, dlqItem, cancellationToken))
            return Result<Unit>.Failure(DlqAuthorization.Forbidden);

        if (dlqItem.Status == "resolved")
        {
            return Result<Unit>.Failure("Tác vụ DLQ đã được giải quyết (resolved) trước đó.");
        }

        var canonicalFlow = DlqAuthorization.Flow(dlqItem);
        var isInClassTask = canonicalFlow == "IN_CLASS_EXAM";
        var isPracticeOrMockTask = canonicalFlow is "PRACTICE" or "MOCK_EXAM";
        var isOfficialTask = canonicalFlow == "OFFICIAL_EXAM";

        if (request.ActorRole != null)
        {
            if (isPracticeOrMockTask && !string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền replay tác vụ Luyện tập hoặc Thi thử.");
            }

            if (isOfficialTask && !string.Equals(request.ActorRole, "examination_officer", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Cán bộ Khảo thí (Examination Officer) mới có quyền replay tác vụ Thi thật.");
            }

            if (isInClassTask &&
                !string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền replay tác vụ Ca thi trong lớp.");
            }
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var now = DateTime.UtcNow;
        var wasAbandoned = string.Equals(dlqItem.Status, "abandoned", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(dlqItem.TaskType, "OfficialExamGrading", StringComparison.OrdinalIgnoreCase))
        {
            OfficialExamGradingTask? officialTask;
            try
            {
                officialTask = JsonSerializer.Deserialize<OfficialExamGradingTask>(dlqItem.PayloadJson, options);
            }
            catch (Exception ex)
            {
                return Result<Unit>.Failure($"Không thể giải mã dữ liệu tác vụ (PayloadJson): {ex.Message}");
            }

            if (officialTask == null || (officialTask.JobId == Guid.Empty && officialTask.SubmissionId == Guid.Empty))
            {
                return Result<Unit>.Failure("Dữ liệu tác vụ OfficialExamGrading sau khi giải mã không hợp lệ.");
            }

            var job = await _context.GradingJobs
                .FirstOrDefaultAsync(j => j.Id == officialTask.JobId || j.SubmissionId == officialTask.SubmissionId, cancellationToken);

            if (job == null)
            {
                return Result<Unit>.Failure("Không tìm thấy GradingJob tương ứng với tác vụ DLQ này.");
            }

            var submission = await _context.ExamQuestionSubmissions
                .Include(s => s.Ticket)
                .FirstOrDefaultAsync(s => s.Id == job.SubmissionId, cancellationToken)
                ?? await _context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(s => s.Id == job.SubmissionId, cancellationToken);

            var isTicketLockedOrPublished = submission?.Ticket?.IsLocked == true ||
                string.Equals(submission?.Ticket?.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(submission?.Ticket?.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

            if (isTicketLockedOrPublished)
            {
                dlqItem.Status = "resolved";
                dlqItem.ResolvedAt = now;
                dlqItem.NextRetryAt = null;
                dlqItem.LastAttemptAt = now;
                dlqItem.ResolutionNotes = "Bỏ qua replay do vé thi đã khóa một chiều hoặc đã công bố điểm.";
                if (wasAbandoned) dlqItem.RetryCount = 0;
                if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;

                await _context.SaveChangesAsync(cancellationToken);
                return Result<Unit>.Success(Unit.Value);
            }

            var isOfficialSubmissionAlreadyGraded = submission != null &&
                (string.Equals(submission.GradingStatus, "ai_graded", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(submission.GradingStatus, "audited", StringComparison.OrdinalIgnoreCase));

            if (!isOfficialSubmissionAlreadyGraded && job.Status != "completed")
            {
                job.Status = "pending";
                job.NextAttemptAt = now;
                job.LockedUntil = null;
                job.UpdatedAt = now;

                if (submission != null && submission.GradingStatus == "failed")
                {
                    submission.GradingStatus = "pending";
                }
            }

            dlqItem.Status = "resolved";
            dlqItem.ResolvedAt = now;
            dlqItem.NextRetryAt = null;
            dlqItem.LastAttemptAt = now;
            if (wasAbandoned) dlqItem.RetryCount = 0;
            if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }

        if (canonicalFlow == "MOCK_EXAM")
        {
            Guid sessionId = Guid.Empty;
            Guid studentId = Guid.Empty;
            try
            {
                using var doc = JsonDocument.Parse(dlqItem.PayloadJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("SessionId", out var sidProp) || root.TryGetProperty("sessionId", out sidProp))
                {
                    sidProp.TryGetGuid(out sessionId);
                }
                if (root.TryGetProperty("StudentId", out var stidProp) || root.TryGetProperty("studentId", out stidProp))
                {
                    stidProp.TryGetGuid(out studentId);
                }
            }
            catch (JsonException)
            {
                if (dlqItem.CorrelationId.HasValue)
                {
                    sessionId = dlqItem.CorrelationId.Value;
                }
            }

            if (sessionId == Guid.Empty && dlqItem.CorrelationId.HasValue)
            {
                sessionId = dlqItem.CorrelationId.Value;
            }

            if (sessionId == Guid.Empty)
            {
                return Result<Unit>.Failure("Dữ liệu tác vụ MockExamGrading không chứa SessionId hợp lệ.");
            }

            var session = await _context.MockExamSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

            if (session == null)
            {
                return Result<Unit>.Failure("Không tìm thấy MockExamSession tương ứng với tác vụ DLQ này.");
            }

            if (!string.Equals(session.Status, "graded", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(session.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                session.Status = "submitted";
                session.TotalScore = null;

                if (_aiGradingService != null)
                {
                    try
                    {
                        var submitHandler = new SubmitMockExamSessionCommandHandler(_context, _aiGradingService);
                        var submitRes = await submitHandler.Handle(new SubmitMockExamSessionCommand(session.Id, session.StudentId), cancellationToken);
                        if (!submitRes.IsSuccess)
                        {
                            dlqItem.LastAttemptAt = now;
                            dlqItem.ErrorMessage = submitRes.Error;
                            await _context.SaveChangesAsync(cancellationToken);
                            return Result<Unit>.Failure($"Replay chấm thi thử thất bại: {submitRes.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        dlqItem.LastAttemptAt = now;
                        dlqItem.ErrorMessage = ex.Message;
                        await _context.SaveChangesAsync(cancellationToken);
                        return Result<Unit>.Failure($"Replay chấm thi thử thất bại: {ex.Message}");
                    }
                }
            }

            dlqItem.Status = "resolved";
            dlqItem.ResolvedAt = now;
            dlqItem.NextRetryAt = null;
            dlqItem.LastAttemptAt = now;
            if (wasAbandoned) dlqItem.RetryCount = 0;
            if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }

        if (isInClassTask)
        {
            var canonicalSubmission = await DlqAuthorization.InClassSubmissionAsync(_context, dlqItem, cancellationToken);
            if (canonicalSubmission is null) return Result<Unit>.Failure(DlqAuthorization.Forbidden);

            if (IsInFlight(dlqItem, now))
            {
                return Result<Unit>.Failure("CONFLICT: Tác vụ DLQ này vừa được đưa lại vào hàng đợi chấm điểm, vui lòng chờ hệ thống xử lý trước khi thử lại.");
            }

            GradingTask? inClassTask = null;
            Guid submissionId = dlqItem.CorrelationId ?? Guid.Empty;

            if (!string.IsNullOrWhiteSpace(dlqItem.PayloadJson))
            {
                try
                {
                    inClassTask = JsonSerializer.Deserialize<GradingTask>(dlqItem.PayloadJson, options);
                    if (inClassTask?.InClassSubmissionId.HasValue == true && inClassTask.InClassSubmissionId.Value != Guid.Empty)
                    {
                        submissionId = inClassTask.InClassSubmissionId.Value;
                    }
                }
                catch (Exception ex)
                {
                    if (submissionId == Guid.Empty)
                    {
                        return Result<Unit>.Failure($"Không thể giải mã dữ liệu tác vụ (PayloadJson): {ex.Message}");
                    }
                }
            }

            if (submissionId == Guid.Empty)
            {
                return Result<Unit>.Failure("Dữ liệu tác vụ InClassGrading không chứa InClassSubmissionId hoặc CorrelationId hợp lệ.");
            }

            var submission = await _context.InClassExamSubmissions
                .FirstOrDefaultAsync(s => s.Id == submissionId, cancellationToken);

            if (submission == null)
            {
                return Result<Unit>.Failure("Không tìm thấy InClassExamSubmission tương ứng với tác vụ DLQ này.");
            }

            var shift = await _context.InClassExamShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(sh => sh.Id == submission.ShiftId, cancellationToken);

            var isInClassAlreadyGradedOrLocked = submission.AiScore.HasValue ||
                string.Equals(submission.Status, "AI_GRADED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(submission.Status, "AUDITED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(submission.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase) ||
                shift?.IsLocked == true ||
                string.Equals(shift?.Status, "FINALIZED", StringComparison.OrdinalIgnoreCase);

            if (isInClassAlreadyGradedOrLocked)
            {
                dlqItem.Status = "resolved";
                dlqItem.ResolvedAt = now;
                dlqItem.NextRetryAt = null;
                dlqItem.LastAttemptAt = now;
                if (wasAbandoned) dlqItem.RetryCount = 0;
                if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;

                await _context.SaveChangesAsync(cancellationToken);
                return Result<Unit>.Success(Unit.Value);
            }

            submission.Status = "SUBMITTED";
            if (wasAbandoned) dlqItem.RetryCount = 0;
            if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;
            dlqItem.Status = "pending";
            dlqItem.ResolvedAt = null;
            dlqItem.NextRetryAt = now.AddMinutes(15);
            dlqItem.LastAttemptAt = now;
            dlqItem.CorrelationId ??= submission.Id;

            inClassTask ??= new GradingTask
            {
                AnswerId = submission.Id,
                SessionId = submission.ShiftId,
                QuestionId = submission.QuestionId,
                StudentId = submission.StudentId,
                AnswerText = submission.Transcript ?? string.Empty,
                IsFollowUp = false,
                IsFullSession = false,
                InClassSubmissionId = submission.Id
            };

            // Never enqueue client/payload resource identities after authorizing a different record.
            inClassTask.AnswerId = submission.Id;
            inClassTask.SessionId = submission.ShiftId;
            inClassTask.QuestionId = submission.QuestionId;
            inClassTask.StudentId = submission.StudentId;
            inClassTask.InClassSubmissionId = submission.Id;
            inClassTask.AnswerText = submission.Transcript ?? string.Empty;
            inClassTask.IsFollowUp = submission.IsFollowUp;
            inClassTask.ParentAnswerId = submission.ParentSubmissionId;

            await _context.SaveChangesAsync(cancellationToken);
            await _gradingQueueChannel.EnqueueAsync(inClassTask, cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }

        if (IsInFlight(dlqItem, now))
        {
            return Result<Unit>.Failure("CONFLICT: Tác vụ DLQ này vừa được đưa lại vào hàng đợi chấm điểm, vui lòng chờ hệ thống xử lý trước khi thử lại.");
        }

        GradingTask? gradingTask;
        try
        {
            gradingTask = JsonSerializer.Deserialize<GradingTask>(dlqItem.PayloadJson, options);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Không thể giải mã dữ liệu tác vụ (PayloadJson): {ex.Message}");
        }

        if (gradingTask == null)
        {
            return Result<Unit>.Failure("Dữ liệu tác vụ sau khi giải mã là rỗng (null).");
        }

        if (gradingTask.AnswerId != Guid.Empty)
        {
            var answer = await _context.PracticeAnswers
                .FirstOrDefaultAsync(a => a.Id == gradingTask.AnswerId, cancellationToken);
            if (answer != null)
            {
                if (string.Equals(answer.Status, "graded", StringComparison.OrdinalIgnoreCase))
                {
                    dlqItem.Status = "resolved";
                    dlqItem.ResolvedAt = now;
                    dlqItem.NextRetryAt = null;
                    dlqItem.LastAttemptAt = now;
                    if (wasAbandoned) dlqItem.RetryCount = 0;
                    if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;
                    await _context.SaveChangesAsync(cancellationToken);
                    return Result<Unit>.Success(Unit.Value);
                }

                answer.Status = "pending";
            }

            dlqItem.CorrelationId ??= gradingTask.AnswerId;
        }

        if (wasAbandoned) dlqItem.RetryCount = 0;
        if (request.ActorUserId.HasValue) dlqItem.ResolvedByUserId = request.ActorUserId.Value;
        dlqItem.Status = "pending";
        dlqItem.ResolvedAt = null;
        dlqItem.NextRetryAt = now.AddMinutes(15);
        dlqItem.LastAttemptAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        await _gradingQueueChannel.EnqueueAsync(gradingTask, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }

    private static bool IsInFlight(OralExamination.Domain.Entities.DeadLetterQueue dlqItem, DateTime now) =>
        string.Equals(dlqItem.Status, "pending", StringComparison.OrdinalIgnoreCase) &&
        dlqItem.NextRetryAt.HasValue &&
        dlqItem.NextRetryAt.Value > now &&
        dlqItem.LastAttemptAt.HasValue &&
        dlqItem.LastAttemptAt.Value >= now.AddMinutes(-2);
}

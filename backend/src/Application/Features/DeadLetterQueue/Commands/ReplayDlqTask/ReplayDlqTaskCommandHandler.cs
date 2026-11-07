using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;

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
        var dlqItem = await _context.DeadLetterQueues
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (dlqItem == null)
        {
            return Result<Unit>.Failure("Không tìm thấy tác vụ DLQ với Id đã cho.");
        }

        if (dlqItem.Status == "resolved")
        {
            return Result<Unit>.Failure("Tác vụ DLQ đã được giải quyết (resolved) trước đó.");
        }

        var isInClassTask =
            string.Equals(dlqItem.TaskType, "InClassGrading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.FlowType, "IN_CLASS_EXAM", StringComparison.OrdinalIgnoreCase);

        var isPracticeOrMockTask = !isInClassTask && (
            string.Equals(dlqItem.TaskType, "PracticeGrading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.TaskType, "MockExamGrading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.FlowType, "PRACTICE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.FlowType, "MOCK_EXAM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.ManagedByRole, "admin", StringComparison.OrdinalIgnoreCase));

        var isOfficialTask = !isInClassTask && !isPracticeOrMockTask && (
            string.Equals(dlqItem.TaskType, "OfficialExamGrading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.FlowType, "OFFICIAL_EXAM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.ManagedByRole, "examination_officer", StringComparison.OrdinalIgnoreCase));

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
                !string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.ActorRole, "lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Giảng viên hoặc Quản trị viên mới có quyền replay tác vụ Ca thi trong lớp.");
            }
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var now = DateTime.UtcNow;

        if (request.ActorUserId.HasValue)
        {
            dlqItem.ResolvedByUserId = request.ActorUserId.Value;
        }

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

            if (job.Status != "completed")
            {
                job.Status = "pending";
                job.NextAttemptAt = now;
                job.LockedUntil = null;
                job.UpdatedAt = now;

                var submission = await _context.ExamQuestionSubmissions
                    .FirstOrDefaultAsync(s => s.Id == job.SubmissionId, cancellationToken);
                if (submission != null && submission.GradingStatus == "failed")
                {
                    submission.GradingStatus = "pending";
                }
            }

            dlqItem.Status = "resolved";
            dlqItem.ResolvedAt = now;
            dlqItem.NextRetryAt = null;
            dlqItem.LastAttemptAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }

        if (string.Equals(dlqItem.TaskType, "MockExamGrading", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dlqItem.FlowType, "MOCK_EXAM", StringComparison.OrdinalIgnoreCase))
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
            catch
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
                        await submitHandler.Handle(new SubmitMockExamSessionCommand(session.Id, session.StudentId), cancellationToken);
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

            await _context.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(Unit.Value);
        }

        if (isInClassTask)
        {
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

            submission.Status = "SUBMITTED";

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

            await _gradingQueueChannel.EnqueueAsync(inClassTask, cancellationToken);

            dlqItem.Status = "resolved";
            dlqItem.ResolvedAt = now;
            dlqItem.NextRetryAt = null;
            dlqItem.LastAttemptAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(Unit.Value);
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

        await _gradingQueueChannel.EnqueueAsync(gradingTask, cancellationToken);

        dlqItem.Status = "resolved";
        dlqItem.ResolvedAt = now;
        dlqItem.NextRetryAt = null;
        dlqItem.LastAttemptAt = now;

        if (gradingTask.AnswerId != Guid.Empty)
        {
            var answer = await _context.PracticeAnswers
                .FirstOrDefaultAsync(a => a.Id == gradingTask.AnswerId, cancellationToken);
            if (answer != null)
            {
                answer.Status = "pending";
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;

public class ReplayDlqTaskCommandHandler : IRequestHandler<ReplayDlqTaskCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGradingQueueChannel _gradingQueueChannel;

    public ReplayDlqTaskCommandHandler(
        IApplicationDbContext context,
        IGradingQueueChannel gradingQueueChannel)
    {
        _context = context;
        _gradingQueueChannel = gradingQueueChannel;
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

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var now = DateTime.UtcNow;

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

        // Đẩy tác vụ vào hàng đợi RAM IGradingQueueChannel để worker tiếp tục xử lý chấm bù
        await _gradingQueueChannel.EnqueueAsync(gradingTask, cancellationToken);

        // Đánh dấu bản ghi DLQ là resolved
        dlqItem.Status = "resolved";
        dlqItem.ResolvedAt = now;
        dlqItem.LastAttemptAt = now;

        // Nếu có PracticeAnswer tương ứng, khôi phục trạng thái về pending
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

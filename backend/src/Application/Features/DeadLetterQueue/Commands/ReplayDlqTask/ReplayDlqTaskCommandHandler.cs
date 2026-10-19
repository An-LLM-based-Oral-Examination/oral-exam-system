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

        GradingTask? gradingTask;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
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
        dlqItem.ResolvedAt = DateTime.UtcNow;

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

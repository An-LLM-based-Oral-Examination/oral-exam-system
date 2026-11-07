using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.DeadLetterQueue.Commands.ResolveDlqTask;

public class ResolveDlqTaskCommandHandler : IRequestHandler<ResolveDlqTaskCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;

    public ResolveDlqTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Unit>> Handle(ResolveDlqTaskCommand request, CancellationToken cancellationToken)
    {
        var dlqItem = await _context.DeadLetterQueues
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (dlqItem == null)
        {
            return Result<Unit>.Failure("Không tìm thấy tác vụ DLQ với Id đã cho.");
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
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền xử lý tác vụ Luyện tập hoặc Thi thử.");
            }

            if (isOfficialTask && !string.Equals(request.ActorRole, "examination_officer", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Cán bộ Khảo thí (Examination Officer) mới có quyền xử lý tác vụ Thi thật.");
            }

            if (isInClassTask &&
                !string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.ActorRole, "lecturer", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Unit>.Failure("FORBIDDEN: Chỉ có Giảng viên hoặc Quản trị viên mới có quyền xử lý tác vụ Ca thi trong lớp.");
            }
        }

        var now = DateTime.UtcNow;
        dlqItem.Status = "resolved";
        dlqItem.ResolvedAt = now;
        dlqItem.NextRetryAt = null;
        dlqItem.LastAttemptAt = now;
        dlqItem.ResolutionNotes = request.ResolutionNotes;
        if (request.ResolvedByUserId.HasValue)
        {
            dlqItem.ResolvedByUserId = request.ResolvedByUserId.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}

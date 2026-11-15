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
        var actor = await DlqAuthorization.ActorAsync(_context, request.ResolvedByUserId, request.ActorRole, true, cancellationToken);
        if (actor is null) return Result<Unit>.Failure(DlqAuthorization.Forbidden);

        var dlqItem = await _context.DeadLetterQueues
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken);

        if (dlqItem == null)
        {
            return Result<Unit>.Failure("Không tìm thấy tác vụ DLQ với Id đã cho.");
        }

        if (!await DlqAuthorization.CanAccessAsync(_context, actor, dlqItem, cancellationToken))
            return Result<Unit>.Failure(DlqAuthorization.Forbidden);

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

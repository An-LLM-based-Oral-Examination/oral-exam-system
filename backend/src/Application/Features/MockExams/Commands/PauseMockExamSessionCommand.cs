using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Application.Features.MockExams.Commands;

public sealed record PauseMockExamSessionCommand(Guid SessionId, Guid StudentId) : IRequest<int?>;

public sealed class PauseMockExamSessionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<PauseMockExamSessionCommand, int?>
{
    public async Task<int?> Handle(PauseMockExamSessionCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var session = await context.MockExamSessions.FirstOrDefaultAsync(
            s => s.Id == request.SessionId && s.StudentId == request.StudentId && s.Status == "in_progress",
            cancellationToken);
        if (session is null || session.ExpiresAt <= (session.PausedAt ?? now)) return null;

        if (session.PausedAt is null)
        {
            session.PausedAt = now;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
        }

        return (int)Math.Ceiling((session.ExpiresAt - session.PausedAt.Value).TotalSeconds);
    }
}

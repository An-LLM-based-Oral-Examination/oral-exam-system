using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Practice.Commands.CompletePracticeSession;

public sealed class CompletePracticeSessionCommandHandler : IRequestHandler<CompletePracticeSessionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CompletePracticeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CompletePracticeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null || session.StudentId != request.StudentId)
        {
            return Result.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        if (session.Status == "completed")
        {
            return Result.Success(); // Idempotent: nếu đã hoàn thành thì trả về thành công
        }

        session.Status = "completed";
        session.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

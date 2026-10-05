using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Application.Features.Practice.Commands.StartPracticeSession;

public sealed class StartPracticeSessionCommandHandler : IRequestHandler<StartPracticeSessionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public StartPracticeSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(StartPracticeSessionCommand request, CancellationToken cancellationToken)
    {
        // Validate Course
        var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId, cancellationToken);
        if (!courseExists)
        {
            return Result<Guid>.Failure("Môn học không tồn tại.");
        }

        // Validate Student
        var studentExists = await _context.Users.AnyAsync(u => u.Id == request.StudentId && u.Role == "student", cancellationToken);
        if (!studentExists)
        {
            return Result<Guid>.Failure("Sinh viên không tồn tại hoặc không hợp lệ.");
        }

        // Create new PracticeSession
        var session = new PracticeSession
        {
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            PracticeMode = request.IsFullSession ? "full_session" : "per_question",
            StartedAt = DateTime.UtcNow,
            Status = "in_progress"
        };

        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(session.Id);
    }
}

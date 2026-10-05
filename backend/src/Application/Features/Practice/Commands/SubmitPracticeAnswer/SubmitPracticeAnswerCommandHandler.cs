using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;

public sealed class SubmitPracticeAnswerCommandHandler : IRequestHandler<SubmitPracticeAnswerCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public SubmitPracticeAnswerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(SubmitPracticeAnswerCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Session & Ownership
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            
        if (session == null || session.StudentId != request.StudentId)
        {
            return Result<Guid>.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        if (session.Status != "in_progress")
        {
            return Result<Guid>.Failure("Phiên luyện tập đã kết thúc.");
        }

        // 2. Add Answer
        var answer = new PracticeAnswer
        {
            SessionId = request.SessionId,
            QuestionId = request.QuestionId,
            StudentId = request.StudentId,
            AnswerText = request.AnswerText,
            AudioUrl = request.AudioUrl,
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.ParentAnswerId,
            Status = "pending", // Waiting for AI Evaluation
            SubmittedAt = DateTime.UtcNow
        };

        _context.PracticeAnswers.Add(answer);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(answer.Id);
    }
}

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
    private readonly IGradingQueueChannel _queueChannel;

    public SubmitPracticeAnswerCommandHandler(IApplicationDbContext context, IGradingQueueChannel queueChannel)
    {
        _context = context;
        _queueChannel = queueChannel;
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
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.ParentAnswerId,
            Status = "pending", // Waiting for AI Evaluation
            SubmittedAt = DateTime.UtcNow
        };

        _context.PracticeAnswers.Add(answer);
        await _context.SaveChangesAsync(cancellationToken); // Tiêu chí: Lưu cực nhanh < 100ms

        // 3. Đẩy vào Hàng đợi BoundedChannel để chấm ngầm
        var gradingTask = new GradingTask
        {
            AnswerId = answer.Id,
            SessionId = session.Id,
            QuestionId = request.QuestionId,
            StudentId = request.StudentId,
            AnswerText = request.AnswerText,
            IsFollowUp = request.IsFollowUp,
            ParentAnswerId = request.ParentAnswerId,
            IsFullSession = session.PracticeMode == "full_session",
            ConnectionId = string.Empty // Lấy từ request nếu có, hoặc để SignalR tự resolve qua UserId
        };

        await _queueChannel.EnqueueAsync(gradingTask, cancellationToken);

        // 4. Trả về Id liền cho Frontend
        return Result<Guid>.Success(answer.Id);
    }
}

using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;

public sealed class SubmitPracticeBatchCommandHandler : IRequestHandler<SubmitPracticeBatchCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;
    private readonly IGradingQueueChannel _queueChannel;

    public SubmitPracticeBatchCommandHandler(IApplicationDbContext context, IGradingQueueChannel queueChannel)
    {
        _context = context;
        _queueChannel = queueChannel;
    }

    public async Task<Result<Unit>> Handle(SubmitPracticeBatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Session & Ownership
        var session = await _context.PracticeSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
            
        if (session == null || session.StudentId != request.StudentId)
        {
            return Result<Unit>.Failure("Phiên luyện tập không tồn tại hoặc không thuộc về sinh viên này.");
        }

        if (session.Status != "in_progress")
        {
            return Result<Unit>.Failure("Phiên luyện tập đã kết thúc.");
        }

        if (session.PracticeMode != "full_session")
        {
            return Result<Unit>.Failure("Tính năng nộp trọn gói chỉ hỗ trợ cho phiên [Full-Session].");
        }

        // 2. Prepare Entities for Bulk Insert
        var answers = new List<PracticeAnswer>();
        var gradingTasks = new List<GradingTask>();

        foreach (var answerDto in request.Answers)
        {
            var answer = new PracticeAnswer
            {
                SessionId = request.SessionId,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
                AnswerText = answerDto.AnswerText,
                IsFollowUp = answerDto.IsFollowUp,
                ParentAnswerId = answerDto.ParentAnswerId,
                Status = "pending",
                SubmittedAt = DateTime.UtcNow
            };

            answers.Add(answer);
            
            // Queue Tasks
            gradingTasks.Add(new GradingTask
            {
                AnswerId = answer.Id, // Note: EF Core will auto-generate Guid if not set, but let's assume it's created or we set it manually. Wait, in .NET Guid is generated on Add, but it's better to assign it upfront for the Task.
                SessionId = session.Id,
                QuestionId = answerDto.QuestionId,
                StudentId = request.StudentId,
                AnswerText = answerDto.AnswerText,
                IsFollowUp = answerDto.IsFollowUp,
                ParentAnswerId = answerDto.ParentAnswerId,
                IsFullSession = true,
                ConnectionId = string.Empty
            });
        }

        // 3. Update Session Status
        session.Status = "completed";
        session.CompletedAt = DateTime.UtcNow;

        // 4. Bulk Insert & Save
        _context.PracticeAnswers.AddRange(answers);
        await _context.SaveChangesAsync(cancellationToken); // 1 Transaction cho tất cả

        // 5. Đẩy vào Hàng đợi BoundedChannel
        foreach (var (task, answer) in gradingTasks.Zip(answers))
        {
            task.AnswerId = answer.Id; // Ensure Id is set after save
            await _queueChannel.EnqueueAsync(task, cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}

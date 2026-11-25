using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

public sealed record SubmitExamAssignmentCommand(Guid AssignmentId, List<Guid>? ExamSetIds, Guid ActorId, string? ActorRole) : IRequest<Result<object>>;

public sealed class SubmitExamAssignmentCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitExamAssignmentCommand, Result<object>>
{
    public Task<Result<object>> Handle(SubmitExamAssignmentCommand command, CancellationToken ct) =>
        db.ExecuteOfficialSemesterBatchAsync(Guid.Empty, token => Submit(command, token), ct);

    private async Task<Result<object>> Submit(SubmitExamAssignmentCommand c, CancellationToken ct)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, c.ActorId, c.ActorRole, UserRole.Lecturer, ct))
            return Fail(OfficialExamErrors.Forbidden);
        var assignment = await db.ExamAuthoringAssignments.AsNoTracking().Include(a => a.Preparation).FirstOrDefaultAsync(a => a.Id == c.AssignmentId, ct);
        if (assignment is null || assignment.LecturerId != c.ActorId) return Fail(OfficialExamErrors.Forbidden);
        var ids = c.ExamSetIds;
        if (ids is null || ids.Count == 0 || ids.Distinct().Count() != ids.Count)
            return Fail("VALIDATION: Chọn các bản nháp khác nhau để gửi duyệt.");
        var papers = await db.ExamSets.Include(p => p.ExamSetQuestions).ThenInclude(q => q.ExamQuestion).ThenInclude(q => q.Rubric).ThenInclude(r => r.Criteria)
            .Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        if (papers.Count != ids.Count || papers.Any(p => p.AssignmentId != assignment.Id || p.CreatedBy != c.ActorId || p.Status != ExamSetStatus.Draft || !p.IsActive))
            return Fail("VALIDATION: Chỉ gửi bản nháp của bạn thuộc đúng phân công.");
        foreach (var paper in papers)
        {
            if (!await ExamPaperRules.CanAuthor(db, paper, c.ActorId, c.ActorRole, ct)) return Fail(OfficialExamErrors.Forbidden);
            var error = await ExamPaperRules.Validate(db, paper, ct);
            if (error is not null) return Fail($"{paper.SetCode}: {error}");
        }
        var submitted = await db.ExamSets.AsNoTracking().CountAsync(p => p.AssignmentId == assignment.Id && p.IsActive &&
            (p.Status == ExamSetStatus.SubmittedForReview || p.Status == ExamSetStatus.Official || p.Status == ExamSetStatus.HandedOver), ct);
        if (submitted + papers.Count < assignment.SetCount)
            return Fail($"VALIDATION: Cần ít nhất {assignment.SetCount} bộ đề hợp lệ theo phân công trước khi gửi duyệt.");
        foreach (var paper in papers)
        {
            paper.Status = ExamSetStatus.SubmittedForReview;
            paper.SubmittedAt = DateTime.UtcNow;
            paper.ReviewNotes = null;
            foreach (var q in paper.ExamSetQuestions.Where(q => q.ExamQuestion.ApprovalStatus == ApprovalStatus.Draft))
                q.ExamQuestion.ApprovalStatus = ApprovalStatus.SubmittedForReview;
            await ExamPaperRules.Record(db, paper, c.ActorId, "SUBMITTED_FOR_REVIEW", null, ct);
        }
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { SubmittedCount = papers.Count });
    }
    private static Result<object> Fail(string message) => Result<object>.Failure(message);
}

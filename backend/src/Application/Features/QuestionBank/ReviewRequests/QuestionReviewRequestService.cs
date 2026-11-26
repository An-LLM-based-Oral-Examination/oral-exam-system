using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.Commands.ReviewQuestionDecision;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank.ReviewRequests;

public sealed class QuestionReviewRequestService(IApplicationDbContext context)
{
    private IQueryable<QuestionReviewRequest> Owned(Guid actorId) =>
        context.QuestionReviewRequests.Where(r => r.Course != null && r.Course.IsActive && context.Users.Any(u =>
            u.Id == actorId && u.IsActive &&
            u.Role == UserRole.DepartmentHead &&
            (r.Course.AcademicProgram != null && r.Course.AcademicProgram.DepartmentHeadUserId != null &&
             r.Course.AcademicProgram.DepartmentHeadUserId != Guid.Empty
                ? r.Course.AcademicProgram.DepartmentHeadUserId : r.Course.DepartmentHeadUserId) == actorId));

    private static ReviewRequestSummary Summary(QuestionReviewRequest r)
    {
        var pending = r.Items.Count(i => i.Decision == "PENDING");
        return new(r.Id, r.CourseId, r.Course.Code, r.Course.Name, r.Lecturer.FullName, r.Lecturer.Email,
            r.SubmittedAt, r.Notes, pending == r.Items.Count ? "PENDING" : pending > 0 ? "IN_PROGRESS" : "COMPLETED",
            r.Items.Count, pending, r.Items.Count(i => i.Decision == ApprovalStatus.Approved),
            r.Items.Count(i => i.Decision == ApprovalStatus.NeedsRevision),
            r.Items.Count(i => i.Decision == ApprovalStatus.Rejected), r.Version);
    }

    public async Task<ReviewRequestPage> List(Guid actorId, Guid? courseId, string? status, string? keyword, int page, CancellationToken ct)
    {
        return await ListQuery(Owned(actorId), courseId, status, keyword, page, ct);
    }

    public Task<ReviewRequestPage> ListMine(Guid actorId, Guid? courseId, string? status, int page, CancellationToken ct) =>
        ListQuery(context.QuestionReviewRequests.Where(r => r.SubmittedBy == actorId &&
            context.Users.Any(u => u.Id == actorId && u.IsActive && u.Role == UserRole.Lecturer)),
            courseId, status, null, page, ct);

    private async Task<ReviewRequestPage> ListQuery(IQueryable<QuestionReviewRequest> source,
        Guid? courseId, string? status, string? keyword, int page, CancellationToken ct)
    {
        var query = source.AsNoTracking().Where(r => r.Course != null && r.Course.IsActive);
        if (courseId.HasValue) query = query.Where(r => r.CourseId == courseId);
        if (status == "PENDING") query = query.Where(r => r.Items.All(i => i.Decision == "PENDING"));
        if (status == "IN_PROGRESS") query = query.Where(r => r.Items.Any(i => i.Decision == "PENDING") && r.Items.Any(i => i.Decision != "PENDING"));
        if (status == "COMPLETED") query = query.Where(r => r.Items.All(i => i.Decision != "PENDING"));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLower();
            query = query.Where(r => r.Lecturer.FullName.ToLower().Contains(term) ||
                r.Lecturer.Email.ToLower().Contains(term));
        }
        var count = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (count + 11) / 12));
        var rows = await query.Include(r => r.Course).Include(r => r.Lecturer).Include(r => r.Items)
            .OrderByDescending(r => r.SubmittedAt ?? DateTime.MinValue).ThenBy(r => r.Id)
            .Skip((page - 1) * 12).Take(12).ToListAsync(ct);
        return new(rows.Select(Summary).ToList(), count, page, 12);
    }

    public async Task<Result<ReviewRequestDetail>> Detail(Guid actorId, Guid id, CancellationToken ct)
    {
        var request = await Owned(actorId).AsNoTracking().Include(r => r.Course).Include(r => r.Lecturer).Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Result<ReviewRequestDetail>.Failure("NOT_FOUND: Không tìm thấy yêu cầu trong phạm vi phụ trách.");
        return Result<ReviewRequestDetail>.Success(await BuildDetailAsync(request, ct));
    }

    public async Task<Result<ReviewRequestDetail>> DetailMine(Guid actorId, Guid id, CancellationToken ct)
    {
        if (actorId == Guid.Empty)
        {
            return Result<ReviewRequestDetail>.Failure("FORBIDDEN: Không xác định được danh tính giảng viên.");
        }

        var isActiveLecturer = await context.Users.AsNoTracking()
            .AnyAsync(u => u.Id == actorId && u.IsActive && u.Role == UserRole.Lecturer, ct);
        if (!isActiveLecturer)
        {
            return Result<ReviewRequestDetail>.Failure("FORBIDDEN: Chỉ giảng viên đang hoạt động mới có quyền xem chi tiết yêu cầu duyệt câu hỏi của mình.");
        }

        var request = await context.QuestionReviewRequests.AsNoTracking()
            .Include(r => r.Course)
            .Include(r => r.Lecturer)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.Course != null && r.Course.IsActive, ct);

        if (request is null)
        {
            return Result<ReviewRequestDetail>.Failure("NOT_FOUND: Không tìm thấy yêu cầu duyệt câu hỏi.");
        }

        if (request.SubmittedBy != actorId)
        {
            return Result<ReviewRequestDetail>.Failure("FORBIDDEN: Bạn không có quyền xem yêu cầu duyệt câu hỏi của giảng viên khác.");
        }

        return Result<ReviewRequestDetail>.Success(await BuildDetailAsync(request, ct));
    }

    private async Task<ReviewRequestDetail> BuildDetailAsync(QuestionReviewRequest request, CancellationToken ct)
    {
        var items = new List<ReviewRequestItemDto>();
        var reviewerIds = request.Items.Where(i => i.ReviewedBy.HasValue).Select(i => i.ReviewedBy!.Value).ToList();
        var names = reviewerIds.Count > 0
            ? await context.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct)
            : new Dictionary<Guid, string>();
        foreach (var item in request.Items.OrderBy(i => i.Id))
        {
            var snapshot = ReviewQuestionSnapshot.Parse(item.SnapshotJson);
            if (snapshot is null && item.UsageScope == "practice")
            {
                var q = await context.PracticeQuestions.AsNoTracking().Include(q => q.Clo).Include(q => q.Rubric).ThenInclude(r => r.Criteria)
                    .FirstOrDefaultAsync(q => q.Id == item.QuestionId, ct);
                if (q is not null) snapshot = ReviewQuestionSnapshot.From(q);
            }
            if (snapshot is null && item.UsageScope == "exam")
            {
                var q = await context.ExamQuestions.AsNoTracking().Include(q => q.Clo).Include(q => q.Rubric).ThenInclude(r => r.Criteria)
                    .FirstOrDefaultAsync(q => q.Id == item.QuestionId, ct);
                if (q is not null) snapshot = ReviewQuestionSnapshot.From(q);
            }
            items.Add(new(item.Id, item.QuestionId, item.UsageScope, item.Decision, item.ReviewNotes,
                item.ReviewedAt, item.ReviewedBy.HasValue ? names.GetValueOrDefault(item.ReviewedBy.Value) : null, snapshot));
        }
        return new(Summary(request), items);
    }

    public async Task<Result<bool>> Decide(Guid actorId, Guid id, ReviewRequestDecision input, CancellationToken ct)
    {
        var request = await Owned(actorId).Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return Result<bool>.Failure("NOT_FOUND: Không tìm thấy yêu cầu trong phạm vi phụ trách.");
        var notes = input.Notes?.Trim();
        if (input.Decision is not (ApprovalStatus.Approved or ApprovalStatus.NeedsRevision or ApprovalStatus.Rejected))
            return Result<bool>.Failure("VALIDATION: Quyết định không hợp lệ.");
        if (notes?.Length > 2000 || input.Decision != ApprovalStatus.Approved && (notes?.Length ?? 0) < 10)
            return Result<bool>.Failure("VALIDATION: Nhận xét cần từ 10 đến 2000 ký tự khi yêu cầu sửa hoặc từ chối.");
        if (input.Decision == ApprovalStatus.Approved && request.SubmittedBy == actorId)
            return Result<bool>.Failure("FORBIDDEN: Không được tự phê duyệt câu hỏi của chính mình.");
        var ids = input.ItemIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0 || ids.Any(itemId => !request.Items.Any(i => i.Id == itemId)))
            return Result<bool>.Failure("VALIDATION: Chọn câu hỏi thuộc yêu cầu này.");
        var items = request.Items.Where(i => ids.Contains(i.Id)).ToList();
        if (items.All(i => i.Decision == input.Decision && i.ReviewedBy == actorId && i.ReviewNotes == notes))
            return Result<bool>.Success(true);
        if (request.Version != input.Version || items.Any(i => i.Decision != "PENDING"))
            return Result<bool>.Failure("CONFLICT: Yêu cầu đã thay đổi. Vui lòng tải lại.");
        var questionIds = items.Select(i => i.QuestionId).ToList();
        var practice = await context.PracticeQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria).Include(q => q.Clo)
            .Where(q => questionIds.Contains(q.Id) && !q.IsDeleted).ToDictionaryAsync(q => q.Id, ct);
        var exam = await context.ExamQuestions.Include(q => q.Rubric).ThenInclude(r => r.Criteria).Include(q => q.Clo)
            .Where(q => questionIds.Contains(q.Id) && !q.IsDeleted).ToDictionaryAsync(q => q.Id, ct);
        foreach (var item in items)
        {
            var p = item.UsageScope == "practice" ? practice.GetValueOrDefault(item.QuestionId) : null;
            var e = item.UsageScope == "exam" ? exam.GetValueOrDefault(item.QuestionId) : null;
            if ((p?.CourseId ?? e?.CourseId) != request.CourseId ||
                (p?.ApprovalStatus ?? e?.ApprovalStatus) != ApprovalStatus.SubmittedForReview)
                return Result<bool>.Failure("CONFLICT: Câu hỏi không còn ở trạng thái chờ duyệt.");
            var currentSnapshot = p is not null ? ReviewQuestionSnapshot.From(p) : ReviewQuestionSnapshot.From(e!);
            if (!string.IsNullOrWhiteSpace(item.SnapshotJson))
            {
                var parsedSnapshot = ReviewQuestionSnapshot.Parse(item.SnapshotJson);
                if (parsedSnapshot is null || !parsedSnapshot.Matches(currentSnapshot))
                    return Result<bool>.Failure("CONFLICT: Nội dung câu hỏi đã thay đổi sau khi gửi duyệt.");
            }
            if (input.Decision == ApprovalStatus.Approved)
            {
                if ((p?.SubmittedBy ?? e?.SubmittedBy) == actorId)
                    return Result<bool>.Failure("FORBIDDEN: Không được tự phê duyệt câu hỏi của chính mình.");
                var error = ReviewQuestionDecisionCommandHandler.ValidateQuestionBeforeApprove(
                    p?.SampleAnswer ?? e?.SampleAnswer, p?.Rubric ?? e?.Rubric, p?.ImageUrl ?? e?.ImageUrl, p?.Source ?? e?.Source);
                if (error is not null) return Result<bool>.Failure(error);
            }
        }
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            var approved = input.Decision == ApprovalStatus.Approved;
            if (item.UsageScope == "practice")
            {
                var q = practice[item.QuestionId];
                item.SnapshotJson ??= ReviewQuestionSnapshot.From(q).ToJson();
                q.ApprovalStatus = input.Decision;
                q.ReviewNotes = notes;
                q.IsActive = approved;
                q.ApprovedBy = approved ? actorId : null;
                q.ApprovedAt = approved ? now : null;
                if (approved) ReviewQuestionDecisionCommandHandler.NormalizeRubricWeights(q.Rubric);
            }
            else
            {
                var q = exam[item.QuestionId];
                item.SnapshotJson ??= ReviewQuestionSnapshot.From(q).ToJson();
                q.ApprovalStatus = input.Decision;
                q.ReviewNotes = notes;
                q.IsActive = approved;
                q.ApprovedBy = approved ? actorId : null;
                q.ApprovedAt = approved ? now : null;
                if (approved) ReviewQuestionDecisionCommandHandler.NormalizeRubricWeights(q.Rubric);
            }
            item.Decision = input.Decision;
            item.ReviewNotes = notes;
            item.ReviewedAt = now;
            item.ReviewedBy = actorId;
        }
        request.Version++;
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<bool>.Failure("CONFLICT: Yêu cầu vừa được xử lý. Vui lòng tải lại."); }
        return Result<bool>.Success(true);
    }
}

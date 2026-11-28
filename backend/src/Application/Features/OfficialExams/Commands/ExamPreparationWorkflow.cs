using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

public enum PreparationAction { Request, Configure, Assign, AssignBatch, SaveDraft, ArchiveDraft, RequestChanges, Reject, HandOver, UpdateReuse }
public sealed record ExamPreparationCommand(Guid Id, Guid ActorId, string? ActorRole, PreparationAction Action,
    RequestExamPreparation? Request = null, ConfigureExamBlueprint? Blueprint = null, AssignExamAuthor? Assignment = null,
    SaveExamSetDraft? Draft = null, ReviewExamSet? Review = null, HandOverExamPreparation? Delivery = null, AssignExamAuthors? Assignments = null,
    int? ReusePercent = null) : IRequest<Result<object>>;

public sealed class ExamPreparationCommandHandler(IApplicationDbContext db) : IRequestHandler<ExamPreparationCommand, Result<object>>
{
    public Task<Result<object>> Handle(ExamPreparationCommand command, CancellationToken ct) =>
        db.ExecuteOfficialSemesterBatchAsync(Guid.Empty, token => Execute(command, token), ct);

    private async Task<Result<object>> Execute(ExamPreparationCommand c, CancellationToken ct)
    {
        var role = c.Action == PreparationAction.Request ? UserRole.ExaminationOfficer :
            c.Action is PreparationAction.SaveDraft or PreparationAction.ArchiveDraft ? UserRole.Lecturer : UserRole.DepartmentHead;
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, c.ActorId, c.ActorRole, role, ct)) return Fail(OfficialExamErrors.Forbidden);
        if (c.Action is PreparationAction.SaveDraft or PreparationAction.ArchiveDraft or PreparationAction.RequestChanges or PreparationAction.Reject) return await ChangePaper(c, ct);
        var session = await db.OfficialExamSessions.Include(x => x.Course).FirstOrDefaultAsync(x => x.Id == c.Id, ct);
        if (session is null) return Fail("NOT_FOUND: Không tìm thấy kỳ thi.");
        if (session.Status != "scheduled") return Fail("CONFLICT: Kỳ thi đã qua giai đoạn chuẩn bị đề.");
        if (!session.Course.IsActive) return Fail(OfficialExamErrors.CourseNotFound);
        if (role == UserRole.DepartmentHead && !await OfficialExamAuthorization.OwnsAcademicCourseAsync(db, session.CourseId, c.ActorId, ct))
            return Fail(OfficialExamErrors.Forbidden);
        var preparation = await db.ExamPreparations.FirstOrDefaultAsync(x => x.SessionId == session.Id, ct);
        if (c.Action == PreparationAction.Request)
        {
            var b = c.Request;
            if (b is null || b.RequiredSetCount < 1 || b.Deadline.Kind == DateTimeKind.Unspecified || b.Deadline.ToUniversalTime() <= DateTime.UtcNow || b.Notes?.Length > 2000)
                return Fail("VALIDATION: Số đề phải lớn hơn 0, hạn bàn giao ở tương lai có múi giờ và ghi chú tối đa 2000 ký tự.");
            if (preparation is not null) return Fail("CONFLICT: Kỳ thi đã có yêu cầu chuẩn bị đề.");
            preparation = new ExamPreparation { SessionId = session.Id, RequiredSetCount = b.RequiredSetCount,
                Deadline = b.Deadline.ToUniversalTime(), Notes = b.Notes?.Trim(), RequestedBy = c.ActorId };
            db.ExamPreparations.Add(preparation);
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { preparation.Id, preparation.SessionId, preparation.RequiredSetCount, preparation.Deadline });
        }
        if (preparation is null) return Fail("VALIDATION: Khảo thí chưa gửi yêu cầu chuẩn bị đề cho kỳ thi.");
        if (preparation.HandedOverAt.HasValue) return Fail("CONFLICT: Yêu cầu đã bàn giao, không thể thay đổi.");
        if (c.Action == PreparationAction.UpdateReuse)
        {
            if (c.ReusePercent is null or < 10 or > 50) return Fail("VALIDATION: Tỷ lệ tái sử dụng phải từ 10% đến 50%.");
            var usages = await Common.ExamReuseRules.Counts(db, session.Id, ct);
            var limit = Common.ExamReuseRules.Limit(preparation.RequiredSetCount, c.ReusePercent.Value);
            if (usages.Values.Any(v => v > limit)) return Fail("CONFLICT: Tỷ lệ mới thấp hơn số lượt câu đã được sử dụng trong kỳ thi.");
            db.AuditLogs.Add(new AuditLog { UserId = c.ActorId, EntityName = "exam_preparations", EntityId = preparation.Id,
                Action = "UPDATE_EXAM_REUSE", OldValues = preparation.ReusePercent.ToString(), NewValues = c.ReusePercent.Value.ToString() });
            preparation.ReusePercent = c.ReusePercent.Value;
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { preparation.ReusePercent, MaxUses = limit, preparation.RequiredSetCount });
        }
        if (c.Action == PreparationAction.Configure)
        {
            var b = c.Blueprint;
            if (b is not null && b.ReusePercent is < 10 or > 50)
                return Fail("VALIDATION: Tỷ lệ tái sử dụng phải từ 10% đến 50%.");
            if (b is null || string.IsNullOrWhiteSpace(b.Name) || b.Name.Length > 200 || b.TotalQuestions is < 1 or > 20 || b.DurationMinutes is < 5 or > 180 ||
                b.EasyCount < 0 || b.MediumCount < 0 || b.HardCount < 0 || (long)b.EasyCount + b.MediumCount + b.HardCount != b.TotalQuestions ||
                b.CloIds is null || b.CloIds.Count == 0 || b.CloIds.Distinct().Count() != b.CloIds.Count)
                return Fail("VALIDATION: Nhập cấu trúc hợp lệ (1-20 câu, 5-180 phút), tổng phân bổ độ khó bằng số câu và chọn ít nhất một CLO.");
            if (await db.CourseClos.AsNoTracking().CountAsync(x => b.CloIds.Contains(x.Id) && x.CourseId == session.CourseId && x.IsActive, ct) != b.CloIds.Count)
                return Fail("VALIDATION: CLO phải đang hoạt động và thuộc môn của kỳ thi.");
            var usages = await Common.ExamReuseRules.Counts(db, session.Id, ct);
            if (usages.Values.Any(v => v > Common.ExamReuseRules.Limit(preparation.RequiredSetCount, b.ReusePercent)))
                return Fail("CONFLICT: Tỷ lệ mới thấp hơn số lượt câu đã được sử dụng trong kỳ thi.");
            preparation.ReusePercent = b.ReusePercent;
            var structure = new ExamStructure { CourseId = session.CourseId, CreatedBy = c.ActorId, Name = b.Name.Trim(),
                TotalQuestions = b.TotalQuestions, DurationMinutes = b.DurationMinutes, EasyCount = b.EasyCount,
                MediumCount = b.MediumCount, HardCount = b.HardCount, RequiredSetCount = preparation.RequiredSetCount };
            db.ExamStructures.Add(structure);
            foreach (var id in b.CloIds) db.ExamStructureClos.Add(new ExamStructureClo { StructureId = structure.Id, CloId = id });
            session.ExamStructureId = structure.Id;
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { StructureId = structure.Id });
        }
        if (c.Action == PreparationAction.AssignBatch)
        {
            var b = c.Assignments;
            if (b is null || b.Lecturers is null || b.Lecturers.Count == 0 || b.Lecturers.Any(x => x is null) ||
                b.Lecturers.Select(x => x.LecturerId).Distinct().Count() != b.Lecturers.Count || b.Lecturers.Any(x => x.SetCount < 1) ||
                b.Deadline.Kind == DateTimeKind.Unspecified || b.Deadline.ToUniversalTime() <= DateTime.UtcNow ||
                b.Deadline.ToUniversalTime() > preparation.Deadline || b.Notes?.Length > 2000)
                return Fail("VALIDATION: Chọn các giảng viên khác nhau, số đề lớn hơn 0 và hạn nộp hợp lệ không sau hạn bàn giao.");
            if (b.StructureId != session.ExamStructureId || !await db.ExamStructureClos.AsNoTracking().AnyAsync(x => x.StructureId == b.StructureId, ct))
                return Fail("VALIDATION: Chọn phiên bản cấu trúc hiện tại đã cấu hình cho kỳ thi.");
            var ids = b.Lecturers.Select(x => x.LecturerId).ToList();
            if (await db.ExamAuthoringAssignments.AsNoTracking().AnyAsync(x => x.PreparationId == preparation.Id && x.StructureId == b.StructureId && ids.Contains(x.LecturerId), ct))
                return Fail("CONFLICT: Có giảng viên đã được giao phiên bản cấu trúc này. Vui lòng tải lại danh sách.");
            foreach (var row in b.Lecturers)
            {
                if (!await OfficialExamAuthorization.IsActiveActorAsync(db, row.LecturerId, UserRole.Lecturer, ct) ||
                    !await db.CourseLecturers.AsNoTracking().AnyAsync(x => x.CourseId == session.CourseId && x.LecturerUserId == row.LecturerId && x.IsActive &&
                        (x.SemesterId == session.SemesterId || x.CourseOffering != null && x.CourseOffering.SemesterId == session.SemesterId && x.CourseOffering.IsEnabled), ct))
                    return Fail("VALIDATION: Giảng viên phải được phân công môn học trong học kỳ của kỳ thi.");
            }
            var assignments = b.Lecturers.Select(row => new ExamAuthoringAssignment {
                PreparationId = preparation.Id, StructureId = b.StructureId, LecturerId = row.LecturerId,
                AssignedBy = c.ActorId, SetCount = row.SetCount, Deadline = b.Deadline.ToUniversalTime(), Notes = b.Notes?.Trim()
            }).ToList();
            foreach (var assignment in assignments) db.ExamAuthoringAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { Count = assignments.Count, Ids = assignments.Select(x => x.Id).ToList() });
        }
        if (c.Action == PreparationAction.Assign)
        {
            var b = c.Assignment;
            if (b is null || b.SetCount < 1 || b.Deadline.Kind == DateTimeKind.Unspecified || b.Deadline.ToUniversalTime() <= DateTime.UtcNow || b.Deadline.ToUniversalTime() > preparation.Deadline || b.Notes?.Length > 2000)
                return Fail("VALIDATION: Số đề phải lớn hơn 0, hạn nộp ở tương lai có múi giờ và không sau hạn bàn giao.");
            if (b.StructureId != session.ExamStructureId || !await db.ExamStructureClos.AsNoTracking().AnyAsync(x => x.StructureId == b.StructureId, ct))
                return Fail("VALIDATION: Chọn phiên bản cấu trúc hiện tại đã cấu hình cho kỳ thi.");
            if (!await OfficialExamAuthorization.IsActiveActorAsync(db, b.LecturerId, UserRole.Lecturer, ct) ||
                !await db.CourseLecturers.AsNoTracking().AnyAsync(x => x.CourseId == session.CourseId && x.LecturerUserId == b.LecturerId && x.IsActive &&
                    (x.SemesterId == session.SemesterId || x.CourseOffering != null && x.CourseOffering.SemesterId == session.SemesterId && x.CourseOffering.IsEnabled), ct))
                return Fail("VALIDATION: Giảng viên phải được phân công môn học trong học kỳ của kỳ thi.");
            if (await db.ExamAuthoringAssignments.AsNoTracking().AnyAsync(x => x.PreparationId == preparation.Id && x.StructureId == b.StructureId && x.LecturerId == b.LecturerId, ct))
                return Fail("CONFLICT: Giảng viên đã được giao phiên bản cấu trúc này.");
            var assignment = new ExamAuthoringAssignment { PreparationId = preparation.Id, StructureId = b.StructureId,
                LecturerId = b.LecturerId, AssignedBy = c.ActorId, SetCount = b.SetCount, Deadline = b.Deadline.ToUniversalTime(), Notes = b.Notes?.Trim() };
            db.ExamAuthoringAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { assignment.Id });
        }
        if (c.Action == PreparationAction.HandOver)
        {
            var ids = c.Delivery?.ExamSetIds;
            if (ids is null || ids.Count != ids.Distinct().Count() || ids.Count < preparation.RequiredSetCount)
                return Fail("VALIDATION: Chọn đủ số bộ đề khác nhau theo yêu cầu Khảo thí.");
            var sets = await db.ExamSets.Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).ThenInclude(x => x.Rubric).ThenInclude(x => x.Criteria)
                .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            if (sets.Count != ids.Count || sets.Any(x => x.SessionId != session.Id || x.StructureId != session.ExamStructureId || x.Status != ExamSetStatus.Official || !x.IsActive || x.IsSampleSet))
                return Fail("VALIDATION: Chỉ bàn giao đề đã duyệt thuộc đúng kỳ thi và phiên bản cấu trúc hiện tại.");
            foreach (var set in sets)
            {
                var error = await ExamPaperRules.Validate(db, set, ct);
                if (error is not null) return Fail(error);
            }
            foreach (var set in sets)
            {
                set.Status = ExamSetStatus.HandedOver;
                set.HandedOverAt = DateTime.UtcNow;
                await ExamPaperRules.Record(db, set, c.ActorId, "HANDED_OVER", null, ct);
            }
            preparation.HandedOverAt = DateTime.UtcNow;
            preparation.HandedOverBy = c.ActorId;
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { preparation.Id, Count = sets.Count, preparation.HandedOverAt });
        }
        return Fail("VALIDATION: Thao tác không hợp lệ.");
    }

    private async Task<Result<object>> ChangePaper(ExamPreparationCommand c, CancellationToken ct)
    {
        var set = await db.ExamSets.Include(x => x.ExamSetQuestions).ThenInclude(x => x.ExamQuestion).ThenInclude(x => x.Rubric).ThenInclude(x => x.Criteria)
            .FirstOrDefaultAsync(x => x.Id == c.Id, ct);
        if (set is null) return Fail(OfficialExamErrors.ExamSetNotFound);
        if (c.Action == PreparationAction.SaveDraft)
        {
            if (!await ExamPaperRules.CanAuthor(db, set, c.ActorId, c.ActorRole, ct, allowInactive: true)) return Fail(OfficialExamErrors.Forbidden);
            if (!set.IsActive) return Fail("CONFLICT: Bộ đề đã bị xóa trước đó.");
            if (set.Status is not (ExamSetStatus.Draft or ExamSetStatus.NeedsRevision or ExamSetStatus.Rejected)) return Fail(OfficialExamErrors.ExamSetStatusInvalid);
            var error = await ExamPaperRules.SetQuestions(db, set, c.Draft?.QuestionIds, c.ActorId, ct);
            if (error is not null) return Fail(error);
            set.Status = ExamSetStatus.Draft;
            set.ReviewNotes = null;
            set.SubmittedAt = null;
            set.ApprovedAt = null;
            set.ApprovedBy = null;
            await ExamPaperRules.Record(db, set, c.ActorId, "DRAFT_SAVED", null, ct);
        }
        else if (c.Action == PreparationAction.ArchiveDraft)
        {
            if (!await ExamPaperRules.CanAuthor(db, set, c.ActorId, c.ActorRole, ct, allowInactive: true)) return Fail(OfficialExamErrors.Forbidden);
            if (!set.IsActive) return Fail("CONFLICT: Bộ đề đã bị xóa trước đó.");
            if (set.Status is not (ExamSetStatus.Draft or ExamSetStatus.NeedsRevision or ExamSetStatus.Rejected)) return Fail(OfficialExamErrors.ExamSetStatusInvalid);
            if (await db.StudentExamTickets.AsNoTracking().AnyAsync(t => t.ExamSetId == set.Id, ct))
                return Fail("CONFLICT: Không thể xóa bộ đề đã được gán cho vé thi sinh viên.");
            var links = set.ExamSetQuestions.ToList();
            if (links.Count > 0)
            {
                db.ExamSetQuestions.RemoveRange(links);
                set.ExamSetQuestions.Clear();
                await ExamPaperRules.RetireUnreferencedPaperCopiesAsync(db, set.Id, links, ct);
            }
            set.IsActive = false;
            await ExamPaperRules.Record(db, set, c.ActorId, "ARCHIVED", null, ct);
            await db.SaveChangesAsync(ct);
            return Result<object>.Success(new { set.Id, set.IsActive, Action = "ARCHIVED" });
        }
        else
        {
            if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(db, set.CourseId, c.ActorId, ct)) return Fail(OfficialExamErrors.Forbidden);
            if (!set.IsActive || set.IsSampleSet || set.Status != ExamSetStatus.SubmittedForReview) return Fail(OfficialExamErrors.ExamSetStatusInvalid);
            if (!await db.ExamAuthoringAssignments.AsNoTracking().AnyAsync(a => a.Id == set.AssignmentId && a.Preparation.HandedOverAt == null, ct))
                return Fail("CONFLICT: Yêu cầu chuẩn bị đề đã hoàn tất.");
            if (!await db.OfficialExamSessions.AsNoTracking().AnyAsync(x => x.Id == set.SessionId && x.Status == "scheduled", ct))
                return Fail("CONFLICT: Kỳ thi đã qua giai đoạn chuẩn bị đề.");
            if (string.IsNullOrWhiteSpace(c.Review?.Notes) || c.Review.Notes.Length > 2000) return Fail("VALIDATION: Nhập phản hồi tối đa 2000 ký tự.");
            set.Status = c.Action == PreparationAction.Reject ? ExamSetStatus.Rejected : ExamSetStatus.NeedsRevision;
            set.ReviewNotes = c.Review.Notes.Trim();
            await ExamPaperRules.Record(db, set, c.ActorId, set.Status, set.ReviewNotes, ct);
        }
        await db.SaveChangesAsync(ct);
        return Result<object>.Success(new { set.Id, set.Status });
    }
    private static Result<object> Fail(string message) => Result<object>.Failure(message);
}

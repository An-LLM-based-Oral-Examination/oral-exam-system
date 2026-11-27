using System.Text.Json;
using OralExamination.Application.Features.QuestionBank;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands;

public static class ExamPaperRules
{
    public static async Task<bool> CanAuthor(IApplicationDbContext db, ExamSet set, Guid actor, string? role, CancellationToken ct)
    {
        if (role != UserRole.Lecturer || !await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, actor, role, role, ct)) return false;
        if (set.CreatedBy != actor || !set.AssignmentId.HasValue || !set.SessionId.HasValue || !set.IsActive) return false;
        if (!await OfficialExamAuthorization.CanLecturerAccessCourseAsync(db, set.CourseId, actor, ct)) return false;
        return await db.ExamAuthoringAssignments.AsNoTracking().AnyAsync(a => a.Id == set.AssignmentId &&
            a.LecturerId == actor && a.StructureId == set.StructureId && a.Structure.CourseId == set.CourseId &&
            a.Preparation.SessionId == set.SessionId && a.Preparation.Session.Status == "scheduled" && a.Preparation.HandedOverAt == null, ct);
    }

    public static async Task<string?> Validate(IApplicationDbContext db, ExamSet set, CancellationToken ct)
    {
        if (!set.SessionId.HasValue || !set.AssignmentId.HasValue || !set.IsActive || set.IsSampleSet)
            return "VALIDATION: Bộ đề phải gắn với kỳ thi và phân công hợp lệ.";
        var a = await db.ExamAuthoringAssignments.AsNoTracking().Include(x => x.Preparation).Include(x => x.Structure)
            .FirstOrDefaultAsync(x => x.Id == set.AssignmentId, ct);
        if (a is null || a.Preparation.HandedOverAt.HasValue || a.Preparation.SessionId != set.SessionId || a.StructureId != set.StructureId ||
            a.LecturerId != set.CreatedBy || a.Structure.CourseId != set.CourseId ||
            !await db.OfficialExamSessions.AsNoTracking().AnyAsync(x => x.Id == set.SessionId && x.CourseId == set.CourseId && x.Status == "scheduled", ct))
            return "VALIDATION: Kỳ thi, môn học, cấu trúc và phân công không khớp.";
        var structure = a.Structure;
        var items = set.ExamSetQuestions.ToList();
        if (items.Count != structure.TotalQuestions || items.Select(x => x.SourceQuestionId ?? x.ExamQuestionId).Distinct().Count() != items.Count)
            return $"VALIDATION: Đề phải có đúng {structure.TotalQuestions} câu khác nhau.";
        var clos = await db.ExamStructureClos.AsNoTracking().Where(x => x.StructureId == structure.Id && x.Clo.IsActive && x.Clo.CourseId == set.CourseId)
            .Select(x => x.CloId).ToListAsync(ct);
        if (clos.Count == 0) return "VALIDATION: Cấu trúc chưa có phạm vi CLO đang hoạt động.";
        foreach (var item in items)
        {
            var q = item.ExamQuestion;
            if (q is null || !q.IsPaperCopy || !q.IsActive || q.IsDeleted || q.CourseId != set.CourseId || q.CloId is null || !clos.Contains(q.CloId.Value) ||
                string.IsNullOrWhiteSpace(q.Title) || string.IsNullOrWhiteSpace(q.Content) || string.IsNullOrWhiteSpace(q.SampleAnswer) || q.SampleAnswer.Trim().Length < 50 ||
                !BloomLevel.All.Contains(q.BloomLevel))
                return "VALIDATION: Mỗi câu phải đủ nội dung, đáp án mẫu, Bloom và thuộc phạm vi CLO của môn học.";
            var rubric = q.Rubric;
            if (rubric is null || !rubric.IsActive || rubric.CourseId != set.CourseId || rubric.TotalMaxScore != 10 || rubric.Criteria.Count < 2 ||
                rubric.Criteria.Any(x => x.MaxScore <= 0 || !RubricNormalization.HasValidScorePrecision(x.MaxScore) || string.IsNullOrWhiteSpace(x.CriterionName) || x.CriterionName.Trim().Length > RubricNormalization.MaxNameLength) || rubric.Criteria.Sum(x => x.MaxScore) != rubric.TotalMaxScore)
                return "VALIDATION: Mỗi câu phải có rubric hợp lệ và tổng điểm tiêu chí bằng tổng điểm rubric.";
        }
        if (items.Count(x => x.ExamQuestion.Difficulty == "easy") != structure.EasyCount ||
            items.Count(x => x.ExamQuestion.Difficulty == "medium") != structure.MediumCount ||
            items.Count(x => x.ExamQuestion.Difficulty == "hard") != structure.HardCount)
            return "VALIDATION: Phân bổ độ khó của đề chưa khớp cấu trúc được giao.";
        return null;
    }

    public static async Task<string?> SetQuestions(IApplicationDbContext db, ExamSet set, List<Guid>? ids, Guid actor, CancellationToken ct)
    {
        if (ids is null || ids.Count != ids.Distinct().Count()) return "VALIDATION: Danh sách câu hỏi không hợp lệ hoặc có câu bị lặp.";
        var sources = await db.ExamQuestions.AsNoTracking().Include(x => x.Rubric).ThenInclude(x => x.Criteria)
            .Where(x => ids.Contains(x.Id) && x.CourseId == set.CourseId && !x.IsDeleted).ToListAsync(ct);
        var old = set.ExamSetQuestions.ToList();
        if (sources.Count != ids.Count || sources.Any(q => q.IsPaperCopy
                ? !old.Any(x => x.ExamQuestionId == q.Id)
                : !(q.ApprovalStatus == ApprovalStatus.Approved && q.IsActive && q.BankCurationStatus != BankCurationStatus.ExcludedFromBank)
                    && !(q.SubmittedBy == actor && q.ApprovalStatus is ApprovalStatus.Draft or ApprovalStatus.NeedsRevision)))
            return "VALIDATION: Chỉ chọn câu đã duyệt hoặc câu do bạn soạn thuộc môn học này.";
        if (sources.Any(q => q.Rubric is null || q.Rubric.CourseId != set.CourseId)) return "VALIDATION: Câu hỏi chưa có rubric thuộc môn học.";
        var origins = sources.Select(q => old.FirstOrDefault(x => x.ExamQuestionId == q.Id)?.SourceQuestionId ?? q.OriginQuestionId ?? q.Id).ToList();
        if (origins.Distinct().Count() != origins.Count) return "VALIDATION: Không được lặp câu hỏi nguồn trong cùng đề.";
        db.ExamSetQuestions.RemoveRange(old);
        set.ExamSetQuestions.Clear();
        foreach (var id in ids)
        {
            var q = sources.Single(x => x.Id == id);
            var rubric = new Rubric { CourseId = q.CourseId, Name = q.Rubric.Name, Description = q.Rubric.Description,
                TotalMaxScore = q.Rubric.TotalMaxScore, Criteria = q.Rubric.Criteria.Select(x => new RubricCriterion {
                    CriterionName = x.CriterionName, Description = x.Description, MaxScore = x.MaxScore, Weight = x.Weight,
                    BloomLevel = x.BloomLevel, OrderIndex = x.OrderIndex }).ToList() };
            var copy = new ExamQuestion { CourseId = q.CourseId, CloId = q.CloId, StructureId = set.StructureId,
                RubricId = rubric.Id, Rubric = rubric, Title = q.Title, Content = q.Content, SampleAnswer = q.SampleAnswer,
                KeyPoints = q.KeyPoints, Difficulty = q.Difficulty, BloomLevel = q.BloomLevel, Source = q.Source,
                ImageUrl = q.ImageUrl, ImageDescription = q.ImageDescription, SubmittedBy = actor, IsPaperCopy = true,
                BankCurationStatus = BankCurationStatus.ExcludedFromBank };
            db.ExamQuestions.Add(copy);
            var membership = new ExamSetQuestion { ExamSetId = set.Id, ExamQuestionId = copy.Id, ExamQuestion = copy,
                IsBankQuestion = q.IsPaperCopy ? old.Single(x => x.ExamQuestionId == q.Id).IsBankQuestion : q.ApprovalStatus == ApprovalStatus.Approved,
                SourceQuestionId = old.FirstOrDefault(x => x.ExamQuestionId == q.Id)?.SourceQuestionId ?? q.OriginQuestionId ?? q.Id,
                OrderIndex = set.ExamSetQuestions.Count + 1 };
            db.ExamSetQuestions.Add(membership);
            if (!set.ExamSetQuestions.Contains(membership)) set.ExamSetQuestions.Add(membership);
        }
        return null;
    }

    public static async Task Record(IApplicationDbContext db, ExamSet set, Guid actor, string action, string? notes, CancellationToken ct)
    {
        var sequence = (await db.ExamSetRevisions.AsNoTracking().Where(x => x.ExamSetId == set.Id).MaxAsync(x => (int?)x.Sequence, ct) ?? 0) + 1;
        var structure = await db.ExamStructures.AsNoTracking().FirstOrDefaultAsync(x => x.Id == set.StructureId, ct);
        var clos = await db.ExamStructureClos.AsNoTracking().Where(x => x.StructureId == set.StructureId).Select(x => x.CloId).ToListAsync(ct);
        db.ExamSetRevisions.Add(new ExamSetRevision { ExamSetId = set.Id, Sequence = sequence, ActorId = actor, Action = action, Notes = notes,
            Snapshot = JsonSerializer.Serialize(new { set.Id, set.SessionId, set.AssignmentId, set.StructureId, set.SetCode, set.Status,
                Structure = structure is null ? null : new { structure.Name, structure.TotalQuestions, structure.DurationMinutes, structure.EasyCount, structure.MediumCount, structure.HardCount, CloIds = clos },
                Questions = set.ExamSetQuestions.OrderBy(x => x.OrderIndex).Select(x => new { x.ExamQuestionId, x.SourceQuestionId, x.IsBankQuestion, x.OrderIndex,
                    x.ExamQuestion.Title, x.ExamQuestion.Content, x.ExamQuestion.CloId, x.ExamQuestion.BloomLevel, x.ExamQuestion.Difficulty,
                    x.ExamQuestion.SampleAnswer, x.ExamQuestion.KeyPoints, x.ExamQuestion.ImageUrl, x.ExamQuestion.ImageDescription,
                    Rubric = new { x.ExamQuestion.Rubric.Name, x.ExamQuestion.Rubric.Description, x.ExamQuestion.Rubric.TotalMaxScore,
                        Criteria = x.ExamQuestion.Rubric.Criteria.OrderBy(r => r.OrderIndex).Select(r => new { r.CriterionName, r.Description, r.MaxScore, r.Weight, r.BloomLevel, r.OrderIndex }) } }) }) });
    }

    public static async Task<List<DuplicateExamQuestion>> Duplicates(IApplicationDbContext db, ExamSet set, CancellationToken ct)
    {
        var session = await db.OfficialExamSessions.AsNoTracking().Include(x => x.Semester).FirstOrDefaultAsync(x => x.Id == set.SessionId, ct);
        if (session?.Semester is null) return [];
        var previous = await db.OfficialExamSessions.AsNoTracking().Where(x => x.CourseId == set.CourseId && x.Semester != null && x.Semester.StartDate < session.Semester.StartDate)
            .OrderByDescending(x => x.Semester!.StartDate).Select(x => (Guid?)x.SemesterId).FirstOrDefaultAsync(ct);
        var ids = set.ExamSetQuestions.Select(x => x.SourceQuestionId ?? x.ExamQuestionId).Distinct().ToList();
        return await db.ExamSetQuestions.AsNoTracking().Where(x => ids.Contains(x.SourceQuestionId ?? x.ExamQuestion.OriginQuestionId ?? x.ExamQuestionId) && x.ExamSetId != set.Id &&
                x.ExamSet.CourseId == set.CourseId && x.ExamSet.IsActive && !x.ExamSet.IsSampleSet && x.ExamSet.Session != null &&
                (x.ExamSet.Status == ExamSetStatus.Official || x.ExamSet.Status == ExamSetStatus.HandedOver) &&
                (x.ExamSet.Session.SemesterId == session.SemesterId || x.ExamSet.Session.SemesterId == previous))
            .Select(x => new DuplicateExamQuestion(x.SourceQuestionId ?? x.ExamQuestion.OriginQuestionId ?? x.ExamQuestionId, x.ExamSet.SetCode,
                x.ExamSet.SessionId!.Value, x.ExamSet.Session!.Title, x.ExamSet.Session.Semester!.Code, x.ExamQuestion.Title)).Distinct().ToListAsync(ct);
    }
}

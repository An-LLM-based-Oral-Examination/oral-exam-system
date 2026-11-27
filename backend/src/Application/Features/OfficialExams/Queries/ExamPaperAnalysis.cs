using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries;

public sealed record ExamPaperAnalysisQuery(Guid Id, Guid ActorId, string? ActorRole, string Kind,
    ConfigureExamBlueprint? Blueprint = null) : IRequest<Result<object>>;

public sealed class ExamPaperAnalysisQueryHandler(IApplicationDbContext db) : IRequestHandler<ExamPaperAnalysisQuery, Result<object>>
{
    public async Task<Result<object>> Handle(ExamPaperAnalysisQuery r, CancellationToken ct)
    {
        if (!await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, r.ActorId, r.ActorRole, UserRole.DepartmentHead, ct))
            return Result<object>.Failure(OfficialExamErrors.Forbidden);
        Guid courseId;
        if (r.Kind == "capacity")
        {
            var session = await db.OfficialExamSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id, ct);
            if (session is null) return Result<object>.Failure("NOT_FOUND: Không tìm thấy kỳ thi.");
            courseId = session.CourseId;
        }
        else
        {
            var paper = await db.ExamSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id && x.IsActive && !x.IsSampleSet, ct);
            if (paper is null) return Result<object>.Failure(OfficialExamErrors.ExamSetNotFound);
            if (paper.Status == ExamSetStatus.Draft) return Result<object>.Failure(OfficialExamErrors.Forbidden);
            courseId = paper.CourseId;
        }
        if (!await OfficialExamAuthorization.OwnsAcademicCourseAsync(db, courseId, r.ActorId, ct))
            return Result<object>.Failure(OfficialExamErrors.Forbidden);
        if (r.Kind == "capacity")
        {
            var b = r.Blueprint;
            if (b is null || b.ReusePercent is < 10 or > 50 || b.CloIds is null || b.CloIds.Count == 0 ||
                b.EasyCount < 0 || b.MediumCount < 0 || b.HardCount < 0 ||
                (long)b.EasyCount + b.MediumCount + b.HardCount != b.TotalQuestions || b.TotalQuestions < 1)
                return Result<object>.Failure("VALIDATION: Phân bổ độ khó, CLO và tỷ lệ tái sử dụng 10–50% phải hợp lệ.");
            if (await db.CourseClos.AsNoTracking().CountAsync(x => b.CloIds.Contains(x.Id) && x.CourseId == courseId && x.IsActive, ct) != b.CloIds.Distinct().Count())
                return Result<object>.Failure("VALIDATION: CLO phải đang hoạt động và thuộc môn học.");
            var preparation = await db.ExamPreparations.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == r.Id, ct);
            if (preparation is null) return Result<object>.Failure("NOT_FOUND: Chưa có yêu cầu chuẩn bị đề.");
            var counts = await ExamReuseRules.Counts(db, r.Id, ct);
            var bank = await db.ExamQuestions.AsNoTracking().Where(q => q.CourseId == courseId && !q.IsPaperCopy && q.IsActive && !q.IsDeleted &&
                q.ApprovalStatus == ApprovalStatus.Approved && q.CloId != null && b.CloIds.Contains(q.CloId.Value) &&
                (q.BankCurationStatus == BankCurationStatus.BankReady || q.BankCurationStatus == BankCurationStatus.CuratedIntoBank))
                .Select(q => new { Source = q.OriginQuestionId ?? q.Id, q.Difficulty }).Distinct().ToListAsync(ct);
            var session = await db.OfficialExamSessions.AsNoTracking().SingleAsync(x => x.Id == r.Id, ct);
            var structure = await db.ExamStructures.AsNoTracking().FirstOrDefaultAsync(x => x.Id == session.ExamStructureId, ct);
            var clos = structure is null ? [] : await db.ExamStructureClos.AsNoTracking().Where(x => x.StructureId == structure.Id).Select(x => x.CloId).ToListAsync(ct);
            var sameBlueprint = structure is not null && structure.EasyCount == b.EasyCount && structure.MediumCount == b.MediumCount &&
                structure.HardCount == b.HardCount && clos.Order().SequenceEqual(b.CloIds.Distinct().Order());
            var fulfilled = sameBlueprint ? await db.ExamSets.AsNoTracking().CountAsync(p => p.SessionId == r.Id && p.StructureId == session.ExamStructureId && p.IsActive && !p.IsSampleSet &&
                (p.Status == ExamSetStatus.SubmittedForReview || p.Status == ExamSetStatus.Official || p.Status == ExamSetStatus.HandedOver), ct) : 0;
            var remainingPapers = Math.Max(0, preparation.RequiredSetCount - fulfilled);
            List<DifficultyCapacity> Evaluate(int percent)
            {
                var limit = ExamReuseRules.Limit(preparation.RequiredSetCount, percent);
                return new[] { ("easy", b.EasyCount), ("medium", b.MediumCount), ("hard", b.HardCount) }.Select(d =>
                {
                    var questions = bank.Where(q => q.Difficulty == d.Item1).ToList();
                    var capacity = questions.Sum(q => (long)Math.Min(remainingPapers, Math.Max(0, limit - counts.GetValueOrDefault(q.Source))));
                    var required = (long)remainingPapers * d.Item2;
                    var missing = Math.Max(0, required - capacity);
                    var additional = remainingPapers == 0 ? 0 : Math.Max(Math.Max(0, d.Item2 - questions.Count),
                        (long)Math.Ceiling(missing / (decimal)Math.Min(limit, remainingPapers)));
                    return new DifficultyCapacity(d.Item1, required, questions.Count, capacity, missing, additional);
                }).ToList();
            }
            int? suggested = Enumerable.Range(10, 41).Where(p => counts.Values.All(v => v <= ExamReuseRules.Limit(preparation.RequiredSetCount, p)) &&
                Evaluate(p).All(d => d.AdditionalQuestionsRequired == 0)).Select(p => (int?)p).FirstOrDefault();
            return Result<object>.Success(new ExamCapacityReport(preparation.RequiredSetCount, b.ReusePercent,
                ExamReuseRules.Limit(preparation.RequiredSetCount, b.ReusePercent), b.ReusePercent <= 20 ? "LOW_REUSE" : b.ReusePercent <= 35 ? "BALANCED" : "HIGH_REUSE",
                Evaluate(b.ReusePercent).Sum(d => d.AdditionalQuestionsRequired), suggested));
        }
        return r.Kind == "bank" ? Result<object>.Success(await PaperBankRules.Candidates(db, r.Id, ct)) :
            Result<object>.Success(await ExamSimilarityRules.Report(db, r.Id, ct));
    }
}

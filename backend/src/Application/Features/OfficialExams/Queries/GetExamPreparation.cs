using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries;

public sealed record GetExamPreparationQuery(Guid? SessionId, Guid ActorId, string? ActorRole,
    Guid? ExamSetId = null, bool Duplicates = false, bool Detail = false) : IRequest<Result<object>>;

public sealed class GetExamPreparationQueryHandler(IApplicationDbContext db) : IRequestHandler<GetExamPreparationQuery, Result<object>>
{
    public async Task<Result<object>> Handle(GetExamPreparationQuery r, CancellationToken ct)
    {
        if (r.ActorRole is not (UserRole.DepartmentHead or UserRole.Lecturer or UserRole.ExaminationOfficer) ||
            !await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, r.ActorId, r.ActorRole, r.ActorRole, ct))
            return Result<object>.Failure(OfficialExamErrors.Forbidden);
        if (r.ExamSetId.HasValue)
        {
            var set = await db.ExamSets.AsNoTracking().Include(x => x.ExamSetQuestions).FirstOrDefaultAsync(x => x.Id == r.ExamSetId, ct);
            if (set is null) return Result<object>.Failure("NOT_FOUND: Không tìm thấy bộ đề.");
            if (!await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(db, set.CourseId, r.ActorId, r.ActorRole, true, ct) ||
                r.ActorRole == UserRole.Lecturer && set.CreatedBy != r.ActorId ||
                r.ActorRole == UserRole.ExaminationOfficer && set.Status != ExamSetStatus.HandedOver ||
                r.ActorRole == UserRole.DepartmentHead && (set.IsSampleSet || set.Status == ExamSetStatus.Draft))
                return Result<object>.Failure(OfficialExamErrors.Forbidden);
            if (r.Duplicates)
            {
                if (r.ActorRole == UserRole.Lecturer) return Result<object>.Failure(OfficialExamErrors.Forbidden);
                return Result<object>.Success(await ExamPaperRules.Duplicates(db, set, ct));
            }
            if (r.Detail)
            {
                var list = await new GetExamSetsQueryHandler(db).Handle(new GetExamSetsQuery(set.CourseId, set.SessionId, null, r.ActorId, r.ActorRole), ct);
                if (!list.IsSuccess) return Result<object>.Failure(list.Error);
                return Result<object>.Success(list.Value.Single(x => x.Id == set.Id));
            }
            var history = await db.ExamSetRevisions.AsNoTracking().Where(x => x.ExamSetId == set.Id).OrderByDescending(x => x.Sequence)
                .Select(x => new { x.Id, x.Sequence, x.Action, x.ActorId, x.CreatedAt, x.Notes, x.Snapshot }).ToListAsync(ct);
            return Result<object>.Success(history);
        }
        var sessions = db.OfficialExamSessions.AsNoTracking().AsQueryable();
        if (r.SessionId.HasValue) sessions = sessions.Where(x => x.Id == r.SessionId);
        var candidates = await sessions.Select(x => new { x.Id, x.CourseId }).ToListAsync(ct);
        var visible = new List<Guid>();
        foreach (var s in candidates)
            if (r.ActorRole == UserRole.ExaminationOfficer ||
                r.ActorRole == UserRole.DepartmentHead && await OfficialExamAuthorization.OwnsAcademicCourseAsync(db, s.CourseId, r.ActorId, ct) ||
                r.ActorRole == UserRole.Lecturer && await OfficialExamAuthorization.CanLecturerAccessCourseAsync(db, s.CourseId, r.ActorId, ct) &&
                await db.ExamAuthoringAssignments.AsNoTracking().AnyAsync(x => x.Preparation.SessionId == s.Id && x.LecturerId == r.ActorId, ct))
                visible.Add(s.Id);
        if (r.SessionId.HasValue && visible.Count == 0) return Result<object>.Failure(OfficialExamErrors.Forbidden);
        var result = await db.ExamPreparations.AsNoTracking().Where(x => visible.Contains(x.SessionId)).OrderByDescending(x => x.RequestedAt)
            .Select(x => new { x.Id, x.SessionId, x.Session.Title, x.Session.CourseId, CourseCode = x.Session.Course.Code,
                x.Session.SemesterId, SemesterCode = x.Session.Semester!.Code, x.Session.ExamStructureId,
                Status = x.HandedOverAt != null ? "HANDED_OVER" :
                    db.ExamSets.Count(e => e.SessionId == x.SessionId && e.StructureId == x.Session.ExamStructureId && e.IsActive && e.Status == ExamSetStatus.Official) >= x.RequiredSetCount ? "READY_FOR_HANDOVER" :
                    db.ExamAuthoringAssignments.Any(a => a.PreparationId == x.Id) ? "IN_PROGRESS" : x.Session.ExamStructureId != null ? "CONFIGURED" : "REQUESTED",
                ApprovedSetCount = db.ExamSets.Count(e => e.SessionId == x.SessionId && e.StructureId == x.Session.ExamStructureId && e.IsActive && e.Status == ExamSetStatus.Official),
                HandedOverSetCount = db.ExamSets.Count(e => e.SessionId == x.SessionId && e.IsActive && e.Status == ExamSetStatus.HandedOver),
                x.RequiredSetCount, x.ReusePercent, x.Deadline, x.Notes, x.RequestedAt, x.RequestedBy, x.HandedOverAt, x.HandedOverBy,
                Assignments = db.ExamAuthoringAssignments.Where(a => a.PreparationId == x.Id && (r.ActorRole != UserRole.Lecturer || a.LecturerId == r.ActorId))
                    .Select(a => new { a.Id, a.LecturerId, LecturerName = db.Users.Where(u => u.Id == a.LecturerId).Select(u => u.FullName).FirstOrDefault(),
                        a.StructureId, a.SetCount, a.Deadline, a.Notes, a.AssignedAt, a.AssignedBy,
                        Blueprint = new { a.Structure.Name, a.Structure.TotalQuestions, a.Structure.DurationMinutes,
                            a.Structure.EasyCount, a.Structure.MediumCount, a.Structure.HardCount,
                            CloIds = db.ExamStructureClos.Where(cl => cl.StructureId == a.StructureId).Select(cl => cl.CloId).ToList() },
                        Draft = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.Draft && e.IsActive),
                        Pending = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.SubmittedForReview && e.IsActive),
                        NeedsRevision = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.NeedsRevision && e.IsActive),
                        Rejected = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.Rejected && e.IsActive),
                        Approved = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.Official && e.IsActive),
                        HandedOver = db.ExamSets.Count(e => e.AssignmentId == a.Id && e.Status == ExamSetStatus.HandedOver && e.IsActive) }).ToList() })
            .ToListAsync(ct);
        return Result<object>.Success(result);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;

public sealed class CreateExamSetCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateExamSetCommand, Result<ExamSetDto>>
{
    public Task<Result<ExamSetDto>> Handle(CreateExamSetCommand r, CancellationToken ct) =>
        db.ExecuteOfficialSemesterBatchAsync(Guid.Empty, token => Create(r, token), ct);

    private async Task<Result<ExamSetDto>> Create(CreateExamSetCommand r, CancellationToken ct)
    {
        if (r.ActorRole is not (UserRole.Lecturer or UserRole.DepartmentHead) ||
            !await OfficialExamAuthorization.IsActiveClaimedActorAsync(db, r.ActorUserId, r.ActorRole, r.ActorRole, ct))
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        if (!await OfficialExamAuthorization.CanAccessCourseForExamSetAsync(
                db, r.CourseId, r.ActorUserId, r.ActorRole, allowExaminationOfficer: false, ct))
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        if (!r.AssignmentId.HasValue)
        {
            if (await db.ExamStructureClos.AsNoTracking().AnyAsync(x => x.StructureId == r.StructureId, ct) ||
                r.SessionId.HasValue && await db.ExamPreparations.AsNoTracking().AnyAsync(x => x.SessionId == r.SessionId, ct))
                return Result<ExamSetDto>.Failure("VALIDATION: Chọn phân công của kỳ thi để tạo đề theo cấu trúc được giao.");
            return await new LegacyCreateExamSetCommandHandler(db).Handle(r, ct);
        }
        if (r.IsSampleSet || r.InlineManualQuestions?.Count > 0)
            return Result<ExamSetDto>.Failure("VALIDATION: Đề theo phân công dùng câu hỏi nguồn và không phải đề mẫu.");
        if (!r.SessionId.HasValue || !r.AssignmentId.HasValue)
            return Result<ExamSetDto>.Failure("VALIDATION: Chọn kỳ thi và phân công trước khi tạo bộ đề.");
        var assignment = await db.ExamAuthoringAssignments.AsNoTracking().Include(x => x.Preparation).Include(x => x.Structure)
            .FirstOrDefaultAsync(x => x.Id == r.AssignmentId, ct);
        if (assignment is null || assignment.LecturerId != r.ActorUserId || assignment.StructureId != r.StructureId ||
            assignment.Structure.CourseId != r.CourseId || assignment.Preparation.SessionId != r.SessionId || assignment.Preparation.HandedOverAt != null)
            return Result<ExamSetDto>.Failure(OfficialExamErrors.Forbidden);
        if (!await db.OfficialExamSessions.AnyAsync(x => x.Id == r.SessionId && x.CourseId == r.CourseId && x.Status == "scheduled", ct))
            return Result<ExamSetDto>.Failure("VALIDATION: Kỳ thi phải thuộc môn học và đang ở giai đoạn chuẩn bị.");
        var code = r.SetCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50 || r.QuestionIds is null)
            return Result<ExamSetDto>.Failure("VALIDATION: Nhập mã bộ đề tối đa 50 ký tự và danh sách câu hỏi hợp lệ.");
        if (await db.ExamSets.AnyAsync(x => x.CourseId == r.CourseId && x.SetCode == code, ct))
            return Result<ExamSetDto>.Failure("CONFLICT: Mã bộ đề đã tồn tại trong môn học.");
        var set = new ExamSet { AssignmentId = assignment.Id, StructureId = assignment.StructureId, CourseId = r.CourseId,
            SessionId = r.SessionId, CreatedBy = r.ActorUserId, SetCode = code };
        var error = await ExamPaperRules.SetQuestions(db, set, r.QuestionIds, r.ActorUserId, ct);
        if (error is not null) return Result<ExamSetDto>.Failure(error);
        db.ExamSets.Add(set);
        await ExamPaperRules.Record(db, set, r.ActorUserId, "CREATED", null, ct);
        await db.SaveChangesAsync(ct);
        return Result<ExamSetDto>.Success(new ExamSetDto { Id = set.Id, AssignmentId = set.AssignmentId, StructureId = set.StructureId,
            CourseId = set.CourseId, SessionId = set.SessionId, CreatedBy = set.CreatedBy, SetCode = set.SetCode, Status = set.Status,
            GeneratedAt = set.GeneratedAt, IsActive = true, Questions = set.ExamSetQuestions.Select(x => new ExamSetQuestionItemDto {
                QuestionId = x.ExamQuestionId, OrderIndex = x.OrderIndex, Title = x.ExamQuestion.Title, Content = x.ExamQuestion.Content,
                Difficulty = x.ExamQuestion.Difficulty, BloomLevel = x.ExamQuestion.BloomLevel }).ToList() });
    }
}

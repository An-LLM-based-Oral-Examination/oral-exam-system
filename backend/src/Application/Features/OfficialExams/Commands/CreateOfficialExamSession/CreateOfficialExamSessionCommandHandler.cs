using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.CreateOfficialExamSession;

public sealed class CreateOfficialExamSessionCommandHandler : IRequestHandler<CreateOfficialExamSessionCommand, Result<OfficialExamSessionResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CreateOfficialExamSessionCommand> _validator;

    public CreateOfficialExamSessionCommandHandler(
        IApplicationDbContext context,
        IValidator<CreateOfficialExamSessionCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<OfficialExamSessionResponse>> Handle(CreateOfficialExamSessionCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<OfficialExamSessionResponse>.Failure(validation.Errors[0].ErrorMessage);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role != UserRole.ExaminationOfficer)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        // Operational creation must not become an alternate academic configuration endpoint.
        if (request.HasFollowUp || request.MaxFollowUpQuestions != OfficialExamRules.DefaultFollowUpQuestions)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.Forbidden);
        }

        var course = await _context.Courses.AsNoTracking()
            .Where(item => item.Id == request.CourseId && item.IsActive)
            .Select(item => new { item.Id, item.Code, item.Name, item.SemesterId })
            .FirstOrDefaultAsync(cancellationToken);
        if (course is null)
        {
            return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.CourseNotFound);
        }

        var normalizedAttemptType = string.IsNullOrWhiteSpace(request.ExamAttemptType)
            ? OfficialExamAttemptType.FinalExam
            : request.ExamAttemptType.Trim().ToUpperInvariant();

        if (!OfficialExamAttemptType.IsValid(normalizedAttemptType))
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "BAD_REQUEST: Loại đợt thi (examAttemptType) chỉ chấp nhận FE hoặc RE.");
        }

        var effectiveSemesterId = request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty
            ? request.SemesterId.Value
            : course.SemesterId;

        if (effectiveSemesterId == Guid.Empty)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "UNPROCESSABLE: Môn học chưa được gắn học kỳ hợp lệ (semester_id bắt buộc).");
        }

        var hasExplicitSemester = request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty;
        var semester = await _context.Semesters
            .AsNoTracking()
            .Where(s => s.Id == effectiveSemesterId)
            .Select(s => new { s.Id, s.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (hasExplicitSemester && semester is null)
        {
            return Result<OfficialExamSessionResponse>.Failure("NOT_FOUND: Không tìm thấy học kỳ được chọn.");
        }

        if (semester is not null && !semester.IsActive)
        {
            return Result<OfficialExamSessionResponse>.Failure("UNPROCESSABLE: Học kỳ được chọn không còn hoạt động.");
        }

        var offering = await _context.CourseOfferings
            .AsNoTracking()
            .Where(co => co.CourseId == request.CourseId && co.SemesterId == effectiveSemesterId)
            .Select(co => new { co.IsEnabled })
            .FirstOrDefaultAsync(cancellationToken);

        if (offering is not null && !offering.IsEnabled)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "UNPROCESSABLE: Môn học đã ngừng mở giảng dạy (CourseOffering) trong học kỳ đã chọn.");
        }

        if (course.SemesterId != effectiveSemesterId && offering is null)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                "UNPROCESSABLE: Môn học chưa được mở giảng dạy (CourseOffering) trong học kỳ đã chọn.");
        }

        var duplicateExists = await _context.OfficialExamSessions
            .AsNoTracking()
            .AnyAsync(
                s => s.CourseId == request.CourseId
                    && s.ExamAttemptType == normalizedAttemptType
                    && (s.SemesterId == effectiveSemesterId
                        || (effectiveSemesterId == course.SemesterId && s.SemesterId == Guid.Empty)),
                cancellationToken);

        if (duplicateExists)
        {
            return Result<OfficialExamSessionResponse>.Failure(
                $"CONFLICT: Môn học {course.Code} đã có đợt thi {normalizedAttemptType} trong học kỳ đã chọn.");
        }

        if (request.ExamStructureId.HasValue && request.ExamStructureId.Value != Guid.Empty)
        {
            var structureMatchesCourse = await _context.ExamStructures.AnyAsync(
                structure => structure.Id == request.ExamStructureId.Value
                    && structure.CourseId == request.CourseId
                    && structure.IsActive,
                cancellationToken);
            if (!structureMatchesCourse)
            {
                return Result<OfficialExamSessionResponse>.Failure(OfficialExamErrors.StructureNotFound);
            }
        }

        var session = new OfficialExamSession
        {
            CourseId = request.CourseId,
            SemesterId = effectiveSemesterId,
            ExamAttemptType = normalizedAttemptType,
            ExamStructureId = request.ExamStructureId,
            Title = request.Title!.Trim(),
            ExamDate = request.ExamDate,
            HasFollowUp = false,
            MaxFollowUpQuestions = OfficialExamRules.DefaultFollowUpQuestions,
            ExamInputMode = request.ExamInputMode!,
            TranscriptBufferSeconds = request.TranscriptBufferSeconds,
            Status = "scheduled",
            CreatedBy = actor.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.OfficialExamSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSessionAttemptUniqueConflict(ex))
        {
            return Result<OfficialExamSessionResponse>.Failure(
                $"CONFLICT: Môn học {course.Code} đã có đợt thi {normalizedAttemptType} trong học kỳ đã chọn.");
        }

        return Result<OfficialExamSessionResponse>.Success(new OfficialExamSessionResponse(
            session.Id,
            session.CourseId,
            session.ExamStructureId,
            session.Title,
            session.ExamDate,
            session.HasFollowUp,
            session.MaxFollowUpQuestions,
            session.ExamInputMode,
            session.TranscriptBufferSeconds,
            session.Status,
            session.SemesterId,
            session.ExamAttemptType,
            course.Code,
            course.Name,
            0,
            0));
    }

    private static bool IsSessionAttemptUniqueConflict(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
            if (string.Equals(constraintName, "uq_official_exam_sessions_semester_course_attempt", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(sqlState, "23505", StringComparison.Ordinal)
                && (current.Message.Contains("uq_official_exam_sessions_semester_course_attempt", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(constraintName)))
            {
                return true;
            }
        }

        return false;
    }
}

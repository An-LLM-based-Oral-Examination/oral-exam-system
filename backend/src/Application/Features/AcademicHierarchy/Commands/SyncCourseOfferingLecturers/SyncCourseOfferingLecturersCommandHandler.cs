using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy.Commands.SyncCourseOfferingLecturers;

public sealed class SyncCourseOfferingLecturersCommandHandler(
    IApplicationDbContext context,
    IValidator<SyncCourseOfferingLecturersCommand> validator)
    : IRequestHandler<SyncCourseOfferingLecturersCommand, Result<SyncCourseOfferingLecturersResponseDto>>
{
    public async Task<Result<SyncCourseOfferingLecturersResponseDto>> Handle(
        SyncCourseOfferingLecturersCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                $"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var offering = await context.CourseOfferings
            .Include(o => o.Course)
                .ThenInclude(c => c.AcademicProgram)
            .Include(o => o.Semester)
            .FirstOrDefaultAsync(o => o.Id == request.CourseOfferingId, cancellationToken);

        if (offering is null)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy môn học được mở trong học kỳ.");
        }

        if (!offering.IsEnabled || !offering.Course.IsActive)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "VALIDATION: Course offering đang bị vô hiệu hóa.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "NOT_FOUND: Không tìm thấy người thực hiện phân công.");
        }

        if (!actor.IsActive)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "FORBIDDEN: Tài khoản người thực hiện đang bị vô hiệu hóa.");
        }

        var isAdmin = string.Equals(actor.Role, UserRole.Admin, StringComparison.OrdinalIgnoreCase);
        var isOwningDepartmentHead =
            string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            offering.Course.AcademicProgram?.DepartmentHeadUserId == actor.Id;

        if (!isAdmin && !isOwningDepartmentHead)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "FORBIDDEN: Chỉ Admin hoặc Trưởng bộ môn của ngành sở hữu môn học mới được phân công giảng viên.");
        }

        var selectedIds = request.LecturerUserIds.ToHashSet();
        var lecturers = await context.Users
            .AsNoTracking()
            .Where(u => selectedIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        if (lecturers.Count != selectedIds.Count)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                "NOT_FOUND: Một hoặc nhiều giảng viên không tồn tại.");
        }

        var invalidLecturer = lecturers.FirstOrDefault(u =>
            !u.IsActive || !string.Equals(u.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase));
        if (invalidLecturer is not null)
        {
            return Result<SyncCourseOfferingLecturersResponseDto>.Failure(
                $"VALIDATION: Người dùng '{invalidLecturer.FullName}' phải là giảng viên đang hoạt động.");
        }

        var assignments = await context.CourseLecturers
            .Where(a => a.CourseOfferingId == offering.Id ||
                        (a.CourseOfferingId == null &&
                         a.CourseId == offering.CourseId &&
                         a.SemesterId == offering.SemesterId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var disabledCount = 0;

        foreach (var assignment in assignments)
        {
            assignment.CourseOfferingId = offering.Id;
            var shouldBeActive = selectedIds.Contains(assignment.LecturerUserId);
            if (assignment.IsActive && !shouldBeActive)
            {
                disabledCount++;
            }

            assignment.IsActive = shouldBeActive;
            assignment.AssignedByUserId = actor.Id;
            assignment.UpdatedAt = now;
        }

        var existingLecturerIds = assignments.Select(a => a.LecturerUserId).ToHashSet();
        foreach (var lecturerId in selectedIds.Where(id => !existingLecturerIds.Contains(id)))
        {
            context.CourseLecturers.Add(new CourseLecturer
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offering.Id,
                CourseId = offering.CourseId,
                SemesterId = offering.SemesterId,
                LecturerUserId = lecturerId,
                AssignedByUserId = actor.Id,
                AssignedAt = now,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Action = "COURSE_OFFERING_LECTURERS_SYNCED",
            EntityName = "course_offerings",
            EntityId = offering.Id,
            NewValues = $"LecturerUserIds={string.Join(',', selectedIds.Order())}",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        var resultLecturers = await context.CourseLecturers
            .AsNoTracking()
            .Where(a => a.CourseOfferingId == offering.Id)
            .OrderBy(a => a.LecturerUser.FullName)
            .Select(a => new CourseLecturerDto(
                a.LecturerUserId,
                a.LecturerUser.FullName,
                a.LecturerUser.Email,
                a.LecturerUser.StudentCode,
                a.AssignedAt,
                a.IsActive,
                a.SemesterId,
                offering.Semester.Code,
                offering.Id))
            .ToListAsync(cancellationToken);

        return Result<SyncCourseOfferingLecturersResponseDto>.Success(
            new SyncCourseOfferingLecturersResponseDto(
                offering.Id,
                offering.CourseId,
                offering.Course.Code,
                offering.SemesterId,
                offering.Semester.Code,
                resultLecturers.Count(x => x.IsActive),
                disabledCount,
                resultLecturers));
    }
}

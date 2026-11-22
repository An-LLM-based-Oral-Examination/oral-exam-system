using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourse;

public sealed class UpdateCourseCommandHandler : IRequestHandler<UpdateCourseCommand, Result<CourseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<UpdateCourseCommand> _validator;

    public UpdateCourseCommandHandler(
        IApplicationDbContext context,
        IValidator<UpdateCourseCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public UpdateCourseCommandHandler(IApplicationDbContext context)
        : this(context, new UpdateCourseCommandValidator())
    {
    }

    public async Task<Result<CourseDto>> Handle(
        UpdateCourseCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CourseDto>.Failure($"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy môn học.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var codeExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id != request.CourseId && c.Code.ToUpper() == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return Result<CourseDto>.Failure($"CONFLICT: Mã môn học '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var wasActive = course.IsActive;
        var oldSummary =
            $"code={course.Code};name={course.Name};credits={course.Credits};academicProgramId={course.AcademicProgramId?.ToString() ?? "null"};semesterId={course.SemesterId};departmentHeadUserId={course.DepartmentHeadUserId?.ToString() ?? "null"};maxMockExamsPerDay={course.MaxMockExamsPerDay};isActive={course.IsActive}";

        if (request.AcademicProgramId.HasValue)
        {
            if (request.AcademicProgramId.Value == Guid.Empty)
            {
                course.AcademicProgramId = null;
            }
            else if (request.AcademicProgramId.Value != course.AcademicProgramId)
            {
                var targetProgram = await _context.AcademicPrograms
                    .AsNoTracking()
                    .Where(p => p.Id == request.AcademicProgramId.Value)
                    .Select(p => new { p.Id, p.IsActive })
                    .FirstOrDefaultAsync(cancellationToken);

                if (targetProgram is null)
                {
                    return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy chương trình đào tạo.");
                }

                if (!targetProgram.IsActive)
                {
                    return Result<CourseDto>.Failure(
                        "VALIDATION: Chương trình đào tạo đích đang tạm khóa.");
                }

                course.AcademicProgramId = targetProgram.Id;
            }
        }

        string? deptHeadName = null;
        if (request.DepartmentHeadUserId.HasValue)
        {
            if (request.DepartmentHeadUserId.Value == Guid.Empty)
            {
                course.DepartmentHeadUserId = null;
            }
            else
            {
                var deptHead = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.DepartmentHeadUserId.Value, cancellationToken);

                if (deptHead is null)
                {
                    return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy người dùng Trưởng bộ môn.");
                }

                if (!deptHead.IsActive)
                {
                    return Result<CourseDto>.Failure("VALIDATION: Tài khoản Trưởng bộ môn đang bị khóa hoặc vô hiệu hóa.");
                }

                if (!string.Equals(deptHead.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<CourseDto>.Failure(
                        $"VALIDATION: Người dùng '{deptHead.FullName}' không có vai trò Trưởng bộ môn ({UserRole.DepartmentHead}).");
                }

                course.DepartmentHeadUserId = deptHead.Id;
                deptHeadName = deptHead.FullName;
            }
        }
        else if (course.DepartmentHeadUserId.HasValue)
        {
            deptHeadName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == course.DepartmentHeadUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        string? semesterCode = null;
        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty && request.SemesterId.Value != course.SemesterId)
        {
            var targetSemester = await _context.Semesters
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SemesterId.Value, cancellationToken);

            if (targetSemester is null)
            {
                return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy học kỳ đích trong hệ thống.");
            }

            semesterCode = targetSemester.Code;

            var oldSemesterId = course.SemesterId;
            var newSemesterId = request.SemesterId.Value;

            var oldOfferingId = await _context.CourseOfferings
                .Where(co => co.CourseId == course.Id && co.SemesterId == oldSemesterId)
                .Select(co => (Guid?)co.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var hasDependentClasses = await _context.Classes
                .AnyAsync(c => c.CourseId == course.Id && c.SemesterId == oldSemesterId, cancellationToken);

            var hasDependentLecturers = await _context.CourseLecturers
                .AnyAsync(cl => cl.CourseId == course.Id &&
                    (cl.SemesterId == oldSemesterId || (oldOfferingId.HasValue && cl.CourseOfferingId == oldOfferingId.Value)),
                    cancellationToken);

            if (hasDependentClasses || hasDependentLecturers)
            {
                return Result<CourseDto>.Failure(
                    "CONFLICT: Không thể chuyển học kỳ của môn học vì đã có lớp học hoặc phân công giảng viên trong học kỳ hiện tại.");
            }

            // Đồng bộ course_offerings trong cùng transaction
            var newOffering = await _context.CourseOfferings
                .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == newSemesterId, cancellationToken);

            if (newOffering is null)
            {
                newOffering = new CourseOffering
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    SemesterId = newSemesterId,
                    IsEnabled = request.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.CourseOfferings.Add(newOffering);
            }
            else
            {
                newOffering.IsEnabled = request.IsActive;
                newOffering.UpdatedAt = now;
            }

            var oldOffering = await _context.CourseOfferings
                .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == oldSemesterId, cancellationToken);

            if (oldOffering is not null)
            {
                oldOffering.IsEnabled = false;
                oldOffering.UpdatedAt = now;
            }

            course.SemesterId = newSemesterId;
        }
        else if (course.SemesterId != Guid.Empty)
        {
            var currentOffering = await _context.CourseOfferings
                .FirstOrDefaultAsync(co => co.CourseId == course.Id && co.SemesterId == course.SemesterId, cancellationToken);

            if (!request.IsActive)
            {
                if (currentOffering is not null && currentOffering.IsEnabled)
                {
                    currentOffering.IsEnabled = false;
                    currentOffering.UpdatedAt = now;
                }
            }
            else if (currentOffering is null)
            {
                _context.CourseOfferings.Add(new CourseOffering
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    SemesterId = course.SemesterId,
                    IsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else if (!wasActive && !currentOffering.IsEnabled)
            {
                currentOffering.IsEnabled = true;
                currentOffering.UpdatedAt = now;
            }
        }

        course.Code = normalizedCode;
        course.Name = request.Name.Trim();
        if (request.Description is not null)
        {
            course.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }
        course.Credits = request.Credits;
        course.MaxMockExamsPerDay = request.MaxMockExamsPerDay;
        if (request.MockExamDurationMinutes > 0)
        {
            course.MockExamDurationMinutes = request.MockExamDurationMinutes;
        }
        if (request.MockExamTotalQuestions > 0)
        {
            course.MockExamTotalQuestions = request.MockExamTotalQuestions;
        }
        course.IsActive = request.IsActive;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId.HasValue && request.ActorUserId.Value != Guid.Empty ? request.ActorUserId.Value : null,
            Action = "COURSE_UPDATED",
            EntityName = "courses",
            EntityId = course.Id,
            OldValues = oldSummary,
            NewValues = $"code={course.Code};name={course.Name};credits={course.Credits};academicProgramId={course.AcademicProgramId?.ToString() ?? "null"};semesterId={course.SemesterId};departmentHeadUserId={course.DepartmentHeadUserId?.ToString() ?? "null"};maxMockExamsPerDay={course.MaxMockExamsPerDay};isActive={course.IsActive}",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        if (semesterCode is null && course.SemesterId != Guid.Empty)
        {
            semesterCode = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.Id == course.SemesterId)
                .Select(s => s.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var dto = new CourseDto
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            Description = course.Description,
            Credits = course.Credits,
            AcademicProgramId = course.AcademicProgramId,
            SemesterId = course.SemesterId,
            SemesterCode = semesterCode,
            DepartmentHeadUserId = course.DepartmentHeadUserId,
            DepartmentHeadName = deptHeadName,
            TranscriptBufferSeconds = course.TranscriptBufferSeconds,
            MaxFollowUpQuestions = course.MaxFollowUpQuestions,
            MaxMockExamsPerDay = course.MaxMockExamsPerDay,
            MockExamDurationMinutes = course.MockExamDurationMinutes,
            MockExamTotalQuestions = course.MockExamTotalQuestions,
            MockExamBloomDistribution = course.MockExamBloomDistribution,
            HasFollowUp = course.HasFollowUp,
            ExamInputMode = course.ExamInputMode,
            IsActive = course.IsActive
        };

        return Result<CourseDto>.Success(dto);
    }
}

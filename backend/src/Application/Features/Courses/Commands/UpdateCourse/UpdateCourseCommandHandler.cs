using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
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
            }
        }

        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            course.SemesterId = request.SemesterId.Value;
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

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new CourseDto
        {
            Id = course.Id,
            Code = course.Code,
            Name = course.Name,
            Description = course.Description,
            Credits = course.Credits,
            SemesterId = course.SemesterId,
            DepartmentHeadUserId = course.DepartmentHeadUserId,
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

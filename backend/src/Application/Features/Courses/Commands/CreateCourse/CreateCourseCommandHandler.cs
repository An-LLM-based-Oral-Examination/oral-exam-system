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

namespace OralExamination.Application.Features.Courses.Commands.CreateCourse;

public sealed class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Result<CourseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IValidator<CreateCourseCommand> _validator;

    public CreateCourseCommandHandler(
        IApplicationDbContext context,
        IValidator<CreateCourseCommand> validator)
    {
        _context = context;
        _validator = validator;
    }

    public CreateCourseCommandHandler(IApplicationDbContext context)
        : this(context, new CreateCourseCommandValidator())
    {
    }

    public async Task<Result<CourseDto>> Handle(
        CreateCourseCommand request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CourseDto>.Failure($"VALIDATION: {validation.Errors[0].ErrorMessage}");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var codeExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Code.ToUpper() == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return Result<CourseDto>.Failure($"CONFLICT: Mã môn học '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        Guid? deptHeadId = null;
        if (request.DepartmentHeadUserId.HasValue && request.DepartmentHeadUserId.Value != Guid.Empty)
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

            deptHeadId = deptHead.Id;
        }

        var semesterId = request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty
            ? request.SemesterId.Value
            : await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Credits = request.Credits,
            SemesterId = semesterId,
            DepartmentHeadUserId = deptHeadId,
            MaxMockExamsPerDay = request.MaxMockExamsPerDay,
            MockExamDurationMinutes = request.MockExamDurationMinutes > 0 ? request.MockExamDurationMinutes : 30,
            MockExamTotalQuestions = request.MockExamTotalQuestions > 0 ? request.MockExamTotalQuestions : 5,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = false,
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Courses.Add(course);
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

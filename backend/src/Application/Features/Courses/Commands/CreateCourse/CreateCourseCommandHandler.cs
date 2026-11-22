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
        string? deptHeadName = null;
        var explicitlyClearedHead = request.DepartmentHeadUserId.HasValue &&
            request.DepartmentHeadUserId.Value == Guid.Empty;

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
            deptHeadName = deptHead.FullName;
        }

        Guid? resolvedProgramId = null;
        Guid? programDefaultHeadId = null;

        if (request.AcademicProgramId.HasValue)
        {
            if (request.AcademicProgramId.Value != Guid.Empty)
            {
                var requestedProgram = await _context.AcademicPrograms
                    .AsNoTracking()
                    .Where(p => p.Id == request.AcademicProgramId.Value)
                    .Select(p => new { p.Id, p.IsActive, p.DepartmentHeadUserId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (requestedProgram is null)
                {
                    return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy chương trình đào tạo.");
                }

                if (!requestedProgram.IsActive)
                {
                    return Result<CourseDto>.Failure(
                        "VALIDATION: Chương trình đào tạo đang tạm khóa, không thể gắn cho môn học mới.");
                }

                resolvedProgramId = requestedProgram.Id;
                programDefaultHeadId = requestedProgram.DepartmentHeadUserId;
            }
        }
        else
        {
            var defaultProgram = await _context.AcademicPrograms
                .AsNoTracking()
                .Where(p => p.IsActive && p.Code == "SE")
                .Select(p => new { p.Id, p.DepartmentHeadUserId })
                .FirstOrDefaultAsync(cancellationToken);

            resolvedProgramId = defaultProgram?.Id;
            programDefaultHeadId = defaultProgram?.DepartmentHeadUserId;
        }

        if (!explicitlyClearedHead &&
            !deptHeadId.HasValue &&
            programDefaultHeadId is Guid programHeadId &&
            programHeadId != Guid.Empty)
        {
            var programHead = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == programHeadId, cancellationToken);

            if (programHead is not null &&
                programHead.IsActive &&
                string.Equals(programHead.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            {
                deptHeadId = programHead.Id;
                deptHeadName = programHead.FullName;
            }
        }

        Guid semesterId;
        string? semesterCode;
        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            var requestedSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.Id == request.SemesterId.Value)
                .Select(s => new { s.Id, s.Code })
                .FirstOrDefaultAsync(cancellationToken);

            if (requestedSemester is null)
            {
                return Result<CourseDto>.Failure("NOT_FOUND: Không tìm thấy học kỳ.");
            }

            semesterId = requestedSemester.Id;
            semesterCode = requestedSemester.Code;
        }
        else
        {
            var latestSemester = await _context.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.StartDate)
                .Select(s => new { s.Id, s.Code })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSemester is null)
            {
                return Result<CourseDto>.Failure("VALIDATION: Chưa có học kỳ hoạt động trong hệ thống.");
            }

            semesterId = latestSemester.Id;
            semesterCode = latestSemester.Code;
        }

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Credits = request.Credits,
            AcademicProgramId = resolvedProgramId,
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
        if (semesterId != Guid.Empty)
        {
            _context.CourseOfferings.Add(new CourseOffering
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                SemesterId = semesterId,
                IsEnabled = request.IsActive,
                CreatedAt = course.CreatedAt,
                UpdatedAt = course.CreatedAt
            });
        }

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.ActorUserId.HasValue && request.ActorUserId.Value != Guid.Empty ? request.ActorUserId.Value : null,
            Action = "COURSE_CREATED",
            EntityName = "courses",
            EntityId = course.Id,
            NewValues = $"code={course.Code};name={course.Name};credits={course.Credits};academicProgramId={course.AcademicProgramId?.ToString() ?? "null"};semesterId={course.SemesterId};departmentHeadUserId={course.DepartmentHeadUserId?.ToString() ?? "null"};maxMockExamsPerDay={course.MaxMockExamsPerDay};isActive={course.IsActive}",
            CreatedAt = course.CreatedAt
        });

        await _context.SaveChangesAsync(cancellationToken);

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

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;

namespace OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;

public sealed class UpdateCourseConfigurationCommandHandler : IRequestHandler<UpdateCourseConfigurationCommand, Result<CourseConfigurationDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateCourseConfigurationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CourseConfigurationDto>> Handle(UpdateCourseConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.ActorRole, "lecturer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.ActorRole, "student", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.ActorRole, "proctor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Giảng viên không có quyền cấu hình tham số môn học.");
        }

        if (request.MaxMockExamsPerDay.HasValue &&
            !string.IsNullOrWhiteSpace(request.ActorRole) &&
            !string.Equals(request.ActorRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền cấu hình hạn ngạch thi thử (MaxMockExamsPerDay).");
        }

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
        {
            return Result<CourseConfigurationDto>.Failure("Môn học không tồn tại trong hệ thống.");
        }

        course.TranscriptBufferSeconds = request.TranscriptBufferSeconds;
        course.MaxFollowUpQuestions = request.MaxFollowUpQuestions;

        if (request.HasFollowUp.HasValue)
        {
            course.HasFollowUp = request.HasFollowUp.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.ExamInputMode))
        {
            course.ExamInputMode = request.ExamInputMode;
        }

        if (request.MaxMockExamsPerDay.HasValue)
        {
            course.MaxMockExamsPerDay = request.MaxMockExamsPerDay.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new CourseConfigurationDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseName = course.Name,
            TranscriptBufferSeconds = course.TranscriptBufferSeconds,
            MaxFollowUpQuestions = course.MaxFollowUpQuestions,
            MaxMockExamsPerDay = course.MaxMockExamsPerDay,
            HasFollowUp = course.HasFollowUp,
            ExamInputMode = course.ExamInputMode,
            IsActive = course.IsActive
        };

        return Result<CourseConfigurationDto>.Success(dto);
    }
}

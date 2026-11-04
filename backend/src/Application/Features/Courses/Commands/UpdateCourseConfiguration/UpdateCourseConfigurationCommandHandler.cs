using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Domain.Enums;

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
        var (actorRole, actorUserId) = ParseActorRoleAndId(request.ActorRole);

        if (string.Equals(actorRole, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Giảng viên không có quyền cấu hình tham số môn học.");
        }

        if (!string.IsNullOrWhiteSpace(actorRole) &&
            !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ có Trưởng bộ môn hoặc Quản trị viên (Admin) mới có quyền cấu hình tham số môn học.");
        }

        if (request.MaxMockExamsPerDay.HasValue &&
            !string.IsNullOrWhiteSpace(actorRole) &&
            !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền cấu hình hạn ngạch thi thử (MaxMockExamsPerDay).");
        }

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
        {
            return Result<CourseConfigurationDto>.Failure("Môn học không tồn tại trong hệ thống.");
        }

        if (string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase) &&
            course.DepartmentHeadUserId.HasValue &&
            actorUserId.HasValue &&
            actorUserId.Value != Guid.Empty &&
            course.DepartmentHeadUserId.Value != actorUserId.Value)
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Trưởng bộ môn chỉ được cấu hình môn học do mình phụ trách.");
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

        if (request.MockExamDurationMinutes.HasValue && request.MockExamDurationMinutes.Value > 0)
        {
            course.MockExamDurationMinutes = request.MockExamDurationMinutes.Value;
        }

        if (request.MockExamTotalQuestions.HasValue && request.MockExamTotalQuestions.Value > 0)
        {
            course.MockExamTotalQuestions = request.MockExamTotalQuestions.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.MockExamBloomDistribution))
        {
            course.MockExamBloomDistribution = request.MockExamBloomDistribution.Trim();
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
            MockExamDurationMinutes = course.MockExamDurationMinutes,
            MockExamTotalQuestions = course.MockExamTotalQuestions,
            MockExamBloomDistribution = course.MockExamBloomDistribution,
            HasFollowUp = course.HasFollowUp,
            ExamInputMode = course.ExamInputMode,
            IsActive = course.IsActive
        };

        return Result<CourseConfigurationDto>.Success(dto);
    }

    private static (string? Role, Guid? UserId) ParseActorRoleAndId(string? rawActorRole)
    {
        if (string.IsNullOrWhiteSpace(rawActorRole))
        {
            return (null, null);
        }

        var separatorIndex = rawActorRole.IndexOf(':');
        if (separatorIndex > 0)
        {
            var rolePart = rawActorRole[..separatorIndex].Trim();
            var idPart = rawActorRole[(separatorIndex + 1)..].Trim();
            if (Guid.TryParse(idPart, out var parsedId) && parsedId != Guid.Empty)
            {
                return (rolePart, parsedId);
            }
        }

        return (rawActorRole.Trim(), null);
    }
}

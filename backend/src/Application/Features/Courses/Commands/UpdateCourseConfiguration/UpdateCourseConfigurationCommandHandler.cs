using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Authorization;
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
        var (actorRole, actorUserId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var course = await _context.Courses
            .Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
        {
            return Result<CourseConfigurationDto>.Failure("Môn học không tồn tại trong hệ thống.");
        }

        if (!AcademicCourseOwnership.CanManage(course, actorRole, actorUserId))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ Admin hoặc Trưởng bộ môn phụ trách được cấu hình môn học.");
        }

        if (request.MaxMockExamsPerDay.HasValue &&
            !string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ Admin được cấu hình hạn ngạch thi thử mỗi ngày.");
        }

        if (request.MockExamDifficultyDistribution is not null)
        {
            Dictionary<string, int>? distribution;
            try
            {
                distribution = JsonSerializer.Deserialize<Dictionary<string, int>>(request.MockExamDifficultyDistribution);
            }
            catch (JsonException)
            {
                return Result<CourseConfigurationDto>.Failure("VALIDATION: Phân bổ độ khó không hợp lệ.");
            }
            var levels = new[] { "easy", "medium", "hard" };
            if (distribution is null || distribution.Count != 3 ||
                levels.Any(level => !distribution.ContainsKey(level)) ||
                distribution.Values.Any(count => count < 0 || count > 100) ||
                distribution.Values.Sum() is < 1 or > 100)
                return Result<CourseConfigurationDto>.Failure("VALIDATION: Phân bổ độ khó phải có từ 1 đến 100 câu.");

            course.MockExamDifficultyDistribution = JsonSerializer.Serialize(distribution);
            course.MockExamTotalQuestions = distribution.Values.Sum();
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

        if (request.MockExamDifficultyDistribution is null && request.MockExamTotalQuestions.HasValue && request.MockExamTotalQuestions.Value > 0)
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
            MockExamDifficultyDistribution = course.MockExamDifficultyDistribution,
            MockExamBloomDistribution = course.MockExamBloomDistribution,
            HasFollowUp = course.HasFollowUp,
            ExamInputMode = course.ExamInputMode,
            IsActive = course.IsActive
        };

        return Result<CourseConfigurationDto>.Success(dto);
    }

}

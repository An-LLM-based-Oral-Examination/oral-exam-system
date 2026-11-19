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
        if (!string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actorRole, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CourseConfigurationDto>.Failure("FORBIDDEN: Chỉ Admin hoặc Trưởng bộ môn phụ trách được cấu hình môn học.");
        }

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

        if (string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            if (!request.MaxMockExamsPerDay.HasValue)
            {
                return Result<CourseConfigurationDto>.Failure(
                    "FORBIDDEN: Quản trị viên chỉ được cập nhật hạn ngạch thi thử (MaxMockExamsPerDay) trên cấu hình môn học.");
            }

            var mutatesAcademicFields =
                (request.TranscriptBufferSeconds != 0 && request.TranscriptBufferSeconds != course.TranscriptBufferSeconds) ||
                (request.MaxFollowUpQuestions != 0 && request.MaxFollowUpQuestions != course.MaxFollowUpQuestions) ||
                (request.HasFollowUp.HasValue && request.HasFollowUp.Value != course.HasFollowUp) ||
                (!string.IsNullOrWhiteSpace(request.ExamInputMode) &&
                 !string.Equals(request.ExamInputMode.Trim(), course.ExamInputMode, StringComparison.Ordinal)) ||
                (request.MockExamDurationMinutes.HasValue && request.MockExamDurationMinutes.Value != course.MockExamDurationMinutes) ||
                (request.MockExamTotalQuestions.HasValue && request.MockExamTotalQuestions.Value != course.MockExamTotalQuestions) ||
                !AreCourseConfigJsonEquivalent(request.MockExamDifficultyDistribution, course.MockExamDifficultyDistribution) ||
                !AreCourseConfigJsonEquivalent(request.MockExamBloomDistribution, course.MockExamBloomDistribution);

            if (mutatesAcademicFields)
            {
                return Result<CourseConfigurationDto>.Failure(
                    "FORBIDDEN: Quản trị viên không được phép thay đổi tham số học thuật (Follow-up, ExamInputMode, Buffer, Cấu trúc đề) trên cấu hình môn học.");
            }

            course.MaxMockExamsPerDay = request.MaxMockExamsPerDay.Value;
        }
        else
        {
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

    private static bool AreCourseConfigJsonEquivalent(string? requested, string? persisted)
    {
        var reqBlank = string.IsNullOrWhiteSpace(requested);
        var dbBlank = string.IsNullOrWhiteSpace(persisted);
        if (reqBlank)
        {
            return true;
        }

        if (dbBlank)
        {
            return false;
        }

        var trimmedReq = requested!.Trim();
        var trimmedDb = persisted!.Trim();
        if (string.Equals(trimmedReq, trimmedDb, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            using var reqDoc = JsonDocument.Parse(trimmedReq);
            using var dbDoc = JsonDocument.Parse(trimmedDb);
            return AreJsonElementsEquivalent(reqDoc.RootElement, dbDoc.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool AreJsonElementsEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var leftProps = left.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
                var rightProps = right.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
                if (leftProps.Count != rightProps.Count)
                {
                    return false;
                }

                for (var i = 0; i < leftProps.Count; i++)
                {
                    if (!string.Equals(leftProps[i].Name, rightProps[i].Name, StringComparison.Ordinal) ||
                        !AreJsonElementsEquivalent(leftProps[i].Value, rightProps[i].Value))
                    {
                        return false;
                    }
                }

                return true;
            }
            case JsonValueKind.Array:
            {
                var leftLen = left.GetArrayLength();
                var rightLen = right.GetArrayLength();
                if (leftLen != rightLen)
                {
                    return false;
                }

                for (var i = 0; i < leftLen; i++)
                {
                    if (!AreJsonElementsEquivalent(left[i], right[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                if (left.TryGetDecimal(out var lDec) && right.TryGetDecimal(out var rDec))
                {
                    return lDec == rDec;
                }

                return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
        }
    }
}

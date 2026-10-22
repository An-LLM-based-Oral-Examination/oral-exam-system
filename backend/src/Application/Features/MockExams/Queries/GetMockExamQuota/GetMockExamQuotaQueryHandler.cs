using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;

/// <summary>
/// Handler xử lý nghiệp vụ tra cứu hạn ngạch thi thử trong ngày (MF-02).
/// Áp dụng Daily Quota Guard do Admin cấu hình theo môn học hoặc cấu hình hệ thống.
/// </summary>
public class GetMockExamQuotaQueryHandler(IApplicationDbContext context) : IRequestHandler<GetMockExamQuotaQuery, Result<MockExamQuotaDto>>
{
    public async Task<Result<MockExamQuotaDto>> Handle(GetMockExamQuotaQuery request, CancellationToken cancellationToken)
    {
        var course = await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.IsActive, cancellationToken);

        if (course is null)
        {
            return Result<MockExamQuotaDto>.Failure("Môn học không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        var sysQuotaStr = await context.SystemConfigs
            .AsNoTracking()
            .Where(c => c.Key == "max_mock_exams_per_day" || c.Key == "MaxMockExamsPerDay")
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var fallbackQuota = int.TryParse(sysQuotaStr, out var parsedSysQuota) && parsedSysQuota > 0 ? parsedSysQuota : 3;
        var maxDailyQuota = course.MaxMockExamsPerDay > 0 ? course.MaxMockExamsPerDay : fallbackQuota;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var quota = await context.MockExamQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.StudentId == request.StudentId 
                                   && q.CourseId == request.CourseId 
                                   && q.QuotaDate == today, cancellationToken);

        var usedCount = quota?.UsedCount ?? 0;
        var remainingCount = Math.Max(0, maxDailyQuota - usedCount);
        var canStartExam = usedCount < maxDailyQuota;

        var dto = new MockExamQuotaDto(
            request.CourseId,
            request.StudentId,
            today,
            usedCount,
            remainingCount,
            maxDailyQuota,
            canStartExam
        );

        return Result<MockExamQuotaDto>.Success(dto);
    }
}

using System;
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
/// Áp dụng Daily Quota Guard (K = 3 lượt/ngày/môn).
/// </summary>
public sealed class GetMockExamQuotaQueryHandler : IRequestHandler<GetMockExamQuotaQuery, Result<MockExamQuotaDto>>
{
    private const int MaxDailyQuota = 3;
    private readonly IApplicationDbContext _context;

    public GetMockExamQuotaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MockExamQuotaDto>> Handle(GetMockExamQuotaQuery request, CancellationToken cancellationToken)
    {
        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == request.CourseId, cancellationToken);

        if (!courseExists)
        {
            return Result<MockExamQuotaDto>.Failure("Môn học không tồn tại trong hệ thống.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var quota = await _context.MockExamQuotas
            .FirstOrDefaultAsync(q => q.StudentId == request.StudentId 
                                   && q.CourseId == request.CourseId 
                                   && q.QuotaDate == today, cancellationToken);

        var usedCount = quota?.UsedCount ?? 0;
        var remainingCount = Math.Max(0, MaxDailyQuota - usedCount);
        var canStartExam = usedCount < MaxDailyQuota;

        var dto = new MockExamQuotaDto(
            request.CourseId,
            request.StudentId,
            today,
            usedCount,
            remainingCount,
            MaxDailyQuota,
            canStartExam
        );

        return Result<MockExamQuotaDto>.Success(dto);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Commands;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamAudioPlaybackUrl;

public sealed record GetOfficialExamAudioPlaybackUrlQuery(
    Guid ActorUserId,
    Guid TicketId,
    Guid? SubmissionId = null) : IRequest<Result<OfficialExamAudioPlaybackUrlResponse>>;

public sealed class GetOfficialExamAudioPlaybackUrlQueryHandler(
    IApplicationDbContext context,
    IStorageService? storageService = null)
    : IRequestHandler<GetOfficialExamAudioPlaybackUrlQuery, Result<OfficialExamAudioPlaybackUrlResponse>>
{
    public async Task<Result<OfficialExamAudioPlaybackUrlResponse>> Handle(
        GetOfficialExamAudioPlaybackUrlQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                "FORBIDDEN: Không xác định được danh tính người truy xuất liên kết phát lại âm thanh.");
        }

        var actor = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null || actor.Role is not (UserRole.ExaminationOfficer or UserRole.Lecturer or UserRole.DepartmentHead))
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                "FORBIDDEN: Bạn không có quyền truy xuất âm thanh bài thi chính thức.");
        }

        var ticket = await context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Shift)
            .Include(t => t.Submissions)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                $"NOT_FOUND: {OfficialExamErrors.TicketNotFound}");
        }

        var authorized = ticket.Shift is not null
            && await OfficialExamAuthorization.CanAuditShiftAsync(context, actor.Id, ticket.Shift, cancellationToken);
        if (!authorized && actor.Role == UserRole.Lecturer)
        {
            authorized = await context.AppealRequests
                .AsNoTracking()
                .AnyAsync(
                    a => a.TicketId == ticket.Id
                        && a.AssignedTo == actor.Id
                        && (a.Status == AppealStatus.AssignedToLecturer || a.Status == AppealStatus.RegradedByLecturer),
                    cancellationToken);
        }

        if (!authorized)
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                "FORBIDDEN: Bạn không phải Giám khảo ca thi, Giảng viên được phân công phúc khảo, Trưởng bộ môn phụ trách môn hoặc Cán bộ Khảo thí.");
        }

        string? rawKeyOrUrl;
        string? sha256;
        Guid? resolvedSubmissionId = null;

        if (request.SubmissionId.HasValue && request.SubmissionId.Value != Guid.Empty)
        {
            var sub = ticket.Submissions.FirstOrDefault(s => s.Id == request.SubmissionId.Value);
            if (sub is null)
            {
                return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                    "NOT_FOUND: Không tìm thấy bản nộp câu hỏi thuộc vé thi này.");
            }

            resolvedSubmissionId = sub.Id;
            var mainSubmissionCount = ticket.Submissions.Count(s => !s.IsFollowUp);
            var allowTicketFallback = !sub.IsFollowUp && mainSubmissionCount <= 1;
            rawKeyOrUrl = sub.AudioObjectKey
                ?? sub.AudioR2Url
                ?? (allowTicketFallback ? ticket.AudioObjectKey ?? ticket.AudioR2Url : null);
            sha256 = sub.AudioHashSha256 ?? (allowTicketFallback ? ticket.AudioHashSha256 : null);
        }
        else
        {
            var ordered = ticket.Submissions
                .OrderBy(s => s.SubmittedAt)
                .ThenBy(s => s.FollowUpIndex)
                .ToList();
            var latestMain = ordered.LastOrDefault(s => !s.IsFollowUp) ?? ordered.LastOrDefault();
            resolvedSubmissionId = latestMain?.Id;
            rawKeyOrUrl = latestMain?.AudioObjectKey
                ?? latestMain?.AudioR2Url
                ?? ticket.AudioObjectKey
                ?? ticket.AudioR2Url;
            sha256 = latestMain?.AudioHashSha256 ?? ticket.AudioHashSha256;
        }

        if (string.IsNullOrWhiteSpace(rawKeyOrUrl))
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                "NOT_FOUND: Vé thi hoặc bài nộp này chưa có tệp ghi âm trên kho lưu trữ R2.");
        }

        var candidate = rawKeyOrUrl.Trim();
        var ttl = TimeSpan.FromMinutes(15);
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var parsedUri))
        {
            if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return Result<OfficialExamAudioPlaybackUrlResponse>.Success(
                    new OfficialExamAudioPlaybackUrlResponse(
                        ticket.Id,
                        resolvedSubmissionId,
                        candidate,
                        sha256,
                        DateTime.UtcNow.Add(ttl)));
            }

            if (string.Equals(parsedUri.Scheme, "javascript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, "data", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, "file", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, "vbscript", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, "blob", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUri.Scheme, "ftp", StringComparison.OrdinalIgnoreCase))
            {
                return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                    "UNPROCESSABLE: Đường dẫn tệp âm thanh chứa giao thức không hợp lệ.");
            }
        }

        if (storageService is null)
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure(
                "UNPROCESSABLE: Dịch vụ kho lưu trữ âm thanh R2 chưa được cấu hình.");
        }

        try
        {
            var presigned = await storageService.CreatePresignedDownloadUrlAsync(candidate, ttl, cancellationToken);
            return Result<OfficialExamAudioPlaybackUrlResponse>.Success(
                new OfficialExamAudioPlaybackUrlResponse(
                    ticket.Id,
                    resolvedSubmissionId,
                    presigned.DownloadUrl,
                    sha256,
                    presigned.ExpiresAt));
        }
        catch (InvalidOperationException ex)
        {
            return Result<OfficialExamAudioPlaybackUrlResponse>.Failure($"UNPROCESSABLE: {ex.Message}");
        }
    }
}

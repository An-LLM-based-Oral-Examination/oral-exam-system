using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Domain.Enums;
using OralExamination.Application.Features.Appeals;

namespace OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;

/// <summary>
/// Handler truy xuất kết quả điểm thi thật chính thức và Scorecard chi tiết theo CLO/Rubric cho sinh viên trên Student Portal (MF-04).
/// </summary>
public sealed class GetOfficialExamGradeQueryHandler(IApplicationDbContext context, AppealWindowOptions? window = null, TimeProvider? clock = null)
    : IRequestHandler<GetOfficialExamGradeQuery, Result<StudentOfficialExamGradeDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<StudentOfficialExamGradeDto>> Handle(
        GetOfficialExamGradeQuery request,
        CancellationToken cancellationToken)
    {
        var ticket = await _context.StudentExamTickets
            .AsNoTracking()
            .Include(t => t.Student)
            .Include(t => t.Shift)
                .ThenInclude(s => s.Session)
                    .ThenInclude(ss => ss.Course)
            .Include(t => t.LecturerAudit)
                .ThenInclude(la => la!.Lecturer)
            .Include(t => t.Submissions)
                .ThenInclude(sub => sub.AiEvaluations)
                    .ThenInclude(ev => ev.Details)
                        .ThenInclude(d => d.Criterion)
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? await _context.StudentExamTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("NOT_FOUND: Vé thi không tồn tại trong hệ thống.");
        }

        if (ticket.StudentId != request.StudentId)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("FORBIDDEN: Bạn không có quyền xem điểm vé thi của sinh viên khác.");
        }

        bool isPublishedOrLocked = AppealWindowPolicy.IsPublished(ticket);

        if (!isPublishedOrLocked)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("UNPROCESSABLE: Điểm ca thi này chưa được giảng viên công bố.");
        }

        var questionIds = ticket.Submissions
            .Select(s => s.ExamQuestionId)
            .Distinct()
            .ToList();

        var questionMap = questionIds.Count > 0
            ? await _context.ExamQuestions
                .AsNoTracking()
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id, cancellationToken)
            : new Dictionary<Guid, Domain.Entities.ExamQuestion>();

        decimal? totalScore = ticket.LecturerAudit?.AuditedScore
            ?? (ticket.Submissions.Count > 0 && ticket.Submissions.Any(s => s.FinalScore.HasValue || s.AiScore.HasValue)
                ? Math.Round(
                    ticket.Submissions.Sum(s => s.FinalScore ?? s.AiScore ?? 0m) / ticket.Submissions.Count,
                    2,
                    MidpointRounding.AwayFromZero)
                : null);

        var detailedScorecard = ticket.Submissions
            .OrderBy(s => s.SubmittedAt)
            .Select(sub =>
            {
                var latestEval = sub.AiEvaluations
                    .OrderByDescending(e => e.EvaluatedAt)
                    .FirstOrDefault();

                var criteriaScores = latestEval?.Details
                    .Select(d => new StudentOfficialExamCriterionScoreDto(
                        d.Criterion?.CriterionName ?? "Tiêu chí đánh giá",
                        d.Score,
                        d.Criterion?.MaxScore ?? 10m,
                        d.Comment))
                    .ToList() ?? new List<StudentOfficialExamCriterionScoreDto>();

                var questionScore = sub.FinalScore ?? sub.AiScore ?? latestEval?.TotalScore ?? 0m;
                var examQuestion = sub.ExamQuestion;
                if (examQuestion is null)
                {
                    questionMap.TryGetValue(sub.ExamQuestionId, out examQuestion);
                }

                return new StudentOfficialExamQuestionScorecardDto(
                    sub.ExamQuestionId,
                    questionScore,
                    criteriaScores,
                    latestEval?.Feedback,
                    null,
                    null,
                    examQuestion?.Title,
                    examQuestion?.Content,
                    examQuestion?.ImageUrl,
                    examQuestion?.ImageDescription,
                    sub.TranscriptWhisper,
                    ticket.LecturerAudit?.OverrideReason);
            })
            .ToList();

        var publishedAt = ticket.GradesPublishedAt;
        var hours = (window ?? new AppealWindowOptions()).Hours;
        var restriction = AppealWindowPolicy.Restriction(ticket, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, hours);
        var hasOpenAppeal = await _context.AppealRequests.AsNoTracking().AnyAsync(a => a.TicketId == ticket.Id &&
            (a.Status == AppealStatus.SubmittedToExamOffice ||
             a.Status == AppealStatus.AssignedToLecturer ||
             a.Status == AppealStatus.RegradedByLecturer), cancellationToken);
        if (hasOpenAppeal) restriction = "Đang có đơn phúc khảo được xử lý.";
        var canAcknowledge = publishedAt.HasValue && !hasOpenAppeal &&
            !ticket.StudentAcknowledgementStatus.Equals("ACKNOWLEDGED", StringComparison.OrdinalIgnoreCase);

        var publishedBy = ticket.LecturerAudit?.Lecturer?.Email
            ?? ticket.LecturerAudit?.Lecturer?.FullName
            ?? "lecturer@fpt.edu.vn";

        return Result<StudentOfficialExamGradeDto>.Success(new StudentOfficialExamGradeDto
        {
            TicketId = ticket.Id,
            StudentCode = ticket.Student?.StudentCode ?? string.Empty,
            FullName = ticket.Student?.FullName ?? string.Empty,
            CourseCode = ticket.Shift?.Session?.Course?.Code ?? string.Empty,
            Status = ticket.Status,
            IsLocked = ticket.IsLocked,
            TotalScore = totalScore,
            PublishedAt = publishedAt,
            PublishedBy = publishedBy,
            StudentAcknowledgementStatus = ticket.StudentAcknowledgementStatus ?? "PENDING",
            DetailedScorecard = detailedScorecard,
            InternalAppeal = new StudentInternalAppealInfoDto(
                restriction == null,
                restriction ?? "Bạn có thể gửi phúc khảo trước thời điểm hết hạn.",
                (int)Math.Ceiling(hours / 24d),
                "/api/v1/appeals",
                publishedAt?.AddHours(hours),
                canAcknowledge)
        });
    }
}

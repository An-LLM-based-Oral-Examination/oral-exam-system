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

namespace OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;

/// <summary>
/// Handler truy xuất kết quả điểm thi thật chính thức và Scorecard chi tiết theo CLO/Rubric cho sinh viên trên Student Portal (MF-04).
/// </summary>
public sealed class GetOfficialExamGradeQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetOfficialExamGradeQuery, Result<StudentOfficialExamGradeDto>>
{
    private readonly IApplicationDbContext _context = context;

    public async Task<Result<StudentOfficialExamGradeDto>> Handle(
        GetOfficialExamGradeQuery request,
        CancellationToken cancellationToken)
    {
        var ticketBase = await _context.StudentExamTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticketBase is null)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("NOT_FOUND: Vé thi không tồn tại trong hệ thống.");
        }

        if (ticketBase.StudentId != request.StudentId)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("FORBIDDEN: Bạn không có quyền xem điểm vé thi của sinh viên khác.");
        }

        bool isPublishedOrLocked = ticketBase.IsLocked
            || string.Equals(ticketBase.Status, ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ticketBase.Status, ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

        if (!isPublishedOrLocked)
        {
            return Result<StudentOfficialExamGradeDto>.Failure("UNPROCESSABLE: Điểm ca thi này chưa được giảng viên công bố.");
        }

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
            ?? ticketBase;

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
            ?? (ticket.Submissions.Any(s => s.FinalScore.HasValue)
                ? Math.Round(ticket.Submissions.Where(s => s.FinalScore.HasValue).Average(s => s.FinalScore!.Value), 2, MidpointRounding.AwayFromZero)
                : (ticket.Submissions.Any(s => s.AiScore.HasValue)
                    ? Math.Round(ticket.Submissions.Where(s => s.AiScore.HasValue).Average(s => s.AiScore!.Value), 2, MidpointRounding.AwayFromZero)
                    : null));

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
                questionMap.TryGetValue(sub.ExamQuestionId, out var examQuestion);

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
                    examQuestion?.ImageDescription);
            })
            .ToList();

        var publishedAt = ticket.LecturerAudit?.AuditedAt
            ?? ticket.SubmittedAt
            ?? ticket.CheckedInAt;

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
                true,
                "Nếu không đồng ý với kết quả, sinh viên có quyền nộp đơn phúc khảo trực tiếp trên cổng thông tin này.",
                3,
                "/api/v1/appeals")
        });
    }
}

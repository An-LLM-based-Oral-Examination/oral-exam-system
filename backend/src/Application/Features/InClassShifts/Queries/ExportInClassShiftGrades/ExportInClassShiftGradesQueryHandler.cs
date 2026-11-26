using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.InClassShifts.Queries.ExportInClassShiftGrades;

public sealed class ExportInClassShiftGradesQueryHandler(
    IApplicationDbContext context,
    IInClassShiftReportExporter exporter)
    : IRequestHandler<ExportInClassShiftGradesQuery, Result<InClassShiftExportFileDto>>
{
    public async Task<Result<InClassShiftExportFileDto>> Handle(
        ExportInClassShiftGradesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty)
        {
            return Result<InClassShiftExportFileDto>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var actor = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ActorUserId, cancellationToken);

        if (actor is null || !actor.IsActive || actor.Role != UserRole.Lecturer)
        {
            return Result<InClassShiftExportFileDto>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        var shift = await context.InClassExamShifts
            .AsNoTracking()
            .Include(s => s.Class)
                .ThenInclude(c => c.Course)
                    .ThenInclude(c => c.Semester)
            .Include(s => s.Class)
                .ThenInclude(c => c.CourseOffering)
                    .ThenInclude(o => o!.Semester)
            .Include(s => s.Lecturer)
            .Include(s => s.Participants)
                .ThenInclude(p => p.Student)
            .Include(s => s.Submissions)
                .ThenInclude(sub => sub.Student)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken);

        if (shift is null)
        {
            return Result<InClassShiftExportFileDto>.Failure(InClassShiftErrors.ShiftNotFound);
        }

        if (shift.Class.LecturerId != actor.Id)
        {
            return Result<InClassShiftExportFileDto>.Failure(InClassShiftErrors.LecturerNotAuthorized);
        }

        if (!InClassShiftAcademicContext.IsOperational(shift.Class))
        {
            return Result<InClassShiftExportFileDto>.Failure(InClassShiftErrors.ClassNotFound);
        }

        var activeEnrollments = await context.ClassEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.ClassId == shift.ClassId && (e.Status == "enrolled" || e.Status == "ACTIVE"))
            .ToListAsync(cancellationToken);

        var participantsByStudent = shift.Participants
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.JoinedAt).First());

        var submissionsByStudent = shift.Submissions
            .GroupBy(s => s.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var studentMap = new Dictionary<Guid, User>();
        foreach (var enrollment in activeEnrollments)
        {
            if (enrollment.Student is not null)
            {
                studentMap[enrollment.StudentId] = enrollment.Student;
            }
        }

        foreach (var participant in shift.Participants)
        {
            if (participant.Student is not null && !studentMap.ContainsKey(participant.StudentId))
            {
                studentMap[participant.StudentId] = participant.Student;
            }
        }

        foreach (var submission in shift.Submissions)
        {
            if (submission.Student is not null && !studentMap.ContainsKey(submission.StudentId))
            {
                studentMap[submission.StudentId] = submission.Student;
            }
        }

        var orderedStudents = studentMap.Values
            .OrderBy(u => u.StudentCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Id)
            .ToList();

        var utcNow = DateTime.UtcNow;
        var effectiveStatus = !shift.IsLocked && InClassShiftLifecycle.IsClosable(shift.Status) && shift.EndTime <= utcNow
            ? "CLOSED"
            : shift.Status;

        var rows = new List<InClassShiftGradeReportRow>(orderedStudents.Count);
        for (var i = 0; i < orderedStudents.Count; i++)
        {
            var student = orderedStudents[i];
            participantsByStudent.TryGetValue(student.Id, out var participant);
            submissionsByStudent.TryGetValue(student.Id, out var studentSubmissions);

            var status = participant?.Status ?? "NOT_JOINED";
            var isAbsentOrNotJoined = participant is null ||
                                      string.Equals(status, "ABSENT", StringComparison.OrdinalIgnoreCase);

            decimal? aiScore = null;
            decimal? finalScore = null;
            if (!isAbsentOrNotJoined)
            {
                aiScore = participant?.TotalAiScore;
                finalScore = participant?.TotalFinalScore;

                if (studentSubmissions is { Count: > 0 })
                {
                    if (!aiScore.HasValue)
                    {
                        var perQuestionAi = studentSubmissions
                            .GroupBy(s => s.QuestionId)
                            .Select(g => g.Where(s => s.AiScore.HasValue).Select(s => s.AiScore!.Value).ToList())
                            .Where(list => list.Count > 0)
                            .Select(list => list.Max())
                            .ToList();
                        if (perQuestionAi.Count > 0)
                        {
                            aiScore = Math.Round(perQuestionAi.Average(), 2, MidpointRounding.AwayFromZero);
                        }
                    }

                    if (!finalScore.HasValue)
                    {
                        var perQuestionFinal = studentSubmissions
                            .GroupBy(s => s.QuestionId)
                            .Select(g => g.Select(s => s.FinalScore ?? s.AiScore).Where(v => v.HasValue).Select(v => v!.Value).ToList())
                            .Where(list => list.Count > 0)
                            .Select(list => list.Max())
                            .ToList();
                        if (perQuestionFinal.Count > 0)
                        {
                            finalScore = Math.Round(perQuestionFinal.Average(), 2, MidpointRounding.AwayFromZero);
                        }
                    }
                }
            }

            var latestNotes = studentSubmissions?
                .Where(s => !string.IsNullOrWhiteSpace(s.LecturerNotes))
                .OrderByDescending(s => s.GradedAt ?? s.SubmittedAt)
                .Select(s => s.LecturerNotes!.Trim())
                .FirstOrDefault();

            rows.Add(new InClassShiftGradeReportRow(
                Index: i + 1,
                StudentCode: student.StudentCode ?? string.Empty,
                FullName: student.FullName,
                Email: student.Email,
                Status: status,
                AiScore: aiScore,
                FinalScore: finalScore,
                LecturerNotes: latestNotes,
                JoinedAtUtc: participant?.JoinedAt,
                FinishedAtUtc: participant?.FinishedAt));
        }

        var semesterCode = shift.Class.CourseOffering?.Semester?.Code
            ?? shift.Class.Course?.Semester?.Code
            ?? "N/A";

        var report = new InClassShiftGradeReport(
            ShiftId: shift.Id,
            SemesterCode: semesterCode,
            CourseCode: shift.Class.Course?.Code ?? string.Empty,
            CourseName: shift.Class.Course?.Name ?? string.Empty,
            ClassCode: shift.Class.Code,
            ClassName: shift.Class.Name,
            ShiftName: shift.ShiftName,
            LecturerName: shift.Lecturer?.FullName ?? actor.FullName,
            ShiftStatus: effectiveStatus,
            IsLocked: shift.IsLocked,
            StartTimeUtc: shift.StartTime,
            EndTimeUtc: shift.EndTime,
            ExportedAtUtc: utcNow,
            Rows: rows);

        var bytes = exporter.ExportExcel(report);
        var fileName = $"BangDiemLop_{Sanitize(shift.Class.Code)}_{Sanitize(shift.ShiftName)}_{shift.Id.ToString("N")[..8]}.xlsx";

        return Result<InClassShiftExportFileDto>.Success(new InClassShiftExportFileDto(
            FileContents: bytes,
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName: fileName));
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Report";
        }

        var result = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            result.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return result.Length == 0 ? "Report" : result.ToString();
    }
}

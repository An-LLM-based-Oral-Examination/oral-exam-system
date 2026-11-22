using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Commands.ImportSessionCandidates;

internal static class CandidateRosterImportMode
{
    public const string Replace = "REPLACE";
    public const string Append = "APPEND";

    public static bool TryNormalize(string? raw, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            normalized = Replace;
            return true;
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed is Replace or Append)
        {
            normalized = trimmed;
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}

internal sealed record CandidateRosterEvaluation(
    Guid SessionId,
    string ExamAttemptType,
    string Mode,
    CandidateRosterImportPreviewResponse Preview,
    IReadOnlyList<Guid> ValidStudentIds,
    IReadOnlyList<Guid> NetNewStudentIds);

internal static class CandidateRosterImportEvaluator
{
    public static async Task<Result<CandidateRosterEvaluation>> EvaluateAsync(
        IApplicationDbContext context,
        Guid sessionId,
        string? rawMode,
        IReadOnlyList<CandidateRosterImportRow>? rows,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            return Result<CandidateRosterEvaluation>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (!CandidateRosterImportMode.TryNormalize(rawMode, out var mode))
        {
            return Result<CandidateRosterEvaluation>.Failure(
                "BAD_REQUEST: Chế độ nhập danh sách (mode) chỉ chấp nhận REPLACE hoặc APPEND.");
        }

        if (rows is null || rows.Count == 0)
        {
            return Result<CandidateRosterEvaluation>.Failure(
                "BAD_REQUEST: Danh sách thí sinh không có dòng dữ liệu nào.");
        }

        if (rows.Count > OfficialExamRules.MaxCandidateRosterRows)
        {
            return Result<CandidateRosterEvaluation>.Failure(
                $"UNPROCESSABLE: Danh sách thí sinh vượt quá giới hạn tối đa {OfficialExamRules.MaxCandidateRosterRows} dòng.");
        }

        var session = await context.OfficialExamSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new
            {
                s.Id,
                s.CourseId,
                s.SemesterId,
                s.ExamAttemptType,
                s.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result<CandidateRosterEvaluation>.Failure($"NOT_FOUND: {OfficialExamErrors.SessionNotFound}");
        }

        if (string.Equals(session.Status, "concluded", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CandidateRosterEvaluation>.Failure(
                "CONFLICT: Không thể nhập hoặc thay đổi danh sách thí sinh cho kỳ thi đã kết thúc.");
        }

        var resolvedSemesterId = session.SemesterId;
        if (resolvedSemesterId == Guid.Empty)
        {
            resolvedSemesterId = await context.Courses
                .AsNoTracking()
                .Where(c => c.Id == session.CourseId)
                .Select(c => c.SemesterId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var sessionShiftIds = await context.RealExamSessionShifts
            .AsNoTracking()
            .Where(sh => sh.SessionId == session.Id)
            .Select(sh => sh.Id)
            .ToListAsync(cancellationToken);

        var hasAllocatedTickets = sessionShiftIds.Count > 0 && await context.StudentExamTickets
            .AsNoTracking()
            .AnyAsync(t => sessionShiftIds.Contains(t.ShiftId), cancellationToken);

        if (mode == CandidateRosterImportMode.Replace && hasAllocatedTickets)
        {
            return Result<CandidateRosterEvaluation>.Failure(
                "CONFLICT: Kỳ thi đã có sinh viên được phân bổ vé thi nên không thể dùng chế độ REPLACE. Hãy dùng chế độ APPEND để bổ sung thí sinh mới.");
        }

        var existingCandidateIds = (await context.OfficialExamCandidates
            .AsNoTracking()
            .Where(c => c.SessionId == session.Id)
            .Select(c => c.StudentId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var enrolledStudentIds = resolvedSemesterId != Guid.Empty
            ? (await OfficialExamSchedulingHelper.GetEligibleEnrolledStudentIdsAsync(
                context, session.CourseId, resolvedSemesterId, cancellationToken)).ToHashSet()
            : new HashSet<Guid>();

        var upperCodes = rows
            .Select(r => r.StudentCode?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var matchedUsers = upperCodes.Count == 0
            ? []
            : await context.Users
                .AsNoTracking()
                .Where(u => u.StudentCode != null && upperCodes.Contains(u.StudentCode.ToUpper()))
                .Select(u => new
                {
                    u.Id,
                    u.StudentCode,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.IsActive
                })
                .ToListAsync(cancellationToken);

        var userByCode = matchedUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.StudentCode))
            .GroupBy(u => u.StudentCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewRows = new List<CandidateRosterImportPreviewRowDto>(rows.Count);
        var issues = new List<CandidateRosterImportIssue>();
        var validStudentIds = new List<Guid>(rows.Count);
        var netNewStudentIds = new List<Guid>(rows.Count);

        var validCount = 0;
        var invalidCount = 0;
        var duplicateCount = 0;

        foreach (var row in rows)
        {
            var code = row.StudentCode?.Trim() ?? string.Empty;
            var rowIssues = new List<string>();

            if (string.IsNullOrWhiteSpace(code))
            {
                rowIssues.Add("MSSV không được để trống.");
            }
            else if (!seenCodesInFile.Add(code))
            {
                duplicateCount++;
                rowIssues.Add($"MSSV '{code}' bị lặp lại trong tệp tải lên.");
            }

            Guid? studentId = null;
            string? fullName = row.FullName;
            string? email = null;
            var isEnrolled = false;
            var alreadyInSession = false;

            if (rowIssues.Count == 0)
            {
                if (!userByCode.TryGetValue(code, out var user))
                {
                    rowIssues.Add($"Không tìm thấy tài khoản sinh viên có MSSV '{code}'.");
                }
                else
                {
                    studentId = user.Id;
                    fullName = user.FullName;
                    email = user.Email;
                    isEnrolled = enrolledStudentIds.Contains(user.Id);
                    alreadyInSession = existingCandidateIds.Contains(user.Id);

                    if (!user.IsActive)
                    {
                        rowIssues.Add($"Tài khoản sinh viên '{code}' đang bị khóa hoặc ngừng hoạt động.");
                    }
                    else if (!string.Equals(user.Role, UserRole.Student, StringComparison.Ordinal))
                    {
                        rowIssues.Add($"Tài khoản '{code}' không thuộc vai trò sinh viên.");
                    }
                }
            }

            var isValid = rowIssues.Count == 0;
            if (isValid && studentId.HasValue)
            {
                validCount++;
                validStudentIds.Add(studentId.Value);

                if (mode == CandidateRosterImportMode.Append && alreadyInSession)
                {
                    duplicateCount++;
                }
                else
                {
                    netNewStudentIds.Add(studentId.Value);
                }
            }
            else
            {
                invalidCount++;
                foreach (var msg in rowIssues)
                {
                    issues.Add(new CandidateRosterImportIssue(row.SourceRow, msg));
                }
            }

            previewRows.Add(new CandidateRosterImportPreviewRowDto(
                row.SourceRow,
                code,
                studentId,
                fullName,
                email,
                isEnrolled,
                alreadyInSession,
                isValid,
                rowIssues));
        }

        var existingCount = existingCandidateIds.Count;
        var projectedTotal = mode == CandidateRosterImportMode.Replace
            ? validStudentIds.Count
            : existingCount + netNewStudentIds.Count;
        var canCommit = invalidCount == 0 && rows.Count > 0;
        var recommendedTotalRooms = OfficialExamSchedulingHelper.ComputeRecommendedRoomCount(projectedTotal);

        var preview = new CandidateRosterImportPreviewResponse(
            session.Id,
            session.ExamAttemptType,
            mode,
            rows.Count,
            validCount,
            invalidCount,
            duplicateCount,
            canCommit,
            existingCount,
            projectedTotal,
            OfficialExamRules.DefaultShiftCapacity,
            recommendedTotalRooms,
            issues,
            previewRows);

        return Result<CandidateRosterEvaluation>.Success(new CandidateRosterEvaluation(
            session.Id,
            session.ExamAttemptType,
            mode,
            preview,
            validStudentIds,
            netNewStudentIds));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;

/// <summary>
/// Handler truy vấn danh sách ca thi phòng Lab (RealExamSessionShift).
/// Áp dụng fail-closed role scoping và ẩn CertificateHash với người không có quyền hậu kiểm/khảo thí.
/// </summary>
public sealed class GetExamShiftsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExamShiftsQuery, Result<List<ExamShiftResponse>>>
{
    private static readonly string[] AllowedRoles =
    [
        UserRole.ExaminationOfficer,
        UserRole.DepartmentHead,
        UserRole.Lecturer,
        UserRole.Proctor
    ];

    private readonly IApplicationDbContext _context = context;

    public async Task<Result<List<ExamShiftResponse>>> Handle(
        GetExamShiftsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ActorUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorRole))
        {
            return Result<List<ExamShiftResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var claimedRole = request.ActorRole.Trim();
        if (!AllowedRoles.Contains(claimedRole, StringComparer.OrdinalIgnoreCase))
        {
            return Result<List<ExamShiftResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.ActorUserId && u.IsActive)
            .Select(u => new { u.Id, u.Role })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null ||
            !AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(actor.Role, claimedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<ExamShiftResponse>>.Failure(OfficialExamErrors.Forbidden);
        }

        var query = _context.RealExamSessionShifts.AsNoTracking().AsQueryable();

        if (string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
        {
            var ownedCourseIds = _context.Courses
                .AsNoTracking()
                .Where(c =>
                    (c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId == actor.Id)
                    || ((c.AcademicProgram == null
                        || c.AcademicProgram.DepartmentHeadUserId == null
                        || c.AcademicProgram.DepartmentHeadUserId == Guid.Empty)
                        && c.DepartmentHeadUserId == actor.Id))
                .Select(c => c.Id);

            var ownedSessionIds = _context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => ownedCourseIds.Contains(s.CourseId))
                .Select(s => s.Id);

            query = query.Where(sh => ownedSessionIds.Contains(sh.SessionId));
        }
        else if (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(sh => sh.ExaminerUserId == actor.Id || sh.ProctorUserId == actor.Id);
        }
        else if (string.Equals(actor.Role, UserRole.Proctor, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(sh => sh.ProctorUserId == actor.Id);
        }

        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.SessionId == request.SessionId.Value);
        }

        if (request.ProctorUserId.HasValue && request.ProctorUserId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.ProctorUserId == request.ProctorUserId.Value);
        }

        if (request.ExaminerUserId.HasValue && request.ExaminerUserId.Value != Guid.Empty)
        {
            query = query.Where(sh => sh.ExaminerUserId == request.ExaminerUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var normalizedStatus = request.Status.Trim().ToLowerInvariant();
            query = query.Where(sh => sh.Status.ToLower() == normalizedStatus);
        }

        var rawShifts = await query
            .OrderByDescending(sh => sh.StartTime)
            .Select(sh => new
            {
                sh.Id,
                sh.SessionId,
                sh.ShiftName,
                sh.RoomLab,
                sh.StartTime,
                sh.EndTime,
                sh.ProctorUserId,
                sh.Status,
                sh.ExaminerUserId,
                ExaminerName = sh.ExaminerUser != null ? sh.ExaminerUser.FullName : null,
                ProctorName = sh.ProctorUser != null ? sh.ProctorUser.FullName : null,
                sh.MaxSeats,
                sh.IsExamPublished,
                IsGradesPublished = sh.StudentExamTickets.Any(t => t.Status == ExamTicketStatus.Published),
                GradesPublishedAt = sh.StudentExamTickets.Where(t => t.GradesPublishedAt != null).Max(t => t.GradesPublishedAt),
                sh.CertificateHash,
                AssignedTicketsCount = sh.StudentExamTickets.Count()
            })
            .ToListAsync(cancellationToken);

        var sessionIds = rawShifts.Select(s => s.SessionId).Distinct().ToList();
        var attemptTypeBySession = sessionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _context.OfficialExamSessions
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ExamAttemptType })
                .ToListAsync(cancellationToken))
                .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.ExamAttemptType) ? OfficialExamAttemptType.FinalExam : s.ExamAttemptType);

        var canAlwaysViewCertificateHash =
            string.Equals(actor.Role, UserRole.ExaminationOfficer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(actor.Role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase);

        var shifts = rawShifts.Select(sh =>
        {
            var attemptType = attemptTypeBySession.GetValueOrDefault(sh.SessionId, OfficialExamAttemptType.FinalExam);
            var remainingSeats = Math.Max(0, sh.MaxSeats - sh.AssignedTicketsCount);
            var canViewCertificateHash = canAlwaysViewCertificateHash
                || (string.Equals(actor.Role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase)
                    && sh.ExaminerUserId.HasValue
                    && sh.ExaminerUserId.Value == actor.Id);

            return new ExamShiftResponse(
                sh.Id,
                sh.SessionId,
                sh.ShiftName,
                sh.RoomLab,
                sh.StartTime,
                sh.EndTime,
                sh.ProctorUserId,
                sh.Status,
                sh.ExaminerUserId,
                sh.ExaminerName,
                sh.ProctorName,
                sh.MaxSeats,
                sh.IsExamPublished,
                sh.IsGradesPublished,
                sh.GradesPublishedAt,
                canViewCertificateHash ? sh.CertificateHash : null,
                attemptType,
                sh.AssignedTicketsCount,
                remainingSeats);
        }).ToList();

        return Result<List<ExamShiftResponse>>.Success(shifts);
    }
}

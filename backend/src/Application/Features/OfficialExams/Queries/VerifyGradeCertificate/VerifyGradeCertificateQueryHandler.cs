using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;

/// <summary>
/// Handler đối chiếu chữ ký số SHA-256 của biên bản điểm ca thi (BE-V02).
/// Chỉ trả về VALID khi mã băm khớp chính xác 100% với trạng thái dữ liệu điểm trong CSDL.
/// </summary>
public sealed class VerifyGradeCertificateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<VerifyGradeCertificateQuery, Result<GradeCertificateVerificationDto>>
{
    public async Task<Result<GradeCertificateVerificationDto>> Handle(
        VerifyGradeCertificateQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CertificateHash))
        {
            return Result<GradeCertificateVerificationDto>.Failure("Mã chứng thư số không hợp lệ hoặc để trống.");
        }

        var normalizedTargetHash = request.CertificateHash.Trim().ToLowerInvariant();
        if (normalizedTargetHash.Length != 64 || !normalizedTargetHash.All(Uri.IsHexDigit))
        {
            return Result<GradeCertificateVerificationDto>.Failure("Mã chứng thư số SHA-256 không đúng định dạng 64 ký tự thập lục phân.");
        }

        var shifts = await context.RealExamSessionShifts
            .AsNoTracking()
            .Include(s => s.Session)
                .ThenInclude(ss => ss.Course)
                    .ThenInclude(c => c.Semester)
            .Include(s => s.ProctorUser)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Student)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.LecturerAudit)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Submissions)
                    .ThenInclude(sub => sub.AiEvaluations)
            .ToListAsync(cancellationToken);

        foreach (var shift in shifts)
        {
            var rows = ExamShiftCertificateHashGenerator.BuildGradeRows(shift);
            var computedHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(shift, rows);

            if (string.Equals(computedHash, normalizedTargetHash, StringComparison.OrdinalIgnoreCase))
            {
                var lockedCount = rows.Count(r => r.IsLocked);
                var dto = new GradeCertificateVerificationDto
                {
                    Status = "VALID",
                    University = "Trường Đại học FPT - Phân hiệu TP.HCM",
                    DigitalSignatureIssuer = "FPTU Examination Board SHA-256 Cryptographic Seal",
                    CertificateHash = computedHash,
                    ShiftId = shift.Id,
                    ShiftName = shift.ShiftName,
                    RoomLab = shift.RoomLab,
                    SessionTitle = shift.Session?.Title ?? string.Empty,
                    CourseCode = shift.Session?.Course?.Code ?? string.Empty,
                    CourseName = shift.Session?.Course?.Name ?? string.Empty,
                    SemesterCode = shift.Session?.Course?.Semester?.Code ?? "FA26",
                    ExamDate = shift.Session?.ExamDate ?? DateOnly.FromDateTime(shift.StartTime),
                    ShiftStartTime = shift.StartTime,
                    ShiftEndTime = shift.EndTime,
                    ProctorName = shift.ProctorUser?.FullName ?? string.Empty,
                    TotalStudents = rows.Count,
                    LockedTicketsCount = lockedCount,
                    IsFullyLocked = rows.Count > 0 && lockedCount == rows.Count,
                    VerifiedAtUtc = DateTime.UtcNow,
                    StudentScores = rows.Select(r => new VerifiedStudentScoreSummaryDto
                    {
                        SeatNumber = r.SeatNumber,
                        StudentCode = r.StudentCode,
                        FullName = r.FullName,
                        FinalScore = r.FinalScore,
                        IsLocked = r.IsLocked
                    }).ToList()
                };

                return Result<GradeCertificateVerificationDto>.Success(dto);
            }
        }

        return Result<GradeCertificateVerificationDto>.Failure("Không tìm thấy chứng thư bảng điểm hợp lệ hoặc dữ liệu đã bị thay đổi so với chữ ký số gốc.");
    }
}

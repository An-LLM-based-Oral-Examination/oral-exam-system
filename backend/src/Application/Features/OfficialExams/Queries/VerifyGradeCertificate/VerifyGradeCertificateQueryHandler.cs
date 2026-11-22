using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.Common;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;

/// <summary>
/// Handler đối chiếu chữ ký số SHA-256 của biên bản điểm ca thi (BE-V02).
/// Truy vấn O(1) trực tiếp qua B-Tree index ix_real_exam_shifts_certificate_hash,
/// sau đó re-hash dữ liệu điểm trực tiếp (tamper-proof) bằng CryptographicOperations.FixedTimeEquals.
/// Chỉ trả về VALID khi mã băm khớp chính xác 100% với trạng thái dữ liệu điểm trong CSDL.
/// </summary>
public sealed class VerifyGradeCertificateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<VerifyGradeCertificateQuery, Result<GradeCertificateVerificationDto>>
{
    private const int LegacyFallbackLimit = 100;

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

        // Step 1: O(1) Index Seek on ix_real_exam_shifts_certificate_hash
        var indexedShift = await BuildShiftIncludeQuery()
            .Where(s => s.CertificateHash == normalizedTargetHash)
            .FirstOrDefaultAsync(cancellationToken);

        if (indexedShift is not null)
        {
            var rows = ExamShiftCertificateHashGenerator.BuildGradeRows(indexedShift);
            var recomputedHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(indexedShift, rows);

            if (!AreHashesEqualConstantTime(recomputedHash, normalizedTargetHash))
            {
                // Tamper detected: stored certificate_hash matched, but live DB scores no longer match!
                return Result<GradeCertificateVerificationDto>.Failure(
                    "Không tìm thấy chứng thư bảng điểm hợp lệ — phát hiện dữ liệu điểm đã bị thay đổi (TAMPERED) so với chữ ký số gốc.");
            }

            return Result<GradeCertificateVerificationDto>.Success(BuildDto(indexedShift, rows, recomputedHash, isTampered: false));
        }

        // Step 2: Bounded legacy fallback only for older completed shifts whose certificate_hash was not yet persisted
        var legacyShifts = await BuildShiftIncludeQuery()
            .Where(s => s.CertificateHash == null && s.Status == "completed")
            .OrderByDescending(s => s.EndTime)
            .Take(LegacyFallbackLimit)
            .ToListAsync(cancellationToken);

        foreach (var shift in legacyShifts)
        {
            var rows = ExamShiftCertificateHashGenerator.BuildGradeRows(shift);
            var computedHash = ExamShiftCertificateHashGenerator.ComputeCertificateHash(shift, rows);
            if (AreHashesEqualConstantTime(computedHash, normalizedTargetHash))
            {
                return Result<GradeCertificateVerificationDto>.Success(BuildDto(shift, rows, computedHash, isTampered: false));
            }
        }

        return Result<GradeCertificateVerificationDto>.Failure(
            "Không tìm thấy chứng thư bảng điểm hợp lệ hoặc dữ liệu đã bị thay đổi so với chữ ký số gốc.");
    }

    private IQueryable<RealExamSessionShift> BuildShiftIncludeQuery() =>
        context.RealExamSessionShifts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Session)
                .ThenInclude(ss => ss.Course)
                    .ThenInclude(c => c.Semester)
            .Include(s => s.Session)
                .ThenInclude(ss => ss.Semester)
            .Include(s => s.ProctorUser)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Student)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.LecturerAudit)
            .Include(s => s.StudentExamTickets)
                .ThenInclude(t => t.Submissions)
                    .ThenInclude(sub => sub.AiEvaluations);

    private static bool AreHashesEqualConstantTime(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left.Trim().ToLowerInvariant());
        var rightBytes = Encoding.ASCII.GetBytes(right.Trim().ToLowerInvariant());
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static GradeCertificateVerificationDto BuildDto(
        RealExamSessionShift shift,
        System.Collections.Generic.IReadOnlyList<ShiftGradeRowDto> rows,
        string verifiedHash,
        bool isTampered)
    {
        var lockedCount = rows.Count(r => r.IsLocked);
        return new GradeCertificateVerificationDto
        {
            Status = isTampered ? "TAMPERED" : "VALID",
            University = "Trường Đại học FPT - Phân hiệu TP.HCM",
            DigitalSignatureIssuer = !string.IsNullOrWhiteSpace(shift.CertificateThumbprint)
                ? "FPTU Examination Board PAdES-B-B X.509 & SHA-256 Cryptographic Seal"
                : "FPTU Examination Board SHA-256 Cryptographic Seal",
            CertificateHash = verifiedHash,
            CertificateThumbprint = shift.CertificateThumbprint,
            CertificateSignedAt = shift.CertificateSignedAt,
            IsTampered = isTampered,
            ShiftId = shift.Id,
            ShiftName = shift.ShiftName,
            RoomLab = shift.RoomLab,
            SessionTitle = shift.Session?.Title ?? string.Empty,
            CourseCode = shift.Session?.Course?.Code ?? string.Empty,
            CourseName = shift.Session?.Course?.Name ?? string.Empty,
            SemesterCode = shift.Session?.Semester?.Code ?? shift.Session?.Course?.Semester?.Code ?? "FA26",
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
    }
}

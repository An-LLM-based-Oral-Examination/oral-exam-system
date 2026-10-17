using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Queries.VerifyGradeCertificate;

/// <summary>
/// Query tra cứu & xác thực công khai mã băm niêm phong bảng điểm điện tử (QR Code Verification — BE-V02).
/// </summary>
public sealed record VerifyGradeCertificateQuery(string CertificateHash) : IRequest<Result<GradeCertificateVerificationDto>>;

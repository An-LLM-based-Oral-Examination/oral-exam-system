using System;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ ký số mật mã PDF chuẩn PAdES-B-B (/ETSI.CAdES.detached, ISO 32000-1) sử dụng chứng thư X.509.
/// </summary>
public interface IPdfDigitalSigner
{
    /// <summary>
    /// Trả về mã băm SHA-256 (64 ký tự hex thường) của chứng thư X.509 đang hoạt động.
    /// </summary>
    string GetActiveCertificateThumbprint();

    /// <summary>
    /// Trả về tên chủ thể (Subject / Common Name) của chứng thư ký số đang hoạt động.
    /// </summary>
    string GetSignerDisplayName();

    /// <summary>
    /// Ký số PDF dạng incremental update với chữ ký PKCS#7/CMS detached (/ETSI.CAdES.detached).
    /// </summary>
    PdfDigitalSignatureResult SignPdf(
        byte[] unsignedPdfBytes,
        string reason,
        string location,
        DateTime signedAtUtc);
}

public sealed record PdfDigitalSignatureResult(
    byte[] SignedPdfBytes,
    string CertificateThumbprint,
    string SignerName,
    DateTime SignedAtUtc);

using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Infrastructure.Reporting;

/// <summary>
/// Ký số PDF chuẩn PAdES-B-B (/SubFilter /ETSI.CAdES.detached) bằng System.Security.Cryptography.Pkcs (MIT)
/// qua cơ chế PDF incremental update (ISO 32000-1 §12.8).
/// Hỗ trợ nạp chứng thư .pfx qua cấu hình ENV (CertificatePath / CertificateBase64 + CertificatePassword);
/// Production fail-closed khi RequireCertificate = true mà thiếu chứng thư; Development sinh chứng thư RSA-2048 ephemeral trong RAM.
/// </summary>
public sealed partial class PadesPdfDigitalSigner : IPdfDigitalSigner, IDisposable
{
    private const int SignatureContentsHexLength = 16_384; // 8,192 bytes DER buffer
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";
    private const string ByteRangePlaceholder = "[0000000000 0000000000 0000000000 0000000000]";

    private readonly Lazy<X509Certificate2> _certificate;
    private readonly Lazy<string> _thumbprintSha256;
    private readonly Lazy<string> _signerDisplayName;

    [GeneratedRegex(@"startxref\s+(\d+)\s+%%EOF", RegexOptions.CultureInvariant)]
    private static partial Regex StartXrefRegex();

    [GeneratedRegex(@"/Size\s+(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex TrailerSizeRegex();

    [GeneratedRegex(@"/Root\s+(\d+)\s+(\d+)\s+R", RegexOptions.CultureInvariant)]
    private static partial Regex TrailerRootRegex();

    [GeneratedRegex(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.CultureInvariant)]
    private static partial Regex ByteRangeRegex();

    [GeneratedRegex(@"/Contents\s*<([0-9A-Fa-f]+)>", RegexOptions.CultureInvariant)]
    private static partial Regex ContentsHexRegex();

    [ActivatorUtilitiesConstructor]
    public PadesPdfDigitalSigner(IConfiguration? configuration = null)
        : this(configuration, static name => Environment.GetEnvironmentVariable(name))
    {
    }

    public PadesPdfDigitalSigner(IConfiguration? configuration, Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        _certificate = new Lazy<X509Certificate2>(
            () => LoadOrCreateCertificate(configuration, getEnvironmentVariable),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _thumbprintSha256 = new Lazy<string>(
            () => Convert.ToHexString(_certificate.Value.GetCertHash(HashAlgorithmName.SHA256)).ToLowerInvariant(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _signerDisplayName = new Lazy<string>(
            () => ExtractDisplayName(_certificate.Value),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal PadesPdfDigitalSigner(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        _certificate = new Lazy<X509Certificate2>(certificate);
        _thumbprintSha256 = new Lazy<string>(
            () => Convert.ToHexString(_certificate.Value.GetCertHash(HashAlgorithmName.SHA256)).ToLowerInvariant(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _signerDisplayName = new Lazy<string>(
            () => ExtractDisplayName(_certificate.Value),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string GetActiveCertificateThumbprint() => _thumbprintSha256.Value;

    public string GetSignerDisplayName() => _signerDisplayName.Value;

    public PdfDigitalSignatureResult SignPdf(
        byte[] unsignedPdfBytes,
        string reason,
        string location,
        DateTime signedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(unsignedPdfBytes);
        if (unsignedPdfBytes.Length < 16 || !Encoding.ASCII.GetString(unsignedPdfBytes, 0, 5).StartsWith("%PDF-", StringComparison.Ordinal))
        {
            throw new ArgumentException("Dữ liệu đầu vào không phải tệp PDF hợp lệ.", nameof(unsignedPdfBytes));
        }

        // Kích hoạt kiểm tra/nạp chứng thư PKI fail-closed ngay khi ký PDF.
        _ = _certificate.Value;

        var utcTime = signedAtUtc.Kind == DateTimeKind.Utc ? signedAtUtc : signedAtUtc.ToUniversalTime();
        var latin1Pdf = Encoding.Latin1.GetString(unsignedPdfBytes);

        var startXrefMatches = StartXrefRegex().Matches(latin1Pdf);
        if (startXrefMatches.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy bảng xref (startxref) trong tệp PDF nguồn.");
        }

        var prevStartXref = long.Parse(startXrefMatches[^1].Groups[1].Value, CultureInfo.InvariantCulture);
        var tailWindowStart = Math.Max(0, latin1Pdf.Length - Math.Min(latin1Pdf.Length, 4096));
        var tailText = latin1Pdf[tailWindowStart..];

        var sizeMatches = TrailerSizeRegex().Matches(tailText);
        var rootMatches = TrailerRootRegex().Matches(tailText);
        if (sizeMatches.Count == 0 || rootMatches.Count == 0)
        {
            sizeMatches = TrailerSizeRegex().Matches(latin1Pdf);
            rootMatches = TrailerRootRegex().Matches(latin1Pdf);
        }

        if (sizeMatches.Count == 0 || rootMatches.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy /Size hoặc /Root trong trailer PDF nguồn.");
        }

        var originalSize = int.Parse(sizeMatches[^1].Groups[1].Value, CultureInfo.InvariantCulture);
        var rootObjNum = int.Parse(rootMatches[^1].Groups[1].Value, CultureInfo.InvariantCulture);
        var rootGenNum = int.Parse(rootMatches[^1].Groups[2].Value, CultureInfo.InvariantCulture);

        var catalogInner = ExtractObjectDictionaryInner(latin1Pdf, rootObjNum, rootGenNum);
        var sigObjNum = originalSize;
        var sigFieldObjNum = originalSize + 1;
        var newSize = originalSize + 2;

        // Build incremental PDF update in memory with fixed-width ByteRange & Contents placeholders
        using var output = new MemoryStream(unsignedPdfBytes.Length + SignatureContentsHexLength + 4096);
        output.Write(unsignedPdfBytes, 0, unsignedPdfBytes.Length);
        if (unsignedPdfBytes[^1] != (byte)'\n')
        {
            output.WriteByte((byte)'\n');
        }

        // 1. Rewritten Catalog object pointing to /AcroForm with /SigFlags 3
        var rootOffset = output.Position;
        var updatedCatalogDict = BuildUpdatedCatalogDictionary(catalogInner, sigFieldObjNum);
        WriteAscii(output, $"{rootObjNum} {rootGenNum} obj\n{updatedCatalogDict}\nendobj\n");

        // 2. Invisible Signature Widget Field object (/FT /Sig)
        var sigFieldOffset = output.Position;
        WriteAscii(output,
            $"{sigFieldObjNum} 0 obj\n" +
            $"<< /Type /Annot /Subtype /Widget /FT /Sig /T (FPTU_Exam_Seal_Sig1) /Rect [0 0 0 0] /F 132 /V {sigObjNum} 0 R >>\n" +
            "endobj\n");

        // 3. Signature Value Dictionary (/SubFilter /ETSI.CAdES.detached — PAdES-B-B)
        var sigObjOffset = output.Position;
        var pdfDate = $"D:{utcTime:yyyyMMddHHmmss}Z";
        var signerDisplayName = GetSignerDisplayName();
        var thumbprintSha256 = GetActiveCertificateThumbprint();
        var escapedSigner = EscapePdfLiteral(signerDisplayName);
        var escapedReason = EscapePdfLiteral(string.IsNullOrWhiteSpace(reason) ? "FPTU Official Oral Exam Grade Seal" : reason);
        var escapedLocation = EscapePdfLiteral(string.IsNullOrWhiteSpace(location) ? "FPT University HCMC" : location);

        WriteAscii(output,
            $"{sigObjNum} 0 obj\n" +
            $"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /ETSI.CAdES.detached " +
            $"/Name ({escapedSigner}) /Reason ({escapedReason}) /Location ({escapedLocation}) /M ({pdfDate}) /ByteRange ");

        var byteRangePos = (int)output.Position;
        WriteAscii(output, ByteRangePlaceholder);
        WriteAscii(output, " /Contents ");

        var contentsOpenAnglePos = (int)output.Position;
        WriteAscii(output, "<");
        var contentsHexStartPos = (int)output.Position;
        WriteAscii(output, new string('0', SignatureContentsHexLength));
        var contentsCloseAnglePos = (int)output.Position;
        WriteAscii(output, "> >>\nendobj\n");

        // 4. Incremental cross-reference table (sorted subsections) + trailer + startxref + %%EOF
        var newXrefOffset = output.Position;
        WriteAscii(output, "xref\n");
        WriteAscii(output, $"{rootObjNum} 1\n");
        WriteAscii(output, $"{rootOffset:D10} {rootGenNum:D5} n \n");
        WriteAscii(output, $"{sigObjNum} 2\n");
        WriteAscii(output, $"{sigObjOffset:D10} 00000 n \n");
        WriteAscii(output, $"{sigFieldOffset:D10} 00000 n \n");
        WriteAscii(output,
            $"trailer\n<< /Size {newSize} /Root {rootObjNum} {rootGenNum} R /Prev {prevStartXref} >>\n" +
            $"startxref\n{newXrefOffset}\n%%EOF\n");

        var pdfBuffer = output.ToArray();

        // 5. Patch exact /ByteRange in place (identical byte length -> offsets stay invariant)
        var part1Start = 0;
        var part1Length = contentsOpenAnglePos;
        var part2Start = contentsCloseAnglePos + 1;
        var part2Length = pdfBuffer.Length - part2Start;

        var actualByteRange = $"[{part1Start:D10} {part1Length:D10} {part2Start:D10} {part2Length:D10}]";
        if (actualByteRange.Length != ByteRangePlaceholder.Length)
        {
            throw new InvalidOperationException("Độ dài /ByteRange vượt quá không gian dự phòng PDF.");
        }

        Encoding.ASCII.GetBytes(actualByteRange, 0, actualByteRange.Length, pdfBuffer, byteRangePos);

        // 6. Compute detached PAdES-B-B CMS signature over ByteRange1 + ByteRange2
        var signedPayload = new byte[part1Length + part2Length];
        Buffer.BlockCopy(pdfBuffer, part1Start, signedPayload, 0, part1Length);
        Buffer.BlockCopy(pdfBuffer, part2Start, signedPayload, part1Length, part2Length);

        var cmsDer = ComputeDetachedCadesSignature(signedPayload, utcTime);
        var cmsHex = Convert.ToHexString(cmsDer);
        if (cmsHex.Length > SignatureContentsHexLength)
        {
            throw new InvalidOperationException(
                $"Chữ ký CMS ({cmsDer.Length} bytes) vượt quá kích thước vùng /Contents ({SignatureContentsHexLength / 2} bytes).");
        }

        Encoding.ASCII.GetBytes(cmsHex, 0, cmsHex.Length, pdfBuffer, contentsHexStartPos);

        return new PdfDigitalSignatureResult(pdfBuffer, thumbprintSha256, signerDisplayName, utcTime);
    }

    /// <summary>
    /// Xác minh chữ ký mật mã PAdES-B-B nhúng trong tệp PDF (trích /ByteRange &amp; /Contents và chạy SignedCms.CheckSignature(true)).
    /// </summary>
    public static PdfSignatureVerificationOutcome VerifyEmbeddedSignature(byte[] signedPdfBytes)
    {
        if (signedPdfBytes is null || signedPdfBytes.Length == 0)
        {
            return new PdfSignatureVerificationOutcome(false, null, null, "PDF rỗng.");
        }

        var latin1 = Encoding.Latin1.GetString(signedPdfBytes);
        var byteRangeMatches = ByteRangeRegex().Matches(latin1);
        var contentsMatches = ContentsHexRegex().Matches(latin1);
        if (byteRangeMatches.Count == 0 || contentsMatches.Count == 0)
        {
            return new PdfSignatureVerificationOutcome(false, null, null, "Không tìm thấy chữ ký số PAdES nhúng trong PDF.");
        }

        var br = byteRangeMatches[^1];
        var a = int.Parse(br.Groups[1].Value, CultureInfo.InvariantCulture);
        var b = int.Parse(br.Groups[2].Value, CultureInfo.InvariantCulture);
        var c = int.Parse(br.Groups[3].Value, CultureInfo.InvariantCulture);
        var d = int.Parse(br.Groups[4].Value, CultureInfo.InvariantCulture);

        if (a < 0 || b < 0 || c < a + b || d < 0 || c + d > signedPdfBytes.Length)
        {
            return new PdfSignatureVerificationOutcome(false, null, null, "Giá trị /ByteRange trong PDF không hợp lệ.");
        }

        var rawHex = contentsMatches[^1].Groups[1].Value;
        var rawBytes = Convert.FromHexString(rawHex);
        var derLength = ReadDerSequenceLength(rawBytes);
        var cmsDer = derLength > 0 && derLength <= rawBytes.Length
            ? rawBytes[..derLength]
            : rawBytes;

        var signedContent = new byte[b + d];
        Buffer.BlockCopy(signedPdfBytes, a, signedContent, 0, b);
        Buffer.BlockCopy(signedPdfBytes, c, signedContent, b, d);

        try
        {
            var signedCms = new SignedCms(new ContentInfo(signedContent), detached: true);
            signedCms.Decode(cmsDer);
            signedCms.CheckSignature(verifySignatureOnly: true);

            var signerCert = signedCms.SignerInfos.Count > 0 ? signedCms.SignerInfos[0].Certificate : null;
            var thumbprint = signerCert is not null
                ? Convert.ToHexString(signerCert.GetCertHash(HashAlgorithmName.SHA256)).ToLowerInvariant()
                : null;
            var subject = signerCert is not null ? ExtractDisplayName(signerCert) : null;

            return new PdfSignatureVerificationOutcome(true, thumbprint, subject, null);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return new PdfSignatureVerificationOutcome(false, null, null, ex.Message);
        }
    }

    private byte[] ComputeDetachedCadesSignature(byte[] payload, DateTime signedAtUtc)
    {
        var certificate = _certificate.Value;
        var contentInfo = new ContentInfo(payload);
        var signedCms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            DigestAlgorithm = new Oid(Sha256Oid),
            IncludeOption = X509IncludeOption.EndCertOnly
        };

        signer.SignedAttributes.Add(new Pkcs9SigningTime(signedAtUtc));
        signer.SignedAttributes.Add(new AsnEncodedData(
            new Oid(SigningCertificateV2Oid),
            BuildSigningCertificateV2Attribute(certificate)));

        signedCms.ComputeSignature(signer);
        return signedCms.Encode();
    }

    /// <summary>
    /// Mã hóa ASN.1 DER cho thuộc tính RFC 5035 / PAdES-B-B SigningCertificateV2 (id-aa-signingCertificateV2).
    /// SigningCertificateV2 ::= SEQUENCE { certs SEQUENCE OF ESSCertIDv2 }
    /// ESSCertIDv2 ::= SEQUENCE { certHash OCTET STRING } (default hashAlgorithm = SHA-256 per RFC 5035 §4)
    /// </summary>
    private static byte[] BuildSigningCertificateV2Attribute(X509Certificate2 cert)
    {
        var certSha256 = cert.GetCertHash(HashAlgorithmName.SHA256);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) // SigningCertificateV2
        using (writer.PushSequence()) // certs SEQUENCE OF ESSCertIDv2
        using (writer.PushSequence()) // ESSCertIDv2
        {
            writer.WriteOctetString(certSha256);
        }
        return writer.Encode();
    }

    private static int ReadDerSequenceLength(byte[] der)
    {
        if (der.Length < 2 || der[0] != 0x30)
        {
            return der.Length;
        }

        var lenByte = der[1];
        if ((lenByte & 0x80) == 0)
        {
            return 2 + lenByte;
        }

        var numLengthBytes = lenByte & 0x7F;
        if (numLengthBytes is <= 0 or > 4 || 2 + numLengthBytes > der.Length)
        {
            return der.Length;
        }

        var contentLength = 0;
        for (var i = 0; i < numLengthBytes; i++)
        {
            contentLength = (contentLength << 8) | der[2 + i];
        }

        return 2 + numLengthBytes + contentLength;
    }

    private static string ExtractObjectDictionaryInner(string latin1Pdf, int objNum, int genNum)
    {
        var pattern = $@"(?:^|\r|\n){objNum}\s+{genNum}\s+obj\s*<<([\s\S]*?)>>\s*endobj";
        var match = Regex.Match(latin1Pdf, pattern, RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : "/Type /Catalog";
    }

    private static string BuildUpdatedCatalogDictionary(string existingInner, int sigFieldObjNum)
    {
        var cleaned = Regex.Replace(
            existingInner,
            @"/AcroForm\s*(?:<<[\s\S]*?>>|\d+\s+\d+\s+R)",
            string.Empty,
            RegexOptions.CultureInvariant).Trim();
        return $"<< {cleaned} /AcroForm << /Fields [{sigFieldObjNum} 0 R] /SigFlags 3 >> >>";
    }

    private static string EscapePdfLiteral(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '(' or ')' or '\\')
            {
                sb.Append('\\');
            }

            sb.Append(ch <= 126 && ch >= 32 ? ch : '?');
        }
        return sb.ToString();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ExtractDisplayName(X509Certificate2 cert)
    {
        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
        return string.IsNullOrWhiteSpace(cn) ? cert.Subject : cn;
    }

    private static X509Certificate2 LoadOrCreateCertificate(
        IConfiguration? configuration,
        Func<string, string?> getEnvironmentVariable)
    {
        var envName = FirstNonWhiteSpace(
            configuration?["ASPNETCORE_ENVIRONMENT"],
            getEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))?.Trim()
            ?? "Development";
        var isProduction = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);

        var requireCertRaw = FirstNonWhiteSpace(
            configuration?["PdfSigning:RequireCertificate"],
            getEnvironmentVariable("PDF_SIGNING_REQUIRE_CERTIFICATE"))?.Trim();
        var requireCertificate = bool.TryParse(requireCertRaw, out var parsedRequire)
            ? parsedRequire
            : isProduction;

        var certBase64 = FirstNonWhiteSpace(
            configuration?["PdfSigning:CertificateBase64"],
            getEnvironmentVariable("PDF_SIGNING_CERTIFICATE_BASE64"));
        var certPath = FirstNonWhiteSpace(
            configuration?["PdfSigning:CertificatePath"],
            getEnvironmentVariable("PDF_SIGNING_CERTIFICATE_PATH"));
        var certPassword = FirstNonWhiteSpace(
            configuration?["PdfSigning:CertificatePassword"],
            getEnvironmentVariable("PDF_SIGNING_CERTIFICATE_PASSWORD"));

        const X509KeyStorageFlags storageFlags = X509KeyStorageFlags.EphemeralKeySet;

        if (!string.IsNullOrWhiteSpace(certBase64))
        {
            var rawPfx = Convert.FromBase64String(certBase64.Trim());
            return ValidateLoadedCertificate(new X509Certificate2(rawPfx, certPassword, storageFlags));
        }

        if (!string.IsNullOrWhiteSpace(certPath))
        {
            var trimmedPath = certPath.Trim();
            if (!File.Exists(trimmedPath))
            {
                throw new FileNotFoundException(
                    $"Không tìm thấy chứng thư ký số PDF tại đường dẫn '{trimmedPath}'.",
                    trimmedPath);
            }

            return ValidateLoadedCertificate(new X509Certificate2(trimmedPath, certPassword, storageFlags));
        }

        if (requireCertificate)
        {
            throw new InvalidOperationException(
                "[FAIL CLOSED] Thiếu chứng thư ký số PDF PKI (PdfSigning:CertificatePath hoặc PdfSigning:CertificateBase64) trong môi trường Production.");
        }

        // Development / Test ephemeral RSA-2048 X.509 certificate in RAM (zero disk secrets)
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=FPTU Oral Examination Board (Ephemeral), O=FPT University HCMC, C=VN",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
            critical: true));

        var now = DateTimeOffset.UtcNow;
        using var created = req.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(2));
        var pfxBytes = created.Export(X509ContentType.Pfx);
        return new X509Certificate2(pfxBytes, (string?)null, storageFlags);
    }

    private static X509Certificate2 ValidateLoadedCertificate(X509Certificate2 cert)
    {
        if (!cert.HasPrivateKey)
        {
            cert.Dispose();
            throw new InvalidOperationException(
                "[FAIL CLOSED] Chứng thư ký số PDF PKI không chứa khóa bí mật (Private Key).");
        }

        if (cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
        {
            var notAfterUtc = cert.NotAfter.ToUniversalTime();
            cert.Dispose();
            throw new InvalidOperationException(
                $"[FAIL CLOSED] Chứng thư ký số PDF PKI đã hết hạn vào {notAfterUtc:yyyy-MM-dd HH:mm:ss}Z.");
        }

        return cert;
    }

    private static string? FirstNonWhiteSpace(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_certificate.IsValueCreated)
        {
            _certificate.Value.Dispose();
        }
    }
}

public sealed record PdfSignatureVerificationOutcome(
    bool IsValid,
    string? CertificateThumbprint,
    string? SignerName,
    string? Error);

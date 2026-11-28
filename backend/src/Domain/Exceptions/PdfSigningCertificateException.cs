using System;

namespace OralExamination.Domain.Exceptions;

public sealed class PdfSigningCertificateException : InvalidOperationException
{
    public PdfSigningCertificateException(string message) : base(message)
    {
    }

    public PdfSigningCertificateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

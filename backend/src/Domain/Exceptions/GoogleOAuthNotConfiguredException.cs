using System;

namespace OralExamination.Domain.Exceptions;

public sealed class GoogleOAuthNotConfiguredException : InvalidOperationException
{
    public GoogleOAuthNotConfiguredException(string message) : base(message)
    {
    }

    public GoogleOAuthNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

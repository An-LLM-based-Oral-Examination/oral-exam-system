using System;

namespace OralExamination.Domain.Exceptions;

public class OneWayLockException : Exception
{
    public OneWayLockException(string message) : base(message)
    {
    }
}

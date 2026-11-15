using OralExamination.Application.Common.Interfaces;

namespace OralExamination.UnitTests.Features.Auth;

// Fixed nonce only for direct handler fixtures. HTTP tests exercise the production random/atomic registry.
internal sealed class AuthTestNonceStore : IGoogleLoginNonceStore
{
    public const string Value = "unit-test-nonce-0000000000000000000000000000";
    private int _consumed;
    public string Issue() => Value;
    public bool TryConsume(string nonce) => nonce == Value && Interlocked.Exchange(ref _consumed, 1) == 0;
}

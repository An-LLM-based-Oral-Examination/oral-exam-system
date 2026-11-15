using FluentAssertions;
using OralExamination.Application.Common.Auth;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class AbuseProtectionTests
{
    [Fact]
    public void FailedAttempts_LockTemporarily_ThenExpire()
    {
        var clock = new TestClock();
        var guard = new LoginAttemptGuard(new LoginProtectionOptions { FailedAttemptLimit = 3, LockoutSeconds = 60 }, clock);
        var user = Guid.NewGuid();
        guard.RecordFailure(user);
        guard.RecordFailure(user);
        guard.RemainingLockout(user).Should().Be(TimeSpan.Zero);
        guard.RecordFailure(user);
        guard.RemainingLockout(user).Should().Be(TimeSpan.FromSeconds(60));
        clock.Now += TimeSpan.FromSeconds(60);
        guard.RemainingLockout(user).Should().Be(TimeSpan.Zero);
        guard.RecordFailure(user);
        guard.RemainingLockout(user).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void SuccessAndWindowExpiry_ResetOnlyThatAccount()
    {
        var clock = new TestClock();
        var guard = new LoginAttemptGuard(new LoginProtectionOptions { FailedAttemptLimit = 2, FailureWindowSeconds = 10 }, clock);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        guard.RecordFailure(first); guard.RecordFailure(second); guard.RecordFailure(second);
        guard.RecordSuccess(first);
        guard.RecordFailure(first);
        guard.RemainingLockout(first).Should().Be(TimeSpan.Zero);
        guard.RemainingLockout(second).Should().BeGreaterThan(TimeSpan.Zero);
        clock.Now += TimeSpan.FromSeconds(11);
        guard.RecordFailure(first);
        guard.RemainingLockout(first).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ParallelFailures_AreCountedWithoutLosingUpdates()
    {
        var guard = new LoginAttemptGuard(new LoginProtectionOptions { FailedAttemptLimit = 5 });
        var user = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(() => guard.RecordFailure(user))));
        guard.RemainingLockout(user).Should().BeGreaterThan(TimeSpan.Zero);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 14, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

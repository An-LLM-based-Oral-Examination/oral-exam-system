using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace OralExamination.Application.Common.Auth;

public sealed class LoginProtectionOptions
{
    public int FailedAttemptLimit { get; set; } = 5;
    public int FailureWindowSeconds { get; set; } = 900;
    public int LockoutSeconds { get; set; } = 900;
}

public interface ILoginAttemptGuard
{
    TimeSpan RemainingLockout(Guid userId);
    bool RecordFailure(Guid userId);
    void RecordSuccess(Guid userId);
    long Lockouts { get; }
}

/// <summary>Thread-safe, expiring account guard shared across requests in one API process.</summary>
public sealed class LoginAttemptGuard : ILoginAttemptGuard
{
    private sealed class Entry { public DateTimeOffset Window; public DateTimeOffset Until; public int Failures; }
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly LoginProtectionOptions _options;
    private readonly TimeProvider _clock;
    private long _lockouts;
    private long _operations;
    private static readonly Meter Meter = new("OralExamination.Authentication", "1.0");
    private static readonly Counter<long> LockoutCounter = Meter.CreateCounter<long>("auth_lockouts_total");
    public static LoginAttemptGuard Shared { get; } = new();
    public long Lockouts => Interlocked.Read(ref _lockouts);

    public LoginAttemptGuard(LoginProtectionOptions? options = null, TimeProvider? clock = null)
    {
        _options = options ?? new LoginProtectionOptions();
        if (_options.FailedAttemptLimit < 1 || _options.FailureWindowSeconds < 1 || _options.LockoutSeconds < 1)
            throw new ArgumentException("Login protection options must be positive.");
        _clock = clock ?? TimeProvider.System;
    }

    public TimeSpan RemainingLockout(Guid userId)
    {
        if (!_entries.TryGetValue(userId, out var entry)) return TimeSpan.Zero;
        lock (entry)
        {
            var now = _clock.GetUtcNow();
            if (entry.Until > now) return entry.Until - now;
            if (entry.Until != default) { entry.Failures = 0; entry.Until = default; entry.Window = now; }
            return TimeSpan.Zero;
        }
    }

    public bool RecordFailure(Guid userId)
    {
        var now = _clock.GetUtcNow();
        var entry = _entries.GetOrAdd(userId, _ => new Entry { Window = now });
        bool locked = false;
        lock (entry)
        {
            if (entry.Until > now) return false;
            if (entry.Until != default || now - entry.Window >= TimeSpan.FromSeconds(_options.FailureWindowSeconds))
            { entry.Failures = 0; entry.Until = default; entry.Window = now; }
            if (++entry.Failures >= _options.FailedAttemptLimit)
            {
                entry.Until = now.AddSeconds(_options.LockoutSeconds);
                Interlocked.Increment(ref _lockouts);
                LockoutCounter.Add(1);
                locked = true;
            }
        }
        if (Interlocked.Increment(ref _operations) % 256 == 0)
            foreach (var item in _entries)
                lock (item.Value)
                    if (item.Value.Until <= now && now - item.Value.Window > TimeSpan.FromSeconds(_options.FailureWindowSeconds + _options.LockoutSeconds))
                        _entries.TryRemove(item.Key, out _);
        return locked;
    }

    public void RecordSuccess(Guid userId) => _entries.TryRemove(userId, out _);
}

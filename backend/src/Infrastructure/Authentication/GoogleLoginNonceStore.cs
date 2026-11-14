using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Exceptions;

namespace OralExamination.Infrastructure.Authentication;

/// <summary>Bounded single-use registry. Restart invalidates pending challenges, never accepts them.</summary>
public sealed class GoogleLoginNonceStore(TimeProvider? clock = null, int capacity = 10000) : IGoogleLoginNonceStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pending = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _issueLock = new();

    public string Issue()
    {
        lock (_issueLock)
        {
            var now = _clock.GetUtcNow();
            foreach (var entry in _pending)
                if (entry.Value <= now) _pending.TryRemove(entry.Key, out _);
            if (_pending.Count >= capacity)
                throw new QuotaExceededException("Đăng nhập Google đang bận. Vui lòng thử lại sau.");
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            _pending[Hash(nonce)] = now.AddSeconds(GoogleLoginRules.NonceLifetimeSeconds);
            return nonce;
        }
    }

    public bool TryConsume(string nonce) => !string.IsNullOrWhiteSpace(nonce) && nonce.Length <= 128
        && _pending.TryRemove(Hash(nonce), out var expires) && expires > _clock.GetUtcNow();

    private static string Hash(string nonce) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));
}

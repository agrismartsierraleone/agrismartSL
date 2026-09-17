using System.Security.Cryptography;
using AgrismartAPI.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace AgrismartAPI.Infrastructure.Auth;

/// <summary>In-memory store for biometric login challenges. Fine for a single-instance API;
/// swap for a distributed cache (Redis, etc.) if the API is scaled out horizontally.</summary>
public class BiometricChallengeStore(IMemoryCache cache) : IBiometricChallengeStore
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);

    public string Issue(Guid userId, string deviceId)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        cache.Set(Key(userId, deviceId), challenge, ChallengeLifetime);
        return Convert.ToBase64String(challenge);
    }

    public bool TryConsume(Guid userId, string deviceId, out byte[] challenge)
    {
        var key = Key(userId, deviceId);
        if (cache.TryGetValue(key, out byte[]? stored) && stored is not null)
        {
            cache.Remove(key);
            challenge = stored;
            return true;
        }

        challenge = [];
        return false;
    }

    private static string Key(Guid userId, string deviceId) => $"biometric-challenge:{userId}:{deviceId}";
}

using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Users;

/// <summary>
/// A device-bound public key used for biometric login. The private key never leaves the
/// device's secure hardware (Keystore/Secure Enclave) — the biometric prompt only unlocks it
/// to sign a server-issued challenge.
/// </summary>
public class BiometricCredential : Entity
{
    public Guid UserId { get; private init; }
    public string DeviceId { get; private init; }
    public string PublicKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    private BiometricCredential(Guid userId, string deviceId, string publicKey)
    {
        UserId = userId;
        DeviceId = deviceId;
        PublicKey = publicKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private BiometricCredential()
    {
        DeviceId = null!;
        PublicKey = null!;
    }

    public static BiometricCredential Create(Guid userId, string deviceId, string publicKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ArgumentException("Public key is required.", nameof(publicKey));
        }

        return new BiometricCredential(userId, deviceId.Trim(), publicKey.Trim());
    }

    public void ReplaceKey(string publicKey) => PublicKey = publicKey.Trim();

    public void MarkUsed() => LastUsedAt = DateTimeOffset.UtcNow;
}

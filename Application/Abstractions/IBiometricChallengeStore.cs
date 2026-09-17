namespace AgrismartAPI.Application.Abstractions;

/// <summary>Short-lived, single-use nonces handed out by <c>POST /auth/biometric/challenge</c>.</summary>
public interface IBiometricChallengeStore
{
    /// <summary>Issues a fresh challenge for the (user, device) pair and returns it base64-encoded.</summary>
    string Issue(Guid userId, string deviceId);

    /// <summary>Consumes the pending challenge for the (user, device) pair, if any. Single use.</summary>
    bool TryConsume(Guid userId, string deviceId, out byte[] challenge);
}

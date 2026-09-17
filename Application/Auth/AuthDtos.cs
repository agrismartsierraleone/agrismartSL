using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Auth;

public record RegisterRequest(string Name, string Email, string Password, UserRole Role);

public record LoginRequest(string Email, string Password);

/// <summary>When <paramref name="MustChangePassword"/> is true, the frontend should route straight to a "set a new password" screen after login.</summary>
public record UserDto(Guid Id, string Name, string Email, UserRole Role, bool MustChangePassword);

public record AuthResponse(string Token, UserDto User);

/// <summary>Self-service password change — used both for a voluntary change and for the forced first-login reset of an admin-issued temporary password.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Registers (or replaces) the calling device's biometric public key. Sent once, right after a normal password login, with the JWT that login returned.</summary>
public record BiometricRegisterRequest(string DeviceId, string PublicKey);

/// <summary>Requests a one-time challenge to sign for biometric login. No auth required — this is how a signed-out device starts the flow.</summary>
public record BiometricChallengeRequest(string Email, string DeviceId);

public record BiometricChallengeResponse(string Challenge);

/// <summary>Completes biometric login by returning the challenge signed with the device's private key.</summary>
public record BiometricLoginRequest(string Email, string DeviceId, string Signature);

using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Auth;

public record RegisterRequest(string Name, string Email, string Password, UserRole Role);

public record LoginRequest(string Email, string Password);

public record UserDto(Guid Id, string Name, string Email, UserRole Role);

public record AuthResponse(string Token, UserDto User);

/// <summary>Registers (or replaces) the calling device's biometric public key. Sent once, right after a normal password login, with the JWT that login returned.</summary>
public record BiometricRegisterRequest(string DeviceId, string PublicKey);

/// <summary>Requests a one-time challenge to sign for biometric login. No auth required — this is how a signed-out device starts the flow.</summary>
public record BiometricChallengeRequest(string Email, string DeviceId);

public record BiometricChallengeResponse(string Challenge);

/// <summary>Completes biometric login by returning the challenge signed with the device's private key.</summary>
public record BiometricLoginRequest(string Email, string DeviceId, string Signature);

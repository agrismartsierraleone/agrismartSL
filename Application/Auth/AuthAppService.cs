using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Auth;

public class AuthAppService(
    IUserRepository userRepository,
    ISellerRepository sellerRepository,
    IBiometricCredentialRepository biometricCredentialRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IBiometricChallengeStore biometricChallengeStore,
    IBiometricSignatureVerifier biometricSignatureVerifier)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters long.");
        }

        if (request.Role == UserRole.Admin)
        {
            throw new ValidationException("Admin accounts cannot be self-registered.");
        }

        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Register(request.Name, request.Email, passwordHasher.Hash(request.Password), request.Role);
        await userRepository.AddAsync(user, cancellationToken);

        if (user.Role == UserRole.Seller)
        {
            await sellerRepository.AddAsync(Seller.Create(user.Name, user.Email, null, null, user.Id), cancellationToken);
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid email or password.");
        }

        return BuildAuthResponse(user);
    }

    /// <summary>Registers, or replaces, the calling device's biometric public key for the authenticated user.</summary>
    public async Task RegisterBiometricAsync(Guid userId, BiometricRegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ValidationException("Device id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PublicKey))
        {
            throw new ValidationException("Public key is required.");
        }

        var existing = await biometricCredentialRepository.GetAsync(userId, request.DeviceId, cancellationToken);
        if (existing is null)
        {
            await biometricCredentialRepository.AddAsync(BiometricCredential.Create(userId, request.DeviceId, request.PublicKey), cancellationToken);
        }
        else
        {
            existing.ReplaceKey(request.PublicKey);
            await biometricCredentialRepository.UpdateAsync(existing, cancellationToken);
        }
    }

    /// <summary>Turns off biometric login for the calling device.</summary>
    public async Task UnregisterBiometricAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        var credential = await biometricCredentialRepository.GetAsync(userId, deviceId, cancellationToken);
        if (credential is not null)
        {
            await biometricCredentialRepository.RemoveAsync(credential, cancellationToken);
        }
    }

    /// <summary>Issues a one-time challenge for a device that already registered a biometric key.</summary>
    public async Task<BiometricChallengeResponse> RequestBiometricChallengeAsync(BiometricChallengeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        var credential = user is null
            ? null
            : await biometricCredentialRepository.GetAsync(user.Id, request.DeviceId, cancellationToken);

        if (user is null || credential is null)
        {
            throw new ValidationException("Biometric login is not set up for this account on this device.");
        }

        var challenge = biometricChallengeStore.Issue(user.Id, request.DeviceId);
        return new BiometricChallengeResponse(challenge);
    }

    /// <summary>Completes biometric login: verifies the signed challenge and issues a normal JWT.</summary>
    public async Task<AuthResponse> LoginWithBiometricAsync(BiometricLoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        var credential = user is null
            ? null
            : await biometricCredentialRepository.GetAsync(user.Id, request.DeviceId, cancellationToken);

        if (user is null
            || credential is null
            || !biometricChallengeStore.TryConsume(user.Id, request.DeviceId, out var challenge)
            || !biometricSignatureVerifier.Verify(credential.PublicKey, challenge, request.Signature))
        {
            throw new ValidationException("Biometric login failed.");
        }

        credential.MarkUsed();
        await biometricCredentialRepository.UpdateAsync(credential, cancellationToken);

        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user) => new(
        jwtTokenGenerator.GenerateToken(user),
        new UserDto(user.Id, user.Name, user.Email, user.Role));
}

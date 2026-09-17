namespace AgrismartAPI.Domain.Users;

public interface IBiometricCredentialRepository
{
    Task<BiometricCredential?> GetAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    Task AddAsync(BiometricCredential credential, CancellationToken cancellationToken = default);

    Task UpdateAsync(BiometricCredential credential, CancellationToken cancellationToken = default);

    Task RemoveAsync(BiometricCredential credential, CancellationToken cancellationToken = default);
}

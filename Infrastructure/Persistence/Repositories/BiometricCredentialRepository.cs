using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class BiometricCredentialRepository(AppDbContext dbContext) : IBiometricCredentialRepository
{
    public Task<BiometricCredential?> GetAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.BiometricCredentials
            .SingleOrDefaultAsync(credential => credential.UserId == userId && credential.DeviceId == deviceId, cancellationToken);
    }

    public async Task AddAsync(BiometricCredential credential, CancellationToken cancellationToken = default)
    {
        dbContext.BiometricCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BiometricCredential credential, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(BiometricCredential credential, CancellationToken cancellationToken = default)
    {
        dbContext.BiometricCredentials.Remove(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class BiometricCredentialConfiguration : IEntityTypeConfiguration<BiometricCredential>
{
    public void Configure(EntityTypeBuilder<BiometricCredential> builder)
    {
        builder.ToTable("BiometricCredentials");
        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.DeviceId).IsRequired().HasMaxLength(200);
        builder.Property(credential => credential.PublicKey).IsRequired();
        builder.Property(credential => credential.CreatedAt).IsRequired();

        builder.HasIndex(credential => new { credential.UserId, credential.DeviceId }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

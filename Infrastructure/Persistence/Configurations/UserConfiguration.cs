using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(UserProfileLimits.PhoneNumberMaxLength);
        builder.Property(u => u.Nin).HasMaxLength(UserProfileLimits.NinMaxLength);
        builder.Property(u => u.MustChangePassword).IsRequired().HasDefaultValue(false);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}

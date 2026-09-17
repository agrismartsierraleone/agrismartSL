using AgrismartAPI.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.ActorEmail).IsRequired().HasMaxLength(320);
        builder.Property(log => log.Action).IsRequired().HasMaxLength(100);
        builder.Property(log => log.Details).HasMaxLength(2000);

        builder.HasIndex(log => log.CreatedAt);
        builder.HasIndex(log => log.TargetUserId);
    }
}

using AgrismartAPI.Domain.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class CheckoutAuditLogConfiguration : IEntityTypeConfiguration<CheckoutAuditLog>
{
    public void Configure(EntityTypeBuilder<CheckoutAuditLog> builder)
    {
        builder.ToTable("CheckoutAuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.IdempotencyKey).IsRequired().HasMaxLength(64);
        builder.Property(log => log.RequestJson).IsRequired().HasColumnType("text");
        builder.Property(log => log.ResponseJson).HasColumnType("text");
        builder.Property(log => log.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(log => log.CreatedAt);
        builder.HasIndex(log => log.OrderId);
        builder.HasIndex(log => log.CheckoutSessionId);
    }
}

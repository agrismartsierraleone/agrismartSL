using AgrismartAPI.Domain.Checkout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class CheckoutSessionConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> builder)
    {
        builder.ToTable("CheckoutSessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.MonimeSessionId).IsRequired().HasMaxLength(100);
        builder.Property(session => session.Status).IsRequired().HasMaxLength(20);
        builder.Property(session => session.RedirectUrl).HasMaxLength(2000);
        builder.Property(session => session.CancelUrl).HasMaxLength(2000);
        builder.Property(session => session.SuccessUrl).HasMaxLength(2000);
        builder.Property(session => session.IdempotencyKey).IsRequired().HasMaxLength(64);
        builder.Property(session => session.RawResponseJson).IsRequired().HasColumnType("text");

        builder.HasIndex(session => session.OrderId).IsUnique();
        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.MonimeSessionId).IsUnique();
    }
}

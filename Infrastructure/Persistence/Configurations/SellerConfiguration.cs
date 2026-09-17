using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class SellerConfiguration : IEntityTypeConfiguration<Seller>
{
    public void Configure(EntityTypeBuilder<Seller> builder)
    {
        builder.ToTable("Sellers");
        builder.HasKey(seller => seller.Id);

        builder.Property(seller => seller.Name).IsRequired().HasMaxLength(200);
        builder.Property(seller => seller.ContactEmail).HasMaxLength(320);
        builder.Property(seller => seller.ContactPhone).HasMaxLength(30);
        builder.Property(seller => seller.Location).HasMaxLength(200);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(seller => seller.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(seller => seller.UserId).IsUnique();
    }
}

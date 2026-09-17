using AgrismartAPI.Domain.Market;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class MarketProductConfiguration : IEntityTypeConfiguration<MarketProduct>
{
    public void Configure(EntityTypeBuilder<MarketProduct> builder)
    {
        builder.ToTable("MarketProducts");
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name).IsRequired().HasMaxLength(200);
        builder.Property(product => product.Price).HasColumnType("decimal(18,2)");
        builder.Property(product => product.Unit).IsRequired().HasMaxLength(100);
        builder.Property(product => product.Description).HasMaxLength(2000);
        builder.Property(product => product.ImageUrl).HasMaxLength(2048);

        builder.HasOne(product => product.Category)
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(product => product.Seller)
            .WithMany()
            .HasForeignKey(product => product.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(product => product.CategoryId);
        builder.HasIndex(product => product.SellerId);
    }
}

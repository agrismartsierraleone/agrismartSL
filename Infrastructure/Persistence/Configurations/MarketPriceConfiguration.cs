using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class MarketPriceConfiguration : IEntityTypeConfiguration<MarketPrice>
{
    public void Configure(EntityTypeBuilder<MarketPrice> builder)
    {
        builder.ToTable("MarketPrices");
        builder.HasKey(price => price.Id);

        builder.Property(price => price.Category).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(price => price.Crop).IsRequired().HasMaxLength(MarketPrice.MaxCropLength);
        builder.Property(price => price.District).IsRequired().HasMaxLength(50);
        builder.Property(price => price.Unit).IsRequired().HasMaxLength(MarketPrice.MaxUnitLength);
        builder.Property(price => price.Price).HasColumnType("decimal(18,2)");

        // Price history outlives the admin who recorded it.
        builder.HasOne<User>().WithMany().HasForeignKey(price => price.RecordedByUserId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(price => new { price.Category, price.Crop, price.District, price.PriceDate }).IsUnique();
        builder.HasIndex(price => price.PriceDate);
    }
}

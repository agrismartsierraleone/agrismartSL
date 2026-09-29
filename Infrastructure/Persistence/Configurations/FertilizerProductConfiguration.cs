using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class FertilizerProductConfiguration : IEntityTypeConfiguration<FertilizerProduct>
{
    public void Configure(EntityTypeBuilder<FertilizerProduct> builder)
    {
        builder.ToTable("FertilizerProducts");
        builder.HasKey(product => product.Id);

        builder.Property(product => product.Key).IsRequired().HasMaxLength(50);
        builder.Property(product => product.Name).IsRequired().HasMaxLength(100);
        builder.Property(product => product.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(product => product.NitrogenPercent).IsRequired();
        builder.Property(product => product.PhosphatePercent).IsRequired();
        builder.Property(product => product.PotashPercent).IsRequired();
        builder.Property(product => product.SulphurPercent);
        builder.Property(product => product.ZincPercent);
        builder.Property(product => product.BagSizeKg).IsRequired();
        builder.Property(product => product.PricePerBag).HasPrecision(18, 2);
        builder.Property(product => product.Notes).HasMaxLength(500);

        builder.HasIndex(product => product.Key).IsUnique();
    }
}

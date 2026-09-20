using AgrismartAPI.Domain.Harvest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class CropProfileConfiguration : IEntityTypeConfiguration<CropProfile>
{
    public void Configure(EntityTypeBuilder<CropProfile> builder)
    {
        builder.ToTable("Crops");
        builder.HasKey(crop => crop.Id);

        builder.Property(crop => crop.Key).IsRequired().HasMaxLength(50);
        builder.Property(crop => crop.Name).IsRequired().HasMaxLength(100);
        builder.Property(crop => crop.BaseTemperatureC).IsRequired();
        builder.Property(crop => crop.UpperTemperatureC).IsRequired();
        builder.Property(crop => crop.GddToMaturity).IsRequired();
        builder.Property(crop => crop.TypicalDaysToMaturity).IsRequired();

        builder.HasIndex(crop => crop.Key).IsUnique();

        var stagesProperty = builder.Property(crop => crop.Stages)
            .HasColumnName("Stages")
            .HasConversion(JsonValueConverters.ForList<GrowthStage>());
        stagesProperty.Metadata.SetValueComparer(JsonValueConverters.ListComparer<GrowthStage>());
    }
}

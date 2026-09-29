using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class FertilizerCropProfileConfiguration : IEntityTypeConfiguration<FertilizerCropProfile>
{
    public void Configure(EntityTypeBuilder<FertilizerCropProfile> builder)
    {
        builder.ToTable("FertilizerCrops");
        builder.HasKey(crop => crop.Id);

        builder.Property(crop => crop.Key).IsRequired().HasMaxLength(50);
        builder.Property(crop => crop.Name).IsRequired().HasMaxLength(100);
        builder.Property(crop => crop.LocalName).HasMaxLength(100);
        builder.Property(crop => crop.NitrogenKgPerHa).IsRequired();
        builder.Property(crop => crop.PhosphateKgPerHa).IsRequired();
        builder.Property(crop => crop.PotashKgPerHa).IsRequired();
        builder.Property(crop => crop.ReferenceYieldTPerHa).IsRequired();
        builder.Property(crop => crop.MaxRealisticYieldTPerHa).IsRequired();
        builder.Property(crop => crop.YieldBasis).IsRequired().HasMaxLength(50);
        builder.Property(crop => crop.PlantsPerHectare);
        builder.Property(crop => crop.IsPerennial).IsRequired();
        builder.Property(crop => crop.Notes).HasMaxLength(1000);

        builder.HasIndex(crop => crop.Key).IsUnique();

        var scheduleProperty = builder.Property(crop => crop.Schedule)
            .HasColumnName("Schedule")
            .HasConversion(JsonValueConverters.ForList<FertilizerSplit>());
        scheduleProperty.Metadata.SetValueComparer(JsonValueConverters.ListComparer<FertilizerSplit>());
    }
}

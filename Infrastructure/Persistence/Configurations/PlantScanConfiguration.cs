using AgrismartAPI.Domain.Diagnoses;
using AgrismartAPI.Domain.Scans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class PlantScanConfiguration : IEntityTypeConfiguration<PlantScan>
{
    public void Configure(EntityTypeBuilder<PlantScan> builder)
    {
        builder.ToTable("PlantScans");
        builder.HasKey(scan => scan.Id);

        builder.Property(scan => scan.UserId).IsRequired();
        builder.Property(scan => scan.ImagePath).IsRequired().HasMaxLength(2048);
        builder.Property(scan => scan.ScannedAt).IsRequired();
        builder.HasIndex(scan => scan.UserId);

        builder.OwnsOne(scan => scan.Diagnosis, diagnosis =>
        {
            diagnosis.Property(d => d.PlantName).HasColumnName("PlantName").IsRequired().HasMaxLength(200);
            diagnosis.Property(d => d.DiseaseName).HasColumnName("DiseaseName").HasMaxLength(200);
            diagnosis.Property(d => d.Confidence).HasColumnName("Confidence");
            diagnosis.Property(d => d.Severity).HasColumnName("Severity").HasConversion<string>().HasMaxLength(20);
            diagnosis.Property(d => d.Summary).HasColumnName("Summary").HasMaxLength(2000);

            var stepsProperty = diagnosis.Property(d => d.TreatmentSteps)
                .HasColumnName("TreatmentSteps")
                .HasConversion(JsonValueConverters.ForList<TreatmentStep>());
            stepsProperty.Metadata.SetValueComparer(JsonValueConverters.ListComparer<TreatmentStep>());
        });

        builder.Navigation(scan => scan.Diagnosis).IsRequired();
    }
}

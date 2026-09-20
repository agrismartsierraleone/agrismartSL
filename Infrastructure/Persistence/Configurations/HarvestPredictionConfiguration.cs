using AgrismartAPI.Domain.Harvest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class HarvestPredictionConfiguration : IEntityTypeConfiguration<HarvestPrediction>
{
    public void Configure(EntityTypeBuilder<HarvestPrediction> builder)
    {
        builder.ToTable("HarvestPredictions");
        builder.HasKey(prediction => prediction.Id);

        builder.Property(prediction => prediction.UserId);
        builder.Property(prediction => prediction.CropId).IsRequired().HasMaxLength(50);
        builder.Property(prediction => prediction.CropName).IsRequired().HasMaxLength(100);
        builder.Property(prediction => prediction.Latitude).IsRequired();
        builder.Property(prediction => prediction.Longitude).IsRequired();
        builder.Property(prediction => prediction.PlantingDate).IsRequired();
        builder.Property(prediction => prediction.AsOf).IsRequired();
        builder.Property(prediction => prediction.AlreadyMature).IsRequired();
        builder.Property(prediction => prediction.CreatedAt).IsRequired();

        builder.HasIndex(prediction => prediction.UserId);

        builder.OwnsOne(prediction => prediction.Gdd, gdd =>
        {
            gdd.Property(g => g.Accumulated).HasColumnName("Gdd_Accumulated");
            gdd.Property(g => g.Required).HasColumnName("Gdd_Required");
            gdd.Property(g => g.Remaining).HasColumnName("Gdd_Remaining");
            gdd.Property(g => g.PercentComplete).HasColumnName("Gdd_PercentComplete");
            gdd.Property(g => g.BaseTemperatureC).HasColumnName("Gdd_BaseTemperatureC");
            gdd.Property(g => g.UpperTemperatureC).HasColumnName("Gdd_UpperTemperatureC");
            gdd.Property(g => g.DaysObserved).HasColumnName("Gdd_DaysObserved");
            gdd.Property(g => g.DaysForecast).HasColumnName("Gdd_DaysForecast");
        });
        builder.Navigation(prediction => prediction.Gdd).IsRequired();

        builder.OwnsOne(prediction => prediction.Stage, stage =>
        {
            stage.Property(s => s.Current).HasColumnName("Stage_Current").HasMaxLength(100);
            stage.Property(s => s.Next).HasColumnName("Stage_Next").HasMaxLength(100);
            stage.Property(s => s.NextStageStarts).HasColumnName("Stage_NextStageStarts");
        });
        builder.Navigation(prediction => prediction.Stage).IsRequired();

        builder.OwnsOne(prediction => prediction.Harvest, harvest =>
        {
            harvest.Property(h => h.Earliest).HasColumnName("Harvest_Earliest");
            harvest.Property(h => h.Expected).HasColumnName("Harvest_Expected");
            harvest.Property(h => h.Latest).HasColumnName("Harvest_Latest");
            harvest.Property(h => h.DaysFromNow).HasColumnName("Harvest_DaysFromNow");
            harvest.Property(h => h.WindowWidthDays).HasColumnName("Harvest_WindowWidthDays");
            harvest.Property(h => h.Confidence).HasColumnName("Harvest_Confidence").HasMaxLength(20);
            harvest.Property(h => h.Basis).HasColumnName("Harvest_Basis").HasMaxLength(500);
        });
        builder.Navigation(prediction => prediction.Harvest).IsRequired();

        builder.OwnsOne(prediction => prediction.Methodology, methodology =>
        {
            methodology.Property(m => m.Model).HasColumnName("Methodology_Model").HasMaxLength(200);
            methodology.Property(m => m.WeatherSource).HasColumnName("Methodology_WeatherSource").HasMaxLength(200);
            methodology.Property(m => m.AnalogYears).HasColumnName("Methodology_AnalogYears");
            methodology.Property(m => m.Notes).HasColumnName("Methodology_Notes").HasMaxLength(500);
        });
        builder.Navigation(prediction => prediction.Methodology).IsRequired();
    }
}

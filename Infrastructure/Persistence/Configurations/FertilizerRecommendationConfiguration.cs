using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class FertilizerRecommendationConfiguration : IEntityTypeConfiguration<FertilizerRecommendation>
{
    public void Configure(EntityTypeBuilder<FertilizerRecommendation> builder)
    {
        builder.ToTable("FertilizerRecommendations");
        builder.HasKey(recommendation => recommendation.Id);

        builder.Property(recommendation => recommendation.UserId).IsRequired();
        builder.Property(recommendation => recommendation.CropId).IsRequired().HasMaxLength(50);
        builder.Property(recommendation => recommendation.CropName).IsRequired().HasMaxLength(100);
        builder.Property(recommendation => recommendation.PlotLabel).HasMaxLength(100);
        builder.Property(recommendation => recommendation.AreaHectares).IsRequired();
        builder.Property(recommendation => recommendation.TotalEstimatedCost).HasPrecision(18, 2);
        builder.Property(recommendation => recommendation.Currency).IsRequired().HasMaxLength(10);
        builder.Property(recommendation => recommendation.InputsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(recommendation => recommendation.ResultJson).IsRequired().HasColumnType("jsonb");
        builder.Property(recommendation => recommendation.CreatedAt).IsRequired();

        builder.HasIndex(recommendation => new { recommendation.UserId, recommendation.CreatedAt });
        builder.HasIndex(recommendation => recommendation.CropId);
    }
}

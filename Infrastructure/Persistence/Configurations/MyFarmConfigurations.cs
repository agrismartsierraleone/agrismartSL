using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Scans;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

// My Farm tables. Foreign keys point at Farms/FarmCrops, Users and PlantScans; none of those tables gain columns here.

public class FarmActivityConfiguration : IEntityTypeConfiguration<FarmActivity>
{
    public void Configure(EntityTypeBuilder<FarmActivity> builder)
    {
        builder.ToTable("FarmActivities");
        builder.HasKey(activity => activity.Id);

        builder.Property(activity => activity.UserId).IsRequired();
        builder.Property(activity => activity.FarmId).IsRequired();
        builder.Property(activity => activity.CropId).HasMaxLength(50);
        builder.Property(activity => activity.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(activity => activity.Title).IsRequired().HasMaxLength(FarmActivity.MaxTitleLength);
        builder.Property(activity => activity.Notes).HasMaxLength(FarmActivity.MaxNotesLength);
        builder.Property(activity => activity.DueDate).IsRequired();
        builder.Property(activity => activity.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(activity => activity.Cost).HasPrecision(18, 2);
        builder.Property(activity => activity.Source).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(activity => activity.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(activity => activity.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Farm>().WithMany().HasForeignKey(activity => activity.FarmId).OnDelete(DeleteBehavior.Cascade);
        // Completed work stays in the farm's history (and cost analytics) when its crop is removed.
        builder.HasOne<FarmCrop>().WithMany().HasForeignKey(activity => activity.FarmCropId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(activity => new { activity.FarmId, activity.Status, activity.DueDate });
        builder.HasIndex(activity => activity.FarmCropId);
    }
}

public class FarmScanConfiguration : IEntityTypeConfiguration<FarmScan>
{
    public void Configure(EntityTypeBuilder<FarmScan> builder)
    {
        builder.ToTable("FarmScans");
        builder.HasKey(scan => scan.Id);

        builder.Property(scan => scan.UserId).IsRequired();
        builder.Property(scan => scan.FarmId).IsRequired();
        builder.Property(scan => scan.PlantScanId).IsRequired();
        builder.Property(scan => scan.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(scan => scan.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Farm>().WithMany().HasForeignKey(scan => scan.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmCrop>().WithMany().HasForeignKey(scan => scan.FarmCropId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<PlantScan>().WithMany().HasForeignKey(scan => scan.PlantScanId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(scan => new { scan.FarmId, scan.CreatedAt });
        builder.HasIndex(scan => scan.PlantScanId).IsUnique();
    }
}

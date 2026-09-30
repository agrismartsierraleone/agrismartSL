using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

// AI assistant tables. All are new; foreign keys point only at these tables or at Users.Id (no change to Users).

public class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.ToTable("AssistantConversations");
        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.UserId).IsRequired();
        builder.Property(conversation => conversation.CreatedAt).IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(conversation => conversation.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt });
    }
}

public class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.ToTable("AssistantMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.ConversationId).IsRequired();
        builder.Property(message => message.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(message => message.Content).IsRequired().HasMaxLength(AssistantMessage.MaxContentLength);
        builder.Property(message => message.Model).HasMaxLength(100);
        builder.Property(message => message.CreatedAt).IsRequired();

        builder.HasOne<AssistantConversation>().WithMany().HasForeignKey(message => message.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt });
    }
}

public class AssistantMessageReportConfiguration : IEntityTypeConfiguration<AssistantMessageReport>
{
    public void Configure(EntityTypeBuilder<AssistantMessageReport> builder)
    {
        builder.ToTable("AssistantMessageReports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.MessageId).IsRequired();
        builder.Property(report => report.ConversationId).IsRequired();
        builder.Property(report => report.UserId).IsRequired();
        builder.Property(report => report.Reason).IsRequired().HasMaxLength(AssistantMessageReport.MaxReasonLength);
        builder.Property(report => report.CreatedAt).IsRequired();

        builder.HasOne<AssistantMessage>().WithMany().HasForeignKey(report => report.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(report => report.MessageId);
        builder.HasIndex(report => report.CreatedAt);
    }
}

public class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.ToTable("Farms");
        builder.HasKey(farm => farm.Id);

        builder.Property(farm => farm.UserId).IsRequired();
        builder.Property(farm => farm.Name).IsRequired().HasMaxLength(100);
        builder.Property(farm => farm.District).HasMaxLength(100);
        builder.Property(farm => farm.CreatedAt).IsRequired();

        builder.HasMany(farm => farm.Crops).WithOne().HasForeignKey(crop => crop.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(farm => farm.Crops).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>().WithMany().HasForeignKey(farm => farm.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(farm => farm.UserId);
    }
}

public class FarmCropConfiguration : IEntityTypeConfiguration<FarmCrop>
{
    public void Configure(EntityTypeBuilder<FarmCrop> builder)
    {
        builder.ToTable("FarmCrops");
        builder.HasKey(crop => crop.Id);

        builder.Property(crop => crop.FarmId).IsRequired();
        builder.Property(crop => crop.CropId).IsRequired().HasMaxLength(50);
        builder.Property(crop => crop.CreatedAt).IsRequired();

        builder.HasIndex(crop => crop.FarmId);
    }
}

public class FarmRecordConfiguration : IEntityTypeConfiguration<FarmRecord>
{
    public void Configure(EntityTypeBuilder<FarmRecord> builder)
    {
        builder.ToTable("FarmRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.UserId).IsRequired();
        builder.Property(record => record.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(record => record.CropId).HasMaxLength(50);
        builder.Property(record => record.RecordDate).IsRequired();
        builder.Property(record => record.Unit).HasMaxLength(30);
        builder.Property(record => record.Notes).HasMaxLength(2000);
        builder.Property(record => record.CreatedAt).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(record => record.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Farm>().WithMany().HasForeignKey(record => record.FarmId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(record => new { record.UserId, record.RecordDate });
    }
}

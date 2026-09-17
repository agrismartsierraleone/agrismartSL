using AgrismartAPI.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);

        builder.Property(order => order.UserId).IsRequired();
        builder.Property(order => order.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(order => order.CreatedAt).IsRequired();

        var itemsProperty = builder.Property(order => order.Items)
            .HasColumnName("Items")
            .HasConversion(JsonValueConverters.ForList<OrderItem>());
        itemsProperty.Metadata.SetValueComparer(JsonValueConverters.ListComparer<OrderItem>());

        builder.HasIndex(order => order.UserId);
    }
}

using AgrismartAPI.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(cart => cart.Id);

        builder.Property(cart => cart.UserId).IsRequired();
        builder.Property(cart => cart.CreatedAt).IsRequired();
        builder.Property(cart => cart.UpdatedAt).IsRequired();

        var itemsProperty = builder.Property(cart => cart.Items)
            .HasColumnName("Items")
            .HasConversion(JsonValueConverters.ForList<CartItem>());
        itemsProperty.Metadata.SetValueComparer(JsonValueConverters.ListComparer<CartItem>());

        builder.HasIndex(cart => cart.UserId).IsUnique();
    }
}

using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Scans;
using AgrismartAPI.Domain.Tutorials;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PlantScan> PlantScans => Set<PlantScan>();
    public DbSet<MarketProduct> MarketProducts => Set<MarketProduct>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<TutorialVideo> TutorialVideos => Set<TutorialVideo>();
    public DbSet<VideoComment> VideoComments => Set<VideoComment>();
    public DbSet<VideoLike> VideoLikes => Set<VideoLike>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<BiometricCredential> BiometricCredentials => Set<BiometricCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

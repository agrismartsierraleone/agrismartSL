using AgrismartAPI.Domain.Audit;
using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Checkout;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Domain.Jobs;
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
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<TutorialVideo> TutorialVideos => Set<TutorialVideo>();
    public DbSet<VideoComment> VideoComments => Set<VideoComment>();
    public DbSet<VideoLike> VideoLikes => Set<VideoLike>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<BiometricCredential> BiometricCredentials => Set<BiometricCredential>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CheckoutSession> CheckoutSessions => Set<CheckoutSession>();
    public DbSet<CheckoutAuditLog> CheckoutAuditLogs => Set<CheckoutAuditLog>();
    public DbSet<CropProfile> Crops => Set<CropProfile>();
    public DbSet<HarvestPrediction> HarvestPredictions => Set<HarvestPrediction>();
    public DbSet<FertilizerCropProfile> FertilizerCrops => Set<FertilizerCropProfile>();
    public DbSet<FertilizerProduct> FertilizerProducts => Set<FertilizerProduct>();
    public DbSet<FertilizerRecommendation> FertilizerRecommendations => Set<FertilizerRecommendation>();
    public DbSet<JobPost> JobPosts => Set<JobPost>();
    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

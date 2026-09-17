using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Tutorials;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence;

public static class AppDbContextSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Role == UserRole.Admin, cancellationToken))
        {
            dbContext.Users.Add(User.Register("Admin", "admin@agrismart.local", passwordHasher.Hash("ChangeMe123!"), UserRole.Admin));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.Users.AnyAsync(user => user.Email == "yayah.waritay@njala.edu.sl", cancellationToken))
        {
            dbContext.Users.Add(User.Register("yayah Waritay", "yayah.waritay@njala.edu.sl", passwordHasher.Hash("Sal@2024"), UserRole.Admin));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.Categories.AnyAsync(cancellationToken))
        {
            dbContext.Categories.AddRange(
                Category.Create("Seeds", "Seeds and planting material."),
                Category.Create("Fertilizer", "Soil nutrition and conditioning products."),
                Category.Create("Produce", "Fresh farm produce."));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.Sellers.AnyAsync(cancellationToken))
        {
            dbContext.Sellers.AddRange(
                Seller.Create("AgroSupply Ltd", null, null, null),
                Seller.Create("GreenGrow Agro", null, null, null),
                Seller.Create("Sunrise Farms", null, null, null),
                Seller.Create("RootStock Nigeria", null, null, null),
                Seller.Create("EcoSoil Ltd", null, null, null));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.MarketProducts.AnyAsync(cancellationToken))
        {
            var categories = await dbContext.Categories.ToDictionaryAsync(category => category.Name, cancellationToken);
            var sellers = await dbContext.Sellers.ToDictionaryAsync(seller => seller.Name, cancellationToken);

            dbContext.MarketProducts.AddRange(
                MarketProduct.Create("Hybrid Maize Seed", categories["Seeds"].Id, 12500m, "25 kg bag", sellers["AgroSupply Ltd"].Id, "High-yield hybrid maize seed suited to tropical climates."),
                MarketProduct.Create("NPK Fertilizer 15-15-15", categories["Fertilizer"].Id, 18000m, "50 kg bag", sellers["GreenGrow Agro"].Id, "Balanced NPK blend for general crop nutrition."),
                MarketProduct.Create("Fresh Tomatoes", categories["Produce"].Id, 8500m, "crate", sellers["Sunrise Farms"].Id, "Vine-ripened tomatoes harvested within 24 hours."),
                MarketProduct.Create("Cassava Stem Cuttings", categories["Seeds"].Id, 4500m, "bundle of 50", sellers["RootStock Nigeria"].Id, "Disease-free cassava stems for planting."),
                MarketProduct.Create("Organic Compost", categories["Fertilizer"].Id, 9000m, "40 kg bag", sellers["EcoSoil Ltd"].Id, "Nutrient-rich compost for organic farming."),
                MarketProduct.Create("Pepper (Fresh)", categories["Produce"].Id, 6000m, "basket", sellers["Sunrise Farms"].Id, "Freshly harvested bell and chili peppers."));
        }

        if (!await dbContext.TutorialVideos.AnyAsync(cancellationToken))
        {
            dbContext.TutorialVideos.AddRange(
                TutorialVideo.Create(
                    "Spotting fall armyworm early",
                    "Learn the early warning signs of fall armyworm infestation before it spreads across your field.",
                    "https://example.com/videos/fall-armyworm.mp4",
                    "https://example.com/thumbnails/fall-armyworm.jpg",
                    "Ngozi A.",
                    "👩🏾‍🌾",
                    "9:56",
                    18400,
                    DateTimeOffset.Parse("2026-06-01T00:00:00Z")),
                TutorialVideo.Create(
                    "Treating early blight in tomatoes",
                    "A step-by-step guide to identifying and treating Alternaria solani in tomato crops.",
                    "https://example.com/videos/early-blight.mp4",
                    "https://example.com/thumbnails/early-blight.jpg",
                    "Chinedu O.",
                    "👨🏾‍🌾",
                    "7:12",
                    9800,
                    DateTimeOffset.Parse("2026-05-15T00:00:00Z")),
                TutorialVideo.Create(
                    "Soil testing basics for smallholders",
                    "Simple, low-cost ways to test your soil's pH and nutrient levels before planting season.",
                    "https://example.com/videos/soil-testing.mp4",
                    "https://example.com/thumbnails/soil-testing.jpg",
                    "Amina S.",
                    "👩🏾‍🌾",
                    "12:34",
                    5600,
                    DateTimeOffset.Parse("2026-04-20T00:00:00Z")));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

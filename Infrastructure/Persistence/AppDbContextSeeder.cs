using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Harvest;
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

        if (!await dbContext.Crops.AnyAsync(cancellationToken))
        {
            // Textbook mid-range thermal-time values, good enough to demonstrate the model and give a
            // sane first answer. They are NOT calibrated for any specific variety or agro-ecological
            // zone — replace with values fitted to local trial data before this drives a real decision.
            IReadOnlyList<GrowthStage> cerealStages =
            [
                new("Emergence", 0.00), new("Vegetative", 0.10), new("Flowering", 0.45),
                new("Grain filling", 0.65), new("Physiological maturity", 0.90)
            ];
            IReadOnlyList<GrowthStage> legumeStages =
            [
                new("Emergence", 0.00), new("Vegetative", 0.12), new("Flowering", 0.40),
                new("Pod fill", 0.62), new("Maturity", 0.88)
            ];
            IReadOnlyList<GrowthStage> rootStages =
            [
                new("Sprouting", 0.00), new("Canopy establishment", 0.12), new("Root initiation", 0.35),
                new("Root bulking", 0.55), new("Maturity", 0.90)
            ];
            IReadOnlyList<GrowthStage> fruitStages =
            [
                new("Emergence", 0.00), new("Vegetative", 0.10), new("Flowering", 0.40),
                new("Fruit set", 0.58), new("Ripening", 0.80)
            ];

            dbContext.Crops.AddRange(
                //                      key            name             Tbase Tupper  GDD    days  stages
                CropProfile.Create("maize",        "Maize",          10.0, 30.0, 1500, 110, cerealStages),
                CropProfile.Create("rice-lowland", "Rice (lowland)", 10.0, 30.0, 2100, 125, cerealStages),
                CropProfile.Create("rice-upland",  "Rice (upland)",  10.0, 30.0, 1850, 110, cerealStages),
                CropProfile.Create("sorghum",      "Sorghum",        10.0, 30.0, 1700, 120, cerealStages),
                CropProfile.Create("millet-pearl", "Pearl millet",   10.0, 34.0, 1400,  90, cerealStages),
                CropProfile.Create("wheat",        "Wheat",           0.0, 25.0, 2000, 140, cerealStages),
                CropProfile.Create("groundnut",    "Groundnut",      10.0, 32.0, 1600, 120, legumeStages),
                CropProfile.Create("cowpea",       "Cowpea",         11.0, 32.0, 1300,  75, legumeStages),
                CropProfile.Create("soybean",      "Soybean",        10.0, 30.0, 1500, 110, legumeStages),
                CropProfile.Create("cassava",      "Cassava",        13.0, 32.0, 4500, 300, rootStages),
                CropProfile.Create("sweet-potato", "Sweet potato",   15.0, 32.0, 1800, 120, rootStages),
                CropProfile.Create("potato",       "Potato",          7.0, 30.0, 1500, 110, rootStages),
                CropProfile.Create("tomato",       "Tomato",         10.0, 30.0, 1400,  95, fruitStages),
                CropProfile.Create("pepper-chili", "Chili pepper",   12.0, 32.0, 1600, 105, fruitStages));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

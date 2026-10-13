using AgrismartAPI.Application.MyFarm;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Tests.MyFarm;

public class CropCalendarTests
{
    // The design's example: rice planted 12 Jun, viewed mid-July.
    private static readonly DateOnly Planted = new(2025, 6, 12);
    private static readonly DateOnly Today = new(2025, 7, 14);

    private static readonly CropCatalog Catalog = new(
        [CropProfile.Create("rice-lowland", "Rice (lowland)", 10, 30, 2100, 128, [new("Emergence", 0), new("Vegetative", 0.1)])],
        [
            FertilizerCropProfile.Create("rice-lowland", "Rice (lowland / IVS)", "Res", 80, 40, 40, 3.5, 6, "paddy", null, false, null,
            [
                new("Basal (at transplanting)", 0, "At transplanting", 30, 100, 100),
                new("Top-dressing 1 (tillering)", 21, "About 3 weeks after transplanting", 40, 0, 0),
                new("Top-dressing 2 (panicle initiation)", 45, "About 6-7 weeks after transplanting", 30, 0, 0)
            ])
        ]);

    private static FarmCrop Rice(DateOnly? planted = null, DateOnly? harvest = null)
    {
        var farm = Farm.Create(Guid.NewGuid(), "Green Valley Farm", "Port Loko", null, null, 4.5);
        var crop = farm.AddCrop("rice-lowland", planted ?? Planted, 3.0);
        crop.UpdateDetails("rice-lowland", "Plot 1", "NERICA 14", planted ?? Planted, harvest, 3.0, null, null, null, null);
        return crop;
    }

    [Fact]
    public void Expected_harvest_is_estimated_from_days_to_maturity_unless_given()
    {
        var info = Catalog.Get("rice-lowland");

        Assert.Equal((new DateOnly(2025, 10, 18), true), CropCalendar.ExpectedHarvest(Rice(), info));
        Assert.Equal((new DateOnly(2025, 10, 1), false), CropCalendar.ExpectedHarvest(Rice(harvest: new DateOnly(2025, 10, 1)), info));
    }

    [Fact]
    public void Progress_and_stage_follow_the_season()
    {
        var crop = Rice();
        var info = Catalog.Get("rice-lowland");
        var (harvest, _) = CropCalendar.ExpectedHarvest(crop, info);

        Assert.Equal(25, CropCalendar.ProgressPercent(crop, harvest, Today)); // 32 of 128 days
        Assert.Equal("Tillering", CropCalendar.GrowthStage(crop, info, harvest, Today));
        Assert.Equal(FarmCropDisplayStatus.Growing, CropCalendar.DisplayStatus(crop, harvest, Today));
        Assert.Equal(FarmCropDisplayStatus.ReadyToHarvest, CropCalendar.DisplayStatus(crop, harvest, harvest!.Value));
        Assert.Equal(FarmCropDisplayStatus.Planned, CropCalendar.DisplayStatus(crop, harvest, Planted.AddDays(-1)));
    }

    [Fact]
    public void Calendar_marks_past_stages_done_and_highlights_fertilizing()
    {
        var (stages, current) = CropCalendar.Stages(Rice(), Catalog.Get("rice-lowland"), [], Today);

        Assert.Equal(["landPreparation", "planting", "fertilizing", "weeding", "pestControl", "harvesting"], stages.Select(s => s.Key));
        Assert.Equal(CalendarStageStatus.Completed, stages[0].Status);
        Assert.Equal(CalendarStageStatus.Completed, stages[1].Status);
        Assert.Equal(CalendarStageStatus.InProgress, stages[2].Status);
        Assert.Equal(CalendarStageStatus.Upcoming, stages[5].Status);
        Assert.Equal("fertilizing", current);
        Assert.Equal((Planted, CropCalendar.DayPrecision), (stages[1].KeyDate!.Value, stages[1].DatePrecision));
        Assert.Equal(new DateOnly(2025, 7, 3), stages[2].KeyDate); // first top-dressing, 21 days after planting
    }

    [Fact]
    public void Open_tasks_after_the_window_make_the_stage_overdue()
    {
        var crop = Rice();
        var landPrep = FarmActivity.Create(Guid.NewGuid(), Guid.NewGuid(), crop.Id, crop.CropId, FarmActivityType.LandPreparation, "Land preparation",
            null, Planted.AddDays(-14), null, FarmActivitySource.Calendar);

        var (stages, current) = CropCalendar.Stages(crop, Catalog.Get("rice-lowland"), [landPrep], Today);

        Assert.Equal(CalendarStageStatus.Overdue, stages[0].Status);
        Assert.Equal("landPreparation", current);
    }

    [Fact]
    public void Planned_tasks_start_today_and_follow_the_fertilizer_schedule()
    {
        var tasks = CropCalendar.PlanActivities(Rice(), Catalog.Get("rice-lowland"), Today);

        Assert.All(tasks, task => Assert.True(task.DueDate >= Today));
        var fertilizer = Assert.Single(tasks, task => task.Type == FarmActivityType.Fertilizing);
        Assert.Equal("Fertilizer application (3rd dose)", fertilizer.Title);
        Assert.Equal(Planted.AddDays(45), fertilizer.DueDate);
        Assert.Contains(tasks, task => task.Title == "Monitor for stem borers");
        Assert.Equal(("Harvest Rice (lowland)", new DateOnly(2025, 10, 18)), (tasks[^1].Title, tasks[^1].DueDate));
    }

    [Fact]
    public void Crops_without_a_planting_date_get_no_tasks_or_dates()
    {
        var farm = Farm.Create(Guid.NewGuid(), "Farm", null, null, null, null);
        var crop = farm.AddCrop("rice-lowland", null, null);
        var info = Catalog.Get("rice-lowland");

        Assert.Empty(CropCalendar.PlanActivities(crop, info, Today));
        var (stages, current) = CropCalendar.Stages(crop, info, [], Today);
        Assert.All(stages, stage => Assert.Equal(CalendarStageStatus.Upcoming, stage.Status));
        Assert.Equal("landPreparation", current);
        Assert.Equal(0, CropCalendar.ProgressPercent(crop, null, Today));
    }
}

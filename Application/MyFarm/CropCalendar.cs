using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Farms;

namespace AgrismartAPI.Application.MyFarm;

/// <summary>A task the crop calendar suggests, before it is saved as a <see cref="FarmActivity"/>.</summary>
public record PlannedActivity(FarmActivityType Type, string Title, string? Notes, DateOnly DueDate);

/// <summary>
/// Pure date arithmetic behind the crop cards and the calendar strip: expected harvest, growth progress,
/// growth stage, calendar stage windows and the tasks generated for a new crop. "today" is always passed in.
/// </summary>
/// <remarks>
/// Windows are rules of thumb for Sierra Leone smallholder crops (land prep ~3 weeks before planting, weeding from
/// week 2, pest scouting from week 3, harvest window of 2 weeks); fertilizer timing comes from the fertilizer
/// calculator's split schedule for the crop.
/// </remarks>
public static class CropCalendar
{
    public const string DayPrecision = "day";
    public const string MonthPrecision = "month";

    /// <summary>The six stages of the calendar strip, in order.</summary>
    public static readonly IReadOnlyList<(string Key, string Name, FarmActivityType Type)> StageDefinitions =
    [
        ("landPreparation", "Land Prep", FarmActivityType.LandPreparation),
        ("planting", "Planting", FarmActivityType.Planting),
        ("fertilizing", "Fertilizing", FarmActivityType.Fertilizing),
        ("weeding", "Weeding", FarmActivityType.Weeding),
        ("pestControl", "Pest Control", FarmActivityType.PestControl),
        ("harvesting", "Harvesting", FarmActivityType.Harvesting)
    ];

    private static readonly Dictionary<string, string> PestsToWatch = new()
    {
        ["rice-lowland"] = "stem borers",
        ["rice-upland"] = "stem borers and blast",
        ["maize"] = "fall armyworm",
        ["sorghum"] = "stem borers",
        ["millet-pearl"] = "downy mildew",
        ["cassava"] = "mealybugs and mosaic disease",
        ["sweet-potato"] = "sweet potato weevils",
        ["potato"] = "late blight",
        ["groundnut"] = "aphids and leaf spot",
        ["cowpea"] = "aphids and pod borers",
        ["soybean"] = "pod-sucking bugs",
        ["tomato"] = "whiteflies and blight",
        ["pepper-chili"] = "aphids and thrips",
        ["okra"] = "flea beetles and aphids",
        ["cocoa"] = "black pod and capsids",
        ["oil-palm"] = "leaf miners"
    };

    public static string? StageKeyFor(FarmActivityType type) =>
        StageDefinitions.Where(stage => stage.Type == type).Select(stage => stage.Key).FirstOrDefault();

    /// <summary>The farmer's date if set, otherwise planting + typical days to maturity.</summary>
    public static (DateOnly? Date, bool IsEstimate) ExpectedHarvest(FarmCrop crop, CropInfo info)
    {
        if (crop.ExpectedHarvestDate is { } given)
        {
            return (given, false);
        }

        return crop.PlantingDate is { } planted && info.TypicalDaysToMaturity is { } days
            ? (planted.AddDays(days), true)
            : (null, false);
    }

    public static FarmCropDisplayStatus DisplayStatus(FarmCrop crop, DateOnly? expectedHarvest, DateOnly today) => crop.Status switch
    {
        FarmCropStatus.Harvested => FarmCropDisplayStatus.Harvested,
        FarmCropStatus.Failed => FarmCropDisplayStatus.Failed,
        _ when crop.PlantingDate is null || crop.PlantingDate > today => FarmCropDisplayStatus.Planned,
        _ when expectedHarvest is { } harvest && harvest <= today => FarmCropDisplayStatus.ReadyToHarvest,
        _ => FarmCropDisplayStatus.Growing
    };

    /// <summary>0-100. Harvested crops are 100; planned crops 0; unknown season length 0.</summary>
    public static int ProgressPercent(FarmCrop crop, DateOnly? expectedHarvest, DateOnly today)
    {
        if (crop.Status == FarmCropStatus.Harvested)
        {
            return 100;
        }

        return Fraction(crop, expectedHarvest, today) is { } fraction ? (int)Math.Round(fraction * 100) : 0;
    }

    public static string? GrowthStage(FarmCrop crop, CropInfo info, DateOnly? expectedHarvest, DateOnly today)
    {
        if (crop.Status == FarmCropStatus.Harvested)
        {
            return "Harvested";
        }

        if (crop.PlantingDate is not { } planted || planted > today)
        {
            return null;
        }

        var fraction = Fraction(crop, expectedHarvest, today);
        if (fraction is null || info.Stages.Count == 0)
        {
            return "Growing";
        }

        return info.Stages.Where(stage => stage.StartFraction <= fraction).OrderBy(stage => stage.StartFraction).LastOrDefault()?.Name
               ?? info.Stages[0].Name;
    }

    /// <summary>The calendar strip for one crop, with each stage's status worked out from its tasks and dates.</summary>
    /// <param name="activities">This crop's activities (any status).</param>
    public static (IReadOnlyList<CalendarStageDto> Stages, string? CurrentStageKey) Stages(
        FarmCrop crop, CropInfo info, IReadOnlyCollection<FarmActivity> activities, DateOnly today)
    {
        var (harvest, _) = ExpectedHarvest(crop, info);
        var stages = new List<CalendarStageDto>();
        foreach (var (key, name, type) in StageDefinitions)
        {
            var (start, end, keyDate, precision) = Window(type, crop.PlantingDate, harvest, info);
            var ofType = activities.Where(activity => activity.Type == type && activity.Status != FarmActivityStatus.Skipped).ToList();
            var completed = ofType.Count(activity => activity.Status == FarmActivityStatus.Completed);
            var pending = ofType.Count - completed;

            CalendarStageStatus status;
            if (crop.Status == FarmCropStatus.Harvested)
            {
                status = CalendarStageStatus.Completed;
            }
            else if (ofType.Count > 0 && pending == 0)
            {
                status = CalendarStageStatus.Completed;
            }
            else if (end is { } stageEnd && stageEnd < today)
            {
                // Window over: done unless tasks are still open.
                status = pending > 0 ? CalendarStageStatus.Overdue : CalendarStageStatus.Completed;
            }
            else if (start is { } stageStart && stageStart <= today)
            {
                status = CalendarStageStatus.InProgress;
            }
            else
            {
                status = CalendarStageStatus.Upcoming;
            }

            stages.Add(new CalendarStageDto(key, name, type, start, end, keyDate, precision, status, ofType.Count, completed));
        }

        return (stages, stages.FirstOrDefault(stage => stage.Status != CalendarStageStatus.Completed)?.Key);
    }

    /// <summary>
    /// Tasks for the crop from today on (past ones are not created: the calendar already treats past windows as done).
    /// None when there is no planting date or the crop is no longer in the field.
    /// </summary>
    public static IReadOnlyList<PlannedActivity> PlanActivities(FarmCrop crop, CropInfo info, DateOnly today)
    {
        if (crop.Status != FarmCropStatus.Active || crop.PlantingDate is not { } planted)
        {
            return [];
        }

        var (harvest, _) = ExpectedHarvest(crop, info);
        var name = info.Name;
        var planned = new List<PlannedActivity>
        {
            new(FarmActivityType.LandPreparation, "Land preparation", "Clear, plough and level the plot before planting.", planted.AddDays(-14)),
            new(FarmActivityType.Planting, $"Plant {name}", crop.Variety is null ? null : $"Variety: {crop.Variety}", planted)
        };

        var schedule = info.IsPerennial ? [] : info.FertilizerSchedule;
        if (schedule.Count == 0)
        {
            planned.Add(new(FarmActivityType.Fertilizing, "Fertilizer application",
                "Use the fertilizer calculator for the amount to apply.", planted.AddDays(21)));
        }
        else
        {
            for (var i = 0; i < schedule.Count; i++)
            {
                var split = schedule[i];
                planned.Add(new(FarmActivityType.Fertilizing, $"Fertilizer application ({Ordinal(i + 1)} dose)",
                    $"{split.Stage}: {split.Timing}.", planted.AddDays(split.DaysAfterPlanting)));
            }
        }

        planned.Add(new(FarmActivityType.Weeding, "Weeding", "Weed before applying fertilizer.", planted.AddDays(21)));
        planned.Add(new(FarmActivityType.Weeding, "Weeding (2nd round)", null, planted.AddDays(42)));

        var pests = PestsToWatch.GetValueOrDefault(info.Id, "pests and diseases");
        planned.Add(new(FarmActivityType.PestControl, $"Monitor for {pests}", "Walk the plot and scan any sick-looking plants.", planted.AddDays(25)));
        planned.Add(new(FarmActivityType.PestControl, $"Monitor for {pests}", null, planted.AddDays(50)));

        if (harvest is { } harvestDate)
        {
            planned.Add(new(FarmActivityType.Harvesting, $"Harvest {name}", null, harvestDate));
        }

        return planned
            .Where(task => task.DueDate >= today)
            .Where(task => harvest is null || task.Type == FarmActivityType.Harvesting || task.DueDate < harvest)
            .OrderBy(task => task.DueDate)
            .ToList();
    }

    private static double? Fraction(FarmCrop crop, DateOnly? expectedHarvest, DateOnly today)
    {
        if (crop.PlantingDate is not { } planted || expectedHarvest is not { } harvest || harvest <= planted)
        {
            return null;
        }

        var fraction = (today.DayNumber - planted.DayNumber) / (double)(harvest.DayNumber - planted.DayNumber);
        return Math.Clamp(fraction, 0, 1);
    }

    private static (DateOnly? Start, DateOnly? End, DateOnly? KeyDate, string Precision) Window(
        FarmActivityType type, DateOnly? planting, DateOnly? harvest, CropInfo info)
    {
        if (planting is not { } p)
        {
            return (null, null, null, MonthPrecision);
        }

        var season = harvest is { } h ? h.DayNumber - p.DayNumber : (int?)null;
        int SeasonDay(double fraction, int fallback) => season is { } s ? (int)Math.Round(s * fraction) : fallback;

        switch (type)
        {
            case FarmActivityType.LandPreparation:
                return (p.AddDays(-21), p.AddDays(-1), p.AddDays(-21), MonthPrecision);
            case FarmActivityType.Planting:
                return (p, p.AddDays(3), p, DayPrecision);
            case FarmActivityType.Fertilizing:
                var splits = info.IsPerennial ? [] : info.FertilizerSchedule;
                var first = splits.Count > 0 ? splits.Min(split => split.DaysAfterPlanting) : 14;
                var last = splits.Count > 0 ? splits.Max(split => split.DaysAfterPlanting) : 45;
                // The strip shows the top-dressing month (the basal dose is part of planting).
                var keyDay = splits.Where(split => split.DaysAfterPlanting > 0).Select(split => split.DaysAfterPlanting).DefaultIfEmpty(first).Min();
                return (p.AddDays(first), p.AddDays(last + 7), p.AddDays(keyDay), MonthPrecision);
            case FarmActivityType.Weeding:
                return (p.AddDays(14), p.AddDays(Math.Max(21, Math.Min(60, SeasonDay(0.5, 60)))), p.AddDays(21), MonthPrecision);
            case FarmActivityType.PestControl:
                var pestEnd = Math.Max(30, SeasonDay(0.8, 90));
                return (p.AddDays(21), p.AddDays(pestEnd), p.AddDays(Math.Min(45, pestEnd)), MonthPrecision);
            case FarmActivityType.Harvesting:
                return harvest is { } hv ? (hv, hv.AddDays(14), hv, DayPrecision) : (null, null, null, DayPrecision);
            default:
                return (null, null, null, MonthPrecision);
        }
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th"
    };
}

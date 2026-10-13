using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Diagnoses;
using AgrismartAPI.Application.Farms;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Application.Scans;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Diagnoses;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.MyFarm;

/// <summary>
/// The "My Farm" screen: crop cards, crop calendar, activities, crop health, map and analytics for one of the
/// signed-in farmer's farms. Every method checks the farm belongs to <c>userId</c> and reports another
/// farmer's farm as not found.
/// </summary>
public class MyFarmAppService(
    IMyFarmRepository repository,
    ICropRepository harvestCropRepository,
    IFertilizerCropRepository fertilizerCropRepository,
    ScanAppService scanAppService,
    IFileStorageService fileStorageService,
    TimeProvider timeProvider)
{
    public const string Currency = "SLE";
    private const int MaxPageSize = 100;

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    // ------------------------------------------------------------------ options

    public async Task<MyFarmOptionsDto> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activityTypes = Enum.GetValues<FarmActivityType>()
            .Select(type => new ActivityTypeOptionDto(type, ActivityLabel(type), CropCalendar.StageKeyFor(type)))
            .ToList();

        return new MyFarmOptionsDto(
            FarmRequestValidator.Districts,
            catalog.All.OrderBy(crop => crop.Name).Select(crop => new CropOptionDto(crop.Id, crop.Name, crop.LocalName, crop.TypicalDaysToMaturity)).ToList(),
            activityTypes,
            ["hectare", "acre", "squareMeter"],
            Currency);
    }

    // ------------------------------------------------------------------ overview

    public async Task<FarmOverviewDto> GetOverviewAsync(
        Guid userId, Guid farmId, Guid? calendarCropId, int activitiesLimit, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        var today = Today;

        var ordered = OrderedCrops(farm, catalog, today).ToList();
        var cards = ordered.Select(crop => ToCard(crop, catalog, activities, today)).ToList();

        // Calendar for the requested crop, else the first card (crops in the field come first).
        var calendarCrop = calendarCropId is { } requested ? GetCrop(farm, requested) : ordered.FirstOrDefault();
        var calendar = calendarCrop is null ? null : ToCalendar(calendarCrop, catalog, activities, today, includeActivities: false);

        var pending = activities.Where(activity => activity.Status == FarmActivityStatus.Pending).ToList();
        var upcoming = pending
            .OrderBy(activity => activity.DueDate)
            .Take(Math.Clamp(activitiesLimit, 1, 20))
            .Select(activity => ToActivityDto(activity, farm, catalog, today))
            .ToList();

        var (health, fromFarm) = await LatestHealthAsync(userId, farm, catalog, cancellationToken);

        return new FarmOverviewDto(
            ToHeader(farm, catalog, today),
            cards,
            calendar,
            upcoming,
            pending.Count,
            pending.Count(activity => activity.DueDate < today),
            health,
            fromFarm);
    }

    // ------------------------------------------------------------------ crops

    public async Task<IReadOnlyList<FarmCropCardDto>> GetCropsAsync(
        Guid userId, Guid farmId, FarmCropDisplayStatus? status, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        var today = Today;

        return OrderedCrops(farm, catalog, today)
            .Select(crop => ToCard(crop, catalog, activities, today))
            .Where(card => status is null || card.Status == status)
            .ToList();
    }

    public async Task<FarmCropCardDto> GetCropAsync(Guid userId, Guid farmId, Guid cropId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        return ToCard(crop, catalog, activities, Today);
    }

    public async Task<FarmCropCardDto> AddCropAsync(Guid userId, Guid farmId, FarmCropUpsertRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var today = Today;
        var valid = MyFarmRequestValidator.ValidateCrop(request, catalog, today, out var errors) ?? throw FertilizerRequestValidator.ToException(errors);

        var crop = farm.AddCrop(valid.CropId, valid.PlantingDate, valid.PlotSizeHectares);
        crop.UpdateDetails(
            valid.CropId,
            valid.PlotName ?? NextPlotName(farm),
            valid.Variety,
            valid.PlantingDate,
            valid.ExpectedHarvestDate,
            valid.PlotSizeHectares,
            request.Latitude,
            request.Longitude,
            valid.Boundary,
            request.Notes);
        // Explicit Add: the key is client-generated, so EF would otherwise treat it as an existing row.
        repository.Add(crop);

        var created = request.GenerateActivities ? AddCalendarActivities(userId, farm, crop, catalog.Get(crop.CropId), today) : [];
        await repository.SaveChangesAsync(cancellationToken);

        return ToCard(crop, catalog, created, today);
    }

    public async Task<FarmCropCardDto> UpdateCropAsync(
        Guid userId, Guid farmId, Guid cropId, FarmCropUpsertRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var today = Today;
        var valid = MyFarmRequestValidator.ValidateCrop(request, catalog, today, out var errors) ?? throw FertilizerRequestValidator.ToException(errors);

        var datesChanged = crop.CropId != valid.CropId || crop.PlantingDate != valid.PlantingDate || crop.ExpectedHarvestDate != valid.ExpectedHarvestDate;
        crop.UpdateDetails(
            valid.CropId,
            valid.PlotName ?? crop.PlotName,
            valid.Variety,
            valid.PlantingDate,
            valid.ExpectedHarvestDate,
            valid.PlotSizeHectares,
            request.Latitude,
            request.Longitude,
            valid.Boundary,
            request.Notes);

        var activities = (await repository.GetActivitiesAsync(farmId, cancellationToken)).ToList();
        foreach (var activity in activities.Where(activity => activity.FarmCropId == crop.Id))
        {
            // Keep the crop-key snapshot in step when the farmer corrects the crop.
            activity.Update(activity.FarmCropId, crop.CropId, activity.Type, activity.Title, activity.Notes, activity.DueDate, activity.Cost);
        }

        if (datesChanged && request.GenerateActivities)
        {
            // Re-plan: drop the old calendar tasks that are still open, then add the new ones.
            var stale = activities
                .Where(activity => activity.FarmCropId == crop.Id && activity.Source == FarmActivitySource.Calendar && activity.Status == FarmActivityStatus.Pending)
                .ToList();
            foreach (var activity in stale)
            {
                repository.Remove(activity);
                activities.Remove(activity);
            }

            activities.AddRange(AddCalendarActivities(userId, farm, crop, catalog.Get(crop.CropId), today));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return ToCard(crop, catalog, activities, today);
    }

    /// <summary>Deletes the crop and its open tasks. Completed tasks stay in the farm's history.</summary>
    public async Task DeleteCropAsync(Guid userId, Guid farmId, Guid cropId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);

        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        foreach (var activity in activities.Where(activity => activity.FarmCropId == crop.Id))
        {
            if (activity.Status == FarmActivityStatus.Pending)
            {
                repository.Remove(activity);
            }
            else
            {
                activity.Update(null, activity.CropId, activity.Type, activity.Title, activity.Notes, activity.DueDate, activity.Cost);
            }
        }

        // Loaded so EF clears the crop link itself (the database does too; this also covers test providers).
        await repository.GetScansAsync(farmId, cancellationToken);
        repository.Remove(crop);
        await repository.SaveChangesAsync(cancellationToken);
        fileStorageService.DeleteIfLocal(crop.ImageUrl);
    }

    public async Task<FarmCropCardDto> SetCropImageAsync(
        Guid userId, Guid farmId, Guid cropId, UploadedFile image, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);

        var previous = crop.ImageUrl;
        var imageUrl = await fileStorageService.SaveAsync(image, "farm-crops", cancellationToken);
        crop.SetImage(imageUrl);
        await repository.SaveChangesAsync(cancellationToken);
        fileStorageService.DeleteIfLocal(previous);

        return await GetCropAsync(userId, farmId, cropId, cancellationToken);
    }

    /// <summary>
    /// Marks the crop harvested: open harvest tasks are completed, other open calendar tasks are skipped and,
    /// when a quantity is given, a yield record is added (it also feeds the AI assistant and analytics).
    /// </summary>
    public async Task<FarmCropCardDto> HarvestCropAsync(
        Guid userId, Guid farmId, Guid cropId, HarvestCropRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);
        var today = Today;
        if (!MyFarmRequestValidator.ValidateHarvest(request, today, out var errors))
        {
            throw FertilizerRequestValidator.ToException(errors);
        }

        var harvestedOn = request.HarvestedOn ?? today;
        crop.MarkHarvested(harvestedOn);

        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        foreach (var activity in activities.Where(activity => activity.FarmCropId == crop.Id && activity.Status == FarmActivityStatus.Pending))
        {
            if (activity.Type == FarmActivityType.Harvesting)
            {
                activity.Complete(harvestedOn, null, null);
            }
            else if (activity.Source == FarmActivitySource.Calendar)
            {
                activity.Skip();
            }
        }

        if (request.Quantity is not null)
        {
            repository.Add(FarmRecord.Create(userId, farm.Id, FarmRecordType.Yield, crop.CropId, harvestedOn, request.Quantity, request.Unit,
                request.Notes ?? $"Harvest from {crop.PlotName ?? "plot"}"));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await GetCropAsync(userId, farmId, cropId, cancellationToken);
    }

    /// <summary>Mark a crop failed (open calendar tasks are skipped) or put it back in the field.</summary>
    public async Task<FarmCropCardDto> SetCropStatusAsync(
        Guid userId, Guid farmId, Guid cropId, CropStatusRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);

        switch (request.Status)
        {
            case FarmCropStatusChange.Active:
                crop.Reactivate();
                break;
            case FarmCropStatusChange.Failed:
                crop.MarkFailed();
                var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
                foreach (var activity in activities.Where(activity =>
                             activity.FarmCropId == crop.Id && activity.Source == FarmActivitySource.Calendar && activity.Status == FarmActivityStatus.Pending))
                {
                    activity.Skip();
                }

                break;
            default:
                throw FertilizerRequestValidator.ToException(new Dictionary<string, string[]> { ["status"] = ["status must be active or failed."] });
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await GetCropAsync(userId, farmId, cropId, cancellationToken);
    }

    public async Task<CropCalendarDto> GetCalendarAsync(Guid userId, Guid farmId, Guid cropId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = GetCrop(farm, cropId);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        return ToCalendar(crop, catalog, activities, Today, includeActivities: true);
    }

    // ------------------------------------------------------------------ activities

    public async Task<PagedResult<FarmActivityDto>> GetActivitiesAsync(
        Guid userId, Guid farmId, FarmActivityStatus? status, Guid? farmCropId, FarmActivityType? type, DateOnly? from, DateOnly? to,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        var today = Today;

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var filtered = activities
            .Where(activity => status is null || activity.Status == status)
            .Where(activity => farmCropId is null || activity.FarmCropId == farmCropId)
            .Where(activity => type is null || activity.Type == type)
            .Where(activity => from is null || activity.DueDate >= from)
            .Where(activity => to is null || activity.DueDate <= to)
            // Open tasks first (soonest first), then finished ones (most recent first).
            .OrderBy(activity => activity.Status == FarmActivityStatus.Pending ? 0 : 1)
            .ThenBy(activity => activity.Status == FarmActivityStatus.Pending ? activity.DueDate.DayNumber : -(activity.CompletedOn ?? activity.DueDate).DayNumber)
            .ThenBy(activity => activity.CreatedAt)
            .ToList();

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(activity => ToActivityDto(activity, farm, catalog, today))
            .ToList();

        return new PagedResult<FarmActivityDto>(items, page, pageSize, filtered.Count);
    }

    public async Task<FarmActivityDto> AddActivityAsync(Guid userId, Guid farmId, FarmActivityRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = ValidateActivity(farm, request);

        var activity = FarmActivity.Create(userId, farm.Id, crop?.Id, crop?.CropId, request.Type, request.Title, request.Notes,
            request.DueDate, request.Cost, FarmActivitySource.Manual);
        repository.Add(activity);
        await repository.SaveChangesAsync(cancellationToken);

        return ToActivityDto(activity, farm, await LoadCatalogAsync(cancellationToken), Today);
    }

    public async Task<FarmActivityDto> UpdateActivityAsync(
        Guid userId, Guid farmId, Guid activityId, FarmActivityRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var activity = await GetActivityAsync(farmId, activityId, cancellationToken);
        var crop = ValidateActivity(farm, request);

        activity.Update(crop?.Id, crop?.CropId, request.Type, request.Title,
            request.Notes, request.DueDate, request.Cost);
        await repository.SaveChangesAsync(cancellationToken);

        return ToActivityDto(activity, farm, await LoadCatalogAsync(cancellationToken), Today);
    }

    public async Task<FarmActivityDto> CompleteActivityAsync(
        Guid userId, Guid farmId, Guid activityId, CompleteActivityRequest? request, CancellationToken cancellationToken)
    {
        request ??= new CompleteActivityRequest();
        var today = Today;
        if (!MyFarmRequestValidator.ValidateCompletion(request, today, out var errors))
        {
            throw FertilizerRequestValidator.ToException(errors);
        }

        return await ChangeActivityAsync(userId, farmId, activityId, activity => activity.Complete(request.CompletedOn ?? today, request.Cost, request.Notes),
            cancellationToken);
    }

    public Task<FarmActivityDto> SkipActivityAsync(Guid userId, Guid farmId, Guid activityId, CancellationToken cancellationToken) =>
        ChangeActivityAsync(userId, farmId, activityId, activity => activity.Skip(), cancellationToken);

    public Task<FarmActivityDto> ReopenActivityAsync(Guid userId, Guid farmId, Guid activityId, CancellationToken cancellationToken) =>
        ChangeActivityAsync(userId, farmId, activityId, activity => activity.Reopen(), cancellationToken);

    public async Task DeleteActivityAsync(Guid userId, Guid farmId, Guid activityId, CancellationToken cancellationToken)
    {
        await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var activity = await GetActivityAsync(farmId, activityId, cancellationToken);
        repository.Remove(activity);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ crop health

    public async Task<CropHealthDto> GetHealthAsync(Guid userId, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var scans = await repository.GetScansAsync(farmId, cancellationToken);

        var history = scans.Select(row => ToHealthDto(ToScanDto(row.Scan), row.Link.FarmCropId, farm, catalog)).ToList();
        var (last, fromFarm) = history.Count > 0 ? (history[0], true) : await LatestHealthAsync(userId, farm, catalog, cancellationToken);
        return new CropHealthDto(last, fromFarm, history.Count, history);
    }

    /// <summary>
    /// Runs the existing plant-scan diagnosis on the photo (it also appears in GET /scans) and files it under this
    /// farm and, optionally, one of its crops.
    /// </summary>
    public async Task<CropHealthScanDto> ScanAsync(
        Guid userId, Guid farmId, Guid? farmCropId, UploadedFile image, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var crop = farmCropId is { } id ? GetCrop(farm, id) : null;

        // The plot pin if set, else the farm pin: the diagnosis engine uses it to narrow down local diseases.
        var (latitude, longitude) = crop?.Latitude is not null ? (crop.Latitude, crop.Longitude) : (farm.Latitude, farm.Longitude);
        var scan = await scanAppService.CreateAsync(userId, image.Content, image.FileName, image.ContentType, latitude, longitude, cancellationToken);

        repository.Add(FarmScan.Create(userId, farm.Id, crop?.Id, scan.Id));
        await repository.SaveChangesAsync(cancellationToken);

        return ToHealthDto(scan, crop?.Id, farm, await LoadCatalogAsync(cancellationToken));
    }

    // ------------------------------------------------------------------ map

    public async Task<FarmMapDto> GetMapAsync(Guid userId, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var today = Today;

        var plots = OrderedCrops(farm, catalog, today).Select(crop =>
        {
            var info = catalog.Get(crop.CropId);
            var (harvest, _) = CropCalendar.ExpectedHarvest(crop, info);
            return new FarmMapPlotDto(crop.Id, crop.CropId, info.Name, crop.PlotName, CropCalendar.DisplayStatus(crop, harvest, today),
                crop.PlotSizeHectares, Point(crop.Latitude, crop.Longitude), crop.Boundary.Select(ToDto).ToList());
        }).ToList();

        return new FarmMapDto(farm.Id, farm.Name, farm.District, Point(farm.Latitude, farm.Longitude), farm.Boundary.Select(ToDto).ToList(),
            farm.AreaHectares, plots);
    }

    public async Task<FarmMapDto> UpdateMapAsync(Guid userId, Guid farmId, FarmMapRequest request, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var boundary = MyFarmRequestValidator.ValidateMap(request, out var errors) ?? throw FertilizerRequestValidator.ToException(errors);

        farm.SetMap(request.Latitude, request.Longitude, boundary);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetMapAsync(userId, farmId, cancellationToken);
    }

    // ------------------------------------------------------------------ analytics

    public async Task<FarmAnalyticsDto> GetAnalyticsAsync(Guid userId, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var catalog = await LoadCatalogAsync(cancellationToken);
        var activities = await repository.GetActivitiesAsync(farmId, cancellationToken);
        var scans = await repository.GetScansAsync(farmId, cancellationToken);
        var yields = await repository.GetYieldRecordsAsync(farmId, cancellationToken);
        var today = Today;

        var cards = farm.Crops.Select(crop => ToCard(crop, catalog, activities, today)).ToList();
        int CountStatus(FarmCropDisplayStatus status) => cards.Count(card => card.Status == status);
        var cropCounts = new CropStatusCountsDto(cards.Count, CountStatus(FarmCropDisplayStatus.Planned), CountStatus(FarmCropDisplayStatus.Growing),
            CountStatus(FarmCropDisplayStatus.ReadyToHarvest), CountStatus(FarmCropDisplayStatus.Harvested), CountStatus(FarmCropDisplayStatus.Failed));

        var inField = cards.Where(card => card.Status is FarmCropDisplayStatus.Planned or FarmCropDisplayStatus.Growing or FarmCropDisplayStatus.ReadyToHarvest).ToList();
        var cultivated = Math.Round(inField.Sum(card => card.PlotSizeHectares ?? 0), 4);
        var areaByCrop = inField
            .Where(card => card.PlotSizeHectares is > 0)
            .GroupBy(card => (card.CropId, card.CropName))
            .Select(group =>
            {
                var hectares = Math.Round(group.Sum(card => card.PlotSizeHectares!.Value), 4);
                return new AreaByCropDto(group.Key.CropId, group.Key.CropName, hectares, cultivated > 0 ? Math.Round(hectares / cultivated * 100, 1) : 0);
            })
            .OrderByDescending(item => item.Hectares)
            .ToList();
        var growing = cards.Where(card => card.Status is FarmCropDisplayStatus.Growing or FarmCropDisplayStatus.ReadyToHarvest).ToList();

        var pending = activities.Count(activity => activity.Status == FarmActivityStatus.Pending);
        var completed = activities.Count(activity => activity.Status == FarmActivityStatus.Completed);
        var skipped = activities.Count(activity => activity.Status == FarmActivityStatus.Skipped);
        var activityCounts = new ActivityCountsDto(
            activities.Count,
            pending,
            completed,
            skipped,
            activities.Count(activity => activity.Status == FarmActivityStatus.Pending && activity.DueDate < today),
            activities.Count(activity => activity.Status == FarmActivityStatus.Pending && activity.DueDate >= today && activity.DueDate <= today.AddDays(6)),
            completed + pending == 0 ? 0 : Math.Round(completed * 100.0 / (completed + pending), 1));

        var byType = activities
            .GroupBy(activity => activity.Type)
            .Select(group => new ActivityTypeCountDto(group.Key, group.Count(), group.Count(activity => activity.Status == FarmActivityStatus.Completed),
                SpentOn(group)))
            .OrderBy(item => item.Type)
            .ToList();

        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);
        var monthly = Enumerable.Range(0, 6).Select(offset =>
        {
            var start = firstMonth.AddMonths(offset);
            var end = start.AddMonths(1);
            bool InMonth(DateOnly? date) => date >= start && date < end;
            var doneInMonth = activities.Where(activity => activity.Status == FarmActivityStatus.Completed && InMonth(activity.CompletedOn)).ToList();
            return new MonthlyActivityDto(
                start.ToString("yyyy-MM"),
                activities.Count(activity => activity.Status != FarmActivityStatus.Skipped && InMonth(activity.DueDate)),
                doneInMonth.Count,
                SpentOn(doneInMonth));
        }).ToList();

        var yieldSummaries = yields
            .GroupBy(record => (record.CropId, record.Unit))
            .Select(group => new YieldSummaryDto(group.Key.CropId, catalog.Get(group.Key.CropId).Name, group.Sum(record => record.Quantity ?? 0), group.Key.Unit, group.Count()))
            .OrderBy(item => item.CropName)
            .ToList();

        var healthy = scans.Count(row => row.Scan.Diagnosis.Severity == Severity.Healthy);
        var health = new HealthSummaryDto(
            scans.Count,
            healthy,
            scans.Count - healthy,
            scans.Count == 0 ? 0 : Math.Round(healthy * 100.0 / scans.Count, 1),
            scans.Count == 0 ? null : scans.Max(row => row.Scan.ScannedAt));

        return new FarmAnalyticsDto(
            farm.Id,
            farm.AreaHectares,
            cultivated,
            farm.AreaHectares is > 0 ? Math.Round(cultivated / farm.AreaHectares.Value * 100, 1) : null,
            growing.Count == 0 ? 0 : (int)Math.Round(growing.Average(card => card.ProgressPercent)),
            cropCounts,
            areaByCrop,
            activityCounts,
            byType,
            monthly,
            SpentOn(activities),
            Currency,
            yieldSummaries,
            health);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Farm> GetOwnedFarmAsync(Guid userId, Guid farmId, CancellationToken cancellationToken)
    {
        var farm = await repository.GetFarmAsync(farmId, cancellationToken);
        if (farm is null || farm.UserId != userId)
        {
            throw new NotFoundException($"Farm '{farmId}' was not found.");
        }

        return farm;
    }

    private static FarmCrop GetCrop(Farm farm, Guid cropId) =>
        farm.Crops.FirstOrDefault(crop => crop.Id == cropId) ?? throw new NotFoundException($"Crop '{cropId}' was not found on this farm.");

    private async Task<FarmActivity> GetActivityAsync(Guid farmId, Guid activityId, CancellationToken cancellationToken) =>
        await repository.GetActivityAsync(farmId, activityId, cancellationToken)
        ?? throw new NotFoundException($"Activity '{activityId}' was not found on this farm.");

    private async Task<FarmActivityDto> ChangeActivityAsync(
        Guid userId, Guid farmId, Guid activityId, Action<FarmActivity> change, CancellationToken cancellationToken)
    {
        var farm = await GetOwnedFarmAsync(userId, farmId, cancellationToken);
        var activity = await GetActivityAsync(farmId, activityId, cancellationToken);
        change(activity);
        await repository.SaveChangesAsync(cancellationToken);
        return ToActivityDto(activity, farm, await LoadCatalogAsync(cancellationToken), Today);
    }

    private FarmCrop? ValidateActivity(Farm farm, FarmActivityRequest request)
    {
        MyFarmRequestValidator.ValidateActivity(request, Today, out var errors);
        FarmCrop? crop = null;
        if (request?.FarmCropId is { } farmCropId)
        {
            crop = farm.Crops.FirstOrDefault(c => c.Id == farmCropId);
            if (crop is null)
            {
                errors["farmCropId"] = ["farmCropId must be one of this farm's crops."];
            }
        }

        return errors.Count > 0 ? throw FertilizerRequestValidator.ToException(errors) : crop;
    }

    private List<FarmActivity> AddCalendarActivities(Guid userId, Farm farm, FarmCrop crop, CropInfo info, DateOnly today)
    {
        var created = CropCalendar.PlanActivities(crop, info, today)
            .Select(task => FarmActivity.Create(userId, farm.Id, crop.Id, crop.CropId, task.Type, task.Title, task.Notes, task.DueDate, null,
                FarmActivitySource.Calendar))
            .ToList();
        foreach (var activity in created)
        {
            repository.Add(activity);
        }

        return created;
    }

    private async Task<CropCatalog> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var harvest = await harvestCropRepository.GetAllAsync(cancellationToken);
        var fertilizer = await fertilizerCropRepository.GetAllAsync(cancellationToken);
        return new CropCatalog(harvest, fertilizer);
    }

    private async Task<(CropHealthScanDto? Scan, bool FromFarm)> LatestHealthAsync(
        Guid userId, Farm farm, CropCatalog catalog, CancellationToken cancellationToken)
    {
        var scans = await repository.GetScansAsync(farm.Id, cancellationToken);
        if (scans.Count > 0)
        {
            return (ToHealthDto(ToScanDto(scans[0].Scan), scans[0].Link.FarmCropId, farm, catalog), true);
        }

        var latest = await repository.GetLatestScanForUserAsync(userId, cancellationToken);
        return latest is null ? (null, false) : (ToHealthDto(ToScanDto(latest), null, farm, catalog), false);
    }

    /// <summary>In the field first (by planting date), then planned, then finished crops.</summary>
    private static IEnumerable<FarmCrop> OrderedCrops(Farm farm, CropCatalog catalog, DateOnly today) =>
        farm.Crops
            .OrderBy(crop =>
            {
                var (harvest, _) = CropCalendar.ExpectedHarvest(crop, catalog.Get(crop.CropId));
                return CropCalendar.DisplayStatus(crop, harvest, today) switch
                {
                    FarmCropDisplayStatus.ReadyToHarvest or FarmCropDisplayStatus.Growing => 0,
                    FarmCropDisplayStatus.Planned => 1,
                    _ => 2
                };
            })
            .ThenBy(crop => crop.PlantingDate ?? DateOnly.MaxValue)
            .ThenBy(crop => crop.CreatedAt);

    private static string NextPlotName(Farm farm)
    {
        var used = farm.Crops.Select(crop => crop.PlotName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 1;
        while (used.Contains($"Plot {n}"))
        {
            n++;
        }

        return $"Plot {n}";
    }

    private static FarmHeaderDto ToHeader(Farm farm, CropCatalog catalog, DateOnly today)
    {
        var inField = farm.Crops.Where(crop =>
        {
            var (harvest, _) = CropCalendar.ExpectedHarvest(crop, catalog.Get(crop.CropId));
            return CropCalendar.DisplayStatus(crop, harvest, today) is not (FarmCropDisplayStatus.Harvested or FarmCropDisplayStatus.Failed);
        }).ToList();

        return new FarmHeaderDto(farm.Id, farm.Name, farm.District, farm.AreaHectares, farm.Latitude, farm.Longitude, inField.Count,
            Math.Round(inField.Sum(crop => crop.PlotSizeHectares ?? 0), 4));
    }

    private static FarmCropCardDto ToCard(FarmCrop crop, CropCatalog catalog, IReadOnlyCollection<FarmActivity> activities, DateOnly today)
    {
        var info = catalog.Get(crop.CropId);
        var (harvest, isEstimate) = CropCalendar.ExpectedHarvest(crop, info);
        var status = CropCalendar.DisplayStatus(crop, harvest, today);
        int? daysToHarvest = harvest is { } h && status is FarmCropDisplayStatus.Planned or FarmCropDisplayStatus.Growing or FarmCropDisplayStatus.ReadyToHarvest
            ? h.DayNumber - today.DayNumber
            : null;

        return new FarmCropCardDto(
            crop.Id,
            crop.FarmId,
            crop.CropId,
            info.Name,
            info.LocalName,
            crop.PlotName,
            crop.Variety,
            crop.PlotSizeHectares,
            crop.PlantingDate,
            harvest,
            isEstimate,
            daysToHarvest,
            status,
            CropCalendar.ProgressPercent(crop, harvest, today),
            CropCalendar.GrowthStage(crop, info, harvest, today),
            crop.HarvestedOn,
            crop.ImageUrl,
            Point(crop.Latitude, crop.Longitude),
            crop.Boundary.Select(ToDto).ToList(),
            crop.Notes,
            activities.Count(activity => activity.FarmCropId == crop.Id && activity.Status == FarmActivityStatus.Pending),
            crop.CreatedAt);
    }

    private static CropCalendarDto ToCalendar(
        FarmCrop crop, CropCatalog catalog, IReadOnlyCollection<FarmActivity> activities, DateOnly today, bool includeActivities)
    {
        var info = catalog.Get(crop.CropId);
        var forCrop = activities.Where(activity => activity.FarmCropId == crop.Id).ToList();
        var (stages, current) = CropCalendar.Stages(crop, info, forCrop, today);
        var activityDtos = includeActivities
            ? forCrop.OrderBy(activity => activity.DueDate).Select(activity => ToActivityDto(activity, crop, info, today)).ToList()
            : [];

        return new CropCalendarDto(crop.Id, crop.CropId, info.Name, crop.PlotName, crop.PlotSizeHectares, current, stages, activityDtos);
    }

    private static FarmActivityDto ToActivityDto(FarmActivity activity, Farm farm, CropCatalog catalog, DateOnly today)
    {
        var crop = activity.FarmCropId is { } id ? farm.Crops.FirstOrDefault(c => c.Id == id) : null;
        return ToActivityDto(activity, crop, catalog.Get(crop?.CropId ?? activity.CropId), today, activity.CropId is null && crop is null);
    }

    private static FarmActivityDto ToActivityDto(FarmActivity activity, FarmCrop? crop, CropInfo info, DateOnly today, bool noCrop = false)
    {
        var dueInDays = activity.DueDate.DayNumber - today.DayNumber;
        return new FarmActivityDto(
            activity.Id,
            activity.FarmId,
            activity.FarmCropId,
            activity.CropId,
            noCrop ? null : info.Name,
            crop?.PlotName,
            activity.Type,
            activity.Title,
            activity.Notes,
            activity.DueDate,
            dueInDays,
            activity.Status == FarmActivityStatus.Pending && dueInDays < 0,
            activity.Status,
            activity.CompletedOn,
            activity.Cost,
            activity.Source,
            activity.CreatedAt);
    }

    private static PlantScanDto ToScanDto(Domain.Scans.PlantScan scan) =>
        new(scan.Id, scan.ImagePath, DiagnosisMapper.ToDto(scan.Diagnosis), scan.ScannedAt);

    private static CropHealthScanDto ToHealthDto(PlantScanDto scan, Guid? farmCropId, Farm farm, CropCatalog catalog)
    {
        var diagnosis = scan.Diagnosis;
        var crop = farmCropId is { } id ? farm.Crops.FirstOrDefault(c => c.Id == id) : null;
        var isHealthy = diagnosis.Severity == Severity.Healthy;
        var headline = diagnosis.Severity switch
        {
            Severity.Healthy => "No major disease detected",
            Severity.Low => $"Minor issue: {diagnosis.DiseaseName}",
            _ => $"{diagnosis.DiseaseName} detected"
        };

        return new CropHealthScanDto(
            scan.Id,
            scan.ImagePath,
            scan.ScannedAt,
            crop?.Id,
            crop is null ? null : catalog.Get(crop.CropId).Name,
            diagnosis.PlantName,
            diagnosis.DiseaseName,
            diagnosis.Severity,
            isHealthy,
            (int)Math.Round(diagnosis.Confidence * 100),
            headline,
            diagnosis.Summary,
            diagnosis.TreatmentSteps);
    }

    private static decimal SpentOn(IEnumerable<FarmActivity> activities) =>
        activities.Where(activity => activity.Status == FarmActivityStatus.Completed).Sum(activity => activity.Cost ?? 0);

    private static GeoPointDto? Point(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lng ? new GeoPointDto(lat, lng) : null;

    private static GeoPointDto ToDto(GeoPoint point) => new(point.Latitude, point.Longitude);

    private static string ActivityLabel(FarmActivityType type) => type switch
    {
        FarmActivityType.LandPreparation => "Land preparation",
        FarmActivityType.Planting => "Planting",
        FarmActivityType.Fertilizing => "Fertilizing",
        FarmActivityType.Weeding => "Weeding",
        FarmActivityType.PestControl => "Pest control",
        FarmActivityType.Harvesting => "Harvesting",
        FarmActivityType.Irrigation => "Irrigation",
        FarmActivityType.Scouting => "Scouting",
        _ => "Other"
    };
}

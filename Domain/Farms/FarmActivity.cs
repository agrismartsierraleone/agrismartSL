using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Farms;

/// <summary>A task on the farm (fertilize, weed, scout for pests...), optionally tied to one crop plot.</summary>
public class FarmActivity : Entity
{
    public const int MaxTitleLength = 120;
    public const int MaxNotesLength = 1000;

    public Guid UserId { get; private init; }
    public Guid FarmId { get; private init; }

    /// <summary>The crop plot this is for. Cleared if the crop is deleted; <see cref="CropId"/> keeps the crop key.</summary>
    public Guid? FarmCropId { get; private set; }

    /// <summary>Crop key snapshot, e.g. "rice-lowland", kept for history after the crop is removed.</summary>
    public string? CropId { get; private set; }

    public FarmActivityType Type { get; private set; }
    public string Title { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly DueDate { get; private set; }
    public FarmActivityStatus Status { get; private set; }
    public DateOnly? CompletedOn { get; private set; }

    /// <summary>Money spent on this task, in Leones (SLE). Optional; feeds the analytics tab.</summary>
    public decimal? Cost { get; private set; }

    public FarmActivitySource Source { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private FarmActivity(
        Guid userId, Guid farmId, Guid? farmCropId, string? cropId, FarmActivityType type, string title, string? notes,
        DateOnly dueDate, decimal? cost, FarmActivitySource source)
    {
        UserId = userId;
        FarmId = farmId;
        FarmCropId = farmCropId;
        CropId = cropId;
        Type = type;
        Title = title;
        Notes = notes;
        DueDate = dueDate;
        Cost = cost;
        Source = source;
        Status = FarmActivityStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private FarmActivity()
    {
        Title = null!;
    }

    public static FarmActivity Create(
        Guid userId, Guid farmId, Guid? farmCropId, string? cropId, FarmActivityType type, string title, string? notes,
        DateOnly dueDate, decimal? cost, FarmActivitySource source)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (farmId == Guid.Empty)
        {
            throw new ArgumentException("Farm id is required.", nameof(farmId));
        }

        Guard(title, cost);
        return new FarmActivity(userId, farmId, farmCropId, CleanKey(cropId), type, title.Trim(), Clean(notes), dueDate, cost, source);
    }

    public void Update(Guid? farmCropId, string? cropId, FarmActivityType type, string title, string? notes, DateOnly dueDate, decimal? cost)
    {
        Guard(title, cost);
        FarmCropId = farmCropId;
        CropId = CleanKey(cropId);
        Type = type;
        Title = title.Trim();
        Notes = Clean(notes);
        DueDate = dueDate;
        Cost = cost;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <param name="cost">Replaces the cost when given; otherwise the planned cost is kept.</param>
    /// <param name="notes">Replaces the notes when given.</param>
    public void Complete(DateOnly completedOn, decimal? cost, string? notes)
    {
        if (cost is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }

        Status = FarmActivityStatus.Completed;
        CompletedOn = completedOn;
        Cost = cost ?? Cost;
        Notes = Clean(notes) ?? Notes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Skip()
    {
        Status = FarmActivityStatus.Skipped;
        CompletedOn = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reopen()
    {
        Status = FarmActivityStatus.Pending;
        CompletedOn = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Guard(string title, decimal? cost)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (cost is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

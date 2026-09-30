using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

public enum FarmRecordType
{
    Planting,
    FertilizerApplication,
    Yield,
    Problem,
    Other
}

/// <summary>Something that happened on the farm: fertilizer applied, a harvest weighed, a problem seen.</summary>
public class FarmRecord : Entity
{
    public Guid UserId { get; private init; }
    public Guid? FarmId { get; private init; }
    public FarmRecordType Type { get; private init; }
    public string? CropId { get; private init; }
    public DateOnly RecordDate { get; private init; }
    public double? Quantity { get; private init; }

    /// <summary>Unit for <see cref="Quantity"/>, e.g. "kg", "bags", "bushels".</summary>
    public string? Unit { get; private init; }

    public string? Notes { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private FarmRecord(
        Guid userId, Guid? farmId, FarmRecordType type, string? cropId, DateOnly recordDate, double? quantity, string? unit, string? notes)
    {
        UserId = userId;
        FarmId = farmId;
        Type = type;
        CropId = cropId;
        RecordDate = recordDate;
        Quantity = quantity;
        Unit = unit;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private FarmRecord()
    {
    }

    public static FarmRecord Create(
        Guid userId, Guid? farmId, FarmRecordType type, string? cropId, DateOnly recordDate, double? quantity, string? unit, string? notes)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new FarmRecord(
            userId, farmId, type,
            string.IsNullOrWhiteSpace(cropId) ? null : cropId.Trim().ToLowerInvariant(),
            recordDate, quantity,
            string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }
}

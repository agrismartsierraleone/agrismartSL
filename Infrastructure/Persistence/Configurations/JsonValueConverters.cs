using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

/// <summary>Stores a small value-object list (e.g. treatment steps) as a JSON column instead of a separate owned table.</summary>
public static class JsonValueConverters
{
    public static ValueConverter<IReadOnlyList<T>, string> ForList<T>() => new(
        list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<T>>(json, (JsonSerializerOptions?)null) ?? new List<T>());

    public static ValueComparer<IReadOnlyList<T>> ListComparer<T>() => new(
        (a, b) => (a ?? new List<T>()).SequenceEqual(b ?? new List<T>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => (IReadOnlyList<T>)list.ToList());
}

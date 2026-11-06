using System.Text.RegularExpressions;

namespace AgrismartAPI.Domain.Logistics;

public record District(string Name, double Latitude, double Longitude);

/// <summary>
/// Sierra Leone's districts, each located at its headquarter town. The coordinates are approximate and only
/// used to estimate distances between districts, so they are not suitable for navigation.
/// </summary>
public static class SierraLeoneDistricts
{
    public static readonly IReadOnlyList<District> All =
    [
        new("Bo", 7.96, -11.74),
        new("Bombali", 8.89, -12.04),
        new("Bonthe", 7.53, -12.50),
        new("Falaba", 9.40, -11.30),
        new("Kailahun", 8.28, -10.57),
        new("Kambia", 9.13, -12.92),
        new("Karene", 9.50, -12.24),
        new("Kenema", 7.88, -11.19),
        new("Koinadugu", 9.59, -11.55),
        new("Kono", 8.64, -10.97),
        new("Moyamba", 8.16, -12.43),
        new("Port Loko", 8.77, -12.79),
        new("Pujehun", 7.35, -11.72),
        new("Tonkolili", 8.72, -11.95),
        new("Western Area Rural", 8.34, -13.07),
        new("Western Area Urban", 8.48, -13.23)
    ];

    // Well-known town names people use instead of the district name.
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Freetown"] = "Western Area Urban",
        ["Waterloo"] = "Western Area Rural",
        ["Makeni"] = "Bombali",
        ["Koidu"] = "Kono",
        ["Kabala"] = "Koinadugu",
        ["Magburaka"] = "Tonkolili"
    };

    /// <summary>Finds the district named in free text such as "Bo", "Kenema town" or "Freetown". Null when there is none.</summary>
    public static District? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Longest first, and on word boundaries, so "Bo" never matches inside "Bombali" or "Bonthe".
        foreach (var name in All.Select(district => district.Name).Concat(Aliases.Keys).OrderByDescending(name => name.Length))
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase))
            {
                var canonical = Aliases.GetValueOrDefault(name, name);
                return All.First(district => district.Name.Equals(canonical, StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    /// <summary>Straight-line distance in kilometres (haversine formula).</summary>
    public static double StraightLineKm(District from, District to)
    {
        const double earthRadiusKm = 6371;
        var latitudeDelta = ToRadians(to.Latitude - from.Latitude);
        var longitudeDelta = ToRadians(to.Longitude - from.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
                + Math.Cos(ToRadians(from.Latitude)) * Math.Cos(ToRadians(to.Latitude)) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

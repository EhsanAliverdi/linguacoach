using System.Text.Json;

namespace LinguaCoach.Domain.ValueObjects;

public static class JsonStringList
{
    public static IReadOnlyList<string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string Merge(string? currentJson, IEnumerable<string>? additions, int cap)
    {
        var merged = new List<string>();
        AddUnique(merged, Read(currentJson));
        AddUnique(merged, additions ?? []);
        return JsonSerializer.Serialize(merged.Take(cap).ToList());
    }

    public static string Replace(IEnumerable<string>? values, int cap)
    {
        var next = new List<string>();
        AddUnique(next, values ?? []);
        return JsonSerializer.Serialize(next.Take(cap).ToList());
    }

    private static void AddUnique(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var cleaned = value?.Trim();
            if (string.IsNullOrWhiteSpace(cleaned)) continue;
            if (target.Any(x => string.Equals(x, cleaned, StringComparison.OrdinalIgnoreCase))) continue;
            target.Add(cleaned);
        }
    }
}

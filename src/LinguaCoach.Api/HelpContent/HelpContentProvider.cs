using System.Reflection;
using System.Text.Json;

namespace LinguaCoach.Api.HelpContent;

/// <summary>
/// Static, dev-authored admin help copy (HTML strings keyed by a dotted key, e.g.
/// "admin.skillGraph.sweepUntaggedModules"). Backed by an embedded JSON resource — not the
/// database, since this content is never edited at runtime, only added via code changes. Loaded
/// once and cached for the process lifetime.
/// </summary>
public interface IHelpContentProvider
{
    IReadOnlyDictionary<string, string> GetAll();
}

public sealed class HelpContentProvider : IHelpContentProvider
{
    private readonly Lazy<IReadOnlyDictionary<string, string>> _content;

    public HelpContentProvider()
    {
        _content = new Lazy<IReadOnlyDictionary<string, string>>(Load);
    }

    public IReadOnlyDictionary<string, string> GetAll() => _content.Value;

    private static IReadOnlyDictionary<string, string> Load()
    {
        var assembly = typeof(HelpContentProvider).Assembly;
        const string resourceName = "LinguaCoach.Api.HelpContent.help-content.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException("help-content.json deserialized to null.");
        return map;
    }
}

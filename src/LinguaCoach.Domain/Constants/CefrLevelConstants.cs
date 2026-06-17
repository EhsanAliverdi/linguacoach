namespace LinguaCoach.Domain.Constants;

/// <summary>
/// Canonical CEFR level string values used by curriculum and placement.
/// StudentProfile.CefrLevel stores one of these strings.
/// Plus/sub-levels (B2+) are not modelled here — see TODOS.md.
/// </summary>
public static class CefrLevelConstants
{
    public const string A1 = "A1";
    public const string A2 = "A2";
    public const string B1 = "B1";
    public const string B2 = "B2";
    public const string C1 = "C1";
    public const string C2 = "C2";

    public static readonly IReadOnlyList<string> All = [A1, A2, B1, B2, C1, C2];

    public static bool IsValid(string? level) =>
        level is not null && All.Contains(level, StringComparer.OrdinalIgnoreCase);
}

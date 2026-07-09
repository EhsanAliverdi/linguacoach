namespace LinguaCoach.Domain.Constants;

/// <summary>
/// Deterministic CEFR level → difficulty band (1-5) mapping, shared so the bank-metadata depth
/// enrichment (Phase E10) and the Today selector's runtime difficulty derivation (Phase D6) stay on
/// one scale. Aligned with the E8 passage authoring convention (A1=1, A2=2, B1=3, B2=4) and extended
/// to C1/C2=5. Returns null for any unrecognized level so nothing indefensible is derived.
/// </summary>
public static class CefrDifficultyBand
{
    public const int Min = 1;
    public const int Max = 5;

    public static int? FromCefr(string? cefrLevel) =>
        (cefrLevel?.Trim().ToUpperInvariant()) switch
        {
            "A1" => 1,
            "A2" => 2,
            "B1" => 3,
            "B2" => 4,
            "C1" or "C2" => 5,
            _ => null,
        };

    /// <summary>Clamps a derived band into the valid 1-5 range.</summary>
    public static int Clamp(int band) => band < Min ? Min : band > Max ? Max : band;
}

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Well-known GUIDs for seed rows. Fixed so migrations are deterministic.
/// </summary>
internal static class SeedData
{
    internal static readonly Guid PersianId = new("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid EnglishId = new("10000000-0000-0000-0000-000000000002");
    internal static readonly Guid FaEnPairId = new("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid WorkplaceEnglishTrackId = new("30000000-0000-0000-0000-000000000001");
    internal static readonly Guid DocumentControllerProfileId = new("40000000-0000-0000-0000-000000000001");

    internal static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}

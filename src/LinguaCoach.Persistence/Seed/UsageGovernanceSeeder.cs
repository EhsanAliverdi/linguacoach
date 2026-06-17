using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds feature definitions and default usage policies idempotently.
/// Only inserts missing records — never overwrites admin edits.
/// </summary>
public static class UsageGovernanceSeeder
{
    public static async Task SeedAsync(LinguaCoachDbContext db, CancellationToken ct = default)
    {
        await SeedFeatureDefinitionsAsync(db, ct);
        await SeedUsagePoliciesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFeatureDefinitionsAsync(LinguaCoachDbContext db, CancellationToken ct)
    {
        var existing = await db.FeatureDefinitions.Select(f => f.Key).ToHashSetAsync(ct);

        var definitions = new List<FeatureDefinition>
        {
            // Prepared learning — TrackOnly, not expensive
            new("lesson.view",            "View Lesson",                    "Student views a lesson",                      FeatureCategory.PreparedLearning, EnforcementMode.TrackOnly, UsageUnitType.Count, false, true,  true),
            new("lesson.complete",        "Complete Lesson",                "Student completes a lesson",                  FeatureCategory.PreparedLearning, EnforcementMode.TrackOnly, UsageUnitType.Count, false, true,  true),
            new("practice.prepared.complete", "Complete Prepared Practice", "Student completes a prepared practice item",  FeatureCategory.PreparedLearning, EnforcementMode.TrackOnly, UsageUnitType.Count, false, true,  true),
            new("tts.replay",             "Replay TTS Audio",               "Student replays cached TTS audio",            FeatureCategory.PreparedLearning, EnforcementMode.TrackOnly, UsageUnitType.Count, false, true,  true),

            // Dynamic AI — TrackOnly by default, can be limited
            new("lesson.generate",        "Generate Lesson",                "AI generates a new lesson",                   FeatureCategory.DynamicAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  false, true),
            new("lesson.regenerate",      "Regenerate Lesson",              "AI regenerates an existing lesson",           FeatureCategory.DynamicAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  false, true),
            new("practice.dynamic.generate", "Generate Dynamic Practice",   "AI generates a dynamic practice item",       FeatureCategory.DynamicAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  false, true),
            new("learning_path.generate", "Generate Learning Path",         "AI generates a learning path",               FeatureCategory.DynamicAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  false, true),
            new("learning_path.regenerate","Regenerate Learning Path",      "AI regenerates a learning path",             FeatureCategory.DynamicAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  false, true),

            // Expensive AI — hard-limitable
            new("writing.evaluate",       "Evaluate Writing",               "AI evaluates a writing submission",           FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  true,  true),
            new("speaking.evaluate",      "Evaluate Speaking",              "AI evaluates a speaking submission",          FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  true,  true),
            new("speaking.live_session",  "Live Speaking Session",          "Real-time AI speaking conversation",          FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Minutes, true, true,  true),
            new("tts.generate",           "Generate TTS Audio",             "AI generates text-to-speech audio",           FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Characters, true, false, true),
            new("stt.transcribe",         "Transcribe Speech",              "AI transcribes student speech input",         FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Seconds, true, false, true),
            new("placement.start",        "Start Placement Assessment",     "Student begins CEFR placement",               FeatureCategory.ExpensiveAi, EnforcementMode.TrackOnly, UsageUnitType.Count,  true,  true,  true),
            new("placement.reset",        "Reset Placement Assessment",     "Admin resets student placement",              FeatureCategory.AdminAction, EnforcementMode.TrackOnly, UsageUnitType.Count,  false, false, true),
        };

        foreach (var def in definitions.Where(d => !existing.Contains(d.Key)))
            db.FeatureDefinitions.Add(def);
    }

    private static async Task SeedUsagePoliciesAsync(LinguaCoachDbContext db, CancellationToken ct)
    {
        var existingPolicies = await db.UsagePolicies.Include(p => p.Rules).ToListAsync(ct);
        var existingNames = existingPolicies.Select(p => p.Name).ToHashSet();

        // Default pilot policy — track only, no limits
        if (!existingNames.Contains("Default Pilot Student"))
        {
            var policy = new UsagePolicy(
                "Default Pilot Student",
                "Default policy for pilot students. Tracks all features. No hard limits.",
                UsagePolicyScopeType.Global,
                isDefault: true,
                isActive: true);

            db.UsagePolicies.Add(policy);
            await db.SaveChangesAsync(ct); // need id for rules

            SeedRulesTrackOnly(db, policy.Id, new[]
            {
                "lesson.view", "lesson.complete", "lesson.generate", "lesson.regenerate",
                "practice.prepared.complete", "practice.dynamic.generate",
                "writing.evaluate", "speaking.evaluate", "tts.generate", "tts.replay",
                "stt.transcribe", "placement.start", "placement.reset",
                "learning_path.generate", "learning_path.regenerate"
            });
        }

        // Low cost policy — limits expensive AI
        if (!existingNames.Contains("Low Cost Student"))
        {
            var policy = new UsagePolicy(
                "Low Cost Student",
                "Limits expensive AI features. Prepared learning remains unlimited.",
                UsagePolicyScopeType.Student,
                isDefault: false,
                isActive: true);

            db.UsagePolicies.Add(policy);
            await db.SaveChangesAsync(ct);

            SeedRulesTrackOnly(db, policy.Id, new[] { "lesson.view", "lesson.complete", "practice.prepared.complete", "tts.replay" });

            // Limited expensive AI — 5 per day
            AddRule(db, policy.Id, "writing.evaluate",      EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 5,  monthlyLimit: 50);
            AddRule(db, policy.Id, "speaking.evaluate",     EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 5,  monthlyLimit: 50);
            AddRule(db, policy.Id, "tts.generate",          EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 20, monthlyLimit: 200);
            AddRule(db, policy.Id, "lesson.generate",       EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 3,  monthlyLimit: 30);
            AddRule(db, policy.Id, "lesson.regenerate",     EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 3,  monthlyLimit: 30);
            AddRule(db, policy.Id, "practice.dynamic.generate", EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 5, monthlyLimit: 50);
            AddRule(db, policy.Id, "learning_path.generate",    EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 1, monthlyLimit: 5);
            AddRule(db, policy.Id, "learning_path.regenerate",  EnforcementMode.HardLimit, UsageUnitType.Count, dailyLimit: 1, monthlyLimit: 5);
            AddRule(db, policy.Id, "stt.transcribe",        EnforcementMode.TrackOnly, UsageUnitType.Seconds);
            AddRule(db, policy.Id, "placement.start",       EnforcementMode.TrackOnly, UsageUnitType.Count);
        }

        // Test unlimited policy
        if (!existingNames.Contains("Test Unlimited"))
        {
            var policy = new UsagePolicy(
                "Test Unlimited",
                "No limits. For QA and internal test accounts.",
                UsagePolicyScopeType.Student,
                isDefault: false,
                isActive: true);

            db.UsagePolicies.Add(policy);
            await db.SaveChangesAsync(ct);

            SeedRulesTrackOnly(db, policy.Id, new[]
            {
                "lesson.view", "lesson.complete", "lesson.generate", "lesson.regenerate",
                "practice.prepared.complete", "practice.dynamic.generate",
                "writing.evaluate", "speaking.evaluate", "tts.generate", "tts.replay",
                "stt.transcribe", "placement.start", "placement.reset",
                "learning_path.generate", "learning_path.regenerate"
            });
        }
    }

    private static void SeedRulesTrackOnly(LinguaCoachDbContext db, Guid policyId, string[] featureKeys)
    {
        foreach (var key in featureKeys)
            AddRule(db, policyId, key, EnforcementMode.TrackOnly, UsageUnitType.Count);
    }

    private static void AddRule(
        LinguaCoachDbContext db,
        Guid policyId,
        string featureKey,
        EnforcementMode mode,
        UsageUnitType unitType,
        long? dailyLimit = null,
        long? weeklyLimit = null,
        long? monthlyLimit = null,
        decimal? dailyCostLimit = null,
        decimal? monthlyCostLimit = null)
    {
        db.UsagePolicyRules.Add(new UsagePolicyRule(
            policyId, featureKey,
            trackingEnabled: true,
            mode,
            unitType,
            dailyLimit, weeklyLimit, monthlyLimit,
            dailyCostLimit, monthlyCostLimit,
            warningThresholdPercent: 80,
            isActive: true));
    }
}

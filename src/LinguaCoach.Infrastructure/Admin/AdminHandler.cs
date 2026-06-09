using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminHandler :
    IAdminStudentQuery,
    IAdminPromptHandler,
    IAdminCurriculumHandler,
    IAdminAiConfigHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAiProviderTester _tester;

    public AdminHandler(LinguaCoachDbContext db, UserManager<ApplicationUser> userManager, IAiProviderTester tester)
    {
        _db = db;
        _userManager = userManager;
        _tester = tester;
    }

    // ── Students ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StudentListItem>> ListStudentsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var query = _db.StudentProfiles.AsQueryable();
        if (!includeArchived)
            query = query.Where(p => p.LifecycleStage != StudentLifecycleStage.Archived);

        var profiles = await query
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        // Batch-load all Identity users matching the student profile user IDs.
        var userIds = profiles.Select(p => p.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        return profiles
            .Where(p => users.ContainsKey(p.UserId))
            .Select(p => ToStudentListItem(p, users[p.UserId].Email ?? string.Empty))
            .ToList();
    }

    public async Task<StudentListItem> UpdateStudentAsync(UpdateStudentProfileCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.UpdateAdminProfile(
            command.FirstName,
            command.LastName,
            command.DisplayName,
            command.CareerContext,
            command.LearningGoal,
            command.LearningGoalDescription,
            command.DifficultSituationsText,
            command.PreferredSessionDurationMinutes,
            command.ProfessionalExperienceLevel,
            command.RoleFamiliarity);

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    public async Task<StudentListItem> ArchiveStudentAsync(ArchiveStudentCommand command, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.Id == command.StudentProfileId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == profile.UserId, ct)
            ?? throw new InvalidOperationException("Student user not found.");

        profile.SetLifecycleStage(StudentLifecycleStage.Archived);
        user.EmailConfirmed = false;

        await _db.SaveChangesAsync(ct);
        return ToStudentListItem(profile, user.Email ?? string.Empty);
    }

    private static StudentListItem ToStudentListItem(StudentProfile p, string email)
        => new(
            p.Id,
            p.UserId,
            email,
            p.FirstName,
            p.LastName,
            p.DisplayName,
            p.OnboardingStatus.ToString(),
            p.LifecycleStage.ToString(),
            p.CefrLevel,
            p.CareerContext,
            p.LearningGoal,
            p.LearningGoalDescription,
            p.DifficultSituationsText,
            p.PreferredSessionDurationMinutes,
            p.ProfessionalExperienceLevel,
            p.RoleFamiliarity,
            p.CreatedAt);

    // ── Prompt templates ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PromptTemplateItem>> ListPromptsAsync(CancellationToken ct = default)
    {
        var prompts = await _db.AiPrompts
            .OrderBy(p => p.Key).ThenByDescending(p => p.Version)
            .ToListAsync(ct);

        return prompts.Select(p => new PromptTemplateItem(
            p.Id, p.Key, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens))
            .ToList();
    }

    public async Task<PromptTemplateDetail> GetPromptAsync(Guid promptId, CancellationToken ct = default)
    {
        var p = await _db.AiPrompts.FirstOrDefaultAsync(x => x.Id == promptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        return new PromptTemplateDetail(p.Id, p.Key, p.Content, p.Version, p.IsActive, p.MaxInputTokens, p.MaxOutputTokens);
    }

    public async Task<PromptTemplateDetail> CreateVersionAsync(CreatePromptVersionCommand command, CancellationToken ct = default)
    {
        var latestVersion = await _db.AiPrompts
            .Where(p => p.Key == command.Key)
            .MaxAsync(p => (int?)p.Version, ct) ?? 0;

        var newPrompt = new AiPrompt(
            command.Key,
            command.Content,
            version: latestVersion + 1,
            maxInputTokens: command.MaxInputTokens,
            maxOutputTokens: command.MaxOutputTokens);

        _db.AiPrompts.Add(newPrompt);
        await _db.SaveChangesAsync(ct);

        return new PromptTemplateDetail(
            newPrompt.Id, newPrompt.Key, newPrompt.Content,
            newPrompt.Version, newPrompt.IsActive,
            newPrompt.MaxInputTokens, newPrompt.MaxOutputTokens);
    }

    public async Task ActivateAsync(ActivatePromptCommand command, CancellationToken ct = default)
    {
        var prompt = await _db.AiPrompts.FirstOrDefaultAsync(p => p.Id == command.PromptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        prompt.Activate();
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(DeactivatePromptCommand command, CancellationToken ct = default)
    {
        var prompt = await _db.AiPrompts.FirstOrDefaultAsync(p => p.Id == command.PromptId, ct)
            ?? throw new InvalidOperationException("Prompt template not found.");
        prompt.Deactivate();
        await _db.SaveChangesAsync(ct);
    }

    // ── Curriculum ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CareerProfileItem>> ListCareerProfilesAsync(CancellationToken ct = default)
    {
        var profiles = await _db.CareerProfiles.OrderBy(c => c.Name).ToListAsync(ct);
        return profiles.Select(c => new CareerProfileItem(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<CurriculumWordItem>> ListWordsAsync(
        Guid careerProfileId, Guid languagePairId, CancellationToken ct = default)
    {
        var words = await _db.CurriculumWordLists
            .Where(w => w.CareerProfileId == careerProfileId && w.LanguagePairId == languagePairId)
            .OrderBy(w => w.Priority)
            .ToListAsync(ct);

        return words.Select(w => new CurriculumWordItem(
            w.Id, w.Word, w.Definition, w.ExampleSentence, w.Priority, w.Tags)).ToList();
    }

    public async Task<CurriculumWordItem> AddWordAsync(AddCurriculumWordCommand command, CancellationToken ct = default)
    {
        var word = new CurriculumWordList(
            command.CareerProfileId,
            command.LanguagePairId,
            command.Word,
            command.Definition,
            command.ExampleSentence,
            command.Priority,
            command.Tags);

        _db.CurriculumWordLists.Add(word);
        await _db.SaveChangesAsync(ct);

        return new CurriculumWordItem(word.Id, word.Word, word.Definition, word.ExampleSentence, word.Priority, word.Tags);
    }

    public async Task<CurriculumWordItem> UpdateWordAsync(UpdateCurriculumWordCommand command, CancellationToken ct = default)
    {
        var word = await _db.CurriculumWordLists.FirstOrDefaultAsync(w => w.Id == command.WordId, ct)
            ?? throw new InvalidOperationException("Curriculum word not found.");

        // CurriculumWordList is an append-only entity; use a new instance approach
        // by updating via EF shadow setters isn't available — call an Update method.
        // We'll add an Update method to the domain entity.
        word.UpdateDetails(command.Definition, command.ExampleSentence, command.Priority, command.Tags);
        await _db.SaveChangesAsync(ct);

        return new CurriculumWordItem(word.Id, word.Word, word.Definition, word.ExampleSentence, word.Priority, word.Tags);
    }

    // ── AI provider config ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiProviderConfigItem>> ListConfigsAsync(CancellationToken ct = default)
    {
        await EnsureActiveFeatureConfigsAsync(ct);
        var configs = await _db.AiProviderConfigs.OrderBy(c => c.FeatureKey).ToListAsync(ct);
        return configs.Select(ToAiProviderConfigItem).ToList();
    }

    public async Task<IReadOnlyList<AiProviderCatalogItem>> ListProvidersAsync(CancellationToken ct = default)
    {
        var credentials = await _db.AiProviderCredentials.ToListAsync(ct);
        var credByProvider = credentials.ToDictionary(c => c.ProviderName, StringComparer.OrdinalIgnoreCase);

        return AiProviderConfig.AllowedModels
            .Select(kvp =>
            {
                credByProvider.TryGetValue(kvp.Key, out var cred);
                return ToCatalogItem(kvp.Key, kvp.Value.Order().ToList(), cred);
            })
            .OrderBy(p => p.ProviderName)
            .ToList();
    }

    public async Task<AiProviderConfigItem> UpdateConfigAsync(UpdateAiProviderConfigCommand command, CancellationToken ct = default)
    {
        var config = await _db.AiProviderConfigs.FirstOrDefaultAsync(c => c.Id == command.ConfigId, ct)
            ?? throw new InvalidOperationException("AI provider config not found.");

        if (!string.IsNullOrWhiteSpace(command.ProviderName) || !string.IsNullOrWhiteSpace(command.ModelName))
        {
            config.Update(
                command.ProviderName ?? config.ProviderName,
                command.ModelName ?? config.ModelName);
        }

        if (command.FallbackEnabled.HasValue
            || command.FallbackProviderName is not null
            || command.FallbackModelName is not null)
        {
            config.SetFallback(
                command.FallbackProviderName ?? config.FallbackProviderName,
                command.FallbackModelName ?? config.FallbackModelName,
                command.FallbackEnabled ?? config.FallbackEnabled);
        }

        await _db.SaveChangesAsync(ct);

        return ToAiProviderConfigItem(config);
    }

    private async Task EnsureActiveFeatureConfigsAsync(CancellationToken ct)
    {
        var activePromptKeys = await _db.AiPrompts
            .Where(p => p.IsActive)
            .Select(p => p.Key)
            .Distinct()
            .ToListAsync(ct);

        var featureKeys = activePromptKeys
            .Select(DeriveFeatureKey)
            .Append("writing.exercise")
            .Concat(KnownRuntimeFeatureKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(k => k.ToLowerInvariant())
            .ToList();

        var existing = await _db.AiProviderConfigs
            .Select(c => c.FeatureKey)
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in featureKeys.Where(k => !existingSet.Contains(k)))
        {
            _db.AiProviderConfigs.Add(new AiProviderConfig(key, "openai", "gpt-4o-mini"));
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);
    }

    private static string DeriveFeatureKey(string promptKey)
    {
        var parts = promptKey.Split('.');
        return parts.Length >= 3 && parts[^1].StartsWith('v') && int.TryParse(parts[^1][1..], out _)
            ? string.Join('.', parts[..^1])
            : promptKey;
    }

    private static readonly string[] KnownRuntimeFeatureKeys =
    [
        "learning_path_generate",
        "learning_path_generate_adaptive",
        "activity_generate_writing",
        "activity_evaluate_writing",
        "activity_generate_listening",
        "activity_generate_speaking_roleplay",
        "activity_evaluate_speaking_roleplay",
        "vocabulary_extract_from_attempt",
        "student_memory_update",
        "placement_assessment_evaluate"
    ];

    private static AiProviderConfigItem ToAiProviderConfigItem(AiProviderConfig c)
        => new(
            c.Id,
            c.FeatureKey,
            c.ProviderName,
            c.ModelName,
            c.FallbackProviderName,
            c.FallbackModelName,
            c.FallbackEnabled);

    public async Task<AiProviderCatalogItem> SetProviderApiKeyAsync(SetProviderApiKeyCommand command, CancellationToken ct = default)
    {
        var normalised = command.ProviderName.Trim().ToLowerInvariant();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);
        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }
        cred.SetApiKey(command.ApiKey);
        await _db.SaveChangesAsync(ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedModels);
        return ToCatalogItem(normalised, allowedModels?.Order().ToList() ?? [], cred);
    }

    public async Task<AiProviderCatalogItem> TestProviderAsync(string providerName, CancellationToken ct = default)
    {
        var normalised = providerName.Trim().ToLowerInvariant();
        var cred = await _db.AiProviderCredentials.FirstOrDefaultAsync(c => c.ProviderName == normalised, ct);

        AiProviderConfig.AllowedModels.TryGetValue(normalised, out var allowedSet);
        var models = allowedSet?.Order().ToList() ?? [];

        var outcomes = await _tester.TestAllModelsAsync(normalised, models, cred?.ApiKey, ct);

        if (cred is null)
        {
            cred = new AiProviderCredential(normalised);
            _db.AiProviderCredentials.Add(cred);
        }
        foreach (var o in outcomes)
            cred.RecordModelTest(o.ModelName, o.Ok, o.LatencyMs, o.Error);

        await _db.SaveChangesAsync(ct);
        return ToCatalogItem(normalised, models, cred);
    }

    private static AiProviderCatalogItem ToCatalogItem(
        string providerName,
        IReadOnlyList<string> models,
        AiProviderCredential? cred)
    {
        var tests = models.Select(m =>
        {
            if (cred?.ModelTests.TryGetValue(m, out var r) == true)
                return new ModelTestStatus(m, r.Ok, r.LatencyMs, r.Error, r.TestedAt);
            return new ModelTestStatus(m, false, 0, null, default);
        }).ToList();

        return new AiProviderCatalogItem(
            providerName,
            models,
            HasApiKey: cred?.ApiKey is not null,
            ModelTests: tests);
    }
}

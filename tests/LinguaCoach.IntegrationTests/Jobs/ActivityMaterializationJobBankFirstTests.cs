using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Curriculum;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.IntegrationTests.Sessions;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;

namespace LinguaCoach.IntegrationTests.Jobs;

/// <summary>
/// Phase D1/D2 — bank-first Today slice. Proves ActivityMaterializationJob injects published
/// Resource Bank content into the AI prompt's TopicHint for vocabulary/reading patterns when
/// matching bank rows exist at the student's CEFR level, that it falls back to today's unchanged
/// legacy behavior (no bank marker) when no matching bank rows exist, and (Phase D2) that the
/// full selected-resource list is durably recorded on LearningActivity.BankResourceProvenanceJson.
/// Uses a context-capturing fake IAiActivityGenerator — no real/live AI provider anywhere in this
/// suite.
/// </summary>
public sealed class ActivityMaterializationJobBankFirstTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ActivityMaterializationJobBankFirstTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Injects_bank_resource_context_when_matching_published_vocabulary_exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (profileId, batchId, exerciseId) = await SeedPendingVocabularyExerciseAsync(db, cefrLevel: "B1");

        var source = new CefrResourceSource("D1 Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(source.Id, "deadline", "B1"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        exercise.LearningActivityId.Should().NotBeNull();

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().NotBeNullOrWhiteSpace();
        activity.BankResourceProvenanceJson.Should().Contain("Vocabulary");
    }

    [Fact]
    public async Task Falls_back_to_legacy_generation_unchanged_when_no_matching_bank_rows_exist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // No CefrVocabularyEntry rows seeded by this test at C1 — and the app-startup E6 seed pack
        // (InternalResourceSeedPackSeeder, wired into Program.cs and run by WebApplicationFactory
        // startup for this fixture) only covers A1-B2, so C1 is genuinely empty for this bank type.
        var (_, batchId, exerciseId) = await SeedPendingVocabularyExerciseAsync(db, cefrLevel: "C1");

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().NotContain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        exercise.LearningActivityId.Should().NotBeNull();

        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().BeNull();
    }

    [Fact]
    public async Task Injects_bank_resource_context_for_reading_multiple_choice_multi_confirming_broadened_pattern_coverage()
    {
        // Phase D2 finding: TodayBankResourceSelector gates purely on pattern.PrimarySkill, not an
        // explicit pattern-key allow-list — so every Reading-primary-skill pattern was already
        // covered, not just the 3 patterns D1's own docs originally called out. This test proves
        // reading_multiple_choice_multi (never explicitly mentioned in D1) gets bank context too.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingMultipleChoiceMulti, primarySkill: "Reading");

        var source = new CefrResourceSource("D2 Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrReadingReferences.Add(new CefrReadingReference(source.Id, "B1", referenceExcerpt: "A short workplace excerpt."));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain("Reading");
    }

    [Fact]
    public async Task Injects_full_reading_passage_context_for_a_full_passage_suitable_reading_pattern()
    {
        // Phase D3 — a Reading-primary comprehension pattern (reading_multiple_choice_single)
        // should anchor on a full CefrReadingPassage, injecting the passage text into TopicHint and
        // recording ReadingPassage provenance on the activity.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingMultipleChoiceSingle, primarySkill: "Reading");

        var source = new CefrResourceSource("D3 Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        const string passageText =
            "The office moved to a new building last month. At first, staff found it hard to locate "
            + "meeting rooms, but signs were added and the problem was soon solved. Most people now "
            + "agree the bright, open space is a real improvement over the old office.";
        db.CefrReadingPassages.Add(new CefrReadingPassage(source.Id, "The New Office", passageText, "B1"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");
        aiGenerator.LastContext!.TopicHint.Should().Contain("ReadingPassage");
        aiGenerator.LastContext!.TopicHint.Should().Contain(passageText);

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain("ReadingPassage");
        activity.BankResourceProvenanceJson.Should().Contain("The New Office");
    }

    [Fact]
    public async Task Phase_D4_vocabulary_primary_pattern_passes_enriched_bank_context_and_role_provenance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.PhraseMatch, primarySkill: "Vocabulary");

        var source = new CefrResourceSource("D4 Vocab Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(source.Id, "deliverable", "B1"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");
        // Phase D4 — vocabulary-primary pattern instruction is injected.
        aiGenerator.LastContext!.TopicHint.Should().Contain("Use the selected vocabulary/usage targets naturally");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain("Vocabulary");
        activity.BankResourceProvenanceJson.Should().Contain("\"role\":\"primary\"");
    }

    [Fact]
    public async Task Phase_D4_reading_comprehension_uses_full_passage_primary_with_supporting_vocabulary_provenance()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingMultipleChoiceSingle, primarySkill: "Reading");

        var source = new CefrResourceSource("D4 Reading Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        const string passageText =
            "Maya spends most Sundays at the community garden near her flat. She waters the vegetables, "
            + "pulls a few weeds, and chats with the other volunteers about what to plant next. Over the "
            + "past year the small plot has become a quiet, friendly place that many neighbours now share.";
        // General (non-workplace) context so it is not filtered for a general learner.
        db.CefrReadingPassages.Add(new CefrReadingPassage(
            source.Id, "The Community Garden", passageText, "B1", contextTagsJson: "[\"general\",\"social\"]"));
        db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(source.Id, "volunteer", "B1"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Use ONLY the following full reading passage");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain("ReadingPassage");
        activity.BankResourceProvenanceJson.Should().Contain("\"role\":\"primary\"");
        // A supporting vocabulary target is attached to the passage anchor.
        activity.BankResourceProvenanceJson.Should().Contain("\"role\":\"supporting\"");
    }

    [Fact]
    public async Task Phase_D4_cloze_pattern_uses_short_reference_not_full_passage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingFillInBlanks, primarySkill: "Reading");

        var source = new CefrResourceSource("D4 Cloze Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        db.CefrReadingReferences.Add(new CefrReadingReference(source.Id, "B1", referenceExcerpt: "A short note about a weekend plan."));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");
        // Cloze pattern instruction, and no full-passage anchor block.
        aiGenerator.LastContext!.TopicHint.Should().Contain("do NOT copy a full reading passage");
        aiGenerator.LastContext!.TopicHint.Should().NotContain("Use ONLY the following full reading passage");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().NotContain("ReadingPassage");
    }

    [Fact]
    public async Task Phase_D5_general_learner_excludes_workplace_tagged_vocabulary_from_the_bundle()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.PhraseMatch, primarySkill: "Vocabulary");

        var source = new CefrResourceSource("D5 Vocab Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var workplace = new CefrVocabularyEntry(source.Id, "quarterly", "B1");
        workplace.SetSelectionMetadata("vocabulary.receptive", null, "[\"workplace\"]", "[]");
        var general = new CefrVocabularyEntry(source.Id, "sunshine", "B1");
        general.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\",\"daily\"]", "[]");
        db.CefrVocabularyEntries.AddRange(workplace, general);
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().Contain(general.Id.ToString());
        activity.BankResourceProvenanceJson.Should().NotContain(workplace.Id.ToString());
    }

    [Fact]
    public async Task Phase_D5_general_learner_falls_back_to_legacy_when_only_workplace_rows_exist()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Routed at A2, a level no other test in this shared-fixture class seeds, so "only workplace
        // rows exist at the routed level" genuinely holds (mirrors the C1-empty fallback test above).
        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "A2", patternKey: ExercisePatternKey.PhraseMatch, primarySkill: "Vocabulary");

        var source = new CefrResourceSource("D5 Workplace-Only Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var workplace = new CefrVocabularyEntry(source.Id, "quarterly", "A2");
        workplace.SetSelectionMetadata("vocabulary.receptive", null, "[\"workplace\"]", "[]");
        db.CefrVocabularyEntries.Add(workplace);
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        // Workplace rows excluded for a general learner → no bank bundle → unchanged legacy generation.
        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().NotContain("Bank resources:");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().BeNull();
    }

    [Fact]
    public async Task Phase_D5_reading_cloze_uses_context_filtered_short_reference_not_full_passage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "B1", patternKey: ExercisePatternKey.ReadingFillInBlanks, primarySkill: "Reading");

        var source = new CefrResourceSource("D5 Cloze Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var reference = new CefrReadingReference(source.Id, "B1", referenceExcerpt: "A short note about a weekend plan.");
        reference.SetSelectionMetadata("reading.gist", null, "[\"general\",\"social\"]", "[]");
        db.CefrReadingReferences.Add(reference);
        db.CefrReadingPassages.Add(new CefrReadingPassage(
            source.Id, "A General Passage",
            "On Saturday, Maya walked to the park near her home, read a few pages of her book on a "
            + "bench, and watched the ducks on the pond before heading back for a quiet family breakfast.",
            "B1", contextTagsJson: "[\"general\"]"));
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");
        aiGenerator.LastContext!.TopicHint.Should().Contain("do NOT copy a full reading passage");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().NotContain("ReadingPassage");
    }

    // ── Phase D6 — topic-aware, subskill/difficulty-fed bank selection ───────────

    [Fact]
    public async Task Phase_D6_reading_comprehension_topic_anchors_supporting_vocabulary_to_passage_context()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // C2 is isolated in this shared fixture (startup seed packs only cover A1-B2; C1 is reserved by
        // the legacy-fallback test which asserts C1 stays empty), so the passage the selector anchors
        // on and the supporting rows it can reach are deterministic.
        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "C2", patternKey: ExercisePatternKey.ReadingMultipleChoiceSingle, primarySkill: "Reading");

        var source = new CefrResourceSource("D6 Topic Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        const string passageText =
            "Nadia had never travelled abroad before, so she spent weeks planning the trip. She booked "
            + "an early flight, printed her hotel itinerary, and packed a small bag so she could move "
            + "easily between cities. The journey turned out to be the calmest holiday she had ever taken.";
        db.CefrReadingPassages.Add(new CefrReadingPassage(
            source.Id, "A First Trip Abroad", passageText, "C2", contextTagsJson: "[\"travel\"]"));

        var travelVocab = new CefrVocabularyEntry(source.Id, "itinerary", "C2");
        travelVocab.SetSelectionMetadata("vocabulary.receptive", null, "[\"travel\"]", "[]");
        var generalVocab = new CefrVocabularyEntry(source.Id, "otherwise", "C2");
        generalVocab.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\"]", "[]");
        db.CefrVocabularyEntries.AddRange(travelVocab, generalVocab);
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        // Supporting vocabulary is anchored on the passage's travel context.
        activity.BankResourceProvenanceJson.Should().Contain(travelVocab.Id.ToString());
        activity.BankResourceProvenanceJson.Should().NotContain(generalVocab.Id.ToString());
        activity.BankResourceProvenanceJson.Should().Contain("topic-anchor");
    }

    [Fact]
    public async Task Phase_D6_cloze_reference_topic_anchors_supporting_vocabulary_to_reference_context()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // C2 references are isolated too (C1 is reserved by the legacy-fallback test). The shared C2
        // vocabulary from the passage test above is fine: the deterministic travel anchor still
        // excludes the general row.
        var (_, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "C2", patternKey: ExercisePatternKey.ReadingFillInBlanks, primarySkill: "Reading");

        var source = new CefrResourceSource("D6 Cloze Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var reference = new CefrReadingReference(source.Id, "C2", referenceExcerpt: "Booking flights and hotels for a long trip.");
        reference.SetSelectionMetadata("reading.gist", null, "[\"travel\"]", "[]");
        db.CefrReadingReferences.Add(reference);

        var travelVocab = new CefrVocabularyEntry(source.Id, "layover", "C2");
        travelVocab.SetSelectionMetadata("vocabulary.receptive", null, "[\"travel\"]", "[]");
        var generalVocab = new CefrVocabularyEntry(source.Id, "nevertheless", "C2");
        generalVocab.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\"]", "[]");
        db.CefrVocabularyEntries.AddRange(travelVocab, generalVocab);
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        activity.BankResourceProvenanceJson.Should().NotContain("ReadingPassage");
        activity.BankResourceProvenanceJson.Should().Contain(travelVocab.Id.ToString());
        activity.BankResourceProvenanceJson.Should().NotContain(generalVocab.Id.ToString());
    }

    [Fact]
    public async Task Phase_D6_difficulty_preference_feeds_band_into_vocabulary_selection()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // C2 is isolated (empty from startup + no other test). Two mixed difficulty bands at the same
        // CEFR let us prove the difficulty signal actually narrows selection rather than relaxing.
        var (profileId, batchId, exerciseId) = await SeedPendingExerciseAsync(
            db, cefrLevel: "C2", patternKey: ExercisePatternKey.PhraseMatch, primarySkill: "Vocabulary");

        // Gentle → one band below the C2-normal band (5) = band 4.
        var profile = await db.StudentProfiles.SingleAsync(p => p.Id == profileId);
        profile.UpdateLearningPreferences(
            preferredName: null, supportLanguageCode: null, supportLanguageName: null,
            translationHelpPreference: null, learningGoals: null, customLearningGoal: null,
            focusAreas: null, customFocusArea: null,
            difficultyPreference: DifficultyPreference.Gentle, preferredSessionDurationMinutes: null);
        await db.SaveChangesAsync();

        var source = new CefrResourceSource("D6 Difficulty Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var band4 = new CefrVocabularyEntry(source.Id, "gentleword", "C2");
        band4.SetSelectionMetadata("vocabulary.receptive", 4, "[\"general\"]", "[]");
        var band5 = new CefrVocabularyEntry(source.Id, "hardestword", "C2");
        band5.SetSelectionMetadata("vocabulary.receptive", 5, "[\"general\"]", "[]");
        db.CefrVocabularyEntries.AddRange(band4, band5);
        await db.SaveChangesAsync();

        var aiGenerator = new ContextCapturingAiActivityGenerator();
        var (job, capturingLogger) = await BuildJobAsync(scope, db, aiGenerator);

        await job.Execute(new FakeJobExecutionContext(
            await CreateInMemorySchedulerAsync(),
            new JobDataMap { [ActivityMaterializationJob.BatchIdKey] = batchId.ToString() }));

        aiGenerator.LastContext.Should().NotBeNull(capturingLogger.LastException?.ToString() ?? "no exception captured");

        var exercise = await db.SessionExercises.AsNoTracking().SingleAsync(e => e.Id == exerciseId);
        var activity = await db.LearningActivities.AsNoTracking().SingleAsync(a => a.Id == exercise.LearningActivityId);
        // Gentle preference → band-4 row selected, band-5 not.
        activity.BankResourceProvenanceJson.Should().Contain(band4.Id.ToString());
        activity.BankResourceProvenanceJson.Should().NotContain(band5.Id.ToString());
        activity.BankResourceProvenanceJson.Should().Contain("difficulty=4");
    }

    private static Task<(Guid ProfileId, Guid BatchId, Guid ExerciseId)> SeedPendingVocabularyExerciseAsync(
        LinguaCoachDbContext db, string cefrLevel) =>
        SeedPendingExerciseAsync(db, cefrLevel, ExercisePatternKey.PhraseMatch, "Vocabulary");

    private static async Task<(Guid ProfileId, Guid BatchId, Guid ExerciseId)> SeedPendingExerciseAsync(
        LinguaCoachDbContext db, string cefrLevel, string patternKey, string primarySkill)
    {
        var user = new ApplicationUser
        {
            UserName = $"d1_{Guid.NewGuid():N}@test.com",
            Email = $"d1_{Guid.NewGuid():N}@test.com",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var profile = new StudentProfile(user.Id);
        profile.SetCefrLevel(cefrLevel);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var path = new LearningPath(profile.Id, "Path", "General workplace English context.");
        db.LearningPaths.Add(path);
        await db.SaveChangesAsync();

        var module = new LearningModule(path.Id, "Module", "desc", 0);
        db.LearningModules.Add(module);
        await db.SaveChangesAsync();

        var batch = new GenerationBatch(profile.Id, GenerationTriggerReason.ManualAdmin, requestedSessionCount: 1);
        db.GenerationBatches.Add(batch);
        await db.SaveChangesAsync();

        var session = new LearningSession(module.Id, "Session", "topic", "goal", 15, primarySkill, 0);
        session.SetGenerationMetadata(profile.Id, 1, batch.Id);
        session.MarkGenerationPending();
        db.LearningSessions.Add(session);
        await db.SaveChangesAsync();

        var exercise = new SessionExercise(
            session.Id, 0, patternKey, primarySkill, "Practise key workplace content.", 3);
        db.SessionExercises.Add(exercise);
        await db.SaveChangesAsync();

        return (profile.Id, batch.Id, exercise.Id);
    }

    private static async Task<(ActivityMaterializationJob Job, CapturingLogger<ActivityMaterializationJob> Logger)> BuildJobAsync(
        IServiceScope scope, LinguaCoachDbContext db, IAiActivityGenerator aiGenerator)
    {
        var scheduler = await CreateInMemorySchedulerAsync();
        var logger = new CapturingLogger<ActivityMaterializationJob>();
        var job = new ActivityMaterializationJob(
            db,
            aiGenerator,
            scope.ServiceProvider.GetRequiredService<IExercisePatternRepository>(),
            scope.ServiceProvider.GetRequiredService<LinguaCoach.Infrastructure.Progress.StudentProgressService>(),
            new SingleSchedulerFactory(scheduler),
            scope.ServiceProvider.GetRequiredService<ILearningGoalContextResolver>(),
            scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>(),
            scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>(),
            scope.ServiceProvider.GetRequiredService<IActivityNoveltyPolicy>(),
            scope.ServiceProvider.GetRequiredService<IActivityContentFingerprintService>(),
            scope.ServiceProvider.GetRequiredService<ITodayBankResourceSelector>(),
            logger);
        return (job, logger);
    }

    private static async Task<IScheduler> CreateInMemorySchedulerAsync()
    {
        var props = new System.Collections.Specialized.NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = $"test-{Guid.NewGuid():N}",
            ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
            ["quartz.threadPool.threadCount"] = "0"
        };
        var factory = new StdSchedulerFactory(props);
        return await factory.GetScheduler();
    }

    private sealed class SingleSchedulerFactory : ISchedulerFactory
    {
        private readonly IScheduler _scheduler;
        public SingleSchedulerFactory(IScheduler scheduler) => _scheduler = scheduler;
        public Task<IScheduler> GetScheduler(CancellationToken ct = default) => Task.FromResult(_scheduler);
        public Task<IScheduler?> GetScheduler(string schedName, CancellationToken ct = default) => Task.FromResult<IScheduler?>(_scheduler);
        public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IScheduler>>(new[] { _scheduler });
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
                LastException = exception;
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>Fake IAiActivityGenerator that succeeds and records the received context, so tests
    /// can assert on exactly what was passed into the prompt (e.g. the injected bank supplement in
    /// TopicHint) without depending on a real/live AI provider.</summary>
    private sealed class ContextCapturingAiActivityGenerator : IAiActivityGenerator
    {
        public ActivityGenerationContext? LastContext { get; private set; }

        public Task<string> GenerateActivityContentAsync(ActivityGenerationContext context, CancellationToken ct)
        {
            LastContext = context;
            return Task.FromResult("""{"title":"Practice"}""");
        }

        public Task<string> EvaluateAttemptAsync(ActivityEvaluationContext context, CancellationToken ct)
            => throw new NotSupportedException("Not used by this test.");
    }
}

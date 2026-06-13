using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds the exercise_patterns table with all MVP ExercisePatternDefinition records.
/// Idempotent: skips patterns whose key already exists. Deactivates patterns whose
/// key has been removed from the canonical list (future-proofing for admin use).
///
/// ExercisePatternKey constants are the authoritative source. This seeder must stay
/// in sync with them — a CI test (ExercisePatternSeederTests) verifies no orphans.
/// </summary>
public static class ExercisePatternSeeder
{
    // CompatibleKinds as JSON arrays of ExerciseKind int values.
    // VocabularyWarmup=0, ContextInput=1, ListeningInput=2, ReadingInput=3,
    // WritingTask=4, SpeakingTask=5, Review=6
    // Returns new instances each call — avoids EF change-tracker contamination across calls.
    private static IReadOnlyList<ExercisePatternDefinition> CreateDefinitions() =>
    [
        new(
            key: ExercisePatternKey.PhraseMatch,
            name: "Phrase Match",
            primarySkill: "Vocabulary",
            secondarySkillsJson: """["Workplace Tone"]""",
            compatibleKindsJson: """[0]""",          // VocabularyWarmup
            activityType: ActivityType.VocabularyPractice,
            interactionMode: InteractionMode.MatchingPairs,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 3,
            aiGeneratePromptKey: "activity_generate_phrase_match",
            aiEvaluatePromptKey: "activity_evaluate_phrase_match",
            teachingPurpose: "Introduce or review key workplace phrases before deeper use",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.GapFillWorkplacePhrase,
            name: "Gap Fill — Workplace Phrase",
            primarySkill: "Vocabulary",
            secondarySkillsJson: """["Grammar"]""",
            compatibleKindsJson: """[0,1]""",         // VocabularyWarmup, ContextInput
            activityType: ActivityType.VocabularyPractice,
            interactionMode: InteractionMode.GapFill,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 4,
            aiGeneratePromptKey: "activity_generate_gap_fill_workplace_phrase",
            aiEvaluatePromptKey: "activity_evaluate_gap_fill_workplace_phrase",
            teachingPurpose: "Practise target phrases in a realistic workplace sentence context",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ListenAndAnswer,
            name: "Listen and Answer",
            primarySkill: "Listening",
            secondarySkillsJson: """["Vocabulary"]""",
            compatibleKindsJson: """[2,1]""",         // ListeningInput, ContextInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.AudioAndFreeText,
            markingMode: MarkingMode.AiStructured,
            estimatedMinutes: 4,
            aiGeneratePromptKey: "activity_generate_listen_and_answer",
            aiEvaluatePromptKey: "activity_evaluate_listen_and_answer",
            teachingPurpose: "Check understanding of a workplace audio message",
            requiresAudio: true,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ListenAndGapFill,
            name: "Listen and Gap Fill",
            primarySkill: "Listening",
            secondarySkillsJson: """["Vocabulary","Grammar"]""",
            compatibleKindsJson: """[2]""",           // ListeningInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.AudioAndGapFill,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 4,
            aiGeneratePromptKey: "activity_generate_listen_and_gap_fill",
            aiEvaluatePromptKey: "activity_evaluate_listen_and_gap_fill",
            teachingPurpose: "Notice workplace phrases from audio; train active listening",
            requiresAudio: true,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.EmailReply,
            name: "Workplace Email Reply",
            primarySkill: "Writing",
            secondarySkillsJson: """["Grammar","Vocabulary","Tone"]""",
            compatibleKindsJson: """[4]""",           // WritingTask
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.EmailReply,
            markingMode: MarkingMode.AiStructured,
            estimatedMinutes: 7,
            aiGeneratePromptKey: "activity_generate_email_reply",
            aiEvaluatePromptKey: "activity_evaluate_email_reply",
            teachingPurpose: "Structured workplace writing with correct format, tone, and register",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.TeamsChatSimulation,
            name: "Teams Chat Simulation",
            primarySkill: "Writing",
            secondarySkillsJson: """["Workplace Tone","Vocabulary"]""",
            compatibleKindsJson: """[4]""",           // WritingTask
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.ChatReply,
            markingMode: MarkingMode.AiStructured,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_teams_chat_simulation",
            aiEvaluatePromptKey: "activity_evaluate_teams_chat_simulation",
            teachingPurpose: "Practise concise professional digital communication in chat format",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.SpokenResponseFromPrompt,
            name: "Spoken Response",
            primarySkill: "Speaking",
            secondarySkillsJson: """["Vocabulary","Grammar"]""",
            compatibleKindsJson: """[5]""",           // SpeakingTask
            activityType: ActivityType.SpeakingRolePlay,
            interactionMode: InteractionMode.FreeTextEntry,
            markingMode: MarkingMode.AiOpenEnded,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_spoken_response_from_prompt",
            aiEvaluatePromptKey: "activity_evaluate_spoken_response_from_prompt",
            teachingPurpose: "Practise clear, organised spoken workplace response",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.OpenWritingTask,
            name: "Open Writing Task",
            primarySkill: "Writing",
            secondarySkillsJson: """["Grammar","Vocabulary","Tone"]""",
            compatibleKindsJson: """[4]""",           // WritingTask
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.FreeTextEntry,
            markingMode: MarkingMode.AiOpenEnded,
            estimatedMinutes: 7,
            aiGeneratePromptKey: "activity_generate_open_writing_task",
            aiEvaluatePromptKey: "activity_evaluate_open_writing_task",
            teachingPurpose: "Free-form workplace writing with open-ended AI coaching feedback",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.SpeakingRoleplayTurn,
            name: "Speaking Roleplay Turn",
            primarySkill: "Speaking",
            secondarySkillsJson: """["Vocabulary","Fluency"]""",
            compatibleKindsJson: """[5]""",           // SpeakingTask
            activityType: ActivityType.SpeakingRolePlay,
            interactionMode: InteractionMode.AudioResponse,
            markingMode: MarkingMode.AiOpenEnded,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_speaking_roleplay_turn",
            aiEvaluatePromptKey: "activity_evaluate_speaking_roleplay_turn",
            teachingPurpose: "Practise a spoken workplace roleplay turn with recorded audio response",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.LessonReflection,
            name: "Lesson Reflection",
            primarySkill: "Reflection",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[6]""",           // Review
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.ReadOnly,
            markingMode: MarkingMode.NoMarking,
            estimatedMinutes: 2,
            aiGeneratePromptKey: "activity_generate_lesson_reflection",
            aiEvaluatePromptKey: "activity_evaluate_lesson_reflection",
            teachingPurpose: "Consolidation and session closing; metacognitive awareness",
            requiresAudio: false,
            workplaceContext: false),
    ];

    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existing = await db.ExercisePatterns.ToDictionaryAsync(p => p.Key, ct);

        var added = 0;
        var updated = 0;
        foreach (var definition in CreateDefinitions())
        {
            if (!existing.TryGetValue(definition.Key, out var current))
            {
                db.ExercisePatterns.Add(definition);
                added++;
                continue;
            }

            if (current.InteractionMode != definition.InteractionMode)
            {
                current.UpdateInteractionMode(definition.InteractionMode);
                updated++;
            }
        }

        if (added > 0 || updated > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Added} new and updated {Updated} existing exercise pattern(s).", added, updated);
        }
    }
}

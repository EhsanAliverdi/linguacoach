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

        new(
            key: ExercisePatternKey.ReadingMultipleChoiceSingle,
            name: "Reading Multiple Choice Single",
            primarySkill: "Reading",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[3]""",           // ReadingInput
            activityType: ActivityType.ReadingTask,
            interactionMode: InteractionMode.MultipleChoice,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_reading_multiple_choice_single",
            aiEvaluatePromptKey: "activity_evaluate_reading_multiple_choice_single",
            teachingPurpose: "Practise careful reading and choosing the best-supported answer",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ReadingFillInBlanks,
            name: "Reading Fill in Blanks",
            primarySkill: "Reading",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[3]""",           // ReadingInput
            activityType: ActivityType.ReadingTask,
            interactionMode: InteractionMode.ReadingFillInBlanks,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_reading_fill_in_blanks",
            aiEvaluatePromptKey: "activity_evaluate_reading_fill_in_blanks",
            teachingPurpose: "Practise reading context clues to choose the correct missing word in a passage",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ReorderParagraphs,
            name: "Reorder Paragraphs",
            primarySkill: "Reading",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[3]""",           // ReadingInput
            activityType: ActivityType.ReadingTask,
            interactionMode: InteractionMode.ReorderParagraphs,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_reorder_paragraphs",
            aiEvaluatePromptKey: "activity_evaluate_reorder_paragraphs",
            teachingPurpose: "Practise reading coherence and logical sequencing of paragraph blocks",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.SummarizeWrittenText,
            name: "Summarize Written Text",
            primarySkill: "Writing",
            secondarySkillsJson: """["Reading"]""",
            compatibleKindsJson: """[4]""",           // WritingTask
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.FreeTextEntry,
            markingMode: MarkingMode.AiStructured,
            estimatedMinutes: 7,
            aiGeneratePromptKey: "activity_generate_summarize_written_text",
            aiEvaluatePromptKey: "activity_evaluate_summarize_written_text",
            teachingPurpose: "Practise identifying main ideas and writing a concise summary in the student's own words",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.WriteEssay,
            name: "Write Essay",
            primarySkill: "Writing",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[4]""",           // WritingTask
            activityType: ActivityType.WritingScenario,
            interactionMode: InteractionMode.FreeTextEntry,
            markingMode: MarkingMode.AiStructured,
            estimatedMinutes: 10,
            aiGeneratePromptKey: "activity_generate_write_essay",
            aiEvaluatePromptKey: "activity_evaluate_write_essay",
            teachingPurpose: "Practise planning and writing a structured essay that directly answers a prompt",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ReadingWritingFillInBlanks,
            name: "Reading and Writing Fill in Blanks",
            primarySkill: "Reading",
            secondarySkillsJson: """["Writing"]""",
            compatibleKindsJson: """[3]""",           // ReadingInput
            activityType: ActivityType.ReadingTask,
            interactionMode: InteractionMode.ReadingWritingFillInBlanks,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_reading_writing_fill_in_blanks",
            aiEvaluatePromptKey: "activity_evaluate_reading_writing_fill_in_blanks",
            teachingPurpose: "Practise reading context clues and word-form knowledge to complete passage blanks",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ReadingMultipleChoiceMulti,
            name: "Reading Multiple Choice Multiple",
            primarySkill: "Reading",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[3]""",           // ReadingInput
            activityType: ActivityType.ReadingTask,
            interactionMode: InteractionMode.MultipleChoiceMulti,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_reading_multiple_choice_multi",
            aiEvaluatePromptKey: "activity_evaluate_reading_multiple_choice_multi",
            teachingPurpose: "Practise reading carefully and selecting all answers supported by the passage",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ListeningMultipleChoiceSingle,
            name: "Listening Multiple Choice Single",
            primarySkill: "Listening",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[2]""",           // ListeningInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.MultipleChoice,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_listening_multiple_choice_single",
            aiEvaluatePromptKey: "activity_evaluate_listening_multiple_choice_single",
            teachingPurpose: "Practise listening for the main idea and choosing the best-supported answer",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ListeningMultipleChoiceMulti,
            name: "Listening Multiple Choice Multiple",
            primarySkill: "Listening",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[2]""",           // ListeningInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.MultipleChoiceMulti,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_listening_multiple_choice_multi",
            aiEvaluatePromptKey: "activity_evaluate_listening_multiple_choice_multi",
            teachingPurpose: "Practise listening for multiple supported details and avoiding distractors",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.ListeningFillInBlanks,
            name: "Listening Fill in Blanks",
            primarySkill: "Listening",
            secondarySkillsJson: """["Writing"]""",
            compatibleKindsJson: """[2]""",           // ListeningInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.ListeningFillInBlanks,
            markingMode: MarkingMode.ExactMatch,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_listening_fill_in_blanks",
            aiEvaluatePromptKey: "activity_evaluate_listening_fill_in_blanks",
            teachingPurpose: "Practise listening for missing words using context, grammar, and sound clues",
            requiresAudio: false,
            workplaceContext: true),

        new(
            key: ExercisePatternKey.SelectMissingWord,
            name: "Select Missing Word",
            primarySkill: "Listening",
            secondarySkillsJson: """[]""",
            compatibleKindsJson: """[2]""",           // ListeningInput
            activityType: ActivityType.ListeningComprehension,
            interactionMode: InteractionMode.MultipleChoice,
            markingMode: MarkingMode.KeyedSelection,
            estimatedMinutes: 5,
            aiGeneratePromptKey: "activity_generate_select_missing_word",
            aiEvaluatePromptKey: "activity_evaluate_select_missing_word",
            teachingPurpose: "Practise predicting a missing word from listening context, grammar, and meaning",
            requiresAudio: false,
            workplaceContext: true),
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

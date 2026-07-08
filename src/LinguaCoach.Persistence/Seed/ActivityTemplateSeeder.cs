using System.Text.Json;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds a small, original, English-only batch of approved/published ActivityTemplates.
/// Phase C1 seeded the first three (PhraseMatch, GapFillWorkplacePhrase,
/// ReadingMultipleChoiceSingle). Phase C2 (2026-07-08) adds three more reading-family patterns
/// (ReadingMultipleChoiceMulti, ReadingFillInBlanks, ReadingWritingFillInBlanks). Phase C3
/// (2026-07-08) adds ReorderParagraphs, using a stock Form.io "datagrid" (reorder enabled) instead
/// of individual answer components. See docs/architecture/practice-gym.md.
///
/// Idempotent per template Key — safe to run on every startup.
///
/// IMPORTANT design note for anyone editing these templates: ActivityTemplateInstanceGenerator
/// personalizes FormIoBaseSchemaJson via AI, but ScoringModelJson is NEVER regenerated — it is
/// applied as-is to whatever the AI produces, keyed by component "key". GenerationInstructions
/// below therefore explicitly forbid the AI from changing which option/value is correct or
/// renaming/removing scored component keys. This is the same constraint the existing pilot
/// template design already accepts; it is not a new risk introduced by this phase.
/// </summary>
public static class ActivityTemplateSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        foreach (var seed in Templates)
        {
            var exists = await db.ActivityTemplates.AnyAsync(t => t.Key == seed.Key, ct);
            if (exists) continue;

            var template = new ActivityTemplate(
                key: seed.Key,
                skill: seed.Skill,
                cefrLevel: seed.CefrLevel,
                activityType: seed.ActivityType,
                subskill: seed.Subskill,
                patternKey: seed.PatternKey,
                contextTagsJson: seed.ContextTagsJson,
                focusTagsJson: seed.FocusTagsJson,
                curriculumObjectiveKey: seed.CurriculumObjectiveKey,
                formIoBaseSchemaJson: seed.FormIoBaseSchemaJson,
                generationInstructions: seed.GenerationInstructions,
                scoringModelJson: seed.ScoringModelJson,
                validationRulesJson: seed.ValidationRulesJson,
                estimatedDurationSeconds: seed.EstimatedDurationSeconds);

            template.Approve();
            template.Publish();

            db.ActivityTemplates.Add(template);
            logger.LogInformation("ActivityTemplateSeeder: seeded template '{Key}' for pattern '{PatternKey}'.", seed.Key, seed.PatternKey);
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record TemplateSeed(
        string Key,
        string Skill,
        string? Subskill,
        string CefrLevel,
        string ActivityType,
        string PatternKey,
        string ContextTagsJson,
        string FocusTagsJson,
        string? CurriculumObjectiveKey,
        string FormIoBaseSchemaJson,
        string GenerationInstructions,
        string ScoringModelJson,
        string ValidationRulesJson,
        int EstimatedDurationSeconds);

    private static string Schema(object components) =>
        JsonSerializer.Serialize(new { display = "form", components }, JsonOptions);

    private static string ScoringRules(Dictionary<string, object> components) =>
        JsonSerializer.Serialize(new { components }, JsonOptions);

    private static string ValidationRules(string[] requiredKeys) =>
        JsonSerializer.Serialize(new { requiredComponentKeys = requiredKeys, maxSchemaLength = 6000 }, JsonOptions);

    private static readonly IReadOnlyList<TemplateSeed> Templates =
    [
        // ── 1. PhraseMatch — vocabulary, B1 ─────────────────────────────────────────────
        new TemplateSeed(
            Key: "phrase_match_workplace_seed_v1",
            Skill: "vocabulary",
            Subskill: "vocabulary.collocation",
            CefrLevel: "B1",
            ActivityType: "VocabularyPractice",
            PatternKey: "phrase_match",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["phrase_matching"]""",
            CurriculumObjectiveKey: "b1.vocabulary.topic_word_families",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "instructions", input = false,
                      html = "<p>Choose the meaning that best matches each workplace phrase.</p>" },
                new { type = "radio", key = "phrase_1", label = "What does 'circle back' mean?",
                      values = new[] {
                          new { label = "Discuss something again later", value = "A" },
                          new { label = "Walk around the office", value = "B" },
                          new { label = "Cancel a meeting", value = "C" } } },
                new { type = "radio", key = "phrase_2", label = "What does 'touch base' mean?",
                      values = new[] {
                          new { label = "Finish a project", value = "A" },
                          new { label = "Make brief contact to check in", value = "B" },
                          new { label = "Start a new task", value = "C" } } },
                new { type = "radio", key = "phrase_3", label = "What does 'attach the file' mean?",
                      values = new[] {
                          new { label = "Print the document", value = "A" },
                          new { label = "Delete the document", value = "B" },
                          new { label = "Send a document along with an email", value = "C" } } },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may reword the three phrase questions and the " +
                "wording of all answer options, and you may change which workplace phrases are asked about. " +
                "Do NOT change the component 'key' values (instructions, phrase_1, phrase_2, phrase_3) or the " +
                "number of components. For each radio component, the option with value 'B' MUST remain the " +
                "correct meaning of the phrase asked about (value 'A' and 'C' must remain incorrect distractors). " +
                "Keep all content in English only — do not add any other language. Keep a professional, " +
                "workplace-appropriate tone suitable for CEFR B1 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["phrase_1"] = new { kind = "single_choice", correctAnswer = "A", points = 1.0 },
                ["phrase_2"] = new { kind = "single_choice", correctAnswer = "B", points = 1.0 },
                ["phrase_3"] = new { kind = "single_choice", correctAnswer = "C", points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["phrase_1", "phrase_2", "phrase_3"]),
            EstimatedDurationSeconds: 180),

        // ── 2. GapFillWorkplacePhrase — vocabulary, B1 ──────────────────────────────────
        new TemplateSeed(
            Key: "gap_fill_workplace_phrase_seed_v1",
            Skill: "vocabulary",
            Subskill: "vocabulary.collocation",
            CefrLevel: "B1",
            ActivityType: "VocabularyPractice",
            PatternKey: "gap_fill_workplace_phrase",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["gap_fill"]""",
            CurriculumObjectiveKey: "b1.vocabulary.topic_word_families",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "sentence_1", input = false,
                      html = "<p>Could you please ______ the meeting to Thursday? I have a scheduling conflict.</p>" },
                new { type = "textfield", key = "blank_1", label = "Fill in the missing word" },
                new { type = "content", key = "sentence_2", input = false,
                      html = "<p>I'll ______ base with you once I have an update from the client.</p>" },
                new { type = "textfield", key = "blank_2", label = "Fill in the missing word" },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may change the two workplace sentences and which " +
                "missing word they test, as long as each sentence still has exactly one clear, unambiguous " +
                "missing word. Do NOT change the component 'key' values (sentence_1, blank_1, sentence_2, " +
                "blank_2) or the number of components. Update the 'html' text in sentence_1/sentence_2 to match " +
                "whatever new sentence you write, keeping the blank shown as a blank (e.g. '______'). Keep all " +
                "content in English only. Keep a professional, workplace-appropriate tone suitable for CEFR B1 " +
                "learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["blank_1"] = new { kind = "text_normalized", correctAnswer = "reschedule", points = 1.0 },
                ["blank_2"] = new { kind = "text_normalized", correctAnswer = "touch", points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["blank_1", "blank_2"]),
            EstimatedDurationSeconds: 150),

        // ── 3. ReadingMultipleChoiceSingle — reading, B1 ────────────────────────────────
        new TemplateSeed(
            Key: "reading_mcq_workplace_seed_v1",
            Skill: "reading",
            Subskill: "reading.detail",
            CefrLevel: "B1",
            ActivityType: "ReadingTask",
            PatternKey: "reading_multiple_choice_single",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["reading_comprehension"]""",
            CurriculumObjectiveKey: "b1.reading.understanding_texts",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "reading_passage", input = false,
                      html = "<p>The marketing team submitted the quarterly report a week late because two " +
                             "members were out sick. The manager decided to extend the internal review period " +
                             "by three days so the finance department would still have enough time to prepare " +
                             "the summary for the board meeting.</p>" },
                new { type = "radio", key = "answer", label = "Why did the manager extend the review period?",
                      values = new[] {
                          new { label = "To reduce costs", value = "A" },
                          new { label = "Because the report was submitted late", value = "B" },
                          new { label = "Because the board meeting was cancelled", value = "C" } } },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may write a different short workplace passage " +
                "(3-5 sentences) and a different single comprehension question with three answer options, as " +
                "long as the passage clearly and unambiguously supports exactly one correct answer. Do NOT " +
                "change the component 'key' values (reading_passage, answer) or the number of components. The " +
                "option with value 'B' MUST remain the correct answer supported by the passage; 'A' and 'C' " +
                "must remain plausible-but-incorrect distractors. Keep all content in English only. Keep a " +
                "professional, workplace-appropriate tone suitable for CEFR B1 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["answer"] = new { kind = "single_choice", correctAnswer = "B", points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["reading_passage", "answer"]),
            EstimatedDurationSeconds: 240),

        // ── 4. ReadingMultipleChoiceMulti — reading, B1 (Phase C2) ──────────────────────
        new TemplateSeed(
            Key: "reading_mcq_multi_workplace_seed_v1",
            Skill: "reading",
            Subskill: "reading.detail",
            CefrLevel: "B1",
            ActivityType: "ReadingTask",
            PatternKey: "reading_multiple_choice_multi",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["reading_comprehension"]""",
            CurriculumObjectiveKey: "b1.reading.understanding_texts",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "reading_passage", input = false,
                      html = "<p>The IT helpdesk received forty tickets on Monday. Twenty-five were resolved " +
                             "the same day, ten were escalated to a specialist team, and five were closed as " +
                             "duplicates. The helpdesk manager praised the team for the fast same-day rate but " +
                             "asked staff to check for duplicate tickets earlier next time.</p>" },
                new { type = "selectboxes", key = "answers",
                      label = "Select all statements supported by the passage",
                      values = new[] {
                          new { label = "Most tickets were resolved the same day", value = "A" },
                          new { label = "No tickets were escalated", value = "B" },
                          new { label = "Some tickets were duplicates", value = "C" },
                          new { label = "The manager was unhappy with the team's speed", value = "D" } } },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may write a different short workplace passage " +
                "(3-5 sentences) and different answer-option wording, as long as the passage clearly and " +
                "unambiguously supports exactly two of the four statements as true and the other two as false " +
                "or unsupported. Do NOT change the component 'key' values (reading_passage, answers) or the " +
                "number of components or options. The options with value 'A' and 'C' MUST remain the two " +
                "statements supported by the passage; 'B' and 'D' must remain unsupported or contradicted by " +
                "the passage. Keep all content in English only. Keep a professional, workplace-appropriate tone " +
                "suitable for CEFR B1 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["answers"] = new { kind = "multiple_choice", correctAnswers = new[] { "A", "C" }, points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["reading_passage", "answers"]),
            EstimatedDurationSeconds: 240),

        // ── 5. ReadingFillInBlanks — reading, B1 (Phase C2) ─────────────────────────────
        new TemplateSeed(
            Key: "reading_fill_in_blanks_workplace_seed_v1",
            Skill: "reading",
            Subskill: "reading.vocabulary_in_context",
            CefrLevel: "B1",
            ActivityType: "ReadingTask",
            PatternKey: "reading_fill_in_blanks",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["vocabulary_in_context"]""",
            CurriculumObjectiveKey: "b1.reading.vocabulary_in_context",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "reading_passage", input = false,
                      html = "<p>Before the client visit, please make sure the meeting room is tidy and the " +
                             "projector is [blank_1]. If anything is missing, [blank_2] the facilities team " +
                             "right away.</p>" },
                new { type = "radio", key = "blank_1", label = "Choose the best word for blank 1",
                      values = new[] {
                          new { label = "working", value = "A" },
                          new { label = "loud", value = "B" },
                          new { label = "expensive", value = "C" } } },
                new { type = "radio", key = "blank_2", label = "Choose the best word for blank 2",
                      values = new[] {
                          new { label = "ignore", value = "A" },
                          new { label = "contact", value = "B" },
                          new { label = "replace", value = "C" } } },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may write a different short workplace passage with " +
                "two blanks (marked as [blank_1] and [blank_2] inside the 'reading_passage' html) and different " +
                "answer-option wording for each blank, as long as exactly one option per blank is clearly correct " +
                "given the passage context. Do NOT change the component 'key' values (reading_passage, blank_1, " +
                "blank_2) or the number of components. The option with value 'A' MUST remain the correct answer " +
                "for blank_1, and the option with value 'B' MUST remain the correct answer for blank_2; the other " +
                "options at each blank must remain plausible-but-incorrect distractors. Keep all content in " +
                "English only. Keep a professional, workplace-appropriate tone suitable for CEFR B1 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["blank_1"] = new { kind = "single_choice", correctAnswer = "A", points = 1.0 },
                ["blank_2"] = new { kind = "single_choice", correctAnswer = "B", points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["reading_passage", "blank_1", "blank_2"]),
            EstimatedDurationSeconds: 200),

        // ── 6. ReadingWritingFillInBlanks — reading/writing, B2 (Phase C2) ──────────────
        new TemplateSeed(
            Key: "reading_writing_fill_in_blanks_workplace_seed_v1",
            Skill: "reading",
            Subskill: "reading.vocabulary_in_context",
            CefrLevel: "B2",
            ActivityType: "ReadingTask",
            PatternKey: "reading_writing_fill_in_blanks",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["word_form"]""",
            CurriculumObjectiveKey: "b2.reading.word_form",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "reading_passage", input = false,
                      html = "<p>The team's ______ (blank_1) on the new process was overwhelmingly positive, " +
                             "so management decided to ______ (blank_2) it across every department.</p>" },
                new { type = "textfield", key = "blank_1", label = "Type the correct word form for blank 1" },
                new { type = "textfield", key = "blank_2", label = "Type the correct word form for blank 2" },
            }),
            GenerationInstructions:
                "Personalize only the surface wording: you may write a different short workplace passage with " +
                "two word-form blanks (marked as ______ (blank_1) and ______ (blank_2) inside the " +
                "'reading_passage' html) and a different pair of target words, as long as each blank has exactly " +
                "one unambiguous correct word form given the sentence's grammar. Do NOT change the component " +
                "'key' values (reading_passage, blank_1, blank_2) or the number of components. Keep the expected " +
                "answer for each blank a single English word only (no punctuation). Keep all content in English " +
                "only. Keep a professional, workplace-appropriate tone suitable for CEFR B2 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["blank_1"] = new { kind = "text_normalized", correctAnswer = "feedback", points = 1.0 },
                ["blank_2"] = new { kind = "text_normalized", correctAnswer = "roll out", points = 1.0 },
            }),
            ValidationRulesJson: ValidationRules(["reading_passage", "blank_1", "blank_2"]),
            EstimatedDurationSeconds: 220),

        // ── 7. ReorderParagraphs — reading, B1 (Phase C3) ───────────────────────────────
        // Stock Form.io "datagrid" with its built-in "reorder" setting (drag-to-reorder rows).
        // The row template carries a hidden "itemId" field (the stable paragraph id) plus a
        // read-only "text" field showing the paragraph. Rows are listed below in an arbitrary
        // SHUFFLED display order — the correct order lives exclusively in ScoringModelJson's
        // "correctOrder" (backend-only, ordered_sequence kind), never in this schema.
        new TemplateSeed(
            Key: "reorder_paragraphs_workplace_seed_v1",
            Skill: "reading",
            Subskill: "reading.inference",
            CefrLevel: "B1",
            ActivityType: "ReadingTask",
            PatternKey: "reorder_paragraphs",
            ContextTagsJson: """["workplace"]""",
            FocusTagsJson: """["reading_coherence"]""",
            CurriculumObjectiveKey: "b1.reading.understanding_texts",
            FormIoBaseSchemaJson: Schema(new object[]
            {
                new { type = "content", key = "instructions", input = false,
                      html = "<p>Drag the steps below into the correct order for onboarding a new " +
                             "team member.</p>" },
                new
                {
                    type = "datagrid",
                    key = "paragraphs",
                    label = "Onboarding steps",
                    reorder = true,
                    disableAddingRemovingRows = true,
                    components = new object[]
                    {
                        new { type = "hidden", key = "itemId", input = true, clearOnHide = false },
                        new { type = "textarea", key = "text", input = true, disabled = true, clearOnHide = false },
                    },
                    defaultValue = new object[]
                    {
                        new { itemId = "p3", text = "By the end of the first week, assign the new hire a mentor from the team who can answer day-to-day questions and check in regularly." },
                        new { itemId = "p1", text = "Before the new hire's start date, IT sets up their email account, laptop, and access to the shared project folders." },
                        new { itemId = "p5", text = "At the 30-day mark, the manager holds a short check-in meeting to review progress and address any open questions." },
                        new { itemId = "p2", text = "On the first day, the manager gives a short welcome tour of the office and introduces the new hire to the immediate team." },
                        new { itemId = "p4", text = "During the second week, the new hire completes their first small task under the mentor's guidance and receives feedback." },
                    },
                },
            }),
            GenerationInstructions:
                "Personalize only minor surface wording within each paragraph (e.g. word choice), while " +
                "keeping every paragraph's logical position in the sequence, its role in the process, and its " +
                "connection to the paragraph before and after it completely unchanged. Do NOT change the " +
                "component 'key' values (instructions, paragraphs), the row 'itemId' values (p1-p5), the number " +
                "of rows, or the 'reorder'/'disableAddingRemovingRows' settings. Do NOT add any numbering, " +
                "ordinal words (first/second/third), or sequence hints inside the paragraph text or " +
                "instructions that would reveal the correct order. Keep all content in English only. Keep a " +
                "professional, workplace-appropriate tone suitable for CEFR B1 learners.",
            ScoringModelJson: ScoringRules(new Dictionary<string, object>
            {
                ["paragraphs"] = new
                {
                    kind = "ordered_sequence",
                    correctOrder = new[] { "p1", "p2", "p3", "p4", "p5" },
                    points = 1.0,
                },
            }),
            ValidationRulesJson: ValidationRules(["paragraphs"]),
            EstimatedDurationSeconds: 240),
    ];
}

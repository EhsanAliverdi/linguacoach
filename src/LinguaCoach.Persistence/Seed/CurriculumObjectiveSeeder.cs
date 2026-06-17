using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds the curriculum_objectives table with the starter Phase 10K syllabus.
/// Idempotent: upserts on Key. Updates title/description/order/difficulty if changed.
/// After seeding, validates that all prerequisite keys exist — throws if any are dangling.
///
/// Scope: A1–B2 starter objectives across writing, reading, listening, speaking,
/// vocabulary, grammar/pronunciation. Multiple learner contexts. workplace is one
/// context only, not the default.
///
/// This seeder does NOT wire objectives to activity routing — that belongs to 10L.
/// </summary>
public static class CurriculumObjectiveSeeder
{
    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var definitions = CreateDefinitions();
        var existing = await db.CurriculumObjectives
            .ToDictionaryAsync(o => o.Key, ct);

        var added = 0;
        var updated = 0;

        foreach (var def in definitions)
        {
            if (!existing.TryGetValue(def.Key, out var current))
            {
                db.CurriculumObjectives.Add(def);
                added++;
                continue;
            }

            // Upsert: update mutable fields if changed.
            if (current.Title != def.Title
                || current.Description != def.Description
                || current.RecommendedOrder != def.RecommendedOrder
                || current.DifficultyBand != def.DifficultyBand
                || current.TeachingNotes != def.TeachingNotes)
            {
                current.UpdateDetails(
                    def.Title,
                    def.Description,
                    def.RecommendedOrder,
                    def.DifficultyBand,
                    def.TeachingNotes);
                updated++;
            }
        }

        if (added > 0 || updated > 0)
            await db.SaveChangesAsync(ct);

        // Post-seed prerequisite integrity check.
        ValidatePrerequisiteIntegrity(definitions, logger);

        if (added > 0 || updated > 0)
            logger.LogInformation(
                "CurriculumObjectiveSeeder: {Added} added, {Updated} updated.",
                added, updated);
    }

    private static void ValidatePrerequisiteIntegrity(
        IReadOnlyList<CurriculumObjective> definitions,
        ILogger logger)
    {
        var allKeys = definitions.Select(d => d.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dangling = new List<string>();

        foreach (var def in definitions)
        {
            var prereqs = ParseJsonStringArray(def.PrerequisiteKeysJson);
            foreach (var prereq in prereqs)
            {
                if (!allKeys.Contains(prereq))
                    dangling.Add($"{def.Key} → missing prereq '{prereq}'");
            }
        }

        if (dangling.Count > 0)
        {
            var msg = $"CurriculumObjectiveSeeder: dangling prerequisite key(s):\n  {string.Join("\n  ", dangling)}";
            logger.LogError("{Message}", msg);
            throw new InvalidOperationException(msg);
        }
    }

    private static List<string> ParseJsonStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        // Simple extraction without System.Text.Json in domain — strip brackets and split on quotes.
        return json
            .Trim('[', ']')
            .Split(',')
            .Select(s => s.Trim().Trim('"'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    // Returns new instances each call to avoid EF change-tracker contamination.
    public static IReadOnlyList<CurriculumObjective> CreateDefinitions() =>
    [
        // ── A1: Greetings and introductions ──────────────────────────────────
        new(
            key: "a1.speaking.greetings_introductions",
            title: "Greetings and Introductions",
            description: "Use common greetings and introduce yourself with name, origin, and simple details.",
            cefrLevel: CefrLevelConstants.A1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","confidence"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.MigrationSettlement}"]""",
            focusTagsJson: """["social_conversation","daily_life"]""",
            prerequisiteKeysJson: "[]",
            recommendedOrder: 10,
            difficultyBand: 1,
            isActive: true,
            isReviewable: true,
            teachingNotes: "Focus on 'Hi / Hello / Nice to meet you'. Short exchanges only."),

        // ── A1: Simple personal information ──────────────────────────────────
        new(
            key: "a1.speaking.personal_information",
            title: "Sharing Simple Personal Information",
            description: "State your name, job, country, and basic personal details in short sentences.",
            cefrLevel: CefrLevelConstants.A1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.MigrationSettlement}"]""",
            focusTagsJson: """["introductions","daily_life"]""",
            prerequisiteKeysJson: $"""["a1.speaking.greetings_introductions"]""",
            recommendedOrder: 20,
            difficultyBand: 1,
            isActive: true,
            isReviewable: true),

        // ── A1: Basic everyday vocabulary ─────────────────────────────────────
        new(
            key: "a1.vocabulary.everyday_basics",
            title: "Basic Everyday Vocabulary",
            description: "Recognise and use high-frequency words for numbers, time, food, places, and common objects.",
            cefrLevel: CefrLevelConstants.A1,
            primarySkill: CurriculumSkillConstants.Vocabulary,
            secondarySkillsJson: """["reading"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.Travel}"]""",
            focusTagsJson: """["daily_life"]""",
            prerequisiteKeysJson: "[]",
            recommendedOrder: 15,
            difficultyBand: 1,
            isActive: true,
            isReviewable: true),

        // ── A1: Simple present tense ──────────────────────────────────────────
        new(
            key: "a1.grammar.simple_present",
            title: "Simple Present Tense",
            description: "Form and use simple present sentences about habits, routines, and facts.",
            cefrLevel: CefrLevelConstants.A1,
            primarySkill: CurriculumSkillConstants.Grammar,
            secondarySkillsJson: """["writing","speaking"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}"]""",
            focusTagsJson: """["grammar_foundation"]""",
            prerequisiteKeysJson: "[]",
            recommendedOrder: 25,
            difficultyBand: 1,
            isActive: true,
            isReviewable: true),

        // ── A1: Basic listening for familiar words ────────────────────────────
        new(
            key: "a1.listening.familiar_words",
            title: "Listening for Familiar Words",
            description: "Identify key words and basic information in very slow, clear speech about familiar topics.",
            cefrLevel: CefrLevelConstants.A1,
            primarySkill: CurriculumSkillConstants.Listening,
            secondarySkillsJson: """["vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.ListeningConfidence}"]""",
            focusTagsJson: """["listening_foundation"]""",
            prerequisiteKeysJson: "[]",
            recommendedOrder: 30,
            difficultyBand: 1,
            isActive: true,
            isReviewable: false),

        // ── A2: Daily routines ────────────────────────────────────────────────
        new(
            key: "a2.speaking.daily_routines",
            title: "Describing Daily Routines",
            description: "Talk about daily habits and routines using present simple and time expressions.",
            cefrLevel: CefrLevelConstants.A2,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","grammar"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}"]""",
            focusTagsJson: """["daily_life"]""",
            prerequisiteKeysJson: $"""["a1.grammar.simple_present","a1.speaking.personal_information"]""",
            recommendedOrder: 110,
            difficultyBand: 1,
            isActive: true,
            isReviewable: true),

        // ── A2: Simple past experiences ───────────────────────────────────────
        new(
            key: "a2.speaking.simple_past",
            title: "Talking About Past Experiences",
            description: "Use simple past tense to describe past events and experiences briefly.",
            cefrLevel: CefrLevelConstants.A2,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["grammar","vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.SocialConversation}"]""",
            focusTagsJson: """["grammar_foundation","daily_life"]""",
            prerequisiteKeysJson: $"""["a2.speaking.daily_routines"]""",
            recommendedOrder: 120,
            difficultyBand: 2,
            isActive: true,
            isReviewable: true),

        // ── A2: Travel questions ──────────────────────────────────────────────
        new(
            key: "a2.speaking.travel_questions",
            title: "Travel Questions and Directions",
            description: "Ask and answer simple questions about transport, locations, and travel plans.",
            cefrLevel: CefrLevelConstants.A2,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","listening"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.Travel}","{CurriculumContextTagConstants.DayToDay}"]""",
            focusTagsJson: """["travel_conversation","daily_life"]""",
            prerequisiteKeysJson: $"""["a1.vocabulary.everyday_basics"]""",
            recommendedOrder: 130,
            difficultyBand: 2,
            isActive: true,
            isReviewable: true,
            teachingNotes: "Focus on 'How do I get to...?', 'Where is...?', 'How long does it take?'"),

        // ── A2: Short messages / writing ─────────────────────────────────────
        new(
            key: "a2.writing.short_messages",
            title: "Writing Short Messages",
            description: "Write simple text messages, notes, and short emails for everyday purposes.",
            cefrLevel: CefrLevelConstants.A2,
            primarySkill: CurriculumSkillConstants.Writing,
            secondarySkillsJson: """["vocabulary","grammar"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.WritingConfidence}"]""",
            focusTagsJson: """["writing_foundation","daily_life"]""",
            prerequisiteKeysJson: $"""["a1.grammar.simple_present"]""",
            recommendedOrder: 140,
            difficultyBand: 2,
            isActive: true,
            isReviewable: false),

        // ── A2: Basic pronunciation clarity ──────────────────────────────────
        new(
            key: "a2.pronunciation.basic_clarity",
            title: "Basic Pronunciation Clarity",
            description: "Produce clear sounds for high-frequency words; reduce common pronunciation errors.",
            cefrLevel: CefrLevelConstants.A2,
            primarySkill: CurriculumSkillConstants.Pronunciation,
            secondarySkillsJson: """["speaking","confidence"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.Pronunciation}"]""",
            focusTagsJson: """["pronunciation_foundation"]""",
            prerequisiteKeysJson: $"""["a1.speaking.greetings_introductions"]""",
            recommendedOrder: 150,
            difficultyBand: 2,
            isActive: true,
            isReviewable: true),

        // ── B1: Giving opinions with reasons ─────────────────────────────────
        new(
            key: "b1.speaking.opinions_with_reasons",
            title: "Giving Opinions with Reasons",
            description: "Express opinions and support them with simple reasons using linking words.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","fluency"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.SocialConversation}"]""",
            focusTagsJson: """["fluency","workplace_communication"]""",
            prerequisiteKeysJson: $"""["a2.speaking.simple_past"]""",
            recommendedOrder: 210,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true),

        // ── B1: Summarising spoken information ───────────────────────────────
        new(
            key: "b1.listening.summarise_spoken",
            title: "Summarising Short Spoken Information",
            description: "Listen to a short talk or conversation and summarise the main points in writing.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Listening,
            secondarySkillsJson: """["writing","vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.ListeningConfidence}","{CurriculumContextTagConstants.StudyAcademic}"]""",
            focusTagsJson: """["listening_comprehension","note_taking"]""",
            prerequisiteKeysJson: $"""["a1.listening.familiar_words"]""",
            recommendedOrder: 220,
            difficultyBand: 3,
            isActive: true,
            isReviewable: false),

        // ── B1: Writing clear emails / messages ───────────────────────────────
        new(
            key: "b1.writing.clear_emails",
            title: "Writing Clear Short Emails and Messages",
            description: "Write clear, well-structured short emails for everyday and workplace purposes.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Writing,
            secondarySkillsJson: """["vocabulary","grammar"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.WritingConfidence}"]""",
            focusTagsJson: """["email_writing","workplace_communication"]""",
            prerequisiteKeysJson: $"""["a2.writing.short_messages"]""",
            recommendedOrder: 230,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true,
            teachingNotes: "Teach subject line, greeting, purpose, action, sign-off structure."),

        // ── B1: Managing everyday problems ────────────────────────────────────
        new(
            key: "b1.speaking.everyday_problems",
            title: "Managing Everyday Problems",
            description: "Explain a problem clearly and ask for help or solutions in everyday situations.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","fluency"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.DayToDay}","{CurriculumContextTagConstants.MigrationSettlement}","{CurriculumContextTagConstants.GeneralEnglish}"]""",
            focusTagsJson: """["problem_solving","daily_life"]""",
            prerequisiteKeysJson: $"""["a2.speaking.daily_routines"]""",
            recommendedOrder: 240,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true),

        // ── B1: Asking follow-up questions ────────────────────────────────────
        new(
            key: "b1.speaking.follow_up_questions",
            title: "Asking Follow-Up Questions",
            description: "Keep a conversation going by asking relevant follow-up questions to show interest.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["listening","fluency"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.SocialConversation}","{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.JobInterviews}"]""",
            focusTagsJson: """["conversation_skills","workplace_communication"]""",
            prerequisiteKeysJson: $"""["b1.speaking.opinions_with_reasons"]""",
            recommendedOrder: 250,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true),

        // ── B1: Job interview basics (workplace, non-default) ─────────────────
        new(
            key: "b1.speaking.job_interview_basics",
            title: "Job Interview — Basic Responses",
            description: "Answer common interview questions clearly: tell me about yourself, strengths, experience.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["vocabulary","confidence"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.JobInterviews}","{CurriculumContextTagConstants.Workplace}"]""",
            focusTagsJson: """["job_search","interview_skills"]""",
            prerequisiteKeysJson: $"""["b1.speaking.opinions_with_reasons"]""",
            recommendedOrder: 260,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true,
            teachingNotes: "Workplace/job-interview context — not a general English default."),

        // ── B1: Exam-inspired reading comprehension ───────────────────────────
        new(
            key: "b1.reading.exam_comprehension",
            title: "Reading Comprehension — Exam Style",
            description: "Read a short passage and answer comprehension questions in an exam-inspired format.",
            cefrLevel: CefrLevelConstants.B1,
            primarySkill: CurriculumSkillConstants.Reading,
            secondarySkillsJson: """["vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.ExamInspired}","{CurriculumContextTagConstants.StudyAcademic}"]""",
            focusTagsJson: """["reading_comprehension","exam_practice"]""",
            prerequisiteKeysJson: $"""["a1.vocabulary.everyday_basics"]""",
            recommendedOrder: 270,
            difficultyBand: 3,
            isActive: true,
            isReviewable: true,
            isExamInspired: true),

        // ── B2: Structured explanations ───────────────────────────────────────
        new(
            key: "b2.speaking.structured_explanations",
            title: "Giving Structured Explanations",
            description: "Explain a process, idea, or situation with clear structure: context, detail, conclusion.",
            cefrLevel: CefrLevelConstants.B2,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["fluency","vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.StudyAcademic}"]""",
            focusTagsJson: """["fluency","workplace_communication","presentation_skills"]""",
            prerequisiteKeysJson: $"""["b1.speaking.opinions_with_reasons"]""",
            recommendedOrder: 310,
            difficultyBand: 4,
            isActive: true,
            isReviewable: true),

        // ── B2: Formal / informal tone control ────────────────────────────────
        new(
            key: "b2.writing.tone_control",
            title: "Formal and Informal Tone Control",
            description: "Adjust writing register between formal (reports, emails) and informal (messages, chats).",
            cefrLevel: CefrLevelConstants.B2,
            primarySkill: CurriculumSkillConstants.Writing,
            secondarySkillsJson: """["vocabulary","grammar"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.WritingConfidence}","{CurriculumContextTagConstants.GeneralEnglish}"]""",
            focusTagsJson: """["email_writing","workplace_communication","register"]""",
            prerequisiteKeysJson: $"""["b1.writing.clear_emails"]""",
            recommendedOrder: 320,
            difficultyBand: 4,
            isActive: true,
            isReviewable: true),

        // ── B2: Summarising longer information ────────────────────────────────
        new(
            key: "b2.listening.summarise_longer",
            title: "Summarising Longer Spoken Information",
            description: "Listen to a longer talk and identify main points, supporting details, and speaker stance.",
            cefrLevel: CefrLevelConstants.B2,
            primarySkill: CurriculumSkillConstants.Listening,
            secondarySkillsJson: """["writing","vocabulary"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.ListeningConfidence}","{CurriculumContextTagConstants.StudyAcademic}","{CurriculumContextTagConstants.Workplace}"]""",
            focusTagsJson: """["listening_comprehension","note_taking"]""",
            prerequisiteKeysJson: $"""["b1.listening.summarise_spoken"]""",
            recommendedOrder: 330,
            difficultyBand: 4,
            isActive: true,
            isReviewable: false),

        // ── B2: Clarifying complex points ─────────────────────────────────────
        new(
            key: "b2.speaking.clarifying_complex",
            title: "Clarifying Complex Points",
            description: "Ask for and give clarification when discussing complex topics; paraphrase effectively.",
            cefrLevel: CefrLevelConstants.B2,
            primarySkill: CurriculumSkillConstants.Speaking,
            secondarySkillsJson: """["listening","fluency"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.Workplace}","{CurriculumContextTagConstants.GeneralEnglish}","{CurriculumContextTagConstants.StudyAcademic}"]""",
            focusTagsJson: """["workplace_communication","conversation_skills"]""",
            prerequisiteKeysJson: $"""["b2.speaking.structured_explanations"]""",
            recommendedOrder: 340,
            difficultyBand: 4,
            isActive: true,
            isReviewable: true),

        // ── B2: Argument structure / writing ─────────────────────────────────
        new(
            key: "b2.writing.argument_structure",
            title: "Structuring a Written Argument",
            description: "Plan and write a short argument or opinion piece with clear thesis, evidence, and conclusion.",
            cefrLevel: CefrLevelConstants.B2,
            primarySkill: CurriculumSkillConstants.Writing,
            secondarySkillsJson: """["vocabulary","grammar","fluency"]""",
            contextTagsJson: $"""["{CurriculumContextTagConstants.StudyAcademic}","{CurriculumContextTagConstants.ExamInspired}","{CurriculumContextTagConstants.WritingConfidence}"]""",
            focusTagsJson: """["academic_writing","exam_practice","argument_skills"]""",
            prerequisiteKeysJson: $"""["b2.writing.tone_control"]""",
            recommendedOrder: 350,
            difficultyBand: 5,
            isActive: true,
            isReviewable: true,
            isExamInspired: true,
            teachingNotes: "Exam-inspired format. Teach PEEL or similar paragraph structure."),
    ];
}

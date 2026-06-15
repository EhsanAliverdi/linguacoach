using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Generates a VocabularyPractice activity deterministically from a student's
/// New/Practising vocabulary items. No AI call required.
/// </summary>
public sealed class VocabularyPracticeGenerator
{
    private const int MinVocabItemsRequired = 3;
    private const int MaxPracticeItems = 5;

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<VocabularyPracticeGenerator> _logger;

    public VocabularyPracticeGenerator(LinguaCoachDbContext db, ILogger<VocabularyPracticeGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if the student has enough vocabulary items to practice.
    /// </summary>
    public async Task<bool> HasEnoughVocabularyAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var count = await _db.StudentVocabularyItems
            .CountAsync(v => v.StudentProfileId == studentProfileId
                          && (v.Status == VocabularyItemStatus.New || v.Status == VocabularyItemStatus.Practising), ct);
        return count >= MinVocabItemsRequired;
    }

    /// <summary>
    /// Generates VocabularyPractice activity content JSON from the student's vocabulary.
    /// Selects up to 5 items, prioritising New items then Practising items.
    /// </summary>
    public async Task<(string ContentJson, string Title)> GenerateContentAsync(
        Guid studentProfileId, CancellationToken ct = default)
    {
        var items = await _db.StudentVocabularyItems
            .Where(v => v.StudentProfileId == studentProfileId
                     && (v.Status == VocabularyItemStatus.New || v.Status == VocabularyItemStatus.Practising))
            .OrderBy(v => v.Status == VocabularyItemStatus.New ? 0 : 1) // New first
                .ThenBy(v => v.StrengthScore)                            // Weakest first within status
                .ThenBy(v => v.LastSeenAtUtc ?? DateTime.MinValue)      // Least recently seen
            .Take(MaxPracticeItems)
            .ToListAsync(ct);

        if (items.Count < MinVocabItemsRequired)
            throw new InvalidOperationException(
                $"Not enough vocabulary items to generate practice. Required: {MinVocabItemsRequired}, found: {items.Count}.");

        var practiceItems = items.Select(BuildFillBlankItem).ToList();

        // Determine activity title from most common category
        var title = BuildTitle(items);

        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = ModuleStageSchema.Version,
            title,
            moduleGoal = "Understand and use these workplace words and phrases accurately in context.",
            primarySkill = "vocabulary",
            secondarySkills = new[] { "reading", "writing" },
            exerciseType = "vocabulary_practice",
            learnContent = new
            {
                teachingTitle = title.Replace("Practise", "Learn"),
                explanation = "These words and phrases help you sound clearer and more professional at work. Read the meaning, example, and usage note before you practise spelling and context.",
                keyPoints = new[]
                {
                    "Check the meaning before choosing the word.",
                    "Notice the word form and common phrase pattern.",
                    "Use the phrase in a workplace context and professional tone."
                },
                examples = practiceItems.Select(i => new
                {
                    phrase = i.Term,
                    meaning = i.Explanation,
                    note = i.Hint
                }),
                strategy = "Say the full example sentence aloud, then cover the word and recall it from the workplace context.",
                commonMistakes = new[]
                {
                    "Choosing a word with a similar meaning but the wrong workplace tone.",
                    "Using the right word with the wrong spelling or phrase pattern."
                },
                sourceLanguageSupport = (string?)null
            },
            practiceContent = new
            {
                instructions = "Fill in the blank with the most professional phrase.",
                scenario = "Workplace vocabulary review",
                task = "Type the missing word or phrase for each sentence.",
                exerciseData = new
                {
                    items = practiceItems.Select(i => new
                    {
                        vocabularyItemId = i.VocabularyItemId,
                        term = i.Term,
                        meaning = i.Explanation,
                        example = i.Prompt,
                        prompt = i.Prompt,
                        correctAnswer = i.ExpectedAnswer,
                        expectedAnswer = i.ExpectedAnswer,
                        hint = i.Hint,
                        explanation = i.Explanation,
                    }),
                    practiceMode = "fill_blank",
                    successChecklist = new[]
                    {
                        "Identify the correct meaning.",
                        "Type the word or phrase accurately.",
                        "Use the phrase in a suitable workplace context."
                    }
                }
            },
            feedbackPlan = new
            {
                evaluationCriteria = new[] { "Meaning accuracy", "Context use", "Word form", "Spelling", "Collocation" },
                rubric = new[]
                {
                    new { criterion = "Meaning accuracy", description = "The student understands the meaning of the target vocabulary.", weight = 0.35 },
                    new { criterion = "Context use", description = "The student can use the word or phrase in an appropriate context.", weight = 0.25 },
                    new { criterion = "Word form and spelling", description = "The student uses the correct form and spelling.", weight = 0.25 },
                    new { criterion = "Collocation", description = "The student recognises common word partnerships or phrase patterns.", weight = 0.15 }
                },
                feedbackFocus = "Help the student remember meaning, usage, spelling, and natural collocations.",
                successCriteria = new[]
                {
                    "The student identifies the correct meaning.",
                    "The student uses the word in a suitable context.",
                    "The student recognises common collocations or phrase patterns."
                }
            }
        });

        _logger.LogInformation(
            "VocabularyPractice content generated StudentProfileId={ProfileId} ItemCount={Count}",
            studentProfileId, practiceItems.Count);

        return (contentJson, title);
    }

    private static FillBlankItem BuildFillBlankItem(StudentVocabularyItem item)
    {
        // Build prompt by replacing the term with a blank in the suggested phrase,
        // or construct a default prompt if no suggested phrase is available.
        var term = item.Term.Trim();
        var termCapitalised = char.ToUpperInvariant(term[0]) + term[1..];

        string prompt;
        if (!string.IsNullOrWhiteSpace(item.SuggestedPhrase))
        {
            // Replace the term (case-insensitive) in the suggested phrase with a blank
            var phrase = item.SuggestedPhrase.Trim();
            var idx = phrase.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                prompt = phrase[..idx] + "_____" + phrase[(idx + term.Length)..];
            else
                prompt = $"_____ {phrase.TrimStart()}";
        }
        else if (!string.IsNullOrWhiteSpace(item.ExampleSentence))
        {
            var sentence = item.ExampleSentence.Trim();
            var idx = sentence.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                prompt = sentence[..idx] + "_____" + sentence[(idx + term.Length)..];
            else
                prompt = $"Complete: {sentence.Replace(termCapitalised, "_____")}";
        }
        else
        {
            prompt = $"Use '_____ ' in a professional workplace context.";
        }

        return new FillBlankItem(
            VocabularyItemId: item.Id,
            Term: term,
            Prompt: prompt,
            ExpectedAnswer: term,
            Hint: $"Think about: {item.MeaningOrExplanation.Split('.')[0].Trim()}.",
            Explanation: item.MeaningOrExplanation);
    }

    private static string BuildTitle(IReadOnlyList<StudentVocabularyItem> items)
    {
        // Pick a friendly title based on the dominant category
        var topCategory = items
            .GroupBy(i => i.Category)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return topCategory switch
        {
            "polite_request" => "Practise polite workplace requests",
            "tone_softener" => "Practise professional tone",
            "workplace_phrase" => "Practise workplace phrases",
            "grammar_pattern" => "Practise grammar patterns",
            "connector" => "Practise connecting phrases",
            "project_vocabulary" => "Practise project vocabulary",
            "common_mistake" => "Practise avoiding common mistakes",
            _ => "Vocabulary practice",
        };
    }

    private sealed record FillBlankItem(
        Guid VocabularyItemId,
        string Term,
        string Prompt,
        string ExpectedAnswer,
        string Hint,
        string Explanation);
}

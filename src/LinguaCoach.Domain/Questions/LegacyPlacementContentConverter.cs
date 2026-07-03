using System.Text.RegularExpressions;

namespace LinguaCoach.Domain.Questions;

/// <summary>
/// Converts the legacy flat PlacementItemDefinition/PlacementAssessmentItem fields (Prompt with
/// choices embedded as "(A) ... (B) ..." text, a separate CorrectAnswer, optional ReadingPassage/
/// ListeningAudioScript) into the shared QuestionContent schema (Unified Question-Schema Phase 2).
/// Used to backfill ContentJson for existing rows and to keep it in sync going forward until the
/// old flat columns are dropped (Phase 7). Lives in Domain (not Infrastructure) so Persistence's
/// seeders can call it directly without crossing the Persistence→Infrastructure boundary.
/// </summary>
public static class LegacyPlacementContentConverter
{
    private static readonly Regex ChoicePattern = new(@"\(([A-Z])\)\s*([^(]+?)(?=\s*\([A-Z]\)|$)", RegexOptions.Compiled);

    public static QuestionContent FromLegacyItem(
        string itemType, string prompt, string correctAnswer,
        string? readingPassage, string? listeningAudioScript)
    {
        QuestionContent leaf = itemType switch
        {
            "multiple_choice" => BuildSingleChoice(prompt, correctAnswer),
            "gap_fill" => new GapFillQuestion { QuestionText = prompt, CorrectAnswer = correctAnswer },
            _ => new FreeTextQuestion { QuestionText = prompt },
        };

        if (!string.IsNullOrWhiteSpace(listeningAudioScript))
            return new ListeningGroupQuestion { AudioScript = listeningAudioScript, Questions = [leaf] };

        if (!string.IsNullOrWhiteSpace(readingPassage))
            return new ReadingGroupQuestion { Passage = readingPassage, Questions = [leaf] };

        return leaf;
    }

    private static SingleChoiceQuestion BuildSingleChoice(string prompt, string correctAnswerKey)
    {
        var choiceIdx = prompt.IndexOf("(A)", StringComparison.OrdinalIgnoreCase);
        var questionText = choiceIdx > 0 ? prompt[..choiceIdx].Trim() : prompt.Trim();

        var choices = ChoicePattern.Matches(prompt)
            .Select(m => new ChoiceOption { Key = m.Groups[1].Value.ToUpperInvariant(), Label = m.Groups[2].Value.Trim() })
            .ToList();

        if (choices.Count == 0)
        {
            // No embedded "(A) ..." choices found (shouldn't happen for multiple_choice items in
            // practice) — fall back to a single implicit choice so the correct answer is never lost.
            choices.Add(new ChoiceOption { Key = correctAnswerKey, Label = correctAnswerKey });
        }

        return new SingleChoiceQuestion { QuestionText = questionText, Choices = choices, CorrectAnswerKey = correctAnswerKey };
    }
}

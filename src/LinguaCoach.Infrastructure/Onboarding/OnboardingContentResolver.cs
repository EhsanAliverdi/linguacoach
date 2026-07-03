using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

/// <summary>
/// Resolves a step's Content, filling in dynamically-sourced choices (Unified Question-Schema
/// Phase 6b's OptionsSource — currently only "languages", the one case that genuinely needs
/// data-driven options rather than admin-authored ones).
/// </summary>
public static class OnboardingContentResolver
{
    public static async Task<QuestionContent?> ResolveAsync(QuestionContent? content, LinguaCoachDbContext db, CancellationToken ct)
    {
        if (content is SingleChoiceQuestion { OptionsSource: "languages" } sc)
        {
            return new SingleChoiceQuestion
            {
                Id = sc.Id, QuestionText = sc.QuestionText, CorrectAnswerKey = sc.CorrectAnswerKey,
                OptionsSource = sc.OptionsSource, Choices = await BuildLanguageChoicesAsync(db, ct),
            };
        }

        if (content is MultipleChoiceQuestion { OptionsSource: "languages" } mc)
        {
            return new MultipleChoiceQuestion
            {
                Id = mc.Id, QuestionText = mc.QuestionText, CorrectAnswerKeys = mc.CorrectAnswerKeys,
                OptionsSource = mc.OptionsSource, Choices = await BuildLanguageChoicesAsync(db, ct),
            };
        }

        return content;
    }

    public static async Task<List<ChoiceOption>> BuildLanguageChoicesAsync(LinguaCoachDbContext db, CancellationToken ct)
    {
        var languages = await db.Languages
            .OrderBy(l => l.Name)
            .Select(l => new ChoiceOption { Key = l.Code, Label = l.Name })
            .ToListAsync(ct);

        var choices = new List<ChoiceOption> { new() { Key = "none", Label = "No, English only" } };
        choices.AddRange(languages);
        return choices;
    }
}

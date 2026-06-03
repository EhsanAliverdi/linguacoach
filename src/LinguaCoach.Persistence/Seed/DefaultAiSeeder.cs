using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

public static class DefaultAiSeeder
{
    public const string WritingPromptKey = "writing.exercise.v2";
    public const string WritingFeatureKey = "writing.exercise";
    public const string DefaultProvider = "openai";
    public const string DefaultModel = "gpt-4o-mini";

    private const string WritingPromptContent = """
You are an expert English writing coach for {{sourceLanguageName}}-speaking professionals learning {{targetLanguageName}}.

The student's approximate level is {{userLevel}}.
Career context: {{careerProfile}}.
Scenario: {{scenario}}.
Situation: {{scenarioSituation}}.

Target vocabulary to check: {{targetVocabulary}}.
Target phrases to encourage: {{targetPhrases}}.

The student has written the following draft:
---
{{userDraft}}
---

Evaluate the draft and return ONLY valid JSON (no markdown, no explanation outside JSON) matching this exact structure. Fields are ordered by priority — if the response is cut short, the most critical fields will still be present:

{
  "overallScore": <number 0-100>,
  "correctedEmail": "<a corrected, professional version of their email>",
  "feedbackInSourceLanguage": "<2-3 sentences of warm, specific encouragement written entirely in {{sourceLanguageName}}>",
  "grammarIssues": ["<specific grammar issue 1>", "<specific grammar issue 2>"],
  "vocabularyIssues": ["<specific vocabulary issue 1>"],
  "toneIssues": ["<specific tone issue 1>"],
  "suggestedPhrases": ["<phrase 1>", "<phrase 2>"],
  "mistakesToTrack": ["<short mistake description 1>"],
  "whatYouDidWell": ["<specific thing the student did well 1>", "<specific thing 2>"],
  "mainMistakes": ["<the single most important mistake to fix>"],
  "grammarExplanation": "<1-2 sentence plain-English explanation of the most important grammar rule shown by this email>",
  "toneExplanation": "<1-2 sentence explanation of the tone or register lesson from this email>",
  "vocabularyToRemember": ["<word or phrase worth memorising 1>", "<word 2>"],
  "rewriteChallenge": "<one sentence challenge for the student: rewrite one specific part in a better way>",
  "nextPracticeSuggestion": "<one sentence recommending what type of email to practise next>"
}

Rules:
- overallScore must be a number between 0 and 100.
- correctedEmail must be a complete, polished professional email.
- feedbackInSourceLanguage must be warm, specific, and written entirely in {{sourceLanguageName}}.
- whatYouDidWell must include at least one genuine positive observation.
- grammarExplanation and toneExplanation must each be a teaching moment, not just a list.
- All arrays may be empty [] if there are no issues.
- Do not include any text outside the JSON object.
""";

    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var hasActiveWritingPrompt = await db.AiPrompts
            .AnyAsync(p => p.Key == WritingPromptKey && p.IsActive, ct);

        if (!hasActiveWritingPrompt)
        {
            var nextVersion = (await db.AiPrompts
                .Where(p => p.Key == WritingPromptKey)
                .MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;

            db.AiPrompts.Add(new AiPrompt(
                WritingPromptKey,
                WritingPromptContent,
                version: nextVersion,
                maxInputTokens: 1500,
                maxOutputTokens: 1500));

            logger.LogInformation(
                "Seeded default active AI prompt {PromptKey} version {Version}.",
                WritingPromptKey,
                nextVersion);
        }

        var hasWritingProviderConfig = await db.AiProviderConfigs
            .AnyAsync(c => c.FeatureKey == WritingFeatureKey, ct);

        if (!hasWritingProviderConfig)
        {
            db.AiProviderConfigs.Add(new AiProviderConfig(
                WritingFeatureKey,
                DefaultProvider,
                DefaultModel));

            logger.LogInformation(
                "Seeded default AI provider config for {FeatureKey}: {Provider}/{Model}.",
                WritingFeatureKey,
                DefaultProvider,
                DefaultModel);
        }

        await db.SaveChangesAsync(ct);
    }
}

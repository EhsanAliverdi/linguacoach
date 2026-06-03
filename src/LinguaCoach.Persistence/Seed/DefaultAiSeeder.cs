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

    public const string ActivityGenerateWritingKey = "activity_generate_writing";
    public const string ActivityEvaluateWritingKey = "activity_evaluate_writing";

    private const string ActivityGenerateWritingContent = """
You are an expert English language teacher creating a writing practice activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Generate a realistic workplace writing scenario and return ONLY valid JSON (no markdown) matching this exact structure:

{
  "title": "<short descriptive title for this activity, 5-10 words>",
  "situation": "<2-3 sentences describing the realistic workplace situation the student must respond to>",
  "learningGoal": "<one sentence stating what communication skill this activity practises>",
  "targetPhrases": ["<useful phrase 1>", "<useful phrase 2>", "<useful phrase 3>", "<useful phrase 4>", "<useful phrase 5>"],
  "targetVocabulary": ["<word 1>", "<word 2>", "<word 3>", "<word 4>", "<word 5>"],
  "exampleText": "<a complete, polished example response the student can study>",
  "commonMistakeToAvoid": "<one sentence describing the single most common mistake {{sourceLanguageName}} speakers make in this type of email, and how to avoid it>",
  "instructionInSourceLanguage": "<2-3 sentences in {{sourceLanguageName}} telling the student what to write and why>"
}

Rules:
- The situation must be specific and believable for {{careerContext}} professionals.
- targetPhrases must be phrases the student should actively try to use in their response.
- The example must be a complete, professional email (not a fragment).
- instructionInSourceLanguage must be written entirely in {{sourceLanguageName}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateWritingContent = """
You are an expert English writing coach evaluating a professional email written by a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content:
{{activityContent}}

Student's submitted email:
---
{{studentSubmission}}
---

Evaluate the submission and return ONLY valid JSON (no markdown) matching this exact structure:

{
  "overallScore": <number 0-100>,
  "correctedEmail": "<a corrected, professional version of their email>",
  "feedbackInSourceLanguage": "<2-3 sentences of warm, specific encouragement written entirely in {{sourceLanguageName}}>",
  "whatYouDidWell": ["<specific strength 1>", "<specific strength 2>"],
  "mainMistakes": ["<most important mistake to fix>"],
  "grammarIssues": ["<specific grammar issue>"],
  "vocabularyIssues": ["<specific vocabulary issue>"],
  "toneIssues": ["<specific tone issue>"],
  "grammarExplanation": "<1-2 sentence teaching moment on the key grammar rule>",
  "toneExplanation": "<1-2 sentence teaching moment on professional tone or register>",
  "vocabularyToRemember": ["<word or phrase worth memorising>"],
  "rewriteChallenge": "<one sentence challenge: rewrite one specific part better>",
  "nextPracticeSuggestion": "<one sentence recommending what to practise next>"
}

Rules:
- overallScore must be a number between 0 and 100.
- whatYouDidWell must include at least one genuine positive observation.
- feedbackInSourceLanguage must be written entirely in {{sourceLanguageName}}.
- All arrays may be empty [] if there are no issues.
- Do not include any text outside the JSON object.
""";

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

        // Activity generation prompt
        var hasGenerateWritingPrompt = await db.AiPrompts
            .AnyAsync(p => p.Key == ActivityGenerateWritingKey && p.IsActive, ct);

        if (!hasGenerateWritingPrompt)
        {
            var nextVersion = (await db.AiPrompts
                .Where(p => p.Key == ActivityGenerateWritingKey)
                .MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;

            db.AiPrompts.Add(new AiPrompt(
                ActivityGenerateWritingKey,
                ActivityGenerateWritingContent,
                version: nextVersion,
                maxInputTokens: 800,
                maxOutputTokens: 1000));

            logger.LogInformation(
                "Seeded AI prompt {PromptKey} version {Version}.",
                ActivityGenerateWritingKey, nextVersion);
        }

        // Activity evaluation prompt
        var hasEvaluateWritingPrompt = await db.AiPrompts
            .AnyAsync(p => p.Key == ActivityEvaluateWritingKey && p.IsActive, ct);

        if (!hasEvaluateWritingPrompt)
        {
            var nextVersion = (await db.AiPrompts
                .Where(p => p.Key == ActivityEvaluateWritingKey)
                .MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;

            db.AiPrompts.Add(new AiPrompt(
                ActivityEvaluateWritingKey,
                ActivityEvaluateWritingContent,
                version: nextVersion,
                maxInputTokens: 2000,
                maxOutputTokens: 1500));

            logger.LogInformation(
                "Seeded AI prompt {PromptKey} version {Version}.",
                ActivityEvaluateWritingKey, nextVersion);
        }

        await db.SaveChangesAsync(ct);
    }
}

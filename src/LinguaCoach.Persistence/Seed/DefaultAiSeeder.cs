using System.Security.Cryptography;
using System.Text;
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
    public const string ActivityGenerateListeningKey = "activity_generate_listening";
    public const string ActivityEvaluateWritingKey = "activity_evaluate_writing";
    public const string LearningPathGenerateKey = "learning_path_generate";
    public const string StudentMemoryUpdateKey = "student_memory_update";
    public const string LearningPathGenerateAdaptiveKey = "learning_path_generate_adaptive";
    public const string VocabularyExtractFromAttemptKey = "vocabulary_extract_from_attempt";

    private const string ActivityGenerateWritingContent = """
You are an expert English language teacher creating a writing practice activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Choose a varied, realistic workplace writing task. Examples of task types (pick one that fits the context):
- workplace email (follow-up, update, request, confirmation)
- Teams or chat message (brief professional message to a colleague)
- incident explanation (explain a delay or problem professionally)
- polite request (ask for approval, information, or support)
- complaint response (respond professionally to a complaint)
- meeting follow-up (summarise action points after a meeting)
- apology message (apologise professionally for an error)
- clarification message (ask for or provide clarification)
- update to manager (brief status update on a task)
- customer support response (respond helpfully to a client question)

Generate a realistic task and return ONLY valid JSON (no markdown) matching this exact structure:

{
  "title": "<short descriptive title for this activity, 5-10 words>",
  "taskType": "<one of: workplace-email | chat-message | incident-explanation | polite-request | complaint-response | meeting-follow-up | apology-message | clarification-message | manager-update | customer-support>",
  "situation": "<2-3 sentences describing the realistic workplace situation the student must respond to>",
  "audience": "<who the student is writing to, e.g. 'your direct manager', 'a client', 'a colleague'>",
  "tone": "<expected tone: formal | semi-formal | polite>",
  "expectedLength": "<guidance on length, e.g. '3-5 sentences' or '1 short paragraph'>",
  "learningGoal": "<one sentence stating what communication skill this activity practises>",
  "skillFocus": "<one key skill, e.g. 'polite requests', 'professional tone', 'follow-up emails'>",
  "targetPhrases": ["<useful phrase 1>", "<useful phrase 2>", "<useful phrase 3>", "<useful phrase 4>", "<useful phrase 5>"],
  "targetVocabulary": ["<word 1>", "<word 2>", "<word 3>", "<word 4>", "<word 5>"],
  "exampleText": "<a complete, polished example response the student can study>",
  "commonMistakeToAvoid": "<one sentence describing the single most common mistake {{sourceLanguageName}} speakers make in this type of message, and how to avoid it>",
  "instructionInSourceLanguage": "<2-3 sentences in {{sourceLanguageName}} telling the student what to write and why>"
}

Rules:
- The situation must be specific and believable for {{careerContext}} professionals.
- Do not repeat the same task type every time — vary it based on the context.
- targetPhrases must be phrases the student should actively try to use in their response.
- The example must be a complete, professional response (not a fragment).
- instructionInSourceLanguage must be written entirely in {{sourceLanguageName}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateWritingContent = """
You are a warm, professional English writing coach evaluating a workplace message written by a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content:
{{activityContent}}

Student's submitted message:
---
{{studentSubmission}}
---

Your job is to coach the student to improve their own writing — not to replace it.

IMPORTANT: The most valuable output is the "changes" list. Focus on targeted, specific improvements.
If the student has many issues (5 or more), set "focusFirst": true and include only the 3-5 most important changes.
The "improvedVersion" is a reference — label it clearly as a suggestion, not the correct answer.

Return ONLY valid JSON (no markdown, no text outside the JSON):

{
  "overallScore": <number 0-100>,
  "coachSummary": "<1-2 warm sentences summarising the overall quality and the most important thing to improve>",
  "whatYouDidWell": ["<specific genuine strength 1>", "<specific genuine strength 2>"],
  "focusFirst": <true if student has 5+ issues and you are limiting to top 3-5, otherwise false>,
  "changes": [
    {
      "type": "<replace | add | remove | reorder>",
      "original": "<the exact phrase from the student's text>",
      "suggested": "<the improved phrase>",
      "reason": "<1 sentence plain explanation of why this change matters>",
      "category": "<grammar | vocabulary | tone | clarity | structure | punctuation>",
      "severity": "<high | medium | low>"
    }
  ],
  "grammarIssues": ["<specific grammar issue>"],
  "vocabularyIssues": ["<specific vocabulary issue>"],
  "toneIssues": ["<specific tone issue>"],
  "clarityIssues": ["<specific clarity issue>"],
  "feedbackInSourceLanguage": "<2-3 sentences of warm, specific encouragement written entirely in {{sourceLanguageName}}>",
  "miniLesson": "<1-2 sentences teaching the single most important rule illustrated by this submission>",
  "nextImprovementStep": "<one actionable sentence telling the student exactly what to try when they rewrite>",
  "improvedVersion": "<a suggested improved version of the student's message — label this as a suggestion, not the answer>",
  "rewriteChallenge": "<one sentence challenge: rewrite one specific part better>",
  "nextPracticeSuggestion": "<one sentence recommending what to practise next>"
}

Rules:
- overallScore must be a number between 0 and 100.
- whatYouDidWell must include at least one genuine positive observation.
- changes must reference exact phrases from the student's submission — not invented examples.
- changes should be ordered by severity (high first).
- feedbackInSourceLanguage must be written entirely in {{sourceLanguageName}}.
- All arrays may be empty [] if there are no issues.
- Do not include any text outside the JSON object.
- The improved version is a coaching reference — do not frame it as "the correct answer".
""";

    private const string ActivityGenerateListeningContent = """
You are an expert English workplace communication coach creating a text-based listening comprehension activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to consider: {{recentMistakes}}

Create a realistic short workplace voice-message task. There is no real audio yet, so the transcript must be hidden from the student until after submit.

Return ONLY valid JSON (no markdown) matching this exact structure:

{
  "activityType": "ListeningComprehension",
  "title": "<short descriptive title, 5-10 words>",
  "scenario": "<1-2 sentences describing who left the message and why>",
  "instructions": "Read the situation first. Then answer the questions as if you listened to the message. The transcript is hidden until after you submit.",
  "speakerRole": "<workplace role speaking>",
  "listenerRole": "<student workplace role>",
  "difficulty": "{{cefrLevel}}",
  "audioScript": "<short realistic workplace voice message, 35-80 words>",
  "transcriptAvailableAfterSubmit": true,
  "questions": [
    {
      "id": "q1",
      "question": "<question answerable from the script>",
      "expectedAnswer": "<short expected answer for backend scoring>",
      "type": "short_answer"
    }
  ],
  "responseTask": {
    "prompt": "<optional short workplace reply task>",
    "expectedFocus": "<what the response should include>"
  }
}

Rules:
- Include 2-4 comprehension questions.
- Questions must be answerable from audioScript.
- Use realistic workplace communication for {{careerContext}}.
- Keep vocabulary appropriate for {{cefrLevel}}.
- Do not use real company names, real person names, secrets, phone numbers, or sensitive content.
- expectedAnswer is for backend evaluation only.
- Do not include text outside the JSON object.
""";

    private const string LearningPathGenerateContent = """
You are an expert English language curriculum designer creating a personalised learning path for a professional.

Student details:
- Career context: {{careerContext}}
- Current English level: {{cefrLevel}}
- Learning goal: {{skillFocus}}
- Source language: {{sourceLanguageName}}
- Target language: {{targetLanguageName}}

Design exactly {{moduleCount}} progressive learning modules for this student. Return ONLY valid JSON (no markdown) matching this exact structure:

{
  "pathTitle": "<concise path title, e.g. 'Workplace English for Document Controllers — B1'>",
  "modules": [
    {
      "order": 1,
      "title": "<module title, 3-6 words>",
      "description": "<1-2 sentences describing what the student will practise in this module>"
    }
  ]
}

Rules:
- pathTitle must include the career context and CEFR level.
- Each module must address a distinct workplace communication skill relevant to {{careerContext}}.
- Modules must progress from foundational to more advanced communication.
- Descriptions must be specific to {{careerContext}} work, not generic.
- Return exactly {{moduleCount}} modules.
- Do not include any text outside the JSON object.
""";

    private const string StudentMemoryUpdateContent = """
You update a compact learning memory for SpeakPath, a workplace English coach.

Input context:
{{memoryUpdateContext}}

Return ONLY valid JSON. Return deltas only, not a full rewrite:

{
  "journeySummaryDelta": "<one short sentence, max 30 words>",
  "newStrengths": ["<short observed strength>"],
  "newWeaknesses": ["<short weakness to practise>"],
  "recurringMistakesToAdd": ["<short recurring mistake>"],
  "coveredScenariosToAdd": ["<short scenario label>"],
  "weakSkillKeys": ["formal_tone"],
  "strongSkillKeys": ["workplace_vocabulary"],
  "recommendedNextFocus": ["<short next focus>"]
}

Allowed skill keys:
grammar_accuracy, formal_tone, sentence_clarity, message_structure,
workplace_vocabulary, concise_writing, softening_language,
summarising_information, clarifying_questions, escalation_language.

Rules:
- Keep every array small: 0-3 items.
- Do not include markdown.
- Do not quote or store the student's full submitted text.
- Focus on workplace communication coaching, not generic grammar.
- Main feedback is handled elsewhere; this is only compact memory.
""";

    private const string VocabularyExtractFromAttemptContent = """
You are a vocabulary coach for SpeakPath, a workplace English learning platform.

Extract 0-5 useful vocabulary items from this writing attempt to help the student improve their workplace English.

Context:
{{extractionContext}}

Return ONLY valid JSON (no markdown):

{
  "items": [
    {
      "term": "<the word or phrase to learn, lowercased>",
      "suggestedPhrase": "<a complete workplace sentence showing this phrase in use>",
      "meaningOrExplanation": "<1-2 sentences: what this means and why it matters in workplace English>",
      "exampleSentence": "<another example sentence in a different workplace context>",
      "category": "<one of: workplace_phrase | polite_request | grammar_pattern | connector | tone_softener | project_vocabulary | common_mistake | useful_expression>",
      "reason": "<one sentence: why this item is useful for this student based on their submission>"
    }
  ]
}

Rules:
- Return 0-5 items only. If there is nothing useful, return an empty items array.
- Prefer tone softeners, polite requests, connectors, and professional expressions.
- Prefer phrases the student got wrong or that would improve their message.
- Do NOT extract simple everyday words (e.g. "send", "the", "please").
- Do NOT extract proper nouns, names, phone numbers, email addresses, company names, or IDs.
- Do NOT extract anything that looks like private or sensitive data.
- Do NOT include items already in the student's known terms.
- Keep explanations friendly and learner-appropriate.
- All text must be in English.
- Do not include any text outside the JSON object.
""";

    private const string LearningPathGenerateAdaptiveContent = """
You are designing the next 3-5 workplace writing modules for SpeakPath.

Adaptive context:
{{adaptiveGenerationContext}}

Return ONLY valid JSON:

{
  "journeySummary": "<short explanation of why these modules are next>",
  "modules": [
    {
      "order": 1,
      "title": "<3-7 word module title>",
      "description": "<1-2 sentences describing the workplace practice>",
      "focusSkill": "<one allowed skill key or short skill label>",
      "reason": "<why this is recommended from the memory>",
      "difficulty": "B1+",
      "fingerprint": {
        "communicationMode": "email",
        "scenarioType": "delay_explanation",
        "audience": "manager",
        "tone": "professional_apologetic",
        "difficulty": "B1+",
        "grammarFocus": "modal_verbs",
        "vocabularyTheme": "project_schedule"
      },
      "avoidsRepeating": ["<covered scenario avoided>"]
    }
  ]
}

Rules:
- Generate 3-5 modules only.
- Do not repeat an existing scenarioType + audience + communicationMode.
- Reuse weak skills through new workplace situations.
- Progress difficulty gradually.
- Keep modules relevant to the student's career context.
- Do not generate a generic full 10-module path.
- Do not include any text outside JSON.
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
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateWritingKey, ActivityGenerateWritingContent,
            maxInputTokens: 900, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListeningKey, ActivityGenerateListeningContent,
            maxInputTokens: 900, maxOutputTokens: 1000, ct);

        // Activity evaluation prompt (v2 — structured diff/changes output)
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateWritingKey, ActivityEvaluateWritingContent,
            maxInputTokens: 2000, maxOutputTokens: 2000, ct);

        // Learning path generation prompt
        await SeedOrUpgradePromptAsync(db, logger,
            LearningPathGenerateKey, LearningPathGenerateContent,
            maxInputTokens: 600, maxOutputTokens: 800, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            StudentMemoryUpdateKey, StudentMemoryUpdateContent,
            maxInputTokens: 1200, maxOutputTokens: 700, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            LearningPathGenerateAdaptiveKey, LearningPathGenerateAdaptiveContent,
            maxInputTokens: 1800, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            VocabularyExtractFromAttemptKey, VocabularyExtractFromAttemptContent,
            maxInputTokens: 1500, maxOutputTokens: 600, ct);

        await db.SaveChangesAsync(ct);
    }

    // Seeds a prompt if no active version exists, or upgrades it if the content has changed.
    private static async Task SeedOrUpgradePromptAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        string key,
        string content,
        int? maxInputTokens,
        int? maxOutputTokens,
        CancellationToken ct)
    {
        var contentHash = ComputeHash(content);
        var activePrompt = await db.AiPrompts
            .Where(p => p.Key == key && p.IsActive)
            .FirstOrDefaultAsync(ct);

        if (activePrompt is not null && ComputeHash(activePrompt.Content) == contentHash)
            return; // Already up to date

        // Deactivate old active version if content changed
        if (activePrompt is not null)
        {
            activePrompt.Deactivate();
            logger.LogInformation(
                "Deactivated outdated AI prompt {PromptKey} version {Version}.",
                key, activePrompt.Version);
        }

        var nextVersion = (await db.AiPrompts
            .Where(p => p.Key == key)
            .MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;

        db.AiPrompts.Add(new AiPrompt(key, content,
            version: nextVersion,
            maxInputTokens: maxInputTokens,
            maxOutputTokens: maxOutputTokens));

        logger.LogInformation(
            "Seeded AI prompt {PromptKey} version {Version}.",
            key, nextVersion);
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16];
    }
}

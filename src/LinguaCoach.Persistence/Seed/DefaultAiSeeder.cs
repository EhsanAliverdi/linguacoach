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
    public const string ActivityTemplateGenerateInstanceKey = "activity_template_generate_instance";
    public const string ResourceCandidateAnalyzeKey = "resource_candidate_analyze";
    public const string ResourceImportProposeColumnMappingKey = "resource_import_propose_column_mapping";
    public const string ActivityGenerateListeningKey = "activity_generate_listening";
    public const string ActivityGenerateSpeakingRolePlayKey = "activity_generate_speaking_roleplay";
    public const string ActivityEvaluateWritingKey = "activity_evaluate_writing";
    public const string ActivityEvaluateSpeakingRolePlayKey = "activity_evaluate_speaking_roleplay";
    public const string LearningPathGenerateKey = "learning_path_generate";
    public const string StudentMemoryUpdateKey = "student_memory_update";
    public const string LearningPathGenerateAdaptiveKey = "learning_path_generate_adaptive";
    public const string VocabularyExtractFromAttemptKey = "vocabulary_extract_from_attempt";
    public const string LessonBatchPlanKey = "lesson_batch_plan";
    public const string LessonGenerateFromResourcesKey = "lesson_generate_from_resources";
    public const string ExerciseGenerateFromResourcesKey = "exercise_generate_from_resources";
    public const string ModuleGenerateFromResourceKey = "module_generate_from_resource";
    // Phase K8 — one shared "fill this missing field" prompt reused by the admin Resource Bank/
    // Lesson/Exercise/Module "Fix with AI" repair action (see AdminRepairFieldGenerator).
    public const string AdminContentRepairFieldKey = "admin_content_repair_field";

    // ── Exercise Pattern Engine — pattern-specific generation prompt keys ─────
    public const string ActivityGeneratePhraseMatchKey        = "activity_generate_phrase_match";
    public const string ActivityGenerateGapFillKey            = "activity_generate_gap_fill_workplace_phrase";
    public const string ActivityGenerateListenAndAnswerKey    = "activity_generate_listen_and_answer";
    public const string ActivityGenerateListenAndGapFillKey   = "activity_generate_listen_and_gap_fill";
    public const string ActivityGenerateEmailReplyKey         = "activity_generate_email_reply";
    public const string ActivityGenerateTeamsChatKey          = "activity_generate_teams_chat_simulation";
    public const string ActivityGenerateSpokenResponseKey     = "activity_generate_spoken_response_from_prompt";
    public const string ActivityGenerateLessonReflectionKey   = "activity_generate_lesson_reflection";
    public const string ActivityGenerateOpenWritingTaskKey    = "activity_generate_open_writing_task";
    public const string ActivityGenerateSpeakingRoleplayTurnKey = "activity_generate_speaking_roleplay_turn";
    public const string ActivityGenerateReadingMultipleChoiceSingleKey = "activity_generate_reading_multiple_choice_single";
    public const string ActivityGenerateReadingMultipleChoiceMultiKey  = "activity_generate_reading_multiple_choice_multi";
    public const string ActivityGenerateReadingFillInBlanksKey         = "activity_generate_reading_fill_in_blanks";
    public const string ActivityGenerateReorderParagraphsKey                = "activity_generate_reorder_paragraphs";
    public const string ActivityGenerateReadingWritingFillInBlanksKey       = "activity_generate_reading_writing_fill_in_blanks";
    public const string ActivityGenerateSummarizeWrittenTextKey             = "activity_generate_summarize_written_text";
    public const string ActivityGenerateWriteEssayKey                       = "activity_generate_write_essay";
    public const string ActivityGenerateListeningMultipleChoiceSingleKey    = "activity_generate_listening_multiple_choice_single";
    public const string ActivityGenerateListeningMultipleChoiceMultiKey     = "activity_generate_listening_multiple_choice_multi";
    public const string ActivityGenerateListeningFillInBlanksKey            = "activity_generate_listening_fill_in_blanks";
    public const string ActivityGenerateSelectMissingWordKey                = "activity_generate_select_missing_word";
    public const string ActivityGenerateHighlightCorrectSummaryKey          = "activity_generate_highlight_correct_summary";
    public const string ActivityGenerateHighlightIncorrectWordsKey          = "activity_generate_highlight_incorrect_words";
    public const string ActivityGenerateWriteFromDictationKey               = "activity_generate_write_from_dictation";
    public const string ActivityGenerateSummarizeSpokenTextKey              = "activity_generate_summarize_spoken_text";
    public const string ActivityGenerateAnswerShortQuestionKey              = "activity_generate_answer_short_question";
    public const string ActivityEvaluateAnswerShortQuestionKey              = "activity_evaluate_answer_short_question";
    public const string ActivityGenerateReadAloudKey                        = "activity_generate_read_aloud";
    public const string ActivityEvaluateReadAloudKey                        = "activity_evaluate_read_aloud";
    public const string ActivityGenerateRepeatSentenceKey                   = "activity_generate_repeat_sentence";
    public const string ActivityEvaluateRepeatSentenceKey                   = "activity_evaluate_repeat_sentence";
    public const string ActivityGenerateRespondToSituationKey               = "activity_generate_respond_to_situation";
    public const string ActivityEvaluateRespondToSituationKey               = "activity_evaluate_respond_to_situation";
    public const string ActivityGenerateDescribeImageKey                     = "activity_generate_describe_image";
    public const string ActivityEvaluateDescribeImageKey                     = "activity_evaluate_describe_image";
    public const string ActivityGenerateRetellLectureKey                     = "activity_generate_retell_lecture";
    public const string ActivityEvaluateRetellLectureKey                     = "activity_evaluate_retell_lecture";
    public const string ActivityGenerateSummarizeGroupDiscussionKey          = "activity_generate_summarize_group_discussion";
    public const string ActivityEvaluateSummarizeGroupDiscussionKey          = "activity_evaluate_summarize_group_discussion";

    // ── Exercise Pattern Engine — pattern-specific evaluation prompt keys ─────
    public const string ActivityEvaluatePhraseMatchKey        = "activity_evaluate_phrase_match";
    public const string ActivityEvaluateGapFillKey            = "activity_evaluate_gap_fill_workplace_phrase";
    public const string ActivityEvaluateListenAndAnswerKey    = "activity_evaluate_listen_and_answer";
    public const string ActivityEvaluateListenAndGapFillKey   = "activity_evaluate_listen_and_gap_fill";
    public const string ActivityEvaluateEmailReplyKey         = "activity_evaluate_email_reply";
    public const string ActivityEvaluateTeamsChatKey          = "activity_evaluate_teams_chat_simulation";
    public const string ActivityEvaluateSpokenResponseKey     = "activity_evaluate_spoken_response_from_prompt";
    public const string ActivityEvaluateLessonReflectionKey   = "activity_evaluate_lesson_reflection";
    public const string ActivityEvaluateOpenWritingTaskKey    = "activity_evaluate_open_writing_task";
    public const string ActivityEvaluateSpeakingRoleplayTurnKey = "activity_evaluate_speaking_roleplay_turn";
    public const string ActivityEvaluateSummarizeWrittenTextKey = "activity_evaluate_summarize_written_text";
    public const string ActivityEvaluateWriteEssayKey = "activity_evaluate_write_essay";
    public const string ActivityEvaluateSummarizeSpokenTextKey = "activity_evaluate_summarize_spoken_text";

    private const string ActivityGenerateWritingContent = """
You are an expert English workplace writing coach creating a staged WritingScenario module for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Create THREE separate stages: Learn, Practice, and FeedbackPlan.
Learn teaches writing skills only. Practice contains the actual writing task.

Return ONLY valid JSON. No markdown. No text outside JSON.

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title>",
  "moduleGoal": "<what the student will be able to write after this module>",
  "primarySkill": "writing",
  "secondarySkills": ["grammar", "vocabulary"],
  "exerciseType": "writing_scenario",
  "estimatedDurationMinutes": <total module time in minutes, e.g. 5>,
  "estimatedLearnMinutes": <time to read and study the Learn stage, e.g. 1>,
  "estimatedPracticeMinutes": <time for the student to complete the writing task, e.g. 3>,
  "estimatedFeedbackMinutes": <time to review feedback, e.g. 1>,
  "learnContent": {
    "teachingTitle": "<short teaching heading>",
    "explanation": "<2-4 sentences teaching the writing concept, not the final task>",
    "keyPoints": ["<writing structure/tone point>", "<clarity point>", "<grammar/vocabulary point>"],
    "examples": [{"phrase": "<useful phrase or sentence starter>", "meaning": "<what it means / when to use it>", "note": "<tone or grammar note>"}],
    "strategy": "<how to plan the answer before writing>",
    "commonMistakes": ["<common writing mistake>", "<common tone/grammar mistake>"],
    "sourceLanguageSupport": "<optional short support in {{sourceLanguageName}}, or null>"
  },
  "practiceContent": {
    "instructions": "<clear instruction for the writing task>",
    "scenario": "<workplace situation>",
    "task": "<what the student must write>",
    "exerciseData": {
      "situation": "<workplace context>",
      "audience": "<recipient or reader>",
      "tone": "<tone requirement>",
      "expectedLength": "<word/sentence target>",
      "prompt": "<final writing prompt shown only in Practice>",
      "requiredPhrases": ["<optional phrase>"],
      "targetVocabulary": ["<optional vocabulary>"],
      "successChecklist": ["<what the response should include>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Clarity", "Tone", "Grammar accuracy", "Vocabulary use"],
    "rubric": [
      {"criterion": "Task completion", "description": "The response addresses the workplace situation and includes the required information.", "weight": 0.3},
      {"criterion": "Clarity and structure", "description": "The response is easy to follow and organised logically.", "weight": 0.25},
      {"criterion": "Tone", "description": "The response uses an appropriate professional tone.", "weight": 0.2},
      {"criterion": "Grammar and vocabulary", "description": "The response uses accurate grammar and suitable workplace vocabulary.", "weight": 0.25}
    ],
    "feedbackFocus": "Help the student improve clarity, tone, grammar, and task completion.",
    "successCriteria": ["The message is clear and complete.", "The tone is appropriate for the audience.", "Grammar and vocabulary support the message."]
  }
}

Critical Learn-stage rules:
- learnContent must not contain the final writing prompt.
- learnContent must not ask the student to complete the task.
- learnContent must not contain textarea, submitted answer, expected final answer, answer key, answer controls, submit labels, or check labels.
- learnContent may contain structure, tone guidance, phrases, short examples, common mistakes, planning strategy, and source-language support.
- practiceContent.exerciseData must include prompt, situation, audience, and tone.
- Keep all content appropriate for {{cefrLevel}} and {{careerContext}}.
- Do not include real company names, secrets, phone numbers, or sensitive content.

CEFR calibration for writing tasks:
| Level | Prompt complexity       | Expected output length | Grammar scope                    |
|-------|-------------------------|------------------------|----------------------------------|
| A1    | Single familiar topic   | 2-3 short sentences    | Simple present/past, basic nouns |
| A2    | Familiar workplace task | 4-6 sentences          | Simple past, common phrases      |
| B1    | Routine professional    | 80-120 words           | Present perfect, connectors      |
| B2    | Complex or nuanced      | 120-180 words          | Conditionals, passive, precision |
Use this table to set prompt complexity, expectedLength, and rubric weight emphasis.

Duration rules:
- estimatedDurationMinutes, estimatedLearnMinutes, estimatedPracticeMinutes, estimatedFeedbackMinutes must all be positive integers.
- estimatedLearnMinutes + estimatedPracticeMinutes + estimatedFeedbackMinutes must not exceed estimatedDurationMinutes.
- estimatedPracticeMinutes must reflect the actual time needed to complete the writing task. A single short writing task is typically 3-5 minutes. Do not claim 5+ minutes of practice for a trivial one-sentence prompt.
- Do not pad learnContent to fill time while keeping practiceContent tiny.
""";

    private const string ActivityEvaluateWritingContent = """
You are a warm, professional English writing coach evaluating a workplace message written by a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content:
{{activityContent}}

If activityContent uses schemaVersion module_stage_v1, evaluate the submission against practiceContent.exerciseData, especially prompt, situation, audience, tone, and expectedLength. Use feedbackPlan as the rubric. Use learnContent only as teaching context for coaching.

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
You are an expert English workplace communication coach creating a staged listening comprehension learning module for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to consider: {{recentMistakes}}

This module has THREE separate stages: Learn (teaching only), Practice (the actual exercise), and FeedbackPlan (how to evaluate the student's answers). There is no real audio yet, so the transcript must be hidden from the student until after submit.

Return ONLY valid JSON (no markdown) matching this exact structure:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short descriptive title, 5-10 words>",
  "moduleGoal": "<one sentence: what the student should be able to do after this module>",
  "skillFocus": "listening",
  "exerciseType": "listening_comprehension",
  "estimatedDurationMinutes": <total module time in minutes, e.g. 5>,
  "estimatedLearnMinutes": <time to read the Learn stage, e.g. 1>,
  "estimatedPracticeMinutes": <time to listen and answer, e.g. 3>,
  "estimatedFeedbackMinutes": <time to review feedback, e.g. 1>,
  "learnContent": {
    "teachingTitle": "<short teaching heading>",
    "explanation": "<2-3 sentences: a GENERAL workplace-listening strategy. Do NOT reference this specific message or its content>",
    "keyPoints": ["<2-4 general listening tips>"],
    "examples": [{"phrase": "<useful general phrase>", "meaning": "<meaning>", "note": "<when to use it>"}],
    "strategy": "<one sentence: what to listen for in general - action, deadline, reason>",
    "commonMistakes": ["<1-3 common listening mistakes>"],
    "sourceLanguageSupport": "<optional 1-sentence listening tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Read the situation first. Then listen and answer the questions. The transcript is hidden until after you submit.",
    "scenario": "<1-2 sentences describing who left the message and why>",
    "task": "<optional short reply task description, or null>",
    "exerciseData": {
      "speakerRole": "<workplace role speaking>",
      "listenerRole": "<student workplace role>",
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
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main idea understood", "Requested action identified", "Deadline/details identified"],
    "rubric": [
      {"criterion": "Main idea", "description": "<what counts as understanding the main idea>", "weight": 0.4},
      {"criterion": "Action and deadline", "description": "<what counts as identifying the action/deadline>", "weight": 0.35},
      {"criterion": "Response quality", "description": "<what counts as a good reply, if responseTask present>", "weight": 0.25}
    ],
    "feedbackFocus": "Main idea, requested action, and deadline; then reply quality if a response task is present",
    "successCriteria": ["<1-2 statements describing what success looks like>"]
  }
}

Critical rules:
- learnContent must NEVER include audioScript, questions, expectedAnswer, transcript, or any reference to this specific message's content. It teaches a general listening strategy only — content that would apply to any workplace listening task.
- practiceContent.exerciseData must include 2-4 comprehension questions, each answerable from audioScript.
- Use realistic workplace communication for {{careerContext}}.
- Keep vocabulary appropriate for {{cefrLevel}}.
- Do not use real company names, real person names, secrets, phone numbers, or sensitive content.
- expectedAnswer is for backend evaluation only.
- Do not include text outside the JSON object. No markdown fences.

CEFR calibration for listening tasks:
| Level | Audio script length | Vocabulary complexity | Question type                  |
|-------|---------------------|-----------------------|--------------------------------|
| A1    | 25-40 words         | Everyday, simple      | Single-fact retrieval          |
| A2    | 40-60 words         | Common workplace      | Fact + simple action           |
| B1    | 60-90 words         | Routine professional  | Detail + implied action        |
| B2    | 80-120 words        | Complex professional  | Detail, inference, implication |
Match audioScript length, vocabulary density, and question depth to {{cefrLevel}}.

Duration rules:
- estimatedDurationMinutes, estimatedLearnMinutes, estimatedPracticeMinutes, estimatedFeedbackMinutes must all be positive integers.
- estimatedLearnMinutes + estimatedPracticeMinutes + estimatedFeedbackMinutes must not exceed estimatedDurationMinutes.
- estimatedPracticeMinutes must reflect realistic time to listen and answer the questions. 2-4 questions from a short audio clip typically take 2-4 minutes. Do not claim 5+ practice minutes for a single 35-word audio clip with one question.
""";

    private const string ActivityGenerateSpeakingRolePlayContent = """
You are an expert English workplace communication coach creating a staged speaking role-play module for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to consider: {{recentMistakes}}

The module has three stages: Learn, Practice, and Feedback.

Learn teaches speaking strategy, useful phrases, pronunciation/fluency tips, and roleplay preparation.
Practice contains the actual speaking roleplay task with recording controls.
Feedback evaluates the spoken response.

STRICT RULE: learnContent must NOT contain any of these: recording controls, microphone instructions as an action, the final roleplay task/prompt as an action, submit button labels, answer keys, or anything that tells the student to perform the recording now.
learnContent MUST contain: speaking strategy, pronunciation tips, fluency tips, useful spoken phrases, short examples, common mistakes.

Return ONLY valid JSON (no markdown, no text outside the JSON object):

{
  "schemaVersion": "module_stage_v1",
  "title": "<short descriptive title, 5-10 words>",
  "moduleGoal": "<one sentence: what the student will be able to say after this module>",
  "primarySkill": "speaking",
  "secondarySkills": ["listening", "vocabulary"],
  "exerciseType": "speaking_roleplay",
  "estimatedDurationMinutes": <total module time in minutes, e.g. 5>,
  "estimatedLearnMinutes": <time to read the Learn stage, e.g. 1>,
  "estimatedPracticeMinutes": <time to prepare and record the spoken response, e.g. 3>,
  "estimatedFeedbackMinutes": <time to review feedback, e.g. 1>,
  "learnContent": {
    "teachingTitle": "<short teaching heading>",
    "explanation": "<2-4 sentences teaching the speaking strategy for this type of workplace situation>",
    "keyPoints": [
      "<fluency or pronunciation point>",
      "<conversation structure point>",
      "<useful language point>"
    ],
    "examples": [
      {
        "phrase": "<useful spoken phrase>",
        "meaning": "<when or why to use it>",
        "note": "<pronunciation, tone, or register note>"
      }
    ],
    "strategy": "<how to prepare and respond during this type of roleplay>",
    "commonMistakes": [
      "<common speaking mistake for this situation>",
      "<common fluency or tone mistake>"
    ],
    "sourceLanguageSupport": "<optional 1-sentence pronunciation or speaking tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "<clear roleplay instruction telling the student what to do in Practice>",
    "scenario": "<workplace or real-life speaking situation, 2-3 sentences>",
    "task": "<what the student must say or do>",
    "exerciseData": {
      "role": "<student role, e.g. 'Document Controller', 'Project Planner'>",
      "partnerRole": "<other speaker or persona, e.g. 'Manager', 'Client', 'Colleague'>",
      "situation": "<context for the speaking task, 1-2 sentences>",
      "prompt": "<1-2 sentences telling the student exactly what to say in their recording>",
      "expectedResponseLength": "30-60 seconds",
      "tone": "<tone requirement, e.g. 'professional and direct'>",
      "requiredPhrases": ["<optional useful phrase>"],
      "targetVocabulary": ["<optional vocabulary word>"],
      "successChecklist": [
        "<key point the response should include>",
        "<key point the response should include>",
        "<key point the response should include>"
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Task completion",
      "Fluency",
      "Pronunciation clarity",
      "Tone",
      "Grammar and vocabulary"
    ],
    "rubric": [
      {
        "criterion": "Task completion",
        "description": "The response addresses the roleplay situation and includes the required information.",
        "weight": 0.3
      },
      {
        "criterion": "Fluency",
        "description": "The response is spoken smoothly enough to be understood.",
        "weight": 0.25
      },
      {
        "criterion": "Pronunciation clarity",
        "description": "The words are clear enough for the listener to understand.",
        "weight": 0.2
      },
      {
        "criterion": "Grammar and vocabulary",
        "description": "The response uses suitable grammar and vocabulary for the situation.",
        "weight": 0.25
      }
    ],
    "feedbackFocus": "Help the student improve fluency, pronunciation clarity, tone, and task completion.",
    "successCriteria": [
      "The response is clear and relevant.",
      "The tone fits the situation.",
      "The speaker uses useful phrases naturally.",
      "The response can be understood by the listener."
    ]
  }
}

Rules:
- The scenario and situation must be specific and believable for {{careerContext}} professionals.
- Keep the speaking task short: 30-60 seconds.
- successChecklist items are what the evaluator checks.
- requiredPhrases and targetVocabulary are optional coaching aids — keep them short.
- Do not use real company names, real person names, phone numbers, or sensitive content.
- Do not include any text outside the JSON object.

CEFR calibration for speaking tasks:
| Level | Response complexity       | Response length | Language features expected                    |
|-------|---------------------------|-----------------|-----------------------------------------------|
| A1    | Single idea, direct       | 2-3 sentences   | Simple present, basic nouns, simple requests  |
| A2    | Two linked ideas          | 3-5 sentences   | Simple past/future, common workplace phrases  |
| B1    | Structured, purposeful    | 30-45 seconds   | Connectors, hedging, polite requests          |
| B2    | Nuanced or multi-part     | 45-60 seconds   | Conditionals, persuasion, precise vocabulary  |
Set prompt, expectedResponseLength, and successChecklist to match {{cefrLevel}}.

Duration rules:
- estimatedDurationMinutes, estimatedLearnMinutes, estimatedPracticeMinutes, estimatedFeedbackMinutes must all be positive integers.
- estimatedLearnMinutes + estimatedPracticeMinutes + estimatedFeedbackMinutes must not exceed estimatedDurationMinutes.
- estimatedPracticeMinutes must reflect realistic time for the student to prepare and record the spoken response. A single 30-60 second speaking task typically requires 2-3 minutes of practice time including preparation. Do not claim 5+ minutes of practice for a single short prompt.
""";

    private const string ActivityEvaluateSpeakingRolePlayContent = """
You are a warm, professional English speaking coach evaluating a workplace spoken response recorded by a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content:
{{activityContent}}

If activityContent uses schemaVersion module_stage_v1:
- Evaluate the submission against practiceContent.exerciseData: role, partnerRole, situation, prompt, tone, successChecklist, requiredPhrases.
- Use feedbackPlan.rubric as the scoring rubric (task completion, fluency, pronunciation clarity, grammar and vocabulary).
- Use feedbackPlan.feedbackFocus as the coaching focus.
- Use learnContent only as teaching context for coaching suggestions.
- Do NOT evaluate pronunciation accuracy or accent. Evaluate pronunciation clarity only (can the listener understand the words?).
- missingExpectedPoints should reference items from successChecklist that were not addressed.

If activityContent is legacy flat JSON (no schemaVersion), evaluate against scenario, speakingGoal, expectedPoints, and suggestedPhrases.

Student's transcript (from their recording):
---
{{transcript}}
---

Evaluate this transcript as a spoken workplace English response.
Focus on: task completion, fluency, clarity of message, professional tone, workplace appropriateness, structure, and vocabulary.
Check whether the student covered the expected points or successChecklist items.

Return ONLY valid JSON (no markdown, no text outside the JSON):

{
  "score": <number 0-100>,
  "transcript": "<copy the student transcript here, unchanged>",
  "coachSummary": "<1-2 warm sentences summarising the overall quality and the key thing to improve>",
  "strengths": [
    "<specific strength in their spoken response>"
  ],
  "improvements": [
    "<specific improvement for clarity, tone, structure, or vocabulary>"
  ],
  "missingExpectedPoints": [
    "<expected point that was not covered>"
  ],
  "suggestedImprovedResponse": "<a suggested improved version of what they could have said>",
  "miniLesson": "<1-2 sentences teaching the most important communication point from this attempt>",
  "nextImprovementStep": "<one actionable sentence telling the student what to focus on when they try again>"
}

Rules:
- Do not score pronunciation, accent, or speech speed.
- Be warm and specific — avoid generic feedback like "good job".
- missingExpectedPoints must only list points that were genuinely absent.
- suggestedImprovedResponse is a coaching reference — not "the correct answer".
- Do not include any text outside the JSON object.
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
- Each module must address a distinct real-life communication skill relevant to {{careerContext}} and {{skillFocus}}.
- Modules must progress from foundational to more advanced communication.
- Descriptions must be specific to {{careerContext}}, not generic. If the context is a workplace role, use workplace situations. If the context is daily life, travel, study, or settlement, use those situations.
- Return exactly {{moduleCount}} modules.
- Do not include any text outside the JSON object.
""";

    private const string StudentMemoryUpdateContent = """
You update a compact learning memory for SpeakPath, an English language coach.

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
- Focus on real communication coaching relevant to the student's context, not generic grammar.
- Main feedback is handled elsewhere; this is only compact memory.
""";

    private const string VocabularyExtractFromAttemptContent = """
You are a vocabulary coach for SpeakPath, an English learning platform.

Extract 0-5 useful vocabulary items from this writing attempt to help the student improve their English for real-life communication.

Context:
{{extractionContext}}

Return ONLY valid JSON (no markdown):

{
  "items": [
    {
      "term": "<the word or phrase to learn, lowercased>",
      "suggestedPhrase": "<a complete real-life sentence showing this phrase in use>",
      "meaningOrExplanation": "<1-2 sentences: what this means and why it matters in everyday English>",
      "exampleSentence": "<another example sentence in a different real-life context>",
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
You are designing the next 3-5 learning modules for SpeakPath.

Adaptive context:
{{adaptiveGenerationContext}}

Return ONLY valid JSON:

{
  "journeySummary": "<short explanation of why these modules are next>",
  "modules": [
    {
      "order": 1,
      "title": "<3-7 word module title>",
      "description": "<1-2 sentences describing what the student will practise>",
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
- Reuse weak skills through new real-life situations relevant to the student's context.
- Progress difficulty gradually.
- Keep modules relevant to the student's career context.
- Do not generate a generic full 10-module path.
- Do not include any text outside JSON.
""";

    // ── Pattern-specific generation prompts ───────────────────────────────────

    private const string ActivityGeneratePhraseMatchContent = """
You are an expert English language teacher creating a staged vocabulary module for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}

Create a three-stage module: Learn (teaching only), Practice (matching task), FeedbackPlan.
The Learn stage must NOT contain the matching pairs as a task to complete, answer keys, or any interactive controls.
The Practice stage contains the actual matching exercise with 6-8 workplace phrase pairs.

Return ONLY valid JSON (no markdown, no text outside the JSON):

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what phrase meanings the student will understand>",
  "primarySkill": "vocabulary",
  "secondarySkills": ["reading"],
  "exerciseType": "phrase_match",
  "learnContent": {
    "teachingTitle": "<short teaching heading>",
    "explanation": "<2-3 sentences teaching the vocabulary concept and workplace usage — do NOT list the pairs as a task>",
    "keyPoints": ["<usage tip>", "<meaning tip>", "<context tip>"],
    "examples": [
      { "phrase": "<one example phrase>", "meaning": "<plain meaning>", "note": "<when to use it at work>" }
    ],
    "strategy": "<one sentence: how to recognise phrase meaning from workplace context>",
    "commonMistakes": ["<one common vocabulary mistake>"],
    "sourceLanguageSupport": "<optional 1-sentence vocabulary tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Match each phrase to its correct meaning.",
    "scenario": "<optional: one sentence workplace context, or null>",
    "task": "Match each workplace phrase to its meaning.",
    "exerciseData": {
      "pairs": [
        { "phrase": "<workplace word or phrase>", "meaning": "<plain-English meaning>", "context": "<one realistic workplace sentence>" }
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Meaning accuracy", "Context recognition"],
    "rubric": [],
    "feedbackFocus": "Help the student understand phrase meaning and workplace usage.",
    "successCriteria": ["Correctly match all phrases to their meanings."]
  }
}

Critical Learn-stage rules:
- learnContent must not list the pairs as a matching task.
- learnContent must not contain answer keys, selected answers, gaps, or interactive controls.
- learnContent may contain meaning, usage notes, one teaching example, strategy, and common mistakes.
- practiceContent.exerciseData.pairs must have 6-8 items.
- Meanings must be clear and unambiguous.
- Items must be useful workplace English for {{careerContext}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateGapFillContent = """
You are an expert English language teacher creating a staged vocabulary/grammar module for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}

Create a three-stage module: Learn (teaching only), Practice (gap-fill task), FeedbackPlan.
The Learn stage must NOT contain any blanks, gaps, or the actual sentences the student must complete.
The Practice stage contains 5-6 workplace gap-fill sentences.

Return ONLY valid JSON (no markdown, no text outside the JSON):

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what workplace phrase or grammar pattern the student will practise>",
  "primarySkill": "vocabulary",
  "secondarySkills": ["reading"],
  "exerciseType": "gap_fill_workplace_phrase",
  "learnContent": {
    "teachingTitle": "<short teaching heading>",
    "explanation": "<2-3 sentences teaching the vocabulary or grammar concept — do NOT include the gap-fill sentences>",
    "keyPoints": ["<usage tip>", "<grammar or vocabulary point>", "<context tip>"],
    "examples": [
      { "phrase": "<a short example phrase or sentence>", "meaning": "<what it means>", "note": "<grammar or usage note>" }
    ],
    "strategy": "<one sentence: how to use context to choose the missing word>",
    "commonMistakes": ["<one common mistake for this language point>"],
    "sourceLanguageSupport": "<optional 1-sentence grammar or vocabulary tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Fill in each blank with the correct workplace word or phrase.",
    "scenario": "<optional: one sentence workplace context, or null>",
    "task": "Complete the missing words in each sentence.",
    "exerciseData": {
      "items": [
        {
          "sentence": "<workplace sentence with ___ for the blank>",
          "answer": "<correct answer>",
          "distractors": ["<plausible wrong option 1>", "<plausible wrong option 2>"],
          "hint": "<optional one-word hint or null>"
        }
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Correct word choice", "Grammar accuracy", "Context understanding"],
    "rubric": [],
    "feedbackFocus": "Help the student choose words based on grammar and workplace context.",
    "successCriteria": ["Fill all missing words correctly."]
  }
}

Critical Learn-stage rules:
- learnContent must not contain gaps, blanks, or the actual gap-fill sentences the student must complete.
- learnContent must not contain answer keys, submit labels, or interactive controls.
- learnContent may contain explanation, key points, one or two teaching examples, strategy, and common mistakes.
- practiceContent.exerciseData.items must have 5-6 items.
- Sentences must be realistic {{careerContext}} workplace contexts.
- Distractors must be plausible but clearly wrong.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateListenAndAnswerContent = """
You are an expert English language teacher creating a workplace listening comprehension activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes: {{recentMistakes}}

Create a realistic spoken workplace message (voicemail, meeting snippet, or team update) and comprehension questions. Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short descriptive title>",
  "learnContent": {
    "teachingTitle": "<title of the listening skill being practised>",
    "explanation": "<1-2 sentences: what this kind of workplace listening develops>",
    "keyPoints": [
      "<strategy point 1>",
      "<strategy point 2>",
      "<strategy point 3>"
    ],
    "strategy": "<one concrete tip for understanding this type of spoken message>",
    "commonMistakes": [
      "<common listening error 1>",
      "<common listening error 2>"
    ],
    "sourceLanguageSupport": "<optional 1-sentence listening tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Listen to the message and answer the questions.",
    "scenario": "<1-2 sentences describing the workplace context>",
    "task": "Answer each comprehension question based on what you heard.",
    "exerciseData": {
      "speakerRole": "<who is speaking, e.g. 'Project Manager'>",
      "listenerRole": "<who the listener is, e.g. 'Team Member'>",
      "audioScript": "<the spoken message text, 60-120 words, natural spoken English>",
      "transcriptAvailableAfterSubmit": true,
      "questions": [
        { "id": "q1", "question": "<comprehension question>", "expectedAnswer": "<concise expected answer>", "type": "short_answer" },
        { "id": "q2", "question": "<comprehension question>", "expectedAnswer": "<concise expected answer>", "type": "short_answer" },
        { "id": "q3", "question": "<comprehension question>", "expectedAnswer": "<concise expected answer>", "type": "short_answer" }
      ],
      "responseTask": {
        "prompt": "<short written follow-up task based on the message>",
        "expectedFocus": "<key language focus for the written response>"
      }
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Accuracy of answers to each question",
      "Evidence of understanding main point and key details",
      "Correct use of workplace vocabulary in answers"
    ],
    "rubric": [
      { "criterion": "Main idea", "weight": 0.4 },
      { "criterion": "Supporting detail", "weight": 0.4 },
      { "criterion": "Implication / inference", "weight": 0.2 }
    ],
    "feedbackFocus": "Comprehension accuracy and vocabulary",
    "successCriteria": [
      "Student correctly identifies the main point",
      "Student answers at least 2 of 3 questions correctly"
    ]
  }
}

Rules:
- learnContent must NEVER include audioScript, questions, expectedAnswer, transcript, or any reference to this specific message's content. It teaches a general listening strategy only.
- practiceContent.exerciseData must include audioScript and 2-3 comprehension questions.
- audioScript must sound natural and spoken, not formal written text.
- Questions should test different aspects: main point, detail, implication.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateListenAndGapFillContent = """
You are an expert English language teacher creating a listening gap-fill activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes: {{recentMistakes}}

Create a spoken workplace message with 4-5 key words/phrases for the student to fill in. Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title>",
  "learnContent": {
    "teachingTitle": "<title of the listening skill being practised>",
    "explanation": "<1-2 sentences: why listening for exact words matters in workplace communication>",
    "keyPoints": [
      "<strategy point 1>",
      "<strategy point 2>",
      "<strategy point 3>"
    ],
    "strategy": "<one concrete tip for catching key words while listening>",
    "commonMistakes": [
      "<common gap-fill listening error 1>",
      "<common gap-fill listening error 2>"
    ],
    "sourceLanguageSupport": "<optional 1-sentence listening tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Listen and fill in the missing words.",
    "scenario": "<1 sentence describing the workplace context>",
    "task": "Complete each gap with the exact word or phrase you hear.",
    "exerciseData": {
      "speakerRole": "<speaker role>",
      "audioScript": "<the full spoken text, 60-90 words>",
      "transcriptAvailableAfterSubmit": true,
      "gaps": [
        {
          "id": "g1",
          "sentenceWithBlank": "<verbatim sentence from audioScript with the answer replaced by ___>",
          "answer": "<exact word/phrase from the script>",
          "hint": "<optional category hint, e.g. 'verb'>"
        },
        {
          "id": "g2",
          "sentenceWithBlank": "<verbatim sentence from audioScript with the answer replaced by ___>",
          "answer": "<exact word/phrase from the script>",
          "hint": "<optional category hint>"
        }
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Exact match of each gap answer (case-insensitive)",
      "Key workplace vocabulary correctly identified",
      "Accurate sound discrimination for similar words"
    ],
    "rubric": [
      { "criterion": "Key vocabulary accuracy", "weight": 0.6 },
      { "criterion": "Functional word accuracy", "weight": 0.4 }
    ],
    "feedbackFocus": "Exact word recognition and workplace vocabulary",
    "successCriteria": [
      "Student correctly fills at least 3 of 5 gaps",
      "Student identifies the key workplace terms"
    ]
  }
}

Rules:
- learnContent must NEVER include audioScript, gaps, sentenceWithBlank, answer, or any reference to this specific message's content. It teaches a general listening strategy only.
- practiceContent.exerciseData must include audioScript and gaps (4-5 items).
- sentenceWithBlank must be a verbatim excerpt from audioScript with the answer replaced by ___.
- The gaps must be key workplace vocabulary, not filler words.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateEmailReplyContent = """
You are an expert English language teacher creating a workplace email reply activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Create a realistic workplace email the student must reply to. Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short descriptive title, 5-10 words>",
  "moduleGoal": "<one sentence: what email writing skill this practises>",
  "primarySkill": "writing",
  "secondarySkills": ["reading", "vocabulary"],
  "exerciseType": "email_reply",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Writing a polite request email'>",
    "explanation": "<2-3 sentences: what makes this type of professional email effective. Teach structure and tone — no reference to the specific email below>",
    "keyPoints": [
      "<email structure point, e.g. 'Open with a clear purpose statement'>",
      "<tone point, e.g. 'Use semi-formal language with your manager'>",
      "<closing point, e.g. 'End with a clear next step or call to action'>"
    ],
    "examples": [
      { "phrase": "<useful email opener phrase>", "meaning": "<when to use it>", "note": "<tone/register note>" },
      { "phrase": "<useful mid-email phrase>", "meaning": "<when to use it>", "note": "<tone/register note>" }
    ],
    "strategy": "<one sentence: how to plan and structure a good reply — general advice, not task-specific>",
    "commonMistakes": [
      "<common email writing mistake {{sourceLanguageName}} speakers make>",
      "<second common mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about this type of email, or null>"
  },
  "practiceContent": {
    "instructions": "Read the email below and write a professional reply.",
    "scenario": "<1-2 sentences describing the workplace context>",
    "task": "Write a professional email reply.",
    "exerciseData": {
      "incomingMessage": "<the full email the student received and must reply to — realistic workplace email, 60-120 words>",
      "recipient": "<who the student is writing to, e.g. 'your line manager'>",
      "relationship": "<e.g. 'manager' | 'colleague' | 'client'>",
      "tone": "<formal | semi-formal | polite>",
      "prompt": "Read the email above and write a professional reply.",
      "requiredInformation": ["<key point the reply must address>", "<second key point>"],
      "requiredPhrases": ["<phrase 1>", "<phrase 2>"],
      "targetVocabulary": ["<word 1>", "<word 2>"],
      "expectedLength": "<e.g. '3-5 sentences' or '1 short paragraph'>",
      "suggestedSubject": "<appropriate subject line for the reply, e.g. 'Re: Project deadline'>",
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Tone", "Clarity", "Email structure", "Grammar and vocabulary"],
    "rubric": [
      { "criterion": "Task completion", "weight": 0.35 },
      { "criterion": "Tone and register", "weight": 0.25 },
      { "criterion": "Clarity and structure", "weight": 0.25 },
      { "criterion": "Grammar and vocabulary", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student write a clear, professional email reply with appropriate tone and structure.",
    "successCriteria": [
      "The reply answers the email clearly and completely.",
      "The tone is appropriate for the relationship.",
      "The email has a clear opening, body, and close."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual incoming email, the specific writing task, expected answer, or any prompt asking the student to complete the task. It teaches general email writing strategy only.
- practiceContent.exerciseData.incomingMessage must be the full realistic workplace email the student reads and replies to.
- practiceContent.exerciseData.prompt must be a short instruction telling the student to write a reply.
- The incoming email must be specific and realistic for {{careerContext}} professionals.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateTeamsChatContent = """
You are an expert English language teacher creating a Teams/Slack chat simulation exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Create a realistic chat exchange where the student must write a professional reply. Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title>",
  "moduleGoal": "<one sentence: what workplace chat communication skill this practises>",
  "primarySkill": "writing",
  "secondarySkills": ["reading", "communication"],
  "exerciseType": "teams_chat_simulation",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Writing clear, concise workplace chat messages'>",
    "explanation": "<2-3 sentences: what makes workplace chat communication effective — general advice about clarity, brevity, and professional tone. No reference to the specific scenario below>",
    "keyPoints": [
      "<chat clarity point, e.g. 'Get to the point in the first sentence'>",
      "<tone point, e.g. 'Keep a friendly but professional tone'>",
      "<length point, e.g. 'Keep replies short — 1-3 sentences is enough'>"
    ],
    "examples": [
      { "phrase": "<useful chat opener phrase>", "meaning": "<when to use it>", "note": "<tone/register note>" },
      { "phrase": "<useful confirmation or response phrase>", "meaning": "<when to use it>", "note": "<tone/register note>" }
    ],
    "strategy": "<one sentence: how to craft a clear, relevant chat reply in a professional context — general advice>",
    "commonMistakes": [
      "<common chat writing mistake {{sourceLanguageName}} speakers make, e.g. writing too formally>",
      "<second common mistake, e.g. being too brief without enough information>"
    ],
    "sourceLanguageSupport": "<optional 1-sentence chat tone tip in {{sourceLanguageName}} if it helps, otherwise null>"
  },
  "practiceContent": {
    "instructions": "Read the chat thread and write the next message.",
    "scenario": "<1-2 sentences describing the workplace chat context>",
    "task": "Write your next message in the chat.",
    "exerciseData": {
      "chatHistory": [
        { "sender": "<colleague name>", "role": "<colleague role>", "message": "<their chat message 1>" },
        { "sender": "<colleague name>", "role": "<colleague role>", "message": "<their chat message 2 if needed, or omit>" }
      ],
      "speakerRole": "<student's role>",
      "recipientRole": "<colleague's role>",
      "tone": "<e.g. 'friendly but professional'>",
      "prompt": "Write your reply to the chat above.",
      "requiredInformation": ["<key point the reply must include>"],
      "requiredPhrases": ["<useful phrase 1>", "<useful phrase 2>"],
      "targetVocabulary": ["<word 1>", "<word 2>"],
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Tone", "Clarity", "Natural chat language", "Grammar and vocabulary"],
    "rubric": [
      { "criterion": "Task completion", "weight": 0.35 },
      { "criterion": "Tone and register", "weight": 0.25 },
      { "criterion": "Clarity and brevity", "weight": 0.25 },
      { "criterion": "Grammar and vocabulary", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student write concise, natural workplace chat replies with appropriate tone.",
    "successCriteria": [
      "The reply is clear and directly addresses the chat.",
      "The tone is friendly and professional.",
      "The message is appropriately brief."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual chat messages, the specific writing task, or any prompt asking the student to complete the task. It teaches general professional chat communication strategy only.
- practiceContent.exerciseData.chatHistory must be a realistic workplace chat thread (1-3 messages) that the student reads and responds to.
- practiceContent.exerciseData.prompt must be a short instruction telling the student to write their reply.
- Chat messages must be concise, as real workplace chat messages are (1-3 sentences each).
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateSpokenResponseContent = """
You are an expert English language teacher creating a spoken response exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what spoken response skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["listening"],
  "exerciseType": "spoken_response_from_prompt",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Giving a clear spoken update'>",
    "explanation": "<2-3 sentences: what makes this type of spoken workplace response effective — general advice about structure, tone, and clarity. No reference to the specific task below>",
    "keyPoints": [
      "<response structure point, e.g. 'Open with the main point, then give a brief reason'>",
      "<fluency point, e.g. 'Speak at a steady pace and use pauses deliberately'>"
    ],
    "examples": [
      { "phrase": "<useful spoken phrase>", "meaning": "<when to use it>", "note": "<tone/pronunciation note>" },
      { "phrase": "<second useful phrase>", "meaning": "<when to use it>", "note": "<tone/pronunciation note>" }
    ],
    "strategy": "<one sentence: how to prepare and deliver the response clearly — general advice, not task-specific>",
    "commonMistakes": [
      "<common spoken response mistake {{sourceLanguageName}} speakers make>",
      "<second common mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about this type of spoken response, or null>"
  },
  "practiceContent": {
    "instructions": "Read the situation and record your spoken response.",
    "scenario": "<2-3 sentences describing the realistic workplace situation>",
    "task": "Record a clear, professional spoken response for this workplace situation.",
    "exerciseData": {
      "prompt": "<the specific spoken task shown to the student in Practice, e.g. 'Record a 30-second update for your manager about the project delay'>",
      "expectedResponseLength": "<e.g. '30-60 seconds' or '3-5 sentences spoken aloud'>",
      "tone": "<e.g. 'professional and direct'>",
      "requiredInformation": ["<key point the response must include>", "<second key point>"],
      "requiredPhrases": ["<useful phrase 1>", "<useful phrase 2>"],
      "targetVocabulary": ["<word 1>", "<word 2>", "<word 3>"],
      "successChecklist": ["<criterion 1>", "<criterion 2>", "<criterion 3>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Fluency", "Clarity", "Tone", "Grammar and vocabulary"],
    "rubric": [
      { "criterion": "Task completion", "weight": 0.35 },
      { "criterion": "Fluency and clarity", "weight": 0.30 },
      { "criterion": "Grammar accuracy", "weight": 0.20 },
      { "criterion": "Vocabulary and tone", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student give clear, natural, professional spoken responses.",
    "successCriteria": [
      "The response addresses the situation clearly.",
      "The tone is appropriate for the workplace context.",
      "The student speaks fluently and is easy to understand."
    ]
  }
}

Rules:
- learnContent must NEVER contain the specific speaking prompt, expected answer, scenario details, recording controls, or any instruction asking the student to complete the speaking task. It teaches general spoken response strategy only.
- practiceContent.exerciseData.prompt must be the specific spoken task instruction shown to the student in Practice.
- The scenario and prompt must be specific and believable for {{careerContext}} professionals.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateLessonReflectionContent = """
You are an English language teacher creating a session-closing reflection activity for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Topic area: {{topicHint}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, e.g. 'Lesson reflection: workplace emails'>",
  "moduleGoal": "<one sentence: what reflection skill or self-awareness this practises>",
  "primarySkill": "reflection",
  "secondarySkills": ["writing"],
  "exerciseType": "lesson_reflection",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'How to reflect on your learning'>",
    "explanation": "<2-3 sentences: why reflection helps language learning and how to notice genuine progress — general advice. No reference to the specific reflection task below>",
    "keyPoints": [
      "<reflection strategy point, e.g. 'Be specific: name the exact phrase or skill you practised'>",
      "<self-correction point, e.g. 'Identify one thing you want to improve next time'>"
    ],
    "examples": [
      { "phrase": "<useful reflection phrase>", "meaning": "<when to use it>", "note": "<usage note>" },
      { "phrase": "<second useful reflection phrase>", "meaning": "<when to use it>", "note": "<usage note>" }
    ],
    "strategy": "<one sentence: how to write a useful reflection — general advice, not task-specific>",
    "commonMistakes": [
      "<common reflection mistake, e.g. 'Writing only vague comments like \"it was good\"'>",
      "<second common mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about reflecting on English learning, or null>"
  },
  "practiceContent": {
    "instructions": "Take a moment to reflect on what you practised today.",
    "scenario": "<1-2 sentences describing today's topic area, e.g. 'You have been practising workplace email writing.'>",
    "task": "Write a short reflection on today's practice.",
    "exerciseData": {
      "prompt": "<the specific reflection prompt shown to the student in Practice, e.g. 'What was the most useful phrase you used today? What would you do differently next time?'>",
      "reflectionFocus": "<what the student should focus on, e.g. 'email tone and workplace vocabulary'>",
      "expectedLength": "<e.g. '3-5 sentences'>",
      "successChecklist": [
        "The reflection identifies one specific strength from today.",
        "The reflection names one thing to improve.",
        "The student gives a concrete next step."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Self-awareness", "Specificity", "Improvement plan", "Clarity"],
    "rubric": [
      { "criterion": "Self-awareness", "weight": 0.35 },
      { "criterion": "Specificity", "weight": 0.30 },
      { "criterion": "Improvement plan", "weight": 0.20 },
      { "criterion": "Clarity", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student notice progress and choose a concrete next improvement step.",
    "successCriteria": [
      "The reflection identifies one strength, one challenge, and one next step.",
      "The student is specific about what they practised today."
    ]
  }
}

Rules:
- learnContent must NEVER contain the specific reflection prompt, expected answer, or any instruction asking the student to complete the reflection task. It teaches general reflection strategy only.
- practiceContent.exerciseData.prompt must be the specific reflection question shown to the student in Practice.
- Keep the tone warm and encouraging.
- The reflection topic must be specific to {{topicHint}} — not generic.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateOpenWritingTaskContent = """
You are an expert English language teacher creating an open-ended workplace writing task for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Create a realistic, free-form workplace writing task (e.g. a short report section, a
proposal paragraph, a status update, a reflective note) that the student writes from
scratch in an open text box — not a reply to an incoming message.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short descriptive title for this activity, 5-10 words>",
  "moduleGoal": "<one sentence: what open writing skill this practises>",
  "primarySkill": "writing",
  "secondarySkills": ["grammar", "vocabulary"],
  "exerciseType": "open_writing_task",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Writing a clear status update'>",
    "explanation": "<2-3 sentences: what makes this type of workplace writing effective — general advice about structure, purpose, and tone. No reference to the specific task below>",
    "keyPoints": [
      "<planning point, e.g. 'Identify your reader and purpose before writing'>",
      "<structure point, e.g. 'Open with the main point, then add details'>",
      "<clarity point, e.g. 'Keep sentences short and direct'>"
    ],
    "examples": [
      { "phrase": "<useful writing phrase>", "meaning": "<when to use it>", "note": "<usage note>" },
      { "phrase": "<second useful phrase>", "meaning": "<when to use it>", "note": "<usage note>" }
    ],
    "strategy": "<one sentence: how to plan and write the response well — general advice, not task-specific>",
    "commonMistakes": [
      "<common open writing mistake {{sourceLanguageName}} speakers make>",
      "<second common mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about this type of writing, or null>"
  },
  "practiceContent": {
    "instructions": "Read the situation and write your response.",
    "scenario": "<2-3 sentences describing the realistic workplace situation>",
    "task": "Write a clear, professional response for this workplace situation.",
    "exerciseData": {
      "prompt": "<the specific writing task, e.g. 'Write a short paragraph explaining the project delay to your manager'>",
      "tone": "<e.g. 'professional and direct' or 'formal'>",
      "expectedLength": "<e.g. '60-80 words' or '2-3 short paragraphs'>",
      "requiredInformation": ["<key point the response must include>", "<second key point>"],
      "requiredPhrases": ["<useful phrase 1>", "<useful phrase 2>"],
      "targetVocabulary": ["<word 1>", "<word 2>", "<word 3>"],
      "successChecklist": ["<criterion 1>", "<criterion 2>", "<criterion 3>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Clarity", "Structure", "Grammar", "Vocabulary"],
    "rubric": [
      { "criterion": "Task completion", "weight": 0.35 },
      { "criterion": "Clarity and structure", "weight": 0.30 },
      { "criterion": "Grammar accuracy", "weight": 0.20 },
      { "criterion": "Vocabulary use", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student improve clear, accurate, well-structured written workplace communication.",
    "successCriteria": [
      "The response is complete and addresses the situation.",
      "The writing is clear and easy to follow.",
      "The tone is appropriate for the workplace context."
    ]
  }
}

Rules:
- learnContent must NEVER contain the specific writing prompt, expected answer, target phrases as a task, or any instruction asking the student to complete the writing. It teaches general workplace writing strategy only.
- practiceContent.exerciseData.prompt must be the specific task instruction shown to the student in Practice.
- The situation and prompt must be specific and believable for {{careerContext}} professionals.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateSpeakingRoleplayTurnContent = """
You are an expert English language teacher creating a spoken workplace roleplay turn for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what roleplay speaking skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["listening"],
  "exerciseType": "speaking_roleplay_turn",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Responding naturally in a workplace conversation'>",
    "explanation": "<2-3 sentences: what makes a good spoken roleplay response — general advice about listening, responding clearly, and matching register. No reference to the specific roleplay task below>",
    "keyPoints": [
      "<roleplay response point, e.g. 'Acknowledge what the other person said before giving your response'>",
      "<tone/fluency point, e.g. 'Match your register to the situation — formal with a manager, warmer with a peer'>"
    ],
    "examples": [
      { "phrase": "<useful roleplay phrase>", "meaning": "<when to use it>", "note": "<tone/pronunciation note>" },
      { "phrase": "<second useful roleplay phrase>", "meaning": "<when to use it>", "note": "<tone/pronunciation note>" }
    ],
    "strategy": "<one sentence: how to listen carefully and respond naturally in a roleplay — general advice, not task-specific>",
    "commonMistakes": [
      "<common roleplay mistake {{sourceLanguageName}} speakers make>",
      "<second common mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about spoken workplace roleplay, or null>"
  },
  "practiceContent": {
    "instructions": "Read the roleplay situation and record your spoken response.",
    "scenario": "<2-3 sentences describing the realistic workplace roleplay situation>",
    "task": "Record your spoken response to your partner's turn.",
    "exerciseData": {
      "role": "<student's role in the roleplay, e.g. 'Project coordinator'>",
      "partnerRole": "<other speaker's role, e.g. 'Your manager'>",
      "partnerTurn": "<exactly what the partner says to start the roleplay turn>",
      "prompt": "<the specific spoken task shown to the student in Practice, e.g. 'Respond to your manager and explain the delay clearly'>",
      "expectedResponseLength": "<e.g. '30-60 seconds' or '3-5 sentences spoken aloud'>",
      "tone": "<e.g. 'professional and respectful'>",
      "requiredInformation": ["<key point the response must include>", "<second key point>"],
      "requiredPhrases": ["<useful phrase 1>", "<useful phrase 2>"],
      "targetVocabulary": ["<word 1>", "<word 2>", "<word 3>"],
      "successChecklist": ["<criterion 1>", "<criterion 2>", "<criterion 3>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Task completion", "Fluency", "Roleplay relevance", "Tone", "Grammar and vocabulary"],
    "rubric": [
      { "criterion": "Task completion", "weight": 0.35 },
      { "criterion": "Fluency and relevance", "weight": 0.30 },
      { "criterion": "Grammar accuracy", "weight": 0.20 },
      { "criterion": "Vocabulary and tone", "weight": 0.15 }
    ],
    "feedbackFocus": "Help the student respond naturally and clearly to a partner's spoken turn.",
    "successCriteria": [
      "The response fits the partner's turn and addresses the situation.",
      "The tone is appropriate for the workplace and role.",
      "The student speaks fluently and is easy to understand."
    ]
  }
}

Rules:
- learnContent must NEVER contain the partnerTurn, prompt, expected answer, recording controls, or any instruction asking the student to complete the roleplay task. It teaches general roleplay response strategy only.
- practiceContent.exerciseData.partnerTurn must be the exact words the partner says.
- practiceContent.exerciseData.prompt must be the specific spoken task instruction shown to the student in Practice.
- The scenario must be realistic for {{careerContext}} professionals.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateReadingMultipleChoiceSingleContent = """
You are an expert English language teacher creating a reading comprehension exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what reading skill this practises>",
  "primarySkill": "reading",
  "secondarySkills": [],
  "exerciseType": "reading_multiple_choice_single",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Finding the main idea in a workplace text'>",
    "explanation": "<2-3 sentences: a general reading strategy for this kind of question — no reference to the specific passage below>",
    "keyPoints": [
      "<reading strategy point 1, e.g. 'Skim for the main idea before reading in detail'>",
      "<reading strategy point 2, e.g. 'Watch for signal words that suggest contrast or cause and effect'>",
      "<reading strategy point 3, e.g. 'Eliminate options that are only partly true'>"
    ],
    "examples": [
      { "phrase": "<example signal word or phrase>", "meaning": "<what it signals in a text>", "note": "<how to use it when reading>" }
    ],
    "strategy": "<one sentence: how to approach a single-answer reading question>",
    "commonMistakes": [
      "<common mistake, e.g. 'Choosing an option that is true but does not answer the question'>",
      "<second common mistake, e.g. 'Picking the first plausible-looking option without checking the others'>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about reading strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the passage, then choose the one best answer to the question.",
    "scenario": "<1 sentence describing the workplace context of the passage>",
    "task": "Read the passage and choose the option that best answers the question.",
    "exerciseData": {
      "passage": "<a realistic workplace reading passage, 80-160 words>",
      "question": "<a single-answer comprehension question about the passage>",
      "options": [
        { "id": "A", "text": "<option A text>" },
        { "id": "B", "text": "<option B text>" },
        { "id": "C", "text": "<option C text>" },
        { "id": "D", "text": "<option D text>" }
      ],
      "correctOptionId": "<id of the correct option, e.g. 'A'>",
      "explanation": "<1-2 sentences explaining why the correct option is right, referring to the passage>",
      "distractorExplanations": {
        "<id of an incorrect option>": "<why this option is wrong>",
        "<id of another incorrect option>": "<why this option is wrong>",
        "<id of the remaining incorrect option>": "<why this option is wrong>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Inference", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student read carefully and choose the best supported answer.",
    "successCriteria": [
      "The selected option is supported by the passage.",
      "The student can explain why the distractors are weaker."
    ]
  }
}

Rules:
- learnContent must NEVER contain the passage, question, options, correctOptionId, explanation, distractorExplanations, or any reference to this specific exercise's content. It teaches general reading strategy only.
- practiceContent.exerciseData.passage must be realistic for {{careerContext}} professionals and relevant to topic area {{topicHint}}.
- practiceContent.exerciseData.options must contain exactly 4 options with ids "A", "B", "C", "D", with exactly one correct option.
- distractorExplanations must contain an entry for each of the 3 incorrect option ids.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateListeningMultipleChoiceSingleContent = """
You are an expert English language teacher creating a listening comprehension exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": [],
  "exerciseType": "listening_multiple_choice_single",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Listening for the main idea'>",
    "explanation": "<2-3 sentences: a general listening strategy for this kind of question — no reference to the specific audio below>",
    "keyPoints": [
      "<how to listen for the main idea>",
      "<how to identify key details>",
      "<how to avoid distractors>"
    ],
    "examples": [
      { "phrase": "<short listening phrase or signal expression>", "meaning": "<what it usually signals>", "note": "<listening strategy note>" }
    ],
    "strategy": "<one sentence: how to listen and choose the best supported answer>",
    "commonMistakes": [
      "<choosing based on one familiar word>",
      "<missing contrast words or negative forms>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about listening strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then choose the one best answer to the question.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen and choose the option that best answers the question.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 30-70 words, realistic for {{careerContext}} professionals>",
      "audioUrl": null,
      "question": "<a single-answer comprehension question about the audio>",
      "options": [
        { "id": "A", "text": "<option A text>" },
        { "id": "B", "text": "<option B text>" },
        { "id": "C", "text": "<option C text>" },
        { "id": "D", "text": "<option D text>" }
      ],
      "correctOptionId": "<id of the correct option, e.g. 'A'>",
      "explanation": "<1-2 sentences explaining why the correct option is right, referring to the audio>",
      "distractorExplanations": {
        "<id of an incorrect option>": "<why this option is wrong>",
        "<id of another incorrect option>": "<why this option is wrong>",
        "<id of the remaining incorrect option>": "<why this option is wrong>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Listening for contrast", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student listen for meaning and choose the best supported answer.",
    "successCriteria": [
      "The selected option is supported by the audio.",
      "The student avoids distractors based on isolated words."
    ]
  }
}

Rules:
- learnContent must NEVER contain the audioScript, transcript, question, options, correctOptionId, explanation, distractorExplanations, or any reference to this specific exercise's content. It teaches general listening strategy only.
- practiceContent.exerciseData.audioScript must be short (30-70 words), natural spoken English, and realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- practiceContent.exerciseData.options must contain exactly 4 options with ids "A", "B", "C", "D", with exactly one correct option.
- distractorExplanations must contain an entry for each of the 3 incorrect option ids.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateSelectMissingWordContent = """
You are an expert English language teacher creating a listening prediction exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening/prediction skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": [],
  "exerciseType": "select_missing_word",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Predicting the missing word'>",
    "explanation": "<2-4 sentences teaching how to predict a missing word from listening context — no reference to the specific audio below>",
    "keyPoints": [
      "<how to listen for context before the missing word>",
      "<how to predict grammar and meaning>",
      "<how to avoid distractors that sound plausible but do not fit>"
    ],
    "examples": [
      { "phrase": "<short example or signal phrase>", "meaning": "<what it helps the student predict>", "note": "<listening/context strategy note>" }
    ],
    "strategy": "<one sentence: how to listen, predict, and select the best missing word>",
    "commonMistakes": [
      "<choosing based on familiar sound only>",
      "<ignoring grammar after the blank>",
      "<missing contrast or cause/effect cues>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about listening/prediction strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then choose the word or phrase that correctly completes it.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen and choose the missing word or phrase.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 30-70 words, realistic for {{careerContext}} professionals, including the correct missing word/phrase naturally>",
      "audioUrl": null,
      "incompleteText": "<the same script with the missing word/phrase replaced by {{missing}}>",
      "question": "Choose the missing word or phrase.",
      "options": [
        { "id": "A", "text": "<option A text>" },
        { "id": "B", "text": "<option B text>" },
        { "id": "C", "text": "<option C text>" },
        { "id": "D", "text": "<option D text>" }
      ],
      "correctOptionId": "<id of the correct option, e.g. 'A'>",
      "explanation": "<1-2 sentences explaining why the correct missing word/phrase fits, referring to the audio>",
      "distractorExplanations": {
        "<id of an incorrect option>": "<why this option is wrong>",
        "<id of another incorrect option>": "<why this option is wrong>",
        "<id of the remaining incorrect option>": "<why this option is wrong>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Listening context understanding", "Prediction from meaning", "Grammar fit", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student use listening context and grammar clues to choose the best missing word.",
    "successCriteria": [
      "The selected word or phrase fits the audio meaning.",
      "The selected option fits the grammar and context.",
      "The student avoids distractors based on sound or isolated words."
    ]
  }
}

Rules:
- learnContent must NEVER contain the audioScript, transcript, incompleteText, question, options, correctOptionId, explanation, distractorExplanations, or any reference to this specific exercise's content. It teaches general listening prediction strategy only.
- practiceContent.exerciseData.audioScript must be short (30-70 words), natural spoken English, realistic for {{careerContext}} professionals and topic area {{topicHint}}, and must include the correct missing word/phrase naturally.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- practiceContent.exerciseData.incompleteText must be the audioScript text with the missing word/phrase replaced by the literal token {{missing}}.
- practiceContent.exerciseData.options must contain exactly 4 options with ids "A", "B", "C", "D", with exactly one correct option matching the missing word/phrase.
- distractorExplanations must contain an entry for each of the 3 incorrect option ids.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateHighlightCorrectSummaryContent = """
You are an expert English language teacher creating a listening summary-selection exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening summary skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["reading"],
  "exerciseType": "highlight_correct_summary",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Choosing the best summary'>",
    "explanation": "<2-4 sentences teaching how to choose the summary that best matches a spoken passage — no reference to the specific audio below>",
    "keyPoints": [
      "<how to listen for the main idea, not just single words>",
      "<how to compare each summary against the whole passage>",
      "<how to reject summaries that add, distort, or omit key facts>"
    ],
    "examples": [
      { "phrase": "<short signal phrase>", "meaning": "<what it tells you about the main idea>", "note": "<summary/listening strategy note>" }
    ],
    "strategy": "<one sentence: how to listen for the gist and select the most accurate summary>",
    "commonMistakes": [
      "<choosing a summary that matches one detail but misses the main point>",
      "<choosing a summary that adds information not in the audio>",
      "<choosing a summary that contradicts a key fact>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about summary-selection strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then choose the summary that best matches what you heard.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen and choose the best summary.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 40-80 words, realistic for {{careerContext}} professionals>",
      "audioUrl": null,
      "question": "Which summary best matches the audio?",
      "options": [
        { "id": "A", "text": "<a one-sentence summary>" },
        { "id": "B", "text": "<a one-sentence summary>" },
        { "id": "C", "text": "<a one-sentence summary>" },
        { "id": "D", "text": "<a one-sentence summary>" }
      ],
      "correctOptionId": "<id of the summary that best matches, e.g. 'B'>",
      "explanation": "<1-2 sentences explaining why the correct summary best matches, referring to the audio>",
      "distractorExplanations": {
        "<id of an incorrect summary>": "<why this summary is wrong: adds, distorts, or omits a key fact>",
        "<id of another incorrect summary>": "<why this summary is wrong>",
        "<id of the remaining incorrect summary>": "<why this summary is wrong>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main-idea comprehension", "Summary accuracy", "Detail verification", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student listen for the overall meaning and choose the most accurate summary.",
    "successCriteria": [
      "The selected summary matches the main idea of the audio.",
      "The selected summary does not add or distort facts.",
      "The student avoids summaries that match only one detail."
    ]
  }
}

Rules:
- learnContent must NEVER contain the audioScript, transcript, question, options, correctOptionId, explanation, distractorExplanations, summaryOptions, or any reference to this specific exercise's content. It teaches general summary-selection strategy only.
- practiceContent.exerciseData.audioScript must be short (40-80 words), natural spoken English, realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- practiceContent.exerciseData.options must contain exactly 4 one-sentence summaries with ids "A", "B", "C", "D", with exactly one summary that best matches the audio.
- distractorExplanations must contain an entry for each of the 3 incorrect option ids.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateHighlightIncorrectWordsContent = """
You are an expert English language teacher creating a listening comprehension exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what careful-listening skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["reading"],
  "exerciseType": "highlight_incorrect_words",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Spotting words that differ'>",
    "explanation": "<2-4 sentences teaching how to listen closely and notice when a written transcript differs from spoken audio — no reference to the specific audio below>",
    "keyPoints": [
      "<how to read along while listening for mismatches>",
      "<how small changes alter meaning>",
      "<how to focus on content words, not just sounds>"
    ],
    "examples": [
      { "phrase": "<short signal phrase>", "meaning": "<what to listen for>", "note": "<listening strategy note>" }
    ],
    "strategy": "<one sentence: how to compare what you hear to what you read>",
    "commonMistakes": [
      "<selecting words that actually match the audio>",
      "<missing a changed word that sounds similar>",
      "<reading the transcript without listening carefully>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about listening-for-differences strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then click the words in the transcript that are different from what you hear.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen and select every word that differs from the audio.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 30-60 words, realistic for {{careerContext}} professionals — this is the CORRECT spoken version>",
      "audioUrl": null,
      "displayTranscript": "<the same passage but with 2-4 words changed to different words; this is the text the student reads>",
      "tokens": [
        { "id": "t0", "text": "<first word of displayTranscript>", "position": 0 },
        { "id": "t1", "text": "<second word>", "position": 1 }
      ],
      "incorrectTokenIds": ["<id of each token whose text differs from the audio, 2-4 ids>"],
      "corrections": {
        "<incorrect token id>": "<the word actually spoken in the audio for that position>"
      },
      "tokenExplanations": {
        "<incorrect token id>": "<1 short sentence: how this word differs and why it matters>"
      },
      "question": "Which words are different from the audio?",
      "explanation": "<1-2 sentences summarising the changed words, referring to the audio>"
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Careful listening", "Word-level accuracy", "Difference detection"],
    "rubric": [],
    "feedbackFocus": "Help the student listen closely and detect every word that differs from the audio.",
    "successCriteria": [
      "The student selects all changed words.",
      "The student does not select words that match the audio.",
      "The student understands how each change alters meaning."
    ]
  }
}

Rules:
- learnContent must NEVER contain audioScript, transcript, displayTranscript, tokens, incorrectTokenIds, corrections, answerKey, or any reference to this specific exercise's content. It teaches general listening-for-differences strategy only.
- practiceContent.exerciseData.audioScript is the CORRECT spoken version, short (30-60 words), natural spoken English, realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- displayTranscript must be the same passage as audioScript but with exactly 2-4 single words changed to different (real, plausible) words.
- tokens must list EVERY word of displayTranscript in order, each with a unique id (t0, t1, ...) and a zero-based position. Split on whitespace; keep punctuation attached to its word.
- incorrectTokenIds must list the token ids whose text differs from the audio — exactly the 2-4 changed words, nothing else.
- corrections and tokenExplanations must contain an entry for each incorrect token id.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateWriteFromDictationContent = """
You are an expert English language teacher creating a write-from-dictation listening exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Generate exactly {{defaultItemsPerPractice}} dictation items in the practice stage.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what dictation/listening-and-writing skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["writing"],
  "exerciseType": "write_from_dictation",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Writing exactly what you hear'>",
    "explanation": "<2-4 sentences teaching how to listen for every word and write it accurately — no reference to the specific sentences below>",
    "keyPoints": [
      "<how to hold a short sentence in memory>",
      "<how to listen for word endings and small words>",
      "<how to check spelling and punctuation>"
    ],
    "examples": [
      { "phrase": "<short signal phrase>", "meaning": "<what to listen for>", "note": "<dictation strategy note>" }
    ],
    "strategy": "<one sentence: how to listen, hold, and write a short sentence accurately>",
    "commonMistakes": [
      "<dropping small words like articles or prepositions>",
      "<misspelling a heard word>",
      "<missing word endings such as plural or past tense>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about dictation strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to each clip, then type exactly what you hear.",
    "scenario": "<1 sentence describing the workplace context of these clips>",
    "task": "Listen to each clip and write the sentence exactly.",
    "exerciseData": {
      "items": [
        {
          "id": "item1",
          "audioScript": "<a short, natural spoken-English sentence, 6-12 words, realistic for {{careerContext}} professionals>",
          "audioUrl": null,
          "answer": "<the exact sentence text the student should type, matching audioScript>",
          "acceptedAnswers": ["<the same sentence>", "<an equally-correct minor variant, optional>"],
          "explanation": "<1 short sentence noting the tricky word or punctuation in this clip>"
        }
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Listening accuracy", "Spelling", "Completeness"],
    "rubric": [],
    "feedbackFocus": "Help the student write each spoken sentence accurately, word for word.",
    "successCriteria": [
      "The student writes every word that was spoken.",
      "Spelling and word endings are correct.",
      "No words are added or dropped."
    ]
  }
}

Rules:
- learnContent must NEVER contain audioScript, transcript, items, answer, acceptedAnswers, answerKey, or any sentence the student must transcribe. It teaches general dictation strategy only.
- practiceContent.exerciseData.items must contain exactly {{defaultItemsPerPractice}} items.
- Each item id must be unique (item1, item2, ...).
- Each audioScript must be a short natural spoken-English sentence (6-12 words) realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- Each item's answer must be the exact text of its audioScript.
- audioUrl must be null — audio is not pre-generated for this format.
- acceptedAnswers must include the exact answer; add at most one plausible minor variant.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateListeningMultipleChoiceMultiContent = """
You are an expert English language teacher creating a listening comprehension exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": [],
  "exerciseType": "listening_multiple_choice_multi",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Listening for multiple details'>",
    "explanation": "<2-4 sentences: a general listening strategy for multiple-answer questions — no reference to the specific audio below>",
    "keyPoints": [
      "<how to listen for multiple key details>",
      "<how to avoid distractors>",
      "<how to track who/what/when/why details>"
    ],
    "examples": [
      { "phrase": "<short listening phrase or signal expression>", "meaning": "<what it usually signals>", "note": "<listening strategy note>" }
    ],
    "strategy": "<one sentence: how to listen and choose all supported answers>",
    "commonMistakes": [
      "<choosing based on isolated familiar words>",
      "<missing one correct detail>",
      "<selecting an unsupported distractor>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about multiple-answer listening strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then choose ALL correct answers to the question.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen and select every option that is supported by the audio.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 30-80 words, realistic for {{careerContext}} professionals>",
      "audioUrl": null,
      "question": "<a multiple-answer comprehension question about the audio>",
      "options": [
        { "id": "A", "text": "<option A text>" },
        { "id": "B", "text": "<option B text>" },
        { "id": "C", "text": "<option C text>" },
        { "id": "D", "text": "<option D text>" }
      ],
      "correctOptionIds": ["<id of first correct option>", "<id of second correct option>"],
      "explanation": "<1-2 sentences explaining why the correct options are right, referring to the audio>",
      "optionExplanations": {
        "A": "<why this option is correct or incorrect, with reference to the audio>",
        "B": "<why this option is correct or incorrect, with reference to the audio>",
        "C": "<why this option is correct or incorrect, with reference to the audio>",
        "D": "<why this option is correct or incorrect, with reference to the audio>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Complete answer selection", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student listen for all supported details and avoid unsupported distractors.",
    "successCriteria": [
      "All selected options are supported by the audio.",
      "No correct options are missed.",
      "Unsupported distractors are avoided."
    ]
  }
}

Rules:
- learnContent must NEVER contain the audioScript, transcript, question, options, correctOptionIds, optionExplanations, or any reference to this specific exercise's content. It teaches general listening strategy only.
- practiceContent.exerciseData.audioScript must be short (30-80 words), natural spoken English, and realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- practiceContent.exerciseData.options must contain exactly 4 options with ids "A", "B", "C", "D".
- correctOptionIds must contain AT LEAST TWO correct option ids (this is a multiple-answer exercise, not a single-answer exercise).
- optionExplanations must contain an entry for every option id ("A", "B", "C", "D").
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateListeningFillInBlanksContent = """
You are an expert English language teacher creating a listening fill-in-the-blanks exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening/word-recognition skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["writing"],
  "exerciseType": "listening_fill_in_blanks",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Listening for missing words'>",
    "explanation": "<2-4 sentences: a general listening strategy for word recognition and spelling — no reference to the specific audio below>",
    "keyPoints": [
      "<how to listen for grammar and context clues>",
      "<how to predict word form before listening>",
      "<how to check spelling and agreement>"
    ],
    "examples": [
      { "phrase": "<short signal phrase or example>", "meaning": "<what it helps the student notice>", "note": "<listening/writing strategy note>" }
    ],
    "strategy": "<one sentence: how to listen, predict, and complete missing words>",
    "commonMistakes": [
      "<missing unstressed words>",
      "<writing the wrong word form>",
      "<spelling errors>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about listening/word-recognition strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio and complete the missing words in the transcript.",
    "scenario": "<1 sentence describing the workplace context of the audio>",
    "task": "Listen to the audio and choose the correct word for each blank.",
    "exerciseData": {
      "audioScript": "<a short, natural spoken-English script, 40-90 words, realistic for {{careerContext}} professionals>",
      "audioUrl": null,
      "passageWithBlanks": "<the same script with {{gap1}}, {{gap2}}, etc. replacing the missing words>",
      "gaps": [
        {
          "id": "gap1",
          "answer": "<the correct word>",
          "acceptedAnswers": ["<optional accepted alternative spelling or form>"],
          "options": ["<correct word>", "<distractor 1>", "<distractor 2>", "<distractor 3>"],
          "explanation": "<why this word fits, with reference to grammar or context>"
        },
        {
          "id": "gap2",
          "answer": "<the correct word>",
          "acceptedAnswers": ["<optional accepted alternative>"],
          "options": ["<correct word>", "<distractor 1>", "<distractor 2>", "<distractor 3>"],
          "explanation": "<why this word fits>"
        },
        {
          "id": "gap3",
          "answer": "<the correct word>",
          "acceptedAnswers": ["<optional accepted alternative>"],
          "options": ["<correct word>", "<distractor 1>", "<distractor 2>", "<distractor 3>"],
          "explanation": "<why this word fits>"
        },
        {
          "id": "gap4",
          "answer": "<the correct word>",
          "acceptedAnswers": ["<optional accepted alternative>"],
          "options": ["<correct word>", "<distractor 1>", "<distractor 2>", "<distractor 3>"],
          "explanation": "<why this word fits>"
        }
      ],
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Listening accuracy", "Context understanding", "Correct word choice", "Word form", "Spelling accuracy"],
    "rubric": [],
    "feedbackFocus": "Help the student listen for missing words using context, grammar, and sound clues.",
    "successCriteria": [
      "Each answer matches the audio.",
      "Each answer fits the grammar and context.",
      "Spelling is accurate."
    ]
  }
}

Rules:
- learnContent must NEVER contain the audioScript, passageWithBlanks, gaps, gap ids, options, answers, acceptedAnswers, or any reference to this specific exercise's content. It teaches general listening/word-recognition strategy only.
- practiceContent.exerciseData.audioScript must be short (40-90 words), natural spoken English, and realistic for {{careerContext}} professionals and topic area {{topicHint}}.
- practiceContent.exerciseData.audioUrl must be null — audio is not pre-generated for this format.
- practiceContent.exerciseData.passageWithBlanks must be a verbatim copy of audioScript with each missing word replaced by {{gap1}}, {{gap2}}, etc. matching the gap ids.
- Include exactly 4 gaps; select key workplace vocabulary, not filler words.
- Each gap's options must contain exactly 4 choices including the correct answer, in random order.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateReadingMultipleChoiceMultiContent = """
You are an expert English language teacher creating a reading comprehension exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what reading skill this practises>",
  "primarySkill": "reading",
  "secondarySkills": [],
  "exerciseType": "reading_multiple_choice_multi",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Selecting all supported answers in a workplace text'>",
    "explanation": "<2-3 sentences: a general reading strategy for multiple-answer questions — no reference to the specific passage below>",
    "keyPoints": [
      "<reading strategy point 1, e.g. 'Read the passage fully before selecting any answers'>",
      "<reading strategy point 2, e.g. 'Each correct option must be directly supported by the passage'>",
      "<reading strategy point 3, e.g. 'Eliminate options that are partially true or not mentioned'>"
    ],
    "examples": [
      { "phrase": "<example signal word or phrase>", "meaning": "<what it signals in a text>", "note": "<how to use it when reading for multiple answers>" }
    ],
    "strategy": "<one sentence: how to approach a multiple-answer reading question>",
    "commonMistakes": [
      "<common mistake, e.g. 'Stopping after finding one correct option without checking the rest'>",
      "<second common mistake, e.g. 'Choosing options that sound reasonable but are not in the passage'>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about multiple-answer reading strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the passage, then choose ALL correct answers to the question.",
    "scenario": "<1 sentence describing the workplace context of the passage>",
    "task": "Read the passage and select every option that is supported by the text.",
    "exerciseData": {
      "passage": "<a realistic workplace reading passage, 80-160 words>",
      "question": "<a multiple-answer comprehension question about the passage>",
      "options": [
        { "id": "A", "text": "<option A text>" },
        { "id": "B", "text": "<option B text>" },
        { "id": "C", "text": "<option C text>" },
        { "id": "D", "text": "<option D text>" }
      ],
      "correctOptionIds": ["<id of first correct option>", "<id of second correct option>"],
      "explanation": "<1-2 sentences explaining why the correct options are right, referring to the passage>",
      "optionExplanations": {
        "A": "<why this option is correct or incorrect, with reference to the passage>",
        "B": "<why this option is correct or incorrect, with reference to the passage>",
        "C": "<why this option is correct or incorrect, with reference to the passage>",
        "D": "<why this option is correct or incorrect, with reference to the passage>"
      },
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Main idea understanding", "Detail recognition", "Inference", "Complete answer selection", "Distractor elimination"],
    "rubric": [],
    "feedbackFocus": "Help the student select all answers supported by the passage and avoid unsupported distractors.",
    "successCriteria": [
      "All selected options are supported by the passage.",
      "No correct options are missed.",
      "Unsupported distractors are avoided."
    ]
  }
}

Rules:
- learnContent must NEVER contain the passage, question, options, correctOptionIds, optionExplanations, or any reference to this specific exercise's content. It teaches general reading strategy only.
- practiceContent.exerciseData.passage must be realistic for {{careerContext}} professionals and relevant to topic area {{topicHint}}.
- practiceContent.exerciseData.options must contain exactly 4 options with ids "A", "B", "C", "D".
- correctOptionIds must contain AT LEAST TWO correct option ids (this is a multiple-answer exercise, not a single-answer exercise).
- optionExplanations must contain an entry for every option id ("A", "B", "C", "D").
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateReadingFillInBlanksContent = """
You are an expert English language teacher creating a reading fill-in-the-blanks exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what reading/context skill this practises>",
  "primarySkill": "reading",
  "secondarySkills": [],
  "exerciseType": "reading_fill_in_blanks",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Using context clues to choose missing words'>",
    "explanation": "<2-3 sentences: a general reading strategy for fill-in-the-blanks questions — no reference to the specific passage below>",
    "keyPoints": [
      "<reading strategy point 1, e.g. 'Read the whole sentence before choosing a word'>",
      "<reading strategy point 2, e.g. 'Use grammar clues — noun, verb, adjective — to narrow your choice'>",
      "<reading strategy point 3, e.g. 'Check that the word fits both the meaning and the grammar'>"
    ],
    "examples": [
      { "phrase": "<example sentence fragment with a blank>", "meaning": "<what the context shows>", "note": "<reading or grammar strategy note>" }
    ],
    "strategy": "<one sentence: how to read each sentence and choose the best missing word>",
    "commonMistakes": [
      "<common mistake, e.g. 'Choosing a word that fits the meaning but not the grammar'>",
      "<second common mistake, e.g. 'Not reading the full sentence before selecting'>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about context-clue strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the passage and choose the correct word for each blank.",
    "scenario": "<1 sentence describing the workplace context of the passage>",
    "task": "Read the passage and select the word that best fits each blank.",
    "exerciseData": {
      "passageWithBlanks": "<a realistic workplace reading passage, 80-150 words, with blanks marked as {{gap1}}, {{gap2}}, {{gap3}} etc.>",
      "gaps": [
        {
          "id": "gap1",
          "answer": "<the correct word for this blank>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<1 sentence: why this word fits the context and grammar of the sentence>"
        },
        {
          "id": "gap2",
          "answer": "<the correct word for this blank>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<1 sentence: why this word fits>"
        },
        {
          "id": "gap3",
          "answer": "<the correct word for this blank>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<1 sentence: why this word fits>"
        }
      ],
      "successChecklist": ["<criterion 1>", "<criterion 2>"]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": ["Context understanding", "Correct word choice", "Grammar fit", "Word form"],
    "rubric": [],
    "feedbackFocus": "Help the student use sentence context and grammar clues to choose missing words.",
    "successCriteria": [
      "Each selected word fits the context of the passage.",
      "Each selected word fits the grammar of the surrounding sentence."
    ]
  }
}

Rules:
- learnContent must NEVER contain the passageWithBlanks, gap ids, gap options, correct answers, or any reference to this specific exercise's content. It teaches general reading/context-clue strategy only.
- practiceContent.exerciseData.passageWithBlanks must use placeholder tokens {{gap1}}, {{gap2}}, {{gap3}} (etc.) exactly where blanks appear — one token per gap.
- Each gap must have exactly 4 options, one of which is the correct answer. Shuffle the options so the correct answer is not always first.
- Gaps must be numbered gap1, gap2, gap3 in order of appearance.
- Include between 2 and 4 gaps total.
- The passage must be realistic for {{careerContext}} professionals and relevant to topic area {{topicHint}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateReorderParagraphsContent = """
You are an expert English language teacher creating a reorder-paragraphs reading exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what sequencing/coherence reading skill this practises>",
  "primarySkill": "reading",
  "secondarySkills": [],
  "exerciseType": "reorder_paragraphs",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Putting paragraphs in a logical order'>",
    "explanation": "<2-4 sentences: general strategy for recognising paragraph order — look for topic sentences, pronoun references, time/sequence words, and logical flow. No reference to the specific paragraphs below.>",
    "keyPoints": [
      "<e.g. 'The opening sentence usually introduces the topic without referring back to earlier text'>",
      "<e.g. 'Pronouns like 'this', 'it', or 'they' refer back to something already mentioned'>",
      "<e.g. 'Sequence words like 'first', 'then', 'finally' signal order'>"
    ],
    "examples": [
      { "phrase": "<sequence signal word or short example>", "meaning": "<what it signals>", "note": "<ordering strategy note>" }
    ],
    "strategy": "<one sentence: how to identify the correct paragraph order>",
    "commonMistakes": [
      "<common ordering mistake>",
      "<common pronoun/reference mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about text cohesion strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the paragraph blocks and put them in the correct logical order.",
    "scenario": "<1 sentence describing the workplace context of the text>",
    "task": "Put the paragraphs in the correct order to form a coherent text.",
    "exerciseData": {
      "items": [
        { "id": "p1", "text": "<paragraph or sentence block, 20-50 words>" },
        { "id": "p2", "text": "<paragraph or sentence block, 20-50 words>" },
        { "id": "p3", "text": "<paragraph or sentence block, 20-50 words>" },
        { "id": "p4", "text": "<paragraph or sentence block, 20-50 words>" }
      ],
      "correctOrder": ["p1", "p2", "p3", "p4"],
      "explanation": "<1-2 sentences: why this order is the most logical>",
      "itemExplanations": {
        "p1": "<why this paragraph comes first>",
        "p2": "<why this paragraph comes second>",
        "p3": "<why this paragraph comes third>",
        "p4": "<why this paragraph comes last>"
      },
      "successChecklist": [
        "The text reads as a coherent whole.",
        "Pronouns and references connect logically."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Opening sentence recognition",
      "Logical sequence",
      "Reference tracking",
      "Cohesion and coherence"
    ],
    "rubric": [],
    "feedbackFocus": "Help the student recognise logical flow and paragraph cohesion.",
    "successCriteria": [
      "The order creates a coherent text.",
      "Pronouns, references, and sequence words connect logically."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual paragraph items, the correct order, answer keys, or any reference to this specific exercise's content. It teaches general sequencing/coherence strategy only.
- practiceContent.exerciseData.items must contain exactly 4 paragraph blocks with ids p1, p2, p3, p4.
- correctOrder must list exactly those 4 ids in the logically correct sequence.
- The items presented to the student will be shuffled — the id order in the items array must NOT match the correctOrder. Place them in a different order so the student must reorder them.
- itemExplanations keys must match the ids in correctOrder exactly.
- Each paragraph block must be realistic for {{careerContext}} professionals and relevant to {{topicHint}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateReadingWritingFillInBlanksContent = """
You are an expert English language teacher creating a reading-and-writing fill-in-the-blanks exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

This exercise combines reading comprehension with vocabulary and word-form knowledge. The student reads a passage and selects the correct word for each blank from dropdown options.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what reading/word-form/vocabulary skill this practises>",
  "primarySkill": "reading",
  "secondarySkills": ["writing"],
  "exerciseType": "reading_writing_fill_in_blanks",
  "learnContent": {
    "teachingTitle": "<short teaching heading, e.g. 'Choosing the right word form'>",
    "explanation": "<2-4 sentences: general strategy for reading context clues and recognising the correct word form (noun, verb, adjective, adverb) or meaning. No reference to the specific passage below.>",
    "keyPoints": [
      "<e.g. 'Read the full sentence before choosing — the surrounding words indicate the word class needed'>",
      "<e.g. 'Collocations matter: certain nouns pair with certain verbs or adjectives'>",
      "<e.g. 'Consider whether a noun, verb, adjective, or adverb fits the grammatical slot'>"
    ],
    "examples": [
      { "phrase": "<target vocabulary or collocation>", "meaning": "<what it means>", "note": "<word-form or usage note>" }
    ],
    "strategy": "<one sentence: how to identify the correct word from context>",
    "commonMistakes": [
      "<common word-form mistake, e.g. using noun where adjective needed>",
      "<common collocation mistake>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about choosing word forms, or null>"
  },
  "practiceContent": {
    "instructions": "Read the passage and choose the correct word for each blank.",
    "scenario": "<1 sentence describing the workplace context of the passage>",
    "task": "Select the best word for each numbered gap in the passage.",
    "exerciseData": {
      "passageWithBlanks": "<A workplace passage of 80-120 words with 4-5 blanks marked as {{gap1}}, {{gap2}}, {{gap3}}, {{gap4}} (and optionally {{gap5}}). Each blank must require the student to use reading context AND word-form or vocabulary knowledge to choose correctly.>",
      "gaps": [
        {
          "id": "gap1",
          "answer": "<correct word>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<why this word is correct, mentioning word form or collocation>"
        },
        {
          "id": "gap2",
          "answer": "<correct word>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<why this word is correct>"
        },
        {
          "id": "gap3",
          "answer": "<correct word>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<why this word is correct>"
        },
        {
          "id": "gap4",
          "answer": "<correct word>",
          "options": ["<correct word>", "<plausible distractor>", "<plausible distractor>"],
          "explanation": "<why this word is correct>"
        }
      ],
      "successChecklist": [
        "Each blank has the grammatically and contextually correct word.",
        "All distractors are plausible but clearly wrong in context."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Word-form accuracy",
      "Vocabulary range",
      "Reading context use",
      "Collocation knowledge"
    ],
    "rubric": [],
    "feedbackFocus": "Help the student recognise word forms and context clues.",
    "successCriteria": [
      "The correct word fits grammatically and contextually in every gap.",
      "All distractors tested a meaningful contrast."
    ]
  }
}

Rules:
- learnContent must NEVER contain the passage, gap answers, options, or any reference to the specific exercise content. It teaches the general reading/word-form strategy only.
- Use exactly the token format {{gap1}}, {{gap2}}, etc. in passageWithBlanks — no spaces inside the braces.
- Each gap must have exactly 3 options. The correct answer must appear in the options array. Shuffle option order so the correct answer is not always first.
- Distractors must be plausible words (e.g. wrong word form, near-synonym, or common collocation error) — not obviously wrong.
- The passage must be realistic for {{careerContext}} professionals and relevant to {{topicHint}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateSummarizeWrittenTextContent = """
You are an expert English language teacher creating a summarize-written-text exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read a workplace passage and write a concise summary in their own words. This exercises reading comprehension AND writing concision/paraphrasing.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what reading-and-summarising skill this practises>",
  "primarySkill": "writing",
  "secondarySkills": ["reading"],
  "exerciseType": "summarize_written_text",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to write a concise summary'>",
    "explanation": "<2-4 sentences: general strategy for identifying the main idea, selecting key details, avoiding minor points, and paraphrasing. No reference to the specific passage below.>",
    "keyPoints": [
      "<e.g. 'Read the whole text first before writing'>",
      "<e.g. 'Identify the topic sentence and 2-3 supporting points'>",
      "<e.g. 'Use your own words — avoid copying phrases from the text'>",
      "<e.g. 'Keep it concise: aim for 30-50 words unless told otherwise'>"
    ],
    "examples": [
      { "phrase": "<useful summary phrase>", "meaning": "<when/how to use it>", "note": "<paraphrasing or concision strategy note>" }
    ],
    "strategy": "<one sentence: how to read, identify key points, and write a concise summary>",
    "commonMistakes": [
      "<copying full sentences from the source>",
      "<including too many minor details>",
      "<writing too much or too little>",
      "<missing the main idea>"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about summarising strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the passage below and write a concise summary in your own words.",
    "scenario": "<1 sentence describing the workplace/professional context>",
    "task": "Write a concise summary of the passage. Use your own words and include the main idea and key points.",
    "exerciseData": {
      "sourceText": "<A workplace passage of 100-150 words, clearly written, with a main idea and 2-3 supporting points. Relevant to {{careerContext}} and {{topicHint}}.>",
      "prompt": "Write a summary of approximately 30-50 words. Include the main idea and key supporting points. Use your own words.",
      "summaryRequirements": {
        "targetWordCount": "30-50 words",
        "maxSentences": 3,
        "mustInclude": ["<key idea from the passage>", "<key idea from the passage>"],
        "avoid": ["<minor detail>", "copying exact phrases from the source"]
      },
      "keyPoints": [
        "<expected key point 1>",
        "<expected key point 2>",
        "<expected key point 3>"
      ],
      "successChecklist": [
        "The summary captures the main idea.",
        "Key supporting points are included.",
        "The summary is concise (30-50 words).",
        "The student has paraphrased rather than copied."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Main idea coverage",
      "Key detail selection",
      "Concision",
      "Paraphrasing",
      "Grammar and vocabulary",
      "Coherence"
    ],
    "rubric": [
      { "criterion": "Content", "description": "Covers the main idea and key supporting points accurately." },
      { "criterion": "Concision", "description": "Keeps the summary focused and appropriately brief." },
      { "criterion": "Language", "description": "Uses clear grammar, vocabulary, and sentence structure." },
      { "criterion": "Paraphrasing", "description": "Avoids copying and expresses ideas in own words." }
    ],
    "feedbackFocus": "Help the student summarise the main idea clearly, concisely, and in their own words.",
    "successCriteria": [
      "The summary captures the main idea.",
      "The summary includes key points and avoids minor details.",
      "The summary is concise and written in the student's own words."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual sourceText, the actual prompt, expected summary, keyPoints from the exercise, or any reference to the specific passage content. It teaches general summarising strategy only.
- practiceContent.exerciseData.sourceText must be a realistic, clearly structured workplace text of 100-150 words.
- keyPoints must reflect what a good summary of THIS specific text should include.
- summaryRequirements.mustInclude items must be derivable from the sourceText — not generic.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateSummarizeWrittenTextContent = """
You are an expert English language teacher evaluating a student's written summary.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (source text and requirements):
{{activityContent}}

Student summary:
{{studentSubmission}}

Evaluate the summary against the rubric criteria and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentence warm but honest feedback: did the student capture the main idea? Is it concise? Any key gaps?>",
  "focusFirst": false,
  "changes": [
    {
      "type": "grammar|vocabulary|content|concision|paraphrasing",
      "original": "<phrase or issue from student's summary>",
      "suggested": "<improved version>",
      "reason": "<brief explanation>",
      "category": "<grammar|vocabulary|content|concision|paraphrasing>",
      "severity": "low|medium|high"
    }
  ],
  "whatYouDidWell": ["<specific positive: main idea captured>", "<specific positive: concise>"],
  "mainMistakes": ["<key content gap or language issue if any>"],
  "grammarIssues": ["<grammar issue if any>"],
  "vocabularyIssues": ["<vocabulary issue if any>"],
  "toneIssues": [],
  "miniLesson": "<one sentence teaching moment about summarising or language use>",
  "improvedVersion": "<a model summary of 30-50 words that demonstrates what a good answer looks like>",
  "nextImprovementStep": "<one specific action for the student to practise next>",
  "feedbackInSourceLanguage": "<1-2 sentences of encouragement in {{sourceLanguageName}}>"
}

Scoring guide:
- 90-100: Main idea + all key points + concise + own words + clean language
- 75-89: Main idea + most key points + mostly own words + minor language issues
- 60-74: Main idea present but key points missing or copied wording
- 40-59: Main idea unclear or significantly incomplete
- 0-39: Missing main idea, off-topic, or just copied the source

If the student's summary is empty or too short to evaluate, set overallScore to 0 and explain in coachSummary.
Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateSummarizeSpokenTextContent = """
You are an expert English language teacher creating a summarize-spoken-text exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will listen to a short spoken text of 60-90 seconds, then write a concise summary in their own words. This exercises listening comprehension AND writing concision/paraphrasing.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening-and-summarising skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["writing"],
  "exerciseType": "summarize_spoken_text",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to summarise what you hear'>",
    "explanation": "<2-4 sentences: general strategy for listening for the main idea, noting key supporting points, and paraphrasing concisely. No reference to the specific audio below.>",
    "keyPoints": [
      "<e.g. 'Listen for the overall topic before details'>",
      "<e.g. 'Note 2-3 key supporting points'>",
      "<e.g. 'Use your own words — do not try to transcribe'>",
      "<e.g. 'Keep it concise: aim for the requested length'>"
    ],
    "examples": [
      { "phrase": "<useful summary phrase>", "meaning": "<when/how to use it>", "note": "<listening or concision strategy note>" }
    ],
    "strategy": "<one sentence: how to listen, identify key points, and write a concise summary>",
    "commonMistakes": [
      "trying to write down every word",
      "including too many minor details",
      "missing the main idea",
      "writing too much or too little"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about summarising spoken text, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to the audio, then write a concise summary in your own words.",
    "scenario": "<1 sentence describing the workplace/professional context>",
    "task": "Write a concise summary of the spoken text. Use your own words and include the main idea and key points.",
    "exerciseData": {
      "audioScript": "<A spoken-style workplace text of 130-200 words (about 60-90 seconds when read aloud), clearly structured, with a main idea and 2-3 supporting points. Relevant to {{careerContext}} and {{topicHint}}. Written as natural speech.>",
      "audioUrl": null,
      "prompt": "Listen to the audio and write a summary of 50-70 words. Include the main idea and key supporting points in your own words.",
      "summaryRequirements": [
        "Cover the main idea",
        "Include key supporting points",
        "Use your own words"
      ],
      "keyPoints": [
        "<expected key point 1 from the audio>",
        "<expected key point 2 from the audio>",
        "<expected key point 3 from the audio>"
      ],
      "successChecklist": [
        "The summary covers the main idea.",
        "Key supporting points are included.",
        "No unsupported details are added.",
        "The student has paraphrased rather than transcribed."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Main idea coverage",
      "Key point selection",
      "Concision",
      "Grammar and vocabulary",
      "Coherence",
      "Unsupported details"
    ],
    "rubric": [
      { "criterion": "Content", "description": "Covers the main idea and key supporting points accurately." },
      { "criterion": "Concision", "description": "Keeps the summary focused and appropriately brief." },
      { "criterion": "Language", "description": "Uses clear grammar, vocabulary, and sentence structure." },
      { "criterion": "Fidelity", "description": "Adds no details that were not in the audio." }
    ],
    "feedbackFocus": "Help the student summarise the spoken text clearly, concisely, and in their own words.",
    "successCriteria": [
      "The summary captures the main idea.",
      "The summary includes key points and avoids unsupported details.",
      "The summary is concise and written in the student's own words."
    ]
  }
}

Rules:
- learnContent must NEVER contain audioScript, transcript, the actual prompt, expected summary, keyPoints, or any reference to the specific audio content. It teaches general summarising strategy only.
- practiceContent.exerciseData.audioScript must be a realistic, clearly structured spoken workplace text of 130-200 words.
- keyPoints must reflect what a good summary of THIS specific audio should include.
- audioUrl must be null. The audio is generated separately from audioScript.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateAnswerShortQuestionContent = """
You are an expert English language teacher creating an answer-short-question speaking exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will listen to short spoken questions and answer each one briefly and clearly. This exercises speaking fluency, listening comprehension, and concise response formation.

Generate exactly 5 short questions (DefaultItemsPerPractice=5). Each question should have a clear, short correct answer (typically 2-8 words).

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what speaking skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["listening"],
  "exerciseType": "answer_short_question",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to answer short questions clearly'>",
    "explanation": "<2-4 sentences: general strategy for listening carefully and responding briefly and clearly. No reference to the actual questions below.>",
    "keyPoints": [
      "<e.g. 'Listen for the key word in the question'>",
      "<e.g. 'Answer directly — avoid long explanations'>",
      "<e.g. 'Use a complete short phrase, not just one word'>",
      "<e.g. 'Speak clearly and at a natural pace'>"
    ],
    "examples": [
      { "phrase": "<example question type>", "meaning": "<what kind of answer it expects>", "note": "<strategy tip>" }
    ],
    "strategy": "<one sentence: how to listen, process the question, and answer concisely>",
    "commonMistakes": [
      "giving too long an answer",
      "repeating the question back",
      "pausing too long before answering",
      "answering a different question than was asked"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about answering short questions, or null>"
  },
  "practiceContent": {
    "instructions": "Listen to each question and type your answer. Keep your answers short and clear.",
    "scenario": "<1 sentence describing the workplace/professional context>",
    "task": "Answer each short question clearly and concisely.",
    "exerciseData": {
      "items": [
        {
          "id": "q1",
          "question": "<short workplace question, 5-12 words>",
          "audioScript": "<same as question text>",
          "audioUrl": null,
          "expectedAnswer": "<short correct answer, 2-8 words>",
          "acceptedAnswers": ["<exact expected answer>", "<common acceptable variation>"],
          "explanation": "<why this is the correct answer>"
        },
        {
          "id": "q2",
          "question": "<short workplace question>",
          "audioScript": "<same as question text>",
          "audioUrl": null,
          "expectedAnswer": "<short correct answer>",
          "acceptedAnswers": ["<exact expected answer>", "<common acceptable variation>"],
          "explanation": "<why this is the correct answer>"
        },
        {
          "id": "q3",
          "question": "<short workplace question>",
          "audioScript": "<same as question text>",
          "audioUrl": null,
          "expectedAnswer": "<short correct answer>",
          "acceptedAnswers": ["<exact expected answer>", "<common acceptable variation>"],
          "explanation": "<why this is the correct answer>"
        },
        {
          "id": "q4",
          "question": "<short workplace question>",
          "audioScript": "<same as question text>",
          "audioUrl": null,
          "expectedAnswer": "<short correct answer>",
          "acceptedAnswers": ["<exact expected answer>", "<common acceptable variation>"],
          "explanation": "<why this is the correct answer>"
        },
        {
          "id": "q5",
          "question": "<short workplace question>",
          "audioScript": "<same as question text>",
          "audioUrl": null,
          "expectedAnswer": "<short correct answer>",
          "acceptedAnswers": ["<exact expected answer>", "<common acceptable variation>"],
          "explanation": "<why this is the correct answer>"
        }
      ],
      "successChecklist": [
        "Each answer is short and direct.",
        "The student responds to the actual question asked.",
        "Answers use natural spoken English."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Answer accuracy",
      "Response concision",
      "Language naturalness",
      "Question comprehension"
    ],
    "rubric": [
      { "criterion": "Accuracy", "description": "The answer matches the expected correct response." },
      { "criterion": "Concision", "description": "The answer is brief and does not include unnecessary content." },
      { "criterion": "Language", "description": "The answer uses natural English phrasing." }
    ],
    "feedbackFocus": "Help the student answer short questions clearly, accurately, and concisely.",
    "successCriteria": [
      "All or most answers are correct.",
      "Answers are brief and direct.",
      "The student demonstrates understanding of each question."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual questions, expected answers, acceptedAnswers, item ids, or any answer key. It teaches general short-answer strategy only.
- Each item must have id, question, audioScript (same as question), audioUrl (null), expectedAnswer, acceptedAnswers (array), and explanation.
- Questions must be short, clear, workplace-relevant, and appropriate for {{cefrLevel}}.
- Expected answers must be short (2-8 words). Include common acceptable variations in acceptedAnswers.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateAnswerShortQuestionContent = """
You are a warm, professional English speaking coach evaluating a student's answers to short spoken questions.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (questions and expected answers):
{{activityContent}}

Student's submitted answers:
{{submittedAnswer}}

Evaluate each answer. For each item, check if the submitted answer matches the expected answer or any accepted answer (case-insensitive, trimmed). Provide brief, encouraging per-item feedback.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0.0-1.0>,
  "overallFeedback": "<2-3 sentences of overall feedback>",
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true/false>,
      "submittedAnswer": "<what the student wrote>",
      "expectedAnswer": "<the correct answer>",
      "feedback": "<1 sentence of item-level feedback>"
    }
  ],
  "coachingTip": "<one actionable tip for improvement>",
  "encouragement": "<one sentence of encouragement>"
}
""";

    private const string ActivityGenerateReadAloudContent = """
You are an expert English language teacher creating a read-aloud speaking exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read short workplace texts aloud and type what they read. This exercises reading fluency, pronunciation awareness, and natural pacing.

Generate exactly 2 texts (DefaultItemsPerPractice=2). Each text should be 20-40 words — short enough to read in one breath, realistic for a workplace context.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what speaking/reading skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["pronunciation", "reading"],
  "exerciseType": "read_aloud",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to read workplace texts aloud clearly'>",
    "explanation": "<2-4 sentences: general strategy for reading aloud with clarity and natural pacing. No reference to the actual texts below.>",
    "keyPoints": [
      "<e.g. 'Read at a natural pace — not too fast, not too slow'>",
      "<e.g. 'Pause at punctuation to help your listener follow'>",
      "<e.g. 'Stress key words to convey the right meaning'>",
      "<e.g. 'Preview the text before reading to avoid surprises'>"
    ],
    "examples": [
      { "phrase": "<example of clear spoken phrasing>", "meaning": "<what makes it clear>", "note": "<strategy tip>" }
    ],
    "strategy": "<one sentence: how to approach reading workplace texts aloud for maximum clarity>",
    "commonMistakes": [
      "reading too fast without pausing",
      "dropping word endings under pressure",
      "losing eye contact with the text mid-sentence",
      "monotone delivery without natural stress"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about reading aloud clearly, or null>"
  },
  "practiceContent": {
    "instructions": "Read each text aloud, then type exactly what you read.",
    "scenario": "<1 sentence describing the workplace context for these texts>",
    "task": "Read each text aloud clearly and naturally, then type what you said.",
    "exerciseData": {
      "items": [
        {
          "id": "t1",
          "text": "<20-40 word workplace text — a notice, announcement, instruction, or short message>",
          "displayTitle": "<short label, e.g. 'Meeting Notice'>",
          "difficulty": "<easy|medium|hard>",
          "expectedText": "<exact same text as above — used for word-overlap scoring>",
          "focusAreas": ["<e.g. 'sentence rhythm'>", "<e.g. 'word stress'>"],
          "explanation": "<one sentence tip for reading this text well>"
        },
        {
          "id": "t2",
          "text": "<20-40 word workplace text>",
          "displayTitle": "<short label>",
          "difficulty": "<easy|medium|hard>",
          "expectedText": "<exact same text as above>",
          "focusAreas": ["<focus area>"],
          "explanation": "<one sentence tip>"
        }
      ],
      "successChecklist": [
        "Each text is read clearly and at a natural pace.",
        "Key words are stressed appropriately.",
        "Punctuation is reflected in natural pauses."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Text accuracy",
      "Word coverage",
      "Natural pacing",
      "Clarity"
    ],
    "rubric": [
      { "criterion": "Accuracy", "description": "The typed transcript closely matches the original text." },
      { "criterion": "Coverage", "description": "Most words from the text are present in the transcript." },
      { "criterion": "Clarity", "description": "The reading sounds natural and well-paced based on the typed output." }
    ],
    "feedbackFocus": "Help the student read workplace texts aloud clearly, accurately, and with natural pacing.",
    "successCriteria": [
      "Word overlap with the original text is high.",
      "The student captures the key content words.",
      "The transcript reflects natural sentence structure."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual texts, expectedText values, item ids, or scoring details. It teaches general read-aloud strategy only.
- Each item must have id, text, displayTitle, difficulty, expectedText (same as text), focusAreas (array), and explanation.
- Texts must be 20-40 words, realistic workplace content (notices, instructions, announcements, short messages), appropriate for {{cefrLevel}}.
- Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateRepeatSentenceContent = """
You are an expert English language teacher creating a repeat-sentence speaking and listening exercise for a {{sourceLanguageName}}-speaking learner of {{targetLanguageName}}.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read or hear a short sentence, then repeat it as accurately as possible and type what they said. This practises listening accuracy, speaking fluency, and sentence rhythm. Content should suit the student's learning goals — which may include day-to-day English, travel, social conversation, academic English, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it.

Generate exactly 5 sentences (DefaultItemsPerPractice=5). Each sentence should be 8-20 words — short enough to hold in working memory and repeat in one breath.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening and speaking skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["listening", "pronunciation"],
  "exerciseType": "repeat_sentence",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to listen and repeat accurately'>",
    "explanation": "<2-4 sentences: general strategy for listening carefully and repeating sentences accurately. No reference to the actual sentences below.>",
    "keyPoints": [
      "<e.g. 'Listen for the key content words first'>",
      "<e.g. 'Keep the sentence rhythm and stress pattern when you repeat'>",
      "<e.g. 'If you miss a word, try to reconstruct from context'>",
      "<e.g. 'Short sentences are easier to hold in memory — practise chunking longer ones'>"
    ],
    "examples": [
      { "phrase": "<example of a sentence chunk>", "meaning": "<what it shows about rhythm or stress>", "note": "<strategy tip>" }
    ],
    "strategy": "<one sentence: how to approach listening and repeating sentences accurately>",
    "commonMistakes": [
      "changing word order when repeating",
      "dropping function words like 'the', 'a', 'to'",
      "replacing words with near-synonyms",
      "repeating too quickly without fully hearing the sentence"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about listening and repeating accurately, or null>"
  },
  "practiceContent": {
    "instructions": "Read each sentence carefully, then repeat it aloud and type exactly what you said.",
    "scenario": "<1 sentence describing the context for these sentences — may be everyday, travel, social, academic, interview, or workplace>",
    "task": "Repeat each sentence as accurately as you can, then type what you said.",
    "exerciseData": {
      "items": [
        {
          "id": "s1",
          "sentence": "<8-20 word sentence appropriate for {{cefrLevel}} and the learning context>",
          "audioScript": "<same as sentence — used as audio script fallback>",
          "audioUrl": null,
          "displayTitle": "<short label, e.g. 'Sentence 1'>",
          "difficulty": "<easy|medium|hard>",
          "focusAreas": ["<e.g. 'sentence rhythm'>", "<e.g. 'function words'>"],
          "explanation": "<one sentence coaching tip for this specific sentence>"
        },
        {
          "id": "s2",
          "sentence": "<8-20 word sentence>",
          "audioScript": "<same as sentence>",
          "audioUrl": null,
          "displayTitle": "<short label>",
          "difficulty": "<easy|medium|hard>",
          "focusAreas": ["<focus area>"],
          "explanation": "<one sentence tip>"
        },
        {
          "id": "s3",
          "sentence": "<8-20 word sentence>",
          "audioScript": "<same as sentence>",
          "audioUrl": null,
          "displayTitle": "<short label>",
          "difficulty": "<easy|medium|hard>",
          "focusAreas": ["<focus area>"],
          "explanation": "<one sentence tip>"
        },
        {
          "id": "s4",
          "sentence": "<8-20 word sentence>",
          "audioScript": "<same as sentence>",
          "audioUrl": null,
          "displayTitle": "<short label>",
          "difficulty": "<easy|medium|hard>",
          "focusAreas": ["<focus area>"],
          "explanation": "<one sentence tip>"
        },
        {
          "id": "s5",
          "sentence": "<8-20 word sentence>",
          "audioScript": "<same as sentence>",
          "audioUrl": null,
          "displayTitle": "<short label>",
          "difficulty": "<easy|medium|hard>",
          "focusAreas": ["<focus area>"],
          "explanation": "<one sentence tip>"
        }
      ],
      "successChecklist": [
        "Each sentence is repeated with all key words present.",
        "Word order matches the original sentence.",
        "Function words are not dropped."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Transcript match",
      "Repeat accuracy",
      "Words matched / missing",
      "Listening and speaking accuracy"
    ],
    "rubric": [
      { "criterion": "Accuracy", "description": "The typed transcript closely matches the original sentence." },
      { "criterion": "Word coverage", "description": "All key words from the sentence are present in the transcript." },
      { "criterion": "Word order", "description": "The word order is preserved from the original sentence." }
    ],
    "feedbackFocus": "Help the student repeat sentences accurately with correct word order and no dropped words.",
    "successCriteria": [
      "Word overlap with the original sentence is high.",
      "The student captures function words as well as content words.",
      "The transcript reflects natural sentence structure."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual sentences, sentence ids, or scoring details. It teaches general listening-and-repeating strategy only.
- Each item must have id, sentence, audioScript (same as sentence), audioUrl (null), displayTitle, difficulty, focusAreas (array), and explanation.
- Sentences must be 8-20 words and appropriate for {{cefrLevel}}.
- Sentences should reflect the learner's context — not hardcoded as workplace-only unless {{careerContext}} indicates a workplace focus.
- Vary difficulty across the 5 items (at least one easy, one hard).
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateRepeatSentenceContent = """
You are a warm English speaking coach evaluating a student's repeat-sentence exercise.

Student level: {{cefrLevel}}

Activity content (sentences and audio scripts):
{{activityContent}}

Student's submitted transcripts:
{{submittedAnswer}}

For each item, compare the student's typed transcript to the original sentence using word overlap. Identify matched words, missing words, and extra words. Provide brief, encouraging per-item feedback about listening accuracy and speaking accuracy.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0.0-1.0>,
  "overallFeedback": "<2-3 sentences of overall feedback on repeat accuracy and listening>",
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if word overlap >= 60%, false otherwise>,
      "submittedAnswer": "<what the student typed>",
      "expectedAnswer": "<the original sentence>",
      "matchedWords": <count of matched words>,
      "missingWords": ["<words not captured>"],
      "feedback": "<1 sentence of item-level feedback on repeat accuracy>"
    }
  ],
  "coachingTip": "<one actionable tip for improving listening and repeat accuracy>",
  "encouragement": "<one sentence of encouragement>"
}
""";

    private const string ActivityGenerateRespondToSituationContent = """
You are an expert English language teacher creating a respond-to-situation speaking exercise for a {{sourceLanguageName}}-speaking learner of {{targetLanguageName}}.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read a short real-life situation and speak or type an appropriate response. This practises real-world communication, social appropriateness, and spoken fluency. Content should suit the student's learning goals — which may include day-to-day English, travel, social conversation, academic English, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it.

Generate exactly 2 situations (DefaultItemsPerPractice=2, max=2). Each situation should be realistic and clear.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what speaking and communication skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["communication", "listening"],
  "exerciseType": "respond_to_situation",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to respond naturally in everyday situations'>",
    "explanation": "<2-4 sentences: general strategy for responding clearly and appropriately in spoken English. No reference to the actual situations below.>",
    "keyPoints": [
      "<e.g. 'Acknowledge what the other person said before giving your response'>",
      "<e.g. 'Keep your response relevant and brief — 1-3 sentences is usually enough'>",
      "<e.g. 'Match your tone to the situation — formal for professional, relaxed for social'>",
      "<e.g. 'If you are unsure, it is fine to ask a polite clarifying question'>"
    ],
    "examples": [
      { "phrase": "<example of a polite, clear response opener>", "meaning": "<what makes it natural>", "note": "<when to use it>" }
    ],
    "strategy": "<one sentence: how to approach responding to real-life situations in English>",
    "commonMistakes": [
      "responding too briefly without acknowledging the situation",
      "using overly formal or stiff language in casual contexts",
      "ignoring the tone or relationship implied in the situation",
      "going off-topic instead of addressing what was asked"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about responding naturally in English, or null>"
  },
  "practiceContent": {
    "instructions": "Read each situation below, then speak or type an appropriate response in English.",
    "scenario": "<1 sentence describing the general context for these situations — may be everyday, travel, social, academic, interview, or workplace>",
    "task": "Respond to each situation as naturally and clearly as you can.",
    "exerciseData": {
      "items": [
        {
          "id": "sit1",
          "situation": "<2-4 sentences describing a real-life situation the student needs to respond to>",
          "contextLabel": "<e.g. 'Daily life', 'Travel', 'Study', 'Workplace', 'Social', 'Interview'>",
          "role": "<the student's role in the situation, e.g. 'customer', 'student', 'friend', 'job applicant'>",
          "audience": "<who the student is speaking to, e.g. 'shop assistant', 'professor', 'colleague', 'interviewer'>",
          "prompt": "<optional: a short direct question or cue to respond to, e.g. 'What do you say?'>",
          "audioScript": null,
          "audioUrl": null,
          "expectedResponseGuidance": "<2-4 sentences describing what a good response should cover — NOT a single correct answer>",
          "goodResponseExamples": [
            "<one example of a natural, appropriate response>",
            "<one alternative phrasing or approach>"
          ],
          "focusAreas": ["<e.g. 'politeness'>", "<e.g. 'clarity'>", "<e.g. 'relevance'>"],
          "explanation": "<one sentence coaching tip for this specific situation>"
        },
        {
          "id": "sit2",
          "situation": "<2-4 sentences describing a different real-life situation>",
          "contextLabel": "<context label>",
          "role": "<student role>",
          "audience": "<audience>",
          "prompt": "<optional prompt>",
          "audioScript": null,
          "audioUrl": null,
          "expectedResponseGuidance": "<2-4 sentences describing what a good response should cover>",
          "goodResponseExamples": [
            "<example response>",
            "<alternative>"
          ],
          "focusAreas": ["<focus area>", "<focus area>"],
          "explanation": "<one sentence tip>"
        }
      ],
      "successChecklist": [
        "The response is relevant to the situation described.",
        "The tone matches the context (formal/informal as appropriate).",
        "The response is clear and complete — not too brief or off-topic."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Relevance to the situation",
      "Clarity and completeness",
      "Natural phrasing",
      "Tone and politeness",
      "Grammar (secondary)"
    ],
    "rubric": [
      { "criterion": "Relevance", "description": "The response directly addresses the situation and what is asked." },
      { "criterion": "Clarity", "description": "The response is easy to understand and gets the point across." },
      { "criterion": "Natural phrasing", "description": "The language sounds natural and appropriate for the context." },
      { "criterion": "Tone", "description": "The tone matches the relationship and situation (formal/informal)." }
    ],
    "feedbackFocus": "Help the student respond naturally, relevantly, and appropriately to real-life situations.",
    "successCriteria": [
      "The response addresses the situation directly.",
      "The tone is appropriate for the context.",
      "The language is clear and natural."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual situations, expected answers, or scoring rubric details. It teaches general responding strategy only.
- Each item must have id, situation, contextLabel, role, audience, audioScript (null), audioUrl (null), expectedResponseGuidance, goodResponseExamples, focusAreas, and explanation. prompt is optional.
- Situations must be realistic, brief (2-4 sentences), and appropriate for {{cefrLevel}}.
- Situations should reflect the learner's context — not hardcoded as workplace-only unless {{careerContext}} indicates a workplace focus.
- Vary the context labels across the 2 items (e.g. one daily life, one travel; or one social, one interview).
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateRespondToSituationContent = """
You are a warm, encouraging English speaking coach evaluating a student's respond-to-situation exercise.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}

Activity content (situations and guidance):
{{activityContent}}

Student's submitted responses:
{{submittedAnswer}}

For each item, evaluate the student's response against the situation and expectedResponseGuidance. Assess relevance, clarity, natural phrasing, and tone. Do NOT require an exact match — the student's response is open-ended.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentences of overall feedback on how well the student responded to the situations>",
  "strengths": [
    "<one specific strength observed across the responses>",
    "<another strength>"
  ],
  "improvements": [
    "<one specific area for improvement>",
    "<another if relevant>"
  ],
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if the response is relevant and appropriate, false if clearly off-topic or empty>,
      "score": <0-100 for this item>,
      "studentResponse": "<what the student submitted>",
      "feedback": "<2-3 sentences of item-level feedback covering relevance, clarity, tone, and natural phrasing>",
      "betterExample": "<one example of a natural, improved response if useful, or null>"
    }
  ],
  "suggestedImprovedResponse": "<one example of a strong overall response approach if needed, or null>",
  "miniLesson": "<one actionable tip for improving spoken responses to real-life situations>",
  "nextImprovementStep": "<one specific thing to practise next>"
}

Rules:
- Do not require exact match. Assess whether the response is appropriate, relevant, and natural.
- Award full or near-full score if the response is clearly appropriate, even if phrasing differs from the example.
- If the student left a response blank or submitted only whitespace, isCorrect=false, score=0, feedback should note the response was not provided.
- Do not penalise minor grammar errors unless they affect understanding.
- Do not claim pronunciation or fluency scoring — this evaluates only the typed text.
- Keep feedback warm, specific, and actionable.
""";

    private const string ActivityGenerateDescribeImageContent = """
You are an expert English language teacher creating a describe-image speaking exercise for a {{sourceLanguageName}}-speaking learner of {{targetLanguageName}}.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read a descriptive image prompt and speak or type a description of what they imagine seeing. This practises descriptive vocabulary, sentence organisation, and natural spoken English. Content should suit the student's learning goals — which may include day-to-day English, travel, social conversation, academic English, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it.

Generate exactly 1 image prompt (DefaultItemsPerPractice=1, max=1). The image prompt should describe a scene clearly so the student can picture it.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what descriptive speaking skill this practises>",
  "primarySkill": "speaking",
  "secondarySkills": ["vocabulary", "communication"],
  "exerciseType": "describe_image",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to describe what you see clearly'>",
    "explanation": "<2-4 sentences: general strategy for describing images clearly in spoken English. Teach vocabulary organisation, detail selection, and structure. No reference to the actual image below.>",
    "keyPoints": [
      "<e.g. 'Start with the overall scene before describing specific details'>",
      "<e.g. 'Use location words: in the foreground, in the background, on the left, at the top'>",
      "<e.g. 'Describe colours, shapes, actions, and relationships between objects'>",
      "<e.g. 'Use present tense for describing what you see right now'>"
    ],
    "examples": [
      { "phrase": "<example of a natural description opener>", "meaning": "<what makes it clear>", "note": "<when to use it>" }
    ],
    "strategy": "<one sentence: how to approach describing an image in spoken English>",
    "commonMistakes": [
      "describing objects in a random order rather than left-to-right or background-to-foreground",
      "using very general words instead of specific descriptive vocabulary",
      "forgetting to describe the overall setting before individual details",
      "translating directly from the first language instead of using English description patterns"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about describing images in English, or null>"
  },
  "practiceContent": {
    "instructions": "Look at the image prompt below. Describe what you see as clearly and naturally as possible. Speak or type your description.",
    "scenario": "<1 sentence describing the general type of scene — may be everyday, travel, social, academic, interview, or workplace>",
    "task": "Describe the image as fully and clearly as you can.",
    "exerciseData": {
      "items": [
        {
          "id": "img1",
          "imagePrompt": "<3-5 sentences describing a realistic scene in enough detail for the student to picture it clearly. Describe what is in the scene: people, objects, setting, colours, actions. Do NOT use real brand names, copyrighted material, or sensitive content.>",
          "imageDescription": "<1-2 sentence summary of the scene for accessibility>",
          "imageUrl": null,
          "displayTitle": "<short title for this image, e.g. 'A busy café'>",
          "contextLabel": "<e.g. 'Daily life', 'Travel', 'Study', 'Workplace', 'Social', 'Nature', 'City'>",
          "focusAreas": ["<e.g. 'describing people'>", "<e.g. 'location words'>", "<e.g. 'actions and movement'>"],
          "expectedResponseGuidance": "<2-4 sentences describing what a good description should cover — NOT a single correct answer. Should mention key visible elements, use of location language, and descriptive vocabulary.>",
          "goodResponseExamples": [
            "<one example of a natural, clear description of this image>",
            "<one alternative approach or focus>"
          ],
          "explanation": "<one sentence coaching tip for describing this type of image>"
        }
      ],
      "successChecklist": [
        "The description covers the main elements visible in the image.",
        "Location words are used to organise the description.",
        "Descriptive vocabulary (colours, shapes, actions) is included.",
        "The description is clear and easy to follow."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Relevance to the image prompt",
      "Detail and completeness",
      "Organisation",
      "Vocabulary range",
      "Clarity",
      "Grammar (secondary)"
    ],
    "rubric": [
      { "criterion": "Relevance", "description": "The description addresses the main elements of the image." },
      { "criterion": "Detail", "description": "The student describes specific details rather than only general impressions." },
      { "criterion": "Organisation", "description": "The description follows a logical order using location language." },
      { "criterion": "Vocabulary", "description": "The student uses descriptive vocabulary appropriate for the scene." }
    ],
    "feedbackFocus": "Help the student describe images clearly using organised structure and rich vocabulary.",
    "successCriteria": [
      "The description covers the key visible elements.",
      "The structure is clear and logical.",
      "Vocabulary is varied and descriptive."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual image prompt, expected descriptions, or scoring rubric details. It teaches general image-description strategy only.
- The item must have id, imagePrompt, imageDescription, imageUrl (null), displayTitle, contextLabel, focusAreas, expectedResponseGuidance, goodResponseExamples, and explanation.
- imageUrl must always be null — no real image URLs or external links.
- Image prompts must be realistic, vivid, and appropriate for {{cefrLevel}}.
- Image scenes should reflect the learner's context — not hardcoded as workplace-only unless {{careerContext}} indicates a workplace focus.
- Do NOT include any text outside the JSON object.
""";

    private const string ActivityEvaluateDescribeImageContent = """
You are a warm, encouraging English speaking coach evaluating a student's describe-image exercise.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}

Activity content (image prompts and guidance):
{{activityContent}}

Student's submitted descriptions:
{{submittedAnswer}}

For each item, evaluate the student's description against the imagePrompt and expectedResponseGuidance. Assess: relevance to the image, amount of detail, organisation, vocabulary range, clarity, and natural spoken style. Grammar is a secondary consideration. Do NOT require an exact match — the student's description is open-ended.

This is NOT computer-vision scoring. You are evaluating the typed text only, not a real image.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentences of overall feedback on the quality and completeness of the description>",
  "strengths": [
    "<one specific strength observed in the description>",
    "<another strength if present>"
  ],
  "improvements": [
    "<one specific area for improvement>",
    "<another if relevant>"
  ],
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if the description is relevant and covers key image elements, false if clearly off-topic or empty>,
      "score": <0-100 for this item>,
      "studentResponse": "<what the student submitted>",
      "feedback": "<2-3 sentences of item-level feedback covering relevance, detail, organisation, vocabulary, and clarity>",
      "betterExample": "<one example of a stronger, more complete description if useful, or null>"
    }
  ],
  "suggestedImprovedResponse": "<one example of a fuller, well-organised description if helpful, or null>",
  "miniLesson": "<one actionable tip for improving image descriptions>",
  "nextImprovementStep": "<one specific thing to practise next>"
}

Rules:
- Do not require exact match. Assess whether the description is relevant, detailed, and clearly expressed.
- Award full or near-full score if the student describes the key elements clearly and uses appropriate vocabulary.
- If the student left a response blank or submitted only whitespace, isCorrect=false, score=0, feedback should note the response was not provided.
- Do not penalise minor grammar errors unless they affect understanding.
- Do not claim pronunciation, fluency, or image-recognition scoring — this evaluates only the typed text.
- Keep feedback warm, specific, and actionable.
""";

    private const string ActivityGenerateRetellLectureContent = """
You are an expert English language teacher creating a retell-lecture listening and speaking exercise for a {{sourceLanguageName}}-speaking learner of {{targetLanguageName}}.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read or listen to a short lecture or talk, then retell the main ideas in their own words. This practises listening comprehension, summarising, and spoken communication. Content should suit the student's learning goals — which may include daily life, travel, study, social communication, migration, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it.

Generate exactly 1 lecture item (DefaultItemsPerPractice=1, max=1). The lecture script should be 80-150 words — a short, realistic talk or explanation that the student can follow at {{cefrLevel}}.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening and speaking skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["speaking", "summarizing", "communication"],
  "exerciseType": "retell_lecture",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to retell key ideas clearly'>",
    "explanation": "<2-4 sentences: general strategy for listening and retelling a talk. Teach how to identify main ideas, note supporting details, and organise a spoken summary. No reference to the actual lecture below.>",
    "keyPoints": [
      "<e.g. 'Listen for the main topic in the first sentence'>",
      "<e.g. 'Note key supporting details — numbers, names, examples'>",
      "<e.g. 'Use your own words — do not try to memorise every sentence'>",
      "<e.g. 'Structure your retelling: first say the main idea, then add key details'>"
    ],
    "examples": [
      { "phrase": "<example of a natural retelling opener>", "meaning": "<what makes it effective>", "note": "<when to use it>" }
    ],
    "strategy": "<one sentence: how to approach retelling a lecture in spoken English>",
    "commonMistakes": [
      "trying to repeat the lecture word-for-word instead of summarising",
      "focusing on minor details and missing the main point",
      "using very short responses without explanation or supporting ideas",
      "translating directly from the first language instead of using natural English summary phrases"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about retelling talks in English, or null>"
  },
  "practiceContent": {
    "instructions": "Read the lecture below carefully. Then retell the main ideas in your own words. Type your response as if you were explaining it to someone who has not heard the lecture.",
    "scenario": "<1 sentence describing the general topic and setting — may be everyday, travel, social, academic, interview, or workplace>",
    "task": "Retell the main ideas of the lecture in your own words.",
    "exerciseData": {
      "items": [
        {
          "id": "lec1",
          "lectureTitle": "<short title for this lecture, e.g. 'How Sleep Affects Memory'>",
          "lectureTopic": "<1 sentence describing the lecture topic>",
          "audioScript": "<80-150 words: the lecture text. Write in a natural spoken style. Include a clear main idea, 2-3 supporting points, and a brief conclusion. Suitable for {{cefrLevel}}.>",
          "audioUrl": null,
          "contextLabel": "<e.g. 'Health', 'Study', 'Travel', 'Daily life', 'Workplace', 'Science', 'Social', 'Interview'>",
          "difficulty": "<e.g. 'intermediate' for B1-B2, 'upper-intermediate' for C1>",
          "keyPoints": [
            "<main idea of the lecture>",
            "<key supporting detail 1>",
            "<key supporting detail 2>"
          ],
          "importantVocabulary": [
            { "word": "<key word or phrase>", "meaning": "<brief meaning>" }
          ],
          "expectedSummaryGuidance": "<2-3 sentences describing what a good retelling should cover — NOT a single correct answer. Should mention main ideas, key details, and appropriate organisation.>",
          "goodResponseExample": "<one example of a natural, clear retelling of this lecture in 3-5 sentences>",
          "focusAreas": ["<e.g. 'main ideas'>", "<e.g. 'key details'>", "<e.g. 'organisation'>", "<e.g. 'clarity'>"]
        }
      ],
      "successChecklist": [
        "The retelling covers the main idea of the lecture.",
        "At least one key supporting detail is included.",
        "The response is in the student's own words, not a direct copy.",
        "The response is clear and logically organised."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Coverage of main ideas",
      "Inclusion of key supporting details",
      "Use of own words (not direct copy)",
      "Organisation and logical flow",
      "Clarity",
      "Vocabulary use",
      "Grammar (secondary)"
    ],
    "rubric": [
      { "criterion": "Main idea coverage", "description": "The retelling clearly states the main point of the lecture." },
      { "criterion": "Detail inclusion", "description": "The student includes at least one important supporting detail." },
      { "criterion": "Own words", "description": "The student paraphrases rather than copying the lecture." },
      { "criterion": "Organisation", "description": "The retelling follows a logical order." }
    ],
    "feedbackFocus": "Help the student identify and retell the main ideas clearly and in their own words.",
    "successCriteria": [
      "The main idea is clearly stated.",
      "Key supporting details are included.",
      "The response is in the student's own words.",
      "The structure is clear and logical."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual lecture script, key points, expected summary, or scoring rubric details. It teaches general retelling strategy only.
- The item must have id, lectureTitle, lectureTopic, audioScript, audioUrl (null), contextLabel, difficulty, keyPoints, importantVocabulary, expectedSummaryGuidance, goodResponseExample, and focusAreas.
- audioUrl must always be null — no real audio URLs.
- The lecture script should be realistic, clear, and appropriate for {{cefrLevel}}.
- Lecture topics should reflect the learner's context — not hardcoded as workplace-only unless {{careerContext}} indicates a workplace focus.
- Do NOT include any text outside the JSON object.
""";

    private const string ActivityEvaluateRetellLectureContent = """
You are a warm, encouraging English speaking coach evaluating a student's retell-lecture exercise.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}

Activity content (lecture scripts and guidance):
{{activityContent}}

Student's submitted retellings:
{{submittedAnswer}}

For each item, evaluate the student's retelling against the audioScript, keyPoints, and expectedSummaryGuidance. Assess: coverage of main ideas, inclusion of supporting details, use of own words, organisation, clarity, and vocabulary. Grammar is a secondary consideration. Do NOT require an exact match — the student's retelling is open-ended.

This is NOT pronunciation scoring, fluency scoring, or audio analysis. You are evaluating the typed text only.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentences of overall feedback on coverage, clarity, and organisation of the retelling>",
  "strengths": [
    "<one specific strength observed in the retelling>",
    "<another strength if present>"
  ],
  "improvements": [
    "<one specific area for improvement>",
    "<another if relevant>"
  ],
  "missingExpectedPoints": [
    "<a key idea or detail from the lecture that the student omitted, if any>"
  ],
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if the retelling covers the main idea and at least one supporting detail, false if clearly off-topic or empty>,
      "score": <0-100 for this item>,
      "studentResponse": "<what the student submitted>",
      "feedback": "<2-3 sentences of item-level feedback covering main idea coverage, detail inclusion, organisation, and clarity>",
      "missingPoints": ["<key idea or detail missed, if any>"],
      "betterExample": "<one example of a stronger retelling if useful, or null>"
    }
  ],
  "suggestedImprovedResponse": "<one example of a fuller, well-organised retelling if helpful, or null>",
  "miniLesson": "<one actionable tip for improving retelling or summarising skills>",
  "nextImprovementStep": "<one specific thing to practise next>"
}

Rules:
- Do not require exact match. Assess whether the retelling covers the main ideas and is clearly expressed.
- Award full or near-full score if the student covers the main idea and key details in clear language.
- If the student left a response blank or submitted only whitespace, isCorrect=false, score=0, feedback should note the response was not provided.
- Do not penalise minor grammar errors unless they affect understanding.
- Do not claim pronunciation, fluency, or audio-quality scoring — this evaluates only the typed text.
- If the student copied the lecture word-for-word, note this and encourage paraphrasing, but still award partial credit for coverage.
- Keep feedback warm, specific, and actionable.
""";

    private const string ActivityGenerateSummarizeGroupDiscussionContent = """
You are an expert English language teacher creating a summarize-group-discussion listening and speaking exercise for a {{sourceLanguageName}}-speaking learner of {{targetLanguageName}}.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read or listen to a short multi-speaker discussion, then summarize the main points, speaker views, agreements, disagreements, and outcomes. This practises listening comprehension, summarising, and spoken communication. Content should suit the student's learning goals — which may include daily life, travel, study, social communication, migration, job interviews, workplace English, or other goals. Do not assume a workplace-only context unless {{careerContext}} indicates it.

Generate exactly 1 discussion item (DefaultItemsPerPractice=1, max=1). The discussion script should be 80-160 words — a short, realistic multi-speaker exchange that the student can follow at {{cefrLevel}}.

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what listening and summarising skill this practises>",
  "primarySkill": "listening",
  "secondarySkills": ["speaking", "summarizing", "communication"],
  "exerciseType": "summarize_group_discussion",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to summarize a group discussion clearly'>",
    "explanation": "<2-4 sentences: general strategy for listening to a group discussion and summarizing it. Teach how to identify main points, note each speaker's position, and organise a spoken or written summary. No reference to the actual discussion below.>",
    "keyPoints": [
      "<e.g. 'Listen for the discussion topic stated early in the conversation'>",
      "<e.g. 'Note each speaker's position or opinion separately'>",
      "<e.g. 'Identify any agreements, disagreements, or decisions reached'>",
      "<e.g. 'Use your own words — do not repeat every sentence from the discussion'>"
    ],
    "examples": [
      { "phrase": "<example of a natural summary opener for a discussion>", "meaning": "<what makes it effective>", "note": "<when to use it>" }
    ],
    "strategy": "<one sentence: how to approach summarizing a group discussion>",
    "commonMistakes": [
      "summarizing only one speaker and ignoring the others",
      "copying phrases directly from the discussion instead of paraphrasing",
      "leaving out the outcome or decision if one was reached",
      "translating directly from the first language instead of using natural English summary phrases"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about summarizing discussions in English, or null>"
  },
  "practiceContent": {
    "instructions": "Read the discussion below carefully. Then summarize the main points, each speaker's view, any agreements or disagreements, and the outcome if there is one. Type your response as if you were explaining it to someone who was not part of the discussion.",
    "scenario": "<1 sentence describing the general topic and setting — may be everyday, travel, social, academic, interview, or workplace>",
    "task": "Summarize the main points of the discussion, including each speaker's view and any agreements, disagreements, or outcomes.",
    "exerciseData": {
      "items": [
        {
          "id": "disc1",
          "discussionTitle": "<short title for this discussion, e.g. 'Planning a Weekend Trip'>",
          "discussionTopic": "<1 sentence describing the discussion topic>",
          "audioScript": "<80-160 words: the discussion text. Write as a natural multi-speaker exchange with 2-3 named speakers. Include a clear topic, each speaker's position or contribution, and a resolution or outcome if appropriate. Suitable for {{cefrLevel}}.>",
          "audioUrl": null,
          "contextLabel": "<e.g. 'Social', 'Study', 'Travel', 'Daily life', 'Workplace', 'Health', 'Interview'>",
          "speakers": [
            { "name": "<speaker 1 name>", "role": "<optional role or relationship>", "viewpoint": null },
            { "name": "<speaker 2 name>", "role": "<optional role or relationship>", "viewpoint": null }
          ],
          "keyPoints": [
            "<main topic or issue discussed>",
            "<speaker 1's main view or contribution>",
            "<speaker 2's main view or contribution>",
            "<agreement, disagreement, or outcome if present>"
          ],
          "agreements": ["<any agreement reached, or null>"],
          "disagreements": ["<any disagreement noted, or null>"],
          "decisionOrOutcome": "<decision or outcome if reached, or null>",
          "importantVocabulary": [
            { "word": "<key word or phrase from the discussion>", "meaning": "<brief meaning>" }
          ],
          "expectedSummaryGuidance": "<2-3 sentences describing what a good summary should cover — NOT a single correct answer. Should mention main points, speaker positions, and outcome if present.>",
          "goodResponseExample": "<one example of a natural, clear summary of this discussion in 3-5 sentences>",
          "focusAreas": ["<e.g. 'main points'>", "<e.g. 'speaker views'>", "<e.g. 'outcome'>", "<e.g. 'organisation'>"]
        }
      ],
      "successChecklist": [
        "The summary covers the main topic of the discussion.",
        "At least one speaker's view or contribution is included.",
        "Any agreement, disagreement, or outcome is mentioned if present.",
        "The response is in the student's own words, not a direct copy."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Coverage of main discussion points",
      "Recognition of speaker views or roles",
      "Inclusion of agreements, disagreements, or outcome where present",
      "Organisation and logical flow",
      "Clarity",
      "Vocabulary use",
      "Grammar (secondary)"
    ],
    "rubric": [
      { "criterion": "Main point coverage", "description": "The summary clearly states the main topic and key points of the discussion." },
      { "criterion": "Speaker views", "description": "The student identifies at least one speaker's position or contribution." },
      { "criterion": "Outcome", "description": "The student mentions any agreement, disagreement, or decision reached." },
      { "criterion": "Own words", "description": "The student paraphrases rather than copying the discussion." }
    ],
    "feedbackFocus": "Help the student identify and summarize each speaker's contribution and any outcomes clearly and in their own words.",
    "successCriteria": [
      "The main topic is clearly stated.",
      "Speaker views or contributions are included.",
      "Any outcome or decision is mentioned.",
      "The response is in the student's own words."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual discussion script, speaker viewpoints, key points, expected summary, or scoring rubric details. It teaches general summarising strategy only.
- The item must have id, discussionTitle, discussionTopic, audioScript, audioUrl (null), contextLabel, speakers, keyPoints, importantVocabulary, expectedSummaryGuidance, goodResponseExample, and focusAreas.
- audioUrl must always be null — no real audio URLs.
- The discussion script should be realistic, clear, and appropriate for {{cefrLevel}}.
- Discussion topics should reflect the learner's context — not hardcoded as workplace-only unless {{careerContext}} indicates a workplace focus.
- Do NOT include any text outside the JSON object.
""";

    private const string ActivityEvaluateSummarizeGroupDiscussionContent = """
You are a warm, encouraging English speaking coach evaluating a student's summarize-group-discussion exercise.

Student level: {{cefrLevel}}
Learning context: {{careerContext}}

Activity content (discussion scripts and guidance):
{{activityContent}}

Student's submitted summaries:
{{submittedAnswer}}

For each item, evaluate the student's summary against the audioScript, keyPoints, speakers, agreements, disagreements, decisionOrOutcome, and expectedSummaryGuidance. Assess: coverage of main discussion points, recognition of speaker views or roles where relevant, inclusion of agreements/disagreements/outcome where present, organisation, clarity, vocabulary use. Grammar is a secondary consideration. Do NOT require an exact match — the student's summary is open-ended.

This is NOT pronunciation scoring, fluency scoring, or audio analysis. You are evaluating the typed text only.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentences of overall feedback on coverage of main points, speaker views, and clarity of the summary>",
  "strengths": [
    "<one specific strength observed in the summary>",
    "<another strength if present>"
  ],
  "improvements": [
    "<one specific area for improvement>",
    "<another if relevant>"
  ],
  "missingExpectedPoints": [
    "<a key point, speaker view, or outcome from the discussion that the student omitted, if any>"
  ],
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if the summary covers the main topic and at least one speaker's contribution, false if clearly off-topic or empty>,
      "score": <0-100 for this item>,
      "studentResponse": "<what the student submitted>",
      "feedback": "<2-3 sentences of item-level feedback covering main point coverage, speaker view recognition, outcome inclusion, organisation, and clarity>",
      "missingPoints": ["<key point, speaker view, or outcome missed, if any>"],
      "betterExample": "<one example of a stronger summary if useful, or null>"
    }
  ],
  "suggestedImprovedResponse": "<one example of a fuller, well-organised summary if helpful, or null>",
  "miniLesson": "<one actionable tip for improving summarising or discussion comprehension skills>",
  "nextImprovementStep": "<one specific thing to practise next>"
}

Rules:
- Do not require exact match. Assess whether the summary covers the main points and is clearly expressed.
- Award full or near-full score if the student covers the main topic, at least one speaker's contribution, and the outcome (if present) in clear language.
- If the student left a response blank or submitted only whitespace, isCorrect=false, score=0, feedback should note the response was not provided.
- Do not penalise minor grammar errors unless they affect understanding.
- Do not claim pronunciation, fluency, or audio-quality scoring — this evaluates only the typed text.
- If the student copied the discussion word-for-word, note this and encourage paraphrasing, but still award partial credit for coverage.
- Keep feedback warm, specific, and actionable.
""";

    private const string ActivityEvaluateReadAloudContent = """
You are a warm, professional English speaking coach evaluating a student's read-aloud exercise.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (texts and expected transcripts):
{{activityContent}}

Student's submitted transcripts:
{{submittedAnswer}}

For each item, compare the student's typed transcript to the expectedText using word overlap. Count matched words, missing words, and extra words. Provide brief, encouraging per-item feedback about clarity, pacing, and word coverage.

Return ONLY valid JSON in this exact format:

{
  "overallScore": <0.0-1.0>,
  "overallFeedback": "<2-3 sentences of overall feedback on reading clarity and accuracy>",
  "itemResults": [
    {
      "itemId": "<item id>",
      "isCorrect": <true if word overlap >= 60%, false otherwise>,
      "submittedAnswer": "<what the student typed>",
      "expectedAnswer": "<the original text>",
      "matchedWords": <count of matched words>,
      "missingWords": ["<key words not captured>"],
      "feedback": "<1 sentence of item-level feedback on clarity and coverage>"
    }
  ],
  "coachingTip": "<one actionable tip for improving read-aloud clarity or pacing>",
  "encouragement": "<one sentence of encouragement>"
}
""";

    private const string ActivityEvaluateSummarizeSpokenTextContent = """
You are an expert English language teacher evaluating a student's summary of a spoken text.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (audio script and requirements):
{{activityContent}}

Student summary:
{{studentSubmission}}

Evaluate the summary against the rubric criteria and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentence warm but honest feedback: did the student capture the main idea? Is it concise? Any key gaps or unsupported details?>",
  "focusFirst": false,
  "changes": [
    {
      "type": "grammar|vocabulary|content|concision|paraphrasing",
      "original": "<phrase or issue from student's summary>",
      "suggested": "<improved version>",
      "reason": "<brief explanation>",
      "category": "<grammar|vocabulary|content|concision|paraphrasing>",
      "severity": "low|medium|high"
    }
  ],
  "whatYouDidWell": ["<specific positive: main idea captured>", "<specific positive: concise>"],
  "mainMistakes": ["<key content gap, unsupported detail, or language issue if any>"],
  "grammarIssues": ["<grammar issue if any>"],
  "vocabularyIssues": ["<vocabulary issue if any>"],
  "toneIssues": [],
  "miniLesson": "<one sentence teaching moment about summarising spoken text or language use>",
  "improvedVersion": "<a model summary of 50-70 words that demonstrates what a good answer looks like>",
  "nextImprovementStep": "<one specific action for the student to practise next>",
  "feedbackInSourceLanguage": "<1-2 sentences of encouragement in {{sourceLanguageName}}>"
}

Scoring guide:
- 90-100: Main idea + all key points + concise + own words + no unsupported details + clean language
- 75-89: Main idea + most key points + mostly own words + minor language issues
- 60-74: Main idea present but key points missing or some unsupported details
- 40-59: Main idea unclear or significantly incomplete
- 0-39: Missing main idea, off-topic, or invented content

If the student's summary is empty or too short to evaluate, set overallScore to 0 and explain in coachSummary.
Do not include any text outside the JSON object.
""";

    private const string ActivityGenerateWriteEssayContent = """
You are an expert English language teacher creating a write-essay exercise for a {{sourceLanguageName}}-speaking professional learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}
Topic area: {{topicHint}}
Recent mistakes to address: {{recentMistakes}}

The student will read an essay prompt and write a structured essay response (introduction, body paragraphs, conclusion).

Return ONLY valid JSON in this exact format:

{
  "schemaVersion": "module_stage_v1",
  "title": "<short title, 5-8 words>",
  "moduleGoal": "<one sentence: what essay-writing skill this practises>",
  "primarySkill": "writing",
  "secondarySkills": [],
  "exerciseType": "write_essay",
  "learnContent": {
    "teachingTitle": "<short heading, e.g. 'How to plan and structure an essay'>",
    "explanation": "<2-4 sentences teaching general essay strategy: answering the prompt directly, organising ideas, and supporting them with examples. No reference to the specific prompt or topic below.>",
    "keyPoints": [
      "<how to answer the question directly>",
      "<how to organise introduction/body/conclusion>",
      "<how to support ideas with examples>"
    ],
    "examples": [
      { "phrase": "<useful essay phrase or structure>", "meaning": "<when to use it>", "note": "<writing strategy note>" }
    ],
    "strategy": "<one sentence: how to plan, structure, and write the essay>",
    "commonMistakes": [
      "not answering the prompt directly",
      "weak paragraph structure",
      "unsupported ideas"
    ],
    "sourceLanguageSupport": "<optional: 1-2 sentences in {{sourceLanguageName}} about essay-writing strategy, or null>"
  },
  "practiceContent": {
    "instructions": "Read the essay prompt below and write a structured essay response.",
    "scenario": "<optional 1 sentence describing the academic/workplace context>",
    "task": "Write an essay responding to the prompt below.",
    "exerciseData": {
      "prompt": "<a clear essay prompt/question relevant to {{careerContext}} and {{topicHint}}>",
      "topic": "<short topic label for the essay>",
      "essayType": "<opinion|discussion|problem-solution|advantage-disadvantage>",
      "requirements": {
        "targetWordCount": "180-250 words",
        "minimumParagraphs": 3,
        "mustAddress": ["<required point 1>", "<required point 2>"],
        "avoid": ["<common issue to avoid>"]
      },
      "planningHints": [
        "<optional planning hint>",
        "<optional planning hint>"
      ],
      "successChecklist": [
        "The essay answers the prompt.",
        "The essay has a clear introduction, body, and conclusion.",
        "The essay supports ideas with examples and clear language."
      ]
    }
  },
  "feedbackPlan": {
    "evaluationCriteria": [
      "Task response",
      "Organisation",
      "Idea development",
      "Coherence and cohesion",
      "Grammar",
      "Vocabulary",
      "Tone and register"
    ],
    "rubric": [
      { "criterion": "Task response", "description": "Answers the prompt directly and fully." },
      { "criterion": "Structure", "description": "Uses clear paragraph organisation and logical flow." },
      { "criterion": "Language", "description": "Uses accurate grammar, vocabulary, and sentence structure." }
    ],
    "feedbackFocus": "Help the student write a clear, structured essay with supported ideas.",
    "successCriteria": [
      "The essay answers the prompt.",
      "The essay has a clear introduction, body, and conclusion.",
      "The essay supports ideas with examples and clear language."
    ]
  }
}

Rules:
- learnContent must NEVER contain the actual essay prompt, topic, model essay, expected answer, or any reference to the specific exercise content. It teaches general essay-writing strategy only.
- practiceContent.exerciseData.prompt must be a clear, answerable essay question.
- requirements.mustAddress items must be derivable from the prompt — not generic.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateWriteEssayContent = """
You are an expert English language teacher evaluating a student's essay.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (essay prompt and requirements):
{{activityContent}}

Student essay:
{{studentSubmission}}

Evaluate the essay against the rubric criteria and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentence warm but honest feedback: did the student answer the prompt? Is the essay well organised? Any key gaps?>",
  "focusFirst": false,
  "changes": [
    {
      "type": "grammar|vocabulary|structure|content|tone",
      "original": "<phrase or issue from student's essay>",
      "suggested": "<improved version>",
      "reason": "<brief explanation>",
      "category": "<grammar|vocabulary|structure|content|tone>",
      "severity": "low|medium|high"
    }
  ],
  "whatYouDidWell": ["<specific positive: answered the prompt>", "<specific positive: clear structure>"],
  "mainMistakes": ["<key content, structure, or language issue if any>"],
  "grammarIssues": ["<grammar issue if any>"],
  "vocabularyIssues": ["<vocabulary issue if any>"],
  "toneIssues": ["<tone/register issue if any>"],
  "miniLesson": "<one sentence teaching moment about essay structure or language use>",
  "improvedVersion": "<a short model paragraph or outline that demonstrates what a strong response looks like>",
  "nextImprovementStep": "<one specific action for the student to practise next>",
  "feedbackInSourceLanguage": "<1-2 sentences of encouragement in {{sourceLanguageName}}>"
}

Scoring guide:
- 90-100: Fully answers the prompt + clear introduction/body/conclusion + well-supported ideas + clean language
- 75-89: Answers the prompt + mostly clear structure + mostly supported ideas + minor language issues
- 60-74: Partially answers the prompt or structure/support is incomplete
- 40-59: Weak response to the prompt, unclear structure, or significant language issues
- 0-39: Does not address the prompt, missing structure, or essay is far too short

If the student's essay is empty or too short to evaluate, set overallScore to 0 and explain in coachSummary.
Do not include any text outside the JSON object.
""";

    // ── Pattern-specific evaluation prompts ───────────────────────────────────

    private const string ActivityEvaluatePhraseMatchContent = """
You are an English language teacher evaluating a student's phrase-matching exercise.

Student level: {{cefrLevel}}
Activity content: {{activityContent}}
Student submission: {{studentSubmission}}

Evaluate whether the student matched the phrases correctly and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<1-2 sentence warm feedback summary>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<specific thing done well>"],
  "mainMistakes": ["<key mistake if any>"],
  "grammarIssues": [],
  "vocabularyIssues": [],
  "toneIssues": [],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": ["<phrase worth memorising>"],
  "miniLesson": "<one sentence teaching moment>",
  "nextImprovementStep": null,
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<what to practise next>",
  "feedbackInSourceLanguage": "<1-2 sentences of encouragement in {{sourceLanguageName}}>"
}
""";

    private const string ActivityEvaluateGapFillContent = """
You are an English language teacher evaluating a student's gap-fill exercise.

Student level: {{cefrLevel}}
Activity content: {{activityContent}}
Student answers: {{studentSubmission}}

Evaluate accuracy and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<1-2 sentence warm feedback>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<specific correct answers or patterns>"],
  "mainMistakes": ["<key errors if any>"],
  "grammarIssues": [],
  "vocabularyIssues": ["<any vocabulary errors>"],
  "toneIssues": [],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": ["<word worth memorising>"],
  "miniLesson": "<one sentence teaching moment about the target language>",
  "nextImprovementStep": null,
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<recommendation for next practice>",
  "feedbackInSourceLanguage": "<1-2 sentences in {{sourceLanguageName}}>"
}
""";

    private const string ActivityEvaluateListenAndAnswerContent = """
You are an English language teacher evaluating a student's listening comprehension answers.

Student level: {{cefrLevel}}
Activity content (questions and expected answers): {{activityContent}}
Student answers: {{studentSubmission}}

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate each answer and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<1-2 sentence warm feedback>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<specific strengths>"],
  "mainMistakes": ["<key errors>"],
  "grammarIssues": [],
  "vocabularyIssues": [],
  "toneIssues": [],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": ["<useful phrase from the audio>"],
  "miniLesson": "<one sentence about listening strategy>",
  "nextImprovementStep": null,
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<recommendation>",
  "feedbackInSourceLanguage": "<1-2 sentences in {{sourceLanguageName}}>",
  "questionFeedback": [
    {
      "questionId": "<id>",
      "question": "<the question>",
      "studentAnswer": "<student's answer>",
      "expectedAnswerSummary": "<expected answer>",
      "isCorrect": <true|false>,
      "score": <0.0-1.0>,
      "feedback": "<specific feedback on this answer>"
    }
  ],
  "transcript": "<the audio transcript>",
  "responseFeedback": "<feedback on the written follow-up response if provided>"
}
""";

    private const string ActivityEvaluateListenAndGapFillContent = """
You are an English language teacher evaluating a listening gap-fill exercise.

Student level: {{cefrLevel}}
Activity content: {{activityContent}}
Student answers: {{studentSubmission}}

Check each gap answer and return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<1-2 sentence warm feedback>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<specific correct answers>"],
  "mainMistakes": ["<key errors>"],
  "grammarIssues": [],
  "vocabularyIssues": ["<vocabulary notes>"],
  "toneIssues": [],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": ["<word/phrase from the gaps>"],
  "miniLesson": "<one sentence about the target vocabulary>",
  "nextImprovementStep": null,
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<recommendation>",
  "feedbackInSourceLanguage": "<1-2 sentences in {{sourceLanguageName}}>",
  "transcript": "<the full audio script>"
}
""";

    private const string ActivityEvaluateEmailReplyContent = """
You are a warm, professional English writing coach evaluating a workplace email reply written by a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content:
{{activityContent}}

If activityContent uses schemaVersion module_stage_v1, evaluate the submission against practiceContent.exerciseData, especially prompt, situation, audience, tone, and expectedLength. Use feedbackPlan as the rubric. Use learnContent only as teaching context for coaching.

Student's reply (JSON with "subject" and "body" fields):
{{studentSubmission}}

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate both the subject line and the body of the reply (subject clarity/relevance, body grammar, vocabulary, tone, structure, and whether it fully addresses the situation). Return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "correctedText": "<a corrected, professional version of their reply>",
  "coachSummary": "<2-3 sentence warm, specific feedback>",
  "focusFirst": <true if many issues — limit to top 3-5>,
  "changes": [
    { "type": "replace|add|remove", "original": "<original text>", "suggested": "<improved text>", "reason": "<why>", "category": "grammar|vocabulary|tone|clarity|structure", "severity": "high|medium|low" }
  ],
  "whatYouDidWell": ["<specific strength>"],
  "mainMistakes": ["<key mistake>"],
  "grammarIssues": ["<grammar issue>"],
  "vocabularyIssues": ["<vocabulary issue>"],
  "toneIssues": ["<tone issue>"],
  "clarityIssues": ["<clarity issue>"],
  "grammarExplanation": "<1-2 sentence grammar explanation>",
  "toneExplanation": "<1-2 sentence tone explanation>",
  "vocabularyToRemember": ["<phrase worth memorising>"],
  "miniLesson": "<concise teaching moment>",
  "nextImprovementStep": "<actionable rewrite instruction>",
  "rewriteChallenge": "<rewrite challenge for the student>",
  "nextPracticeSuggestion": "<recommendation for next practice>",
  "feedbackInSourceLanguage": "<2-3 sentences in {{sourceLanguageName}}>"
}
""";

    private const string ActivityEvaluateTeamsChatContent = """
You are an English language coach evaluating a professional chat reply written by a {{sourceLanguageName}}-speaking student.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (includes "learningGoal" — the communication goal this exercise is testing):
{{activityContent}}

Student's reply:
{{studentSubmission}}

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate the chat reply against the activity's "learningGoal" as well as tone, conciseness, and professional register:
- Did the reply address the goal stated in "learningGoal" (e.g. asking for clarification, giving a status update, responding politely)?
- Was the tone appropriate (not over-apologising, not too casual or too formal)?
- Was the message clear and easy to understand?
- If clarification was relevant to the goal, did the student ask a useful clarifying question?

Return ONLY valid JSON matching the same schema as email evaluation:

{
  "overallScore": <0-100>,
  "correctedText": "<an improved version of their chat reply>",
  "coachSummary": "<1-2 sentence feedback that references whether the goal was reached>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<strength>"],
  "mainMistakes": ["<key mistake, including if the goal was not addressed>"],
  "grammarIssues": [],
  "vocabularyIssues": [],
  "toneIssues": ["<any tone issues — overly formal / too casual / over-apologising>"],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": "<1-2 sentences on professional chat register>",
  "vocabularyToRemember": ["<phrase>"],
  "miniLesson": "<one sentence teaching moment on chat communication>",
  "nextImprovementStep": "<actionable suggestion>",
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<recommendation>",
  "feedbackInSourceLanguage": "<1-2 sentences in {{sourceLanguageName}}>"
}
""";

    private const string ActivityEvaluateSpokenResponseContent = """
You are an English speaking coach evaluating a spoken workplace response from a {{sourceLanguageName}}-speaking student.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (prompt and expected points):
{{activityContent}}

Student's transcribed response:
{{studentSubmission}}

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate clarity, content coverage, and professional register. Return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentence warm feedback on the spoken response>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<specific strength in the spoken response>"],
  "mainMistakes": ["<key area to improve>"],
  "grammarIssues": ["<grammar notes if applicable>"],
  "vocabularyIssues": [],
  "toneIssues": [],
  "clarityIssues": ["<clarity issues if any>"],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": ["<phrase from the expected points worth memorising>"],
  "miniLesson": "<one sentence tip for spoken workplace communication>",
  "nextImprovementStep": "<one sentence suggestion for next spoken practice>",
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<recommendation>",
  "feedbackInSourceLanguage": "<2-3 sentences in {{sourceLanguageName}}>",
  "speakingStrengths": ["<strength 1>", "<strength 2>"],
  "speakingImprovements": ["<improvement area 1>"],
  "missingExpectedPoints": ["<any expected point not covered>"],
  "suggestedImprovedResponse": "<a model spoken response in written form>"
}
""";

    private const string ActivityEvaluateSpeakingRoleplayTurnContent = """
You are an English speaking coach evaluating a recorded workplace roleplay turn from a {{sourceLanguageName}}-speaking student.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (scenario, prompt, and expected points):
{{activityContent}}

Student's transcribed spoken response:
{{studentSubmission}}

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate clarity, content coverage, and professional register. Return ONLY valid JSON:

{
  "overallScore": <0-100>,
  "coachSummary": "<2-3 sentence warm feedback on the spoken response>",
  "strengths": ["<strength 1>", "<strength 2>"],
  "improvements": ["<improvement area 1>", "<improvement area 2>"],
  "missingExpectedPoints": ["<any expected point not covered>"],
  "suggestedImprovedResponse": "<a model spoken response in written form>",
  "miniLesson": "<one sentence tip for spoken workplace communication>",
  "nextImprovementStep": "<one sentence suggestion for next spoken practice>"
}

Rules:
- overallScore must be a number between 0 and 100.
- strengths must include at least one genuine positive observation.
- improvements and missingExpectedPoints may be empty arrays if there are none.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateOpenWritingTaskContent = """
You are a warm, professional English writing coach evaluating an open-ended workplace writing task from a {{sourceLanguageName}}-speaking student learning {{targetLanguageName}}.

Student level: {{cefrLevel}}
Career context: {{careerContext}}

Activity content (situation and prompt):
{{activityContent}}

Student's submitted writing:
---
{{studentSubmission}}
---

Student's current progress on the skill this exercise targets: {{studentSkillContext}}
Use this to make coachSummary specific and encouraging — do not repeat it verbatim.

Evaluate clarity, grammar, vocabulary, and professional tone. Return ONLY valid JSON:

{
  "overallScore": <number 0-100>,
  "coachSummary": "<1-2 warm sentences summarising the overall quality and the most important thing to improve>",
  "strengths": ["<specific genuine strength 1>", "<specific genuine strength 2>"],
  "improvements": ["<specific improvement area 1>", "<specific improvement area 2>"],
  "missingExpectedPoints": ["<any part of the prompt the student did not address>"],
  "suggestedImprovedResponse": "<a suggested improved version of the student's writing — label this as a suggestion, not the answer>",
  "miniLesson": "<1-2 sentences teaching the single most important rule illustrated by this submission>",
  "nextImprovementStep": "<one actionable sentence telling the student exactly what to try next time>"
}

Rules:
- overallScore must be a number between 0 and 100.
- strengths must include at least one genuine positive observation.
- improvements and missingExpectedPoints may be empty arrays if there are none.
- Do not include any text outside the JSON object.
""";

    private const string ActivityEvaluateLessonReflectionContent = """
You are an English language teacher acknowledging a student's lesson reflection.

Student level: {{cefrLevel}}

Activity content:
{{activityContent}}

If activityContent uses schemaVersion module_stage_v1, evaluate the submission against practiceContent.exerciseData, especially prompt, situation, audience, tone, and expectedLength. Use feedbackPlan as the rubric. Use learnContent only as teaching context for coaching.

Student's reflection:
{{studentSubmission}}

Respond warmly and return ONLY valid JSON:

{
  "overallScore": 100,
  "coachSummary": "<2-3 sentence warm, encouraging acknowledgement of their reflection>",
  "focusFirst": false,
  "changes": [],
  "whatYouDidWell": ["<something specific from their reflection worth noting>"],
  "mainMistakes": [],
  "grammarIssues": [],
  "vocabularyIssues": [],
  "toneIssues": [],
  "clarityIssues": [],
  "grammarExplanation": null,
  "toneExplanation": null,
  "vocabularyToRemember": [],
  "miniLesson": null,
  "nextImprovementStep": null,
  "rewriteChallenge": null,
  "nextPracticeSuggestion": "<one sentence encouragement for the next session>",
  "feedbackInSourceLanguage": "<2-3 sentences in {{sourceLanguageName}} praising their reflection>"
}
""";

    private const string LessonBatchPlanContent = """
You are an English course planner for SpeakPath. Plan the next batch of guided lesson sessions for one professional learner.

Compact learner summary:
{{summary}}

Generate exactly {{sessionCount}} progressive lesson session plans. Return ONLY valid JSON (no markdown) as a single array:

[
  {
    "title": "<short session title, 4-8 words>",
    "topic": "<the workplace topic for this session>",
    "sessionGoal": "<one sentence describing what the learner will be able to do after this session>",
    "focusSkill": "<one of: listening | speaking | writing | vocabulary>",
    "durationMinutes": <10 | 15 | 20 | 30>,
    "exercises": [
      {
        "exercisePatternKey": "<a valid exercise pattern key, e.g. listen_and_answer, phrase_match, email_reply, spoken_response_from_prompt>",
        "primarySkill": "<listening | speaking | writing | vocabulary>",
        "instructions": "<student-facing instructions for this step>",
        "estimatedMinutes": <positive integer>
      }
    ]
  }
]

Rules:
- Return exactly {{sessionCount}} session plans in the array.
- Each session must have 3-5 exercises.
- Every exercise must use one of these valid exercisePatternKey values only:
  phrase_match, gap_fill_workplace_phrase, listen_and_answer, listen_and_gap_fill, email_reply, teams_chat_simulation, spoken_response_from_prompt, lesson_reflection.
- Use patterns as practice formats. Do not use "writing" as a generic fallback for every skill.
- For vocabulary-focused lessons, use phrase_match or gap_fill_workplace_phrase.
- For listening-focused lessons, use listen_and_answer or listen_and_gap_fill.
- For speaking-focused lessons, use spoken_response_from_prompt.
- For writing-focused lessons, use email_reply or teams_chat_simulation.
- Include at least one explicit teaching/practice step before the final review.
- Prefer ending each session with lesson_reflection.
- Do not repeat scenarios listed in avoidRepeating.
- Match difficulty to the learner's studentLevel and domainComplexity.
- Address the recurringIssues and nextFocusRecommendation from the summary.
- Do not include any text outside the JSON array.

CEFR-aware pattern selection guidance:
| Level | Prefer                                     | Avoid or limit                         |
|-------|--------------------------------------------|----------------------------------------|
| A1    | phrase_match, gap_fill_workplace_phrase    | Multi-step writing tasks               |
| A2    | gap_fill_workplace_phrase, listen_and_answer | Long email tasks without scaffolding |
| B1    | email_reply, listen_and_answer, spoken_response_from_prompt | All-vocabulary sessions |
| B2    | teams_chat_simulation, spoken_response_from_prompt, email_reply | Over-scaffolded A2-style tasks |
Use the learner's studentLevel from the summary to select patterns that are appropriately challenging.
""";

    // Phase J2a — AI-assisted "Generate Learn" composer. A deliberately separate action from the
    // deterministic Lesson composer (see IGenerateLessonFromResourcesWithAiHandler's doc comment);
    // this prompt only generates teaching prose — metadata (CEFR/skill/subskill/tags/difficulty)
    // stays deterministic from the selected resources/request.
    private const string LessonGenerateFromResourcesContent = """
You are an expert English teacher writing a short teaching/explanation block for a workplace English learning platform.

Selected source material (from the platform's own reviewed Resource Bank — teach from this, do not invent unrelated content):
{{resourcesSummary}}

Student level: {{cefrLevel}}
Skill: {{skill}}
Subskill: {{subskill}}
Context tags: {{contextTags}}
Focus tags: {{focusTags}}
Admin notes: {{notes}}

Write a clear teaching explanation of the source material above, suitable for a student at {{cefrLevel}}. Return ONLY valid JSON (no markdown, no text outside the JSON object):

{
  "title": "<short teaching title, 4-10 words>",
  "body": "<2-5 short paragraphs teaching the concept clearly, appropriate for {{cefrLevel}}>",
  "examples": ["<realistic example sentence or usage 1>", "<example 2>", "<example 3>"],
  "commonMistakes": ["<a common mistake learners at this level make with this material>"],
  "usageNotes": "<optional 1 sentence of extra usage guidance, or empty string>"
}

Rules:
- Teach only the selected source material above — do not introduce unrelated grammar points, vocabulary, or topics.
- body must be genuine teaching prose (explain the concept), not a restatement of the source material's raw fields.
- examples must be complete, realistic sentences a learner could actually use or encounter — not single isolated words unless the source material is itself a single word, in which case show it in a full sentence.
- commonMistakes may be an empty array if there is no well-known common mistake for this specific material.
- Keep vocabulary and sentence complexity appropriate for {{cefrLevel}} (see the CEFR calibration table below).
- Do not include real company names, real person names, phone numbers, or sensitive content.
- Do not include any text outside the JSON object. No markdown fences.

CEFR calibration for teaching prose:
| Level | Sentence complexity           | Body length        |
|-------|--------------------------------|---------------------|
| A1    | Very short, simple sentences   | 2 short paragraphs  |
| A2    | Short sentences, common words  | 2-3 short paragraphs|
| B1    | Everyday connected sentences   | 3-4 paragraphs      |
| B2    | More nuanced, some complexity  | 3-5 paragraphs      |
""";

    // Phase J2b — AI-assisted "Generate Activity" composer. AI supplies only framing content
    // (gap-fill sentence / multiple-choice distractors / comprehension question) — the correct
    // answer, scoring rule, and answer key always stay deterministic (see
    // IGenerateActivityFromResourcesWithAiHandler's doc comment).
    private const string ExerciseGenerateFromResourcesContent = """
You are an expert English exercise writer creating ONE practice item for a workplace English learning platform.

Resource being practiced: {{resourceTitle}} ({{resourceType}})
Definition/description: {{resourceDefinition}}
Requested exercise type: {{activityType}}
Student level: {{cefrLevel}}
Skill: {{skill}}
Admin notes: {{notes}}

Return ONLY valid JSON (no markdown, no text outside the JSON object):

{
  "promptText": "<see rules below for this exercise type>",
  "correctAnswerText": "<see rules below — only used by reading_multiple_choice_single>",
  "distractors": ["<see rules below>"]
}

Rules by exercise type:

If the requested exercise type is "gap_fill":
- promptText must be exactly one natural, realistic sentence that uses "{{resourceTitle}}" in context, with "{{resourceTitle}}" itself replaced by the blank marker "___" (three underscores).
- promptText must NOT contain the word or phrase "{{resourceTitle}}" anywhere else in the sentence — only the "___" marker.
- correctAnswerText may be left as an empty string "" — it is not used for this exercise type.
- distractors must be an empty array [].

If the requested exercise type is "multiple_choice_single":
- promptText may be left as an empty string "" — it is not used for this exercise type.
- correctAnswerText may be left as an empty string "" — it is not used for this exercise type (the correct answer always comes from the resource's own definition, never from you).
- distractors must contain exactly 3 short, plausible-but-clearly-INCORRECT alternative definitions for "{{resourceTitle}}", written in the same style and length as the real definition above.
- distractors must NOT be synonyms, paraphrases, or partial restatements of the real definition above — each must describe a genuinely different meaning.
- Never include the real definition, or anything close to it, as one of the distractors.

If the requested exercise type is "short_answer":
- promptText must be one specific comprehension question about the passage/excerpt content above, answerable in 1-2 sentences.
- promptText must reference something specific from the excerpt, not a generic question that could apply to any passage.
- correctAnswerText may be left as an empty string "" — it is not used for this exercise type (open-ended, not deterministically scored).
- distractors must be an empty array [].

If the requested exercise type is "reading_multiple_choice_single":
- promptText must be one specific comprehension question about the passage/excerpt content above, answerable by picking a single option — not a generic question that could apply to any passage.
- correctAnswerText must be the single, unambiguous correct answer to that question, based only on what the excerpt/passage above actually says — never invent details not present in the text.
- distractors must contain exactly 3 short, plausible-but-clearly-INCORRECT answers to the same question, similar in length and style to correctAnswerText, that a careless reader might mistakenly pick — but that are definitively wrong based on the text above.
- distractors must NOT be synonyms, paraphrases, or partial restatements of correctAnswerText.

General rules:
- Keep language appropriate for {{cefrLevel}}.
- Do not include real company names, real person names, phone numbers, or sensitive content.
- Do not include any text outside the JSON object. No markdown fences.
""";

    // Phase K8 — one shared prompt for the admin "Fix with AI" repair action across Resource
    // Bank/Lesson/Exercise/Module. Deliberately generic and single-purpose: given a description
    // of what's missing plus grounding context, write ONLY that one field's text. Never used for
    // correctness-critical data (answer keys, scoring rules, schemas) — callers only ask for
    // descriptive/explanatory text.
    private const string AdminContentRepairFieldContent = """
You are helping an admin fill in one missing piece of content on a workplace English learning
platform.

Content kind: {{entityKind}}
Missing field: {{fieldLabel}}
Known context (use this, do not invent unrelated details): {{context}}

Write ONLY the missing field's text described above, grounded in the known context. Keep it
concise and in plain English appropriate for the stated CEFR level if one is given.

Return ONLY valid JSON (no markdown, no text outside the JSON object):

{
  "value": "<the missing field's text>"
}

Rules:
- "value" must be a single non-empty string, no markdown formatting, no quotes-within-quotes.
- Do not include real company names, real person names, phone numbers, or sensitive content.
- Do not include any text outside the JSON object. No markdown fences.
""";

    // Phase J2c — AI-assisted "Generate Module" composer, "from resource" entry point only. AI
    // supplies only the module's own descriptive framing — no answer key/scoring at this level.
    private const string ModuleGenerateFromResourceContent = """
You are writing the descriptive framing for a Module on a workplace English learning platform. A
Module combines an existing, already-approved Lesson (teaching content) with an existing,
already-approved Exercise (practice task) — you are NOT creating either of those; you are only
writing the module's own title, description, and student-facing completion feedback.

The Lesson (already written, already approved):
Title: {{lessonTitle}}
Content: {{lessonBody}}

The Exercise (already written, already approved):
Title: {{exerciseTitle}}
Instructions: {{exerciseInstructions}}
Type: {{activityType}}

Student level: {{cefrLevel}}
Skill: {{skill}}
Admin notes: {{notes}}

Return ONLY valid JSON (no markdown, no text outside the JSON object):

{
  "title": "<short module title, 4-10 words, describing what the student will learn and practice>",
  "description": "<1-2 sentences describing what this module helps the student practice, referencing the actual Lesson and Exercise content above>",
  "feedbackPlan": {
    "completionMessage": "<one warm, specific sentence shown to the student when they finish this module>",
    "evaluationCriteria": ["<criterion 1>", "<criterion 2>"],
    "feedbackFocus": "<one sentence: what post-completion feedback for this module should focus on>"
  }
}

Rules:
- Reference the actual Lesson/Exercise content above — do not write generic filler that could apply to any module.
- Do not invent new teaching content, new practice tasks, or a new correct answer — you are only writing framing/coaching copy around the existing Lesson and Exercise.
- Do not include real company names, real person names, phone numbers, or sensitive content.
- Do not include any text outside the JSON object. No markdown fences.
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

    private const string ActivityTemplateGenerateInstanceContent = """
You are personalizing ONE reusable activity template into a specific instance for a language learner.

Skill: {{skill}}
Subskill: {{subskill}}
Student level: {{cefrLevel}}
Activity type: {{activityType}}
Topic hint: {{topicHint}}
Learner preferences: {{learnerPreferences}}

Author-written generation instructions for this template (follow these closely):
{{generationInstructions}}

The template's base Form.io schema (student-safe — contains no correct answers or scoring data). Personalize the wording/scenario/content of this schema for the topic hint and level above, but you MUST preserve every component's "key" and "type" exactly as given — only change label/content/text-like properties:
{{baseSchema}}

Return ONLY a valid Form.io schema JSON object, in the exact same shape as the base schema above (same top-level "display"/"components" structure, same component keys and types). No markdown. No text outside JSON. Never include a correct answer, "score", "rubric", or any scoring-related property anywhere in the output — this schema is shown directly to the student.
""";

    // Phase E2 — advisory classification of one staged ResourceCandidate. This output is never
    // trusted to decide ValidationStatus by itself (ResourceCandidateValidationService does that,
    // deterministically) — it only suggests values for the candidate's classification fields.
    private const string ResourceCandidateAnalyzeContent = """
You are classifying ONE staged English-language learning resource candidate for an internal content pipeline. Your output is advisory only — a separate deterministic system decides whether this candidate is actually approved, not you.

Candidate type (as already inferred by the import pipeline, do not change it): {{candidateType}}
Candidate language code: {{languageCode}}
Source name: {{sourceName}}
Source license: {{sourceLicense}}

Canonical text:
{{canonicalText}}

Normalized data (as imported):
{{normalizedJson}}

Additional raw context (may be empty):
{{rawContext}}

Return ONLY valid JSON (no markdown, no text outside the JSON object) matching this exact shape:

{
  "candidateType": "{{candidateType}}",
  "languageCode": "en",
  "cefrLevel": "<one of A1, A2, B1, B2, C1, C2, or null if you cannot judge confidently>",
  "cefrConfidence": <number 0-1: how confident you are in cefrLevel>,
  "primarySkill": "<one of writing, reading, listening, speaking, vocabulary, grammar, pronunciation, fluency, confidence, or null>",
  "subskill": "<a matching subskill for primarySkill using the pattern '<skill>.<name>', e.g. 'reading.gist', or null>",
  "difficultyBand": <integer 1-5 within the CEFR level, 1=easiest, or null>,
  "contextTags": ["<short real-life context tag, e.g. 'workplace_email'>"],
  "focusTags": ["<short focus tag, e.g. 'polite_requests'>"],
  "grammarTags": ["<grammar point this content illustrates, if any>"],
  "vocabularyTags": ["<notable vocabulary theme, if any>"],
  "pronunciationTags": ["<pronunciation feature, only if candidateType relates to speaking/listening>"],
  "activitySuitabilityTags": ["<short label for what kind of exercise this content could support, e.g. 'gap_fill', 'reading_comprehension'>"],
  "safetyTags": ["<short label ONLY if the content is unsafe/inappropriate for a learning platform — otherwise leave this empty>"],
  "qualityScore": <number 0-1: overall content quality/usefulness for language learning>,
  "needsHumanReview": <true if you are unsure about classification or quality, otherwise false>,
  "qualityIssues": ["<short description of any quality problem you noticed, e.g. 'text is too short to classify reliably'>"],
  "suggestedActivityUses": ["<short suggestion for how this content could be used in an exercise>"],
  "searchText": "<a short lowercase space-separated string of useful search keywords for this candidate>"
}

Rules:
- Never invent a correct answer, score, or scoring rubric for this content — this is a content classification pass, not an exercise author.
- If you cannot confidently classify a field, use null (or an empty array for tag lists) rather than guessing.
- safetyTags must stay empty unless the content is genuinely unsafe/inappropriate — do not flag ordinary workplace or everyday content.
- Do not include any text outside the JSON object. No markdown fences.
""";

    // Phase K1 — proposes a column-rename mapping for an admin's uploaded/pasted import file.
    // Advisory only: the admin always reviews/confirms before it's applied, and the caller drops
    // any suggested field name that isn't in the recognized set — this prompt cannot change what a
    // "recognized field" means, it can only suggest which of the file's own columns matches one.
    private const string ResourceImportProposeColumnMappingContent = """
You are proposing a column-rename mapping for an admin importing English-language learning content. You do NOT decide anything final — an admin reviews and confirms your suggestion before it is ever applied.

The file's column names: {{columns}}

The recognized field names this import pipeline understands: {{recognizedFields}}

A small sample of the file's rows (bounded, may not include every column):
{{sampleRowsJson}}

For EACH of the file's own column names listed above, suggest which recognized field name (if any) it most likely corresponds to, based on the column name itself and the sample data. Only suggest a field from the recognized list — never invent a new field name. If a column doesn't clearly correspond to any recognized field (e.g. it's metadata like "CoreInventory" or "Threshold" that this pipeline doesn't use), set its field to null.

Return ONLY valid JSON (no markdown, no text outside the JSON object) matching this exact shape:

{
  "mapping": {
    "<exact column name as given>": { "field": "<one of the recognized field names, or null>", "confidence": <number 0-1> }
  }
}

Rules:
- Include every one of the file's columns as a key in "mapping", even if its field is null.
- Never suggest a field name that isn't in the recognized field list given above.
- Prefer null over a low-confidence guess — an admin can always map it manually.
- Do not include any text outside the JSON object. No markdown fences.
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

        // TTS provider configs — default to fake so CI never calls OpenAI.
        // Admin switches to openai/tts-1/onyx via the AI Config UI.
        await SeedTtsProviderConfigAsync(db, logger, "tts.listening", ct);
        await SeedTtsProviderConfigAsync(db, logger, "tts.placement", ct);

        // Activity generation prompt
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateWritingKey, ActivityGenerateWritingContent,
            maxInputTokens: 1600, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListeningKey, ActivityGenerateListeningContent,
            maxInputTokens: 1600, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSpeakingRolePlayKey, ActivityGenerateSpeakingRolePlayContent,
            maxInputTokens: 1900, maxOutputTokens: 2000, ct);

        // AI Bank-First Teaching Architecture Phase 5 — ActivityTemplate instance generation
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityTemplateGenerateInstanceKey, ActivityTemplateGenerateInstanceContent,
            maxInputTokens: 2400, maxOutputTokens: 2200, ct);

        // Phase E2 — resource candidate AI analysis (advisory classification)
        await SeedOrUpgradePromptAsync(db, logger,
            ResourceCandidateAnalyzeKey, ResourceCandidateAnalyzeContent,
            maxInputTokens: 3200, maxOutputTokens: 1400, ct);

        // Phase K1 — AI-assisted import column-mapping proposal (advisory only)
        await SeedOrUpgradePromptAsync(db, logger,
            ResourceImportProposeColumnMappingKey, ResourceImportProposeColumnMappingContent,
            maxInputTokens: 1600, maxOutputTokens: 1200, ct);

        // Phase K8 — admin "Fix with AI" repair action, shared across Resource Bank/Lesson/
        // Exercise/Module.
        await SeedOrUpgradePromptAsync(db, logger,
            AdminContentRepairFieldKey, AdminContentRepairFieldContent,
            maxInputTokens: 1200, maxOutputTokens: 500, ct);

        // Activity evaluation prompts
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateWritingKey, ActivityEvaluateWritingContent,
            maxInputTokens: 2000, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSpeakingRolePlayKey, ActivityEvaluateSpeakingRolePlayContent,
            maxInputTokens: 1500, maxOutputTokens: 1200, ct);

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

        // Lesson buffer — batch lesson plan generation prompt (T13).
        await SeedOrUpgradePromptAsync(db, logger,
            LessonBatchPlanKey, LessonBatchPlanContent,
            maxInputTokens: 1500, maxOutputTokens: 2500, ct);

        // Phase J2a — AI-assisted "Generate Learn" composer.
        await SeedOrUpgradePromptAsync(db, logger,
            LessonGenerateFromResourcesKey, LessonGenerateFromResourcesContent,
            maxInputTokens: 2200, maxOutputTokens: 1600, ct);

        // Phase J2b — AI-assisted "Generate Activity" composer.
        await SeedOrUpgradePromptAsync(db, logger,
            ExerciseGenerateFromResourcesKey, ExerciseGenerateFromResourcesContent,
            maxInputTokens: 1200, maxOutputTokens: 700, ct);

        // Phase J2c — AI-assisted "Generate Module" composer.
        await SeedOrUpgradePromptAsync(db, logger,
            ModuleGenerateFromResourceKey, ModuleGenerateFromResourceContent,
            maxInputTokens: 2200, maxOutputTokens: 700, ct);

        // Exercise Pattern Engine — pattern-specific generation prompts
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGeneratePhraseMatchKey, ActivityGeneratePhraseMatchContent,
            maxInputTokens: 800, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateGapFillKey, ActivityGenerateGapFillContent,
            maxInputTokens: 900, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListenAndAnswerKey, ActivityGenerateListenAndAnswerContent,
            maxInputTokens: 1000, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListenAndGapFillKey, ActivityGenerateListenAndGapFillContent,
            maxInputTokens: 1000, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateEmailReplyKey, ActivityGenerateEmailReplyContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateTeamsChatKey, ActivityGenerateTeamsChatContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSpokenResponseKey, ActivityGenerateSpokenResponseContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateLessonReflectionKey, ActivityGenerateLessonReflectionContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateOpenWritingTaskKey, ActivityGenerateOpenWritingTaskContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSpeakingRoleplayTurnKey, ActivityGenerateSpeakingRoleplayTurnContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReadingMultipleChoiceSingleKey, ActivityGenerateReadingMultipleChoiceSingleContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListeningMultipleChoiceSingleKey, ActivityGenerateListeningMultipleChoiceSingleContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListeningMultipleChoiceMultiKey, ActivityGenerateListeningMultipleChoiceMultiContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateListeningFillInBlanksKey, ActivityGenerateListeningFillInBlanksContent,
            maxInputTokens: 1500, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSelectMissingWordKey, ActivityGenerateSelectMissingWordContent,
            maxInputTokens: 1400, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateHighlightCorrectSummaryKey, ActivityGenerateHighlightCorrectSummaryContent,
            maxInputTokens: 1400, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateHighlightIncorrectWordsKey, ActivityGenerateHighlightIncorrectWordsContent,
            maxInputTokens: 1400, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateWriteFromDictationKey, ActivityGenerateWriteFromDictationContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReadingMultipleChoiceMultiKey, ActivityGenerateReadingMultipleChoiceMultiContent,
            maxInputTokens: 1400, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReadingFillInBlanksKey, ActivityGenerateReadingFillInBlanksContent,
            maxInputTokens: 1400, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReorderParagraphsKey, ActivityGenerateReorderParagraphsContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReadingWritingFillInBlanksKey, ActivityGenerateReadingWritingFillInBlanksContent,
            maxInputTokens: 1500, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSummarizeWrittenTextKey, ActivityGenerateSummarizeWrittenTextContent,
            maxInputTokens: 1500, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateWriteEssayKey, ActivityGenerateWriteEssayContent,
            maxInputTokens: 1300, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSummarizeSpokenTextKey, ActivityGenerateSummarizeSpokenTextContent,
            maxInputTokens: 1600, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateAnswerShortQuestionKey, ActivityGenerateAnswerShortQuestionContent,
            maxInputTokens: 1800, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateAnswerShortQuestionKey, ActivityEvaluateAnswerShortQuestionContent,
            maxInputTokens: 2000, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateRepeatSentenceKey, ActivityGenerateRepeatSentenceContent,
            maxInputTokens: 2000, maxOutputTokens: 2200, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateRepeatSentenceKey, ActivityEvaluateRepeatSentenceContent,
            maxInputTokens: 1800, maxOutputTokens: 1000, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateRespondToSituationKey, ActivityGenerateRespondToSituationContent,
            maxInputTokens: 2100, maxOutputTokens: 2400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateRespondToSituationKey, ActivityEvaluateRespondToSituationContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateDescribeImageKey, ActivityGenerateDescribeImageContent,
            maxInputTokens: 2000, maxOutputTokens: 2400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateDescribeImageKey, ActivityEvaluateDescribeImageContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateRetellLectureKey, ActivityGenerateRetellLectureContent,
            maxInputTokens: 2000, maxOutputTokens: 2400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateRetellLectureKey, ActivityEvaluateRetellLectureContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateSummarizeGroupDiscussionKey, ActivityGenerateSummarizeGroupDiscussionContent,
            maxInputTokens: 2400, maxOutputTokens: 2800, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSummarizeGroupDiscussionKey, ActivityEvaluateSummarizeGroupDiscussionContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityGenerateReadAloudKey, ActivityGenerateReadAloudContent,
            maxInputTokens: 1200, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateReadAloudKey, ActivityEvaluateReadAloudContent,
            maxInputTokens: 1800, maxOutputTokens: 1000, ct);

        // Exercise Pattern Engine — pattern-specific evaluation prompts
        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluatePhraseMatchKey, ActivityEvaluatePhraseMatchContent,
            maxInputTokens: 1000, maxOutputTokens: 800, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateGapFillKey, ActivityEvaluateGapFillContent,
            maxInputTokens: 1000, maxOutputTokens: 800, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateListenAndAnswerKey, ActivityEvaluateListenAndAnswerContent,
            maxInputTokens: 1200, maxOutputTokens: 1000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateListenAndGapFillKey, ActivityEvaluateListenAndGapFillContent,
            maxInputTokens: 1000, maxOutputTokens: 800, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateEmailReplyKey, ActivityEvaluateEmailReplyContent,
            maxInputTokens: 2000, maxOutputTokens: 2000, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateTeamsChatKey, ActivityEvaluateTeamsChatContent,
            maxInputTokens: 1500, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSpokenResponseKey, ActivityEvaluateSpokenResponseContent,
            maxInputTokens: 1500, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateLessonReflectionKey, ActivityEvaluateLessonReflectionContent,
            maxInputTokens: 800, maxOutputTokens: 600, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateOpenWritingTaskKey, ActivityEvaluateOpenWritingTaskContent,
            maxInputTokens: 2000, maxOutputTokens: 1500, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSpeakingRoleplayTurnKey, ActivityEvaluateSpeakingRoleplayTurnContent,
            maxInputTokens: 1500, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSummarizeWrittenTextKey, ActivityEvaluateSummarizeWrittenTextContent,
            maxInputTokens: 2000, maxOutputTokens: 1200, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateWriteEssayKey, ActivityEvaluateWriteEssayContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);

        await SeedOrUpgradePromptAsync(db, logger,
            ActivityEvaluateSummarizeSpokenTextKey, ActivityEvaluateSummarizeSpokenTextContent,
            maxInputTokens: 2000, maxOutputTokens: 1400, ct);

        // AI Config Categories — category-level provider routing.
        // llm.default acts as the catch-all for all LLM features.
        // TTS categories are independent — must be explicitly configured by admin.
        await SeedAiConfigCategoryAsync(db, logger, "llm.default",        "Default LLM",              "fake", "fake",  null, ct);
        await SeedAiConfigCategoryAsync(db, logger, "llm.generation",     "Content Generation",       null,   null,    null, ct);
        await SeedAiConfigCategoryAsync(db, logger, "llm.evaluation",     "Evaluation & Feedback",    null,   null,    null, ct);
        await SeedAiConfigCategoryAsync(db, logger, "llm.memory",         "Memory & Learning Path",   null,   null,    null, ct);
        await SeedAiConfigCategoryAsync(db, logger, "tts.listening",      "Listening TTS",            "fake", "fake",  null, ct);
        await SeedAiConfigCategoryAsync(db, logger, "tts.placement",      "Placement TTS",            "fake", "fake",  null, ct);

        // Lesson generation settings — single-row install-wide defaults (T9).
        var hasSettings = await db.LessonGenerationSettings.AnyAsync(ct);
        if (!hasSettings)
        {
            db.LessonGenerationSettings.Add(new LessonGenerationSettings());
            logger.LogInformation("Seeded default LessonGenerationSettings (buffer=5, threshold=1, batch=4).");
        }

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

        if (activePrompt is not null
            && ComputeHash(activePrompt.Content) == contentHash
            && activePrompt.MaxInputTokens == maxInputTokens
            && activePrompt.MaxOutputTokens == maxOutputTokens)
            return; // Already up to date

        // Deactivate old active version if content or token budget changed
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

    private static async Task SeedTtsProviderConfigAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        string featureKey,
        CancellationToken ct)
    {
        var exists = await db.AiProviderConfigs.AnyAsync(c => c.FeatureKey == featureKey, ct);
        if (exists) return;

        db.AiProviderConfigs.Add(new AiProviderConfig(featureKey, "fake", "fake", "fake"));
        logger.LogInformation("Seeded TTS provider config for {FeatureKey}: fake/fake/fake.", featureKey);
    }

    private static async Task SeedAiConfigCategoryAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        string categoryKey,
        string displayName,
        string? providerName,
        string? modelName,
        string? voiceName,
        CancellationToken ct)
    {
        var exists = await db.AiConfigCategories.AnyAsync(c => c.CategoryKey == categoryKey, ct);
        if (exists) return;

        db.AiConfigCategories.Add(new AiConfigCategory(categoryKey, displayName, providerName, modelName, voiceName));
        logger.LogInformation(
            "Seeded AI config category {CategoryKey} ({DisplayName}): provider={Provider} model={Model}.",
            categoryKey, displayName, providerName ?? "(inherit)", modelName ?? "(inherit)");
    }
}

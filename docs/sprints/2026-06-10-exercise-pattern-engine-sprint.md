---
status: current
lastUpdated: 2026-06-10 11:02
owner: engineering
supersedes:
supersededBy:
---

# Sprint: Exercise Pattern Engine

**Date:** 2026-06-10
**Sprint name:** Exercise Pattern Engine
**Status:** Complete
**Preceding sprint:** Today's Lesson / Learning Session (complete)
**Product goal:** Replace broad ActivityType-based generation with named, schema-controlled ExercisePattern records so every lesson step has a precise, reusable interaction format with a known frontend renderer and AI content contract.

---

## Problem Statement

Today's Lesson works end-to-end, but the exercise content is thin. The session generator assigns `ExerciseKind` slots (VocabularyWarmup, ListeningInput, WritingTask…) and then maps them to one of six broad `ActivityType` values. The AI receives only the `ActivityType` and a topic hint — it has no instruction on interaction format, answer schema, or marking mode. As a result:

- Every WritingTask produces a generic open-ended writing prompt regardless of whether it should be a gap-fill, an email reply, a sentence rewrite, or a chat simulation.
- Every VocabularyWarmup produces a card list, even when a matching-pairs interaction would be far more engaging.
- The frontend cannot render different exercise formats — it just shows a text box.
- There is no AI content contract: the JSON shape varies per prompt run and the frontend must guess.

The Exercise Pattern Engine fixes this by introducing `ExercisePattern` as the connective layer between the session generator (which picks patterns) and the AI generator (which fills them with content using a strict schema).

---

## Concept Map

```
ExerciseKind       = teaching slot in a lesson template (VocabularyWarmup, WritingTask…)
                     Already exists. One per SessionExercise row.

ExercisePattern    = named interaction format (PhraseMatch, GapFill, EmailReply, TeamsChatSimulation…)
                     NEW. Many patterns map to one ExerciseKind.
                     Defines: primary skill, secondary skills, ActivityType, InteractionMode,
                               MarkingMode, estimated minutes, AI prompt key, content JSON schema.

ActivityType       = broad implementation bucket (WritingScenario, ListeningComprehension…)
                     Unchanged. ExercisePattern resolves to exactly one ActivityType.

InteractionMode    = how the frontend renders the exercise
                     NEW enum. Determines which Angular component is instantiated.

MarkingMode        = how the answer is evaluated
                     NEW enum. Determines which evaluation path the backend uses.
```

The session generator selects an `ExercisePattern` for each `ExerciseKind` slot. The AI generates content using a strict JSON schema defined by the pattern. The frontend reads `InteractionMode` and instantiates the correct renderer component.

---

## Architecture Decision: Enum + Seeded DB Records (Hybrid)

**Decision: Code-owned enum for keys + seeded DB records for metadata.**

### Options considered

| Option | Pros | Cons |
|---|---|---|
| Pure enum | Simple, type-safe, no DB reads | Cannot be edited by Admin without deploy; no runtime metadata |
| Pure DB records | Fully admin-configurable | Schema migrations for every pattern field; no compile-time safety |
| Hybrid: enum keys + seeded DB rows | Type-safe keys in code; metadata queryable at runtime; Admin-editable later | Enum and DB must stay in sync (acceptable — enum is the canonical source) |

**Hybrid is correct for MVP.** Pattern keys are string constants defined in a C# `ExercisePatternKey` static class (not an enum, to allow DB seeding and future admin extension without an enum value). Metadata lives in a new `exercise_patterns` table seeded at startup. Admin UI is out of scope for MVP but the table is designed to support it.

---

## New Domain Concepts

### InteractionMode

How the frontend renders the exercise. One renderer component per mode.

```csharp
public enum InteractionMode
{
    ReadOnly          = 0,  // Micro-lesson: student reads/listens, no submission
    FreeTextEntry     = 1,  // Open text box (email reply, writing scenario)
    GapFill           = 2,  // Inline blanks in a passage (click or type)
    MultipleChoice    = 3,  // Select one from A/B/C/D options
    MatchingPairs     = 4,  // Drag-and-drop or click-to-pair two columns
    SentenceBuilder   = 5,  // Drag scrambled words into correct order
    ErrorCorrection   = 6,  // Highlight errors in text, type correction
    ChatReply         = 7,  // Teams-style chat bubble UI, type reply
    AudioAndFreeText  = 8,  // Play audio clip first, then free text answer
    AudioAndGapFill   = 9,  // Play audio, fill gaps in transcript
}
```

### MarkingMode

How the answer is evaluated.

```csharp
public enum MarkingMode
{
    AiOpenEnded       = 0,  // AI evaluates free text holistically
    AiStructured      = 1,  // AI evaluates against a rubric defined in the pattern's eval prompt
    ExactMatch        = 2,  // Answer matches one of N accepted strings (deterministic)
    KeyedSelection    = 3,  // Frontend/backend checks selection against correctIndex
    NoMarking         = 4,  // Read-only step; no submission required
}
```

### ExercisePatternDefinition

The metadata record for one named pattern. Stored in `exercise_patterns` table. Seeded from code.

```csharp
public sealed class ExercisePatternDefinition
{
    public string Key { get; init; }              // "gap_fill_with_workplace_phrase"
    public string Name { get; init; }             // "Gap Fill — Workplace Phrase"
    public string PrimarySkill { get; init; }     // "Vocabulary"
    public string[] SecondarySkills { get; init; }
    public ExerciseKind[] CompatibleKinds { get; init; }
    public ActivityType ActivityType { get; init; }
    public InteractionMode InteractionMode { get; init; }
    public MarkingMode MarkingMode { get; init; }
    public int EstimatedMinutes { get; init; }
    public string AiGeneratePromptKey { get; init; }  // key in ai_prompts table
    public string AiEvaluatePromptKey { get; init; }  // key in ai_prompts table
    public bool RequiresAudio { get; init; }
    public bool WorkplaceContext { get; init; }
    public string TeachingPurpose { get; init; }
}
```

### SkillMix

Already implicit in `PrimarySkill` + `SecondarySkills`. No new type needed. The session generator uses `ExercisePatternDefinition.PrimarySkill` and `SecondarySkills[]` when applying weak-skill substitution.

---

## Database Changes

### New table: `exercise_patterns`

```sql
CREATE TABLE exercise_patterns (
    key                     TEXT PRIMARY KEY,
    name                    TEXT NOT NULL,
    primary_skill           TEXT NOT NULL,
    secondary_skills_json   TEXT NOT NULL DEFAULT '[]',
    compatible_kinds_json   TEXT NOT NULL DEFAULT '[]',
    activity_type           TEXT NOT NULL,
    interaction_mode        INTEGER NOT NULL,
    marking_mode            INTEGER NOT NULL,
    estimated_minutes       INTEGER NOT NULL,
    ai_generate_prompt_key  TEXT NOT NULL,
    ai_evaluate_prompt_key  TEXT NOT NULL,
    requires_audio          BOOLEAN NOT NULL DEFAULT FALSE,
    workplace_context       BOOLEAN NOT NULL DEFAULT TRUE,
    teaching_purpose        TEXT NOT NULL,
    is_active               BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

This table is **read-only at runtime** (MVP). It is seeded by `ExercisePatternSeeder` at startup. Admin UI to edit patterns is deferred.

### Changes to `session_exercises`

No column changes. `exercise_pattern_key` already exists. The engine now enforces that every `exercise_pattern_key` value matches a row in `exercise_patterns`.

### Changes to `learning_activities`

Add one nullable column:

```sql
ALTER TABLE learning_activities ADD COLUMN exercise_pattern_key TEXT NULL;
```

This links an activity back to the pattern it was generated from. Required for:
- Correct frontend renderer selection (read `InteractionMode` from pattern)
- Correct evaluation path (read `MarkingMode` from pattern)
- Future admin analytics ("which patterns produce the best outcomes")

### New `ai_prompts` rows

One `activity_generate_{patternKey}` prompt and one `activity_evaluate_{patternKey}` prompt per MVP pattern. Seeded by `AiPromptSeeder`. The prompt key naming convention from `learning-activity-engine.md` is extended:

```
activity_generate_{patternKey}   e.g. activity_generate_gap_fill_workplace_phrase
activity_evaluate_{patternKey}   e.g. activity_evaluate_gap_fill_workplace_phrase
```

The existing `activity_generate_writing` / `activity_evaluate_writing` prompts continue to work for the legacy `FreeTextEntry` path.

---

## MVP Pattern List

**Selection criteria for MVP:**
- Covers all four ExerciseKind slots used in current session templates (VocabularyWarmup, ListeningInput, WritingTask, Review) plus a speaking option
- Includes at least one interaction per InteractionMode (so the frontend renderer set is established)
- No audio-generation dependency (RequiresAudio = false) except ListeningGapFill, which reuses existing TTS pipeline
- High product value — these are patterns students encounter in every real English lesson

### 8 MVP Patterns

---

#### 1. `phrase_match` — Matching Pairs

| Field | Value |
|---|---|
| Name | Phrase Match |
| Primary skill | Vocabulary |
| Secondary skills | Workplace Tone |
| Compatible ExerciseKinds | VocabularyWarmup |
| ActivityType | VocabularyPractice |
| InteractionMode | MatchingPairs |
| MarkingMode | KeyedSelection |
| EstimatedMinutes | 2–3 |
| RequiresAudio | No |
| TeachingPurpose | Introduce or review key workplace phrases before deeper use |

**AI content JSON schema:**
```json
{
  "patternKey": "phrase_match",
  "topic": "Explaining a delay professionally",
  "pairs": [
    { "id": "1", "left": "I apologise for the delay", "right": "acknowledging you are late" },
    { "id": "2", "left": "I'll have it ready by", "right": "committing to a deadline" },
    { "id": "3", "left": "Could you please confirm", "right": "politely requesting verification" },
    { "id": "4", "left": "As a next step", "right": "signalling an action plan" }
  ],
  "instructionInSourceLanguage": "این عبارات را با معنی آنها تطبیق دهید."
}
```

**Answer / evaluation schema:**
```json
{ "submittedPairs": [{ "leftId": "1", "rightId": "acknowledging you are late" }] }
```
Marking: deterministic `KeyedSelection` — no AI eval call needed.

---

#### 2. `gap_fill_workplace_phrase` — Gap Fill

| Field | Value |
|---|---|
| Name | Gap Fill — Workplace Phrase |
| Primary skill | Vocabulary |
| Secondary skills | Grammar |
| Compatible ExerciseKinds | VocabularyWarmup, ContextInput |
| ActivityType | VocabularyPractice |
| InteractionMode | GapFill |
| MarkingMode | ExactMatch |
| EstimatedMinutes | 3–4 |
| RequiresAudio | No |
| TeachingPurpose | Practise target phrases in a realistic workplace sentence context |

**AI content JSON schema:**
```json
{
  "patternKey": "gap_fill_workplace_phrase",
  "topic": "Explaining a delay professionally",
  "passage": "I ___[1]___ for the delay on the IFC submittal. I ___[2]___ it ready by Thursday. Could you please ___[3]___ the new deadline?",
  "gaps": [
    { "id": "1", "acceptedAnswers": ["apologise", "apologize"] },
    { "id": "2", "acceptedAnswers": ["will have", "'ll have"] },
    { "id": "3", "acceptedAnswers": ["confirm"] }
  ],
  "wordBank": ["apologise", "will have", "confirm", "explain", "send"],
  "instructionInSourceLanguage": "جاهای خالی را با کلمات مناسب پر کنید."
}
```

**Answer / evaluation schema:**
```json
{ "answers": [{ "gapId": "1", "value": "apologise" }, { "gapId": "2", "value": "will have" }] }
```
Marking: `ExactMatch` — backend checks each gap answer against `acceptedAnswers`.

---

#### 3. `listen_and_answer` — Audio + Free Text

| Field | Value |
|---|---|
| Name | Listen and Answer |
| Primary skill | Listening |
| Secondary skills | Vocabulary |
| Compatible ExerciseKinds | ListeningInput, ContextInput |
| ActivityType | ListeningComprehension |
| InteractionMode | AudioAndFreeText |
| MarkingMode | AiStructured |
| EstimatedMinutes | 4 |
| RequiresAudio | Yes (TTS) |
| TeachingPurpose | Check understanding of a workplace audio message |

**AI content JSON schema:**
```json
{
  "patternKey": "listen_and_answer",
  "topic": "Explaining a delay professionally",
  "audioScript": "Hi Sarah, I'm calling about the IFC submittal. We were expecting it yesterday — can you let me know the status and when we can expect it?",
  "questions": [
    { "id": "q1", "question": "What is the caller asking about?", "guidanceForMarking": "Should mention the IFC submittal / document" },
    { "id": "q2", "question": "What two things does the caller want to know?", "guidanceForMarking": "Status and expected completion date" }
  ],
  "instructionInSourceLanguage": "به پیام صوتی گوش دهید و سوالات را پاسخ دهید."
}
```

**Answer / evaluation schema:**
```json
{ "answers": [{ "questionId": "q1", "value": "The caller is asking about the IFC submittal" }] }
```
Marking: `AiStructured` — AI evaluates each answer against `guidanceForMarking`.

---

#### 4. `listen_and_gap_fill` — Audio + Gap Fill

| Field | Value |
|---|---|
| Name | Listen and Gap Fill |
| Primary skill | Listening |
| Secondary skills | Vocabulary, Grammar |
| Compatible ExerciseKinds | ListeningInput |
| ActivityType | ListeningComprehension |
| InteractionMode | AudioAndGapFill |
| MarkingMode | ExactMatch |
| EstimatedMinutes | 4 |
| RequiresAudio | Yes (TTS) |
| TeachingPurpose | Notice workplace phrases from audio; train active listening |

**AI content JSON schema:**
```json
{
  "patternKey": "listen_and_gap_fill",
  "topic": "Explaining a delay professionally",
  "audioScript": "I apologise for the delay on the submittal. I will have it ready by Thursday. Could you please confirm if that works for you?",
  "gappedTranscript": "I ___[1]___ for the delay on the submittal. I will have it ___[2]___ by Thursday. Could you please ___[3]___ if that works for you?",
  "gaps": [
    { "id": "1", "acceptedAnswers": ["apologise", "apologize"] },
    { "id": "2", "acceptedAnswers": ["ready"] },
    { "id": "3", "acceptedAnswers": ["confirm"] }
  ],
  "instructionInSourceLanguage": "گوش دهید و جاهای خالی متن را پر کنید."
}
```

Marking: `ExactMatch`.

---

#### 5. `email_reply` — Free Text (structured output)

| Field | Value |
|---|---|
| Name | Workplace Email Reply |
| Primary skill | Writing |
| Secondary skills | Grammar, Vocabulary, Tone |
| Compatible ExerciseKinds | WritingTask |
| ActivityType | WritingScenario |
| InteractionMode | FreeTextEntry |
| MarkingMode | AiStructured |
| EstimatedMinutes | 7 |
| RequiresAudio | No |
| TeachingPurpose | Structured workplace writing with correct format, tone, and register |

**AI content JSON schema:**
```json
{
  "patternKey": "email_reply",
  "topic": "Explaining a delay professionally",
  "incomingEmail": {
    "from": "Manager <s.chen@company.com>",
    "subject": "IFC Submittal — Status Update",
    "body": "Hi,\n\nI wanted to check in on the IFC submittal. We were expecting it yesterday and I haven't heard anything. Can you please update me on the status and let me know when we can expect it?\n\nThanks,\nSarah"
  },
  "taskDescription": "Reply to Sarah's email. Apologise for the delay, explain the reason briefly, and commit to a specific date.",
  "targetPhrases": ["I apologise for the delay", "I will have this ready by", "Please let me know if"],
  "wordLimit": 120,
  "rubric": {
    "tone": "Professional and solution-focused. Avoid blame.",
    "structure": "Opening acknowledgement, brief reason, commitment to date, closing offer",
    "phraseUsage": "Must use at least 2 of the target phrases"
  },
  "instructionInSourceLanguage": "به این ایمیل پاسخ حرفه‌ای بنویسید."
}
```

**Answer / evaluation schema:**
```json
{ "replyText": "Hi Sarah,\n\nI apologise for the delay..." }
```
Marking: `AiStructured` — AI evaluates against `rubric`.

---

#### 6. `teams_chat_simulation` — Chat Reply

| Field | Value |
|---|---|
| Name | Teams Chat Simulation |
| Primary skill | Writing |
| Secondary skills | Workplace Tone, Vocabulary |
| Compatible ExerciseKinds | WritingTask |
| ActivityType | WritingScenario |
| InteractionMode | ChatReply |
| MarkingMode | AiStructured |
| EstimatedMinutes | 5 |
| RequiresAudio | No |
| TeachingPurpose | Practise concise professional digital communication in chat format |

**AI content JSON schema:**
```json
{
  "patternKey": "teams_chat_simulation",
  "topic": "Explaining a delay professionally",
  "chatThread": [
    { "sender": "Manager", "message": "Hi, can you give me an update on the IFC submittal? We were expecting it yesterday.", "timestamp": "09:14" }
  ],
  "studentRole": "Document Controller",
  "targetPhrases": ["I apologise for the delay", "I'll have it ready by", "I'll keep you updated"],
  "toneGuidance": "Professional and solution-focused. No excuses, just facts and commitment.",
  "wordLimit": 60,
  "rubric": {
    "tone": "Must be professional, not defensive",
    "conciseness": "Under word limit",
    "completeness": "Must address status and timeline",
    "phraseUsage": "At least one target phrase used"
  },
  "instructionInSourceLanguage": "به پیام مدیر خود در چت تیمز پاسخ دهید."
}
```

Marking: `AiStructured`.

---

#### 7. `spoken_response_from_prompt` — Speaking

| Field | Value |
|---|---|
| Name | Spoken Response |
| Primary skill | Speaking |
| Secondary skills | Vocabulary, Grammar |
| Compatible ExerciseKinds | SpeakingTask |
| ActivityType | SpeakingRolePlay |
| InteractionMode | FreeTextEntry |
| MarkingMode | AiOpenEnded |
| EstimatedMinutes | 5 |
| RequiresAudio | No (fake STT for MVP) |
| TeachingPurpose | Practise clear, organised spoken workplace response |

Note: `InteractionMode = FreeTextEntry` for MVP (fake STT). When real STT lands, a new `AudioRecording` mode will replace it without changing the pattern definition.

**AI content JSON schema:**
```json
{
  "patternKey": "spoken_response_from_prompt",
  "topic": "Explaining a delay professionally",
  "scenario": "Your manager stops you in the corridor and asks for an update on the IFC submittal. You have 30 seconds to explain the delay and commit to a date.",
  "targetPhrases": ["I apologise for the delay", "I'll have it ready by"],
  "timeLimit": 30,
  "instructionInSourceLanguage": "پاسخ شفاهی خود را ضبط کنید."
}
```

Marking: `AiOpenEnded`.

---

#### 8. `lesson_reflection` — Read Only

| Field | Value |
|---|---|
| Name | Lesson Reflection |
| Primary skill | Reflection |
| Secondary skills | — |
| Compatible ExerciseKinds | Review |
| ActivityType | WritingScenario (placeholder, no submission) |
| InteractionMode | ReadOnly |
| MarkingMode | NoMarking |
| EstimatedMinutes | 2 |
| RequiresAudio | No |
| TeachingPurpose | Consolidation and session closing; metacognitive awareness |

**AI content JSON schema:**
```json
{
  "patternKey": "lesson_reflection",
  "topic": "Explaining a delay professionally",
  "coachNote": "You practised three key professional phrases today. Your tone in the email reply was professional — well done. Next time, focus on making your opening sentence even shorter.",
  "phrasesToRemember": ["I apologise for the delay", "I'll have it ready by [date]", "Please let me know if you have any questions"],
  "selfReflectionPrompts": [
    "Which phrase will you use first at work?",
    "Was there a step you found difficult? Which one?"
  ],
  "instructionInSourceLanguage": "درس امروز را مرور کنید."
}
```

Marking: `NoMarking` — the Review step calls `POST /api/sessions/{id}/exercises/{eid}/complete` directly.

---

## How AI Generates Content Using Strict Schemas

### Current problem

The AI receives `ActivityType = WritingScenario` and a `TopicHint`. The prompt says "generate a writing activity". The output shape is loosely defined and inconsistently structured.

### New approach

Each pattern has a dedicated `activity_generate_{patternKey}` prompt. The prompt includes:

1. The exact JSON schema the AI must output (as a system-level instruction with a JSON schema block)
2. The topic, CEFR level, domain complexity, career context, and session goal injected as template variables
3. A schema validation step: the backend deserialises the response into a typed DTO and rejects + retries if it does not match

**Prompt variable set (all generation calls):**
```
{{PatternKey}}
{{TopicHint}}
{{SessionGoal}}
{{CEFRLevel}}
{{DomainComplexity}}
{{ProfessionalExperienceLevel}}
{{RoleFamiliarity}}
{{CareerContext}}
{{LanguagePairCode}}
{{SourceLanguageName}}
{{TargetLanguageName}}
{{RecentMistakesSummary}}
```

**Prompt key examples:**
```
activity_generate_phrase_match
activity_evaluate_phrase_match
activity_generate_gap_fill_workplace_phrase
activity_evaluate_gap_fill_workplace_phrase
activity_generate_email_reply
activity_evaluate_email_reply
activity_generate_teams_chat_simulation
activity_evaluate_teams_chat_simulation
activity_generate_lesson_reflection
```

### Schema enforcement

The `ExercisePrepareHandler` currently passes raw JSON to `LearningActivity.AiGeneratedContentJson` without schema validation. After this sprint:

1. Each pattern has a typed C# record (e.g. `GapFillContent`, `EmailReplyContent`) in `LinguaCoach.Application/Sessions/PatternContent/`.
2. `ExercisePrepareHandler` deserialises the AI response into the typed record. If deserialisation fails, it retries once. If retry fails, it falls back to `SystemFallback`.
3. The typed record is re-serialised to JSON before storage — guaranteeing consistent shape regardless of AI output variation.

---

## How Today's Lesson Selects Patterns

### Current state

`SessionDurationTemplates.cs` hardcodes `PatternKey` strings directly in the template steps. There is no runtime lookup against pattern metadata.

### After this sprint

The session generator is extended to:

1. Load `ExercisePatternDefinition` records from `exercise_patterns` at generation time (cached in-process).
2. For each `ExerciseKind` slot in the template, look up available patterns compatible with that kind.
3. Apply selection rules from `exercise-pattern-library.md`:
   - Match `CEFRLevel` to pattern difficulty range
   - Prefer patterns that target `WeakSkills` from `StudentSkillProfile`
   - Exclude patterns used in the last N sessions (avoid repetition)
   - Enforce micro-lesson prerequisite (if using `gap_fill_*`, check whether `phrase_match` or `micro_lesson_phrases` precedes it)
4. If no pattern selection is needed (MVP templates are narrow), fall through to the hardcoded default.

For MVP, the templates still hardcode pattern keys — but the generator now validates that each key exists in `exercise_patterns` at session creation time. Dynamic pattern selection is Phase 2 of this sprint (see implementation phases).

### Template patternKey corrections

The current templates use non-canonical keys (`writing_response`, `speaking_role_play`). As part of this sprint, templates are updated to use canonical keys from the pattern library:

| Old key | Canonical key |
|---|---|
| `writing_response` | `email_reply` |
| `speaking_role_play` | `spoken_response_from_prompt` |

The `ExercisePrepareHandler.ResolveKind()` switch is updated to use canonical keys and no longer needs the prefix-based fallbacks.

---

## How Practice Gym Reuses the Pattern Library

Practice Gym currently calls `GET /api/activity?type=WritingScenario` which bypasses the pattern engine entirely. After this sprint:

1. A new optional `patternKey` query parameter is added: `GET /api/activity?patternKey=email_reply`
2. When `patternKey` is supplied, `ActivityGetHandler` looks up the `ExercisePatternDefinition`, resolves the `ActivityType`, and passes the pattern's `ai_generate_prompt_key` to `IAiActivityGenerator`.
3. The frontend Practice Gym can select from available pattern keys in a dropdown (pattern selector UX is out of scope for this sprint — the parameter exists but the UI shows it only as a developer/admin tool).
4. When `patternKey` is absent, existing behaviour is unchanged.

The Practice Gym continues to work unchanged for any student who does not use the `patternKey` param.

---

## Frontend Renderer Strategy

### New component: `ExerciseRendererComponent`

A dispatch component instantiated by `ActivityLessonComponent` (and eventually `LessonComponent` inline). It reads `interactionMode` from the activity DTO and renders the correct child:

```
ExerciseRendererComponent
  ├── ReadOnlyStepComponent         (InteractionMode.ReadOnly)
  ├── FreeTextEntryComponent        (InteractionMode.FreeTextEntry)   ← existing WritingSessionComponent
  ├── GapFillComponent              (InteractionMode.GapFill)         ← NEW
  ├── MultipleChoiceComponent       (InteractionMode.MultipleChoice)  ← NEW
  ├── MatchingPairsComponent        (InteractionMode.MatchingPairs)   ← NEW
  ├── SentenceBuilderComponent      (InteractionMode.SentenceBuilder) ← deferred
  ├── ErrorCorrectionComponent      (InteractionMode.ErrorCorrection) ← deferred
  ├── ChatReplyComponent            (InteractionMode.ChatReply)       ← NEW
  ├── AudioAndFreeTextComponent     (InteractionMode.AudioAndFreeText)← NEW (wraps existing audio player)
  └── AudioAndGapFillComponent      (InteractionMode.AudioAndGapFill) ← NEW
```

### ActivityDto changes

`ActivityDto` gains two new fields:

```typescript
interface ActivityDto {
  // existing fields...
  interactionMode: InteractionMode;   // NEW — drives renderer selection
  exercisePatternKey: string | null;  // NEW — for analytics and debugging
}
```

`InteractionMode` is returned from the backend. The frontend does **not** derive it from `activityType` — this was the old approach and it is what caused the ambiguity.

### Token-aware design

- `MatchingPairsComponent`: pairs rendered as chips; correct pair fades to `--color-success`; incorrect pair shakes and resets
- `GapFillComponent`: inline `<input>` elements within passage text; correct fill turns green; wrong fill turns red with correct answer shown
- `ChatReplyComponent`: Teams-inspired chat bubble layout; word counter; target phrases shown as collapsible hint panel
- `AudioAndFreeTextComponent`: reuses existing `AudioPlayerComponent`; answer panel below audio controls
- All new components use existing design tokens from `_variables.scss` — no new colours introduced

### Which renderers to build in MVP

Build only the renderers needed for the 8 MVP patterns:

| MVP Pattern | Renderer needed |
|---|---|
| phrase_match | MatchingPairsComponent |
| gap_fill_workplace_phrase | GapFillComponent |
| listen_and_answer | AudioAndFreeTextComponent |
| listen_and_gap_fill | AudioAndGapFillComponent |
| email_reply | FreeTextEntryComponent (exists) |
| teams_chat_simulation | ChatReplyComponent |
| spoken_response_from_prompt | FreeTextEntryComponent (exists) |
| lesson_reflection | ReadOnlyStepComponent |

`SentenceBuilderComponent` and `ErrorCorrectionComponent` are deferred to pattern P3.

---

## Model / Database Summary

### New

| Artifact | Location | Notes |
|---|---|---|
| `InteractionMode` enum | `LinguaCoach.Domain/Enums/` | |
| `MarkingMode` enum | `LinguaCoach.Domain/Enums/` | |
| `ExercisePatternDefinition` entity | `LinguaCoach.Domain/Entities/` | |
| `exercise_patterns` table | EF migration | Seeded from `ExercisePatternSeeder` |
| `ExercisePatternSeeder` | `LinguaCoach.Infrastructure/Seeding/` | Seeds all MVP pattern rows |
| Pattern content records (typed DTOs) | `LinguaCoach.Application/Sessions/PatternContent/` | One C# record per MVP pattern |
| `IExercisePatternRepository` + EF impl | Application / Infrastructure | Simple `GetByKey`, `GetAll` |

### Modified

| Artifact | Change |
|---|---|
| `LearningActivity` | Add `ExercisePatternKey` nullable string property |
| `learning_activities` | EF migration: add `exercise_pattern_key TEXT NULL` |
| `ActivityDto` | Add `InteractionMode`, `ExercisePatternKey` fields |
| `ExercisePrepareHandler` | Look up pattern definition; pass pattern prompt key to AI; deserialise typed content |
| `SessionDurationTemplates` | Update pattern keys to canonical names (`email_reply`, `spoken_response_from_prompt`) |
| `ExercisePrepareHandler.ResolveKind()` | Update switch for canonical keys; remove prefix-based fallbacks |
| `AiPromptSeeder` | Add `activity_generate_{patternKey}` and `activity_evaluate_{patternKey}` for 8 MVP patterns |
| `ActivityLessonComponent` | Read `interactionMode` from DTO; dispatch to `ExerciseRendererComponent` |

---

## Implementation Phases

### Phase 1 — Domain and DB ✅ COMPLETE (2026-06-10)

1. ✅ Add `InteractionMode` and `MarkingMode` enums to `LinguaCoach.Domain/Enums/`
2. ✅ Add `ExercisePatternDefinition` entity with EF configuration
3. ✅ Add `exercise_pattern_key TEXT NULL` column to `learning_activities`
4. ✅ Write and apply EF migration (T33_ExercisePatternEngine)
5. ✅ Add `IExercisePatternRepository` interface and EF implementation (`GetByKey`, `GetAllActive`, `GetByKind`)
6. ✅ Write `ExercisePatternSeeder` with all 8 MVP patterns
7. ✅ Register seeder in startup pipeline
8. ✅ Unit tests: seeder produces correct metadata for each pattern; `GetByKind` returns expected patterns
9. ✅ All 711 tests passing (356 unit + 355 integration)

### Phase 2 — Backend: pattern-aware prepare + AI prompts ✅ COMPLETE (2026-06-10)

1. ✅ Add pattern content DTOs (typed C# records) for all 8 MVP patterns (`PatternContentDtos.cs`)
2. ✅ Extend `ExercisePrepareHandler`:
   - ✅ Load `ExercisePatternDefinition` by `exercise.ExercisePatternKey`
   - ✅ Pass pattern's `AiGeneratePromptKey` as `OverridePromptKey` via `ActivityGenerationContext`
   - ✅ Set `LearningActivity.ExercisePatternKey` on the newly created activity
   - ✅ Fall back to `SystemFallback` with correct shape on AI failure
   - ✅ Return clear error for inactive pattern keys
3. ✅ Update `ActivityGetHandler`: populate `InteractionMode` and `ExercisePatternKey` on `ActivityDto`
4. ✅ Update `SessionDurationTemplates`: `writing_response` → `email_reply`, `speaking_role_play` → `spoken_response_from_prompt`
5. ✅ Update `ExercisePrepareHandler.ResolveKind()` and `SessionGeneratorService.ResolveKind()` for all canonical keys
6. ✅ Write and seed `activity_generate_*` and `activity_evaluate_*` prompts for all 8 MVP patterns
7. ✅ `ActivityDto` extended with `InteractionMode` and `ExercisePatternKey` fields
8. ✅ Unit and integration tests: 762 total (380 unit + 382 integration), 0 failures

**Deferred to later phase (by design):**
- Practice Gym `patternKey` parameter — not needed yet, no UI selector
- Admin UI for pattern management
- Typed DTO round-trip validation at AI generation boundary

### Phase 3 — Frontend renderers ✅ COMPLETE (2026-06-10)

1. ✅ Created `ExerciseRendererComponent` as the interaction-mode dispatch shell.
2. ✅ Built/wired `MatchingPairsComponent` for `phrase_match`.
3. ✅ Built/wired `GapFillComponent` for `gap_fill_workplace_phrase`.
4. ✅ Built/wired `AudioAndFreeTextComponent` for `listen_and_answer`.
5. ✅ Built/wired `AudioAndGapFillComponent` for `listen_and_gap_fill`.
6. ✅ Built/wired `ChatReplyComponent` for `teams_chat_simulation`.
7. ✅ Integrated `ReadOnlyStepComponent` and `FreeTextEntryComponent` into the renderer shell.
8. ✅ Wired `ActivityLessonComponent` to use `ExerciseRendererComponent` for activities with `interactionMode`; legacy activity mocks remain safe.
9. ✅ API response exposes `interactionMode`, `exercisePatternKey`, and pattern `contentJson`; raw JSON is only returned for pattern-keyed activities to preserve legacy listening answer privacy.
10. ✅ `npm run build` passes.

### Phase 4 — E2E tests and validation ✅ COMPLETE (2026-06-10)

1. ✅ Playwright renderer coverage added for MatchingPairs, GapFill, ChatReply, AudioAndFreeText, AudioAndGapFill, ReadOnly, and legacy FreeText fallback.
2. ✅ Existing Today's Lesson flow remains covered by `today-lesson.spec.ts`.
3. ✅ Existing Practice Gym/activity flows remain covered by vocabulary, listening, speaking, and dashboard route tests.
4. ✅ Legacy listening privacy remains covered by backend and Playwright assertions: no raw transcript or `expectedAnswer` appears before submission.
5. ✅ Pattern-keyed activities expose renderer-scoped `contentJson`; legacy non-pattern listening activities do not expose raw answer-bearing JSON.
6. ✅ Full Playwright suite passes: 97/97 with `--workers=1`.

### Final validation baseline (2026-06-10)

| Check | Result |
|---|---|
| `dotnet test` | ✅ Passed — 762 total (380 unit + 382 integration) |
| `npm run build` | ✅ Passed |
| `npx playwright test --workers=1` | ✅ Passed — 97/97 |

### Regression audit

| Area | Validation |
|---|---|
| Today's Lesson | ✅ Existing prepare/open/return and lesson lifecycle Playwright coverage passes |
| Practice Gym | ✅ Existing dashboard Practice Gym routing plus legacy activity-type flows pass |
| Pattern renderers | ✅ Pattern-keyed activities dispatch through `ExerciseRendererComponent` by `interactionMode` |
| Legacy fallback | ✅ Legacy writing/free-text activity remains renderable without pattern metadata |
| Listening privacy | ✅ Legacy listening DTOs do not expose transcript, `audioScript`, or `expectedAnswer` before submit |
| Pattern `contentJson` | ✅ Exposed only for pattern-keyed activities and used only for renderer content |

Known non-blocking warnings:

- Angular build reports existing admin CSS budget warnings (`admin-dashboard` component CSS and `admin-app-layout` CSS).
- Angular build reports existing skipped selector warnings (`& -> Empty sub-selector`).
- These warnings are not related to Phase 4 validation and were not changed in this closure pass.

---

## Test Plan

### Unit tests

- `ExercisePatternSeeder`: each MVP pattern has correct `InteractionMode`, `MarkingMode`, `ActivityType`, `CompatibleKinds`
- `ExercisePatternDefinition`: `GetByKind` returns correct patterns for each `ExerciseKind`
- `ExercisePrepareHandler`: typed DTO deserialisation for each MVP pattern content shape
- `ExercisePrepareHandler.ResolveKind()`: canonical key → correct `ExerciseKind`
- `SessionDurationTemplates`: updated keys match existing patterns

### Integration tests

- `POST /api/sessions/{id}/exercises/{eid}/prepare` with each pattern key → correct `ActivityType`, `InteractionMode`, `ExercisePatternKey`
- Unknown pattern key returns 400
- `ExercisePatternKey` is set on `LearningActivity` row in DB
- `GET /api/activity/{id}` returns `interactionMode` and `exercisePatternKey`
- `GET /api/activity?patternKey=email_reply` returns activity with correct `ActivityType`

### Playwright E2E

- Full lesson flow: open each of the 8 MVP pattern types in a lesson step
- MatchingPairs: correct pair selection persists; submit enabled after all pairs matched
- GapFill: correct answers render green; word bank words are clickable (if word-bank mode)
- ChatReply: word counter decrements; target phrases hint panel is collapsible
- AudioAndFreeText: audio player renders before answer area; audio plays
- ReadOnly: "Got it" advances without calling `/complete` on the activity
- Regression: all existing 90 Playwright tests pass

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| AI output does not match typed DTO schema consistently | Medium | High | Schema included verbatim in prompt; retry once; fallback to SystemFallback on second failure |
| Audio patterns (listen_and_answer, listen_and_gap_fill) depend on TTS pipeline working correctly | Low | Medium | TTS already in production from ListeningComprehension sprint; audio patterns reuse same path |
| MatchingPairs and GapFill frontend state management is more complex than FreeText | Medium | Medium | Build stateless: each renderer emits one `submitAnswer` event to parent; parent owns state |
| Canonical key rename breaks existing test sessions in DB | Low | Low | `ResolveKind()` retains old keys as aliases during the transition period; test DB is reset per test run |
| 8 new AI prompts require iteration to produce good content quality | High | Medium | Write prompts in Phase 2 with explicit JSON schema; verify with manual AI calls before wiring; include example outputs |
| `exercise_patterns` seeder diverges from `ExercisePatternKey` constants over time | Medium | Medium | `ExercisePatternKey` constants are the canonical source; seeder is generated from them; CI test verifies no orphaned keys |

---

## Acceptance Criteria

- [x] `exercise_patterns` table exists and is seeded with all 8 MVP patterns
- [x] `learning_activities` has `exercise_pattern_key` column
- [x] `ExercisePrepareHandler` reads the pattern definition, passes the pattern's AI prompt key, and sets `ExercisePatternKey` on the created activity
- [x] `ActivityDto` returns `interactionMode` and `exercisePatternKey`
- [x] `MatchingPairsComponent` renders phrase_match exercises correctly
- [x] `GapFillComponent` renders gap_fill_workplace_phrase exercises and accepts typed/word-bank answers
- [x] `AudioAndFreeTextComponent` renders listen_and_answer with audio player first
- [x] `AudioAndGapFillComponent` renders listen_and_gap_fill with inline gap inputs
- [x] `ChatReplyComponent` renders teams_chat_simulation with chat bubble UI and word counter
- [x] `ReadOnlyStepComponent` renders lesson_reflection without a submission form
- [x] `FreeTextEntryComponent` handles email_reply, spoken_response_from_prompt, and pattern-aware free-text fallback
- [x] All 8 MVP patterns produce AI-generated content from the correct prompt key
- [x] `dotnet test` passes (762 tests: 380 unit + 382 integration)
- [x] `npm run build` passes
- [x] Full Playwright suite passes (97/97, including prior Today's Lesson and Practice Gym regressions)

---

## Out of Scope

- Admin UI for editing pattern definitions
- Configurable session template UI
- Dynamic pattern selection by StudentSkillProfile (patterns are still hardcoded in templates for MVP)
- `SentenceBuilderComponent` and `ErrorCorrectionComponent` (P3 patterns)
- Session reflection AI prompt (deferred)
- Pronunciation scoring
- Call Mode
- Advanced TTS voices
- MinIO / IFileStorageService migration
- Full gamification

---

## Documentation Impact

Docs updated during sprint closure:

- `docs/sprints/2026-06-10-exercise-pattern-engine-sprint.md` — marked all phases and acceptance criteria complete; added final validation baseline and known warnings.
- `docs/sprints/current-sprint.md` — marked Exercise Pattern Engine complete and recommended the next sprint.
- `docs/handoffs/current-product-state.md` — added final Exercise Pattern Engine status and deferred gaps.
- `docs/architecture/README.md` — updated implementation state table.
- `docs/architecture/exercise-pattern-library.md` — documented frontend renderer contract and `contentJson` privacy boundary.
- `docs/architecture/learning-activity-engine.md` — documented `ExercisePatternKey`, pattern prompt naming, `ActivityDto` fields, and renderer flow.

Docs that are authoritative inputs (read, not modified):
- `docs/architecture/professional-experience-domain-complexity.md`
- `docs/architecture/student-learning-memory.md`
- `docs/architecture/placement-assessment-model.md`

---

## Decisions Made

1. **Hybrid approach: code constants + seeded DB records.** Pattern keys are C# string constants; metadata lives in `exercise_patterns` table. Admin UI can be layered on top later.
2. **`InteractionMode` drives frontend renderer selection**, not `ActivityType`. This decouples the UI from the broad category and allows multiple interaction formats within the same ActivityType.
3. **`MarkingMode` is pattern-defined**, not inferred from `ActivityType`. This allows deterministic `ExactMatch` marking for GapFill/MatchingPairs without AI calls.
4. **Typed C# records per pattern content schema.** AI output is validated by deserialisation before storage. If it fails shape, retry once then fall back to SystemFallback.
5. **`ExercisePatternKey` is stored on `LearningActivity`.** This is the durable link between a generated activity instance and the pattern that produced it. Required for analytics and evaluation routing.
6. **Session templates keep hardcoded pattern keys for MVP.** Dynamic selection from `StudentSkillProfile` is Phase 2 of the Exercise Pattern Engine sprint, not the first phase.
7. **Practice Gym gets an optional `patternKey` query param.** Existing Practice Gym calls are unchanged. The param unlocks pattern-specific generation when the gym eventually offers a pattern selector.
8. **Template canonical key migration.** `writing_response` → `email_reply`; `speaking_role_play` → `spoken_response_from_prompt`. The `ResolveKind()` method retains old keys as aliases during the migration window.

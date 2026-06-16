# Phase 9K — Content Context / Goal-Alignment Cleanup

**Date:** 2026-06-16
**Related sprint:** Phase 9K
**Type:** Implementation review + cleanup

---

## Context

SpeakPath had hardcoded workplace-only assumptions in prompts, seeders, skill labels, Angular UI text, and test helpers. The app effectively forced all students into a workplace learning context, even when their actual goal was daily life, migration/settlement, travel, study, or social conversation. This cleanup makes the app context-flexible: workplace remains a valid context but is no longer the forced default.

---

## Files reviewed

### Backend (src/)
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExercisePatternSeeder.cs`
- `src/LinguaCoach.Persistence/Seed/ExerciseTypeDefinitionSeeder.cs`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Infrastructure/LearningPath/AiLearningPathGeneratorHandler.cs`
- `src/LinguaCoach.Infrastructure/LearningPath/DefaultPathFactory.cs`
- `src/LinguaCoach.Infrastructure/Memory/StudentMemoryService.cs`
- `src/LinguaCoach.Infrastructure/Sessions/SessionGeneratorService.cs`
- `src/LinguaCoach.Infrastructure/Activity/SpeakingRolePlayEvaluator.cs`
- `src/LinguaCoach.Infrastructure/Activity/VocabularyPracticeGenerator.cs`
- `src/LinguaCoach.Infrastructure/Placement/PlacementService.cs`
- `src/LinguaCoach.Domain/ExercisePatternKey.cs`
- `src/LinguaCoach.Application/Activity/PatternContentDtos.cs`

### Angular (src/LinguaCoach.Web/src/)
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html`
- `src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts`
- `src/LinguaCoach.Web/src/app/features/landing/landing.component.ts`
- `src/LinguaCoach.Web/src/app/features/learning-path/learning-path.component.html`
- `src/LinguaCoach.Web/src/app/features/onboarding/onboarding-shell/onboarding-shell.component.ts`

### Tests
- `tests/LinguaCoach.IntegrationTests/Api/ActivityTestFactory.cs`
- `tests/LinguaCoach.IntegrationTests/Api/PatternKeyedActivityEndpointTests.cs`
- `tests/LinguaCoach.IntegrationTests/Api/PracticeGymNextEndpointTests.cs`
- `tests/LinguaCoach.UnitTests/Activity/ExactMatchEvaluatorTests.cs`
- `tests/LinguaCoach.UnitTests/LearningPath/DefaultPathFactoryTests.cs` (updated)
- `tests/LinguaCoach.UnitTests/Activity/ContextFlexibilityTests.cs` (new)

---

## Workplace assumptions found

### High priority (forced workplace regardless of student context)

1. **`DefaultAiSeeder.cs` — `LearningPathGenerateContent`**: Rule said "Each module must address a distinct **workplace** communication skill" — no student context considered.
2. **`DefaultAiSeeder.cs` — `LearningPathGenerateAdaptiveContent`**: System prompt said "designing the next 3-5 **workplace** writing modules" and rules said "Reuse weak skills through new **workplace** situations."
3. **`DefaultAiSeeder.cs` — `StudentMemoryUpdateContent`**: System prompt: "SpeakPath, a **workplace** English coach." Rule: "Focus on **workplace** communication coaching."
4. **`DefaultAiSeeder.cs` — `VocabularyExtractFromAttemptContent`**: "**workplace** English learning platform", "improve their **workplace** English", phrase examples all workplace-framed.
5. **`DefaultPathFactory.cs`**: All 5 fallback modules were workplace-specific (professional emails, document control, etc.). Path title prefixed "Workplace English for …".
6. **`AiActivityGeneratorHandler.cs`**: `topicHint` defaulted to `"workplace communication"` when no hint supplied.
7. **`AiLearningPathGeneratorHandler.cs`**: `skillFocus` defaulted to `"workplace communication"` when `SkillFocus` not set on profile.

### Medium priority (display labels)

8. **`StudentMemoryService.cs` — `SkillLabels`**: "Formal **workplace** tone", "**Workplace** vocabulary" shown to students.
9. **`PlacementService.cs` — `SkillLabels`**: "**Workplace** vocabulary", "Formal **workplace** tone".
10. **`ExercisePatternSeeder.cs` — `teachingPurpose`**: Many patterns described as "workplace" in their teaching purpose (student-facing text), e.g. "Introduce or review key **workplace** phrases", "Practise clear, organised spoken **workplace** response".

### Angular UI (student-facing copy)

11. **`landing.component.ts`**: Eyebrow "Workplace communication practice", headline "Practise the **workplace** message before you send it", body copy framing AI as workplace-scenario generator.
12. **`onboarding-shell.component.ts`**: "Set up a **workplace** practice path that matches your role."
13. **`dashboard.component.ts`** (`howItWorks`): "AI generates a realistic **workplace** scenario for your career and level."
14. **`dashboard.component.html`**: "We'll use it to build the right **workplace** English course for you." (x2), "SpeakPath is tracking your **workplace** English progress."
15. **`learning-path.component.html`**: "SpeakPath is building a clearer picture of your **workplace** English."

---

## What was changed

### `DefaultAiSeeder.cs`
- `LearningPathGenerateContent`: Module rule now uses "real-life communication skill relevant to `{{careerContext}}` and `{{skillFocus}}`" with instruction to match context type (workplace/daily life/travel/study/settlement).
- `LearningPathGenerateAdaptiveContent`: System prompt changed to "next 3-5 **learning** modules"; module description template broadened; rule changed to "Reuse weak skills through new real-life situations relevant to the student's context."
- `StudentMemoryUpdateContent`: System prompt changed to "SpeakPath, an **English language** coach"; coaching rule broadened to "real communication coaching relevant to the student's context."
- `VocabularyExtractFromAttemptContent`: Platform description, phrase examples, and explanation templates all de-workplaced.

### `DefaultPathFactory.cs`
- All 5 fallback module titles and descriptions changed to general real-life communication topics.
- Path title changed from `"Workplace English for {context} — {level}"` to `"English for {context} — {level}"`.

### `AiActivityGeneratorHandler.cs`
- Default `topicHint` changed from `"workplace communication"` to `"everyday real-life communication"`.

### `AiLearningPathGeneratorHandler.cs`
- Default `skillFocus` changed from `"workplace communication"` to `"general communication"`.

### `StudentMemoryService.cs`
- `"Formal workplace tone"` → `"Formal tone"`
- `"Workplace vocabulary"` → `"Useful vocabulary"`

### `PlacementService.cs`
- `"Workplace vocabulary"` → `"Vocabulary"`
- `"Formal workplace tone"` → `"Formal tone"`

### `ExercisePatternSeeder.cs`
- Added `CreateDefinitionsPublic()` method for test access.
- Updated `teachingPurpose` for: `phrase_match`, `gap_fill_workplace_phrase`, `listen_and_answer`, `listen_and_gap_fill`, `spoken_response_from_prompt`, `open_writing_task`, `speaking_roleplay_turn`, `read_aloud`, `answer_short_question`.
- Patterns retained workplace teachingPurpose where format is inherently workplace-specific: `email_reply` ("Structured workplace writing"), `teams_chat_simulation`, `email_reply`.

### Angular UI
- `landing.component.ts`: Eyebrow, headline, and body copy broadened to real-life English.
- `onboarding-shell.component.ts`: "workplace practice path" → "personalised practice path".
- `dashboard.component.ts`: `howItWorks` array updated to drop workplace assumption.
- `dashboard.component.html`: Two placement card copy strings updated; learning focus string updated.
- `learning-path.component.html`: Journey summary fallback text updated.

### Tests added
- `tests/LinguaCoach.UnitTests/LearningPath/DefaultPathFactoryTests.cs`: 4 new tests for context-flexibility.
- `tests/LinguaCoach.UnitTests/Activity/ContextFlexibilityTests.cs`: New file with 9 tests covering:
  - `DefaultPathFactory` title does not force "Workplace English for" prefix.
  - Fallback modules do not contain "workplace" in titles.
  - Workplace context still supported as one option.
  - `ExercisePatternSeeder` teachingPurpose does not say "workplace" for general-purpose patterns.
  - `respond_to_situation` and `describe_image` have `workplaceContext: false`.
  - Internal key `activity_generate_gap_fill_workplace_phrase` preserved for compatibility.
- `tests/LinguaCoach.UnitTests/LinguaCoach.UnitTests.csproj`: Added `LinguaCoach.Persistence` project reference.

---

## What was intentionally NOT renamed (compatibility)

The following internal identifiers were deliberately preserved to maintain DB, route, pattern engine, and prompt key compatibility:

| Identifier | Reason |
|---|---|
| `gap_fill_workplace_phrase` (pattern key) | DB column value, route parameter, seeder key — changing breaks data |
| `activity_generate_gap_fill_workplace_phrase` (prompt key) | Stored in `AiPrompts` table, referenced by seeder and handler |
| `activity_evaluate_gap_fill_workplace_phrase` (prompt key) | Same |
| `WorkplaceContext` (bool property on `ExercisePatternDefinition`) | DB column name — migration would be needed to rename |
| `careerContext` (field on `StudentProfile`) | Named for admin-created profiles; used by prompts but not renamed |
| `WorkplaceSeniority` (property on `StudentProfile`) | DB column, migration needed to rename |
| `workplace_vocabulary` (skill key in memory/placement) | Stored in JSON fields; display label changed but key preserved |
| `formal_tone` (skill key) | Same |
| Migration files | Never touched |
| Enum values (`WorkplaceSeniority`, etc.) | Never renamed |

---

## How workplace now behaves as one context among many

- **Prompt generation**: All AI prompts use `{{careerContext}}` to personalise content. If the admin sets `CareerContext = "Document Controller"`, the AI generates workplace scenarios. If it is "Daily life learner" or "New arrival", the AI generates appropriate real-life scenarios.
- **Fallback path**: The hard-coded fallback modules now cover general communication skills (clarity, spoken confidence, reading, writing, relationship building) — applicable to any context.
- **Default topicHint**: Changed to "everyday real-life communication" so AI-generated content is not biased toward workplace when no specific topic is set.
- **Skill labels**: Student-facing labels no longer say "workplace vocabulary" or "workplace tone" — they say "Useful vocabulary" and "Formal tone", which apply across all contexts.
- **Format teachingPurpose**: General formats (phrase match, gap fill, listening, spoken response, open writing, roleplay, read aloud) no longer describe their purpose as workplace-specific. Formats that are inherently workplace (email reply, Teams chat) retain their description as-is since that is their actual function.

---

## Future admin/student context controls still needed

The following are **not** implemented in Phase 9K and remain for a future phase:

1. **Student-facing context selection in onboarding**: The onboarding UI still uses workplace-framed career/industry questions. A future phase should offer context options: workplace, daily life, travel, study, migration/settlement.
2. **Per-student context field surface in prompts**: `CareerContext` and `LearningGoal` fields exist on `StudentProfile` but are set only by admins. Students cannot self-select their context goal yet.
3. **`WorkplaceContext` bool on `ExercisePatternDefinition`**: This flag currently filters patterns in some UI contexts. A future phase could expose a `ContextType` enum (Workplace, DailyLife, General) rather than a boolean.
4. **`email_reply` and `teams_chat_simulation` patterns**: These are inherently workplace formats. When a student's context is daily life or migration, these patterns should either be excluded from their Practice Gym or produce non-workplace scenarios (e.g., a personal email rather than a work email). Not in scope for Phase 9K.
5. **Schema rename consideration**: `WorkplaceSeniority`, `CareerContext`, `workplace_vocabulary` skill key — these names imply workplace-only. A future DB migration could rename them to `ContextSeniority`, `LearnerContext`, `context_vocabulary` but this carries migration risk and is not urgent.

---

## Test results

All tests pass:
- Architecture tests: 3 passed
- Unit tests: 890 passed (including 13 new Phase 9K tests)
- Integration tests: 513 passed
- Angular production build: succeeds (pre-existing warnings only, no errors)

---

## Final verdict

Phase 9K is complete. The app is no longer workplace-only by default. Workplace remains fully supported as one context among many. All internal keys, DB columns, migration files, and route parameters are preserved. Student-facing text, AI prompt defaults, and fallback path content are now context-flexible.

---

## Next recommended action

1. Update the onboarding flow to offer non-workplace context choices (daily life, migration/settlement, study, travel) — this is the highest-impact remaining gap.
2. When `CareerContext` or `LearningGoal` is set on the student profile, thread it through all prompt generation paths explicitly (currently done via `{{careerContext}}` variable but the field is admin-set only).
3. Consider a `ContextType` enum on `ExercisePatternDefinition` to replace the boolean `WorkplaceContext` in a future schema migration.

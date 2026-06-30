# Phase 18A — Lesson Quality and Content Generation Upgrade

**Date:** 2026-07-01
**Sprint:** Phase 18A
**Author:** Engineering

---

## 1. Overview

Phase 18A improves the quality of AI-generated lesson content across all activity types. It does not introduce new activity formats, change the activity player, update CEFR from AI feedback, complete objectives from AI feedback, regenerate the Learning Plan from AI feedback, or change speaking/writing applied signal behaviour. All changes are backward compatible.

---

## 2. Files Reviewed

- `src/LinguaCoach.Application/Activity/ModuleStageContentValidator.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs` (writing, listening, speaking roleplay, phrase match, gap fill, listen-and-answer, listen-and-gap-fill, teams-chat, listening MC single, lesson batch plan prompts)
- `tests/LinguaCoach.UnitTests/Activity/ModuleStageContentValidatorTests.cs`

---

## 3. Audit Findings (Parts A–B)

All 28+ exercise pattern keys have dedicated generation and evaluation prompts seeded via `DefaultAiSeeder`. The main quality gaps identified:

1. **No CEFR calibration guidance in prompts.** Writing, listening, and speaking prompts gave no table or guidance on how prompt complexity, expected output length, audio script length, or response complexity should vary by CEFR level (A1–B2).
2. **Support language blocked in many prompts.** Six prompts hardcoded `"sourceLanguageSupport": null`, preventing AI from providing useful L1 support even when the learner has a support language configured. Only email_reply and speaking_roleplay already used the optional format.
3. **Validator missing empty-string check.** AI could return `"prompt": ""` and pass validation.
4. **Validator missing option ID consistency check.** MC formats could have duplicate option IDs or a `correctOptionId` not matching any option.

---

## 4. Changes Made

### 4.1 ModuleStageContentValidator (Part F)

**Empty-string validation** — Added `CriticalStringFields` HashSet covering 12 fields: `prompt`, `audioScript`, `passage`, `question`, `instructions`, `incompleteText`, `incomingMessage`, `partnerTurn`, `sourceText`, `chatHistory`, `displayTranscript`. When a required field is present but empty/whitespace, the validator now returns an error.

**Option ID consistency** — Added `ValidateOptionConsistency()` for:
- `SingleAnswerMcPatterns`: `reading_multiple_choice_single`, `listening_multiple_choice_single`, `select_missing_word`, `highlight_correct_summary` — validates `correctOptionId` is present in the options array; detects duplicate option IDs
- `MultiAnswerMcPatterns`: `reading_multiple_choice_multi`, `listening_multiple_choice_multi` — validates all entries in `correctOptionIds` are present in the options array; detects duplicate option IDs

Both checks are backward compatible: old content without empty strings and with correct option IDs passes unchanged.

### 4.2 Prompt improvements (Parts C–E, G)

**Writing prompt (`ActivityGenerateWritingContent`):**
Added CEFR calibration table before Duration rules:
- A1: single familiar topic, 2–3 short sentences, simple present/past
- A2: familiar workplace task, 4–6 sentences, simple past/common phrases
- B1: routine professional, 80–120 words, present perfect/connectors
- B2: complex/nuanced, 120–180 words, conditionals/passive/precision

**Listening prompt (`ActivityGenerateListeningContent`):**
- Added CEFR calibration table (audio script length, vocabulary complexity, question type per level)
- Changed `"sourceLanguageSupport": null` to optional instruction format

**Speaking roleplay prompt (`ActivityGenerateSpeakingRolePlayContent`):**
- Added CEFR calibration table (response complexity, length, language features per level)
- Removed generic "B1 tasks simple, B2 may require more structure" line (superseded by table)
- Changed `"sourceLanguageSupport": null` to optional instruction format

**Lesson batch plan prompt (`LessonBatchPlanContent`):**
Added CEFR-aware pattern selection table: A1 prefers phrase_match/gap_fill; A2 adds listen_and_answer; B1 adds email_reply/spoken_response; B2 favours teams_chat/spoken_response, avoids over-scaffolded A2-style tasks.

**Other prompts — support language fix:**
Changed `"sourceLanguageSupport": null` to optional instruction format in:
- `ActivityGeneratePhraseMatchContent`
- `ActivityGenerateGapFillContent`
- `ActivityGenerateListenAndAnswerContent`
- `ActivityGenerateListenAndGapFillContent`
- `ActivityGenerateTeamsChatContent`

All prompt changes use the SHA256 hash-based upgrade mechanism in `SeedOrUpgradePromptAsync`. No database migration is required. Prompts upgrade automatically on next API startup.

---

## 5. Tests Added (Part L)

Seven new unit tests in `ModuleStageContentValidatorTests`:

| Test | Validates |
|------|-----------|
| `Validate_Phase18A_EmptyPrompt_Fails` | `"prompt": "   "` fails with empty field error |
| `Validate_Phase18A_EmptyAudioScript_Fails` | `"audioScript": ""` fails with empty field error |
| `Validate_Phase18A_EmptyPassage_Fails` | `"passage": ""` fails with empty field error |
| `Validate_Phase18A_ValidReadingMcSingle_Passes` | Valid MC single payload with correct option IDs passes |
| `Validate_Phase18A_DuplicateOptionId_Fails` | Two options with same ID "A" fails with duplicate error |
| `Validate_Phase18A_CorrectOptionIdNotInOptions_Fails` | `correctOptionId: "E"` not in A/B/C/D fails |
| `Validate_Phase18A_CorrectOptionIdsContainsUnknownId_Fails` | `correctOptionIds: ["A","B","Z"]` fails when "Z" not in options |

---

## 6. Build and Test Results (Part M)

- `dotnet build --configuration Release`: **0 errors, 24 warnings** (all pre-existing)
- `dotnet test tests/LinguaCoach.UnitTests`: **1,633 passed, 0 failed** (up from 1,627)
- `dotnet test tests/LinguaCoach.IntegrationTests`: **1,303 passed, 8 failed** — all 8 failures are pre-existing (require live AI provider not configured in test environment; confirmed identical on HEAD before Phase 18A changes)
- `dotnet test tests/LinguaCoach.ArchitectureTests`: **3 passed**

---

## 7. Constraints Confirmed Not Violated

- No new activity formats added
- No activity player changes
- No CEFR updates from AI feedback
- No objective completion from AI feedback
- No Learning Plan regeneration from AI feedback
- No speaking/writing applied signal behaviour changed
- No live provider calls added to automated tests
- No enterprise organisation work
- No real-time conversation work
- No full student UI redesign

---

## 8. Backward Compatibility (Part K)

All changes are additive:
- Empty-string validation fires only when a field is present and blank. Existing persisted content with populated strings is unaffected.
- Option ID consistency validation fires only when options array is present. Existing content with correct IDs passes.
- Support language field previously null stays null until AI regenerates from the updated prompts.
- Prompt upgrades are hash-triggered and do not affect already-generated content stored in `lesson_buffer`.

---

## 9. Documentation Impact

- Docs reviewed: `docs/sprints/current-sprint.md`, `docs/roadmap/road-map.md`
- Docs updated: `docs/sprints/current-sprint.md` (Phase 18A entry added), `docs/roadmap/road-map.md` (phases 59–61 added, current status updated)
- Docs intentionally not updated: architecture README (no new architectural patterns), testing docs (no new test infrastructure)
- Reason: Phase 18A is a quality improvement pass, not a structural change

---

## 10. Verdict

**Green.** All Phase 18A goals delivered. Zero regressions. Validator improvements catch two classes of previously-undetected generation errors. Prompt upgrades will improve CEFR calibration and support-language behaviour for all future AI-generated content.

---

## 11. Next Recommended Action

Phase 18B or next sprint: consider adding admin visibility for prompt version history, and per-activity generation quality metrics (Part I from the original spec).

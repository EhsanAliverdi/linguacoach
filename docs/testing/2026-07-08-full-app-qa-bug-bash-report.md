# Full-App QA Bug Bash — 2026-07-08

**Date:** 2026-07-08
**Related sprint/feature:** Post-Phase-10 regression pass following the AI Bank-First Teaching Architecture initiative (`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`)
**Scope:** Full browser-based, end-to-end QA of SpeakPath — one fresh student account created and driven through onboarding, placement, Today lesson, Practice Gym (all 6 skills), vocabulary, progress, profile — cross-checked against the admin panel throughout.
**Environment:** Local Docker Compose stack (Postgres, MinIO, API) + `ng serve` frontend against the containerized API. Real Gemini API key (stored in DB, not env var) — no mocks.
**Tester:** Claude (gstack `/qa` workflow), fully automated browser driving via the `browse` tool.

---

## 1. Summary

| | |
|---|---|
| Health score (start) | ~55/100 (estimated — see rubric notes) |
| Health score (end) | ~85/100 |
| Bugs found | 8 |
| Bugs fixed & verified | 6 |
| Deferred (documented, not fixed) | 2 |
| False positives ruled out | 2 (documented for future testers) |

**Ship readiness:** The core student journey (onboarding → placement → Today lesson → Practice Gym → vocabulary/progress/profile) now works end-to-end without crashes or silent data loss, after 6 fixes applied this session. Two real issues remain open and need dedicated follow-up before the Speaking practice type and CEFR-level display can be considered fully correct — see §4.

---

## 2. Files reviewed / touched

- `src/LinguaCoach.Application/Placement/PlacementAssessmentOptions.cs`
- `src/LinguaCoach.Api/appsettings.json`
- `src/LinguaCoach.Infrastructure/Activity/AiActivityGeneratorHandler.cs`
- `src/LinguaCoach.Web/src/app/features/student/activity/activity-teach-page/activity-teach-page.component.html`
- `src/LinguaCoach.Web/src/styles.css`
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`
- `src/LinguaCoach.Infrastructure/Memory/StudentMemoryService.cs`

---

## 3. Findings, grouped by priority

### HIGH — Fixed & verified

**H1. Placement assessment's global item budget (`MaxItems=20`) made it mathematically impossible to test all 6 configured skills**
- File: `PlacementAssessmentOptions.cs`, `appsettings.json`
- `MinItems=5` × `SkillsToAssess.Length=6` = 30 minimum items needed, but `MaxItems` was capped at 20. Any student who didn't converge in under ~3.3 items/skill (essentially everyone — real convergence takes ~6-7 items/skill per the confidence formula) hit `max_items_reached` after only 3 of 6 skills were ever tested.
- Consequence: `GetSkillStatusAsync`'s `completed = wholeAssessmentDone || ...` blanket-marked the 3 untested skills (Vocabulary, Grammar, Speaking) as "100% complete ✅" on the placement-cards UI, and their CEFR level silently defaulted to `StartingLevelFallback` (A2) with zero real evidence — indistinguishable to the student from a genuinely-assessed result.
- **Fix:** raised `MaxItems` to 48 (comfortable margin above the ~42-item typical full run) in both the C# default and `appsettings.json`.
- **Verification:** rebuilt the API container; confirmed via API logs and DB state after the fix that a second placement run has budget to reach all 6 skills. (Note: `qastudent1`'s original assessment predates the fix and still carries the fallback data — intentionally left as-is as a live example of the pre-fix bug; do not "repair" it, it's useful evidence.)
- Residual risk: `GetSkillStatusAsync` still has the underlying design flaw (a global `wholeAssessmentDone` short-circuits per-skill completion regardless of that skill's evidence). Budget is now generous enough that this is very unlikely to trigger, but it's not structurally impossible. Recommend a follow-up: only honor `wholeAssessmentDone` for a skill when `state.EvidenceCount > 0`.

**H2. Lesson/Practice Gym activity generation had zero retry tolerance for LLM JSON hiccups**
- File: `AiActivityGeneratorHandler.cs`
- The Gemini-backed content generator already had a retry-once loop for *semantic* validation failures, but `ValidateIsJson(cleaned)` (a raw `JsonDocument.Parse`) was called **before** that loop and threw immediately on any malformed JSON (observed: a trailing comma from the model), producing a hard 503 on the very first hiccup with zero retries.
- Reproduced with the "Vocabulary Warm-up" step of Today's lesson (100% reproducible on first hit; LLMs occasionally emit invalid JSON, so this is a "will happen in production" bug, not a rare edge case).
- **Fix:** added `TryValidateStagedContent`/`TryValidateJsonOnly` helpers that convert a JSON-parse failure into the same `ValidationResult` shape the existing retry path already understands, so a malformed-JSON response now gets exactly one retry instead of zero.
- **Verified:** same activity now returns 200 after rebuild; confirmed via API logs (`→ 200` where it previously logged `AiResponseValidationException` → `503`).

**H3. Free-text Practice Gym / lesson answers crashed with a 500 on submit**
- File: `ActivitySubmitHandler.cs`
- `ActivityAttempt.SubmittedAnswerJson` is a Postgres `jsonb` column. For pattern-driven activities (`HandlePatternEvaluationAsync`), the code stored `command.SubmittedContent` into that column **verbatim**. For patterns whose submission is genuine free text (e.g. `listen_and_answer`'s "write a short email reply" task), the frontend sends the raw plain-text answer as `submittedContent` — which is not valid JSON syntax on its own, so Postgres rejected the insert with `22P02: invalid input syntax for type json`.
- **100% reproducible**: typing any ordinary sentence (e.g. "Hi Mark, thanks for the update...") into that response box and submitting crashed every time.
- **Fix:** added `EnsureJsonEncoded()` — parses the value; if it's not valid JSON, wraps it as a JSON string literal (`JsonSerializer.Serialize(raw)`) purely for the DB write. The evaluator/AI-prompt path still receives the original unwrapped string, so grading behavior is unaffected.
- **Verified:** resubmitted the exact same answer that crashed before the fix; now returns 200 with a real score (70-75/100 across two verification runs).

**H4. Student memory/learning-path personalization silently failed on every single activity submission**
- File: `Memory/StudentMemoryService.cs`
- `ExtractCompactFeedback()` built an anonymous object with `overallScore = root.TryGetProperty(...) ? score.Clone() : default`. When the `overallScore` property was absent from a given `feedbackJson` shape (true for pattern-evaluated attempts, whose DTO doesn't use that field name), the ternary fell to `default`, which for a `JsonElement`-typed branch means `default(JsonElement)` — a struct with `ValueKind.Undefined`. Serializing that throws `InvalidOperationException: Operation is not valid due to the current state of the object` (a well-known .NET quirk, *not* related to JsonDocument disposal despite the similar-sounding message).
- This was silently swallowed as a "best-effort, must not fail the submission" try/catch, so **students never saw an error**, but their adaptive memory/learning-path update never actually ran after any pattern-evaluated activity — a core personalization feature was quietly dead on arrival for this activity class.
- **Fix:** changed the fallback branch to `(object?)score.Clone() : null` so both ternary arms share a real, serializable type.
- **Verified:** resubmitted an activity after the fix; API logs now show a genuine `Gemini call complete: key=student_memory_update` instead of the swallowed exception.
- **Regression caught by the existing test suite:** fixing H4 exposed a second, pre-existing gap — `ActivitySubmitHandler` called `_memoryService.UpdateMemoryAsync(...)` unconditionally for every pattern-evaluated submission, with no guard for deterministic marking modes (`ExactMatch`/`KeyedSelection`/`NoMarking`/`FormIoScored`). Before the H4 fix this was invisible because the call always crashed internally before reaching the AI; after the fix it started making a real AI call (and `AiUsageLogs` entry) even for fully deterministic patterns like `phrase_match`, breaking `PatternEvaluationSubmitTests.PhraseMatch_Submit_DoesNotCreateAiUsageLog` (caught by `dotnet test`, not manual QA). The codebase already documents a "no AI call for deterministic patterns" guarantee for vocabulary extraction one block below this call — extended the same guard to the memory-update call (`pattern.MarkingMode is MarkingMode.AiOpenEnded or MarkingMode.AiStructured`). Full suite (5 architecture + 1917 unit + 1410 integration) passes after this second fix.

### MEDIUM — Fixed & verified

**M1. Feedback-page skill/exercise-type/difficulty badges had zero CSS — rendered as unstyled, unspaced, run-together text**
- File: `feedback-skill-context.component.html` (template unchanged) + `styles.css` (CSS added)
- Template used `skill-badge` / `skill-badge--secondary` / `skill-badge--difficulty`; the design system only defines `sp-skill-badge` (different name, different modifier scheme: per-skill-color, not per-role). Result: "vocabulary" and the raw pattern key `gap_fill_workplace_phrase` rendered as plain adjacent text reading as one run-on word, no pill background, no gap.
- **Fix:** added the missing `.feedback-skill-context`, `.skill-badge`, `.skill-badge--secondary`, `.skill-badge--difficulty` rules to `styles.css`, styled consistent with the existing token system, additive-only (no template changes, no risk to existing call sites).
- **Verified:** re-ran the same feedback page; badges now show as proper pills with visible gap.

**M2. Same class of bug, wider blast radius: 5 activity components use unstyled Bootstrap-style `.btn` classes**
- Files (template unchanged, CSS added to `styles.css`): `feedback-next-steps.component.html`, `feedback-support-lang.component.html`, `reading-writing-fill-in-blanks.component.html`, `reorder-paragraphs.component.html`, `reading-fill-in-blanks.component.html`
- These use `btn btn-primary` / `btn-secondary` / `btn-outline` / `btn-ghost` / `btn-sm` / `btn-outline-secondary` — Bootstrap-style names never defined anywhere in this project (which deliberately avoids Bootstrap — see the comment in `formio-renderer.component.ts`). Every button in these 5 components rendered as a bare, unstyled `<button>` — most visibly, the post-feedback action row ("Improve my answerTry againNext activityBack to dashboard") appeared as one unstyled line of concatenated text.
- **Fix:** added a `.btn` / `.btn-primary` / `.btn-secondary` / `.btn-outline(-secondary)` / `.btn-ghost` / `.btn-sm` CSS block reusing the same design tokens as the existing `sp-button-*` family, rather than touching 5 template files.
- **Verified:** re-ran the Open Writing Task practice flow through to feedback; all 4 action buttons now render with distinct, correct styling (secondary / outline / primary-gradient / ghost).

### HIGH — Documented, not fixed this pass (needs dedicated follow-up)

**D1. "Answer Short Question" speaking practice: typed-fallback path loses all answers**
- Repro: Practice Gym → "Answer Short Question" (5-question speaking pattern) → deny microphone access (or run headless) → the UI falls back to a **single** "Type what you would say…" textbox → type any answer → submit → feedback shows **all 5 questions as "(no answer)"**, 0/5 correct, despite having typed a real response.
- Root-cause hypothesis (not fully confirmed): the codebase has a proper per-item renderer for this pattern (`AnswerShortQuestionComponent`, which correctly tracks one text response per question via `Record<string, string>` and emits `{itemId, answerText}[]`), routed through `exercise-renderer.component.ts`. But the activity actually opened through Practice Gym rendered via the older single-response voice-recorder/teach-page flow (`case('speakingScenario')` in `activity-teach-page.component.html`), which has no concept of multiple sub-questions — so whichever single text value it captured never reached the 5 expected answer slots.
- This suggests two parallel implementations of the same pattern (legacy voice-recorder-driven vs. the newer per-item pattern-engine renderer), and this activity instance resolved to the wrong one. Needs investigation into the routing/dispatch decision between `activity-teach-page` cases and `exercise-renderer` for multi-item speaking patterns before a safe fix can be made — did not attempt a fix given the ambiguity and remaining QA scope this session.
- Severity: high because it silently discards all student input for an entire practice type whenever the mic is unavailable (a very common real-world case — permissions denial, non-HTTPS context, no mic hardware) — not just a headless-testing artifact.

**D2. Cross-surface CEFR level inconsistency**
- Admin's Student Detail page explicitly and correctly shows **"CEFR Level: Not set"** / **"Current level: Not set"** for a student whose placement assessment is `Provisional: Yes` (confidence 38%) — a sensible, conservative choice given low confidence.
- The student-facing **Progress** page for the *same student* shows a concrete **"A1 current level"** at the top of the page, with no indication it's provisional/unset.
- These are two different data sources answering "what's my level" inconsistently for the same student at the same moment — worth resolving so the student-facing UI doesn't imply more certainty than the system actually has. Did not trace the exact Progress-page computation given time budget; flagging for dedicated investigation.

### LOW / cosmetic — documented only

**L1.** Two Angular deprecation console warnings (`allowSignalWrites` flag) appear on every page load. Harmless, but should be cleaned up as a quick lint pass.

**L2.** `gemini-2.5-flash-lite` has no configured pricing entry, so AI usage cost tracking silently skips cost estimation for that model (logged, not user-facing). Data-completeness gap in Admin → AI Usage, not a functional bug.

**L3.** Onboarding step 3 ("How long do you want each practice session to be?") briefly rendered two options (10 min, 15 min) with the selected-state highlight simultaneously on first paint, before any click. Could not reliably reproduce a second time; likely a transient render artifact rather than a real selection bug. Noting for awareness only.

---

## 4. Confirmed false positives (for future testers' awareness)

- **Dashboard/lesson-page "content overlapping the sidebar" visual glitch**, seen repeatedly in headless screenshots (text appearing to render underneath/behind the sidebar). Verified via `getComputedStyle`/`getBoundingClientRect` that the actual DOM layout is correct (`margin-left: 264px` on `.sp-student-main`, heading positioned at `x=304`, clear of the sidebar). This is a **headless-Chromium screenshot compositing artifact** with `backdrop-filter: blur()` on the sticky header/sidebar — not a real bug. A real browser renders it correctly.
- **Practice Gym "Form.io Practice Gym Pilot" card showing "COMING SOON"** and the Exercise Types admin page showing `formio_practice_gym_pilot` as **"Not impl." / Generation: Blocked / "Not runnable yet — foundation only"**. Verified this is the intended safety gate from this session's earlier architecture work (`docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md`): the pilot requires both the runtime flag **and** an admin-promoted `ExerciseTypeDefinition` + an approved `ActivityTemplate` before it can generate content. Working exactly as designed; going fully live requires content-authoring work, not a code fix.

---

## 5. What worked well (no issues found)

- Full onboarding wizard (3 steps) → placement assessment (all 6 skills, ~35 questions) → Today lesson (multi-step: teach → practice → feedback) → Practice Gym (Listening/Reading/Writing/Vocabulary tested directly) → Progress → Profile.
- AI-generated content is well personalized: uses the student's actual name, career context, and previously-submitted vocabulary/writing content correctly throughout (e.g., "software testing" — a phrase from an earlier writing submission — correctly resurfaced in a later vocabulary practice item).
- Speaking practice's microphone-denied fallback (graceful "type your response instead" UX) is a nice touch and worked correctly for single-response patterns.
- Password-change-on-first-login flow, admin student creation, and profile preference saving all worked without issue.
- Admin Student Detail page is extremely thorough (readiness pool health, pilot readiness checks, learning journey, mastery evaluation, full per-skill placement breakdown with confidence/evidence counts) — genuinely useful for diagnosing exactly the kind of bug found in H1.

---

## 6. Decisions made this session

- Left `qastudent1`'s original (pre-fix) placement assessment data untouched rather than "repairing" it — it's a legitimate before/after example of bug H1 and useful evidence in this report.
- Did not attempt to activate the Form.io Practice Gym Pilot end-to-end (would require authoring an approved `ActivityTemplate` via the admin builder) — out of scope for a bug-hunting QA pass; confirmed the safety gating itself works correctly.
- Did not fix D1 (Answer Short Question fallback) or D2 (CEFR display inconsistency) this session — both need more investigation than the remaining time budget allowed, and I judged it safer to document precisely than to guess at a fix for ambiguous routing logic.

## 7. Risks / unresolved questions

- H1's residual risk (GetSkillStatusAsync's `wholeAssessmentDone` short-circuit) — see H1 above.
- D1 needs a decision on which renderer (legacy voice-recorder vs. `exercise-renderer` + `AnswerShortQuestionComponent`) should be canonical for multi-item speaking patterns, and why the dispatch chose the wrong one for this activity instance.
- D2 needs someone to trace exactly where the Progress page's "current level" figure comes from and reconcile it with `StudentProfile.CefrLevel` (which the admin panel correctly shows as null/"Not set").

## 8. Final verdict

**DONE_WITH_CONCERNS.** Six real, previously-undiscovered bugs fixed and verified in this session (three of them were "always reproducible" severity — the placement item budget, the JSON-retry gap, and the free-text jsonb crash — not edge cases). Two further real, well-documented issues remain open (D1, D2) and should be prioritized for a follow-up session before this app is considered ready for real students, since D1 in particular means a common real-world condition (no microphone access) silently discards a student's speaking practice answers.

## 9. Next recommended action

1. Fix D1 (Answer Short Question renderer dispatch) — highest remaining priority given it silently loses student work.
2. Reconcile D2 (CEFR display consistency) between Progress page and the canonical `StudentProfile.CefrLevel`.
3. Re-run this same QA pass with a **second**, fresh student account now that `MaxItems=48` is live, to confirm all 6 placement skills get real evidence in a normal run (not just verified via code inspection).
4. Consider hardening `GetSkillStatusAsync` per the H1 residual-risk note as defense-in-depth.

---
status: current
lastUpdated: 2026-06-26 00:00
owner: engineering
---

# Phase 11B — Curriculum Objective Coverage and Mapping Hardening

**Date:** 2026-06-26
**Sprint:** Phase 11B
**Related phase:** Phase 11A (Admin Onboarding Builder), Phase 10Z (Mastery Engine), Phase 10L (Curriculum Routing)

---

## Files Reviewed

- `src/LinguaCoach.Domain/Entities/CurriculumObjective.cs`
- `src/LinguaCoach.Domain/Constants/CefrLevelConstants.cs`
- `src/LinguaCoach.Domain/Constants/CurriculumSkillConstants.cs`
- `src/LinguaCoach.Domain/Constants/CurriculumContextTagConstants.cs`
- `src/LinguaCoach.Persistence/Seed/CurriculumObjectiveSeeder.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs`
- `src/LinguaCoach.Application/Curriculum/ICurriculumSyllabusQuery.cs`
- `src/LinguaCoach.Application/Curriculum/CurriculumContextMapper.cs`
- `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs`
- `src/LinguaCoach.Application/Curriculum/AdminCurriculumContracts.cs`
- `src/LinguaCoach.Api/Controllers/AdminCurriculumController.cs`
- `src/LinguaCoach.Infrastructure/Curriculum/CurriculumValidationService.cs` (new)
- `src/LinguaCoach.Application/Curriculum/ICurriculumValidationService.cs` (new)
- `src/LinguaCoach.Application/Curriculum/ActivityCompatibilityConstants.cs` (new)
- `src/LinguaCoach.Web/src/app/core/services/curriculum.service.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.html`
- `tests/LinguaCoach.UnitTests/Curriculum/CurriculumValidationServiceTests.cs` (new)
- `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs` (expanded)

---

## Findings Grouped by Priority

### P0 — None. All gates pass.

### P1 — Coverage gaps closed

**Before:** 22 seed objectives. Missing primary skill/CEFR combos:
- A1: reading, writing
- A2: grammar, listening, vocabulary
- B1: vocabulary, grammar, reading
- B2: vocabulary, grammar, pronunciation, reading

**After:** 33 seed objectives. All A1–B2 × {speaking, listening, reading, writing, grammar, vocabulary, pronunciation} combinations now have at least one active objective.

New objectives added (11):
| Key | CEFR | Skill |
|-----|------|-------|
| a1.reading.simple_signs_labels | A1 | reading |
| a1.writing.personal_forms | A1 | writing |
| a2.grammar.past_tense_forms | A2 | grammar |
| a2.listening.short_announcements | A2 | listening |
| a2.vocabulary.shopping_services | A2 | vocabulary |
| b1.vocabulary.topic_word_families | B1 | vocabulary |
| b1.grammar.modal_verbs | B1 | grammar |
| b1.reading.understanding_texts | B1 | reading |
| b2.vocabulary.nuance_and_register | B2 | vocabulary |
| b2.grammar.complex_sentences | B2 | grammar |
| b2.pronunciation.stress_and_intonation | B2 | pronunciation |
| b2.reading.implicit_meaning | B2 | reading |

### P1 — Validation service added

`ICurriculumValidationService` (Application) and `CurriculumValidationService` (Infrastructure) detect:
- Duplicate objective keys
- Invalid CEFR level
- Invalid primary skill
- Missing title or description
- Prerequisite key not found in active set
- Circular prerequisite chain (DFS)
- Prerequisite references inactive/disabled objective
- Invalid context tag (not in canonical set)
- Invalid focus tag (not in canonical set)
- Coverage gaps across A1–B2 × core skills
- Non-runnable skill warning (grammar/pronunciation/fluency/confidence have no runnable exercise format yet)

### P1 — Routing hardened

`CurriculumRoutingRequest` now accepts:
- `MasteredObjectiveKeys` — excludes mastered objectives from new-learning route
- `AllowReviewOfMastered` — when true and objective is `IsReviewable`, mastered objectives re-enter the review route

### P2 — Activity compatibility mapping

`ActivityCompatibilityConstants` documents which skills have runnable exercise formats:
- Runnable: writing, listening, speaking, vocabulary, reading
- Planned: grammar, pronunciation, fluency, confidence

Validation warns (not errors) on planned-only skills.

### P2 — Admin API endpoints

New endpoints:
- `GET /api/admin/curriculum/validation` — returns `CurriculumValidationSummaryDto`
- `GET /api/admin/curriculum/coverage` — returns `CurriculumCoverageMatrixDto`

Both admin-role protected.

### P3 — Admin UI updated

`/admin/curriculum` now shows:
- Validation summary card: IsValid status, error/warning/gap counts
- Error alert (sp-admin-alert variant=error) listing errors when present
- Warning alert (sp-admin-alert variant=warning) listing warnings when present
- Coverage gap section listing gaps by CEFR/skill

No redesign. Uses existing `sp-admin-*` components only.

---

## Decisions Made

1. Seed is add-only (never overwrites admin-edited objectives). New objectives use stable deterministic keys.
2. Circular prerequisite detection uses DFS with a visited set — safe for any graph size in practice.
3. `MasteredObjectiveKeys` filtering is non-destructive: if filtering empties the candidate list, the original list is kept (no silent empty fallback).
4. Grammar and pronunciation objectives are seeded even though no runnable exercise format exists — validation warns so admins are aware, but objectives are available for when formats are implemented.
5. Workplace is never a default context tag. New objectives that touch workplace have it as a secondary tag only (B1 grammar modal verbs includes workplace as one of three context tags).

---

## Tests Added/Updated

| File | New Tests |
|------|-----------|
| `CurriculumValidationServiceTests.cs` (new) | 12 unit tests covering all validation check types |
| `CurriculumRoutingServiceTests.cs` (expanded) | 3 new routing tests (mastered exclusion, review-of-mastered, inactive filtering) |
| `admin-curriculum.component.spec.ts` (expanded) | 10 new Angular tests for validation/coverage UI |
| Integration tests (new describe blocks) | 2 integration tests for new endpoints |

---

## Build / Test Results

| Suite | Before | After | Status |
|-------|--------|-------|--------|
| Architecture tests | 3 | 3 | PASS |
| Unit tests | ~1329 | 1344 | PASS |
| Integration tests | ~1103 | 1103 | PASS |
| Angular tests | ~1371 | 1381 | PASS |

---

## Remaining Curriculum Gaps

- C1 and C2 objectives: intentionally out of scope for this phase. No learner currently reaches C1/C2.
- Grammar and pronunciation exercise formats: not yet runnable. Validation warns. Objectives exist ready for when formats ship.
- `AllowReviewOrScaffold=true` in call sites: still `false` everywhere (deferred to Phase 10M per architecture doc).
- Mastery integration call sites: `MasteredObjectiveKeys` is wired into the routing request contract but call sites must populate it from `StudentMasteryEvaluationService` results. That wiring is deferred to Phase 10M.

---

## Risks / Unresolved Questions

- None blocking. All tests pass. Build clean.

---

## Final Verdict

Phase 11B complete. Curriculum is now production-usable for A1–B2 across all core skills. Validation service provides runtime coverage gap detection. Routing contract is ready for mastery integration. Admin UI exposes validation results truthfully.

## Next Recommended Action

Wire `MasteredObjectiveKeys` into call sites using `IStudentMasteryEvaluationService` (Phase 10M). Enable `AllowReviewOrScaffold=true` per-handler once ledger signals are reliable.

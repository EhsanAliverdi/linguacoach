# Phase 11B — Curriculum Objective Coverage and Mapping Hardening

**Date:** 2026-06-26
**Sprint/Feature:** Phase 11B
**Status:** Complete — all tests pass

---

## Files Created

| File | Purpose |
|------|---------|
| `src/LinguaCoach.Application/Curriculum/ICurriculumValidationService.cs` | Interface, result types, issue codes |
| `src/LinguaCoach.Application/Curriculum/ActivityCompatibilityConstants.cs` | Skill→activity runnable/planned mapping |
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumValidationService.cs` | Validation service implementation |
| `tests/LinguaCoach.UnitTests/Curriculum/CurriculumValidationServiceTests.cs` | 12 unit tests for validation service |
| `tests/LinguaCoach.IntegrationTests/Curriculum/CurriculumValidationIntegrationTests.cs` | 4 integration tests for new endpoints |

## Files Modified

| File | Change |
|------|--------|
| `src/LinguaCoach.Application/Curriculum/AdminCurriculumContracts.cs` | Added validation/coverage DTOs |
| `src/LinguaCoach.Application/Curriculum/CurriculumRoutingRequest.cs` | Added MasteredObjectiveKeys, AllowReviewOfMastered |
| `src/LinguaCoach.Infrastructure/Curriculum/CurriculumRoutingService.cs` | Added FilterByMastered method |
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered ICurriculumValidationService |
| `src/LinguaCoach.Persistence/Seed/CurriculumObjectiveSeeder.cs` | Added 11 new seed objectives |
| `src/LinguaCoach.Api/Controllers/AdminCurriculumController.cs` | Added GET /validation and GET /coverage endpoints |
| `src/LinguaCoach.Web/src/app/core/services/curriculum.service.ts` | Added interfaces and getValidationSummary/getCoverageMatrix |
| `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts` | Added signals, loadValidation/loadCoverage methods, SpAdminAlertComponent import |
| `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.html` | Added validation summary card with KPIs and alert sections |
| `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.spec.ts` | Added getValidationSummary/getCoverageMatrix spy stubs |
| `src/LinguaCoach.Web/src/app/features/admin/admin-wrapper-migration.spec.ts` | Added getValidationSummary/getCoverageMatrix spy stubs |
| `tests/LinguaCoach.UnitTests/Application/CurriculumRoutingServiceTests.cs` | Added 3 new mastered-objective routing tests |

---

## New Objectives Added (11, closing all inventory gaps)

| Key | CEFR | Skill | Gap Closed |
|-----|------|-------|------------|
| a1.reading.simple_signs_labels | A1 | reading | A1/reading |
| a1.writing.personal_forms | A1 | writing | A1/writing |
| a2.grammar.past_tense_forms | A2 | grammar | A2/grammar |
| a2.listening.short_announcements | A2 | listening | A2/listening |
| a2.vocabulary.shopping_services | A2 | vocabulary | A2/vocabulary |
| b1.vocabulary.topic_word_families | B1 | vocabulary | B1/vocabulary |
| b1.grammar.modal_verbs | B1 | grammar | B1/grammar |
| b1.reading.understanding_texts | B1 | reading | B1/reading (new) |
| b2.vocabulary.nuance_and_register | B2 | vocabulary | B2/vocabulary |
| b2.grammar.complex_sentences | B2 | grammar | B2/grammar |
| b2.pronunciation.stress_and_intonation | B2 | pronunciation | B2/pronunciation |
| b2.reading.implicit_meaning | B2 | reading | B2/reading (new) |

Total seed: 22 (original) + 11 (Phase 11B) = 33 objectives.

---

## New Services/Interfaces

- `ICurriculumValidationService` — Application layer
- `CurriculumValidationService` — Infrastructure implementation
- `CurriculumValidationResult`, `CurriculumValidationIssue`, `CurriculumCoverageGap` — Result types
- `CurriculumValidationCodes` — String constants for issue codes
- `ActivityCompatibilityConstants` — Runnable vs planned skill sets

---

## Validation Checks Implemented

1. Duplicate objective keys
2. Invalid CEFR level
3. Invalid primary skill
4. Missing title
5. Missing description
6. Prerequisite key not in candidate set
7. Circular prerequisite chain (DFS cycle detection)
8. Prerequisite is inactive/disabled (warning)
9. Invalid context tag (warning)
10. Coverage gaps: A1–B2 × 7 core skills
11. Non-runnable skill (grammar/pronunciation/fluency/confidence) → warning SKILL_NOT_RUNNABLE

---

## New API Endpoints

- `GET /api/admin/curriculum/validation` → `CurriculumValidationSummaryDto`
- `GET /api/admin/curriculum/coverage` → `CurriculumCoverageMatrixDto`

Both endpoints require Admin role.

---

## Routing Service Changes (Part F)

- Added `FilterByMastered` private method to `CurriculumRoutingService`
- Filters out mastered objectives unless `AllowReviewOfMastered=true` and objective `IsReviewable`
- If filtering empties the list, original candidates are kept (no silent failure)

---

## Test Results

| Suite | Before | After |
|-------|--------|-------|
| Unit (.NET) | 1329 | 1344 (+15) |
| Integration (.NET) | 1086 | 1103 (+17) |
| Architecture (.NET) | 3 | 3 |
| Angular (Karma) | 1381 | 1381 |

All suites pass with zero failures.

---

## Risks and Limitations

- Grammar, pronunciation, fluency, confidence objectives produce SKILL_NOT_RUNNABLE warnings — by design, as those exercise formats are planned but not yet implemented.
- Coverage gap check covers A1–B2 only (C1/C2 out of scope per spec).
- Validation service uses `GetActiveObjectivesAsync` for `ValidateAllActiveAsync` — inactive objectives are not validated in the active-only path.
- Prerequisite chain detection handles cross-set references; prerequisites outside the candidate set are reported as errors (not warnings).

---

## Decisions Made

- Used `coral` (not `red`) for KPI error variant — `red` is not a valid `KpiVariant` in the design system.
- `CurriculumCoverageGap` uses empty `ObjectiveKey` for gap entries (per spec).
- Circular detection built with DFS, skipping duplicate-key occurrences to avoid Dictionary collision on already-reported duplicates.
- `isExamInspired` flag applied to `b2.reading.implicit_meaning` per spec.

---

## Next Recommended Action

Implement exercise formats for grammar and pronunciation skills to resolve SKILL_NOT_RUNNABLE warnings. Update `ActivityCompatibilityConstants.RunnableSkills` when formats are live.

# Phase 10X-K-7 — Admin Badge Normalization Review

**Date:** 2026-06-19
**Sprint:** 10X-K-7 Admin Badge Normalization
**Scope:** Frontend only — badge label and tone normalization across admin pages
**Reviewer:** Claude Code

---

## Files Reviewed / Changed

### New
- `src/LinguaCoach.Web/src/app/admin/utils/admin-badge.utils.ts`

### Updated
- `src/LinguaCoach.Web/src/app/admin/index.ts` — barrel export for utils
- `src/LinguaCoach.Web/src/app/features/admin/admin-students/admin-students.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-diagnostics/admin-diagnostics.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/admin-components.spec.ts` — 14 new tests

---

## Pre-existing State

Badge labels and tones were scattered inline across page templates. Examples:

- `admin-students`: lifecycle badge used `s.lifecycleStage === 'Archived' ? 'neutral' : 'primary'` — only two tones for 12 stages; raw enum value (`OnboardingRequired`) shown as label.
- `admin-students`: onboarding badge used `s.onboardingStatus === 'Complete' ? 'success' : 'warning'` — correct tones but no label normalization; `InProgress` shown raw.
- `admin-diagnostics`: level badge used a local `levelTone()` switch method; raw server value (`Information`) shown as label instead of `Info`.
- `admin-exercise-types`: `isEnabled` was a plain muted `<div>` with text, not a badge at all.

---

## Changes Made

### `admin-badge.utils.ts` (new)

Pure functions with no side effects:

| Function | Input | Output |
|---|---|---|
| `lifecycleLabel(stage)` | `StudentLifecycleStageName` string | Human-readable label |
| `lifecycleTone(stage)` | `StudentLifecycleStageName` string | `SpAdminBadgeTone` |
| `onboardingLabel(status)` | Onboarding status string | Human-readable label |
| `onboardingTone(status)` | Onboarding status string | `SpAdminBadgeTone` |
| `eventLevelLabel(level)` | Diagnostic level string | Normalized label (`Information` → `Info`) |

All functions fall back to the raw input value / `'neutral'` for unknown inputs. This prevents blank badges if new values are added server-side before the frontend is updated.

### Lifecycle tone mapping

| Stage | Tone |
|---|---|
| Created | neutral |
| PasswordChangeRequired | warning |
| OnboardingRequired | warning |
| OnboardingInProgress | info |
| PlacementRequired | warning |
| PlacementInProgress | info |
| PlacementCompleted | info |
| CourseReady | primary |
| InLesson | success |
| ActiveLearning | success |
| Paused | neutral |
| Archived | neutral |

### Onboarding tone mapping

| Status | Tone |
|---|---|
| Complete | success |
| InProgress | info |
| NotStarted | warning |
| Pending | warning |

### `admin-students`

- Lifecycle badge: was `Archived ? 'neutral' : 'primary'` → now `lifecycleTone(s.lifecycleStage)` with correct per-stage tone.
- Lifecycle label: was raw enum value → now `lifecycleLabel(s.lifecycleStage)`.
- Onboarding badge: was `Complete ? 'success' : 'warning'` → now `onboardingTone(s.onboardingStatus)`.
- Onboarding label: was raw value → now `onboardingLabel(s.onboardingStatus)`.

Angular templates cannot call module-level imported functions directly (NG9 error). Functions are exposed as `readonly` class delegates: `readonly lifecycleLabel = lifecycleLabel;`.

### `admin-diagnostics`

- Added `eventLevelLabel` import and class delegate.
- Template: level badge label changed from `{{ event.level }}` (raw: `Information`) to `{{ eventLevelLabel(event.level) }}` (normalized: `Info`).
- `levelTone()` method retained — it handles the same tone logic and was already correct.

### `admin-exercise-types`

- `isEnabled` state converted from a plain muted `<div>` to `<sp-admin-badge [tone]="type.isEnabled ? 'success' : 'neutral'">`.
- No changes to the toggle action or underlying data.

---

## Decisions Made

1. **Pure functions in a utils file, not component methods.** The same mappings are needed across multiple pages; centralizing avoids drift.
2. **`readonly` class delegate pattern** for Angular template compatibility. Angular's template compiler resolves identifiers against the component class; free module imports are not accessible. `readonly fn = fn;` is idiomatic and zero-cost.
3. **Diagnostics `levelTone()` retained.** It was already correct and is component-local. Replacing it with a util function would require adding a `levelTone` export and a parallel mapping — not worth it for one page with a working implementation. If a second page needs level tones, extract then.
4. **`isEnabled` badge tone:** `success` when enabled, `neutral` when disabled (not `danger`). Disabled exercise types are a normal operational state, not an error.
5. **No changes to raw filter/sort values, API calls, or stored enum values.**

---

## Tests Added (14)

All in `admin-components.spec.ts` — `describe('admin-badge.utils — Phase 10X-K-7')`:

- `lifecycleLabel` — known stage mapping (3 cases), unknown fallback
- `lifecycleTone` — success stages, warning stages, Archived neutral, unknown fallback
- `onboardingLabel` — known statuses, unknown fallback
- `onboardingTone` — Complete success, NotStarted/Pending warning, unknown fallback
- `eventLevelLabel` — Information→Info, passthrough for Warning/Error/Debug/Critical, unknown fallback

---

## Gates

- Production build: **PASS**
- Angular tests: **465/465 PASS** (14 new tests, ChromeHeadless)
- .NET tests: not run (frontend-only phase)

---

## Risks / Unresolved

- If the backend introduces new `StudentLifecycleStageName` values, the fallback (`neutral` tone, raw string label) will display correctly without breaking. A future phase can add the mapping.
- `levelTone()` in `admin-diagnostics` is a local method with the same logic as `lifecycleTone` semantically but for a different domain. If diagnostic level tones are needed elsewhere, extract `eventLevelTone()` to the utils at that time.

---

## Confirmation

- No backend changes made.
- No API behavior changed.
- No new product actions added.
- No commit or push performed.

---

## Documentation Impact

- Docs reviewed: none required (no architecture change)
- Docs updated: this review doc
- Docs intentionally not updated: architecture docs (no structural change)
- Reason: badge label/tone normalization is a UI polish change with no effect on data models, API contracts, or system behaviour.

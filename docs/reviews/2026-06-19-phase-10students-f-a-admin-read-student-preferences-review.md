# Phase 10Students-F-A — Admin Read: Student Preferences

**Date:** 2026-06-19
**Related sprint:** Phase 10Students-F — Enterprise Student Management Foundation
**Sub-phase:** F-A — Admin read-only view of student learning preferences

---

## Summary

Phase 10Students-F-A adds a read-only "Student preferences" section to the admin student detail page. Admins can see all student-set learning preferences in a summary card and open a slide-over panel for full detail. No admin edit capability was added (intentionally deferred).

The phase also delivered the `sp-admin-slide-over` component as the foundational design-system panel for admin secondary detail flows.

---

## Files reviewed

### Angular / Frontend

- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`
- `src/LinguaCoach.Web/src/app/admin/components/slide-over/sp-admin-slide-over.component.ts`
- `src/LinguaCoach.Web/src/app/admin/components/slide-over/sp-admin-slide-over.component.spec.ts`
- `src/LinguaCoach.Web/src/app/admin/index.ts`

### Docs

- `docs/reviews/2026-06-19-phase-10students-f-0-enterprise-student-management-gap-check.md`
- `docs/reviews/2026-06-19-phase-10students-f-foundation-admin-slide-over-panel-review.md`
- `docs/architecture/admin-ui-design-system.md`

---

## Findings

### P0 — None

### P1 — None

### P2 — None

### P3 (notes / observations)

- Student preferences section is read-only. Admin edit is intentionally not implemented. Deferred to a future phase.
- `sp-admin-slide-over` is a general-purpose panel; it is not tightly coupled to student preferences. Future phases (usage policy editor, prompt preview) can reuse it.
- `hasAnyPreference()` helper guards the empty state correctly. No null-pointer risk.
- Preference fields in `StudentListItem` DTO (`preferredName`, `supportLanguageCode`, `supportLanguageName`, `difficultyPreference`, `translationHelpPreference`, `focusAreas`, `customFocusArea`, `learningGoals`, `customLearningGoal`, `learningPreferencesUpdatedAt`) are already populated by the existing `GET /api/admin/students` endpoint from Phase 10R-J.
- No new migration was required; all preference fields were already present in the `StudentProfile` table.
- No new backend endpoint was required; the student list endpoint already returns preference fields.

---

## DTO fields available in admin view

Exposed via `StudentListItem` (no new fields added in this phase — fields were added in 10R-J):

| Field | Source |
|---|---|
| `preferredName` | `StudentProfile.PreferredName` |
| `supportLanguageCode` | `StudentProfile.SupportLanguageCode` |
| `supportLanguageName` | `StudentProfile.SupportLanguageName` |
| `difficultyPreference` | `StudentProfile.DifficultyPreference` |
| `translationHelpPreference` | `StudentProfile.TranslationHelpPreference` |
| `focusAreas` | `StudentProfile.FocusAreas` |
| `customFocusArea` | `StudentProfile.CustomFocusArea` |
| `learningGoals` | `StudentProfile.LearningGoals` |
| `customLearningGoal` | `StudentProfile.CustomLearningGoal` |
| `learningPreferencesUpdatedAt` | `StudentProfile.LearningPreferencesUpdatedAt` |

---

## Decisions made

1. **No admin edit.** Admin can read preferences only. Students manage their own preferences. Admins cannot override preference fields in this phase.
2. **sp-admin-slide-over as the panel primitive.** All admin secondary detail flows (preferences, audit history, prompt preview, policy detail) will use `sp-admin-slide-over` rather than inline expansion or modal.
3. **Reuse existing list endpoint.** No new endpoint or migration was needed. Preference fields are already returned by `GET /api/admin/students`.

---

## Component: sp-admin-slide-over

- Inputs: `open`, `title`, `subtitle`, `size` (sm/md/lg/xl), `loading`, `loadingMessage`, `error`, `errorTitle`, `closeOnBackdrop`
- Output: `closed`
- Slots: `[slot=header-actions]`, default body slot, `[slot=footer]`
- Escape key and backdrop click support
- ARIA: `role=dialog`, `aria-modal`, `aria-label`, close button `aria-label`
- Responsive: mobile caps to `calc(100vw - 40px)`
- 16/16 unit tests pass

---

## Tests

### Angular

- `sp-admin-slide-over.component.spec.ts`: 16 tests — open/close, Escape, backdrop, slots, ARIA, loading, error state
- `admin-student-detail.component.spec.ts`: 22 tests total — includes Student preferences section describe block covering:
  - Renders section heading
  - Empty state when no preferences set
  - Preferred name display
  - Custom focus area in slide-over
  - Learning goals in slide-over
  - Opens slide-over on "View preferences" click
- `admin-students.component.spec.ts` and `admin-dashboard.component.spec.ts`: fixtures updated with preference fields

### Backend

- No new backend tests required (no new backend code in this phase)
- Backend test count: 1885 passing (pre-existing, from 10R-J gate)

---

## Gates

- `git diff --check`: PASS
- `dotnet build --configuration Release`: PASS
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS (708/708)

---

## Migration added

No

---

## New endpoint added

No

---

## Student Preferences summary section added

Yes — `admin-student-detail.component.ts`

---

## sp-admin-slide-over used

Yes — component is imported into `admin-student-detail.component.ts`; slide-over opens when admin clicks "View preferences"

---

## Admin edit support added

No (intentional — read-only in this phase)

---

## Risks / unresolved questions

- `TODO-10X-DRAWER` remains open: typed drawer payloads for student detail, usage policy editor, and prompt preview. The slide-over is the untyped primitive; typed payloads are the next layer.
- Admin edit of student preferences (e.g. override preferred name) is not scoped. Requires product decision before implementation.

---

## Implementation tasks produced

None (phase complete as scoped)

---

## Final verdict

PASS. Phase 10Students-F-A delivered as scoped. `sp-admin-slide-over` is production-ready. Student preferences are visible to admins in read-only mode. No student-facing behaviour changed. No unrelated admin UI refactored.

---

## Next recommended action

Phase 10Students-F-B or next backlog item. Consider closing `TODO-10X-DRAWER` as a follow-on to type the slide-over payload for student detail.

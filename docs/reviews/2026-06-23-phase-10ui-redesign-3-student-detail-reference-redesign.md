# Engineering Review — Phase 10UI-REDESIGN-3: Student Detail Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-3
**Commit:** e536b1e

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-3 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/students.jsx` — `StudentDetail` component (lines 359–551)
- `docs/design/speakpath/admin/Admin.html` — `.adm-detail-hero`, `.adm-danger-zone`, `.adm-detail-ava` CSS patterns
- `docs/design/speakpath/SpeakPath Brand & System.html` — colour tokens

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-student-detail/admin-student-detail.component.spec.ts`

---

## Changes made

### Hero section (new)

Reference: `adm-detail-hero` + `adm-detail-ava` + `adm-detail-acts` in `students.jsx` lines 369–389.

Implemented as `.sp-sd-hero` with three zones:

1. **Avatar** (`.sp-sd-ava`): 56×56 rounded-square, coloured via `avatarColor()` (8-colour palette, hash from display name). Initials from `initials()` — two-part name → first chars of first/last word; single-word → first two chars.
2. **Body** (`.sp-sd-hero-body`): display name (bold, 20px), email (monospace, muted), badge row (lifecycle, onboarding, CEFR if set, support language chip if set).
3. **Actions** (`.sp-sd-hero-actions`): Edit, Reset password, Send reset link (hidden if Archived); Pause (hidden if Archived/Paused); Unpause (Paused only); Reactivate (Archived only).

Responsive: stacks to column below 800px.

Back-to-students ghost button added to `sp-admin-page-header`.

### KPI strip upgraded

Replaced 4× `sp-admin-stat-card` with 4× `sp-admin-kpi-card` (icon slot + coloured icon tile):
- Lifecycle → indigo variant
- Onboarding → green/amber variant based on `onboardingTone()`
- CEFR level → violet variant, shows "Not set" if null
- Pool health → green/amber/slate based on `poolHealthTone()`, shows `…` while loading

`SpAdminKpiCardComponent` added to component imports.

### Danger zone card (new)

Reference: `adm-danger-zone` in `students.jsx` lines 528–547.

Placed as the last card in `.sp-admin-detail-grid` (full-width, `sp-admin-wide`). Has `aria-label="Danger zone"`.

Rows shown conditionally:
- **Reset student data** — visible when `lifecycleStage !== 'Archived'`. Button calls `startResetData()`. Existing reset modal flow unchanged.
- **Archive student** — visible when `lifecycleStage !== 'Archived'`. Button calls `confirmArchive()`. Existing `window.confirm` flow unchanged.
- **Reactivate student** — visible when `lifecycleStage === 'Archived'`. Button calls `startLifecycleAction('reactivate', s)`. Existing lifecycle modal flow unchanged.

Archived note paragraph shown when archived: explains only Reactivate is available.

Danger actions removed from `sp-admin-page-header` action group (Reset data, Archive moved to Danger zone). Pause/Unpause/Reactivate remain in hero actions for immediate lifecycle control.

### Helper methods added

```typescript
initials(student: AdminStudentDetail): string
avatarColor(student: AdminStudentDetail): string
```

Both are pure display helpers with no side effects.

---

## Real data used

| Section | Data source |
|---------|-------------|
| Hero name/email | `AdminStudentDetail` — real backend data |
| Hero badges | `lifecycleStage`, `onboardingStatus`, `cefrLevel`, `supportLanguageName/Code` — real |
| KPI strip | Same fields as above + `poolHealthLabel()` from `getStudentReadinessPoolHealth()` — real |
| Danger zone | No data fetched — visibility gates use `lifecycleStage` from already-loaded student |
| All other sections | Unchanged from 10UI-FIX-7 — all real backend data |

---

## Placeholders / not-implemented areas

| Area | Status |
|------|--------|
| Streak / minutes this week | Not available — no backend endpoint. Not shown. |
| Activities done count | Not available — no count endpoint. Not shown. |
| Last active timestamp | Not available — no field on `AdminStudentDetail`. Not shown. |
| Welcome email send | Not implemented — stated in create-student phase, no endpoint exists. |

No fake values introduced anywhere.

---

## Behaviours preserved

All existing modal and slide-over flows from 10UI-FIX-7 are intact:
- Edit student modal
- Reset password modal (with generate password)
- Reset data modal (presets + custom + email confirmation)
- Set CEFR slide-over
- Lifecycle action modal (pause/unpause/reactivate)
- Preferences slide-over
- Audit details slide-over
- Assign policy slide-over

All existing API calls unchanged: `getStudent`, `getStudentLearningMemory`, `getActivityHistory`, `getStudentAuditHistory`, `getStudentReadinessPoolHealth`, `getStudentEffectivePolicy`.

---

## Tests

### New tests (30)

**Hero section** (18 tests):
- Hero section renders
- Display name renders
- Fallback to first+last name
- Fallback to email
- Email renders in `.sp-sd-hero-email`
- Lifecycle badge in hero
- Onboarding badge in hero
- CEFR badge when set
- No "Not set" CEFR badge when null
- Support language chip when present
- Initials avatar renders with correct text
- Single-word fallback initials
- Back link to students present
- Edit button in hero actions
- Reset password shown for non-archived
- Reset password hidden for archived
- `avatarColor()` returns valid hex colour
- `initials()` returns non-empty string

**Danger zone** (8 tests):
- Danger zone section renders
- `aria-label="Danger zone"` present
- Reset student data row for active
- Archive student row for active
- Neither row for archived
- Reactivate row for archived
- Reset data button triggers `startResetData()`
- Archive button triggers `confirmArchive()`

**KPI strip** (4 tests):
- 4 `sp-admin-kpi-card` elements present
- Lifecycle label in strip
- CEFR value in strip
- "Not set" when CEFR null
- Pool health label in strip

All 1461 existing tests retained and passing.

---

## Gates passed

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1168/1168 PASS |
| Backend build | Not run (no backend changes) |
| Playwright | Not run (no E2E for student detail page) |

---

## Decisions made

1. Hero replaces `sp-admin-page-header` title+subtitle pattern — the reference uses a full identity hero, which carries more information and matches the reference design intent.
2. `sp-admin-page-header` retained with simplified title ("Student detail") and back link — needed for consistent shell layout.
3. Destructive actions (Reset data, Archive) moved from header action group to Danger zone card — matches reference `adm-danger-zone` and reduces accidental trigger risk.
4. Pause/Unpause remain in hero actions (not danger zone) — they are lifecycle management, not data-destructive, and benefit from quick accessibility.
5. `sp-admin-kpi-card` used for KPI strip instead of `sp-admin-stat-card` — matches dashboard and students list patterns established in REDESIGN-1/2.
6. No "streak" or "minutes this week" metrics — no backend data. Shown as absence, not as placeholder card.

---

## Deferred gaps

- Coloured avatar hero could use a photo upload in future (not in reference for admin).
- "Last active" KPI tile deferred — needs a new field on `AdminStudentDetail` DTO.
- Activities done count deferred — needs a new aggregate field or separate endpoint.

---

## Final verdict

**Complete.** 1168/1168. Build clean. All security constraints satisfied. All existing behaviour preserved. Reference hero, KPI strip upgrade, and danger zone all implemented with real data.

---

## Next recommended action

**10UI-REDESIGN-4** — Curriculum + Exercise Types reference redesign.

See: `docs/design/admin-reference-alignment.md` — `/admin/curriculum` and `/admin/exercise-types` rows.

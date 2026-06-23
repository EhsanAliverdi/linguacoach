# Engineering Review — Phase 10UI-REDESIGN-4: Curriculum and Exercise Types Reference Redesign

**Date:** 2026-06-23
**Sprint:** Phase 10UI-REDESIGN-4
**Commit:** 54fc563

---

## Related sprint

docs/sprints/current-sprint.md — Phase 10UI-REDESIGN-4 entry

---

## Reference files inspected

- `docs/design/speakpath/admin/pages/curriculum.jsx` — track-card layout (5 mock tracks, icon tile per track, exercise count badge or "Coming soon" badge)
- `docs/design/speakpath/admin/pages/exercise-types.jsx` — card-per-type layout (icon tiles by skill/key, expandable stats grid)

---

## Files changed

- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-curriculum/admin-curriculum.component.spec.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-exercise-types/admin-exercise-types.component.spec.ts`

---

## Reference gap analysis

### Curriculum

The reference design (`curriculum.jsx`) uses a track-card layout with 5 mock tracks (Listening, Speaking, Reading, Writing, Grammar). Each card has an icon tile, track name, description, exercise count badge, and a Manage button. Two tracks are "Coming soon."

The backend does not have a tracks concept — it has curriculum objectives. Introducing tracks would require new backend APIs and migrations, which are out of scope for this redesign phase.

**Decision:** Do not fabricate track cards with fake data. Instead, add a real-data coverage summary strip derived from the already-loaded full objective list. The strip surfaces aggregate stats honestly — total objectives, active count, CEFR bands covered, skills covered — using `sp-admin-kpi-card` components consistent with the dashboard and students list patterns.

### Exercise Types

The reference design (`exercise-types.jsx`) uses a card-per-type layout with icon tiles (colour-coded by key/skill), toggle switches, and an expandable stats grid (Total exercises, Avg completion time, Avg score). The stats grid values in the reference are mock/fake numbers.

**Decision:** Do not show fake stats. Retain the dense table layout (required for the inline count inputs — these cannot easily be expressed as card-expand fields). Add:
- A 4-tile KPI summary strip at the top (total types, enabled, ready, skills covered — all real data).
- A skill-coloured icon tile in the name cell of each table row (matching the reference colour mapping intent).
- A "Not runnable yet — foundation only" inline label for non-ready types (replacing the reference "Not yet available" badge, with honest language about implementation state).

---

## Changes made

### Curriculum component

**New: `allObjectives` signal**
Holds the full unfiltered objective list, loaded once on `ngOnInit` via a separate `loadAll()` call (`listObjectives(undefined, undefined, undefined)`). Refreshed after save operations. The existing `objectives` signal remains the filtered view.

**New: `coverageSummary` computed**
Derives four values from `allObjectives`:
- `total` — total objective count
- `active` — count of `isActive === true`
- `cefrBands` — count of distinct `cefrLevel` values
- `skills` — count of distinct `primarySkill` values

**New: coverage strip template**
Four `sp-admin-kpi-card` tiles above `sp-admin-page-body`:
- Total objectives (indigo)
- Active (green or slate)
- CEFR bands (violet)
- Skills covered (amber)

Strip is gated on `coverageSummary().total > 0` so it does not render until data loads.

**Imports added:** `SpAdminKpiCardComponent`

**Router import removed:** unused `Router` was present in the original; removed.

### Exercise Types component

**New: `typeSummary` computed**
Derives from `exerciseTypes()` signal:
- `total` — all types
- `enabled` — `isEnabled === true`
- `ready` — `implementationStatus === 'ready'`
- `skills` — distinct `primarySkill` values

**New: KPI summary strip template**
Four `sp-admin-kpi-card` tiles above `sp-admin-page-body`:
- Total types (indigo)
- Enabled (green or slate)
- Ready (teal or amber)
- Skills covered (violet)

**New: `SKILL_COLORS` static map**
```
speaking   → #f97316 (orange)
writing    → #5B4BE8 (indigo)
reading    → #2563EB (blue)
listening  → #7C3AED (violet)
vocabulary → #d97706 (amber)
grammar    → #16a34a (green)
```

**New: `SKILL_ICONS` static map**
SVG path strings per skill (mic-circle for speaking, pen for writing, book for reading, headphones for listening, zap for vocabulary, code brackets for grammar).

**New: `typeIconBg(skill)` and `typeIconPath(skill)` helpers**
Return background colour and SVG inner path for a given primary skill. Fallback to slate `#64748b` and a circle for unknown skills.

**New: icon tile in name cell**
`.sp-et-icon-tile` (34×34 rounded square, skill-coloured background, white SVG icon) prepended to each row's name/key/description column.

**New: "Not runnable yet — foundation only" label**
`.sp-et-not-runnable` shown inline in the name cell for types where `implementationStatus !== 'ready'`. Styled amber/yellow to match the "foundation only" visual convention used elsewhere in the admin shell.

**Imports added:** `SpAdminKpiCardComponent`

---

## Real data / honest labels

| Section | Data source | Notes |
|---------|-------------|-------|
| Curriculum KPI strip | Derived from full `listObjectives()` response | No estimates or placeholders |
| Exercise types KPI strip | Derived from `listExerciseTypes()` response | No fake counts |
| Icon tiles | Colour by `primarySkill` — field on `ExerciseTypeDefinition` | Not from mock reference data |
| "Not runnable yet" label | Shown when `implementationStatus !== 'ready'` — real field | Honest about backend state |
| Reference "avg completion / avg score" stats | **Not shown** — no backend endpoint exists | Absent, not placeholder |

No fake data introduced anywhere.

---

## Behaviours preserved

### Curriculum
- Filter bar (CEFR, skill, active/inactive filters)
- Create objective form
- Edit objective form (all fields)
- Activate/Deactivate per-row actions
- Routing preview view
- Error state

### Exercise Types
- Filter bar (search, skill, status)
- Count inputs (min/default/max items and options per row)
- Enable/Disable toggle via row actions
- Save counts via row actions
- `countError()` validation (negative values, min ≤ default ≤ max)
- Pagination

---

## Tests

### New tests (18 total)

**Curriculum (6):**
- `loadAll()` is called on init with no filters
- `coverageSummary().total` reflects loaded count
- `coverageSummary().cefrBands` counts unique CEFR levels
- `coverageSummary().skills` counts unique primary skills
- `sp-admin-kpi-card` elements rendered when allObjectives loaded
- Coverage strip has `aria-label="Curriculum coverage summary"`
- `coverageSummary().active` counts only active objectives

**Exercise Types (12):**
- `typeSummary().total` matches loaded count
- `typeSummary().enabled` counts only enabled
- `typeSummary().ready` counts only ready
- `typeSummary().skills` counts unique primary skills
- `sp-admin-kpi-card` elements rendered
- Summary strip has `aria-label="Exercise types summary"`
- `typeIconBg()` returns non-empty hex for known skill
- `typeIconBg()` returns fallback for unknown skill
- `.sp-et-icon-tile` rendered in name cell
- `.sp-et-not-runnable` shown for non-ready type
- `.sp-et-not-runnable` absent for ready type

**Total: 1186/1186 pass.**

---

## Gates passed

| Gate | Result |
|------|--------|
| `git diff --check` | Clean |
| Production build | Clean |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 1186/1186 PASS |
| Backend build | Not run (no backend changes) |
| Playwright | Not run (no E2E specs for these pages) |

---

## Decisions made

1. Curriculum does not get fake track cards — the backend has objectives, not tracks. The coverage summary strip is the honest equivalent.
2. Exercise Types retains the dense table layout — the reference card layout cannot accommodate the inline count inputs without significant UX regression.
3. Icon tiles use `primarySkill` for colour, not the reference's key-based mapping — more future-proof as new types are added.
4. "Not runnable yet — foundation only" replaces reference "Not yet available" — more informative about actual implementation state.
5. Reference avg-completion/avg-score stats not shown — no backend endpoint. Absent, not placeholder.

---

## Deferred gaps

- Curriculum track-level grouping: requires backend tracks API and migration. Not in scope.
- Exercise Types card-per-type layout: requires redesigning count inputs as expandable card sections. Deferred to a future redesign phase if desired.
- Exercise Types avg completion/avg score stats: requires a new analytics endpoint. Not implemented.

---

## Final verdict

**Complete.** 1186/1186. Build clean. All security constraints satisfied. No fake data introduced. All existing behaviour on both pages preserved.

---

## Next recommended action

**10UI-REDESIGN-5** — AI Config and Prompts reference redesign.

See: `docs/design/admin-reference-alignment.md` — `/admin/ai-config` and `/admin/prompts` rows.

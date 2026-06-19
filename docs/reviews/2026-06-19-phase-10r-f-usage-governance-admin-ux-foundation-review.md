---
status: current
lastUpdated: 2026-06-19
owner: engineering
supersedes:
supersededBy:
---

# Phase 10R-F — Usage Governance Admin UX Foundation

**Date:** 2026-06-19
**Related sprint:** 10R-F (Usage Governance Admin UX Foundation)
**Files reviewed:**
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.ts`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.html`
- `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.spec.ts`
- `src/LinguaCoach.Web/src/app/core/services/usage-governance.service.ts`
- `src/LinguaCoach.Api/Controllers/AdminUsageGovernanceController.cs`
- `src/LinguaCoach.Domain/Entities/UsagePolicy.cs`
- `src/LinguaCoach.Domain/Entities/UsagePolicyRule.cs`

---

## Findings grouped by priority

### P0 — Already solid before this phase

- Policy list table (name, scope, default badge, active badge, rule count, edit) was already wired correctly.
- Create/edit form with name, description, scope type, isDefault, isActive was functional.
- Loading, empty, error states existed.
- Save success/error feedback existed.
- Full API (list, get, create, update, assign student, get student usage) present in `AdminUsageGovernanceController.cs`.

### P1 — Gaps addressed in this phase

1. **No summary stat cards** — Admin had no at-a-glance view. Added three stat cards: Total Policies, Active count, Default policy name.
2. **Rules only shown as a count** — Admin could not see what a policy actually limits without a backend call. Added expandable rule detail rows showing: feature key (code pill), feature display name (via feature definitions lookup), enforcement mode badge (HardLimit=danger, SoftWarning=warning, TrackOnly=success), unit type, per-rule active state, and a human-readable limit summary (daily/weekly/monthly count and cost limits).
3. **`features` signal loaded but unused** — The component fetched feature definitions from the API but never displayed feature names. Added `featureNameMap` computed signal and `featureName(key)` helper. Unknown keys fall back to the raw key.
4. **`enforcementBadgeColor` method returned unconstrained `string`** — Caused a TS type error against `SpAdminBadgeTone`. Renamed to `enforcementBadgeTone`, corrected return type to `SpAdminBadgeTone`.
5. **Missing admin wrappers** — Added `SpAdminStatCardComponent`, `SpAdminSectionCardComponent`, `SpAdminCodePillComponent` to imports and template.
6. **Scope badge not styled** — Scope type was raw text; now shown as a neutral badge for visual parity with other admin pages.
7. **Policy description not displayed in list** — Description is now shown as a muted subtitle under the policy name in the table row.

### P2 — Not addressed (out of scope for this phase)

- Rule create/edit UI — no endpoint exists for upsert of individual rules. Would require backend work.
- Student-level policy assignment UI — `assignStudentPolicy` endpoint exists but student list integration was excluded per phase scope.
- Pagination — policy list is unlikely to grow large enough to require pagination in pilot. Deferred.
- Full analytics/reporting page — explicitly excluded.
- Billing/payment features — excluded.
- AI Usage page redesign — excluded.

---

## Decisions made

- `enforcementBadgeTone` return type fixed to `SpAdminBadgeTone` (not a new API, a type-correctness fix).
- Rule detail uses expand/collapse toggle per row to avoid bloating the table when rules are not needed at a glance.
- Feature display name falls back to `featureKey` when feature definitions are not yet loaded or the key is unknown — this is safe because the API always returns definitions on load.
- Used `sp-admin-section-card` to wrap the policy table (consistent with other admin pages post-10X-K).
- Used `sp-admin-stat-card` with three tone variants (indigo, success, violet) for total/active/default cards.
- Used `sp-admin-code-pill` for feature keys in the rule detail view.

---

## AskUserQuestion answers

None required. Phase scope was well-defined.

---

## Implementation tasks produced

None. Phase is complete.

---

## Risks or unresolved questions

- **Rule management UI is blocked on backend.** There is currently no API endpoint to create, update, or delete individual `UsagePolicyRule` records via the admin API. The `CreateUsagePolicyRequest` accepts a `rules` array, but the update endpoint (`PUT /api/admin/usage-policies/{id}`) only updates policy-level fields (name, description, isDefault, isActive). A future phase will need to add rule upsert endpoints before a full rule editor is possible.
- **Student assignment UI deferred.** `assignStudentPolicy` endpoint exists but there is no admin list page integration. Deferred to a future phase after the student management page is confirmed stable.

---

## Final verdict

Phase 10R-F complete. The Usage Policies admin page is now production-usable for the pilot:

- Admin can see existing policies with at-a-glance stat cards.
- Admin can identify the default policy by name in the stat card and by badge in the table.
- Admin can expand any policy to see its rules including feature names, enforcement mode, unit type, and limit values.
- Admin can create and edit policy-level fields with full feedback.
- No backend was changed.
- No unrelated admin UI was refactored.
- No commit or push was made.

---

## Next recommended action

Add a `PUT /api/admin/usage-policies/{id}/rules` or per-rule `POST/PUT/DELETE` endpoint to enable rule management UI in the next governance phase.

---

## Gates

- `git diff --check`: clean
- `dotnet build`: not run (no backend changes)
- `dotnet test`: not run (no backend changes)
- `npm run build -- --configuration production`: **PASS**
- `npm test -- --watch=false --browsers=ChromeHeadless`: **PASS — 667/667**
- Playwright: not run (no Playwright tests exist for usage-policies page)

---

## Files changed

| File | Change |
|---|---|
| `admin-usage-policies.component.ts` | Added stat cards, rule expand/collapse, featureNameMap computed, enforcementBadgeTone type fix, new wrappers imported |
| `admin-usage-policies.component.html` | Added stat card grid, section-card wrapper, rule detail expand rows, scope badge, description subtitle, improved empty state message |
| `admin-usage-policies.component.spec.ts` | Added 9 new tests (stat cards, computed signals, toggle expand, feature name lookup, enforcement badge tone, rule limit summary, empty state) |

---

## Documentation impact

- Docs reviewed: `docs/sprints/current-sprint.md`, `docs/handoffs/current-product-state.md`
- Docs updated: `docs/sprints/current-sprint.md` (sprint entry added), this review doc created
- Docs intentionally not updated: `docs/architecture/` (no architectural change), `TODOS.md` (rule management TODO captured in this review)
- Reason: phase is frontend-only UX polish with no API or domain model changes

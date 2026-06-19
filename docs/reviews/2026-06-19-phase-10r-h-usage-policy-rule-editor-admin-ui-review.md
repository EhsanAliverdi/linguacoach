# Phase 10R-H — Usage Policy Rule Editor Admin UI — Engineering Review

**Date:** 2026-06-19
**Sprint:** Phase 10R-H
**Reviewer:** Claude (engineering agent)
**Status:** COMPLETE

---

## Related sprint / feature

Phase 10R-H builds on the 10R-G backend foundation to give admin users a full create/edit/delete UI for individual usage policy rules.

---

## Files reviewed and changed

| File | Change |
|---|---|
| `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.ts` | Full rewrite — rule editor signals, form fields, modal state, CRUD methods, local state helpers |
| `src/LinguaCoach.Web/src/app/features/admin/admin-usage-policies/admin-usage-policies.component.html` | Updated — Add/Edit/Delete rule buttons, rule editor modal, delete confirmation modal |

No backend changes. No new Angular service methods. No migration.

---

## Findings

### P0 — None

### P1 — None

### P2 — Observations (no action required)

**Feature key immutability on edit.** The `Update` domain method intentionally omits `FeatureKey` from mutation. The UI communicates this clearly in the edit modal: the key is shown as a read-only code pill with a note that deletion and re-addition is required to retarget a rule. This is the correct pattern.

**Local state update vs reload.** After add/update/delete, the component mutates the `policies` signal in place via `addRuleInPlace`, `updateRuleInPlace`, `removeRuleInPlace`. This avoids a round-trip and preserves expanded row state. Risk: if the server returns a slightly different shape (e.g. server-computed fields), the local state could diverge. Acceptable for now — the API response is the canonical `UsagePolicyRule` shape and is used directly.

**Feature select falls back to free-text input.** If `featureDefinitions` is empty (API unavailable), the rule editor shows a plain text input instead of a select. This is a graceful degradation, not a bug.

**No spec test additions in this phase.** The component spec (`admin-usage-policies.component.spec.ts`) was not extended with rule editor tests. The existing 670 tests all pass. Rule editor behaviour is covered by the service-level tests added in 10R-G. Adding component-level interaction tests is a low-priority follow-up (no dedicated TODO raised — scope is small and the component pattern is identical to other admin modals already tested).

---

## Decisions made

| Decision | Rationale |
|---|---|
| Modal-based rule editor (`sp-admin-modal variant="form"`) | Matches existing admin page patterns; avoids inline form complexity in a data table row |
| Delete confirmation modal (`sp-admin-modal variant="danger"`) | Consistent with existing destructive-action pattern across admin pages |
| Signal-per-field for rule form | Matches existing policy form pattern in same component; no need for reactive forms |
| `featureOptions` computed from `featureDefinitions` signal | Degrades to text input if definitions unavailable |
| `saveSuccess` signal shared between policy and rule operations | Rule success messages use the same top-level alert, keeping UI compact |
| No full `load()` reload after rule CRUD | Preserves expanded row state; API response shape is trusted |

---

## AskUserQuestion answers

None required for this phase.

---

## Implementation tasks produced

None. Phase 10R-H is self-contained.

---

## Risks / unresolved questions

- DB unique index on `(UsagePolicyId, FeatureKey)` deferred to TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT.
- No spec tests added for the rule editor modal interaction. Low priority.

---

## Build and test results

| Check | Result |
|---|---|
| `npm run build --configuration production` | ✅ Clean |
| `npm test --watch=false --browsers=ChromeHeadless` | ✅ 670/670 |

---

## Final verdict

Phase 10R-H is complete. Admin users can now add, edit, and delete individual usage policy rules from the expanded policy row in the Usage Policies admin page, with modal forms, delete confirmation, client-side validation, inline error display, and local state update without full reload.

## Next recommended action

Commit Phase 10R-H. Next priority is TODO-10R-RULE-MGMT-UNIQUE-CONSTRAINT (optional hardening) or moving to the next product phase.

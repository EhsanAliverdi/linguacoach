# Skill Graph Admin UX Bug-Fix Round

**Date**: 2026-07-24
**Related**: Skill Graph rebuild Phase 6 (`docs/sprints/skill-graph-phase-6-admin-ux-sprint.md`), which had just closed out with 6.3e. This round is a direct response to a batch of user-reported bugs/UX issues found while using the finished Phase 6 admin surface.

## Context

Immediately after Phase 6.3e (Graph Change Suggestions Service) shipped, the user reported seven distinct problems while actually using the Skill Graph admin page and its Edit/View/Create pages. This document covers the investigation and fix for each.

## Files reviewed

- `src/LinguaCoach.Web/src/app/design-system/admin/components/table/sp-admin-table.component.ts` (shared table component used by every admin bulk-edit table)
- `src/LinguaCoach.Web/src/app/features/admin/admin-skill-graph/admin-skill-graph.component.{ts,html,spec.ts}`
- `src/LinguaCoach.Web/src/app/features/admin/admin-skill-graph/admin-skill-graph-node-{view,edit,create}/*.ts`
- `src/LinguaCoach.Application/SkillGraph/SkillGraphContracts.cs`
- `src/LinguaCoach.Infrastructure/SkillGraph/GraphChangeSuggestionService.cs`
- `src/LinguaCoach.Infrastructure/SkillGraph/NearDuplicateConfirmationService.cs` (new)
- `src/LinguaCoach.Api/Controllers/AdminSkillGraphController.cs`
- `src/LinguaCoach.Persistence/Seed/DefaultAiSeeder.cs`

## Findings, grouped by priority

### P0 — real defects

1. **Bulk-select checkbox bug on every table with bulk edit.** Root cause: `sp-admin-table.component.ts`'s row template rendered the per-row bulk checkbox nested inside `@for (column of columns; track column.key)`. Angular's new `@for` control-flow syntax gives each `@for` its own implicit `$index` — the nested column loop's `$index` (the column's position, e.g. always `2`) **shadowed** the outer row loop's `$index`, so every row's checkbox read/wrote the exact same fixed index regardless of which row was actually clicked. Result: checking any one row's box visually checked every row, and unchecking it cleared every row. Fixed by aliasing the outer loop's index (`@for (row of rows; track $index; let rowIndex = $index)`) and using `rowIndex` everywhere inside the nested column loop. This is a shared design-system component, so the fix applies to every admin table with bulk edit at once, not just Skill Graph's Nodes table.
   - Regression test added: `sp-admin-table.component.spec.ts` (3 tests) — verified it fails against the pre-fix code (checking row 0 incorrectly checked all 3 rows) and passes with the fix.

2. **"Back"/"Cancel" always navigated to `/admin/skill-graph`, ignoring where the admin actually came from.** Since the node graph preview on View/Edit lets an admin click a neighbor node and jump to *its* View/Edit page, a user who navigated Node A → (click neighbor) → Node B → Back expected to land back on Node A, not the main list. Fixed by replacing every hardcoded `router.navigateByUrl('/admin/skill-graph')` exit point (View's Back, Edit's Cancel/post-save-navigate/Done-reviewing, Create's Cancel) with Angular's `Location.back()`, which respects real browser history regardless of how the admin arrived.

### P1 — real UX gaps

3. **Near-duplicate node detection was genuinely poor.** The 6.3c-era metric was title-only Jaro-Winkler, a character-positional algorithm built for short strings (names) — on longer sentence-like titles it rewards shared common English letters/prefixes even when the actual content is unrelated. The user's own example, "Reading a short biography" vs "Reading a holiday blog post" (89% similarity, clearly not duplicates), was reproduced and used as a regression test. Replaced with a Sorensen-Dice coefficient over character bigrams (far more discriminative for sentence-length text), combined 70%/30% across title and description (a near-identical title with a genuinely different description now pulls the score down). Also added:
   - Descriptions are now returned on every near-duplicate suggestion so the admin can judge without navigating away.
   - Both node titles are real links to their View page (safe now that fix #2 makes Back work correctly).
   - **On-demand per-pair AI confirmation** (user's explicit request, confirmed via `AskUserQuestion` — chose "on-demand per-pair" over "automatic during audit" to keep audit cost at zero unless requested): new `INearDuplicateConfirmationService`, new prompt `skill_graph_confirm_near_duplicate`, new endpoint `POST /skill-graph/suggestions/near-duplicates/confirm`. Same bounded-call/retry-once/never-throws shape as every other AI service in this codebase. Purely advisory — never merges anything itself.
   - **Merge now requires an explicit confirm step** (previously a single click merged immediately with no way to review or undo). "Keep A"/"Keep B" stages a confirmation panel describing exactly what will happen; nothing is called until "Confirm merge."
   - Live result against the real 600-node curriculum: the audit went from **170 suggestions** (old metric) to **29** (new metric, before cleanup) to **1** (after cleaning up disposable test nodes left over from earlier verification sessions that were polluting the count) — and that one remaining suggestion ("Modals of deduction: present" vs "...past", 87% similar) was correctly judged **not a duplicate** by the on-demand AI confirmation, with a sound one-sentence justification. This is the intended real-world behavior: a plausible candidate surfaced, then correctly dismissed on inspection.

4. **"Graph audit" and "Near-duplicate nodes" were two separate cards doing what the user correctly identified as "literally the same thing."** Merged into one "Graph audit" card with a single "Run graph audit" button that runs both deterministic checks in parallel (`forkJoin`) and renders both result lists (Redundant edges / Near-duplicate nodes) as subsections of the same card.

5. **Isolated-nodes banner told the admin to "click a node below" with no clickable affordance anywhere.** Each isolated node is now rendered as a real button inside the banner that navigates to its View page.

6. **"Fix All with AI" lived in the page header, separate from the banner describing the problem it fixes.** Moved inside the banner itself (button on the right side of the same alert), matching the layout the user asked for.

## Decisions made

- **AI verification approach for near-duplicates**: presented three options via `AskUserQuestion` (on-demand per-pair / automatic during audit / deterministic-only for now). User chose **on-demand per-pair** — keeps the audit itself free and instant; AI cost is opt-in only when an admin wants a second opinion on a specific pair.
- **"Fix All with AI in the header, not the banner" pattern exists on 7 other admin pages** (Modules, Exercises, Lessons, Resource Bank, and their detail pages) — this round only fixed the Skill Graph page, since that's the one reported. Applying the same change everywhere is a separate, larger, explicitly-scoped follow-up if the user wants it (not done here to avoid silently touching 7 unrelated pages without confirmation).

## Implementation tasks completed

- [x] Fixed the `$index` shadowing bug in `sp-admin-table.component.ts` (affects every bulk-edit table app-wide)
- [x] Replaced hardcoded back-navigation with `Location.back()` on View/Edit/Create
- [x] Rewrote near-duplicate similarity from title-only Jaro-Winkler to bigram-Dice title+description weighted
- [x] Added descriptions to `NearDuplicateNodeCandidate`/`NearDuplicateNodeSuggestion` and the API response
- [x] New `INearDuplicateConfirmationService` + `skill_graph_confirm_near_duplicate` prompt + `POST .../near-duplicates/confirm` endpoint
- [x] Merged "Graph audit" + "Near-duplicate nodes" UI into one card, one button
- [x] Added a confirm-before-merge step (stage → confirm/cancel) instead of one-click merge
- [x] Made near-duplicate node titles real links to their View page
- [x] Made the isolated-nodes banner's node list clickable
- [x] Moved "Fix All with AI" into its banner on the Skill Graph page
- [x] Cleaned up ~16 disposable test nodes left over (not deactivated — `Reject` in this codebase sets review status only, not `IsActive`) from earlier Phase 6.3b-6.3e live-verification sessions, which were inflating the near-duplicate/isolated-node counts in the shared dev DB

## Verification

- Backend: 30 architecture + 2,521 unit + 1,363 integration, all green
- Frontend: `tsc --noEmit` clean (only pre-existing unrelated e2e spec errors); full Karma suite run before/after via `git stash` comparison confirmed **no regressions** (baseline without this round's changes: 237 failed/1490 passed; with changes: 234 failed/1499 passed — the failure count is dominated by pre-existing unrelated flaky specs, e.g. `admin-app-layout` breakpoint tests and `admin-wrapper-migration`)
- New regression tests: `sp-admin-table.component.spec.ts` (3, confirmed to fail against pre-fix code), `admin-skill-graph.component.spec.ts` (+8), `GraphChangeSuggestionServiceTests.cs` (updated + false-positive regression case), `NearDuplicateConfirmationServiceTests.cs` (+7), `AdminSkillGraphEndpointTests.cs` (+5)
- Live end-to-end verification against the real reseeded dev DB via Playwright: bulk-select independence on the real Nodes table, merged Graph audit card showing both suggestion types from one click, AI confirmation returning a correct real verdict, confirm-before-merge panel appearing and Cancel working, node-title-link navigation, and Back correctly returning to the audit list after visiting a node

## Risks / unresolved

- The "Fix All with AI in header" pattern remains unfixed on 7 other admin pages (Modules, Exercises, Lessons, Resource Bank + their detail pages) — deferred, not requested for this round.
- `SkillGraphNode.Reject()` does not set `IsActive = false` — a rejected node stays "active" for isolated-node/near-duplicate audit purposes. This is pre-existing behavior (not introduced by this round) but it's the reason disposable test nodes from earlier sessions kept polluting live audits despite being "rejected." Not fixed here since it wasn't part of the reported issues and changing `Reject`'s semantics could have wider ripple effects; noted for future attention if it causes confusion again.

## Final verdict (first pass)

All seven reported issues fixed and live-verified. Ready to commit.

## Follow-up: dedicated Graph Audit page (same session, immediate user follow-up)

Right after the first pass shipped, the user asked for a further restructuring: all of the audit
content (missing tags, isolated nodes, redundant edges, near-duplicates) should live on its own
page, reached from the main Skill Graph list via a link — not inline on the list page itself. A
second follow-up in the same exchange asked that every finding render as a **table row** (same
convention as the main Nodes table), with real action buttons and a search filter per category,
rather than plain bulleted lists.

### What changed

- New route `/admin/skill-graph/audit` → `AdminSkillGraphAuditComponent`, lazy-loaded like every
  other Skill Graph sub-page.
- The main Skill Graph list page (`admin-skill-graph.component.ts/html`) had the tag-issues banner,
  isolated-nodes banner, and full "Graph audit" card removed entirely, replaced with a single
  compact "Graph Audit" entry-point card ("Go to Graph Audit" button). The only audit-related state
  the list page still owns is `redundantEdgeSuggestions` for the narrow case where accepting a
  Reconnect (6.3b/6.3e, an action that only exists on this page) itself surfaces a new redundant
  edge — everything else moved to the new page.
- The new audit page renders four independent `sp-admin-table` instances, each with its own search
  filter and per-row action buttons, matching the Nodes table's established look:
  - **Missing tags** — Title + "Fix with AI" per row (new: previously only the aggregate "Fix All"
    existed; added `fixOneNodeWithAi`/`isFixingNode` for per-row repair), plus the existing bulk
    "Fix All with AI" button.
  - **Isolated nodes** — Title/Key/CEFR/Skill/Status + "Add prerequisite →" (goes straight to the
    Edit page, since that's literally the documented next step).
  - **Redundant edges** — From/To/Reason + Dismiss/Remove edge.
  - **Near-duplicate nodes** — Node A/Node B/CEFR-Skill/Similarity + a "Review" toggle that expands
    an `sp-admin-table` `rowDetail` panel (descriptions, Confirm with AI, Dismiss, Keep A/Keep B,
    and the merge-confirmation panel) — auto-expanded whenever an AI result or a pending merge
    exists for that pair, even without a manual toggle, so state is never hidden.
- Descriptions/AI-confirm/merge-confirm/dismiss/stage-merge logic is otherwise unchanged from the
  first pass — only its location and its row-vs-list rendering changed.

### Verification

- `tsc --noEmit` clean, full production build succeeds and generates the new page as its own lazy
  chunk (confirmed by grep against the built output).
- New spec: `admin-skill-graph-audit.component.spec.ts` (16 tests, migrated + extended from the
  tests removed off the main page's spec, plus new coverage for `isDuplicateRowExpanded`'s
  toggle/auto-expand behavior). Main page's spec updated: obsolete isolated-node-banner test
  replaced with a `goToAuditPage()` navigation test; unused API mocks removed.
- Full Karma suite: 1503 passed / 237 failed, same pre-existing failure count as the established
  baseline (237) with more tests passing than either prior run — no regressions.
- Live-verified via Playwright against the real dev DB: the main page now shows only the compact
  entry-point card; the audit page's four tables all render correctly with real data (535 missing-
  tag nodes, 1 isolated node, 10 redundant edges, 1 near-duplicate pair on the real curriculum);
  the near-duplicate row's "Review" toggle correctly expands to show descriptions and all four
  action buttons.

## Final verdict

Both the original 7-issue round and this table-based-audit-page follow-up are complete and
live-verified. Ready to commit.

## Next recommended action

Ask the user whether the "Fix All with AI" header-to-banner move should be applied to the other 7
admin pages that share the same pattern.

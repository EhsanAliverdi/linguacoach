# Phase J4B follow-up: Import Content tabs, run-candidates page, pagination

**Date:** 2026-07-13
**Related:** Phase J4B (`docs/reviews/2026-07-13-phase-j4b-student-submit-import-tabs-nav-fix-review.md`)
**Type:** Implementation review (direct user UI correction, no AskUserQuestion round)

## Trigger

After Phase J4B landed (commit `13ad6941`), the user reviewed the redesigned Import Content page
live and gave direct correction:

> "the UI imporved but it is not matching outhe rparts of the app, the tabs should be simillar to
> other pages. take AI configuration as example you can see the styling is different. make sure
> this page also use the shard admin design component like tabs and ... also on import histo, when
> import run selected candidate for selectd run should open the table on different page not on the
> same page. also bot runs tabel and candidate for selected run, need to have both frontend and
> backend pagination. same as other pages."

Three concrete corrections:
1. Tabs must use the shared admin tab component/pattern (reference: `/admin/ai-config`), not the
   ad-hoc `sp-admin-button` toggle pair J4B shipped.
2. Selecting a run in Import History must navigate to a genuinely different page, not expand a
   panel inline below the runs table on the same page.
3. Both the runs table and the run-candidates table need real frontend + backend pagination.

## Files reviewed

- `src/LinguaCoach.Web/src/app/features/admin/admin-ai-config/admin-ai-config.component.html` —
  confirmed the shared tab markup: `sp-admin-tab-bar` container (`role="tablist"`), `sp-admin-tab`
  buttons (`role="tab"`, `aria-selected`), `sp-admin-tab--active` modifier. Defined once in
  `admin-tokens.css` (lines 559-583), used in exactly 3 admin pages before this change
  (`admin-ai-config`, `admin-notifications`, `admin-student-detail`).
- `src/LinguaCoach.Web/src/app/features/admin/admin-modules/admin-modules.component.ts/.html` —
  confirmed the standard pagination pattern: `page = signal(1)`, `pageSize` const,
  `totalCount = signal(0)`, `totalPages = computed(...)`, `onPageChange()`,
  `<sp-admin-pagination>` inside `<sp-admin-table-footer>`.
- `src/LinguaCoach.Web/src/app/core/services/admin-resource-import.service.ts` — confirmed
  `AdminResourceImportRunService.list()` and `AdminResourceCandidateService.list()` already accept
  real `page`/`pageSize` and return `totalCount` — **no backend changes needed**, pure frontend
  wiring.
- `src/LinguaCoach.Web/src/app/features/admin/admin-content-import/admin-content-import.component.ts/.html`
  — the file this correction targets.
- `src/LinguaCoach.Web/src/app/app.routes.ts` — existing `content/import` route and legacy
  `resource-sources`/`resource-import-runs`/`resource-candidates` redirects.

## Changes made

### 1. Shared tab component (finding: cosmetic-only mismatch, no backend impact)
Replaced the `sp-admin-button` solid/outline toggle pair with the native
`sp-admin-tab-bar`/`sp-admin-tab`/`sp-admin-tab--active` markup, matching AI Config exactly (same
CSS classes, same `role="tablist"`/`role="tab"`/`aria-selected` bindings). No new component was
needed — this is a shared CSS-class pattern, not a bespoke Angular component.

### 2. Run-candidates page (finding: navigation UX gap)
New route `content/import/runs/:runId` → new standalone component
`AdminImportRunCandidatesComponent` (`src/LinguaCoach.Web/src/app/features/admin/admin-import-run-candidates/`).
It owns its own candidates table (with pagination), preview drawer, and reject modal — the same
review/publish/reject/preview logic previously duplicated inline in Import History, now living on
its own page reached by `router.navigate()` from a runs-table row click.

`selectRun(runId)` in `AdminContentImportComponent` now navigates instead of setting local
signal state:
```ts
selectRun(runId: string): void {
  this.router.navigate(['/admin/content/import/runs', runId]);
}
```

A "← Back to Import History" button on the new page navigates back to
`/admin/content/import?tab=history`, and `AdminContentImportComponent.ngOnInit()` now reads the
`tab` query param to land back on the History tab. The old `importRunId` deep-link query param
(used by external bookmarks/links) now redirects straight to the new run page instead of reviving
the old inline-panel behavior:
```ts
const importRunId = this.route.snapshot.queryParamMap.get('importRunId');
if (importRunId) {
  this.router.navigate(['/admin/content/import/runs', importRunId], { replaceUrl: true });
  return;
}
```

The "New Import" tab's own pipeline section (candidates freshly staged by an import just
submitted on that same tab) was **not** moved to a separate page — that flow is a direct linear
continuation of the action the admin just took, not a "browse history, pick one of many" pattern,
so it correctly stays inline per Phase J4B's original tab design. Only the History tab's
select-a-run-from-a-list flow was in scope for the "different page" correction.

### 3. Pagination (finding: both list tables silently showed only the first page)
Backend was already page-aware; only frontend wiring was missing.

- **Runs table** (Import History, `AdminContentImportComponent`): added
  `runsPage`/`runsPageSize`/`runsTotalCount`/`runsTotalPages` signals, `onRunsPageChange()`,
  `<sp-admin-pagination>` in the table's `<sp-admin-table-footer>`. `loadRecentRuns()` now calls
  `importRunSvc.list(this.runsPage(), this.runsPageSize)` instead of a hardcoded `list(1, 10)`.
- **Run-candidates table** (new `AdminImportRunCandidatesComponent`): `page`/`pageSize`/
  `totalCount`/`totalPages` signals, `onPageChange()`, `<sp-admin-pagination>` footer, calling
  `candidateSvc.list(this.page(), this.pageSize, undefined, this.runId)`.
- **New Import tab's own candidates table** (`AdminContentImportComponent`, unrelated to the
  user's specific complaint but built on the identical shared table/loadCandidates() code path):
  given the same treatment for consistency — `candidatesPage`/`candidatesTotalPages`/
  `onCandidatesPageChange()` — rather than leaving one of the app's three candidate tables silently
  un-paginated while its siblings were fixed.

## Findings, grouped by priority

**Critical (all fixed):**
- Tabs did not match the shared admin design system, contradicting explicit visual-consistency
  intent stated by the user and demonstrated by 3 other admin pages already using
  `sp-admin-tab-bar`.
- Run selection expanded content inline on the same page/URL instead of navigating — no
  bookmarkable/back-button-friendly URL for a specific run's candidates (the old `importRunId`
  query param existed but was a side-channel, not primary navigation).
- Both the runs table and the (inline, now-removed) candidates-for-selected-run table had zero
  pagination UI despite the backend already supporting it — with more than one page of runs or
  candidates, older items were silently unreachable.

**No issues found requiring further scope:** the AI Config reference page's tab pattern is a pure
CSS-class convention (not a wrapped Angular component), so adopting it required no new shared
component and no changes to `design-system/admin`.

## Decisions made

- Kept the "New Import" tab's pipeline section inline (not moved to its own page) — it is a
  single-flow continuation of an action just taken, structurally different from Import History's
  "pick one of many past runs" browse pattern that the user's feedback was about.
- Extended pagination to the New Import tab's candidates table too, even though the user's
  complaint named the History-tab table specifically, since it shares the exact same
  `loadCandidates()`/table markup and leaving it unpaginated while its sibling tables were fixed
  would have been an inconsistency, not a scope-preserving choice.
- No AskUserQuestion was used for this round — the user's corrections were explicit and concrete
  (which page to mirror, which flow to fix, which tables need pagination), leaving no ambiguous
  decision point.

## Implementation tasks produced

None outstanding — all three corrections are implemented and verified in this pass.

## Risks or unresolved questions

- `AdminImportRunCandidatesComponent` duplicates the candidate-review action logic
  (`analyzeCandidate`, `approveAndPublish`, `openReject`/`confirmReject`, `openPreview`, preview
  drawer template) that also still lives in `AdminContentImportComponent` for the New Import tab's
  inline pipeline. This mirrors the codebase's existing convention of self-contained admin page
  components (e.g. `admin-modules` vs `admin-lessons` do not share a base class), but is worth
  flagging as a candidate for later extraction if a third near-identical candidate-review surface
  is ever added.
- Live browser verification (see below) covered navigation, pagination UI, and console errors, but
  did not exercise a second page of runs/candidates (the seeded dev data currently fits on one
  page for both tables) — pagination *controls* render correctly with `totalPages` computed from
  the real `totalCount`, but multi-page click-through was not observed live.

## Verification

- `npx tsc -p tsconfig.app.json --noEmit` — clean, no errors.
- `npm run build -- --configuration production` — new/changed chunks
  (`admin-content-import-component`, `admin-import-run-candidates-component`) compiled with no
  errors. The build's one `ERROR` (bundle initial budget exceeded, 2.56 MB vs 1 MB budget) was
  confirmed pre-existing on `main` before this change (verified via `git stash` + rebuild) —
  unrelated to this work, not introduced by it.
- Live browser smoke test (gstack `browse`, logged in as seeded `admin@linguacoach.local`):
  - `/admin/content/import` — New Import tab renders with the `sp-admin-tab-bar` underline style,
    visually matching `/admin/ai-config`'s tab bar (side-by-side screenshot comparison).
  - Import History tab shows the runs table with a working pagination footer
    ("Page 1 of 1 · 8 runs", Previous/1/Next controls).
  - Clicking a run row navigated the browser to
    `/admin/content/import/runs/27ed8f3f-2a0a-405e-8f32-a6923f1a5672` — a distinct URL/page, not
    an inline expansion — showing a run summary card and a candidates table with its own
    pagination footer ("Page 1 of 1 · 8 candidates").
  - "← Back to Import History" navigated to `/admin/content/import?tab=history` with the History
    tab correctly re-selected.
  - No console errors on either page throughout.

## Final verdict

All three corrections implemented and verified live. No backend changes were required (import
run/candidate list endpoints were already paginated). Ready to commit locally.

## Next recommended action

Commit this change locally (no push/deploy, per standing Phase J4B boundaries). Resume Phase J5
(import content-type expansion) only after this and the original J4B round are both considered
closed by the user.

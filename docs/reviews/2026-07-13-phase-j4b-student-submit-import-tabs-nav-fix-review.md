---
status: current
lastUpdated: 2026-07-13 01:00
owner: engineering
supersedes:
supersededBy:
---

# Phase J4B — Student Runtime Submit Verification + Import Content UX/Nav Fix

**Date:** 2026-07-13
**Related sprint/feature:** Follow-up to `docs/reviews/2026-07-13-j3-j4-live-browser-smoke-test-review.md`,
which surfaced three real usability issues during browser smoke testing: (1) an unconfirmed
hypothesis that the student `/activity` page has the same missing-submit-button gap as the J3 admin
preview modal, (2) a confusing Import Content page layout, (3) an apparent duplicated admin nav.
**Type:** Investigation + three scoped fixes. J5 explicitly not started.
**HEAD before work:** `86cdfb31804ee00d224bb4fad651b4c25414da4a`

**Files reviewed:** `activity-lesson.component.ts`, `activity-practice-page.component.ts`/`.html`,
`exercise-renderer.component.ts`/`.html`, `formio-renderer.component.ts`, `admin-modules.component.ts`
(J3's fix, for pattern reuse), `admin-content-import.component.ts`/`.html`,
`admin-app-layout.component.html`/`.ts`, `ExerciseGenerationService.cs`/`AiExerciseGenerationService.cs`
(schema shape confirmation), `PracticeGymSuggestionsController.cs`, `placement.guard.ts`.

---

## Step 0 — Audit findings

**Student runtime submit path.** `ExerciseRendererComponent` (`exercise-renderer.component.html:1-6`)
rendered `app-formio-renderer` with only a `(submit)` event listener — no submit button in the
schema, no external trigger anywhere in this component, its host
(`activity-practice-page.component.html:157-163`, which only had a "Back to Today" button below the
renderer), or `activity-lesson.component.ts` (which only reacts to the `formIo` payload kind once
emitted, never triggers submission itself). Cross-checked against the actual generated schemas —
`ExerciseGenerationService.ComposeGapFill`/`ComposeMultipleChoiceSingle` and their AI-generated
equivalents in `AiExerciseGenerationService.cs` — both produce `{"components": [...]}` with no
`type: "button"` component. **Confirmed: this is the same bug Phase J3 found and fixed in the admin
preview modal, now confirmed present in the real student-facing runtime too**, not just a
hypothesis.

**Import Content UX.** `admin-content-import.component.html` mixed three concerns in one linear
page: a "Recent import runs" chip row at the very top (clicking a chip reused the *same* pipeline
table used for a just-completed import, with no visual distinction between "you just imported this"
and "you're browsing history"), the import form itself, and the candidates review table — all
sharing one `currentRunId`/`showPipeline` signal pair with no separation of intent. This is exactly
the "recent import runs mixed into the form" and "clicking a chip opens data lower on the page"
behavior reported.

**Admin nav duplication.** `admin-app-layout.component.html` has exactly one "Learning Setup"
section (with exactly one "Curriculum" and one "Exercise Types" item) in each of its two `<nav>`
blocks — the always-present mobile drawer and the always-present desktop sidebar, a standard
responsive dual-DOM pattern. Every screenshot taken across this session and the prior one shows a
single, correct sidebar with no visual duplication. The closed mobile drawer, however, was not
excluded from the accessibility tree or from raw DOM-text extraction (`aria-hidden`/`inert` were
absent), so tooling that reads `textContent` rather than the rendered page — which is exactly how
the duplication was originally noticed — sees the full nav twice. Root cause: a missing
accessibility/hidden-state attribute on the off-canvas drawer, not a rendering or template bug.

---

## Part A — Student runtime submit fix

**Fix:** Added the identical pattern J3 already used and browser-verified: a
`@ViewChild(FormioRendererComponent) formioRenderer` reference and a `submitFormIoAnswer()` method
in `ExerciseRendererComponent`, plus a "Submit answer" button (styled `sp-button-primary`, matching
the existing `GapFillComponent`'s own submit-button convention) rendered directly below
`<app-formio-renderer>` in the `formIoSchema` branch of `exercise-renderer.component.html`. No
scoring rules changed, no new scoring path, no answer-key exposure — the button only calls
`FormioRendererComponent.submitForm()`, which triggers Form.io's own existing validation +
`(submit)` event pipeline; the existing `onFormIoSubmitted()` → `answerSubmitted.emit({kind:
'formIo', ...})` → `activity-practice-page` → `activity-lesson.component.ts` → the existing
`POST api/activity/{id}/attempt` submission path is completely unchanged.

**Live browser verification: not performed this session.** Creating a new test student account and
resetting an existing student's password were both blocked by the auto-mode classifier as
unauthorized writes to the shared, persistent dev database. Asked the user directly
(`AskUserQuestion`) how to proceed; they chose to skip the live student click-through and rely on:
(1) the fix being structurally identical to J3's already-verified fix, (2) confirmation via `grep`
that no other code path in the student runtime calls `submitForm()` for this schema shape (so no
double-submit risk was introduced), and (3) the full build/test suite passing. This gap is tracked
as `TODO-025` for whenever authorized test-account access is available.

---

## Part B — Import Content UX redesign

Restructured `admin-content-import.component.ts`/`.html` around two tabs, with **no backend
changes** — same `AdminContentImportService`/`AdminResourceImportRunService`/
`AdminResourceCandidateService`/`AdminResourceSourceService` calls as before, same API contract:

- **New Import tab** — the import form only (paste/upload toggle, source name, content type, input
  mode, content), with CEFR/skill/subskill/difficulty/context/focus tags moved into a collapsed
  native `<details>` "Show advanced defaults" section (matching an existing collapsible pattern
  already used elsewhere in the student app). A step-flow line ("1. Add content → 2. Structure into
  candidates → 3. Review → 4. Approve & publish to the Resource Bank") replaces the previous
  "review below" hint. After a successful import, the candidates review panel appears directly below
  — the same "New Import" tab, never a separate surface — completing the 4-step flow in one place.
- **Import History tab** — the "Recent import runs" chips moved here as a proper table (Source /
  File / Staged count / status), replacing the top-of-page chip row entirely. Selecting a run shows
  its candidates in a panel directly below the table, within the History tab, with a "Selected" badge
  on the active row and a "Close" action — never bleeding into the New Import tab.
- A new `pipelineContext` signal (`'new' | 'history' | null`) tracks which tab's action last loaded
  the shared `candidates`/`currentRunId` state, so the same underlying data never renders in the
  wrong tab (previously the single shared state made "just imported" and "historical" candidates
  indistinguishable). A deep link with `?importRunId=` (used by other pages) now lands on the History
  tab with that run pre-selected, since a deep link is always a "look up this run" intent.
- Copy was reworded toward product language: page subtitle now reads "Import unstructured content,
  review structured candidates, and publish approved items to the Resource Bank"; "Content type"
  replaces "Resource type" as the visible label; the primary button reads "Import as candidates".

No new import content types were added, and no AI import-structuring logic was added — both
explicitly out of scope per the task boundaries.

---

## Part C — Admin nav duplication fix

Added `[attr.aria-hidden]="!drawerOpen()"` and `[attr.inert]="!drawerOpen() ? '' : null"` to the
mobile drawer `<aside>` in `admin-app-layout.component.html`. This is the standards-compliant fix:
real screen readers and any tooling built on the browser's actual accessibility tree will now
correctly skip the closed drawer's content, and the closed drawer's interactive elements
(links/buttons) are no longer focusable via keyboard, which they previously were. Verified via
`document.querySelector('aside').outerHTML` that both attributes are correctly applied when the
drawer is closed. The `browse` tool's own `text` command (raw `textContent`, which doesn't respect
CSS `transform`/ARIA/`inert` semantics) still reports the nav twice at narrower-than-`xl` viewports
— a limitation of that specific extraction method, not of the application; every visual screenshot
taken this session and the prior one shows a single, correctly-rendered sidebar with no duplication.
No nav structure, labels, or routes were changed — only the closed drawer's hidden/inert state.

---

## Validation

- `git diff --check` — clean, no whitespace/conflict-marker issues.
- `dotnet build --configuration Release` — 0 errors (unchanged warning baseline; no backend files
  changed this phase).
- `dotnet test --configuration Release` — **3,459/3,459 passing** (5 architecture, 2,142 unit, 1,312
  integration) — identical count to the pre-phase baseline, since no backend code changed.
- `npm run build -- --configuration production` — no new TS/Angular compile errors across all three
  changed components; fails only the same pre-existing bundle-size budget (1.00 MB budget vs 2.56 MB
  actual), unrelated to this phase.
- Karma frontend unit tests — not run; still blocked by pre-existing, unrelated `TODO-H8-2` (five
  unrelated spec-fixture compile failures), documented only per task instructions.

**Browser smoke test (via `gstack browse` against the already-running dev stack):**
1. **Import Content tabs** — confirmed: "New Import" tab shows the redesigned form with step-flow
   copy and collapsed advanced defaults; clicking "Import History" switches to the runs table with
   no chips above the form.
2. **Import History run selection** — confirmed: clicking a run shows a "Selected" badge on that row
   and its candidates appear in a "Candidates for selected run" panel directly below, within the
   History tab.
3. **Sidebar duplication** — confirmed at the DOM level that the closed mobile drawer now carries
   `aria-hidden="true"` and `inert=""`; confirmed via every screenshot this session that the visible
   sidebar has never shown duplicated sections.
4. **Student `/activity` Form.io submit** — **not live-tested** (see Part A; blocked by account-
   creation restrictions, user chose to skip per `AskUserQuestion`).
5. **Admin Module preview (J3) still works** — not re-run live this session (further live-DB test
   setup was declined per the same user decision), but confirmed via `git status` that none of J3's
   files (`admin-modules.component.ts`/`.html`, `AdminModulePreviewService.cs`,
   `ModulePreviewContracts.cs`) were touched this phase, so there is no plausible regression path.

---

## Documentation impact

- Docs reviewed: `docs/reviews/2026-07-13-j3-j4-live-browser-smoke-test-review.md`,
  `docs/reviews/2026-07-11-phase-j3-admin-module-preview-review.md`,
  `docs/reviews/2026-07-11-phase-j4-exercise-launch-support-honesty-review.md`.
- Docs updated: this review file; `docs/roadmap/road-map.md` (Decision Log entry);
  `docs/handoffs/current-product-state.md` (new dated entry); `TODOS.md` (new `TODO-025` for the
  unverified live-student-account gap).
- Docs intentionally not updated: none — scope was contained to the five files changed.
- Reason: n/a.

## AskUserQuestion decisions

Asked whether to (a) skip the live student browser test and rely on code-level/pattern-reuse
evidence, (b) create a new throwaway student account, or (c) have the user provide test credentials
— after both account creation and password reset were blocked by the auto-mode classifier as
unauthorized shared-database writes. **User chose (a): skip the live test.** Recorded as `TODO-025`.

## Risks or unresolved questions

- **The student `/activity` submit fix has not been verified against a real student session.**
  High confidence given the identical, already-verified J3 pattern and the absence of any competing
  `submitForm()` call path, but this is inference, not observation. Tracked as `TODO-025`.
- The `browse` tool's `text` extraction still reports the nav twice at narrower-than-`xl` viewports
  despite the correct `aria-hidden`/`inert` fix — flagged as a tooling limitation in this review, not
  treated as an unresolved application bug, but worth re-confirming with a true accessibility-tree
  inspection (e.g. Chrome DevTools' Accessibility panel) if it resurfaces as a concern.
- Import Content's "New Import" and "Import History" tabs share one `pipelineContext` state
  variable by design (simplicity over introducing two fully independent state trees) — switching
  directly from a fresh import to History and back to New Import without starting another import
  will not re-show the just-imported run's candidates in New Import (the admin would need to find it
  via History instead). This was a deliberate trade-off to avoid stale/wrong-tab bleed-through, not
  an oversight.

## Final verdict

All three requested fixes are implemented and scoped correctly: the student runtime submit gap is
closed using the same proven pattern as J3 (pending live verification, tracked separately), the
Import Content page now reads as a clear four-step pipeline with history properly separated from
new-import review, and the admin nav's closed mobile drawer is now correctly hidden from
accessibility tooling. No backend code changed. J5 was not started. Nothing was pushed or deployed.

## Next recommended action

Obtain authorized test-student access (new throwaway account or provided credentials) to close
`TODO-025` — this is the one remaining gap before the student-facing H10 launch bridge can be
considered fully browser-verified end to end. J5 (import content-type expansion) remains the next
audit-ordered phase after that.

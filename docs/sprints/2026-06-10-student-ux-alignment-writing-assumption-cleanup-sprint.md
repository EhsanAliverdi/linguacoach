---
status: complete
lastUpdated: 2026-06-10 22:00
owner: product
supersedes:
supersededBy:
---

# Sprint: Student UX Alignment / Writing-Assumption Cleanup

**Date:** 2026-06-10
**Sprint type:** UX cleanup / navigation alignment / stale copy removal
**Related sprints:** Today's Lesson (2026-06-10), Exercise Pattern Engine (2026-06-10), Pattern Evaluation Engine (2026-06-10)

---

## Context

SpeakPath has moved well beyond the early writing/email prototype. The following foundations are complete:
- Today's Lesson / Learning Session (phases 1–5B)
- Exercise Pattern Engine
- Pattern Evaluation Engine
- Pattern-aware renderers and evaluation
- Student learning memory and adaptive path
- Placement assessment
- Admin AI configuration

However, the student UI, navigation, routes, seed/demo/test data, and some documentation still contain old writing/email-first assumptions that contradict the current product direction.

---

## 1. Audit Table

### 1.1 Navigation

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| Desktop sidebar — top item | "Dashboard" label, links to `/dashboard` | "Dashboard" is not a student-facing product concept | Rename label to **Today** | `student-app-layout.component.html` |
| Desktop sidebar — second item | "My Path" label, links to `/my-path` | Old label; implies a writing path | Rename label to **Journey** | `student-app-layout.component.html` |
| Desktop sidebar — Practice link | Links to `/activity` (opens activity shell) | Silently starts an activity, not a Practice Gym landing | Change to link to `/practice` (new route) OR keep `/activity` but add practice gym landing guard | `student-app-layout.component.html`, `app.routes.ts` |
| Desktop sidebar — Vocabulary item | Separate top-level "Vocabulary" nav item | Product decision: Vocabulary should not be top-level | Remove from sidebar nav | `student-app-layout.component.html` |
| Mobile bottom nav — first item | "Home" label, links to `/dashboard` | No product label used; inconsistent with sidebar | Rename to **Today** | `student-app-layout.component.html` |
| Mobile bottom nav — second item | "My Path" label, links to `/my-path` | Old label | Rename to **Journey** | `student-app-layout.component.html` |
| Mobile bottom nav — center FAB | Links to `/activity` | Silently starts an activity | Route to Practice Gym landing | `student-app-layout.component.html` |
| Mobile bottom nav — no Profile item | Profile only visible on desktop | Inconsistency | Add Profile to bottom nav | `student-app-layout.component.html` |
| Header logo | Links to `/dashboard`, aria-label "Go to Dashboard" | Old label in aria-label | Update aria-label to "Go to Today" | `student-app-layout.component.html` |

### 1.2 Dashboard / Today page

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| "Recommended next" section | Large gradient card with "Recommended next" pill, links to `/activity` | Old writing/email-first banner; navigates to activity shell rather than structured lesson | Replace with or repurpose as lesson readiness or "Continue learning" section that links to Today's Lesson | `dashboard.component.html`, `dashboard.component.ts` |
| "Continue learning" CTA inside Recommended next | `routerLink="/activity"` | Goes directly to activity, not lesson flow | Link to `/lesson/:sessionId` when session exists | `dashboard.component.html` |
| "Full path" link inside Recommended next | `routerLink="/my-path"` | Old route label | Update to `/journey` if route is renamed, or leave as `/my-path` with label "View Journey" | `dashboard.component.html` |
| Learning focus card — "View focus" button | Links to `/my-path` | Old route label | Update to `/journey` if renamed | `dashboard.component.html` |
| Practice Gym cards on dashboard | Cards navigate to `/activity?type=WritingScenario` etc. | Cards on Today page create session noise; Practice Gym belongs on Practice page | Move Practice Gym cards to new `/practice` route; keep secondary "Practice Gym" link on Today | `dashboard.component.html`, `dashboard.component.ts` |
| Page title (browser tab / heading) | "Dashboard" (implied, no explicit h1) | Old label | Add visible page heading "Today's Lesson" | `dashboard.component.html` |

### 1.3 Learning Path / My Path / Journey page

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| Learning memory fallback copy | `'SpeakPath is building a clearer picture of your workplace writing.'` | Writing-only framing | Change to `'SpeakPath is building a clearer picture of your workplace English.'` | `learning-path.component.html:69` |
| Page title / heading | "Your journey" (eyebrow), no explicit h1 | Acceptable but matches "My Path" nav label | Page heading can remain as "Learning Journey"; nav label updated to "Journey" | `learning-path.component.html` |
| Route | `/my-path` | Old route name | Keep `/my-path` internally with a `/journey` redirect added for future-proofing; update all links | `app.routes.ts`, `student-app-layout.component.html`, `dashboard.component.html`, `learning-path.component.html` |
| "Continue practising →" button on current module | `routerLink="/activity"` | Goes directly to activity, not to structured lesson | Change to link to Today's Lesson or Practice Gym depending on intent | `learning-path.component.html:246` |

### 1.4 Practice route / Practice Gym

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| `/activity` route | No landing page — immediately renders `ActivityLessonComponent` | Confusing entry point for Practice | Add a `/practice` route with a `PracticeGymComponent` landing page; `/activity` is preserved for direct activity access | `app.routes.ts`, new component |
| Practice Gym on dashboard | Lists Writing, Listening, Vocabulary, Speaking, Pronunciation cards — all link to `/activity?type=X` | Practice Gym belongs on Practice page, not Today | Move to `/practice` page | `dashboard.component.html` |
| Practice nav item | Links to `/activity` | Should link to Practice landing page | Update to `/practice` | `student-app-layout.component.html` |

### 1.5 Vocabulary

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| Top-level sidebar nav item | Visible "Vocabulary" nav item | Product decision: Vocabulary not top-level | Remove from sidebar | `student-app-layout.component.html` |
| `/vocabulary` route | Exists and works | Route can stay; accessible from Practice/Progress | Keep route; remove from nav | `app.routes.ts` (no change needed) |

### 1.6 Seed / demo / test data

| Area | Current behaviour | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| `WritingScenarioSeeder` | Seeds writing-only email scenarios as primary sample content | Writing-only starting assumption | These are valid fallback/writing activity seeds; tag clearly as `WritingScenario` type seeds, not the home page model | `WritingScenarioSeeder.cs` (no deletion needed — these are valid activity seeds) |
| `LearningActivitySeeder` | Mirrors WritingScenarios into `LearningActivity` as `SystemFallback` | Same concern — writing-only fallback for missing AI activities | Keep as-is (these are fallback activities); do not delete | `LearningActivitySeeder.cs` (no change) |
| `disabled-actions-cleanup.spec.ts` mock data | Module title "Professional email writing", `nextPracticeSuggestion: 'Try writing an email to explain a delay.'` | Writing-email-only fixture copy | Update mock titles and suggestions to use mixed-skill examples | `disabled-actions-cleanup.spec.ts:41, :49` |
| `core-flow-smoke.spec.ts` mock data | Module descriptions `'Practice writing clear, polite workplace emails.'` (lines 162, 242, 285, 337), `nextPracticeSuggestion: 'Try writing an email to explain a delay.'` (line 435) | Writing-only test fixture copy | Update to mixed-skill descriptions e.g. `'Practice professional workplace communication.'` | `core-flow-smoke.spec.ts` |
| `lesson-activity-wiring.spec.ts` mock data | `sessionGoal: 'Practice email writing.'` (line 70) | Writing-only session goal in fixture | Update to `'Practice professional workplace communication.'` | `lesson-activity-wiring.spec.ts:70` |
| `today-lesson.spec.ts` mock data | `focusSkill: 'Writing'`, exercise 2 instructions: `'Write a professional email explaining a 3-day document delay.'` | Valid test data for Writing exercises — only the email framing for a Writing-focus test is acceptable | Keep the Writing-focus test as-is; add a second fixture with a mixed-skill session for broader coverage | `today-lesson.spec.ts` |

### 1.7 Documentation

| Area | Current state | Problem | Recommended fix | Files likely affected |
|---|---|---|---|---|
| `current-sprint.md` | Lists "Practice Gym Separation" as deferred | Correct — this sprint plans it | Update after implementation | `current-sprint.md` |
| `current-product-state.md` | Student flow ends with "Student reaches dashboard" | "Dashboard" label is being retired | Update to "Student reaches Today page" | `current-product-state.md` |
| `learning-path` component memory fallback copy | "workplace writing" | Writing-only | Update copy | `learning-path.component.html` |
| Sprint docs referencing writing-first path | Various older sprints reference writing modules as the primary journey | Older sprints are historical; current docs should not contradict the new direction | Mark older sprints with `status: historical`; current docs must reflect the mixed-skill model | Older sprint `.md` files |

---

## 2. Target UX Model

### Today (`/dashboard`)
**Question answered:** "What should I do now?"

- Primary heading: "Today's Lesson"
- Main CTA: Today's Lesson card (start / resume / review states) — already implemented
- Motivational status: streak placeholder, activity stats
- Current learning focus + memory summary
- Secondary links: Practice Gym, Learning Journey
- No "Dashboard" label anywhere in the student-facing UI
- Practice Gym cards move to the `/practice` page

### Journey (`/my-path` → label: Journey)
**Question answered:** "Where am I in my course?"

- Page heading: "Learning Journey"
- Shows the real mixed-skill learning path — modules, activities, progress
- Memory summary: strengths, weak areas, next focus
- Must not imply the course is writing/email-only
- Fix fallback copy: "workplace writing" → "workplace English"
- "Continue practising" CTA inside module → links to Today's Lesson or Practice Gym

### Practice (`/practice` — new route)
**Question answered:** "What can I practise freely?"

- Page heading: "Practice Gym"
- Landing page for free practice by skill or pattern
- MVP: skill cards for Vocabulary, Listening, Writing, Speaking, Workplace Chat, Email, Gap Fill, Phrase Match
- Cards with `Coming soon` for unimplemented patterns
- Does not auto-start a writing/email activity on page load
- Vocabulary accessible here (and on Progress)

### Progress (`/progress`)
**Question answered:** "How am I improving?"

- No change needed in this sprint
- Vocabulary strength / weak phrases accessible here
- Existing implementation preserved

### Profile (`/profile`)
**Question answered:** "What are my settings?"

- No change needed in this sprint

---

## 3. Route / Menu Cleanup Plan

### Routing decisions

| Route | Action | Notes |
|---|---|---|
| `/dashboard` | Keep — rename display label to "Today" | Today is the student home; route path unchanged |
| `/my-path` | Keep — add `/journey` redirect; update all nav labels to "Journey" | Low-risk compatibility; preserve existing Playwright tests that navigate to `/my-path` |
| `/practice` | Create new route — `PracticeGymComponent` | New standalone component; replaces `/activity` as the Practice nav target |
| `/activity` | Keep — used for direct activity access via `activityId` param | Do not break lesson → activity → returnTo flow |
| `/vocabulary` | Keep — accessible from Practice and Progress | Remove from top-level nav only |
| `/progress` | Keep unchanged | |
| `/profile` | Keep unchanged | |

### Navigation label changes

**Desktop sidebar (before → after):**
- Dashboard → **Today**
- My Path → **Journey**
- Practice (links to `/activity`) → **Practice** (links to `/practice`)
- Progress → **Progress** (unchanged)
- Vocabulary → **removed**
- Profile → **Profile** (unchanged)

**Mobile bottom nav (before → after):**
- Home → **Today**
- My Path → **Journey**
- Practice FAB (links to `/activity`) → **Practice** (links to `/practice`)
- Progress → **Progress** (unchanged)
- Profile → **Profile** (unchanged, already present)

---

## 4. Seed / Test / Demo Data Cleanup Plan

**Rule: Do not delete real user data. Do not delete valid activity seeds. Clean only fixture copy that contradicts the product direction.**

### Safe changes (copy-only, no DB writes)

| File | Change |
|---|---|
| `e2e/disabled-actions-cleanup.spec.ts` | Update mock module title from "Professional email writing" to "Professional workplace communication"; update `nextPracticeSuggestion` from email-only to mixed |
| `e2e/core-flow-smoke.spec.ts` | Update 4 instances of `'Practice writing clear, polite workplace emails.'` to `'Practice professional workplace communication.'`; update `nextPracticeSuggestion` |
| `e2e/lesson-activity-wiring.spec.ts` | Update `sessionGoal: 'Practice email writing.'` to `'Practice professional workplace communication.'` |
| `src/app/features/learning-path/learning-path.component.html` | Update fallback copy from "workplace writing" to "workplace English" |

### No action needed

| File | Reason |
|---|---|
| `WritingScenarioSeeder.cs` | These are valid writing activity seeds; writing IS one of the four implemented activity types |
| `LearningActivitySeeder.cs` | Mirrors WritingScenarios as SystemFallback; valid and intentional |
| `today-lesson.spec.ts` | Writing-focus session with email task is a valid test for the Writing skill path |
| `pattern-evaluation-result.spec.ts` | `email_reply` is a real exercise pattern; tests are correct |
| `placement-assessment.spec.ts` | `confidence_email` is a valid placement question |

### Proposed DB-level change (requires approval before implementation)
None required in this sprint. No seed rows need to be deleted or renamed. The `WritingScenario` seed rows are valid activities. The `LearningActivitySeeder` mirrors them correctly.

---

## 5. Documentation Cleanup Plan

| Doc | Current state | Action |
|---|---|---|
| `docs/handoffs/current-product-state.md` | "Student reaches dashboard" in flow description | Update to "Student reaches Today page (dashboard)" |
| `docs/sprints/current-sprint.md` | Lists "Practice Gym Separation" as deferred | Update to reflect this sprint's completion after implementation |
| `docs/architecture/README.md` | Does not mention Today/Journey/Practice/Progress nav model | Add navigation model section or link to sprint doc |
| Older sprint docs (e.g. `learning-experience-improvement-sprint.md`, `vocabulary-extraction-from-writing-attempts-sprint.md`) | May imply writing-first journey | Add `status: historical` frontmatter; do not delete |
| `learning-path.component.html` fallback copy | "workplace writing" | Fix as part of Phase 3 implementation |

**Principle:** Historical sprint docs may remain. They document what was decided and built at a point in time. Current docs (`current-sprint.md`, `current-product-state.md`, `architecture/README.md`) must not contradict the new product structure.

---

## 6. Implementation Phases

### Phase 1 — Audit and plan (this document)
Deliverable: this sprint plan, reviewed and approved.

### Phase 2 — Navigation labels and route cleanup
**Scope:**
- Rename sidebar nav labels: Dashboard → Today, My Path → Journey, remove Vocabulary
- Update Practice nav link to `/practice` (placeholder route for now)
- Rename mobile bottom nav: Home → Today, My Path → Journey
- Add `/journey` redirect to `/my-path` in `app.routes.ts`
- Update all `routerLink="/my-path"` refs to `/journey` (or keep `/my-path` — TBD per risk assessment)
- Update header logo aria-label

**Files:**
- `student-app-layout.component.html`
- `app.routes.ts`
- `dashboard.component.html` (Full path / View focus links)

**Tests to update:**
- Any Playwright test asserting "Dashboard" or "My Path" label in nav
- Add new Playwright assertions: nav shows Today, Journey, Practice, Progress, Profile; no Dashboard label; no Vocabulary nav item

### Phase 3 — Today page motivational home alignment
**Scope:**
- Add explicit "Today's Lesson" h1 heading to dashboard page
- Remove Practice Gym cards from Today page (or keep as secondary section below the fold)
- Replace / repurpose "Recommended next" section: this old section shows learning path module info and links to `/activity`; replace with a simpler "Your learning path" summary that links to `/lesson/:sessionId` (the real lesson) or `/journey`
- Fix `"Continue learning"` CTA link inside Recommended next: change from `/activity` to lesson route
- Fix "Full path" link: update from `/my-path` to `/journey`

**Files:**
- `dashboard.component.html`
- `dashboard.component.ts`

### Phase 4 — Journey / My Path mixed-skill copy cleanup
**Scope:**
- Update learning memory fallback copy: "workplace writing" → "workplace English"
- Update "Continue practising →" button in current module actions: link to Today's Lesson or `/practice`
- Verify no other writing-only copy in the page

**Files:**
- `learning-path.component.html`

### Phase 5 — Practice Gym landing page
**Scope:**
- Create `/practice` route with `PracticeGymComponent`
- Page heading: "Practice Gym — On-demand practice"
- Skill/pattern cards (MVP): Writing, Listening, Vocabulary, Speaking, Workplace Chat, Email, Gap Fill, Phrase Match
- Cards that are functional link to `/activity?type=X` or `/activity?pattern=X`
- Cards not yet functional show "Coming soon"
- Vocabulary card links to `/vocabulary`
- Practice does NOT auto-start a writing/email activity on load

**Files:**
- New: `src/app/features/practice/practice-gym.component.ts`
- New: `src/app/features/practice/practice-gym.component.html`
- `app.routes.ts` (add `/practice` route)

### Phase 6 — Seed / demo / test data cleanup
**Scope:**
- Update fixture copy in 3 Playwright test files (copy-only, no DB changes)
- Update `learning-path.component.html` fallback copy (done in Phase 4)

**Files:**
- `e2e/disabled-actions-cleanup.spec.ts`
- `e2e/core-flow-smoke.spec.ts`
- `e2e/lesson-activity-wiring.spec.ts`

### Phase 7 — Documentation cleanup and QA ✅ complete
**Scope:**
- Update `docs/handoffs/current-product-state.md`
- Update `docs/sprints/current-sprint.md`
- Add `status: historical` to older sprint docs that imply writing-first
- Run full test suite: `dotnet test`, `npm run build`, `npx playwright test --workers=1`

**Delivered:**
- `docs/handoffs/current-product-state.md` — student nav model section added; "Student reaches dashboard" updated to "Student reaches Today page"; test baseline updated; Practice Gym Separation removed from next-work list
- `docs/sprints/current-sprint.md` — sprint marked complete; deferred list updated; Practice Gym Separation moved out of deferred (MVP shipped)
- `docs/architecture/README.md` — Practice Gym separation marked Done; sprint doc table updated; test counts updated
- 20 older sprint docs — `status: historical` frontmatter added
- Sprint doc: this document closed

---

## 7. Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Route changes break existing Playwright tests | Medium | Use redirect `/journey → /my-path`; update tests in same phase |
| "Continue learning" CTA change breaks lesson start flow | Low | Lesson route already wired; test before shipping |
| Practice Gym landing page reveals missing patterns | Medium | Use "Coming soon" states for unimplemented patterns; do not block on backend |
| Removing Vocabulary from nav hides it unexpectedly | Low | Vocabulary accessible via Practice and Progress; document this |
| Old screenshots / visual tests | Low | No visual regression tests currently; update if introduced |
| Backend "recommended next" endpoint writing-first | Medium | Frontend cleanup is independent; backend `SessionGeneratorService` already handles mixed-skill selection |
| Copy cleanup reveals deeper product gaps in Journey | Low | Journey page already uses real `LearningPath` data; mixed-skill copy update is safe |

---

## 8. Acceptance Criteria

### Navigation
- [ ] Student sidebar shows: **Today**, **Journey**, **Practice**, **Progress**, **Profile** — in this order
- [ ] No "Dashboard" label visible in student-facing navigation
- [ ] No top-level "Vocabulary" nav item in desktop sidebar
- [ ] Mobile bottom nav shows: **Today**, **Journey**, **Practice** (FAB), **Progress**, **Profile**
- [ ] Clicking Today navigates to `/dashboard` (the Today page)
- [ ] Clicking Journey navigates to the Learning Journey page
- [ ] Clicking Practice navigates to the Practice Gym landing page (not directly to an activity)
- [ ] `/journey` (if added) redirects to `/my-path` or vice-versa without 404

### Today page
- [ ] Today page acts as the motivational home/dashboard
- [ ] Today's Lesson card is the primary CTA
- [ ] Page has a visible "Today's Lesson" heading or context
- [ ] "Continue learning" / lesson CTA links to the real lesson route, not directly to `/activity`
- [ ] No hardcoded "Recommended next" writing-email-only copy

### Journey page
- [ ] Page heading or eyebrow says "Learning Journey" (not "My Path")
- [ ] Memory fallback copy says "workplace English" (not "workplace writing")
- [ ] "Continue practising" inside module actions does not auto-start a writing email

### Practice page
- [ ] `/practice` route exists and loads a Practice Gym landing page
- [ ] Practice Gym shows skill/pattern options — does not auto-start a writing/email activity on load
- [ ] Vocabulary accessible from Practice Gym

### Data integrity
- [ ] No real user data deleted
- [ ] Valid writing scenario seed rows preserved
- [ ] Test fixture copy updated (writing-only descriptions replaced with mixed-skill descriptions)

### Test suite
- [ ] `dotnet test` passes (no regressions)
- [ ] `npm run build` passes
- [ ] `npx playwright test --workers=1` passes — all tests green
- [ ] New Playwright tests assert: Today/Journey/Practice/Progress/Profile nav structure; Dashboard label absent; Vocabulary nav absent; Practice does not auto-start an activity

---

## 9. Documentation Impact

**Docs to update during this sprint:**
- `docs/handoffs/current-product-state.md` — "dashboard" → "Today page"
- `docs/sprints/current-sprint.md` — update deferred list after Practice Gym completion
- `docs/sprints/2026-06-10-student-ux-alignment-writing-assumption-cleanup-sprint.md` — this document

**Docs intentionally not updated:**
- Older sprint docs (`vocabulary-extraction-from-writing-attempts-sprint.md`, etc.) — remain as historical record; add `status: historical` frontmatter only

**Docs to review for future update (not in this sprint):**
- `docs/architecture/learning-activity-engine.md` — may need a navigation model note
- `docs/architecture/course-session-learning-model.md` — confirm no writing-first assumptions remain

---

## 10. Implementation Readiness

All blockers from prior sprints are resolved:
- Activity types (Writing, Listening, Vocabulary, Speaking) are all implemented
- Lesson flow (start → exercise → activity → returnTo) is wired
- Pattern Evaluation Engine is complete
- No DB migration needed for this sprint

**This sprint is safe to begin immediately after plan approval.**

---

## Next recommended action

Approve this plan and begin **Phase 2: Navigation labels and route cleanup** as the lowest-risk, highest-visibility change.

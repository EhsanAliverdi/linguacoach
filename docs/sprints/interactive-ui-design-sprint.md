# Interactive UI + Branding Design Sprint

## Sprint goal

Make SpeakPath feel like a polished, colourful, interactive AI English learning app — not a plain enterprise dashboard. Deliver a strong brand identity, a motivating dashboard, a beautiful activity lesson flow, and a learning path UI that helps students understand exactly where they are and what to do next.

---

## Status: Phase 1 + prototype alignment complete

All Phase 1 and prototype-alignment work is committed to `main`. See [Product Backlog](../backlog/product-backlog.md) for outstanding items.

---

## What was completed

### Phase 1 — Brand foundation + dashboard polish

- `index.html`: Plus Jakarta Sans font (replaced Inter)
- `styles.css`: full SpeakPath prototype token system:
  - `--sp-grad-brand`, `--sp-grad-cool`, `--sp-grad-warm`, `--sp-grad-soft`
  - All skill colour tokens: writing, speaking, listening, vocabulary, pronunciation
  - Neutral and status tokens matching prototype exactly
  - Shadow tokens: `sh-xs`, `sh-sm`, `sh-md`, `sh-lg`, `sh-glow`
  - Radius tokens: `r-sm`, `r`, `r-lg`, `r-xl`, `r-full`
  - Gradient utility classes: `.grad-brand`, `.grad-cool`, `.grad-warm`, `.grad-soft`
- App shell pattern: desktop left sidebar + mobile bottom nav (5 items, raised Practice centre button)
  - `.sp-app`, `.sp-shell-app`, `.sp-side`, `.sp-sidebrand`, `.sp-sidelink`, `.sp-main`, `.sp-topbar`, `.sp-content`, `.sp-bottomnav`, `.sp-navbtn`
- Layout helpers: `.sp-statgrid`, `.sp-skillgrid`, `.sp-grid2`, `.sp-h1`, `.sp-section-h`, `.sp-icobox`, `.sp-col`
- Dashboard redesigned: topbar greeting ("Good afternoon 👋" / "Hi, {name}"), hero card, 3 stat tiles, 2-column grid, skill cards, coach card, streak calendar placeholder, bottom nav
- My Path page: wrapped in sp-app shell, cool gradient header with SVG progress ring, vertical spine journey, module cards with status states and progress bars
- Activity lesson: wrapped in sp-app shell; InfoBlock pattern; collapsible example (`<details>`); word count in textarea footer; coach avatar microcopy row; prototype feedback layout with score ring, coach bubble, wins/improvements/grammar/tone/vocabulary sections; rewrite challenge card (cool gradient)
- Landing page: prototype logo mark, phrase chips, gradient CTA button
- Progress page (new): placeholder page in sp-app shell with gradient header, stat tiles, skill progress bars, honest empty state for recent results
- Profile page (new): placeholder page with avatar card, settings rows, sign-out button
- Routes: `/progress` and `/profile` added to `app.routes.ts`
- Playwright smoke test updated: removed stale `heading: /Welcome back/` assertion; updated CTA to `role: link, name: /Continue learning/i`; updated button text to `/Get.*feedback/i` and `/Continue to next|Next activity/i`
- Sidebar CSS: fixed `align-self:flex-start` to prevent pointer-event interception of main content
- All Angular build, Playwright, and dotnet tests passing (309/309)

### Design tokens aligned to prototype

The app now uses the exact CSS variable names and values from the SpeakPath prototype (`SpeakPath.html` and `SpeakPath Brand & System.html`). The prototype is the canonical visual reference.

---

## What remains intentionally placeholder

The following dashboard and inner-page elements show honest placeholder UI because the backend data does not exist yet. **Do not replace these with fake real data.**

| Element | Placeholder shown | What will replace it |
|---------|------------------|----------------------|
| Practice streak stat tile | `—` | Real streak count from `StudentProfile` |
| Minutes this week stat tile | `—` | Derived from `ActivityAttempt` timestamps |
| Activities done stat tile | `—` | Count of `ActivityAttempt` rows |
| Coach card message | "Complete your first activity to unlock coach insights." | Latest `ActivityAttempt` feedback summary |
| Latest completed activity card | "No activity completed yet" | Last `ActivityAttempt` with score and title |
| Streak calendar (7 days) | All empty dots | `[M,T,W,T,F,S,S]` boolean array from streak system |
| Progress page stat tiles | `—` | Real values from progress API |
| Progress page recent results | Empty state | ActivityAttempt list with scores and dates |
| Profile learning goal | "Workplace English" | `LearningTrack.Name` from `StudentProfile` |
| Profile current level | "Not assessed yet" | CEFR level from assessment |
| Profile skill cards (4 of 5) | "Coming soon" | Remove label when backend feature ships |

---

## Remaining sprint phases (not started)

### Phase 2 — Activity lesson deeper polish

- Phrase chips as interactive toggles (tap to insert into textarea — prototype Practice state)
- Score ring animation on mount
- Collapsible example section in Learning state (currently `<details>` — could be smoother)
- Coach avatar component shared between dashboard card and feedback phase
- More structured feedback diff view (strikethrough → green replacement pattern from prototype)

### Phase 3 — My Path deeper polish

- Locked module visual treatment improvements (blur, lock overlay)
- Journey visualisation refinements (animated spine, better mobile stacking)
- Module detail view (`/my-path/modules/:id`) — deferred pending backend support

### Phase 4 — Future activity type visual placeholders

- Dashboard skill section: currently shows 5 skill cards (Writing active, 4 others "Coming soon")
- No backend work required for placeholders
- Remove "Coming soon" labels only when backend feature is shipped

### Phase 5 — Component extraction

- Extract `AppShellComponent` (sidebar + topbar + bottom nav) — currently duplicated across 5 pages
- Extract `StatCard`, `SkillCard`, `ModuleCard`, `ScoreRing`, `CoachMessage` as reusable Angular components
- See [Product Backlog — Design system follow-ups](../backlog/product-backlog.md) for full list

---

## Files changed (cumulative)

| File | Change |
|------|--------|
| `src/LinguaCoach.Web/src/index.html` | Plus Jakarta Sans font |
| `src/LinguaCoach.Web/src/styles.css` | Full prototype token + shell CSS |
| `dashboard.component.html` | Full redesign — sidebar shell, hero, stats, skills, coach |
| `dashboard.component.ts` | `greetingTime()` computed, `weekDays` array |
| `activity-lesson.component.html` | Full redesign — sidebar shell, InfoBlock, collapsibles |
| `activity-lesson.component.ts` | `wordCount` getter |
| `learning-path.component.html` | Wrapped in sp-app shell |
| `progress.component.ts` | New placeholder page (inline template) |
| `profile.component.ts` | New placeholder page (inline template) |
| `app.routes.ts` | Added `/progress` and `/profile` routes |
| `e2e/core-flow-smoke.spec.ts` | Updated selectors to match new UI text |
| `docs/sprints/interactive-ui-design-sprint.md` | This file |
| `docs/architecture/speakpath-design-system.md` | Prototype alignment section added |
| `docs/backlog/product-backlog.md` | New — full product backlog |

---

## Test plan

| Test | Status |
|------|--------|
| Angular production build | ✅ Clean |
| Playwright smoke test | ✅ 1/1 passed |
| dotnet unit + integration tests | ✅ 309/309 passed |

---

## Reference

- Prototype files: `docs/design/references/speakpath-prototype/`
- Design system: [speakpath-design-system.md](../architecture/speakpath-design-system.md)
- Product backlog: [product-backlog.md](../backlog/product-backlog.md)

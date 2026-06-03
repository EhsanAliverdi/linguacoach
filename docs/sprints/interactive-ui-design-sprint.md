# Interactive UI + Branding Design Sprint

## Sprint goal

Make SpeakPath feel like a polished, colourful, interactive AI English learning app — not a plain enterprise dashboard. Deliver a strong brand identity, a motivating dashboard, a beautiful activity lesson flow, and a learning path UI that helps students understand exactly where they are and what to do next.

## Current state

- All screens use a minimal white/slate palette with indigo accents
- Dashboard: welcome message + path/module card + sidebar — functional but visually flat
- Activity lesson: three-phase flow (learning → writing → feedback) — works correctly, lacks visual warmth
- Learning path endpoint exists (`GET /api/learning-path`) but no dedicated frontend page
- Global styles: 100-line `styles.css` using Tailwind `@layer components` with `sp-*` utility classes
- No font import (falls back to Inter/system-ui)
- No brand colour tokens beyond slate/indigo
- Angular 18 standalone components, Tailwind CSS v4, no component library

## In scope

- Global design tokens: colour palette, typography, spacing rhythm
- `styles.css` — extend `sp-*` component classes; no structural breakage
- `index.html` — add Google Fonts (Inter variable or similar)
- Dashboard: hero section, learning path card, module progress, skill cards placeholder, CTA polish
- Activity lesson: learning/practice/feedback phases — visual warmth, better chips, coach-feel feedback
- Learning Path page: new `/my-path` route with module list (read-only, no edits)
- App shell: top nav on mobile and desktop; section links to Dashboard, My Path, Activities (stub), Profile (stub)
- Design system: new `sp-*` classes for LearningPathCard, ModuleCard, SkillBadge, PhraseChip, VocabularyChip, FeedbackCard, CoachMessage, ScoreBadge, EmptyState, LoadingState, ErrorState, AI Generated badge
- Playwright smoke test updated to match any changed selectors
- All existing backend functionality unchanged

## Out of scope

- Speaking, Listening, Vocabulary, Pronunciation backend implementation
- Any database schema changes
- New API endpoints
- CEFR assessment changes
- Admin panel redesign
- Authentication flow changes

## Screens affected

| Screen | Route | Changes |
|--------|-------|---------|
| Landing | `/` | Typography + colour lift; CTA button colours |
| App shell / nav | all | New mobile-first navigation bar |
| Dashboard | `/dashboard` | Hero, path card, module card, skill section, CTA |
| Activity lesson | `/activity` | All three phases polished |
| Learning Path | `/my-path` *(new)* | Module list, progress journey |

## Component plan

### New Angular components (only where justified)

| Component | Path | Purpose |
|-----------|------|---------|
| `SkillBadgeComponent` | `core/components/skill-badge/` | Coloured pill showing activity type (Writing, Speaking, etc.) — used on ModuleCard and ActivityCard |
| `ScoreBadgeComponent` | `core/components/score-badge/` | Large animated score circle used on feedback screen |
| `ModuleCardComponent` | `core/components/module-card/` | Reusable card for learning path module list |

Everything else is `sp-*` utility classes in `styles.css`. Angular components only where template complexity justifies encapsulation.

### New `sp-*` classes (styles.css)

```
sp-module-card         card with left colour bar + status indicator
sp-phrase-chip         indigo rounded pill (target phrase)
sp-vocab-chip          slate rounded pill (vocabulary word)
sp-skill-badge         small coloured tag with icon prefix
sp-score-ring          large score number inside coloured ring
sp-coach-message       warm coach comment block (emerald tint)
sp-feedback-card       structured feedback section card
sp-empty-state         centred icon + text empty state block
sp-loading-pulse       animated skeleton loading card
sp-source-lang-block   RTL Persian instruction block (already exists, refine)
sp-ai-badge            indigo "AI generated" small badge
sp-fallback-badge      slate "System" small badge
sp-progress-track      slim progress bar row (already exists as inline, extract)
sp-module-status       coloured dot — current / complete / locked
```

## Colour palette

| Token | Hex | Use |
|-------|-----|-----|
| `brand-ink` | `#0c0a1e` | Primary text, nav background |
| `brand-violet` | `#6d28d9` | Primary action colour (replaces slate-950 buttons) |
| `brand-violet-light` | `#ede9fe` | Violet tint surfaces |
| `brand-teal` | `#0d9488` | Progress, success, "what you did well" |
| `brand-amber` | `#d97706` | Warnings, challenges, "common mistake" |
| `brand-coral` | `#e11d48` | Errors, grammar issues |
| `brand-sky` | `#0284c7` | Speaking/listening skill (future) |
| `brand-emerald` | `#059669` | Scores ≥ 75 |
| Slate palette | existing | Surfaces, borders, secondary text |
| Indigo palette | existing | Accent (kept for continuity) |

Custom tokens added to Tailwind config `theme.extend` (or CSS vars in v4 using `@theme`).

## Typography direction

- **Font:** Inter (Variable font via Google Fonts) — already in system stack, just needs explicit import for weight variety
- **Display headings (h1):** `text-3xl–5xl font-extrabold tracking-tight` — stronger than current `font-bold`
- **Section labels (eyebrow):** keep `sp-eyebrow` pattern, upgrade to violet
- **Body:** `text-sm/text-base leading-relaxed` — already good
- **Module titles:** `font-bold text-lg` with left colour bar accent

## Layout rules

1. Mobile-first — all layouts stack vertically, expand on `sm:`/`lg:`
2. Max content width: `max-w-5xl` (unchanged)
3. Card radius: `rounded-2xl` for cards, `rounded-xl` for inputs/chips (unchanged)
4. Page background: gradient from `#f8fafc` → `#eef2ff` (violet tint, not stone) — update `.sp-page`
5. Section spacing: `gap-5` between cards (unchanged)
6. Nav: sticky top, `backdrop-blur`, height `h-14`

## Activity UI patterns

### Learning phase
- Situation: full-width card with coloured left border (violet)
- Persian instruction: RTL block, indigo-tinted, visible label "In Persian"
- Target phrases: `sp-phrase-chip` pills in a wrap row
- Vocabulary: `sp-vocab-chip` pills, slightly different shade
- Example: collapsible `<details>` for space saving on mobile
- Common mistake: amber warning card
- CTA: full-width violet `sp-button-primary` on mobile

### Practice phase
- Situation mini-card stays visible in sidebar/above
- Phrase chips remain visible as reference
- Textarea: large, `min-h-56`, rounded, warm shadow on focus
- Character count: subtle `xs` text below
- Submit CTA: full-width violet

### Feedback phase
- Score: `sp-score-ring` — large number, coloured ring (green/amber/red)
- "What you did well": emerald coach card `sp-coach-message`
- "Feedback in Persian": RTL block, persists with label
- Corrected version: clean code-style block
- Grammar / tone / vocabulary: collapsible cards to avoid overwhelming
- Rewrite challenge: amber challenge card
- Next suggestion: slate-soft info block
- CTAs: Try again (secondary) + Next activity (violet primary)

## Future activity type visual patterns (placeholders only)

| Type | Badge colour | Icon concept | Phase |
|------|-------------|--------------|-------|
| WritingScenario | Violet | Pencil | ✅ now |
| SpeakingRolePlay | Sky | Microphone | Phase 4 |
| ListeningComprehension | Teal | Headphones | Phase 4 |
| VocabularyPractice | Amber | BookOpen | Phase 4 |
| PronunciationPractice | Rose | Waveform | Phase 4 |
| ReadingTask | Emerald | FileText | Phase 4 |

## Implementation phases

### Phase 1 — Brand foundation + dashboard polish *(this sprint)*
- `index.html`: Inter variable font import
- `styles.css`: new colour tokens, update `sp-*` classes, add new utility classes
- Dashboard: hero, path card, module card, skill preview section, Start activity CTA
- App shell nav: responsive navigation with section links
- Landing: colour/type lift only

### Phase 2 — Activity lesson polish *(next sprint or continuation)*
- Learning/practice/feedback states with new chips and coach-feel cards
- `ScoreBadgeComponent`
- Example collapsible
- All three phases polished

### Phase 3 — Learning Path page *(follow sprint)*
- `/my-path` route
- `ModuleCardComponent`
- Progress journey
- Module status states

### Phase 4 — Future activity visual patterns *(follow sprint)*
- `SkillBadgeComponent`
- Activity type placeholder cards on dashboard skill section
- No backend work

## Test plan

| Test | Tool | Scope |
|------|------|-------|
| Angular build | `ng build --configuration=production` | After every phase |
| Playwright smoke | `playwright test` | After dashboard or activity HTML changes |
| dotnet tests | `dotnet test` | No backend changes; run once to confirm no regression |
| Visual review | Browser | Dashboard, activity lesson, landing |

Selectors to keep stable (Playwright depends on them):
- `heading: /Welcome back/` — dashboard h1
- `link: 'Start activity'` — dashboard CTA
- `text: "Today's activity"` — activity lesson eyebrow
- `heading: /Follow-up email/` — activity h1 (driven by AI data, not hardcoded)
- `button: 'Start writing'`
- `label: 'Write your response'`
- `button: 'Get feedback'`
- `text: 'Overall score'`
- `button: 'Next activity'`

## Risks

| Risk | Mitigation |
|------|-----------|
| Tailwind v4 `@theme` syntax unfamiliar | Use CSS custom properties in `:root` instead of `tailwind.config.js` for v4 compatibility |
| Playwright selectors break after text changes | Keep eyebrow/label/button text identical; only change visual styling |
| Over-engineering component tree | Default to `sp-*` classes; only extract components for reused templates |
| Font import adds latency | Use `display=swap` and preconnect; fallback to system Inter is already good |
| Phase 1 scope creep | Hard stop after dashboard + nav + styles; activity lesson is Phase 2 |

## Files to change (Phase 1)

```
src/LinguaCoach.Web/src/index.html
src/LinguaCoach.Web/src/styles.css
src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.html
src/LinguaCoach.Web/src/app/features/dashboard/dashboard/dashboard.component.ts
src/LinguaCoach.Web/src/app/features/landing/landing.component.ts
src/LinguaCoach.Web/src/app/app.routes.ts                        (add /my-path stub)
src/LinguaCoach.Web/src/app/features/learning-path/ (new)        (LearningPathPage)
```

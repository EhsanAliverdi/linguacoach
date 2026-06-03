# SpeakPath Design System

## Brand direction

SpeakPath is an AI-powered English learning platform for practical workplace and real-life communication. The brand supports immigrant professionals who are skilled at their jobs but need confidence and clarity in workplace English — writing emails, following up professionally, asking for approvals, and navigating workplace culture.

The brand should feel:

- **Warm and supportive** — like a knowledgeable friend coaching you through a real situation, not a red-pen teacher marking your work
- **Confident and professional** — the product is serious and career-relevant, not childish or gamified
- **Modern and interactive** — colourful, responsive, and rewarding to use
- **Focused and clear** — every screen communicates one purpose; no clutter

The brand does **not** feel like:
- A corporate HR compliance tool
- A grammar textbook
- A generic SaaS dashboard
- A consumer language-learning game (no streaks, no coins, no cartoon characters)

## Colour palette

### Brand tokens

```css
:root {
  --sp-violet:        #6d28d9;   /* primary actions, brand accent */
  --sp-violet-mid:    #7c3aed;   /* hover states */
  --sp-violet-light:  #ede9fe;   /* violet tint surfaces */
  --sp-violet-dark:   #4c1d95;   /* dark backgrounds, nav */

  --sp-teal:          #0d9488;   /* progress, success, "what you did well" */
  --sp-teal-light:    #ccfbf1;   /* teal tint */

  --sp-amber:         #d97706;   /* challenges, warnings, common mistakes */
  --sp-amber-light:   #fef3c7;   /* amber tint */

  --sp-coral:         #e11d48;   /* errors, grammar issues */
  --sp-coral-light:   #ffe4e6;   /* coral tint */

  --sp-sky:           #0284c7;   /* speaking/listening future skill type */
  --sp-emerald:       #059669;   /* scores ≥ 75, positive reinforcement */

  --sp-ink:           #0c0a1e;   /* primary text, nav background */
  --sp-surface:       #f8fafc;   /* page background base */
}
```

### Semantic usage

| Context | Token | Tailwind equivalent |
|---------|-------|---------------------|
| Primary CTA | `--sp-violet` | `bg-violet-700` |
| Hover CTA | `--sp-violet-mid` | `bg-violet-600` |
| Brand tint surface | `--sp-violet-light` | `bg-violet-50` |
| Progress bar fill | `--sp-teal` | `bg-teal-600` |
| "What you did well" | `--sp-teal` | `bg-teal-50 border-teal-200` |
| Common mistake | `--sp-amber` | `bg-amber-50 border-amber-200` |
| Grammar error | `--sp-coral` | `bg-rose-50 border-rose-200` |
| Score ≥ 75 | `--sp-emerald` | `text-emerald-600` |
| Score 50–74 | `--sp-amber` | `text-amber-600` |
| Score < 50 | `--sp-coral` | `text-rose-600` |
| Nav / dark surface | `--sp-ink` | `bg-[#0c0a1e]` |
| Page background | gradient | `bg-gradient-to-b from-slate-50 to-violet-50` |

### Activity type colours

| Activity type | Colour | Badge class |
|---------------|--------|-------------|
| WritingScenario | Violet | `sp-skill-badge-writing` |
| SpeakingRolePlay | Sky | `sp-skill-badge-speaking` |
| ListeningComprehension | Teal | `sp-skill-badge-listening` |
| VocabularyPractice | Amber | `sp-skill-badge-vocabulary` |
| PronunciationPractice | Rose | `sp-skill-badge-pronunciation` |
| ReadingTask | Emerald | `sp-skill-badge-reading` |

## Typography

### Font stack

```css
font-family: 'Inter Variable', 'Inter', system-ui, -apple-system, sans-serif;
```

Import via Google Fonts in `index.html`:
```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Inter:ital,opsz,wght@0,14..32,100..900;1,14..32,100..900&display=swap" rel="stylesheet">
```

### Scale

| Role | Class | Size | Weight |
|------|-------|------|--------|
| Page hero | `sp-display` | `text-4xl sm:text-5xl` | `font-extrabold` |
| Section heading | `sp-heading` | `text-2xl sm:text-3xl` | `font-bold` |
| Card title | `sp-card-title` | `text-lg` | `font-bold` |
| Eyebrow label | `sp-eyebrow` | `text-xs uppercase` | `font-bold` letter-spacing wide |
| Body | (base) | `text-sm leading-relaxed` | `font-normal` |
| Caption | `sp-caption` | `text-xs` | `font-medium` |

## Reusable component classes (styles.css)

All defined in `@layer components` using Tailwind `@apply`.

### Layout

```
.sp-page           — page wrapper, gradient background, min-h-dvh
.sp-shell          — max-w-5xl, horizontal padding, vertical padding
.sp-narrow-shell   — max-w-xl, for auth/onboarding flows
```

### Brand

```
.sp-brand          — logo lockup inline-flex
.sp-brand-mark     — square brand icon (S monogram)
```

### Cards

```
.sp-card           — white card, rounded-2xl, border, shadow-sm
.sp-card-soft      — tinted soft card (violet-50)
.sp-module-card    — module list card with left status bar
.sp-feedback-card  — feedback section card with subtle left border
.sp-coach-message  — warm coach comment (teal tint)
```

### Buttons

```
.sp-button-primary    — violet filled (replaces slate-950)
.sp-button-accent     — indigo filled (kept for variety)
.sp-button-secondary  — white bordered
.sp-button-ghost      — transparent, hover tint
.sp-link              — text link
```

### Form controls

```
.sp-label    — form label
.sp-input    — text input / textarea base
.sp-option   — selectable option card (onboarding)
```

### Status + feedback

```
.sp-eyebrow          — section label (uppercase, small, coloured)
.sp-score-ring       — large score display with coloured ring
.sp-skill-badge      — activity type pill (writing/speaking/etc.)
.sp-phrase-chip      — target phrase pill (violet-tinted)
.sp-vocab-chip       — vocabulary word pill (slate-tinted)
.sp-ai-badge         — "AI generated" label badge
.sp-fallback-badge   — "System" label badge
.sp-module-status    — coloured dot (current / complete / locked)
.sp-progress-track   — slim progress bar row with label
```

### State patterns

```
.sp-empty-state      — centred empty state (icon + heading + body)
.sp-loading-pulse    — skeleton loading animation
.sp-alert-info       — info alert (violet tint)
.sp-alert-error      — error alert (rose tint)
.sp-alert-success    — success alert (emerald tint)
.sp-alert-warning    — warning alert (amber tint)
```

## Layout rules

1. **Mobile-first** — all grids start single column, expand at `sm:` and `lg:`
2. **Content max-width** — `max-w-5xl` (`1024px`) for main content
3. **Narrow shell** — `max-w-xl` for onboarding, auth, settings
4. **Card radius** — `rounded-2xl` (16px) for cards; `rounded-xl` (12px) for inputs and chips
5. **Page background** — gradient: `from-slate-50 to-violet-50/40`
6. **Section spacing** — `gap-5` (`20px`) between stacked cards; `gap-4` inside card content
7. **Navigation** — sticky top, height `h-14`, backdrop blur, white/translucent background
8. **Touch targets** — min height `min-h-11` (44px) for all interactive elements
9. **RTL text** — Persian/source language blocks use `dir="rtl" lang="fa"` with explicit LTR override for labels

## Activity UI patterns

### Learning phase card structure

```
┌──────────────────────────────────────┐
│ [Eyebrow] Today's activity           │
│ [H1] Activity title                  │
│ [Body] Learning goal                 │
├──────────────────────────────────────┤
│ Situation card (violet left border)  │
│ Persian instruction (RTL, indigo)    │
│ Target phrases (phrase chips)        │
│ Vocabulary (vocab chips)             │
│ Example (collapsible)                │
│ Common mistake (amber warning)       │
├──────────────────────────────────────┤
│ [CTA] Start writing →                │
└──────────────────────────────────────┘
```

### Practice phase

```
┌──────────────────────────────────────┐
│ Situation (mini, always visible)     │
│ Phrase chips (reference row)         │
├──────────────────────────────────────┤
│ Textarea — "Write your response"     │
│ [Character count]                    │
│ [CTA] Get feedback                   │
└──────────────────────────────────────┘
```

### Feedback phase

```
┌────────────────┬─────────────────────┐
│ Score ring     │ Corrected version   │
│ (green/amber/  │ Grammar lesson      │
│  red)          │ Tone lesson         │
│                │ Vocabulary          │
│ What you did   │ Rewrite challenge   │
│ well (emerald) │ Next suggestion     │
│                │                     │
│ Vocab to       │ [Try again]         │
│ remember       │ [Next activity]     │
└────────────────┴─────────────────────┘
Feedback in Persian (full width, RTL)
```

## Module card pattern

```
┌─[status bar]──────────────────────────┐
│ [Module N] [SkillBadge]               │
│ Module title                  [●●○○○] │
│ Short description                     │
│ 1 of 3 activities completed           │
└───────────────────────────────────────┘
```

Status bar colours:
- Current module: violet left border, violet-50 background
- Completed: teal left border, teal-50 background
- Locked (future modules): slate-200 left border, white background, opacity-60

## Skill badge patterns

Each activity type gets a small badge used on module cards, activity headers, and the dashboard skill section.

```
[✏ Writing]      violet bg, white text
[🎤 Speaking]    sky bg, white text
[🎧 Listening]   teal bg, white text
[📖 Vocabulary]  amber bg, white text
[🔊 Pronunciation] rose bg, white text
[📄 Reading]     emerald bg, white text
```

Implementation: simple `<span>` with `sp-skill-badge sp-skill-badge-{type}` classes. No Angular component needed until used in 3+ templates.

## Future activity type visual placeholders (Phase 4)

The dashboard skill section shows all 6 activity types as cards. Currently only Writing is active. Others show as "Coming soon" with their brand colour and icon. This communicates the product roadmap to testers without requiring backend implementation.

```
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ ✏ Writing    │ │ 🎤 Speaking  │ │ 🎧 Listening │
│ Active       │ │ Coming soon  │ │ Coming soon  │
└──────────────┘ └──────────────┘ └──────────────┘
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ 📖 Vocab     │ │ 🔊 Pronunc.  │ │ 📄 Reading   │
│ Coming soon  │ │ Coming soon  │ │ Coming soon  │
└──────────────┘ └──────────────┘ └──────────────┘
```

## Implementation constraints

1. **Tailwind v4** — use `@theme` block in `styles.css` for custom tokens (not `tailwind.config.js` which is not used in v4 CSS-first mode)
2. **No component library** — pure Tailwind + `sp-*` classes
3. **Angular components** — only for templates shared across ≥ 3 screens and containing non-trivial logic
4. **No backend changes** — all changes are frontend-only
5. **Progressive enhancement** — Playwright smoke test selectors must not break; use stable text labels for interactive elements

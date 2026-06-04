# SpeakPath Design System

## The prototype is the target

The visual reference for all student-facing screens is the SpeakPath prototype at:

```
docs/design/references/speakpath-prototype/
```

Key prototype files:

| File | Purpose |
|------|---------|
| `SpeakPath.html` | Full app prototype with exact CSS tokens and layout rules |
| `SpeakPath Brand & System.html` | Brand canvas — tokens, components, component examples |
| `app/app.jsx` | App shell: sidebar, topbar, bottom nav, routing |
| `app/screens.jsx` | Dashboard, My Path, Progress, Profile screen layouts |
| `app/activity.jsx` | Activity flow: Intro, Practice, Feedback states |
| `app/components.jsx` | Shared components: Logo, SkillIcon, ScoreBadge, CoachMessage, etc. |
| `app/data.jsx` | Mock data structure — defines the shape of all UI data |

When making any UI decision, inspect the prototype first. Use prototype CSS class names and token values exactly. Do not invent tokens or layouts that conflict with the prototype.

---

## Brand direction

SpeakPath is an AI-powered English learning platform for practical workplace and real-life communication. The brand supports immigrant professionals who are skilled at their jobs but need confidence and clarity in workplace English — writing emails, following up professionally, asking for approvals, and navigating workplace culture.

The brand should feel:

- **Warm and supportive** — like a knowledgeable friend coaching you through a real situation, not a red-pen teacher marking your work
- **Confident and professional** — the product is serious and career-relevant
- **Modern and interactive** — colourful, responsive, and rewarding to use
- **Focused and clear** — every screen communicates one purpose; no clutter

The brand does **not** feel like:
- A corporate HR compliance tool
- A grammar textbook
- A generic SaaS dashboard
- A consumer language-learning game

---

## Design tokens (exact prototype values)

All tokens are CSS custom properties defined in `styles.css` `:root`. They match the prototype exactly.

### Brand gradients

```css
--sp-grad-brand:      linear-gradient(135deg, #FF7A59 0%, #B45CF0 52%, #5B4BE8 100%);
--sp-grad-brand-soft: linear-gradient(135deg, #FFF0EA 0%, #F4ECFF 55%, #ECEAFF 100%);
--sp-grad-warm:       linear-gradient(135deg, #FF8A6B 0%, #FF6FA0 100%);
--sp-grad-cool:       linear-gradient(135deg, #7C6CFF 0%, #5B4BE8 100%);
```

Gradient utility classes (apply to elements directly):
- `.grad-brand` — brand gradient with radial highlight overlays (hero cards)
- `.grad-cool` — cool indigo gradient (My Path header, rewrite challenge card)
- `.grad-warm` — warm coral gradient (streak dots, coming-soon accents)
- `.grad-soft` — soft pastel gradient (profile card, soft backgrounds)

### Skill colour palette

Each skill has three tokens: solid, soft background, ink (on-soft text).

| Skill | Solid | Soft | Ink |
|-------|-------|------|-----|
| Writing | `#5B4BE8` | `#EDEBFF` | `#3A2EA8` |
| Speaking | `#FB6B57` | `#FFEAE4` | `#C23C2C` |
| Listening | `#9B5CF6` | `#F2E9FF` | `#6B2EB8` |
| Vocabulary | `#F0982C` | `#FFF1DC` | `#B26410` |
| Pronunciation | `#10B5A4` | `#DFF6F2` | `#0A7468` |

CSS custom properties follow the pattern `--sp-{skill}`, `--sp-{skill}-soft`, `--sp-{skill}-ink`.

### Neutral palette (warm lilac-tinted)

```css
--sp-ink:     #211B36;  /* primary text */
--sp-text:    #4B4462;  /* body text */
--sp-muted:   #8B85A0;  /* secondary/caption text */
--sp-faint:   #BDB8CC;  /* disabled, placeholder */
--sp-border:  #ECE9F5;  /* default border */
--sp-border2: #E2DEF0;  /* stronger border */
--sp-surface: #FFFFFF;  /* card background */
--sp-surface2:#FBFAFE;  /* elevated surface */
--sp-canvas:  #F6F4FB;  /* page background */
--sp-canvas2: #EFECF9;  /* inset background, stat tile bg */
```

### Status tokens

```css
--sp-success:      #13B07C;
--sp-success-soft: #E0F6EE;
--sp-warn:         #F0982C;
--sp-warn-soft:    #FFF1DC;
```

### Shadow tokens

```css
--sp-sh-xs:   0 1px 2px rgba(33,27,54,.06);
--sp-sh-sm:   0 2px 8px rgba(60,48,140,.07);
--sp-sh-md:   0 8px 24px rgba(60,48,140,.10);
--sp-sh-lg:   0 16px 40px rgba(60,48,140,.14);
--sp-sh-glow: 0 10px 30px rgba(123,80,220,.30);  /* brand button glow */
```

### Radius tokens

```css
--sp-r-sm: 14px;   /* buttons, chips, inputs */
--sp-r:    18px;   /* standard cards */
--sp-r-lg: 22px;   /* main cards */
--sp-r-xl: 28px;   /* hero/gradient cards */
--sp-r-full: 999px; /* pills, full-round */
```

---

## Typography

### Font

```
'Plus Jakarta Sans' — geometric, warm, professional
```

Loaded via Google Fonts in `index.html`. Weights: 400, 500, 600, 700, 800, italic 500.

### Scale

| Role | Size | Weight |
|------|------|--------|
| Page/screen heading | `sp-h1` (25px / 30px desktop) | 800 |
| Section heading | 16px (`sp-section-h h3`) | 800 |
| Card title | 14–15px | 800 |
| Body | 14–15px | 500 |
| Caption / muted | 11–12px | 600–700 |
| Eyebrow | 11px, uppercase, letter-spacing .08em | 800 |

---

## App shell pattern

The prototype defines two nav patterns. The Angular app now implements both.

### Desktop (≥ 900px): Left sidebar

```
┌──────────────┬────────────────────────────────┐
│  sp-side     │  sp-main                        │
│  (264px)     │  ┌────────────────────────┐    │
│              │  │  sp-topbar             │    │
│  Logo        │  │  Good afternoon 👋     │    │
│              │  │  Hi, {name}            │    │
│  Dashboard   │  └────────────────────────┘    │
│  My Path     │  ┌────────────────────────┐    │
│  Practice    │  │  sp-content            │    │
│  Progress    │  │  (page content here)   │    │
│  Profile     │  └────────────────────────┘    │
│              │                                  │
│  streak card │                                  │
└──────────────┴────────────────────────────────┘
```

Sidebar CSS classes: `.sp-side`, `.sp-sidebrand`, `.sp-sidelink`, `.sp-sidelink.is-active`

Active state: white background + `sh-sm` shadow + 4px gradient left bar (`.sp-sidelink.is-active::before`).

### Mobile: Bottom nav

```
┌──────────────────────────────────────────────┐
│  page content                                 │
│  (scrolls above the fixed nav)                │
└──────────────────────────────────────────────┘
┌────┬────┬──────────┬────┬────┐  ← fixed bottom
│Home│Path│[Practice]│Prg.│Prof│
│    │    │  raised  │    │    │
└────┴────┴──────────┴────┴────┘
```

Practice (centre) button: raised gradient circle (`sp-sh-glow`, `border:3px solid canvas`), floats 22px above the bar.

Classes: `.sp-bottomnav`, `.sp-navbtn`, `.sp-navbtn.is-active`.

---

## Placeholder UI rules

> **Rule: never show fake real data. Always show honest placeholders.**

When a backend feature is not implemented, the UI must:

1. Show `—` for numeric values (not `0`, not `7`, not invented numbers)
2. Show "Coming soon" for unimplemented skill cards — not a progress bar with fake values
3. Show an empty state with clear copy ("Complete your first activity to see your progress here.") — not fake activity entries
4. Keep "Coming soon" cards visually present — do not remove them; they communicate the roadmap

When a backend feature ships:
1. Remove the `—` placeholder and bind to real data
2. Remove the "Coming soon" label from skill cards
3. Remove the honest placeholder text from coach/streak cards

Current placeholders and their replacement conditions are tracked in [product-backlog.md](../backlog/product-backlog.md).

---

## Component classes (styles.css)

### App shell

```
.sp-app          — page root; radial gradient background
.sp-shell-app    — flex row (sidebar + main)
.sp-side         — desktop sidebar (display:none mobile, flex desktop ≥900px)
.sp-sidebrand    — logo lockup at top of sidebar
.sp-sidelink     — nav item; .is-active variant
.sp-main         — flex column (topbar + content)
.sp-topbar       — greeting row; max-width 1080px
.sp-content      — scrollable page content; max-width 520px mobile / 1080px desktop
.sp-bottomnav    — fixed mobile bottom nav (hidden ≥900px)
.sp-navbtn       — bottom nav item; .is-active variant
```

### Layout helpers

```
.sp-col          — flex column
.sp-row          — flex row align-center
.sp-icobox       — square icon container (grid place-items-center, radius r-sm)
.sp-statgrid     — 3-column equal stat tiles grid
.sp-skillgrid    — 2-col mobile, 5-col desktop skill cards grid
.sp-grid2        — 2-col desktop grid (1.55fr / 1fr) — dashboard main layout
.sp-h1           — 25px/30px bold heading
.sp-section-h    — section header row (title left, action link right)
.sp-muted        — colour: muted
.sp-faint        — colour: faint
```

### Cards

```
.sp-card         — white card, border, r-lg, sh-sm
.sp-card-pad     — padding 18px (add to sp-card when needed)
.sp-card-soft    — grad-brand-soft background, #EADBFF border (coach card, streak card)
.sp-info-block   — activity lesson info section (same as sp-card, sh-xs)
.sp-module-card  — module journey card; variants: .sp-module-card-current / -complete / -locked
```

### Buttons

```
.sp-button-primary   — gradient brand button with glow shadow
.sp-button-ghost     — white bordered button
.sp-button-secondary — canvas-2 soft button
.sp-btn              — prototype-exact button base
.sp-btn-primary      — prototype-exact gradient variant
.sp-btn-ghost        — prototype-exact ghost variant
.sp-btn-soft         — prototype-exact soft variant
.sp-btn-block        — full-width button
```

### Pills, chips, badges

```
.sp-pill               — small rounded label (inline-flex, r-full)
.sp-chip               — interactive phrase chip (clickable, hover/active states)
.sp-phrase-chip        — display-only phrase chip (activity lesson)
.sp-vocab-chip         — vocabulary chip (vocabulary-soft background, column layout)
.sp-ai-badge           — "✦ AI generated" gradient badge
.sp-fallback-badge     — "Backup content" warning badge
.sp-skill-badge        — skill type badge; variants: -writing, -speaking, etc.
.sp-eyebrow            — uppercase section eyebrow label
```

### Progress

```
.sp-progress-track          — progress bar container (9px height)
.sp-progress-fill           — brand gradient fill
.sp-progress-fill-writing   — writing-colour fill
.sp-progress-fill-speaking  — speaking-colour fill
.sp-progress-fill-vocabulary
.sp-progress-fill-pronunciation
.sp-progress-fill-listening
```

### Collapsible

```
details.sp-collapsible              — wrapper
details.sp-collapsible > summary    — header row (no default marker)
summary .sp-chevron                 — rotating chevron icon
.sp-collapsible-body                — padded content below summary
```

### Coach

```
.sp-coach-bubble   — speech bubble (border-radius: 4px 16px 16px 16px)
```

### State patterns

```
.sp-empty-state     — centred empty state card (dashed border)
.sp-loading-pulse   — skeleton animation
.sp-alert-info      — writing-soft tinted info alert
.sp-alert-error     — speaking-soft tinted error alert
.sp-alert-success   — success-soft tinted success alert
.sp-alert-warning   — vocabulary-soft tinted warning alert
```

### RTL

```
.sp-source-lang-block   — Persian/RTL instruction block (writing-soft, dir:rtl)
```

---

## Activity UI patterns

### Learning (Intro) phase — from prototype `activity.jsx` Intro component

```
[skill badge] [AI badge] [time pill]
h1.sp-h1  — activity title
InfoBlock "The situation"   (icon: chat, accent: writing)
Goal card                   (grad-brand-soft, target icon)
InfoBlock "Phrases to try"  (icon: quote, phrase chips)
InfoBlock "Vocabulary"      (icon: book, vocab chips)
details.sp-collapsible "See an example message"
Warning card                (warn-soft, bulb icon, mistake text)
RTL source-language block   (sp-source-lang-block)
sp-button-primary (full width) "Start writing"
```

### Practice phase — from prototype `activity.jsx` Practice component

```
[skill badge]
h1 "Write your message"
Task card (writing-soft bg, target icon)
Email card (sp-card, no-pad):
  header row: [pen icon] "To: your manager"
  textarea (no border/outline, min-height 180px)
  footer: [word count] [char count / Autosaved]
"TAP A PHRASE TO ADD IT" + phrase chips
details.sp-collapsible "Vocabulary hints"
details.sp-collapsible "Peek at the example"
Coach row: [gradient circle avatar] [microcopy]
sp-button-primary (full width) "Get coach feedback"
```

### Feedback phase — from prototype `activity.jsx` Feedback component

```
Score card (grad-brand-soft):
  SVG ProgressRing (r=38, C=238.76)
  Score number + /100
  Band pill (Great work / Good effort / Keep going)

Coach message row:
  [gradient circle avatar] Pace / your AI coach
  sp-coach-bubble (4px 16px 16px 16px border-radius)

InfoBlock "Polished version"     (corrected pre, success-soft)
InfoBlock "What you did well"    (checkCircle list, success accent)
InfoBlock "Gentle improvements"  (arrow list, vocabulary accent)
InfoBlock "Grammar focus"        (writing accent)
InfoBlock "Tone & politeness"    (listening accent)
InfoBlock "Vocabulary to remember" (vocab pills)

Rewrite challenge card (grad-cool, refresh icon, white text)

InfoBlock "Suggested next"       (next activity card)

Actions: [Try again (ghost, flex-1)] [Continue to next (primary, flex-2)]
sp-button-secondary (full width) "Back to my path"  →  /my-path
```

---

## Module card pattern — from prototype `components.jsx` ModuleCard

```
sp-module-card
  [SkillIcon 48px, soft or solid depending on state]  [content]  [chevron/lock]

content:
  MODULE {n}  [IN PROGRESS / ✓ COMPLETE / SOON] pill
  h3 title (15px, 800)
  description (12.5px, muted)
  [progress bar + X/Y count]  ← only for current or complete

States:
  current  → border: writing, shadow: sh-md
  complete → border: success
  locked   → border: border2, opacity: .72, cursor: default
```

---

## Skill badge / card patterns

### Skill badge (pill)
```
.sp-skill-badge.sp-skill-badge-{skill}
  background: var(--sp-{skill}-soft)
  color: var(--sp-{skill}-ink)
```

### Skill card (dashboard grid)
```
sp-card (padding 14px, flex column, gap 11px)
  SkillIcon (38px, soft=true)
  skill name (14px, 800) + level label (11.5px, muted)
  sp-progress-track
    sp-progress-fill-{skill}

Active (Writing): border-color: writing, hover: translateY(-3px)
Coming soon: opacity: .65, no hover, no link
```

---

## Future activity type placeholders

All unimplemented skill cards must remain on the dashboard with "Coming soon" state. Remove the label only when the backend feature ships. Do not remove cards from the grid.

| Skill | Badge colour | Status |
|-------|-------------|--------|
| Writing | `--sp-writing` | ✅ Active |
| Speaking | `--sp-speaking` | Coming soon |
| Listening | `--sp-listening` | Coming soon |
| Vocabulary | `--sp-vocabulary` | Coming soon |
| Pronunciation | `--sp-pronunciation` | Coming soon |

---

## Implementation constraints

1. **Prototype first** — inspect `docs/design/references/speakpath-prototype/` before adding or changing any UI
2. **Exact token values** — use the CSS custom properties; do not hardcode hex colours in templates
3. **No fake data** — placeholder UI must always be honest. See [placeholder rules](#placeholder-ui-rules)
4. **No component library** — pure `sp-*` classes; Angular components only where justified (≥3 uses, non-trivial logic)
5. **Tailwind v4** — CSS-first; no `tailwind.config.js` for custom tokens; use `:root` CSS custom properties
6. **Mobile-first** — all layouts stack single column, expand at 900px (sidebar breakpoint) or earlier
7. **Touch targets** — all interactive elements: `min-height: 44px`
8. **RTL text** — Persian source-language blocks: `dir="rtl" lang="fa"`; labels and eyebrows use `dir="ltr"` override
9. **Coming soon features** — remain visible in the UI, visually present but functionally disabled (no `href`, no `routerLink`, reduced opacity)
10. **Playwright stability** — use `getByRole` + regex names for interactive elements so text changes do not silently break tests; always run smoke test after HTML changes

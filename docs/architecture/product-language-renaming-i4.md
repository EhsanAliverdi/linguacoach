---
status: planned
lastUpdated: 2026-07-10
owner: product / engineering
---

# Phase I4 — Product Language Cleanup (Rename)

**Date decided:** 2026-07-10
**Type:** decision + scope doc only. **Nothing has been renamed yet.** This document captures the
naming decision and where it touches, so a future implementation phase can execute it without
re-deriving the scope from scratch.

## The decision

Current internal/admin terminology grew out of the H-track's bank-first model (`LearnItem`,
`ActivityDefinition`, `ModuleDefinition`, "Daily Lesson Module Pipeline") and is more
implementation-flavored than product-friendly. The user has decided on clearer product/admin
language:

| Current name | New name |
|---|---|
| `LearnItem` | **Lesson** |
| `ActivityDefinition` | **Exercise** |
| `ModuleDefinition` | **Module** |
| Daily Lesson pipeline/container (H6) | **Daily Plan** or **Today Plan** (final pick TBD — see Open Questions) |

**Composition model in the new language:**
- A **Module** contains: **Lesson** + **Exercise** + **Feedback**.
- A **Daily Plan** contains several **Modules**.

This is a renaming/relabeling decision, not a data-model change — the existing entity
relationships (`ModuleDefinitionLearnItemLink`, `ModuleDefinitionActivityLink`, `FeedbackPlanJson`,
`IDailyLessonModuleSelectionService`'s "select several approved Modules for today" behavior)
already match this composition; only the names change.

## Why now

I2 (just completed, 2026-07-10) deleted the entire legacy fallback pipeline, so bank-first
Learn/Activity/Module is now the *only* content-delivery model — Today and Practice Gym have
nothing else standing behind these names anymore. That makes this the right moment to fix the
names before I3's nav consolidation locks in a final admin IA, and before I5 (expanding bank-first
coverage to the other exercise types) multiplies the number of places these names appear.

## Scope survey (as of 2026-07-10, before any rename)

Rough occurrence counts, to size the effort — not a file-by-file worklist:

| Term | Backend (`src/`, `.cs`) | Frontend (`src/LinguaCoach.Web/src`, `.ts`/`.html`) |
|---|---|---|
| `LearnItem` | ~62 files | ~19 files |
| `ActivityDefinition` | ~50 files | ~11 files |
| `ModuleDefinition` | ~44 files | ~8 files |
| `DailyLesson` | ~20 files (backend + frontend combined) | |

This spans: Domain entities, EF configurations + a **data migration** (table/column renames —
`learn_items`→`lessons` etc., or an additive rename via new tables if a zero-downtime path is
preferred), Application contracts/DTOs, Infrastructure services (generation, lookup, launch
bridge), API controllers/routes (`/api/admin/learn-items`→`/api/admin/lessons`,
`/api/admin/activities`→`/api/admin/exercises`, `/api/admin/modules` stays), Angular
components/services/models/routes, nav labels, and every doc that currently uses this vocabulary
(`docs/architecture/product-model-realignment-h0.md`, `docs/architecture/learning-activity-engine.md`,
every H3-H10/I0-I2 review doc — those stay as historical record with old names, but
forward-looking docs should adopt the new vocabulary once implemented).

## Known naming collision to resolve before implementation

**`/admin/lessons` is already taken.** It currently routes to `AdminLessonsComponent` — the "Today
Delivery Health" diagnostics page (originally about the legacy session-generation pipeline; largely
inert now since I2B removed the generation jobs it diagnosed, and I2B already turned
`AdminGenerationController`'s retry/manual-generate actions into no-ops). Renaming `LearnItem` →
"Lesson" needs either:
- **(a)** retire/fold "Today Delivery Health" elsewhere (it's already a candidate for
  Advanced/Diagnostics or outright removal given I2B left it mostly inert — worth a quick audit at
  I4 kickoff rather than assuming), freeing `/admin/lessons` for the renamed Learn Item page, or
- **(b)** give the renamed Learn Item page a different route (e.g. `/admin/lesson-content`) and
  leave `/admin/lessons` as-is.

This must be decided at I4 kickoff, not assumed — flag it as the first open question to resolve
with the user before writing any code.

## Suggested implementation shape (not started, for a future planning session)

Given the scale (≥100 backend files, ≥30 frontend files, a data migration, and every admin nav
label), a future I4 planning session should size this the same way I0-I2 were: a dedicated
Step -1/audit pass, then implementation, likely split by layer or by concept rather than attempted
as one sitting:
- **I4a** — Domain/Persistence rename (entities, EF configs, migration) + Application/Infrastructure
  rename (DTOs, services, contracts).
- **I4b** — API routes/controllers rename (breaking route changes — old routes could redirect or
  just be replaced outright, consistent with this project's "not yet deployed, no compatibility
  shim needed" convention used throughout the I-track).
- **I4c** — Frontend rename (components, services, models, nav labels, routes) + the
  `/admin/lessons` collision resolution above.
- **I4d** — "Daily Lesson" → Daily Plan/Today Plan rename (a smaller, more contained slice —
  `IDailyLessonModuleSelectionService`, `TodaysSessionResult`, dashboard UI copy).

Each slice should get its own build/test verification pass and its own commit, matching the I0-I2
pattern (small, verifiable, sequential changes rather than one giant unreviewable diff).

## Open questions (resolve before implementation starts)

1. **Daily Plan vs. Today Plan** — final pick. "Today Plan" reads more naturally in-product
   ("Here's your Today Plan"); "Daily Plan" is more generic/timeless. Needs a decision, not a
   default.
2. **The `/admin/lessons` collision** (above) — retire/relocate "Today Delivery Health," or pick a
   different route for the renamed Learn Item page.
3. **Database rename strategy** — rename tables/columns directly via migration (simplest, matches
   this project's convention of not carrying compatibility shims for a not-yet-deployed product),
   or something more conservative. Given every prior I-track phase (I0-I2) renamed/dropped tables
   directly with no compatibility period, direct rename is the consistent default unless the user
   says otherwise.
4. **Does "Exercise" collide with anything?** — `ExercisePatternDefinition`/`ExerciseTypeDefinition`
   (the legacy pattern-catalog entities, still present, referenced by the deleted-but-not-fully-purged
   legacy naming in a few places per I2's residual-cleanup notes) already use "Exercise" as a
   prefix for a different, older concept. Renaming `ActivityDefinition` → `Exercise` needs a
   pass to confirm this doesn't create confusing double-meaning — worth auditing whether
   `ExercisePatternDefinition`/`ExerciseTypeDefinition` are still load-bearing post-I2 or are
   themselves cleanup candidates first.

## What this phase is NOT

- Not a data-model change — no new relationships, no new fields beyond what renaming requires.
- Not a scope expansion of bank-first coverage (that's I5) or AI-driven generation (I6).
- Not implemented by this document — this is the decision + scope record only.

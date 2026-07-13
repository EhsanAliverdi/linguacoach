# Scoping: AI-assisted import column-mapping detection (Phase K1, proposed)

**Date:** 2026-07-13
**Related:** Follow-up to the CEFR-J import bug fix (`docs/reviews/2026-07-13-cefrj-import-field-recognition-bugfix-review.md`)
**Type:** Implementation readiness review — **no code written**, scoping only

## Trigger

After fixing the specific `headword`/`CEFR` column-name gap, the user asked two follow-up
questions:
1. Shouldn't the file initially be processed by AI to identify structure, rather than relying on
   an ever-growing fixed list of recognized column names?
2. Error messages need to be a lot better generally, since data comes from different sources with
   different structures.

Confirmed: no AI structure/column detection exists anywhere in this codebase today. The "Mixed
(auto-detect per row)" option built in J5b is deterministic field-name matching over a fixed set,
explicitly *not* AI — the UI has said "AI structure detection is not yet implemented" since Phase
H2 and stayed that way through all of J5a-d. This document scopes what a real version of that
would look like, following this codebase's existing AI-feature conventions, before any code is
written.

## What already exists (researched, not built here)

- **AGENTS.md §4 AI Rules** (lines 173-235): AI must not be the system planner — the backend owns
  selection/structure/validation; AI does one small bounded task and returns structured JSON that
  the backend validates. Every prompt must be token-bounded (no full-file dumps). Every call is
  tracked (`featureKey`/provider/model/userId/isFallback/wasSuccessful/tokens/cost/correlationId).
  Provider failure must degrade gracefully with a fallback, never crash the caller. Tests must
  never depend on a real AI provider.
- **Closest existing precedent**: `ResourceCandidateAnalysisService` (Phase E2) — AI proposes an
  advisory classification (CEFR/skill/subskill) for an already-staged candidate; a fully separate
  deterministic service (`ResourceCandidateValidationService`) is the only thing that ever decides
  pass/fail. AI failure (unavailable, bad JSON, one retry exhausted) degrades to a graceful
  `Success=false` result — never throws to the caller, never blocks the underlying deterministic
  pipeline from working.
- **Category**: a "propose the column mapping" task is a classification/judgment pass over
  admin-supplied data, matching `llm.evaluation` by existing convention (same category
  `resource_candidate_analyze` already uses) — not `llm.generation` (that's for producing new
  student-facing content) or `llm.memory`.
- **Prompt storage**: DB-seeded, versioned, content-hash-addressed via `DefaultAiSeeder` — a new
  prompt key/template registered the same way `resource_candidate_analyze` is.
- **Test safety**: `AiProviderResolver` already treats `provider="fake"/model="fake"` as
  categorically unusable and every seeded test category defaults to it — a new AI-backed feature
  is automatically exercised via its graceful-degrade path in CI with zero extra test scaffolding,
  same as every existing AI feature.

## Proposed design (for discussion — not yet approved)

**Core principle, mirroring the E2 precedent exactly: AI proposes, the existing deterministic
pipeline decides.** The AI call never changes what a "recognized field" means or bypasses any
existing gate — it only proposes a *column rename* that the admin confirms, which is then applied
as a pure header-rewrite *before* the unchanged `ResourceImportService` pipeline runs. This means
zero changes to Gate 1/2/3, `InferCandidateType`, `ExtractCanonicalTextForType`, or any publish/
preview logic — the AI-assisted path and today's fixed-alias path both funnel into the exact same,
already-tested deterministic machinery.

1. **New step in the file-upload flow**: after a file is selected but before "Import as
   candidates" runs, parse just the header row (+ a small bounded sample, e.g. 3-5 rows) and call
   a new `IResourceImportColumnMappingService` — one AI call, `llm.evaluation` category, prompt
   receives only the header row + sample rows (truncated/bounded per AGENTS.md), returns a
   structured JSON proposal: `{ "headword": "word", "CEFR": "cefrLevel", "pos": null, ... }` (null
   = "no confident mapping, leave as-is").
2. **Admin review/confirm UI**: show the proposed mapping as an editable table (source column →
   suggested recognized field, admin can change or clear any row) before the import actually
   submits. This is the "backend/human decides" half of the AGENTS.md rule — the AI's proposal is
   never applied silently.
3. **Apply as a header rewrite**: on confirm, the chosen mapping renames matching columns in the
   parsed rows in-memory, then feeds the *exact same* `ResourceImportService.ImportAsync` call
   that runs today — no changes to the import pipeline itself.
4. **Graceful degrade**: if AI is unavailable (fake/fake in tests, or a real outage in prod), skip
   straight to today's behavior — the review step shows "AI suggestion unavailable" and the admin
   can still rename columns manually or proceed with the existing fixed-alias matching. The feature
   is additive, never a required step.

## Complementary, AI-independent improvement (small, could ship first or alongside)

The "better error messages" ask doesn't need AI at all. Gate 3 got this treatment in the bug fix
just shipped (lists the row's actual columns + a concrete suggestion). The same treatment could
extend to:
- Gate 1 (English-only rejection) — currently a generic detected-language message.
- The within-run/cross-run duplicate-fingerprint rejection — currently doesn't say *which* other
  row/run it collided with.

This is a much smaller, self-contained pass with no AI dependency and no new UI — could be done as
its own quick fix independent of the AI-mapping feature's timeline.

## Decisions (user, AskUserQuestion, 2026-07-13)

1. **Both flows** — file-upload (`AdminResourceImportController`) AND paste-based Content Import
   (`AdminContentImportController`) get the AI mapping-proposal step, not just file-upload.
2. **Always show the review/confirm UI** — every CSV/JSON import surfaces the proposed mapping for
   confirmation, even when it's trivially correct (every column already matches a recognized
   name). Consistent behavior over saving a click on the common case.
3. **Bundled into one phase (K1)** — the Gate 1/duplicate-message improvements and AI-assisted
   column mapping ship together as a single implementation pass, not sequenced separately.

## Risks

- Adds a second AI call to the import flow (cost/latency), mitigated by being opt-in-by-default-
  skip on graceful degrade and by the existing per-call usage tracking/cost logging.
- The "propose a rename, admin confirms" UX is one extra step in a flow that's currently one click
  ("Import as candidates") — needs to stay unobtrusive when the AI's suggestion is trivially
  correct (e.g. auto-accept when there's exactly one plausible mapping per column, only surface
  the review UI for ambiguous/low-confidence proposals) to avoid friction on the common case.
- Not scoped here: extending AI-assisted mapping to JSON files with nested/non-tabular structure
  (out of scope — the current pipeline is CSV/JSONL-row-shaped, this proposal doesn't change that
  assumption).

## Final verdict

Scoping approved by the user (2026-07-13, three decisions recorded above). Design follows this
codebase's existing AI-feature conventions closely (same category, same prompt-storage/versioning
pattern, same "AI proposes, deterministic system decides" split already proven in Phase E2).
Proceeding to implementation as Phase K1.

## Next recommended action

Implement Phase K1: (a) Gate 1/duplicate-message improvements (no AI, quick), (b) new
`IResourceImportColumnMappingService` (AI proposes a column rename, `llm.evaluation` category, new
DB-seeded prompt), (c) admin review/confirm UI wired into both the file-upload and paste-based
Content Import flows, always shown. See the follow-up implementation review for what actually
shipped.

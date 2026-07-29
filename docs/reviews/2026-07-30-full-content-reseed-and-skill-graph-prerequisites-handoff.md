# Full Content Reseed & Skill Graph Prerequisites — Session Handoff

**Date:** 2026-07-30
**Related:** Full content reseed plan (`melodic-meandering-hickey.md`, approved in an earlier plan-mode session), Skill Graph admin UX
**Files reviewed/changed:** see commit list below
**Status:** Complete and merged to `main`. No open PRs — every commit landed directly on `main` per this session's working style.

## Summary

This session executed the entire "Full Content Reseed" plan end to end (Phases A–G plus a reading-passage integration phase not in the original letter scheme), then, in response to direct user feedback, closed a real functional gap the reseed had left behind: the Skill Graph's `SkillGraphPrerequisiteEdge` table was completely empty despite 14,070 seeded nodes, and the admin Graph UI didn't scale to that node count. Both are now fixed.

Everything described here is live in the local dev DB and pushed to `origin/main`. Nothing is pending review or blocked.

## What was built, in order

### 1. Content reseed (Phases A–G + reading integration)

A standalone console tool, `tools/LinguaCoach.ContentSeeder`, reads hand-authored (or, for reading, pre-existing) JSON seed files and loads the DB via the existing deterministic Lesson/Exercise/Module generation pipeline — no controllers, no live AI calls except the one pre-authorized exception (TTS for listening audio).

| Domain | Content chains | Seed file(s) | Notes |
|---|---|---|---|
| Grammar | 500 | `data/seed-json/grammar-seed.json` | CEFR-J grammar profile, hand-written explanations |
| Vocabulary | 9,775 of 9,934 target | `data/seed-json/vocabulary-seed.json` | 3 words permanently skipped — genuine source-CSV data-quality artifacts (invalid part-of-speech pairings), never fabricated |
| CEFR-scale taxonomy | 236 nodes, no content chain (pure taxonomy) | `data/seed-json/cefr-scales-seed.json` | Real Council of Europe CEFR Companion Volume descriptor scales |
| Speaking | 180 | `data/seed-json/speaking-seed.json` | 10 topics × 6 levels × 3 prompts, hand-written |
| Listening | 180 | `data/seed-json/listening-seed.json` | 10 topics × 6 levels × 3 passages; **real Gemini TTS audio**, uploaded to MinIO |
| Reading | 3,053 | `data/cerfj-reading.json` (pre-existing, parsed directly) | Auto-linked to matching vocabulary leaves + the CEFR reading-comprehension scale |
| **Total** | **13,688 content chains** | | Each with a `ResourceBankItem` → `Lesson` → `Exercise` → `Module` → `ModuleSkillGraphNodeLink` |

Key implementation details worth knowing if you touch this again:

- **`LeafContentSeeder.SeedOneAsync`** is idempotent by checking `ModuleSkillGraphNodeLinks` for the target `SkillGraphNodeId` before doing any work — safe to re-run after an interrupted process.
- **Speaking/Listening** each leaf gets its own dedicated `SkillGraphNode` (for idempotency) *plus* a secondary link to a shared Phase-E CEFR-scale leaf (e.g. `speaking.scale_overall_spoken_production.b1`). `SeedOneAsync` takes an `additionalSkillGraphNodeIds` param for this.
- **Listening audio**: `SeedListeningAsync` calls `GeminiTextToSpeechService` directly (bypassing `TtsProviderResolver`, whose `tts.listening` DB category defaults to `"fake"` for CI safety), using the real key from `AiProviderCredentials`. Synthesized audio is cached locally to `data/seed-audio/listening/` (gitignored, ~76MB — too large to commit; the transcript text in `listening-seed.json` is the durable source of truth) so a re-run never re-spends TTS quota.
  - **Bug found and fixed during this**: `MinioFileStorageService.SaveAsync` does **not** throw when the target bucket doesn't exist — it silently no-ops server-side while returning success. This orphaned a handful of `AudioStorageKey`s before a `storage.HealthCheckAsync()` guard was added at the start of `SeedListeningAsync`. If you ever see a `ResourceBankItem` with an `AudioStorageKey` that 404s on playback, this is almost certainly why — check whether the MinIO bucket existed at seed time.
  - The local dev stack's `FILE_STORAGE_PROVIDER` was switched from `Local` to `Minio` (via an untracked `.env` at repo root) so seeded audio is actually reachable by the running `api` container, not just the console tool's local disk. If you're setting up a fresh dev environment and audio playback 404s, check this.
- **Reading**: parses `data/cerfj-reading.json` (a JSONL chat-format dataset) directly — no separate authored seed file. Strips an occasional "Here is a reading passage about X:" preamble and `**bold**` markdown (the deterministic cloze composer HTML-encodes text verbatim, so literal asterisks would otherwise show up in the student-facing exercise). Vocabulary cross-linking is done via an in-memory headword lookup built once from all ~9,775 vocabulary leaves (including slash-variant splitting, e.g. `"color/colour"` → two lookup keys).
- **Known pre-existing, unrelated orphans**: ~200 `ResourceBankItem` rows from the app's own `InternalResourceSeedPack*Seeder` classes (unpublished admin-review candidates) are NOT part of this reseed and were deliberately left untouched — don't clean these up without understanding that separate candidate-review workflow first.

### 2. Skill Graph prerequisite edges (in response to direct user feedback)

The reseed above shipped 14,070 `SkillGraphNode`s but **zero** `SkillGraphPrerequisiteEdge` rows. User feedback: *"none of the nodes have linking edge... this doesn't make sense."* An agent traced every runtime consumer of this table before any edges were written — see findings below, since they explain *why* this matters and *how much* it matters.

**Real functional impact of zero edges** (confirmed by tracing the code, not guessed):
- `SkillGraphRoutingService` never read this table at all (a stale planning-doc claim to the contrary was wrong).
- Today Plan / Practice Gym's `HasUnmetPrerequisite` composer signal was always `false` — a soft ranking nudge that silently never fired.
- `LearningPlanService.IsBlocked` (a **real hard gate** on learning-plan objectives) was always `false` for every objective — this method was written specifically to fix an earlier "Bug #5" (permanently-inert `IsBlocked`), and with no edge data its fix had no effect.
- Admin Skill Graph UI's per-node "Prerequisites"/"Unlocks" panels were empty for all 14,070 nodes.
- The *only* real sequencing gate in the app is the student's coarse CEFR level (A1–C2), set only by placement test/onboarding/admin override, **never** auto-advanced by mastery evaluation, and completely decoupled from this edge table.

**What was built** — 4,482 total `SkillGraphPrerequisiteEdge` rows, via a new domain-agnostic loader in `ContentSeeder` (`SeedPrerequisitesAsync`, invoked as `dotnet run --project tools/LinguaCoach.ContentSeeder -- prerequisites <file.json>`, reading `{Node, Prerequisite, Reason}` triples keyed by `SkillGraphNode.Key`):

| Domain | Edges | Method | Seed file |
|---|---|---|---|
| Grammar | 795 | Two layers: (1) 229 mechanical within-family edges derived from the CEFR-J dataset's own aff/neg/int_aff/int_neg suffix convention (negative/interrogative forms depend on their affirmative); (2) 566 hand-curated cross-family curriculum edges (present→past→future tense/aspect, active→passive, modal nuance ladders, sentence-pattern complexity, conditionals) | `data/seed-json/grammar-prerequisites-seed.json` |
| Speaking | 450 | Topic-scoped CEFR-level chaining: every leaf at level N depends on every leaf at level N-1 in the same topic container | `data/seed-json/speaking-prerequisites-seed.json` |
| Listening | 450 | Same topic-scoped chaining as Speaking | `data/seed-json/listening-prerequisites-seed.json` |
| Reading | 2,787 | No topic grouping exists to chain within (each of the 3,053 passages has an essentially unique topic — Decision 7 of the original plan), so each non-A1 passage instead depends on the shared `reading.scale_overall_reading_comprehension.<level-1>` CEFR-scale leaf | `data/seed-json/reading-prerequisites-seed.json` |
| **Vocabulary** | **0 — explicit decision, not an oversight** | User was asked and chose to skip: word-to-word prerequisites aren't pedagogically meaningful at ~9,775-leaf granularity | — |

Every edge set was verified offline (0 duplicates, 0 cycles via DFS) before seeding, and one real bug was found and fixed in the process: `"PASSIVE: PRESENT — AFF. DEC."` is the one grammar family whose affirmative form has no `_aff` suffix (unlike every sibling form), so the classifier never recognized it as that family's entry point — it was completely disconnected and the family's representative had wrongly defaulted to the NEG form. Fixed in `build-grammar-prereqs.js`'s companion script (not committed — scratch tooling; the *output* JSON is what's committed) before seeding.

**AdminSkillGraphController.GetGraph** now accepts optional `cefrLevel`/`skill` query params, filtering both nodes and the edges returned (an edge is only included if both endpoints are within the filtered node set). Left unfiltered when both are omitted, since the node-detail page's "where this node sits" BFS neighborhood preview legitimately needs the full graph (a prerequisite can cross level/skill boundaries).

### 3. Skill Graph admin UI fixes (direct user feedback, same session)

1. **Filter gate**: the Graph tab previously fetched and rendered the *entire* graph (14,070+ nodes) on first click — illegible and no longer viable as a payload. It now requires selecting both a CEFR level and a skill (e.g. "A1" + "Grammar") before it fetches or draws anything; changing either re-fetches. Verified live: A1+Grammar renders 228 nodes / 196 edges cleanly.
2. **Node shape**: leaf nodes were plain circles (Cytoscape's implicit default) with labels overflowing below them — illegible once labels got long or nodes got dense. Switched to `round-rectangle`, sized to the label text (`width: 'label', height: 'label'`), label centered inside. Added a `labelColorFor()` helper since C1/C2's dark CEFR background colors needed light text for contrast once the label moved inside the shape.
3. **Removed the now-redundant in-canvas CEFR toggle legend** (`activeLevels`/`toggleLevel`) — user follow-up: since the page-level filter already gates to one CEFR level before the graph ever loads, the A1–C2 toggle chips inside the canvas could only ever show/hide everything or nothing.

Files touched: `AdminSkillGraphController.cs`, `admin.api.service.ts`, `admin-skill-graph.component.ts`/`.html`, `sp-admin-skill-graph-viz.component.ts`.

## Verification performed

- Full backend suite (`dotnet test`) run and green after every phase: **30 ArchitectureTests / 2,582 UnitTests / 1,410 IntegrationTests**, every single time, no exceptions.
- Frontend Karma suite has **234 pre-existing failures**, confirmed via a stashed baseline run (baseline showed 237 failures — this session's changes didn't add any; the small variance is pre-existing flakiness unrelated to this work). Do not assume these 234 are new — they predate this session.
- Admin UI spot-checked live via one-off Playwright scripts (written, run, screenshotted, then deleted — not committed, since they were verification scaffolding, not durable tests) for: a Reading module's admin detail view, a Listening module's admin detail view, the Skill Graph coverage overview, a grammar node's Prerequisites/Unlocks panel (before and after the edge-seeding), and the new Graph-tab filter gate + square nodes.
- All DB row counts cross-verified against expected totals at every step (content chains, prerequisite edges, zero orphaned modules).

## What's still open / explicitly deferred

1. **Vocabulary prerequisite edges** — explicitly skipped per user decision (see above). If revisited, the previously-discussed fallback design was CEFR-level chaining *within each of the ~37 topic containers* (not global, not per-word) — same pattern as Speaking/Listening, just scaled to vocabulary's container structure.
2. **CEFR auto-advancement from mastery** — flagged as a related but separate gap: nothing in the app currently promotes a student's CEFR level based on `StudentMasteryEvaluationService` results; level is only set by placement test, onboarding, or admin override. This means even a perfect prerequisite graph has limited effect on real content gating today, since CEFR level (not the prerequisite edges) is the actual hard filter Today Plan/Practice Gym apply. User explicitly deferred this to a separate future session — do not conflate the two when picking this back up.
3. **3 permanently-skipped vocabulary words** (`automatic`/verb, `minster`/verb, `remonstrate`/vern) — genuine source-CSV data-quality artifacts, not a to-do; consistent with the "skip, never fabricate" discipline used throughout.
4. **~200 pre-existing orphaned `ResourceBankItem` rows** from the app's own internal seed packs — not part of this reseed, do not touch without understanding that separate workflow.

## Final verdict

All planned reseed phases (A–G) complete. Skill Graph prerequisite structure now real and connected for grammar/speaking/listening/reading (4,482 edges). Admin Graph UI now scales to the current (and future) node count. No known regressions — backend fully green, frontend failure count unchanged from pre-session baseline. Nothing blocking; the three items above are scoped future work, not loose ends from this session.

## Next recommended action

If continuing this thread of work in a future session: either (a) revisit vocabulary prerequisite edges with the topic-scoped CEFR-chaining design above, or (b) scope the CEFR auto-advancement feature (touches `StudentProfile`, `StudentMasteryEvaluationService`, likely notifications) as its own planning pass — it's a materially different, larger feature than anything done in this session.

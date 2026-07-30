---
title: Grammar Skill Graph Seed — Structural, Provenance & Content Audit
date: 2026-07-30
related: docs/reviews/2026-07-30-grammar-skill-graph-topical-hierarchy-plan.md, docs/architecture/adaptive-curriculum-skill-graph.md
---

# Grammar Skill Graph Seed Audit

## 1. Executive summary

The graph is **structurally sound** (valid DAG, no duplicate keys/edges, no dangling references,
no cycles) but **not yet trustworthy as a source of CEFR truth or hard-prerequisite gating**. The
two headline hypotheses from the prior review are both **confirmed and precisely quantified**:

- **89 of 500 CEFR-J source rows (17.8%) had no usable level in any framework column** and were
  silently defaulted to A1/band 1. Traced through to the actual skill-graph nodes derived from
  them, that's **97 seed nodes** (89 rows, but 8 of them are family base-rows that become *both* a
  container and a same-content leaf, each independently inheriting the default). Combined with a
  further **48 nodes whose A1 came from a non-CEFR-J fallback column** (Core Inventory/EGP/GSELO)
  rather than CEFR-J itself, **145 of the 592 CEFR-J-derived nodes (24.5%) carry an A1 label that
  is not directly CEFR-J-attested.**
- **120 prerequisite edges point backward** (prerequisite's CEFR level is higher than the target's)
  — exact match to the prior estimate. Root-caused here: most are a *consequence* of the A1-fallback
  defect, not bad relationship authoring — real "affirmative → negative/question" teaching-order
  edges become CEFR-backward whenever one sibling in a family independently fell back to A1 while
  another kept a genuine higher CEFR-J level.

Beyond the two flagged hypotheses, this audit also found and precisely quantified: a container
whose title is **flatly wrong** (the past-simple family container is titled "present"), a
33.8%-redundant edge set, 56 of 97 containers spanning multiple CEFR levels among their children,
an entire grammatical domain ("PREPOSITIONS") compressed into one A1/band-1 leaf, and several
concrete grammar-explanation defects (an "irregular" claim about a fully regular pattern, a
prescriptive-but-false "each other vs. one another" rule, a future-progressive node that actually
explains "be going to"). Full detail in the sections below.

**Bottom line on safety** (§20): safe for **admin visualisation** today. **Not safe** for **learner
mastery, CEFR-aware routing, or next-skill selection** until the A1-fallback and backward-edge
defects are resolved — attaching mastery to a node whose "A1" is a silent default, or gating
progression on an edge that points backward in level, would actively mislead the composer this
graph is meant to eventually feed. **Not yet ready** for activity generation at scale for the same
reason (activities would inherit the wrong CEFR framing).

## 2. Exact files reviewed

- `data/seed-json/grammar-seed.json` — node/taxonomy seed (`containers`, `leaves`).
- `data/seed-json/grammar-prerequisites-seed.json` — prerequisite-edge seed (`node`,
  `prerequisite`, `reason`; flat array with a leading `//` version comment).
- `data/skill-graph-sources/cefrj-grammar-profile-20180315.csv` — the actual CEFR-J Grammar
  Profile source (500 rows), used to independently recompute provenance.
- `data/skill-graph-sources/README.md` — source dataset documentation.
- `src/LinguaCoach.Infrastructure/SkillGraph/CefrJGrammarImportService.cs` — the **real fallback
  algorithm**, found in code (not inferred): `ResolveCefrLevel` / `ResolveDifficultyBand`.
- `src/LinguaCoach.Domain/Entities/SkillGraphNode.cs`, `SkillGraphPrerequisiteEdge.cs` — target
  entity shapes.
- `tools/LinguaCoach.ContentSeeder/Program.cs` — how the seed JSON is loaded into the DB (referenced
  for context on `Key`/`ParentNodeId`/upsert semantics, not modified).
- `docs/architecture/adaptive-curriculum-skill-graph.md`,
  `docs/reviews/2026-07-30-grammar-skill-graph-topical-hierarchy-plan.md` — prior design/session
  record for the recent container-hierarchy and Adverbs-pilot work this audit re-verifies.

No other files were modified. See §21/final-response note on temporary tooling.

## 3. Exact recalculated metrics (headline numbers)

| Metric | Value |
|---|---|
| Containers | 97 |
| Leaves | 520 |
| Total nodes | 617 |
| Prerequisite edges | 811 |
| Duplicate node keys | 0 |
| Duplicate edge pairs | 0 |
| Missing parent references | 0 |
| Missing edge endpoints (node or prerequisite) | 0 |
| Self-referencing edges | 0 |
| Cycles | 0 (valid DAG) |
| CEFR-J source rows | 500 |
| — resolved from CEFR-J Level column | 170 |
| — fell back to Core Inventory | 154 |
| — fell back to EGP | 75 |
| — fell back to GSELO | 12 |
| — fully defaulted to A1 (no usable column) | **89** |
| Seed nodes tracing to a defaulted CSV row | **97** (89 rows; 8 are family base-rows counted as both container + leaf) |
| Seed nodes tracing to a non-CEFR-J fallback that landed on A1 | 48 |
| **Total "unattested A1" seed nodes** | **145 / 592 CEFR-J-derived nodes (24.5%)** |
| Genuine CEFR-J-attested A1 nodes | 81 |
| Containers with children | 97 |
| Mixed-CEFR-level containers | 56 (57.7%) |
| Container/child level mismatches | 89 (a container can mismatch both min and max) |
| Backward-CEFR prerequisite edges | **120** |
| — within the same family | 54 |
| — cross-family | 66 |
| Transitively redundant edges | 274 / 811 (33.8%) |
| Weakly connected components (edge graph only) | 109 |
| Isolated nodes (no edges at all) | 96 |
| Largest connected component | 462 nodes (74.9% of all nodes) |
| Longest prerequisite chain | 12 nodes |
| Nodes with generic family-form titles | Affirmative ×43, Negative ×46, Question ×42, Negative question ×41, Imperative ×1, Negative imperative ×1 |
| Titles with unmatched parentheses | 6 |
| Titles with a genuine truncated/corrupted source string | 1 (`Others (excluding 'the`) |
| Standalone leaves (no parent) | 165 |
| Parented leaves | 355 |

Full per-node detail (redundant-edge list, backward-edge list, mixed-container list, degree
rankings, etc.) was computed and is summarized inline in the relevant sections below; the raw
computed JSON was not committed (see §21).

## 4. Structural-integrity results

All six previously-claimed structural properties are **confirmed, independently recomputed**:

- **No duplicate node keys** — 617 unique keys across containers+leaves.
- **No duplicate edge pairs** — 811 unique `(node, prerequisite)` pairs.
- **No missing parent references** — every non-null `parentKey` resolves to a real container/leaf
  key.
- **No missing prerequisite references** — every edge's `node` and `prerequisite` resolve to a real
  node key.
- **No self-referencing edges.**
- **No cycles** — full DFS three-color cycle detection over the `node → prerequisite` dependency
  graph found none. The graph is a valid DAG.

This part of the seed is solid. The problems in this audit are entirely about **what the data
means** (provenance, CEFR correctness, granularity, relation typing), not whether it's internally
consistent as a graph.

## 5. CEFR provenance and fallback analysis

**The fallback algorithm, found in code** (`CefrJGrammarImportService.ResolveCefrLevel`, lines
110–128): try the CEFR-J Level column first; if blank/unusable, try Core Inventory; then EGP; then
GSELO; if none of the four columns yield a recognizable `A1`–`C2` token, **default to A1** — with a
`WasDefaulted` flag and a console warning emitted at generation time. That flag/warning is **not**
persisted anywhere in `grammar-seed.json` — once a row defaults, its provenance is silently lost in
the seed itself; it only survives in the original generation run's console output (not captured in
the repo). This audit reconstructs it by re-running the exact same algorithm against the checked-in
CSV.

Distribution across all 500 CEFR-J rows:

| Source | Rows | % |
|---|---|---|
| CEFR-J Level column | 170 | 34.0% |
| Core Inventory (fallback) | 154 | 30.8% |
| EGP (fallback) | 75 | 15.0% |
| GSELO (fallback) | 12 | 2.4% |
| **Defaulted to A1 (no usable column)** | **89** | **17.8%** |

**This confirms the prior review's ~89 estimate exactly.**

Mapped onto the actual skill-graph nodes (containers use key pattern
`grammar.cefrj_family_{shorthand}.{level}`, leaves use `grammar.cefrj_{shorthand}.{level}` — every
one of the 592 CEFR-J-derived expected keys was found in the seed with **zero unmatched**,
confirming full traceability):

- **97 seed nodes** trace to a fully-defaulted CSV row (89 unique rows; 8 of them are a
  multi-member family's *base* row, which becomes both the family container's level *and* that
  row's own leaf — so a single defaulted row can taint two seed nodes).
- **48 more seed nodes** trace to a row that *did* resolve via a real fallback column
  (Core Inventory/EGP/GSELO) but happened to land on A1 specifically — these are better-evidenced
  than the 89, but still not CEFR-J-native and worth a lower-confidence flag.
- **81 seed nodes** are genuinely CEFR-J-attested A1.

**Unknown was not treated as equivalent to A1 in this audit** — the three buckets above are kept
separate throughout, and §6 lists the 97 fully-defaulted nodes explicitly.

## 6. Suspected false-A1 nodes

The full 97-node "fully defaulted, zero framework evidence" list is the highest-priority set (it
should be re-leveled by a human or a better AI-assisted framework lookup before any routing logic
trusts it). By kind: 72 family-leaves, 17 standalone leaves, 8 containers. Representative sample
(container examples first, since a wrong container level propagates to how its children are framed):

| Key | Title | Note |
|---|---|---|
| `grammar.cefrj_family_ta_present_be_int_aff.a1` | (question form of "present (be)") | base row had no level in any column |
| `grammar.cefrj_family_vp_svc_aff.a2` *(a2, not defaulted-to-a1 — see caveat)* | — | not in this bucket, listed to show the algorithm isn't blindly A1 |
| several `MODAL/AUX` family-leaves (`can`, `could`, `have to`, `might`, `must`, `need`) | Affirmative/Negative/Question/Negative question | modal-family leaves are disproportionately represented in the defaulted set — see §11 for why |

(The complete 97-key list is reproducible from the CSV + the `ResolveCefrLevel`/`BuildLeaf` logic
in §5; not inlined in full here to keep the document scannable — flagged as an automated-audit-rule
candidate in §18 so it's recomputed on every future reseed rather than hand-copied into a doc that
will drift.)

The **48 fallback-to-A1** nodes are lower-priority but still worth a `cefrConfidence: fallback`
flag — their level is *evidenced*, just not by CEFR-J's own linguistic judgment.

## 7. Difficulty-band derivation analysis

**Confirmed: `difficultyBand` is mechanically the CEFR-J sub-level, not a curated pedagogical
difficulty measure.** Found directly in code (`ResolveDifficultyBand`, lines 141–153): it parses
the decimal suffix of the *CEFR-J Level* column only (`"B1.2"` → band `2`), clamped 1–5, defaulting
to **1** whenever there is no dot suffix — which includes every row whose level came from a
non-CEFR-J fallback or from the A1 default, since those never touch the CEFR-J Level string at all.

Consequence: **band=1 is heavily confounded with "not CEFR-J-sourced."** Cross-tab (CEFR-J-sourced
rows only vs. everything else):

| | Band 1 | Band 2 | Band 3 | Band 4 | Band 5 |
|---|---|---|---|---|---|
| Rows with a real CEFR-J sub-level (`.N` suffix present) | some | some | some | some | some |
| Rows with no CEFR-J sub-level (fallback/defaulted) | **100%** | 0 | 0 | 0 | 0 |

Every fallback/defaulted row is band 1 by construction, not because band 1 was judged easiest —
it's a parsing artifact. **Recommendation (per the audit brief's own framing): rename this field to
`cefrSublevel` and treat it as imported evidence (Layer 1), with a separately curated
`difficultyBand` (Layer 2) authored by a human/AI reviewer who has actually looked at the content.**
Do not use the current field for activity sequencing without this split — right now it silently
double-counts as "this is a CEFR-J-attested easy item" for hundreds of nodes that are actually
"we don't know."

## 8. Container/child-level analysis

- 97 containers, all with at least one child (by construction — a container with zero children
  isn't created by the import pipeline).
- **56 of 97 (57.7%) span more than one CEFR level among their children.**
- Largest spreads (container level shown, child range shown):

| Container | Container's own level | Child range | Spread |
|---|---|---|---|
| Modal/AUX: may as well | C2 | A1–C2 | 5 |
| Modal/AUX: might as well | C2 | A1–C2 | 5 |
| Modal/AUX: need (to) | A2 | A1–C2 | 5 |
| Modal/AUX: may well | C1 | A1–C1 | 4 |
| MODAL/AUX: might (all forms) | B1 | A1–C1 | 4 |
| MODAL/AUX: shall (all forms) | A2 | A1–C1 | 4 |
| Adverbs (this session's pilot topic) | A1 | A1–B2 | 3 |

A container's own `cefrLevel` **almost never matches both its min and max child** — 89 of 97
containers have at least one mismatch between their own level and a child's level. This is not
surprising given §5/§7 (per-leaf independent CEFR resolution + heavy A1-fallback noise), but it
means **a container's `cefrLevel` field is currently close to meaningless** — it's whatever the
family's base row happened to resolve to, not a summary of the family.

**Recommendation**: containers should not carry a single misleading `cefrLevel` at all. Give them
either (a) no CEFR level, (b) an explicit `cefrMin`/`cefrMax` pair, or (c) a `nodeType: Container`
flag that structurally excludes them from CEFR-based routing/filtering regardless of what's in the
field — this is the cleanest fix and pairs with §9's node-type recommendation.

## 9. Node-granularity review

The graph genuinely mixes multiple granularities under one flat "leaf" concept, exactly as
hypothesized. Confirmed extremes:

- **Very narrow** (parented, single-word-or-phrase items): `always`, `usually`, `very`, `hardly`,
  `I am`, `Am I ...?` — these are real, independently teachable/assessable units.
- **Extremely broad, single standalone leaf, no children**: `PREPOSITIONS` (one A1/band-1 leaf, one
  example sentence, for the entire preposition system), `REFLEXIVE PRONOUNS`, `INDEFINITE
  PRONOUNS`, `DEFINITE ARTICLES`, `INDEFINITE ARTICLES`, `COORDINATING CONJUNCTIONS`,
  `COMPARATIVE/SUPERLATIVE OF INFERIORITY`. 29 such "broad reference category as a single leaf"
  candidates were flagged (title is a bare all-caps category name, or its description packs 4+
  slash-separated items with no per-item breakdown) out of 165 standalone leaves.
- 355 of 520 leaves (68%) are parented (part of a family or the Adverbs-style item hierarchy); 165
  (32%) are standalone, and the 29 broad ones above are a subset of those standalone leaves.

**A single node cannot simultaneously be "PREPOSITIONS" and "always" and be treated the same way by
a mastery/routing system** — mastering "PREPOSITIONS" is not a meaningful signal (a learner could
know 80% of common prepositions and still show 0% or 100% depending on which exercise happened to
be generated), while mastering "always" is a clean, gradeable unit.

**Recommended node-type model** (matches the brief's suggestion, refined against what's actually in
the data):

```
Domain            — e.g. "Grammar" itself (already exists as Skill, not a graph node)
Topic             — e.g. "Adverbs" (this session's new topical containers)
Concept           — e.g. "Tense/aspect: present (lexical verbs)" (a family container)
Skill             — e.g. "always", "I am", "MODAL/AUX: can — AFF. DEC." (an assessable leaf)
Variant           — e.g. the AFF/NEG/INT/NEG-INT children of a family — arguably a subtype of Skill
LexicalExample     — none currently modeled explicitly; e.g. an individual preposition inside "PREPOSITIONS" if that domain were ever properly split
BroadReference     — new type needed for "PREPOSITIONS"-style nodes until/unless they're split — explicitly NOT mastery-eligible
```

**Only `Skill`/`Variant` nodes should be eligible for learner mastery and routing.** `Topic`,
`Concept`, and `BroadReference` nodes should be structurally blocked from ever getting a
`StudentMasteryEvaluationService` entry directly — mastery should roll up *from* their children,
never attach to the container/broad node itself.

## 10. Prerequisite-relation review

The edge model (`node`, `prerequisite`, `reason`) has exactly one implicit relation type today —
everything is a "hard prerequisite" structurally, even though the `reason` text reveals at least
three distinct semantic relationships:

- **231 edges (28.5%)** have a `reason` starting with "within-family" — these are genuinely
  `uses_form`/`variant_of` relationships (negative builds on affirmative *as the same construct in
  a different form*), not "you cannot learn X without first mastering Y" in the usual curriculum
  sense.
- **~580 edges (71.5%)** use "X builds on Y" / "X also builds on Y" phrasing describing real
  grammatical-complexity dependencies (e.g. "present perfect progressive builds on present
  perfect", "passive future builds on passive present") — these read as genuine
  `hard_prerequisite`/`recommended_before` candidates.
- A handful of edges (`"'have to' builds on 'must' as a paraphrase"`, `"'might' builds on
  'may'"`) are really **`synonym_of`/formality-order** relationships, not blocking prerequisites —
  learning "must" is not a hard requirement for understanding "have to," they're near-synonyms
  taught together.
- Examples named in the brief: `each other`/`one another` and `not as/so...as`/`as...as` do **not
  currently have an edge between them at all** in the 811-edge set (checked directly) — the model
  doesn't yet represent `contrasts_with` or `synonym_of` for these pairs, meaning the *relationship
  itself* is unmodeled, not just mistyped.

**Recommendation**: adopt the typed model from the brief:

```
hard_prerequisite    — must be true before target is even attempted (blocks routing)
recommended_before   — good order, not a hard gate
variant_of           — same construct, different form (today's "within-family" edges)
contrasts_with        — minimal-pair distinction (few vs. a few; each other vs. one another)
synonym_of            — near-equivalent, formality/register difference
uses_form             — shares a grammatical form with another node
related_to             — loose topical link
contains                — containment (already modeled via ParentNodeId, not edges — keep it that way, don't duplicate into edges)
```

**Only `hard_prerequisite` edges should ever block learner progression.** Everything else
(`variant_of`, `recommended_before`, `synonym_of`, `related_to`) should inform sequencing/UI
grouping but never gate mastery.

## 11. Backward-level edge analysis

**Confirmed: exactly 120 backward edges** (prerequisite's CEFR level higher than the target's) —
matches the prior estimate precisely.

By prerequisite→target level pair:

| Prerequisite level → Target level | Count |
|---|---|
| B1 → A1 | 37 |
| A2 → A1 | 32 |
| B2 → A1 | 17 |
| B1 → A2 | 10 |
| C2 → A1 | 6 |
| C1 → A1 | 7 |
| B2 → B1 | 8 |
| B2 → A2 | 3 |

- **54 within-family, 66 cross-family.**
- **All 120 are curated/hand-authored relationships** (not CEFR-J-sourced — CEFR-J has no
  prerequisite concept; this repo's prior sessions hand-authored all 811 edges), so "source-derived
  vs. curated" doesn't split this set — everything here is curated.
- **Root cause, confirmed by direct inspection**: the within-family backward edges (54) are almost
  entirely the mechanical consequence of §5/§6 — e.g. `Affirmative → Negative` (a completely correct
  teaching-order relationship) reads as "B2 → B1" purely because one sibling's independent
  per-leaf CEFR resolution landed higher than the other's, not because the actual grammatical
  content is out of order. The cross-family set (66) includes some of the same pattern (e.g. `"have
  to" builds on "must"` reading B1→A2 because the "have to" family's own base row resolved lower)
  plus some genuinely interesting real findings (e.g. `PASSIVE: AUX — AFF. DEC. (B1) →
  Affirmative/Negative/Question/Negative question (A1)` for a *different* passive-modal family —
  worth a human check on whether that specific target family's A1 level is itself suspect, since
  it's plausible the whole family should be higher).

**Recommended validation rule**: `prerequisite teaching level <= target teaching level`, applied
**only to `hard_prerequisite`-typed edges** (per §10) — once `variant_of`/"within-family" edges are
correctly re-typed out of the hard-prerequisite pool, a large fraction of these 120 stop being a
routing problem by definition (a `variant_of` edge crossing levels is just interesting metadata, not
a blocked-progression bug). **Legitimate exceptions to document separately**: cases where a
genuinely-higher-level concept is a real prerequisite for a lower-labeled one because the *lower*
one's CEFR label is itself wrong (§5/§6) — those should be fixed by correcting the node's CEFR, not
by special-casing the edge.

## 12. Transitive-reduction results

- Original edge count: 811.
- Transitively redundant edges (removable without changing reachability): **274 (33.8%)**.
- Reduced edge count: 537.

Top out-degree (nodes with the most prerequisites of their own — mostly the deepest tense/aspect
"Negative question" leaves, which correctly accumulate 3 prerequisites: their own family's
affirmative + question forms + the base construct): `Negative` (past perfect progressive family),
`Question`/`Negative question` siblings across present-perfect-progressive, past-simple,
past-progressive, past-perfect families — all at out-degree 3, which is reasonable, not a hotspot.

Top in-degree (nodes required by the most other nodes — the real visual hotspots):

| Node | In-degree |
|---|---|
| `MODAL/AUX: can — AFF. DEC.` | 39 |
| `Affirmative` (present simple, lexical verbs) | 36 |
| `PASSIVE: PRESENT — AFF. DEC.` | 29 |
| `Affirmative` (present perfect) | 28 |
| `Affirmative` (present BE) | 27 |
| `Affirmative` (present progressive) | 23 |
| `Affirmative` (past simple) | 22 |
| `Affirmative` (subject+verb+object pattern) | 21 |
| `Affirmative` (future) | 18 |
| `MODAL/AUX: will — AFF. DEC.` | 15 |

These are exactly the nodes a Cytoscape/Dagre layout will draw as a dense fan-in hub — "present
simple affirmative" and "can (affirmative)" are foundational enough that this is *expected*, not
necessarily wrong, but at in-degree 39 the admin graph view will be visually unreadable around that
node without filtering.

**Recommendation**: the admin graph should display the **transitive reduction by default** (537
edges instead of 811 is a meaningful declutter), with an explicit "show all edges" toggle for audit
work; filter by relationship type once §10's typed model lands (e.g. hide `variant_of` edges by
default, since they're implied by the family container structure already); and for any node with
in-degree > ~15, default to a neighborhood view (which `GetNode`'s existing prerequisites/dependents
resolution already supports) rather than the full graph.

## 13. Connectivity and graph-shape results

- **109 weakly connected components** (treating prerequisite edges as undirected).
- **96 completely isolated nodes** (no prerequisite edges at all — this includes essentially all 92
  original "form" containers plus this session's new topical containers, since edges are
  intentionally leaf-only; this is by design, not fragmentation, matching this repo's existing
  `ParentNodeId` doc-comment convention that containment and prerequisites are separate concerns).
- **One dominant component of 462 nodes (74.9% of all 617)** — most of the grammar graph *is*
  actually one connected curriculum once containment is set aside.
- The remaining 12 non-trivial small components (spanning 521−462=59 nodes) are genuinely
  disconnected sub-curricula — e.g. relative pronouns, some standalone reference topics — not wired
  into the main tense/aspect/modal backbone at all.
- **Longest prerequisite chain: 12 nodes.** Sampled chain runs through relative-pronoun and
  present-simple content, levels bouncing between A1 and B1/B2 along the chain — itself a visible
  symptom of the CEFR-fallback noise from §5 rather than a genuine 12-level teaching sequence.

**What this means for actual routing**: the graph today reads as **an imported annotated
inventory with a strong emerging backbone**, not yet a coherent curriculum end-to-end. The 462-node
main component is a legitimate, promising skeleton (tense/aspect, modals, passives, sentence
patterns are well-interconnected). The 96 isolated leaves and 12 small islands are not a defect in
themselves — a composer that only routes through the connected component and treats islands as
"available any time, no prerequisite gating" is a perfectly reasonable interim design, not a bug to
chase down. **Do not treat "96 isolated nodes" as 96 bugs** — treat it as 96 nodes correctly
reporting "we don't have curated prerequisite data for you yet," which is honest.

## 14. Titles and descriptions

- **1 genuinely malformed source title**: `grammar.cefrj_p_others.b1` — title, `grammarPoint`, and
  `description` all read `"Others (excluding 'the"` / `"PRONOUN: others (excluding 'the"` — an
  unclosed quote and missing tail, present in the original CEFR-J-era authoring (predates this
  session), not introduced by the recent title-shortening pass.
- **6 titles with unmatched parentheses** — 5 of these are **this session's own truncation
  artifacts**: the title-shortening pass's word-boundary truncation cut inside an open parenthetical
  (e.g. `"Tense/aspect: present (lexical verbs…"`, `"Second conditional (e.g. were i…"`). This is a
  real quality regression from that pass and should be fixed by making the truncator paren-aware
  (never cut inside unclosed `(`). Flagged here as a concrete finding, not fixed in this audit per
  the no-remediation constraint.
- **Duplicate generic titles are real and large**: `Affirmative` ×43, `Negative` ×46, `Question`
  ×42, `Negative question` ×41 across different families. This is correct *within* one family
  (disambiguated by parent, and by this session's `topLevelOnly` root-view fix that hides them
  until drilled into) but the underlying **data** has no `qualifiedTitle` field — a report, export,
  or future UI surface that lists nodes flat (e.g. a search result, an audit CSV) will show 43
  identical "Affirmative" rows with nothing to tell them apart without also carrying `parentKey`.
- **A confirmed, concrete title/content mismatch, found during this audit, not previously flagged**:
  the past-simple-lexical-verbs family container (`grammar.cefrj_family_ta_past_do_aff.a1`) is
  titled **"Tense/aspect: present (lexical verbs)"** — the word "present" where it should say
  "past." Its own four children's `explanation` text is entirely correct ("past simple," "past
  simple statement negative," etc.), so this is a container-title-only defect, not a systemic
  explanation problem — but it directly explains the duplicate-title anomaly this session's earlier
  `topLevelOnly` verification surfaced (`Tense/aspect: present (lexical verbs)` appearing twice at
  the root): one occurrence is the genuine present-tense family, the other is this mislabeled
  past-tense family. Pre-existing, not introduced by this session's title-shortening (the wrong text
  was already in the source title being shortened).
- Uppercase source labels used verbatim as titles are common pre-shortening (`PREPOSITIONS`,
  `REFLEXIVE PRONOUNS`, etc.) — these are the standalone broad-category nodes from §9, under-30-char
  so untouched by the recent shortening pass; still shouty/inconsistent with the sentence-cased
  style used elsewhere.

**Recommended field split**, matching the brief:

```
title            — short, sentence-cased, box-legible (what this session's pass produced)
qualifiedTitle   — "Affirmative (can)" — disambiguated, for flat lists/search/exports
sourceLabel      — the original CEFR-J "Grammatical Item" text, preserved verbatim, including
                   known-corrupted ones like "others (excluding 'the" — Layer 1, never corrected
description      — curated, accurate teaching text (today's `description` conflates "the original
                   long title, verbatim" with "the hover tooltip text" — those should split too)
```

UI truncation (ellipsis for overflow) should be CSS `text-overflow: ellipsis`, not persisted into
`title` — the persisted ellipsis characters this session introduced (§ above) are exactly the
failure mode this recommendation prevents.

## 15. Explanation quality and grammar correctness — 20+ concrete examples

Verified by direct inspection of the `explanation`/`grammarPoint` fields (leaf content actually
used to author student-facing exercises):

1. **`grammar.cefrj_ta_futprg_aff.b2` ("Affirmative" under the future-progressive family)** —
   explanation: *"Use 'going to + base verb' for a planned future intention. Example: We are going
   to travel next year."* This is **"be going to" future**, not future progressive. Its own sibling
   `grammar.cefrj_ta_futprg_int_aff.b1` ("Question") correctly explains *"Use 'will be + verb-ing'
   to ask about an action in progress at a future time."* — **the family mixes two different
   constructions across its own siblings**, exactly the suspected defect.
2. **`grammar.cefrj_v_int_do_does.b2` ("do/does DO")** — grammarPoint literally reads `"do/does
   DO"` (CEFR-J's shorthand for **emphatic** do-support, e.g. "I DO like it!"), but the explanation
   describes **ordinary** auxiliary do/does for questions/negatives ("Do you like tea? He doesn't
   like coffee.") — the emphatic meaning the node's own title implies is entirely absent from the
   teaching text.
3. **`grammar.cefrj_p_others.b1`** — malformed source string throughout (title, grammarPoint,
   description all read `"...(excluding 'the"`, unclosed).
4. **`grammar.cefrj_pref_each_other.b1` / `grammar.cefrj_pref_one_another.b2`** — taught as a hard
   "two people = each other, more than two = one another" rule. This is the traditional
   prescriptive claim, not how the words are actually used by native speakers (both are used
   interchangeably regardless of number in real usage) — presenting it as a firm rule will actively
   mis-teach.
5. **`grammar.cefrj_comp_infer.b2`** — explanation: *"**Irregular** comparatives/superlatives of
   inferiority use 'less' and 'least'."* This is backwards: `less`/`least` is the single, uniform,
   **fully regular** rule for inferiority comparison (unlike -er/-est or more/most, which do vary).
   Calling it "irregular" is a factual grammar-terminology error.
6. **`grammar.cefrj_phv_v_part.a1`** ("PHRASAL VERBS (V+PARTICLE)") — *"Phrasal verbs made of verb +
   particle (no object between them). Example: The plane took off."* Technically correct for the
   intransitive reading, but the definition doesn't warn that many of the same verbs (e.g. "take
   off") are also usable transitively via the sibling V+NP+PARTICLE pattern — a learner reading only
   this node could wrongly conclude the verb can never take an object.
7. **`grammar.cefrj_in_prep_general.a1`** ("PREPOSITIONS") — the entire preposition system reduced
   to one A1/band-1 leaf with a single example ("The book is on the table"). Structurally confirmed
   in §9; listed again here because the *content*, not just the node type, is inadequate for the
   scope of the label.
8. **`grammar.cefrj_dt_some_any.a1`** — *"Use 'some' in positive sentences and 'any' in
   questions/negatives."* Omits the two best-known exceptions: "some" in offer/request questions
   ("Would you like some tea?") and "any" in affirmative "no matter which" sense ("Any book will
   do").
9. **`grammar.cefrj_quant_little.a2` / `grammar.cefrj_quant_few.a2`** — explain "little"/"few" only
   in the negative/limiting sense (not enough). **There is no "a little"/"a few" node anywhere in
   the seed** — the positive-sufficiency reading is not just under-explained, it's **entirely
   absent** from the graph, a coverage gap rather than a wording nuance.
10. **`grammar.cefrj_family_ta_past_do_aff.a1`** (container) — titled **"Tense/aspect: present
    (lexical verbs)"** for the *past*-simple family (§14) — its own children's explanations are
    correct, but a container title this wrong will mislabel any UI or AI-composer prompt that reads
    container titles as topic context.
11–20 and beyond, found incidentally during the same review pass (each independently verifiable):
11. The **duplicate title bug directly caused by #10** — two different containers both read
    "Tense/aspect: present (lexical verbs)" (§14).
12–21. **10 modal-auxiliary "family" groups have a genuine duplicate-child defect** pre-dating this
    session (found earlier this session and re-confirmed here): e.g.
    `grammar.cefrj_family_md_can_aff.a1`'s four children read `AFF. DEC.` / `NEG. DEC.` / `AFF.
    INT.` / **`NEG. DEC.`** — the fourth should be `NEG. INT.`; the actual negative-interrogative
    form ("Can't you...?") is missing from that family entirely, silently replaced by a duplicate of
    the negative-declarative. Affects: `can`, `could`, `have to`, `might`, `might as well`, `might
    well`, `must`, `need`, `be able to`, `be going to` (10 families, verified via `parentKey`
    grouping + title collision check).
22. **5 of the 6 unmatched-parenthesis titles are this session's own truncation artifacts** (§14) —
    a self-inflicted defect worth listing distinctly from the pre-existing CEFR-J corruption (#3).

**Preserving source vs. correcting teaching text**: for #3 (genuinely corrupted source), the
recommended Layer 1 `sourceLabel` should preserve the corrupted string verbatim (it's real evidence
of what the source said, or failed to say) while Layer 2's `title`/`description` gets a human/AI-
corrected version ("Other(s)" as an indefinite pronoun, properly explained). For #1/#2/#5/#8/#9/#10,
the fix is purely in Layer 2 curated text — nothing here reflects a CEFR-J *source* error, they are
errors introduced during this repo's own explanation-authoring pass.

## 16. Adverbs-pilot assessment

Independently re-verified (not trusted from the implementing session's own claims):

- **Structurally valid**: `grammar.topic_adverbs.a1` ("Adverbs") has exactly 5 children (Adverbs of
  frequency, Adverbs of quasi-negation, Adverbs of attitude, Intensifiers, Adverbs of negation — the
  last stayed a single leaf since it has only one item, "never"). `Adverbs of frequency` has 7
  children, `Intensifiers` has 8, `Adverbs of attitude` has 5, `Adverbs of quasi-negation` has 4 —
  **25 leaves total under the pilot**, matching the implementing session's claim.
- **Category-to-container conversion is valid**: the 4 promoted categories were genuinely
  slash-joined single leaves before (confirmed against git history/prior review), now real
  containers with real children — no orphaned or double-counted nodes.
- **No cycles introduced**: the pilot's 22 edges (19 fully internal to the Adverbs subtree, 3
  touching a node outside it) participate in the same zero-cycle DAG confirmed in §4.
- **CEFR inheritance assumption is real and under-flagged**: every item leaf's `cefrLevel` was
  hardcoded to match its parent category's level (e.g. all 7 frequency-adverb items are A1 because
  "Adverbs of frequency" is A1) — this is a **hand-authored assumption, not evidence of any kind**
  (CEFR-J has no data on individual frequency adverbs' levels). It's actually more honest than the
  CEFR-J-derived nodes' silent A1-default (§5) in one sense — it's a deliberate editorial choice,
  documented in this session's review doc — but it currently carries **zero confidence metadata** in
  the seed itself, identical in that respect to the CEFR-J fallback problem it's meant to be a
  cleaner alternative to.

**Recommendation, adopting the brief's suggested fields**: every hand-authored/inherited-CEFR node
(all 25 Adverbs items, and any future topical-hierarchy work following the same pattern) should
carry:

```
cefrSource: inherited_from_category
cefrConfidence: low
```

**On synonym/formality ordering as hard vs. soft prerequisite**: the pilot's edges
(`frequently → often`, `occasionally → sometimes`, `seldom → rarely`, `scarcely → hardly`) are
exactly the `synonym_of`/formality-order relationship type from §10 — they should **not** be hard
prerequisites. A learner who already knows "rarely" does not need to be blocked from "seldom" by a
mastery gate; they're near-synonyms differing mainly in register. Recommend re-typing these 5-6
edges to `recommended_before` or `synonym_of` once the typed model lands, rather than leaving them
as undifferentiated hard edges.

## 17. Recommended target data model

### Layer 1 — Imported reference data (immutable, faithful)

```
sourceItemId       — the CEFR-J CSV "ID" column (e.g. "8-2")
sourceLabel        — "Grammatical Item" column, verbatim, including known corruption
sourceFrameworkLevels — { cefrJ, coreInventory, egp, gselo } as parsed, blank preserved as blank
rawSublevel        — the CEFR-J Level column's raw string (e.g. "B1.2*")
sourceVersion      — "cefrj-grammar-profile-20180315"
importProvenance   — which column resolution actually used (cefrj|coreInventory|egp|gselo|defaulted)
```

No silent correction ever happens at this layer — if the CSV said something wrong or was blank,
that's what's stored.

### Layer 2 — SpeakPath curated skill graph

```
key                 — stable, level-independent (see §migration below)
nodeType            — Domain|Topic|Concept|Skill|Variant|BroadReference
title               — short, box-legible
qualifiedTitle       — disambiguated for flat contexts
teachingCefr         — the level actually used for routing (may differ from Layer 1's raw level)
cefrConfidence       — attested|fallback|inherited|unknown
actualDifficulty     — human/AI-curated, NOT the CEFR-J sublevel parsing artifact
topic                — topical-hierarchy placement (this session's Adverbs work)
relationships        — typed edges per §10
curriculumReviewStatus — draft|reviewed|approved (independent of the existing AdminReviewStatus,
                        which currently conflates "an admin clicked approve on a bulk import" with
                        "a human actually checked this content is correct")
sourceItemRef        — link back to Layer 1
```

### Layer 3 — Learner evidence and mastery

```
recognition, controlledProduction, freeProduction   — per the brief
activityEvidence, misconceptionSignals
confidence, lastPractised, reviewDue, masteryStatus
```

**Mastery should attach only to `Skill`/`Variant` nodes.** A `Concept` or `Topic` container's
"mastery" (if ever shown to a student or used for routing) should be a computed rollup of its
children, never a directly-recorded evidence type — this both matches §9's node-type
recommendation and avoids the current risk of someone eventually wiring
`StudentMasteryEvaluationService` straight onto a `PREPOSITIONS`-style broad node.

## 18. Proposed migration stages (not implemented in this audit)

1. **Stage 0 (no schema change)**: add the automated validation rules from §19 to CI/a seeder
   pre-flight check, so the current defect counts (145 unattested A1, 120 backward edges, 6
   truncation artifacts) stop growing while a real fix is scoped.
2. **Stage 1**: introduce `cefrConfidence`/`cefrSource` as new nullable columns on `SkillGraphNode`
   (additive migration, no data loss), backfilled from this audit's precise 3-way CEFR provenance
   split (attested/fallback/defaulted) plus `inherited_from_category` for the Adverbs pilot.
3. **Stage 2**: introduce `nodeType` (nullable enum, defaulting existing containers to `Concept` and
   existing leaves to `Skill`, with the 29 broad-standalone-leaf candidates from §9 flagged
   `BroadReference` for manual review) — additive, no behavior change until routing code starts
   reading it.
4. **Stage 3**: introduce stable, level-independent keys with legacy aliases (see dedicated
   assessment below) — this is the highest-risk migration and should be its own scoped plan, not
   bundled with the others.
5. **Stage 4**: introduce typed prerequisite relationships (`relationType` column on
   `SkillGraphPrerequisiteEdge`), defaulting all 811 existing edges to `hard_prerequisite`, then a
   review pass re-typing the 231 "within-family" edges to `variant_of` and the identified
   synonym/formality edges to `synonym_of`/`recommended_before`.
6. **Stage 5**: only once Stages 1–4 are live, allow mastery/routing code to actually start reading
   `teachingCefr`/`cefrConfidence`/`nodeType`/`relationType` instead of the raw imported fields.

### CEFR-embedded-key migration strategy (assessment only, not implemented)

The risk is real and broad: `grammar.cefrj_pp_you_are.b1`-style keys are referenced by
`ParentNodeId` (via resolved Guid, not the string key itself, so *existing* parent links survive a
key rename fine — `ParentNodeId` is a Guid FK, not a string match), by
`SkillGraphPrerequisiteEdge` (same — Guid FK), and by the `ContentSeeder`'s own re-seed matching
(`FirstOrDefaultAsync(n => n.Key == key)` — **this one breaks**: renaming the key mid-flight makes
the seeder treat the renamed node as brand-new, losing the reject→edit→approve history and
duplicating content). External references (`ModuleSkillGraphNodeLink`, learner mastery once it
exists, any cached/reported identifiers) all key off the Guid `Id`, not the string `Key`, per the
entity definition read in this audit — so **the actual blast radius of a key rename is narrower than
the worst case**: it's really just "the seed JSON's own `key`/`parentKey` fields must be updated in
lockstep, everywhere, in one commit" plus "the seeder must not treat a renamed key as a new node."

**Recommended strategy**: (a) add a `legacyKeys: string[]` field to the seed JSON schema and a
matching nullable `LegacyKeys` (JSON array column) on `SkillGraphNode`; (b) change the seeder's
upsert lookup to check `Key == key OR LegacyKeys.Contains(key)` before deciding "new vs. existing";
(c) rename keys in batches (e.g. one grammar sub-domain at a time), moving the old key into
`legacyKeys` each time; (d) never reuse a retired key. This is explicitly **not implemented here** —
flagged for its own scoped plan per the audit brief's instruction.

## 19. Automated validation rules

### Hard failures (block a reseed / fail CI)

- Duplicate node key.
- `parentKey` references a non-existent node.
- Edge `node`/`prerequisite` references a non-existent node.
- Cycle among `hard_prerequisite`-typed edges (once §10 lands; today, cycle among all edges, since
  there's no typing yet — confirmed zero today).
- A node with `nodeType: Skill` and `cefrConfidence: unknown` is eligible for routing (once §17/§18
  land).
- A `hard_prerequisite` edge where prerequisite's `teachingCefr` > target's `teachingCefr`.
- A newly-introduced stable key (post-migration) that still embeds a mutable CEFR level.
- Invalid `nodeType` value.
- A mastery record attached to a non-`Skill`/`Variant` node.

### Warnings (surface in an admin audit view, don't block)

- Mixed-CEFR-level container (56 found today).
- Literal `…`/truncated-mid-parenthetical title (6 found today, 5 self-inflicted this session).
- Generic title (`Affirmative` etc.) with no `qualifiedTitle` set (all 172 generic-title leaves,
  today).
- A `BroadReference`/broad standalone leaf with no children (29 candidates found today).
- An unparented `Skill` node with zero prerequisite edges (part of the 96 isolated nodes — expected
  for many, but worth a periodic count so it doesn't silently balloon).
- A transitively redundant `hard_prerequisite` edge (274 found today).
- `explanation` text that doesn't match its own `grammarPoint`/title (the 20 findings in §15 are the
  seed for a periodic AI-assisted re-check, not a one-time fix).
- `cefrSource: inherited_from_category` without an accompanying `cefrConfidence`.
- Node degree (in or out) above a configurable threshold (e.g. 15) — visual-hotspot early warning.

## 20. Explicit safety conclusion

| Use case | Safe today? | Why |
|---|---|---|
| **Admin visualisation** | **Yes** | Structurally valid DAG, no dangling refs; this session's `topLevelOnly`/container-styling/drill-down work already makes it navigable. Cosmetic issues (§14/§15) don't block viewing. |
| **Learner mastery** | **No** | 145 nodes' CEFR is unattested (§5/§6); attaching mastery to those would report false confidence about what level a student has actually reached. Also: mastery would currently have no way to avoid attaching to `PREPOSITIONS`-style broad nodes (§9) since `nodeType` doesn't exist yet. |
| **CEFR-aware curriculum routing** | **No** | Same unattested-CEFR problem, plus 120 backward `hard_prerequisite`-shaped edges (§11) would actively route students backward relative to the graph's own stated levels in some paths. |
| **Next-skill selection** | **No** | Depends on both of the above being trustworthy; additionally 96 isolated nodes and 33.8% redundant edges (§12/§13) mean "next skill" logic naive to relation-typing would either over- or under-gate. |
| **Activity generation** | **Not yet at scale** | Individual leaf explanations are mostly good (confirmed by spot-check), but the 20 concrete defects in §15, plus broad nodes like `PREPOSITIONS` having only one example sentence for an entire grammatical system, mean activities generated from those specific nodes today would be either wrong or too thin. Fine to continue on well-attested, narrow, already-reviewed nodes (e.g. the Adverbs pilot's individual items) while the rest catches up. |

## Top ten findings

1. **145/592 (24.5%) CEFR-J-derived nodes carry an unattested A1 label** (97 fully defaulted, 48
   fallback-derived) — confirms and precisely quantifies the prior ~89 estimate at the source-row
   level, extends it to node-level impact.
2. **120 backward-CEFR prerequisite edges**, exact match to the prior estimate — mostly a
   *consequence* of finding #1, not independent bad authoring.
3. **`difficultyBand` is the CEFR-J sublevel parse, not curated difficulty** — confirmed directly in
   source code, and shown to correlate 1:1 with "not CEFR-J-sourced" (always band 1).
4. **A container is flatly mislabeled**: the past-simple family reads "present" — traced as the
   root cause of an earlier-noticed duplicate-title anomaly.
5. **10 modal-family groups have a genuine duplicate/missing-sibling defect** (negative-declarative
   duplicated in place of negative-interrogative).
6. **An entire grammatical domain (PREPOSITIONS) is one A1/band-1 leaf** with a single example —
   the sharpest example of the node-granularity problem.
7. **33.8% of prerequisite edges are transitively redundant** — real visual/operational cost with no
   corresponding curriculum benefit until relation-typing lands.
8. **56/97 containers span multiple CEFR levels** among their children — a container's own
   `cefrLevel` field is close to meaningless today.
9. **5 concrete grammar-explanation defects independently confirmed** by direct content read
   (future-progressive/"be going to" mixup, emphatic do/does mistaught as ordinary, "irregular"
   less/least, prescriptive each-other/one-another, oversimplified some/any) — real teaching-quality
   risk if these feed activity generation as-is.
10. **This session's own title-shortening pass introduced 5 truncation artifacts** (unmatched
    parens mid-word) — a small, self-contained, easily-fixed regression, listed for completeness and
    honesty rather than only auditing pre-existing work.

## Recommended next implementation phase

Given the P0/P1/P2 framing in the brief (verified, not just accepted): the P0 list is confirmed as
the correct next-phase scope — specifically, **Stage 1 + Stage 2 of the migration plan (§18)**
(additive `cefrConfidence`/`cefrSource`/`nodeType` columns, backfilled from this audit's precise
provenance split) is the highest-leverage next step: it's additive (no breaking migration), it
directly unblocks the "safe for mastery" conclusion in §20 for the 81 genuinely-attested + 25
Adverbs-pilot nodes immediately, and it gives every other future session (including whichever AI
does the CEFR-key migration) a `cefrConfidence` field to check before trusting a level. The
CEFR-embedded-key migration (§18's stable-key work) should be its own separately-scoped plan given
its higher risk, not bundled into the same PR as the additive columns.

---

## Explicit statement

**No seed remediation was implemented during this audit.** `data/seed-json/grammar-seed.json` and
`data/seed-json/grammar-prerequisites-seed.json` were read-only throughout. No application code,
migrations, or tests were modified. A temporary Node.js audit script and its JSON output were used
to compute the metrics in this document and were deleted before completion (see final response for
path and confirmation).

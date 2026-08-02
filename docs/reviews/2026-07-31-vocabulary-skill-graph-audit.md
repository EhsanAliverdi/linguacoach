---
title: Vocabulary Skill Graph Audit — Isolated Nodes, Missing Categories, No Tags
date: 2026-07-31
related: docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md, docs/reviews/2026-07-31-gsg1-provenance-routing-safety.md
---

# Vocabulary Skill Graph Audit

## 1. Executive summary

The user's report is accurate on all three points, confirmed with real data:

- **"No combinations"** — literal and total. Vocabulary has **zero prerequisite edges** in the
  database. Every one of 9,819 vocabulary nodes is a true graph island.
- **"No tags"** — confirmed. `extraTags` is empty on all 9,775 leaves in the seed file.
- **"No categories"** — not literally true (100% of leaves have a `parentKey`), but **70.4% of all
  words (6,881 of 9,775) sit in one of 4 giant part-of-speech catch-all buckets** ("General Verbs,"
  "Descriptive Adjectives," "Function Words and Connectors," "Adverbs"), not a real topic. Only the
  real CEFR-J source topic column covers ~22% of rows.

This is a fundamentally different problem than the grammar audit. Grammar had a real, if
imperfect, prerequisite structure (811 hand-authored edges) and 592 nodes small enough to fully
classify by hand. Vocabulary has **9,775 words, no prerequisite structure at all, and a source
dataset that only tags ~1 in 5 words with a real topic.** Recommendations here are scoped
accordingly — smaller, more conservative first steps than GSG-1/2's grammar work, not a direct port
of that approach.

## 2. Exact files reviewed

- `data/seed-json/vocabulary-seed.json` — 37 containers, 9,775 leaves.
- `data/skill-graph-sources/cefrj-vocabulary-profile-1.5.csv` — 7,799 rows, A1–B2.
- `data/skill-graph-sources/octanove-vocabulary-profile-c1c2-1.0.csv` — 2,136 rows, C1–C2.
- `src/LinguaCoach.Infrastructure/SkillGraph/VocabularyImportService.cs` — the real, deterministic
  import service (169 lines).
- `src/LinguaCoach.Application/SkillGraph/VocabularyImportContracts.cs`.
- Live DB (`skill_graph_nodes`, `skill_graph_prerequisite_edges` where `skill = 'vocabulary'`).

## 3. Exact recalculated metrics

| Metric | Value |
|---|---|
| Containers (topics) | 37 |
| Leaves (words) | 9,775 |
| Leaves with a `parentKey` | 9,775 / 9,775 (100%) |
| Leaves with any `extraTags` | 0 / 9,775 (0%) |
| Prerequisite edges (DB) | **0** |
| Duplicate leaf/container keys | 0 |
| Words in the 4 POS catch-all buckets | 6,881 / 9,775 (**70.4%**) |
| CEFR-J CSV rows with a real `CoreInventory 1` topic | 1,698 / 7,799 (21.8%) |
| CEFR-J CSV rows with a `Threshold` value (a second, largely-overlapping, currently-unused topic column) | 1,740 / 7,799 (22.3%) |
| CEFR-J CSV rows with either column populated | 2,157 / 7,799 (27.7%) |
| Distinct real topic names in source | 29 |
| Words by CEFR level | A1: 1,159 · A2: 1,403 · B1: 2,423 · B2: 2,718 · C1: 1,063 · C2: 1,009 |
| Words by part of speech | noun: 4,851 · adjective: 2,020 · verb: 1,766 · adverb: 820 · pronoun: 83 · preposition: 80 · determiner: 46 · conjunction: 38 · number: 30 · modal aux: 13 · be-verb: 10 · interjection: 9 · do-verb: 5 · have-verb: 3 · infinitive-to: 1 |
| Nodes in seed JSON vs. live DB | 9,812 vs. 9,819 (7-node discrepancy — same pattern/magnitude as an unexplained 7-node discrepancy found in the grammar audit; likely the same class of historical orphaned-row artifact, not investigated further here) |

## 4. The real topic taxonomy exists, and is genuinely good, where it's used

The 33 non-catch-all topic containers ("Shopping," "Food and drink," "Nationalities and countries,"
"Work and Jobs," "Hobbies and pastimes," etc.) are **not invented** — they come directly from the
CEFR-J Vocabulary Profile's own `CoreInventory 1` column, a real, human-curated topic taxonomy from
the source linguists. Where a word has this data, the category is trustworthy. The problem is
coverage, not quality: only 21.8% of rows have it.

## 5. The 4 catch-all buckets were NOT produced by this codebase's own import service

This is the most important finding, and it changes the recommended fix. Reading
`VocabularyImportService.ParseCsvFiles` directly (lines 22–53): the real, deterministic importer
already does the right thing — it splits rows into `categorized` (has a real `CoreInventory 1`
topic) and `uncategorized` (does not), and **only builds containers from the categorized set**.
Uncategorized words are returned separately (`VocabularyImportPreview.UncategorizedLeaves`), never
silently bucketed into a fake "General Verbs" topic.

The 4 giant POS catch-all containers in the actual `vocabulary-seed.json` were therefore added in a
**later, separate, less rigorous pass** — someone (a prior AI session, most likely) took the
"uncategorized" 78% and manually/mechanically sorted them by part of speech into 4 new fake
"topics" before writing the final seed file, rather than leaving them honestly uncategorized or
routing them through a proper classification step. This mirrors exactly the kind of gap the
grammar audit found in `CefrJGrammarImportService`'s A1-fallback default — a real, careful import
path exists, but the actual shipped seed data took a shortcut past it.

## 6. Feasible edge model — vocabulary does not have grammar's kind of prerequisite structure

Grammar's 811 edges work because there's a real linguistic teaching-order fact behind most of them
("negative form builds on the affirmative," "present perfect builds on present simple"). **No
equivalent fact exists between two arbitrary vocabulary words.** Knowing "shop" does not require or
build on knowing "book." Options, in order of how defensible they are:

- **CEFR-level-within-topic ordering** (weakest but easiest): within one real topic, an A1 word
  could be a soft `recommended_before` for a B1 word of the same topic. Mechanically generatable
  for the ~2,157 words with a real topic; **not meaningfully generatable for the 70.4% without
  one** (there's no topic to order within). Pedagogical value is genuinely thin — vocabulary
  acquisition research does not generally treat this as a real prerequisite relationship, just a
  rough sequencing hint at best.
- **Synonym/near-synonym clustering** (most valuable, not currently possible): would need a real
  lexical resource (WordNet-style synonym data, or an AI-assisted pass) — **no such source exists
  in this repo today.** Not a "backfill from existing data" job like grammar's provenance was; this
  would be new content generation.
- **Word-family/morphological clustering** ("act"/"active"/"action"/"actor"): also not derivable
  from the current two CSVs (no lemma/root column) — same "new data" caveat as synonyms.
- **Frequency-based ordering**: the `Threshold` column (§3) hints at frequency/thematic banding but
  isn't a clean frequency rank, and 72% of rows still lack it.

**Recommendation**: do not attempt a full edge model in the next phase. If anything ships first, it
should be the CEFR-within-topic `recommended_before` edges, scoped explicitly to the ~2,157
genuinely-topicked words, clearly labeled as a soft sequencing hint (never a `hard_prerequisite`,
per the typed-relationship model already recommended for grammar's GSG-2) — not a claim that this
is how vocabulary should really be gated.

## 7. Feasible tag model

`SkillGraphNode.ContextTagsJson`/`FocusTagsJson` already exist as real, populated JSON-array
columns — technically reusable for vocabulary today, no schema change needed. But
`CurriculumContextTagConstants` (the only currently-validated tag vocabulary, 13 values:
`general_english`, `day_to_day`, `travel`, `study_academic`, `migration_settlement`,
`job_interviews`, `social_conversation`, `workplace`, `pronunciation`, `listening_confidence`,
`writing_confidence`, `exam_inspired`, `custom`) is a **student life-goal taxonomy**, not a
word-topic taxonomy — "shopping," "food," "clothes" don't belong in it, and forcing them in would
corrupt a vocabulary that Sprint 3's goal-vector routing already depends on.

**Recommendation**: don't reuse `CurriculumContextTagConstants` for this. The 33 real CoreInventory
topic names are already a reasonable tag vocabulary in miniature — the cheapest real improvement is
surfacing the existing topic (already in `ParentNodeId`) as a visible tag/badge in the admin UI,
not inventing a second, separate tag field that duplicates it.

## 8. Recommended target model (mirrors GSG-1's provenance discipline, smaller scope)

Rather than re-categorizing 6,881 words in one pass (expensive, and this audit found no cheap
source data to drive it), apply the same honesty-over-guessing principle GSG-1 used for grammar's
CEFR fallback:

```
topicSource: cefrj_core_inventory | pos_fallback | uncategorized
topicConfidence: attested | fallback
```

- Words currently in a real CoreInventory topic → `attested`.
- Words currently dumped in one of the 4 POS catch-all containers → re-labeled `fallback`, and the
  container itself renamed to make the fallback nature honest (e.g. "General Verbs" →
  "Verbs — uncategorized" or similar), rather than presenting a part-of-speech bucket as if it were
  a real semantic topic.
- This is additive metadata, same migration shape as `CefrConfidence`/`NodeType` — low risk, no
  data loss, directly reusable for a future admin UI filter ("show only reliably-categorized
  vocabulary").

**Explicitly not recommended as a next phase**: generating prerequisite edges at scale, inventing a
new tag taxonomy, or attempting AI-assisted re-categorization of the 6,881 fallback words — each of
these needs its own scoped decision (and, for the edge/tag work, new source data or a real content
budget) rather than being bundled into a "just fix vocabulary" pass.

## 9. Prioritized findings

- **P0 (data honesty)**: the 4 POS catch-all buckets are presented as real topics when they are a
  later, undocumented shortcut around the real import service's own uncategorized-word handling.
  Mislabeling this as "vocabulary is well-organized into 37 topics" would be actively misleading if
  it ever informs routing.
- **P1 (structure)**: zero prerequisite edges. Worth a scoped decision on whether vocabulary needs
  *any* edges at all, versus being a legitimately edge-less "practice pool" skill in the eventual
  composer — not obviously a bug the way it looked in the graph view.
- **P2 (visualization)**: the admin graph currently renders all 9,775 words with no edges as a flat
  island field once you drill into any topic. This is honest given §3/§6, but a large flat list
  inside "General Verbs" (1,766 words) is not a usable admin view regardless of the categorization
  question — worth a pagination/search improvement independent of the data-quality fixes above.

## 10. Explicit conclusion on safety

Same framing as the grammar audit: **safe for admin visualisation** today (with the P2 UX caveat
above). **Not safe for CEFR-aware routing or next-skill selection** — not because of missing
prerequisite edges (that may be an acceptable design for a vocabulary practice pool) but because
70.4% of words are sitting under a topic label that doesn't actually mean what it appears to mean.
**Not evaluated for learner mastery or activity generation** — out of this audit's scope; the
per-word `definition`/`explanation` content quality was not reviewed here (unlike the grammar
audit's §15 content pass), and should be if mastery/routing work on vocabulary is scoped next.

## Next recommended action

Get explicit sign-off on which of §8's `topicSource`/`topicConfidence` fields to add before writing
any code — this is a smaller, well-scoped follow-up (mirrors GSG-1's Stage 1) that makes the
existing 70.4% honestly labeled without attempting the much larger, currently-underspecified
work of real re-categorization, edge generation, or a new tag taxonomy.

---

## Explicit statement

No seed remediation was implemented during this audit. `data/seed-json/vocabulary-seed.json` was
read-only throughout. No application code, migrations, or tests were modified. All computation was
done via inline `node -e` one-liners in the shell (not saved as scripts, nothing to clean up).

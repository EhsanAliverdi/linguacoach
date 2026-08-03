---
status: active
lastUpdated: 2026-08-04
owner: architecture
---

# Unit Content Seeding Rules

Compulsory rules for turning one unit of external teaching material into skill-graph seed
content (`data/seed-json/*.json`). Every unit processed this way — past, current, or future —
must satisfy every rule below before it's considered done. This file is the single source of
truth for the process; do not re-derive or improvise these rules per unit.

## 1. No reference to the source material, anywhere

No file name, node key, node title, description, lesson body, exercise text, code comment, or
doc may name the source book, its publisher, its internal unit/file/lesson numbering (e.g. "1A,"
"File 1," "Episode 1"), or its character/scenario names. Node keys and titles use plain,
standard ELT terminology only (e.g. `grammar.verb_be.singular_i_you`, "Verb be — singular: I and
you"). The source material informs *what* to teach and *in what order* — it never appears as an
identifier or label anywhere in the codebase.

## 2. No verbatim text

Every dialogue, sentence, question, and example written into a seed file is newly authored —
different names, numbers, and scenarios than the source. Fixed generic social expressions that
are standard English regardless of source ("Nice to meet you," "How are you?") are fine to reuse
as-is; specific invented dialogue lines, character names, and scenario combinations are not.

## 3. Every section of a lesson becomes a node — nothing silently dropped

A real lesson is a sequence of numbered sections (e.g. an opening listening/speaking warm-up,
a grammar presentation, a vocabulary set, a pronunciation focus, a further speaking/writing
task). **Each distinct section becomes its own leaf** (or is folded into an existing leaf only
when it is genuinely the same teaching point practiced a second time — never dropped because it
seemed minor). Before a unit is marked done, enumerate every numbered section of every lesson in
it and confirm each one maps to a node. A section that is "just" a warm-up dialogue or a
speaking-practice activity still gets a real leaf with its own lesson body and exercises — it is
not optional filler.

## 4. Cross-references are resolved, not left dangling

When a lesson points to a fuller reference elsewhere (a grammar reference section, a vocabulary
reference section, a paired-practice/communication activity, a writing task), that referenced
section's real content — the fuller rule table, the extra practice items, the complete word
list — is read and incorporated into the corresponding node's lesson body and exercises. A
cross-reference is a signal that the in-lesson snippet is incomplete on its own; the node must
reflect the complete teaching point, not just the shorter in-lesson excerpt.

## 4b. Reference/bank pages are read in full, not reconstructed from memory (2026-08-04)

Many source books have dedicated reference sections beyond the numbered lessons — a grammar
reference bank, a vocabulary reference bank, paired-practice/communication pages. Rule 4 requires
folding these in when a lesson cross-references them; this rule requires actually opening and
reading each one, not reconstructing its likely content from the in-lesson snippet plus general
ELT knowledge. Reconstructing from memory reliably misses real content — a past pass on this
project inferred vocabulary and grammar-rule word lists instead of reading the source's actual
reference pages, and a follow-up audit that opened every page found real gaps (missing words,
missing spelling rules) on the majority of leaves checked. Before a unit is marked done:

- **Open and read every Vocabulary Bank / Grammar Bank / Communication (or equivalent) page** for
  that unit's lessons, not just the in-lesson excerpt.
- **Cross-check each leaf's word list or rule set against the actual reference page**, word for
  word / rule for rule, and add anything missing.
- Treat a reference page as the ground truth for what "complete" means for that leaf — a
  plausible-sounding word list is not the same as a verified-complete one.

## 5. Container/leaf shape

- A **container** is purely structural/thematic and may be skill-less (`Skill = null`). It
  groups leaves — and may group other containers — around a topic or theme, and may itself carry
  prerequisite edges to other containers.
- A **leaf** is a single, independently-measurable skill item (`Skill` is required: grammar,
  vocabulary, pronunciation, speaking, listening, reading, writing, fluency, confidence, or
  collocation). Every leaf gets a full lesson body, example sentences, common mistakes, and one
  or more original exercises whose count and question depth match the source section's real
  depth (a section with several sub-exercises becomes a leaf with several `Exercise` rows, not
  one generic auto-composed exercise).

## 4a. Lesson body reads like a real teaching unit, not a fact dump (2026-08-04)

A Lesson's `Body` is what a student actually reads. A single sentence cramming a comma-separated
list of facts ("Common country names in English, always written with a capital letter: Brazil,
Canada, China...") is not acceptable — it does not teach, it just states. Every Lesson body must:

- **Open with a framing sentence** that tells the student what they're about to learn and why it
  matters ("In this lesson, you'll learn... — useful for..."). Never start directly with the raw
  content.
- **Present the core content in a structured, readable way** — short paragraphs and bullet-style
  lines (`\n\n` between sections, `•` for lists), not one dense run-on sentence. `Lesson.Body` is
  plain text rendered with `white-space:pre-wrap`/`<pre>` on both the admin and student views, so
  real line breaks render correctly — use them.
- **Close with a concrete "by the end of this lesson" statement** connecting the content back to
  something the student can actually do.
- **Include a supporting image when the topic is visual/concrete** (`Lesson.ImageUrl` — e.g.
  country flags for a countries lesson, a labeled classroom photo for classroom vocabulary).
  Only set this with a real, rights-cleared image URL — never fabricate or guess a URL. When no
  suitable image is available yet, leave it null rather than guessing; it's a content-curation
  gap to close later, not a reason to block the rest of the lesson.

## 5a. Leaf ↔ Lesson ↔ Module (2026-08-03)

A leaf is what a student must master before moving to the next node — it is the mastery gate.
Concretely:

- **Every leaf gets exactly one `Lesson`** — the canonical explanation for that leaf, assigned
  via `Lesson.AssignToLeaf(leafId)` immediately after the Lesson is created. Enforced by a
  partial unique index (`ix_lessons_skill_graph_node_id_unique`) so a leaf can never end up with
  two Lessons.
- **A leaf may be referenced by many `Module`s.** `Module` is the delivery/bundling concept (what
  gets packaged together for one learning session, e.g. Today Plan or a spaced-repetition
  review) — not the thing that owns the leaf's content. Two different Modules can legitimately
  both teach leaf X on two different days.
- `Lesson.SkillGraphNodeId` is nullable at the type level (not a constructor argument) because
  Lesson is also created by unrelated admin/AI authoring flows that build content from source
  material before any target leaf is decided — forcing a leaf id at construction time there
  would break real, already-working functionality unconnected to this seeding process. The
  curated seeding pipeline (`RichContentSeeder`) is the one path that always assigns it
  immediately, so every leaf produced by this process has exactly one Lesson in practice.

## 5b. Content only uses what's already been taught (2026-08-04)

A leaf's `LessonBody`, `Examples`, `CommonMistakes`, and every exercise question must only use
vocabulary and grammar structures that a student has already met by that point in the sequence —
never a word or structure from a later or sibling-but-not-yet-reached leaf, and never a word that
appears nowhere in the taught vocabulary at all.

- **Before writing a leaf's content**, build (or update) the cumulative list of everything taught
  so far: every word listed in an earlier vocabulary leaf's word list, every fixed phrase from an
  earlier functional-language leaf, and the grammar structures already covered by earlier grammar
  leaves (per the prerequisite chain and file/lesson order, not alphabetical or domain order).
- **Names, numbers already taught, and closed-class function words** (pronouns, articles,
  prepositions, conjunctions, auxiliary verbs) don't need to appear in a vocabulary leaf first —
  everything else (nouns, verbs, adjectives, adverbs) does.
- **When a leaf needs a word that hasn't been taught yet**, either restructure the example to use
  an already-taught word instead, or — if the word is genuinely necessary and reusable — add it to
  that leaf's own vocabulary content first (or an earlier sibling leaf's), so it's taught before
  it's assumed.
- **This applies within a single unit too**, not just across units: a grammar leaf must not lean
  on a vocabulary leaf that comes later in the same file's teaching order.
- **Vocabulary, functional-language, and pronunciation leaves are self-introducing** — their own
  lesson body and examples are how a word gets taught in the first place, so a leaf in one of
  these three domains may freely use its own new words in its own content. **Grammar, reading,
  writing, and listening leaves are application-only** — they must never introduce a brand-new
  content word; they only reuse words already taught by an earlier vocabulary/functional-
  language/pronunciation leaf (or, rarely, by an earlier grammar leaf's own worked examples).
- **When a grammar leaf's examples are deliberately built around a specific vocabulary leaf's
  words** (e.g. a possessives lesson practiced through family vocabulary), add an explicit
  prerequisite edge so that vocabulary leaf precedes the grammar leaf — don't rely on
  `difficultyBand` alone to get the order right, since two leaves can share a band.
- **Two narrow exemptions**, both because they aren't target-language content the student must
  produce: (1) metalinguistic/grammatical terminology used to *explain* a rule in `LessonBody` or
  `CommonMistakes` (e.g. "possessive," "imperative," "percentage") — this is the medium of
  instruction, the same way exercise instructions like "Choose the correct answer" don't need
  pre-teaching; (2) a word used purely to illustrate a spelling/grammar pattern inside a quoted
  wrong-vs-right example (e.g. "'watchs' is wrong — say 'watches'" to illustrate a plural
  spelling rule) — the point is the pattern, not the word itself. Don't stretch these exemptions
  to cover a word that also appears as ordinary sentence content elsewhere in the same leaf.
- **Verify before marking a unit done**: re-read every new leaf's content once the whole unit is
  drafted, checking it against the cumulative vocabulary list built through the end of that unit —
  don't rely on the word "feeling" basic; confirm it was actually taught.

## 6. Prerequisites reflect real teaching order

Prerequisite edges are authored explicitly based on genuine pedagogical sequencing (what must be
understood before what), not generated mechanically. Edges may cross skills freely (a vocabulary
leaf can be a prerequisite for a grammar leaf, or vice versa) — this is expected and correct
where the source material's own sequencing implies it.

## 7. File organization

Seed content is organized by skill domain (`grammar-seed.json`, `vocabulary-seed.json`,
`pronunciation-seed.json`, `functional-language-seed.json`, etc.), each covering every unit
processed so far — not one file per unit or per lesson. A `prerequisites-seed.json` carries all
cross-skill/cross-domain edges in one place. New domains get new files as needed (e.g. no
`pronunciation-seed.json` existed before content needed it).

## 8. Per-unit completion checklist

Before a unit is considered seeded:

- [ ] Every numbered section of every lesson in the unit maps to a leaf (rule 3).
- [ ] Every cross-reference in those sections has been read and folded in (rule 4).
- [ ] Every Vocabulary Bank / Grammar Bank / Communication (or equivalent) reference page for the
      unit has actually been opened and read, and cross-checked word-for-word / rule-for-rule
      against the matching leaf — not reconstructed from memory or the in-lesson snippet (rule 4b).
- [ ] Every leaf has a lesson body, examples, common mistakes, and exercises matching the
      source's real exercise/question depth (rule 5).
- [ ] Every word used in every leaf's content has already been taught by that point in the
      sequence, or is a name/number-already-taught/closed-class function word (rule 5b).
- [ ] Prerequisite edges have been authored for every real sequencing relationship, including
      ones to/from leaves introduced in earlier units (rule 6).
- [ ] No node key, title, file name, or doc references the source material (rules 1–2).

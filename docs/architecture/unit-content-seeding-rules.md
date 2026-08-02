---
status: active
lastUpdated: 2026-07-31
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
- [ ] Every leaf has a lesson body, examples, common mistakes, and exercises matching the
      source's real exercise/question depth (rule 5).
- [ ] Prerequisite edges have been authored for every real sequencing relationship, including
      ones to/from leaves introduced in earlier units (rule 6).
- [ ] No node key, title, file name, or doc references the source material (rules 1–2).

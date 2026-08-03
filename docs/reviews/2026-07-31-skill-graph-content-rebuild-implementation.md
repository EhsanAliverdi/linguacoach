---
title: Skill Graph Container/Leaf Content Rebuild — Implementation Record
date: 2026-07-31
related:
  - docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md
  - docs/reviews/2026-07-31-vocabulary-skill-graph-audit.md
  - docs/architecture/adaptive-curriculum-skill-graph.md
  - docs/architecture/unit-content-seeding-rules.md
---

# Skill Graph Container/Leaf Content Rebuild — Implementation Record

## 1. Summary

Following the grammar and vocabulary seed audits (real container hierarchies, but zero
cross-skill bundling and zero collocation nodes), this session implemented the schema change a
container/leaf redesign needed, wrote a compulsory process spec
(`docs/architecture/unit-content-seeding-rules.md`) for turning source teaching material into
seed content, and rebuilt the skill graph's content from scratch under that spec — organized by
skill domain (`grammar-seed.json`, `vocabulary-seed.json`, `pronunciation-seed.json`,
`functional-language-seed.json`, `prerequisites-seed.json`), covering two units' worth of
beginner content end to end (`SkillGraphNode` → `Lesson`/`Exercise`/`Module` →
`ModuleSkillGraphNodeLink`).

## 2. Corrections made mid-session

An earlier pass in this session got the approach wrong in two ways, corrected before proceeding:

- **Source-material references leaked into node keys, titles, and file names** (e.g. a lesson
  numbering scheme and a scenario title borrowed directly from the source book). Deleted and
  rebuilt with plain ELT terminology only — see rule 1 of the seeding-rules spec.
- **A single lumped-together file per lesson**, rather than the established per-skill-domain
  file organization. Reverted to `grammar-seed.json`/`vocabulary-seed.json`/etc., each covering
  every processed unit so far, per rule 7.

The wrong first pass's DB rows, JSON file, and seeder code were all deleted before rebuilding
(nothing from it was kept).

## 3. Decisions locked in (via AskUserQuestion / direct instruction)

- Functional/social phrases → classified under **Speaking** (`speaking.functional_phrases`
  subskill), not Vocabulary.
- **Collocation** → a new top-level skill (`CurriculumSkillConstants.Collocation`), a peer of
  vocabulary/grammar/pronunciation/speaking. No leaves use it yet (true collocations are sparse
  in beginner-level material) — flagged as an open item, not a defect.
- Granularity → as close to 1:1 with the source's real exercise/question count as practical, not
  one generic auto-composed exercise per leaf.
- Copyright → internal dev instance; still no verbatim source text or audio/video. All content
  is newly authored to hit the same teaching points in the same exercise shapes.
- Scope → two units' worth of content (four lesson-blocks plus one functional-language episode),
  enough for real cross-lesson prerequisite chains.
- **Compulsory process rule** (this session's key correction): every section of a source lesson
  must map to a node — nothing silently dropped — and every cross-reference to a fuller
  reference section must be read and folded into the corresponding node. Codified permanently in
  `docs/architecture/unit-content-seeding-rules.md`.

## 4. Schema changes

- `SkillGraphNode.Skill` is `string?` — required on leaves, optional (and used) on containers.
  Migration `20260731021938_MakeSkillGraphNodeSkillNullable`.
- `SkillGraphRoutingService`'s candidate query filters `Skill != null` explicitly.
- `CurriculumSkillConstants.Collocation` added.
- `CurriculumSubskillConstants.SpeakingFunctionalPhrases` added, wired into `Speaking`'s subskill
  list.
- **Bug fixed during this session**: `tools/LinguaCoach.ContentSeeder`'s `UpsertContainersAsync`/
  `UpsertLeafAsync` helpers hard-coded a concrete `Skill` value for every container (copied from
  the pre-existing single-skill-tree seeders) — this silently defeated the skill-less-container
  redesign until caught by inspecting seeded row counts. `Skill` is now `string?` through the
  whole helper chain; the four new domain seeders explicitly pass `null` for containers.

## 5. Data changes

- Existing 13 seed JSON files (pre-dating this redesign) archived, not deleted, to
  `data/seed-json/archive/2026-07-31-pre-rebuild/`.
- Dev DB wiped after confirming zero rows in `StudentExerciseLaunch`/
  `StudentTodayPlanModuleAssignment`/`StudentPracticeGymModuleAssignment` (no real student data
  at risk).
- New content seeded via four domain files + one prerequisites file:
  - `grammar-seed.json` — 1 container ("Verb be"), 4 leaves (singular I/you, singular
    he/she/it, plural we/you/they, Wh-/How questions), 11 exercises.
  - `vocabulary-seed.json` — 4 containers, 6 leaves (numbers 0–10, numbers 11–100 and phone
    numbers, days of the week, countries, nationalities, classroom objects), 11 exercises.
  - `pronunciation-seed.json` (new domain — no prior file covered this content shape) — 1
    container, 3 leaves, 3 exercises.
  - `functional-language-seed.json` (new domain) — 3 containers, 6 leaves (short social
    exchanges, classroom instructions, introducing yourself, saying goodbye, spelling/alphabet,
    service requests), 9 exercises.
  - `prerequisites-seed.json` — 6 cross-lesson edges (e.g. singular I/you → singular he/she/it →
    plural we/you/they → Wh-/How questions; numbers 0–10 → numbers 11–100; countries →
    nationalities; spelling → service requests).
- Seeded via a new `RichContentSeeder` helper (`tools/LinguaCoach.ContentSeeder/
  RichContentSeeder.cs`) that builds a full `Lesson` + multiple multi-question `Exercise` rows +
  a `Module` per leaf — deliberately separate from the existing CSV-driven `LeafContentSeeder`
  path (a poor fit for curated, multi-exercise lesson content; that path and its CLI domains are
  untouched).
- Final DB state: 28 `SkillGraphNode` rows (9 skill-less containers, 19 leaves), 6 prerequisite
  edges, 19 `Module`/`Lesson` rows, 34 `Exercise` rows.

## 6. An unrelated finding, cleaned up

Mid-session, 4 unexpected `collocation`-skill nodes appeared in the DB. Root cause: the admin
Skill Graph UI's existing AI "draft nodes" feature (`POST /api/admin/skill-graph/draft`) was
called against the live API — visible in the API logs as a Gemini call
(`skill_graph_propose_nodes`) — not something this session's seeding code did. Deleted (all 4
were `PendingReview`, never approved) since they aren't part of the curated rebuild content.
Flagging here since it's a real, reproducible behavior of the admin UI (adding a new
`CurriculumSkillConstants` value makes it a candidate for that feature's next draft pass) worth
being aware of if the dev API is left running with the admin UI open during future work.

## 7. Verification performed

- `dotnet test` — all three suites green: UnitTests 2594/2594, ArchitectureTests 30/30,
  IntegrationTests 1410/1410. (Fixed `GrammarSeedIntegrityTests`' hardcoded paths to the old
  seed files twice — once to point at the archive, once more after the archive folder itself was
  renamed to remove a source-material reference.)
- Dev DB row counts verified against each seed file's actual content (9 containers with
  `skill IS NULL`, 19 leaves with `skill` set, matching skill/subskill breakdown).
- API container rebuilt and restarted; boots healthy against the migrated schema.

## 8. Known gaps / next steps (per the seeding-rules checklist)

Per `docs/architecture/unit-content-seeding-rules.md`'s per-unit checklist, this pass is **not**
a fully exhaustive section-by-section rebuild of both source units — some combined
reading/listening-comprehension sections in the source material (sections that practice an
already-covered grammar/pronunciation point in listening form, rather than teaching something
new) were treated as covered by the existing leaf's exercises rather than split into their own
listening/reading-skill leaf. This is a judgment call, not an oversight, but should be revisited
explicitly (per rule 3) before declaring the two units fully covered:
- The combined reading/listening comprehension sections in three of the four grammar lessons.
- Any remaining consolidation/review content between the two units.
- `Collocation` skill has zero leaves — expected at this content level, not a defect.

## 9. Final verdict

Schema redesign, process spec, and first real content rebuild complete and verified at the
DB/test/API-boot level, this time following the compulsory seeding rules (no source-material
references, per-skill file organization, full section coverage, cross-reference resolution).
Ready for review against the checklist in section 8 before extending further.

## 10. Next recommended action

Review the checklist gaps in §8 and decide whether the combined comprehension sections warrant
their own listening/reading leaves before this content set is considered complete.

---

## 11. Addendum (2026-08-04) — remaining lesson-blocks closed, §8 gaps resolved

Following §10, the grammar/vocabulary/pronunciation/functional-language content built in this
pass already covered every lesson-block's headline grammar/vocabulary/pronunciation point (all
5 lesson-blocks worth of source material, not just one) — that was not obvious from §5 alone
since content was organized by skill-domain file rather than lesson-by-lesson. An explicit
lesson-by-lesson audit against the seeding-rules checklist found exactly four genuine gaps —
sections that teach something new, not just extra practice of an already-covered point — and
closed them:

- **A pronunciation leaf for number-pair stress** (`pronunciation.beginner_sounds.
  number_stress_pairs`) — distinguishing pairs like 13/30 by stress, not segmental sound, genuinely
  distinct from the three existing sound-based pronunciation leaves.
- **A new Reading domain** (`reading-seed.json`, CLI keyword `reading-comprehension` — distinct
  from the legacy CSV-driven `reading` keyword to avoid collision) — one leaf, reading a short
  original dialogue for gist then detail.
- **A new Writing domain** (`writing-seed.json`, CLI keyword `writing`) — one leaf, formatting
  rules for a personal-information form (name capitalization, phone/email format).
- **A new Listening domain** (`listening-seed.json`, CLI keyword `listening-comprehension` —
  distinct from the legacy `listening` keyword, which also does real TTS audio generation) — one
  leaf, comprehension of a short spoken description, delivered as a text transcript (no audio/
  video bundled, per the copyright exclusion rule).

**Resolved without new leaves** (rule 3's "same teaching point, not new content" exception,
applied deliberately rather than by omission):
- A lesson-block's own listening/speaking practice of an already-taught grammar/pronunciation
  point (no new teaching content, just more practice) — left as-is, not duplicated into a new
  leaf.
- Cross-referenced paired-speaking/information-gap activities (further practice of already-taught
  vocabulary/grammar) — judged as additional practice of existing content, not a new teachable
  point requiring its own leaf.

4 new cross-skill prerequisite edges added (10 total now), each justified by a genuine content
dependency (e.g. the personal-information writing leaf requires already knowing numbers 11–100
to format a phone number).

**Final state**: 35 `SkillGraphNode` rows (12 skill-less containers, 23 leaves spanning grammar,
vocabulary, pronunciation, speaking, reading, writing, and listening), 23 `Module`/`Lesson` pairs
(each leaf has exactly one Lesson, verified), 42 `Exercise` rows, 10 prerequisite edges. All three
backend test suites green (UnitTests 2594, IntegrationTests 1417, ArchitectureTests 30). Every
lesson-block from the two source units (1A, 1B, the Practical-English episode, 2A, 2B) now has
its full teaching content represented in the graph.

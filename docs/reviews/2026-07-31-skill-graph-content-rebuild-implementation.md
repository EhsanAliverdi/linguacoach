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

## 12. Addendum (2026-08-03) — quality re-review of all 23 lesson bodies

Requested follow-up: re-read every lesson body/exercise across all 7 active seed files and
verify each genuinely meets the "proper teaching unit" bar (framing sentence, structured
content, closing outcome statement — rule 4a) and has adequate practice depth, not just correct
prose.

**Structure/prose check (rule 4a)**: all 23 lessons already followed the framing → structured
content → closing template correctly. No rewrites needed here.

**Depth check**: found a real inconsistency — 7 of the 23 leaves had only 1 exercise (or
noticeably fewer questions) while sibling leaves in the same container had 2, with no
pedagogical reason for the gap. Fixed by adding one exercise to each:

- `vocabulary.countries_and_nationalities.countries` — was 1 exercise/4 questions vs. its
  sibling `nationalities` at 2/7. Added a "choose the correctly written country name" choice
  exercise (capitalization, "the" usage) — now 2 exercises/7 questions.
- `pronunciation.beginner_sounds.h_ai_i`, `.i_ou_s_sh`, `.dz_tsh_sh` — each had only 1 exercise
  while their sibling `number_stress_pairs` had 2. Added a "find the odd one out" choice
  exercise to each, using only words already introduced in that leaf's own examples — now all
  4 pronunciation leaves have 2 exercises each.
- `functional_language.meeting_people.introducing_yourself`, `.saying_goodbye`,
  `functional_language.everyday_transactions.spelling_and_alphabet` — each had only 1 exercise
  while their siblings had 2. Added a second exercise to each (a text-completion or choice
  exercise reinforcing the same fixed phrases) — now all 6 functional-language leaves have 2
  exercises each.

No content was found to be factually wrong, off-topic, or in violation of the no-book-reference/
no-verbatim-text rules — this pass was purely a depth-parity fix, not a rewrite.

**Re-seed**: `TRUNCATE` + full re-run of all 7 domain seeders + prerequisites, in the
established order. Final counts: 35 nodes (12 containers / 23 leaves), 23 Lessons, **49**
Exercises (was 42 — +7 from this pass), 10 prerequisite edges. All three backend test suites
green (UnitTests 2594, IntegrationTests 1418, ArchitectureTests 30). API container restarted and
confirmed healthy.

**Final verdict**: content quality bar is now consistently met across all 23 leaves, including
practice depth. No further action needed unless new gaps are found in a future audit.

**Next recommended action**: none pending from this pass; resume normal feature work.

## 13. Addendum (2026-08-03) — Files 3-5 built, extending coverage past the original two files

Following the same compulsory process (`docs/architecture/unit-content-seeding-rules.md`), three
more source-material lesson-blocks were read and built out in full: a nouns/determiners
lesson-block, a possessives/description lesson-block, and a simple-present lesson-block, each
following the source's own A-lesson / B-lesson / Practical-English-episode structure. No book
references, unit numbers, or scenario names were carried into any node key, title, or file name
— only the underlying grammar/vocabulary/pronunciation/functional-language points and their
teaching order.

**New containers (11)**: 4 grammar (`grammar.nouns_and_determiners`, `grammar.possessives`,
`grammar.adjectives`, `grammar.simple_present`), 6 vocabulary (`vocabulary.everyday_objects`,
`vocabulary.people_and_family`, `vocabulary.descriptive`, `vocabulary.food_and_drink`,
`vocabulary.common_verb_phrases`, `vocabulary.time_and_feelings`), 1 functional-language
(`functional_language.time_and_arrangements`). No new pronunciation container was needed — all 8
new pronunciation leaves fold into the existing `pronunciation.beginner_sounds` container, since
they're still beginner-level discrete sounds.

**New leaves (25)**, each with a full lesson body (framing/structure/closing per rule 4a), 2
original exercises, and examples/common mistakes:

- Grammar (6): singular/plural nouns + a/an, this/that/these/those, possessive adjectives +
  possessive 's, adjective word order and form, simple present affirmative/negative (I/you/we/
  they), simple present questions (I/you/we/they).
- Vocabulary (8): small everyday objects, souvenirs, family members, colors and common
  adjectives, everyday food and drink, common verb phrases (love/live/work/want/have +
  object), telling the time, saying how you feel.
- Pronunciation (8): plural -s endings (/s//z//ɪz/), voiced /ð/ + sentence rhythm, /ʊr//s//k/ +
  the letter-c rule, /ʌ//æ//ə/, /ɑr//ɔr/ + linking, /dʒ/ vs /g/ (the letter-g/j rule), /w//v/ +
  linking, /ɑ/ + silent consonants.
- Functional language (3): understanding and asking about prices, ordering food and drink,
  asking and telling the time (including apologizing for lateness).

**Resolved without new leaves** (rule 3's "same teaching point, not new content" exception): a
review-and-check section between two of the source lesson-blocks (pure revision, no new teaching
point) — not duplicated into a leaf, consistent with how earlier review sections were handled.

**13 new prerequisite edges** (23 total now), each a genuine dependency: e.g. this/that/these/
those requires singular/plural nouns first (the demonstrative choice depends on that
distinction); souvenirs vocabulary requires this/that/these/those (the shopping dialogue is built
on them); simple-present questions require simple-present affirmative/negative first; telling the
time requires numbers 11-100 (time expressions reuse those number words).

**Re-seed**: full `TRUNCATE` + re-run of all domain seeders (grammar, vocabulary, pronunciation,
functional-language, reading-comprehension, writing, listening-comprehension) + prerequisites, in
the established order. Final counts: 71 `SkillGraphNode` rows (23 skill-less containers, 48
leaves), 48 Lessons (every leaf has exactly one, verified via
`lessons_without_leaf = 0`), 99 Exercises, 23 prerequisite edges. All three backend test suites
green (UnitTests 2594, IntegrationTests 1418, ArchitectureTests 30). API container restarted and
confirmed healthy.

**Final verdict**: content now covers the source material's first five lesson-blocks in full
(the original two plus these three), at the same quality bar established and verified in §12.

**Next recommended action**: none pending; further lesson-blocks can be added the same way when
requested.

## 14. Addendum (2026-08-04) — Files 6-8 built

Following the same compulsory process, three more source lesson-blocks were read and built out:
a third-person/routines lesson-block, a weekend/opinions lesson-block (plus a date/phone
practical-English episode), and a permission/preferences lesson-block. No book references, unit
numbers, or scenario/character names were carried into any node key, title, or file name.

**New containers (9)**: 5 grammar (`grammar.adverbs_of_frequency`, `grammar.question_word_order`,
`grammar.imperatives_and_object_pronouns`, `grammar.modals`, `grammar.liking_verbs`), 4
vocabulary (`vocabulary.work`, `vocabulary.daily_routine`, `vocabulary.entertainment`,
`vocabulary.free_time`). No new pronunciation, functional-language, or reading containers were
needed — new leaves in those domains fold into existing containers
(`pronunciation.beginner_sounds`, `functional_language.time_and_arrangements`,
`reading.everyday_situations`).

**New leaves (22)**, each with a full lesson body, 2 original exercises, and examples/common
mistakes:

- Grammar (6): simple present third person (he/she/it), adverbs of frequency, question word
  order (be + simple present), imperatives + object pronouns, can/can't, like/love/hate + -ing.
- Vocabulary (7): jobs and places of work, a typical day, free-time verb phrases (common verb
  phrases 2), kinds of movies, months and ordinal numbers, public-sign verb phrases (common verb
  phrases 3), activities.
- Pronunciation (6): /y/ and /yu/, /w//h//ɛr//aʊ/, sentence rhythm with opinions, /θ/ in ordinal
  numbers, can/can't stress (/æ/ vs /ə/), /ʊ//u//ŋ/.
- Functional language (2): saying the date, talking on the phone.
- Reading (1): a short survey-style article with percentages.

**Resolved without a new leaf** (rule 3's "same teaching point, not new content" exception): the
third-person -s pronunciation pattern (/s//z//ɪz/) is the identical phonological rule already
taught for plural nouns, just applied to verbs instead — rather than duplicating it, the existing
`pronunciation.beginner_sounds.z_s_plural_endings` leaf was retitled to cover both and given a
third exercise using verb examples (lives, watches, works). Movie/TV video-listening tasks and
dating-profile/choir-video listening tasks were additional practice of already-taught
grammar/vocabulary (simple present routines, activities + -ing) — not duplicated into new leaves,
consistent with the video/audio exclusion rule and the "extra practice, not new content" pattern
established in §11.

**13 new prerequisite edges** (36 total now), all vocab-before-grammar or grammar-before-its-own-
extension: e.g. jobs vocabulary before third-person simple present (the grammar is practiced
through job Q&A); simple-present questions before the more complex be/simple-present word-order
lesson; can/can't grammar before its own stress-pattern pronunciation leaf.

**Re-seed**: full `TRUNCATE` + re-run of all domain seeders + prerequisites, in the established
order. Final counts: 102 `SkillGraphNode` rows (32 skill-less containers, 70 leaves), 70 Lessons
(every leaf has exactly one, verified via `lessons_without_leaf = 0`), 144 Exercises, 36
prerequisite edges. All three backend test suites green (UnitTests 2594, IntegrationTests 1418,
ArchitectureTests 30). API container restarted and confirmed healthy.

**Final verdict**: content now covers the source material's first eight lesson-blocks in full, at
the same quality bar established in §12.

**Next recommended action**: none pending; further lesson-blocks can continue the same way when
requested.

## 15. Addendum (2026-08-04) — Files 9-12 built; source material fully covered

Following the same compulsory process, the final four lesson-blocks of the source book were read
and built out: a present-continuous/clothes lesson-block, a hotels/simple-past-be lesson-block
(plus an inviting/offering practical-English episode), a simple-past-regular/irregular
lesson-block (plus a directions practical-English episode), and a final simple-past review
lesson-block. This completes every lesson-block in the source material end to end.

**New containers (10)**: 3 grammar (`grammar.present_continuous`, `grammar.existential_there`,
`grammar.simple_past`), 4 vocabulary (`vocabulary.clothes`, `vocabulary.hotels`,
`vocabulary.spatial_prepositions`, `vocabulary.places_in_town`), 2 functional-language
(`functional_language.invitations_and_offers`, `functional_language.directions`), 1 writing
(`writing.everyday_texts`).

**New leaves (33)**, each with a full lesson body, 2 original exercises, and examples/common
mistakes:

- Grammar (7): present continuous forms, present continuous vs. simple present, there's a/there
  are some, simple past of be, simple past regular verbs, simple past irregular verbs
  (get/go/have/do), eight more irregular past forms (buy/leave/say/see/send/sit/tell/write).
- Vocabulary (9): traveling phrases, clothes, hotel facilities, in/on/under, in/on/at for time
  and place, life-change verb phrases, get/go/have/do verb phrases, location/direction words,
  common places in town.
- Pronunciation (7): rhythm in present-continuous questions, /ər/ and other vowel sounds, the
  /ɪr/ vs. /ɛr/ spelling ambiguity, was/were stress, regular past -ed endings
  (/d//t//ɪd/ — the same three-way pattern as plural -s, now applied to a new suffix), vowel
  sounds in irregular past forms, polite intonation for requests.
- Functional language (4): offering food and drink, inviting and responding, asking for
  directions, giving directions.
- Reading (3): finding word meaning from context, correcting false information in a text,
  reading a short narrative story.
- Writing (2): an invitation email, a blog post about yesterday.
- Listening (1): completing missing information from a conversation transcript.

**Resolved without a new leaf** (rule 3's exception): the final lesson-block's own content is a
pure review board game (dice-and-squares format covering only already-taught simple past
material) with no new teaching point — not duplicated into a leaf, the same judgment applied to
every other pure-review section across this project. Several "sentence rhythm" pronunciation
sections with no new sound were folded into their grammar leaf's own practice rather than given
a redundant leaf, except where the rhythm was tied to a genuinely new practice context (e.g.
present-continuous questions, polite request intonation) — consistent with the precedent set in
§11 and §14.

**25 new prerequisite edges** (61 total now), continuing the same vocab-before-grammar and
grammar-before-its-extension patterns established throughout this project — e.g. hotel
vocabulary before there's-a/there-are-some; the simple past of be chained directly onto the
existing verb-be question mastery; each new irregular-verb leaf chained onto the previous one.

**Re-seed**: full `TRUNCATE` + re-run of all domain seeders + prerequisites, in the established
order. Final counts: 145 `SkillGraphNode` rows (42 skill-less containers, 103 leaves), 103
Lessons (every leaf has exactly one, verified via `lessons_without_leaf = 0`), 210 Exercises, 61
prerequisite edges. All three backend test suites green (UnitTests 2594, IntegrationTests 1418,
ArchitectureTests 30). API container restarted and confirmed healthy.

**Scope note**: this covers every lesson-block (grammar/vocabulary/pronunciation/speaking/
reading/writing/listening point) in the source book's main unit sequence. Reference-only sections
(the book's own grammar reference tables, vocabulary reference lists, and communication/
information-gap pages) were not opened as separate content — their content was already folded
into the relevant leaf's lesson body per rule 4 wherever a lesson referenced them, using
originally-authored explanations of the same grammar/vocabulary point rather than copying the
reference page. No audio or video files were used or bundled at any point, per the copyright
exclusion rule — every listening/video-listening section was either represented as an original
text transcript, or (where it was pure extra practice of an already-taught point) folded into an
existing leaf rather than treated as new content.

**Final verdict**: the entire source book is now represented in the skill graph, at the quality
bar established and audited in §12.

**Next recommended action**: none pending. Any further work would be a fresh audit/quality pass
of this now-complete set, not additional coverage.

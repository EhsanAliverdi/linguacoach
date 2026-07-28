# Skill Graph source data

Real, published CEFR-referenced grammar and vocabulary profiles, used as
the deterministic + AI-assisted source for the Skill Graph rebuild
(2026-07-28, see `docs/reviews/` for the implementation record once it
lands).

- `cefrj-grammar-profile-20180315.csv` — CEFR-J Grammar Profile, 500 rows.
  Hyphenated `ID` column encodes AFF/NEG/INT family structure (e.g. `8`,
  `8-1`, `8-2`, `8-3`), consumed by `CefrJGrammarImportService`.
- `cefrj-vocabulary-profile-1.5.csv` — CEFR-J Vocabulary Profile, 7,799
  words, A1-B2. `CoreInventory 1`/`CoreInventory 2` columns carry a real
  (partial — ~22% of rows) topic taxonomy.
- `octanove-vocabulary-profile-c1c2-1.0.csv` — Octanove Vocabulary
  Profile, 2,136 words, C1-C2. No topic data.
- `cefr-companion-volume-scales.pdf` — Council of Europe, "Common European
  Framework of Reference for Languages: Learning, teaching, assessment —
  Structured overview of all CEFR scales" (the CEFR Companion Volume's
  illustrative descriptor scales). Source of the Reading/Listening/
  Speaking/Writing `SkillGraphNode` taxonomy (2026-07-29) — Grammar and
  Vocabulary come from the CSVs above; these four skills come from this
  document's ~41 named descriptor scales (e.g. "Overall Reading
  Comprehension", "Reading for Orientation") with real A1-C2 "can-do"
  descriptors, not an invented taxonomy. The copyright of the descriptive/
  illustrative scales belongs to the Council of Europe — "Publishers
  should ask permission prior to using these instruments, and they must
  mention the copyright" (per the document's own notice). Used here for
  internal curriculum tagging, not redistribution.
- `cefr-companion-volume-scales.json` — the PDF's descriptor scales
  extracted (`pdftotext` + a parser, see `data/seed-json/cefr-scales-seed.json`
  for how it's turned into Skill Graph containers/leaves) into structured
  JSON: one entry per scale (`name`, `skillContext`, `levels: {A1..C2:
  descriptorText}`). `"No descriptor available"` cells from the source are
  dropped, never fabricated.

These are third-party published datasets — see each project's own site for
license terms before redistributing outside this repo.

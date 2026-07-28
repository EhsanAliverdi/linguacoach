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

These are third-party published datasets — see each project's own site for
license terms before redistributing outside this repo.

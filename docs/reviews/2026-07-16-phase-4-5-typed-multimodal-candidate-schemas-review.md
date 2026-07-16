# Phase 4.5 — Typed Multimodal Candidate Schemas

**Date:** 2026-07-16
**Related:** Phase 4.4E (`docs/reviews/2026-07-16-phase-4-4e-real-audio-duration-measurement-review.md`),
Phase 4.4D/C/B/A/4.4, Phase 4.3, Phase 4.2
**HEAD before work:** `6a180808` (feat: measure import audio duration)

## Scope

Full brief attempted: typed candidate contracts for the six candidate types with a real Resource
Bank publish target, a centralized parse/validate/serialize service, gating at creation/approve/
publish, a type-aware Candidate Review editor, asset-provenance enforcement, and a Playwright flow.
No `AskUserQuestion` scoping call was made — the brief's core deliverable (typed schemas + central
serializer + three enforcement gates) was judged achievable as a single bounded effort, matching
this session's Phase 4.4E precedent.

## 1. Audit of the pre-4.5 state

Before writing any code, the current shape was audited directly (not delegated blind):

- **`ResourceCandidate.NormalizedJson`** — a single free-text JSON blob holding every type-specific
  field, keyed by whatever field names the source row or staging code happened to use. No schema.
- **Field-name aliasing** — `ResourceCandidateFieldHelper.GetFieldCI` was the only real "contract":
  a case-insensitive, ordered alias lookup (`word`/`lemma`/`headword`, `grammarKey`/`title`,
  `passage`/`text`, etc.), duplicated verbatim between `ResourceCandidatePreviewService` and
  `ResourceCandidatePublishService`.
- **Candidate creation** — `ResourceImportService.ProcessRow` serializes the entire raw row into
  `NormalizedJson` verbatim (CSV/JSON generic import); `ImportPackageProcessingService.CreateListeningCandidate`
  builds a fixed `{"title":...,"transcript":...}` shape for audio-derived candidates.
- **Candidate editing API** — `PUT .../content` accepted a raw `NormalizedJson` string from the
  Angular admin UI and replaced the column verbatim; no server-side shape validation existed.
- **Candidate Review Angular page** — a single raw-JSON textarea was the *entire* editing
  experience for every candidate type; `FormioRendererComponent` was used only for
  `ActivityTemplateCandidate` preview, not editing.
- **Publish** — `ResourceCandidatePublishService.BuildTargetEntity` re-parsed `NormalizedJson` via
  `ResourceCandidateFieldHelper` independently per type, mapping into `ResourceBankItemContent`
  records (`VocabularyContent`, `GrammarContent`, `ReadingReferenceContent`, `ReadingPassageContent`,
  `WritingPromptContent`, `ListeningPassageContent`, `SpeakingPromptContent`) — six of the seven
  `ResourceCandidateType` values routed to a real bank target; `ActivityTemplateCandidate` and
  `Unknown` were (and remain) explicitly blocked.
- **Provenance** — `ImportCandidateAssetLink` (many-to-many `ResourceCandidate` ↔ `ImportAsset`)
  already existed and is the real FK-style provenance mechanism, created only in
  `ImportPackageProcessingService`'s Listening-candidate path.
- **Direct Resource-to-Exercise generation** — confirmed absent: exercise generation only ever reads
  from published `ResourceBankItem` rows via `LessonResourceLookup`, never a `ResourceCandidate`
  directly.

## 2. Typed candidate contracts

Six discriminated records under `LinguaCoach.Application.ResourceImport.CandidateContent`
(`CandidateContentContracts.cs`), one per candidate type with a real publish target — no new type
was invented:

| Candidate type | Content record | Fields |
|---|---|---|
| VocabularyEntry | `VocabularyCandidateContent` | Word*, Definition, PartOfSpeech, Examples[] |
| GrammarProfileEntry | `GrammarCandidateContent` | Title*, Explanation, Examples[], CommonMistakes[] |
| ReadingPassage | `ReadingCandidateContent` | PassageText*, Title, TextType, ReferenceSource |
| ListeningPassage | `ListeningCandidateContent` | Title*, Transcript |
| SpeakingPrompt | `SpeakingCandidateContent` | Title*, PromptText*, Instructions, Context, SuggestedDurationSeconds |
| WritingPrompt | `WritingCandidateContent` | Title*, PromptText*, Instructions, Genre, SuggestedMinWords, ExpectedLevel |

(`*` = enforced non-blank by `Validate`.) `ReadingPassage` deliberately covers both the short
"reference excerpt" and full-length "passage" Resource Bank shapes — that routing decision (by text
length) is made at publish time exactly as before this phase; the candidate stage doesn't need to
know which one it will become. CEFR level/skill/subskill/tags stay on the `ResourceCandidate`
entity itself (unchanged) — content records hold only genuinely type-specific fields, matching the
`ResourceBankItemContent` precedent this design mirrors.

## 3. Centralized serialization and validation

`IResourceCandidateContentSerializer` / `ResourceCandidateContentSerializer` — the **only** place
that parses, validates, or serializes a candidate's `NormalizedJson` from this phase forward:

```csharp
bool SupportsTypedSchema(ResourceCandidateType);
CandidateContentParseResult Parse(ResourceCandidateType, string normalizedJson, string? canonicalTextFallback = null);
CandidateContentValidationResult Validate(ResourceCandidateType, CandidateContent);
string Serialize(CandidateContent);
```

`ResourceCandidatePublishService.BuildTargetEntity` and every `Build*Entity` helper were rewritten
to call `Parse`/`Validate` instead of `ResourceCandidateFieldHelper.GetFieldCI` directly — the
duplicated per-type field-alias logic that used to live in the publish service is gone, replaced by
one call into the central serializer per type.

**Migration/compatibility strategy — the smallest safe additive design considered:** no new column
was added to `ResourceCandidate`. Every type's alias table lists its canonical field name first,
then every legacy source-column alias the pre-4.5 code understood (`word`/`lemma`/`headword`,
`grammarKey`/`title`, `passage`/`text`, `prompt`, `transcript`, `scenario`, `durationSeconds`,
`minWords`, etc.) — a pre-4.5 candidate row parses through the *exact same* lookup path a
brand-new typed row does. There is no separate, permanently-maintained "legacy fallback" branch —
just one alias table, visibly declared, used identically for old and new content.

**`canonicalTextFallback` — two deliberately different strictness levels, one function:**
- **Publish and Approve** call `Parse(type, json, candidate.CanonicalText)` — if the primary field
  (Word/Title/PassageText/PromptText) is missing from the JSON, `CanonicalText` fills it. This
  reproduces the exact pre-4.5 publish-time fallback behavior (`(GetFieldCI(...) ?? candidate.CanonicalText).Trim()`),
  so none of the 29 pre-existing `ResourceCandidatePublishServiceTests` needed their fixtures
  changed.
- **The typed edit endpoint** calls `Parse(type, typedContentJson)` with **no** fallback — an admin
  explicitly clearing a required field in the typed editor is treated as a genuine error, not
  something to silently rescue from whatever `CanonicalText` happens to hold.

This is the one deliberate asymmetry in the design: leniency where it protects backward
compatibility with data staged before this phase existed, strictness where an admin is actively
authoring content right now.

## 4. Gated creation, approval, and publication

**Creation (`ResourceImportService.ProcessRow`, new Gate 4):** enforced only when
`request.ImportPackageId is not null` — i.e. only the gated, plan-driven package pipeline (Phase 4.2's
Import Execution Plan gate). The legacy, ungated single-file E1 admin upload remains exactly as
permissive as before (`NeedsReview`, fix it in Candidate Review) — tightening that separate,
older, pre-plan-gate pipeline was judged out of scope and is tracked as
`TODO-4.5-GENERIC-CSV-STRICT-VALIDATION`. Gate 4 uses `Parse` **without** the CanonicalText
fallback — a freshly-staged package-driven row must carry its own real typed field, not merely
happen to share a value with whatever column CanonicalText was inferred from. A row that fails is
rejected via the existing `RejectRow` gate-rejection path (same mechanism Gates 1–3 already use) —
never silently staged as a malformed candidate.

**Approval (`AdminResourceCandidateApproveHandler`):** parses+validates the candidate's typed
content (with the CanonicalText fallback) before calling `Approve`; throws
`CandidateContentValidationException` (carrying structured `CandidateFieldError[]`) if invalid.

**Publication (`ResourceCandidatePublishService`):** parses+validates again (defense-in-depth,
never trusts a stale approval), independent of every other live-re-checked gate (provenance,
English-only, source approval/license, ValidationStatus, ReviewStatus).

## 5. Candidate editing and review

**API:** `UpdateResourceCandidateContentCommand` gained `TypedContentJson` — a JSON object shaped
exactly like the candidate's typed schema. The handler parses+validates it strictly (no
CanonicalText fallback) and, on success, persists the **canonical** serialized form (not the
admin's raw input verbatim) via `_contentSerializer.Serialize`. On failure, nothing is persisted —
`CandidateContentValidationException` propagates to a `400` with a structured `fieldErrors` array.
The legacy raw `NormalizedJson` parameter still exists for the two untyped candidate types.

**Angular:** the Candidate Review edit modal (`admin-import-run-candidates`) now renders a
type-specific form per candidate type — Word/Definition/PartOfSpeech/Examples for Vocabulary,
Title/Explanation/Examples/CommonMistakes for Grammar, Title/PassageText/TextType/ReferenceSource
for Reading, Title/Transcript for Listening, Title/PromptText/Instructions/Context/SuggestedDuration
for Speaking, Title/PromptText/Instructions/Genre/SuggestedMinWords/ExpectedLevel for Writing —
instead of a raw JSON textarea, which is now the fallback **only** for `ActivityTemplateCandidate`/
`Unknown`. Field-level server errors render inline next to the offending field
(`sp-admin-form-field`'s `error` input). Both the inline "Approve & Publish" row button and the
dropdown row action are gated on `!hasContentErrors(item)` in addition to the existing
`canAttemptPublish` (ValidationStatus-derived) gate — an admin can never even attempt approval on a
row the typed schema has already flagged. A published candidate has no `edit` row action at all
(pre-existing behavior, unaffected) — read-only by omission.

## 6. Media provenance

New `ImportAssetProvenanceGuard.EnsureAssetBelongsToPackage` — enforced at the one call site that
creates `ImportCandidateAssetLink` rows today (`ImportPackageProcessingService`'s Listening-candidate
creation). Always true by construction on that path (both the audio and transcript assets come from
the same package's own asset list), but is now an explicit, independently-tested invariant rather
than an implicit one — closes the "candidate cannot reference an asset from another package"
requirement for the one real code path that creates these links. No UI exists yet to let an admin
*attempt* a cross-package reference (tracked as `TODO-4.5-ZIP-CROSS-PACKAGE-UI`) — building that was
out of scope ("Do not build ... new upload infrastructure").

## 7. Data migration

**No migration.** Deliberately the smallest safe design of the options considered (typed payload
columns / schema-version field / migrate existing NormalizedJson / retain NormalizedJson as the
typed document) — see section 3. No live DB touched; nothing to report as migrated or not migrated.

## Critical tests

| # | Requirement | Test(s) |
|---|---|---|
| 1 | Each type validates valid payloads | `ResourceCandidateContentSerializerTests.Each_supported_candidate_type_validates_a_valid_canonical_payload` (×6 via `[Theory]`) |
| 2 | Missing required fields fail with structured errors | `Vocabulary_missing_word_fails_validation_with_a_structured_error`, `Grammar_missing_title_...`, `Reading_missing_passage_text_...`, `Speaking_missing_prompt_text_and_title_...`, `Writing_missing_prompt_text_...` |
| 3 | Unknown fields/types fail safely | `ActivityTemplateCandidate_has_no_typed_schema_and_parse_fails_safely`, `Unknown_candidate_type_has_no_typed_schema_and_parse_fails_safely`, `Empty_json_object_fails_to_parse_safely_...`, `Malformed_json_fails_to_parse_safely_rather_than_throwing`, `Validate_rejects_a_content_type_that_does_not_match_the_declared_candidate_type` |
| 4 | Approved-plan routing creates the selected typed candidate | `Package_driven_CSV_row_maps_into_typed_candidate_fields`, `Package_driven_JSON_row_maps_into_typed_listening_candidate_fields` |
| 5 | CSV mappings populate typed fields | `Package_driven_CSV_row_maps_into_typed_candidate_fields` |
| 6 | JSON mappings populate typed fields | `Package_driven_JSON_row_maps_into_typed_listening_candidate_fields` |
| 7 | Candidate editing round-trips without losing fields | `Typed_content_edit_round_trips_without_losing_fields`, `ResourceCandidateContentSerializerTests.Serialize_round_trips_through_Parse_without_losing_fields` |
| 8 | Invalid candidate cannot be approved | `Invalid_candidate_content_cannot_be_approved`, `Typed_content_edit_with_a_missing_required_field_is_rejected_and_never_persisted` |
| 9 | Invalid candidate cannot be published | `Candidate_with_unparseable_typed_content_cannot_be_published` |
| 10 | Valid typed candidate publishes to the correct Resource Bank entity | `Valid_typed_vocabulary_candidate_approves_and_publishes_to_the_Resource_Bank` (integration, real HTTP + real bank row) + all 29 pre-existing `ResourceCandidatePublishServiceTests` (now routed through the typed serializer) |
| 11 | Listening candidate preserves audio asset + transcript provenance | `Approved_audio_without_transcript_uses_STT_and_creates_a_Listening_candidate` (extended with `ImportCandidateAssetLink` assertions) |
| 12 | Candidate cannot reference an asset from another package | `ImportAssetProvenanceGuardTests` (×2) |
| 13 | Existing package/plan provenance remains enforced | Pre-existing `ResourceCandidatePublishServiceTests` provenance-gate tests, unchanged and passing |
| 14 | Existing plan-driven execution tests still pass | Full unit/integration suite, unchanged baseline tests all passing |
| 15 | Existing STT/AI accounting and cost-ceiling tests still pass | Full unit/integration suite, unchanged baseline tests all passing |
| 16 | Direct Resource-to-Exercise generation remains absent | Confirmed via audit (section 1) — no code path exists; not re-asserted with a new test since there is nothing to assert against (an absence proof, not a behavior) |

## Tests

| Suite | Count | Result |
|---|---|---|
| Backend unit | 2,398 | Pass (+31 over the 2,367 Phase 4.4E baseline) |
| Backend integration | 1,325 | Pass (+3 over the 1,322 baseline) |
| Backend architecture | 26 | Pass (unchanged) |
| Angular `tsc --noEmit` | — | Zero new errors (pre-existing baseline errors — `feedbackPolicy`/`moduleSuggestions` — unchanged) |
| Angular production build | — | Succeeds |
| Angular unit (Karma) | — | **Blocked** — same pre-existing baseline TypeScript compile errors as every prior phase this session; `admin-import-run-candidates.component.spec.ts` (16 new specs) compiles cleanly under `tsc --noEmit` but cannot execute until that unrelated baseline is fixed |
| Playwright | 67 total (63 passed, 4 skipped) | Full suite, `--workers=1`: 63/63 non-skipped pass, 4 pre-existing unrelated skips, 0 failures — includes the 2 new `candidate-review-typed-editing.spec.ts` tests |

**New test files:** `ResourceCandidateContentSerializerTests.cs` (21 tests), `ImportAssetProvenanceGuardTests.cs` (2 tests), `AdminResourceCandidateTypedContentEndpointTests.cs` (3 integration tests), `admin-import-run-candidates.component.spec.ts` (16 Angular unit tests), `candidate-review-typed-editing.spec.ts` (2 Playwright tests) — plus targeted additions to `ResourceCandidatePublishServiceTests.cs`, `ResourceCandidateReviewWorkflowTests.cs`, `ResourceImportServiceTests.cs`, `ImportPackageProcessingServiceTests.cs`.

## Known limitations / deferred

- `TODO-4.5-ZIP-CROSS-PACKAGE-UI` — no UI path exists yet for an admin to attempt linking an asset
  from a different package; the guard is real and tested, just not reachable through any current UI.
- `TODO-4.5-GENERIC-CSV-STRICT-VALIDATION` — the legacy ungated single-file import path was not
  tightened to Gate 4's strict validation; deliberately scoped out to avoid touching a large,
  separate pre-Phase-4 pipeline and its existing test suite.
- Angular unit tests for the new editor cannot execute under Karma in this environment — blocked by
  the same pre-existing, unrelated baseline TypeScript errors every phase this session has hit.
  Verified via `tsc --noEmit` instead.
- The Playwright flow mocks the backend at the network layer (matching this repo's existing
  Playwright convention — see `import-stt-operations.spec.ts`), not a live backend. The genuine
  end-to-end backend flow (real HTTP, real SQLite, real typed validation gate, real publish into a
  real `ResourceBankItem` row) is instead covered by `AdminResourceCandidateTypedContentEndpointTests.cs`,
  an integration test against the real API host.

## Verdict

The typed schema layer is real, centralized, and load-bearing at all three points the brief asked
for (create/approve/publish), with zero regressions across 3,749 backend tests. The raw-JSON editor
is gone as the primary experience for six of the eight candidate types, replaced by structured forms
with inline validation. No migration risk was taken — the design intentionally avoided a schema-version
column in favor of structural detection, which also means there is no live-DB action item at all.

## Next recommended action

Pick up `TODO-4.5-ZIP-CROSS-PACKAGE-UI` if/when an asset-selection UI is prioritized;
`TODO-4.5-GENERIC-CSV-STRICT-VALIDATION` is lower priority and only matters if the legacy ungated
import path is still actively used.

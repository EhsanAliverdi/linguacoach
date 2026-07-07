---
status: current
lastUpdated: 2026-07-07 10:30
owner: architecture
supersedes: (onboarding designer/renderer portions of prior OnboardingV2 model)
supersededBy:
---

# Form.io Onboarding + Placement Model

## Purpose

Replaces the hand-rolled onboarding question designer/renderer (`OnboardingFlowDefinition` /
`OnboardingStepDefinition` / `StudentOnboardingProgress`) with an admin-authored Form.io wizard,
and adds Form.io-based per-item rendering/authoring to the existing adaptive placement engine.
Form.io is used strictly as a frontend rendering/authoring library — no Form.io server, cloud
project, enterprise module, or submission API is used anywhere. All state, scoring, validation,
and lifecycle logic remain owned by the SpeakPath backend.

## Onboarding

**Architecture**: one admin-designed Form.io wizard template, submitted once by the student.

- `StudentFlowTemplate` (`LinguaCoach.Domain/Entities/StudentFlowTemplate.cs`) — named,
  versioned container. `FlowKind.Onboarding` is the only value used today; `FlowKind.Placement`
  is reserved but unused (placement stays on its own item-bank model, see below).
- `StudentFlowTemplateVersion` — one version's `FormIoSchemaJson` (student-safe, no correct
  answers) and `ScoringRulesJson` (backend-only, keyed by Form.io component `key` — currently
  used for the two CEFR quick-check questions' correct answers, e.g.
  `{"assessment_q1": {"correctAnswerKey": "b"}}`). Never merge these two documents — the schema
  validator (`IFormIoSchemaValidationService`) actively rejects any schema that contains an
  answer/scoring-shaped key.
- `StudentFlowSubmission` — one row per in-progress or completed submission
  (`Started` → `Submitted` → `Evaluated`), storing the raw Form.io `submission.data` as
  `SubmissionJson`.

**Component-key convention**: admin-authored components must use one of a fixed key set,
enumerated in `OnboardingAnswerMapping` (`LinguaCoach.Domain/Enums/OnboardingAnswerMapping.cs`,
kept from the prior model as the canonical list) — `preferred_name`, `support_language`,
`learning_goals`, `custom_learning_goal`, `focus_areas`, `custom_focus_area`,
`difficulty_preference`, `session_duration`, `career_context`, `professional_experience_level`,
`role_familiarity`, plus `assessment_q1`/`assessment_q2` for the CEFR quick-check. The backend
normalizer (`StudentOnboardingFlowService.ApplyToProfileAsync`) maps by these known keys — no
runtime registration of arbitrary field names.

**Endpoints**:
- Admin (`AdminOnboardingController`, `api/admin/onboarding`): `GET templates`, `GET
  templates/active`, `GET templates/{id}`, `POST templates`, `PUT templates/{id}/draft`, `POST
  templates/{id}/publish`, `POST templates/{id}/archive`.
- Student (`OnboardingController`, `api/onboarding`): `GET active` (published schema + any draft
  submission to prefill), `POST save-draft`, `POST submit` (validates required fields
  server-side, normalizes into `StudentProfile`, scores the quick-check questions, advances
  `StudentLifecycleStage` `OnboardingRequired`/`OnboardingInProgress` → `PlacementRequired`).

**Removed** (no production/UAT data existed to preserve, confirmed with product owner before
deletion): `OnboardingFlowDefinition`, `OnboardingCategoryDefinition`, `OnboardingStepDefinition`,
`StudentOnboardingProgress`, `StudentOnboardingResponse` and their handlers/seeder. The V1 legacy
onboarding flow (`OnboardingHandler.cs`, `IOnboardingHandler`/`IOnboardingStatusQuery`/
`IOnboardingExperienceHandler`) is unrelated and was left untouched.

## Placement

**Update (2026-07-07): placement is now natively Form.io-authored end to end.** The sections
below describe the current, completed state; see `docs/reviews/2026-07-07-placement-formio-migration-engineering-review.md`
for the migration record, and `docs/reviews/2026-07-07-placement-item-metadata-cleanup-and-pagination.md`
for the follow-up that removed `ItemType`/`Prompt` and added the routed admin editor + paged list.

**Architecture unchanged**: `PlacementAssessmentService.cs`'s adaptive engine (item selection,
per-skill CEFR confidence, completion, CEFR finalization, learning-plan regeneration) is
byte-for-byte untouched by this migration. Form.io is only ever asked to render one
already-selected item; it never drives selection or scoring.

**Native fields (no more legacy fallback)**:
- `PlacementItemDefinition` — `FormIoSchemaJson` (student-safe) and `ScoringRulesJson`
  (backend-only correct-answer/rubric data, keyed by Form.io component key) are now the *only*
  authoring representation, plus `ScoringRulesVersion` (bumped on every scoring-rule edit). The
  legacy flat fields (`CorrectAnswer`, `ReadingPassage`, `ListeningAudioScript`, `ContentJson`)
  and the entire `QuestionContent`/`QuestionEditorComponent` authoring path have been removed —
  all 72 seeded items and all admin-authored items use native Form.io schemas.
- **Update (2026-07-07): `ItemType`/`Prompt` also removed from `PlacementItemDefinition`.** Once
  the Form.io schema is the only thing a student ever sees, these two admin-typed fields were pure
  duplication of what the schema's first component already says — and could silently drift out of
  sync with it. `Skill`, `CefrLevel`, `ItemOrder`, and `IsEnabled` are the only remaining authored
  metadata; a question's display label/type for the admin list is now derived on read from
  `FormIoSchemaJson` (`PlacementItemSchemaLabel.ExtractLabel`/`ExtractComponentType` in
  `LinguaCoach.Infrastructure/Placement/`), never persisted separately. `PlacementItemBankSeeder`'s
  idempotency check changed accordingly: it now skips seeding a (skill, CEFR level) pair if *any*
  row already exists for it (schema-independent), rather than matching on `Prompt` text, so an
  admin's later edit to a seeded item's schema is never overwritten or duplicated on the next app
  restart. `PlacementAssessmentItem` (the per-assessment issued-item snapshot) is unaffected — it
  still carries its own `ItemType`/`Prompt` for historical audit/review screens, now populated at
  issuance time from the same schema-label helper instead of copied from the definition's
  (now-removed) fields.
- `PlacementAssessmentItem` — `SourceItemDefinitionId` (FK) plus an immutable snapshot taken at
  issuance: `FormIoSchemaJson`, `ScoringRulesJsonSnapshot`, `ScoringRulesVersionSnapshot`. This
  means editing an item definition later never changes how an already-issued, already-answered
  item is scored/audited. `SubmissionDataJson` (the raw Form.io `submission.data`) and
  `NormalizedAnswerJson` (per-component normalized values) replace the old single-string
  `Response`/`AnswerJson` fields.
- `QuestionContentToFormIoMapper` (the server-side fallback mapper) has been deleted — there is
  no fallback path anymore; every enabled item is required to carry a native `FormIoSchemaJson`.

**Scoring**: `PlacementScoringService.ScoreSubmission` reads the item's `ScoringRulesJsonSnapshot`
(a `ScoringRulesDocument` of per-component `ComponentScoringRule`s — kinds: `single_choice`,
`multiple_choice`, `text_exact`, `text_normalized`, plus a `requiresManualOrAiEvaluation`
placeholder for future AI-scored speaking/writing items) and scores **every component
independently**, not just the first — this closes the previous multi-question-group limitation.
`PlacementFormIoScoringValidator` rejects scoring rules that reference a component key absent
from the paired Form.io schema, at both create and update time.

**API**:
- `GET /student/placement/next` → `PlacementNextItemDto` carries `formIoSchema` only — no
  `content`/`readingPassage`/`correctAnswer` fields exist on this DTO anymore.
  `ScoringRulesJson` is never included in any student-facing DTO (enforced by an integration test
  that asserts on the raw JSON response body, not just the DTO shape).
- `POST /student/placement/respond` now takes the full Form.io submission:
  `{ assessmentId, itemId, submission: { data: { <componentKey>: <value>, ... } }, durationSeconds, skill }`
  — replacing the old single-string `response` field. The backend verifies the item belongs to
  the caller's active assessment before scoring.

Admin item-bank authoring (`AdminPlacementItemController`) requires `FormIoSchemaJson` and
`ScoringRulesJson` on every create/update — the admin UI's `FormioBuilderComponent` is the only
schema editor (the old `QuestionEditorComponent` was deleted, along with the shared
`QuestionContent`/`QuestionAnswer` domain model and its onboarding-era `Questions` namespace,
since nothing outside placement referenced them).

**Admin item-bank list/edit UI (2026-07-07)**: item editing is a dedicated routed page
(`/admin/placement-items/new` and `/admin/placement-items/{itemId}`,
`AdminPlacementItemEditorComponent`) rather than a slide-over drawer, mirroring the onboarding
template designer's own-route pattern — the Form.io builder gets a full-width canvas instead of
being squeezed into a panel. The list page (`AdminPlacementItemsComponent`) follows the
exercise-types admin page's styling: `sp-admin-filter-bar` for the skill filter, `sp-admin-table`
in `layout="first-column-fluid"` projection mode with a raw `<table>` (no extra `sp-admin-card`
shell), `sp-admin-table-actions` dropdown for row actions, and `sp-admin-table-footer` +
`sp-admin-pagination` for pagination.

`GET /api/admin/placement-items` is server-side paged and filterable
(`?page=1&pageSize=20&skill=grammar`), returning `{ items, totalCount, overallTotalCount,
enabledCount, skillCount }` — `totalCount` reflects the skill filter (drives the pager);
`overallTotalCount`/`enabledCount`/`skillCount` are always unfiltered global bank stats for the
KPI strip. `GET /api/admin/placement-items/{itemId}` was added alongside this (previously the
list-then-find-by-id was fine when the list returned everything; once it's paged the editor page
needs a direct lookup).

**Also removed in this migration** (confirmed dead, zero remaining callers): the separate legacy
`/api/placement/*` controller stack (`PlacementController`, `PlacementHandlers`, `PlacementAnswer`
entity, `AiPlacementEvaluator`/`FakePlacementEvaluator`, legacy `PlacementService`/`PlacementAudioService`)
— this predated the adaptive `/api/student/placement/*` path and had no frontend callers left.

## Schema validation (shared)

`IFormIoSchemaValidationService` / `FormIoSchemaValidationService`
(`LinguaCoach.Infrastructure/Onboarding/FormIoSchemaValidationService.cs`) is used by both
onboarding template drafts/publishes and placement item authoring:

- Approved component types only: `textfield`, `textarea`, `radio`, `select`, `selectboxes`,
  `checkbox`, `number`, `email`, `content`, `panel`, `columns`, `table`, `wizard`, `form`,
  `button` (Form.io auto-adds a submit button to every form).
- Rejects script/eval-style properties (`customConditional`, `calculateValue`,
  `customDefaultValue`, `validate.custom`) **only when they carry a non-empty/meaningful
  value** — Form.io's builder stamps every component with these keys defaulted to `""`, so a
  naive presence-check would reject every schema the builder produces.
- Rejects external `dataSrc` (only inline `dataSrc: "values"` allowed).
- Rejects any answer/scoring-shaped key (`correctAnswerKey`, `correctAnswer`, `score`, `rubric`,
  `scoringWeight`) appearing anywhere in a schema meant to be student-safe — these must live only
  in the separate `ScoringRulesJson` document.
- Recurses into nested containers (`panel`/`columns`/`table`/`wizard` pages).

## Frontend

`@formio/js` (MIT) only — not `@formio/angular`, to avoid pulling in `ngx-bootstrap`/Bootstrap
CSS alongside this project's Tailwind design system. Two thin standalone wrapper components:
`shared/formio/formio-renderer.component.ts` (wraps `Formio.createForm`) and
`shared/formio/formio-builder.component.ts` (wraps `Formio.builder`, with a best-effort
component-type restriction matching the backend allow-list). No `formio` project URL is ever
configured — schemas render fully client-side against a local object, so no submission or
schema ever reaches Form.io's own servers.

## Known limitations

- The admin Form.io builder's component-restriction is best-effort (Form.io's builder
  customization API), enforced authoritatively server-side by
  `FormIoSchemaValidationService` regardless of what the client-side builder allows through.
- Speaking/writing placement items that need AI evaluation are not yet scored automatically;
  `requiresManualOrAiEvaluation` scoring rules are supported as a placeholder but excluded from
  adaptive selection until an evaluator is wired up (same functional gap as before this
  migration — no new limitation introduced).

---
status: current
lastUpdated: 2026-07-07 15:00
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
  answers) and `ScoringRulesJson` (backend-only, keyed by Form.io component `key`). Never merge
  these two documents — the schema validator (`IFormIoSchemaValidationService`) actively rejects
  any schema that contains an answer/scoring-shaped key. **Update (2026-07-07): see "Quiz tab
  authoring" below** — `ScoringRulesJson` is `ScoringRulesDocument`-shaped (`{"components":
  {"<key>": {"kind": "single_choice", "correctAnswer": "b"}}}`), shared with placement's scoring
  shape; the pre-2026-07-07 flat `{"<key>": {"correctAnswerKey": "b"}}` shape is still read
  compatibly forever (never re-migrated) by `FormIoQuizAnnotationCodec.ParseScoringRules`.
  **Update (2026-07-07, later): the seeded default onboarding template no longer includes any
  scored questions.** It originally shipped with a 10-question "CEFR quick check"
  (`assessment_q1..assessment_q10`) that duplicated what the placement assessment already
  determines properly (adaptive, per-skill) — this was removed from `OnboardingTemplateSeeder`
  entirely, so `ScoringRulesJson`/`AuthoringSchemaJson` are `null` on the seeded version. The
  scoring machinery itself (`StudentOnboardingFlowService`'s submit handler, `ComponentAnswerScorer`)
  is unchanged and still generic — it scores whatever quiz-annotated components a template
  actually has, so an admin can still opt into adding scored questions to onboarding via the Quiz
  tab if they choose to; the seeded default simply doesn't ship with any.
- `StudentFlowSubmission` — one row per in-progress or completed submission
  (`Started` → `Submitted` → `Evaluated`), storing the raw Form.io `submission.data` as
  `SubmissionJson`.

**Component-key convention**: `StudentOnboardingFlowService.ApplyToProfileAsync` maps submitted
Form.io data onto `StudentProfile` fields by a fixed set of hardcoded component-key string
literals — `preferred_name`, `support_language`, `learning_goals`, `custom_learning_goal`,
`focus_areas`, `custom_focus_area`, `difficulty_preference`, `session_duration`, `career_context`,
`professional_experience_level`, `role_familiarity`. **This is a real fragility, not just a
convention**: there is no runtime registration, no admin-UI indicator, and no validation tying a
component's `key` in the Form.io builder to this list — if an admin renames or deletes one of
these keys while editing the template, that field silently stops populating the student's profile
on submit (no error, no warning). `OnboardingAnswerMapping`
(`LinguaCoach.Domain/Enums/OnboardingAnswerMapping.cs`) documents the same list as an enum but is
not itself enforced against the schema at save time.

**Endpoints**:
- Admin (`AdminOnboardingController`, `api/admin/onboarding`): `GET templates`, `GET
  templates/active`, `GET templates/{id}`, `POST templates`, `PUT templates/{id}/draft`, `POST
  templates/{id}/publish`, `POST templates/{id}/archive`.
- Student (`OnboardingController`, `api/onboarding`): `GET active` (published schema + any draft
  submission to prefill), `POST save-draft`, `POST submit` (validates required fields
  server-side, normalizes into `StudentProfile`, scores any quiz-annotated components present,
  advances `StudentLifecycleStage` `OnboardingRequired`/`OnboardingInProgress` →
  `PlacementRequired`).

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

**Reading passages and listening audio (2026-07-07 seeding fix)**: `PlacementItemBankSeeder`'s
internal `SeedItem` fixture now carries two optional fields beyond `Prompt`/`CorrectAnswer`:
- `Passage` — for reading items, rendered as a leading read-only Form.io `content` component
  (`key: "reading_passage"`) placed *before* the question component in `FormIoSchemaJson`, so the
  passage is always visible while answering rather than crammed into the question's own label.
  Fixes a real bug where the seeded B2 reading items' questions referenced "the passage"/"the
  text" (e.g. "The passage implies that the author...") with **no passage content ever
  authored** — those three items now carry real short passages.
- `ListeningScript` — the spoken transcript for listening items, carried *only* in the
  backend-only `ScoringRulesJson.listeningAudioScript` (synthesized to audio on demand by
  `AdaptivePlacementAudioService` / `GET /student/placement/audio/{assessmentId}/items/{itemId}/listening`).
  Previously the visible question label repeated the entire transcript verbatim (e.g. `"You hear:
  'Turn left at the traffic lights.' Where do you turn?"`), which meant the listening skill was
  answerable by reading alone — the audio was redundant. The seed data was rewritten so the
  visible label is only the question (`"Where do you turn?"`), and the transcript exists solely
  as audio.
- `PlacementAssessmentItem` rows are an immutable snapshot taken at assessment-start time — an
  in-progress assessment does not pick up a later item-bank content fix; a new assessment must be
  started (or the assessment/its items deleted in dev) to see updated seed content.

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

## Quiz tab authoring (2026-07-07)

See `docs/reviews/2026-07-07-formio-quiz-tab-and-builder-consolidation.md` for the full
implementation review (architecture decisions, testing, and the one recommended manual-QA
follow-up).

Both flows' admin editors previously required hand-typing a separate "Scoring rules (JSON)"
textarea alongside the Form.io builder. This is replaced by a per-component **"Quiz" tab** inside
the builder's own component-settings modal (radio/select/selectboxes/checkbox/textfield/textarea),
added via Form.io's per-instance `Formio.builder(el, schema, { editForm: {...} })` option
(`shared/formio/quiz-edit-tab.ts`, wired into `FormioBuilderComponent`'s shared `BUILDER_OPTIONS` —
identical for every consumer). The admin only ever sets Enable + a correct answer + optional
points; `quiz.rule.kind` is derived from the component's own type by
`finalizeQuizAnnotations` (`shared/formio/quiz-scoring-rule.model.ts`) immediately before save.

**Server-side authoritative split — the security-critical piece.** The admin's Angular code holds
exactly one schema at a time: the *annotated authoring schema*, with `component.quiz = { enabled,
rule }` embedded per component. On save, the client sends this ONE schema as `authoringSchemaJson`
— it never independently constructs a "clean" version itself. The backend
(`IFormIoQuizSchemaSplitter` / `FormIoQuizSchemaSplitter`, `LinguaCoach.Application.FormIo` /
`LinguaCoach.Infrastructure.FormIo`) is the sole authority that:
1. Extracts each enabled component's `quiz.rule` into a `ScoringRulesDocument` (backend-only).
2. **Unconditionally deletes the `quiz` property** from every component it walks, regardless of
   its `enabled` value or shape (deletion by key, not by round-tripping through a typed DTO, so a
   malformed/unexpected `quiz` node can never survive by omission).
3. Runs the result back through the *unchanged* `IFormIoSchemaValidationService`/
   `PlacementFormIoScoringValidator` as an independent second gate.

As defense in depth, `FormIoSchemaValidationService.ForbiddenAnswerLeakKeys` also now includes
`"quiz"` itself — a raw `quiz` key surviving on a schema being validated as student-facing is
rejected outright, in case the splitter itself ever has a bug (it wouldn't otherwise be caught,
since the validator's existing leak-key scan only checks direct component properties and a
`properties` bag, not arbitrary nested objects like `component.quiz.rule`).

`AddPlacementItemCommand`/`UpdatePlacementItemCommand`/`SaveOnboardingTemplateDraftCommand` all
gained an optional `AuthoringSchemaJson` field — when present, the three save handlers call the
splitter and use its output instead of the command's `FormIoSchemaJson`/`ScoringRulesJson` (which
become ignored placeholders in that case). `PlacementItemDefinition` and
`StudentFlowTemplateVersion` each gained a nullable `AuthoringSchemaJson` column (admin-only, never
returned from a student-facing endpoint) so the annotated schema round-trips for re-editing.
**No backfill**: existing items/versions keep `AuthoringSchemaJson` null and continue scoring via
their existing `ScoringRulesJson` exactly as before, until an admin re-saves through the new Quiz
tab UI — the editor shows a banner ("this item's answers predate the Quiz tab") in that state.

**Shared scoring shape**: onboarding's scoring parser was upgraded from a bespoke
`{correctAnswerKey}`-only shape onto placement's richer `ComponentScoringRule` (`Kind`/
`CorrectAnswer(s)`/`Points`/...), with the actual comparison logic
(`single_choice`/`multiple_choice`/`text_exact`/`text_normalized`) extracted into one shared
`ComponentAnswerScorer` (`LinguaCoach.Application.FormIo`) used by both `PlacementScoringService`
and `StudentOnboardingFlowService`.

**Builder consolidation**: the single-page/multi-step-wizard toggle (previously bespoke code in
`AdminOnboardingEditorComponent`) moved into `FormioBuilderComponent` itself as an opt-in
`showDisplayModeToggle` input — onboarding passes `true`, placement doesn't pass it at all (no
toggle appears, since placement items are always single-schema). This is the concrete mechanism
for "one shared builder component, features individually enabled per consumer."

Regression coverage: `FormIoQuizSchemaSplitterTests` (unit — the leak-invariant centerpiece, walks
the entire output JSON tree asserting no `quiz` key survives, for arbitrarily nested/malformed
input) and `QuizAnnotationLeakRegressionTests` (integration — real HTTP admin-save + student-read
endpoints for both flows, asserting the raw response body never contains `quiz`/`correctAnswer`).

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
- Rejects any answer/scoring-shaped key (`correctAnswerKey`, `correctAnswer`, `correctAnswers`,
  `score`, `rubric`, `scoringWeight`, `quiz`) appearing anywhere in a schema meant to be
  student-safe — these must live only in the separate `ScoringRulesJson` document.
- Recurses into nested containers (`panel`/`columns`/`table`/`wizard` pages).

## Frontend

`@formio/js` (MIT) only — not `@formio/angular`, to avoid pulling in `ngx-bootstrap`/Bootstrap
CSS alongside this project's Tailwind design system. Two thin standalone wrapper components:
`shared/formio/formio-renderer.component.ts` (wraps `Formio.createForm`) and
`shared/formio/formio-builder.component.ts` (wraps `Formio.builder`, one shared instance/options
object used identically by both onboarding and placement — see "Quiz tab authoring" above for the
`editForm`/`showDisplayModeToggle` extensions). No `formio` project URL is ever configured —
schemas render fully client-side against a local object, so no submission or schema ever reaches
Form.io's own servers.

**Palette restriction (2026-07-07 correction)**: the builder hides Form.io's "Premium" palette
group (`builder: { premium: false }` in `BUILDER_OPTIONS`) — none of its components are in the
backend allow-list, and they require a licensed Form.io project this app never configures.
Basic/Advanced/Layout/Data stay fully shown; this is a narrower, verified-safe restriction, not a
1:1 mirror of the backend allow-list (an earlier version of this doc claimed a "best-effort"
full restriction that was never actually implemented in code — corrected here). The backend
validator remains the real security boundary regardless.

## Known limitations

- The admin Form.io builder's palette restriction only hides the "Premium" group (see "Frontend"
  above) — it is not a 1:1 mirror of the backend's allow-list, which remains the actual
  enforcement point (`FormIoSchemaValidationService`) regardless of what the client-side builder
  allows through.
- Speaking/writing placement items that need AI evaluation are not yet scored automatically;
  `requiresManualOrAiEvaluation` scoring rules are supported as a placeholder but excluded from
  adaptive selection until an evaluator is wired up (same functional gap as before this
  migration — no new limitation introduced).

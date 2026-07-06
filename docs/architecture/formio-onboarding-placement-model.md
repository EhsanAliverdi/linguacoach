---
status: current
lastUpdated: 2026-07-06 00:00
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

**Architecture unchanged**: `PlacementAssessmentService.cs`'s adaptive engine (item selection,
per-skill CEFR confidence, completion, CEFR finalization, learning-plan regeneration) is
untouched. Form.io is only ever asked to render one already-selected item; it never drives
selection or scoring.

**Additive fields**:
- `PlacementItemDefinition` gained `FormIoSchemaJson` (student-safe) and `ScoringRulesJson`
  (backend-only correct-answer/rubric data). Existing flat fields (`Prompt`, `CorrectAnswer`,
  `ContentJson`, etc.) are unchanged and remain the item's uniqueness/dedup identity and legacy
  authoring path — both authoring styles coexist.
- `PlacementAssessmentItem` gained `SourceItemDefinitionId` (FK to `PlacementItemDefinition`),
  used by the adaptive engine's dedup check (`PlacementAssessmentService.IsUsed`) in place of
  fragile `Prompt`-string matching, with a fallback to `Prompt` matching for items issued before
  this field existed.
- A server-side fallback mapper (`QuestionContentToFormIoMapper`,
  `LinguaCoach.Infrastructure/Placement/`) converts the redacted `QuestionContent` to a minimal
  Form.io schema for any item that hasn't been re-authored with a native `FormIoSchemaJson` yet
  — this always runs against already-redacted content
  (`QuestionContentRedactor.RedactCorrectAnswers`), so it cannot leak an answer.

**API**: `GET /student/placement/next` now returns a `formIoSchema` field alongside the existing
`content` field (never a correct answer). `POST /student/placement/respond` is unchanged — the
frontend extracts a single response value from the Form.io submission before posting (a
deliberate risk-reduction choice: the battle-tested adaptive scoring/selection code path was not
touched to accommodate structured multi-question Form.io payloads; this is a known limitation
for multi-question group items, same as the pre-existing `buildLegacyResponse()` gap).

Admin item-bank authoring (`AdminPlacementItemController`) now optionally accepts
`FormIoSchemaJson`/`ScoringRulesJson` alongside the existing required `Content` field — additive,
not a replacement of the existing `QuestionEditorComponent`-based authoring flow.

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

- Placement's multi-question group items (listening/reading groups) still collapse to a single
  response value on submit, same pre-existing limitation as before this migration.
- The admin Form.io builder's component-restriction is best-effort (Form.io's builder
  customization API), enforced authoritatively server-side by
  `FormIoSchemaValidationService` regardless of what the client-side builder allows through.

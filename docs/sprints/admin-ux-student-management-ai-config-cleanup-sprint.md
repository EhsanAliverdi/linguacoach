---
status: current
lastUpdated: 2026-06-09 13:56
owner: engineering
supersedes:
supersededBy:
---

# Admin UX, Student Management & AI Config Cleanup Sprint

## Status

Implemented and under verification.

## Product Goal

Make the admin area credible and practical before the next large guided-learning sprint:

- Admin pages use the available screen width.
- Student management supports create, edit, and archive.
- Create student is part of the Students flow, not a permanent sidebar item.
- Curriculum is no longer surfaced as a vague admin feature.
- AI feature routing is complete and includes primary and fallback provider/model controls.

## Architecture Decisions

- Student archive is a soft lifecycle operation using `StudentLifecycleStage.Archived`; it does not hard-delete students, attempts, audio, or learning data.
- Archived students are hidden from the default admin Students list and can be included with an explicit archived filter.
- Archive also marks the Identity user as inactive (`EmailConfirmed = false`) so archived students cannot sign in.
- Student edit updates existing `StudentProfile` fields only. It does not expose password reset and does not mutate language pair or lifecycle stage from the edit form.
- Create student route remains `/admin/create-student`, but the sidebar no longer links to it. The Students page owns the "Create student" action.
- Curriculum remains in code/API/data because it may become a future seed/fallback content source, but it is hidden from admin navigation and dashboard actions for now.
- AI provider configs are ensured for active prompt/runtime feature keys when admin AI config is listed. This keeps routing visible for placement, memory, adaptive path, listening, speaking, writing, and vocabulary extraction.

## In Scope

- Admin content width and responsive grid cleanup.
- Students list action button for Create student.
- Reusable toast service/component.
- Create student success navigation back to Students with a success toast.
- Student profile edit for first name, last name, display name, work context, learning goals, difficult situations, session duration, professional experience, and role familiarity.
- Student archive operation.
- AI config fallback provider/model/enabled fields in API and UI.
- AI config feature labels with raw feature keys retained as technical text.
- Tests and documentation updates.

## Out of Scope

- LearningSession.
- Today page.
- TeamsChatSimulation.
- MinIO.
- Pronunciation.
- Call Mode.
- Password reset flow.
- Hard-delete student data.
- Admin lifecycle reset tools.
- Reworking Curriculum into a new product concept.

## API Changes

- `GET /api/admin/students?includeArchived=true|false`
  - Defaults to active students only.
  - Returns `studentProfileId`, lifecycle stage, and editable profile fields.
- `PUT /api/admin/students/{studentProfileId}`
  - Updates admin-editable student profile fields.
- `POST /api/admin/students/{studentProfileId}/archive`
  - Sets lifecycle stage to `Archived` and disables login.
- `GET /api/admin/ai-config`
  - Ensures rows exist for active runtime AI feature keys.
  - Returns fallback provider/model/enabled fields.
- `PUT /api/admin/ai-config/{configId}`
  - Supports primary and fallback provider/model updates.

## DB Changes

No migration required. The sprint uses existing `StudentProfile` lifecycle/profile fields and existing `AiProviderConfig` fallback columns.

## Frontend Changes

- `.sp-admin-content` now fills the admin main area instead of capping page content at 1200px.
- Admin sidebar removed Create student and Curriculum entries.
- Admin dashboard removed Curriculum quick action and marks implemented Speaking/Listening AI-related activity status as active.
- Students page now has create, edit, archive, active/archived filtering, and lifecycle badges.
- Create student page navigates to Students after success and shows a toast.
- AI Config feature routing rows show human labels, raw keys, primary provider/model, fallback provider/model, and fallback enabled state.

## Curriculum Review

Current Curriculum area:

- Lists career profiles.
- Lets admins manage vocabulary words under a career/language pair.
- Is not part of the current activity flow, placement assessment, or guided session architecture.

Decision:

- Hide Curriculum from admin navigation/dashboard for now.
- Keep the route/API/data intact to avoid destructive churn.
- Treat future curriculum work as a defined seed/fallback content source only if the LearningSession / ExercisePattern engine needs curated content. It should not remain a vague static content editor that competes with AI-generated activities.

## AI Feature Keys Covered

- `writing.exercise`
- `learning_path_generate`
- `learning_path_generate_adaptive`
- `activity_generate_writing`
- `activity_evaluate_writing`
- `activity_generate_listening`
- `activity_generate_speaking_roleplay`
- `activity_evaluate_speaking_roleplay`
- `vocabulary_extract_from_attempt`
- `student_memory_update`
- `placement_assessment_evaluate`

## Test Plan

- `dotnet test LinguaCoach.slnx`
- `npm run build`
- `npx playwright test`

Focused coverage:

- Admin can update a student profile.
- Admin can archive a student.
- Archived students are hidden from default list and cannot log in.
- Non-admin users cannot access update/archive endpoints.
- AI config lists active runtime feature keys and fallback fields.
- AI config accepts valid fallback provider/model and rejects invalid fallback combinations.
- Sidebar no longer shows Create student.
- Students page shows Create student action.
- Create student success returns to Students and shows a toast.
- AI Config shows placement/speaking feature labels and fallback controls.

## Risks

- Create student no longer leaves the admin on the credentials confirmation panel. Admins must retain/copy the temporary password before submitting.
- Curriculum is hidden, not deleted. Future agents should decide whether to redefine or remove it when the guided course engine is implemented.
- AI config auto-creates missing feature config rows using the default OpenAI `gpt-4o-mini` model. Admins should review provider routing before production use.

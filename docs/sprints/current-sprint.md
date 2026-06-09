---
status: current
lastUpdated: 2026-06-09 13:56
owner: product
supersedes:
supersededBy:
---

# Current Sprint â€” SpeakPath

Last updated: 2026-06-09

---

## Current priority

**Admin UX, Student Management & AI Config Cleanup is complete** (see `docs/sprints/admin-ux-student-management-ai-config-cleanup-sprint.md`). Next priority: **Guided Course (LearningSession / Today page)**.

---

Documentation governance cleanup completed on 2026-06-09: every code change now requires a documentation impact review, final reports must include documentation impact details, and selected source-of-truth docs carry freshness metadata.

---

## Current state

All four activity types (WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay) are implemented and verified end-to-end.

Onboarding now hands students to placement directly. The dashboard is lifecycle-aware: placement-required students see a placement CTA, course-ready students can see their starting-level summary, and Practice Gym is secondary on-demand practice.

Admin cleanup is implemented: admin pages use the available width, Create student lives under Students, admins can edit/archive students, Curriculum is hidden from admin navigation pending redefinition, and AI Config includes primary/fallback routing for all active runtime AI feature keys.

The architecture redesign sprint (course-session-placement-redesign-sprint) defined the new learning model and placement assessment model. No large code changes were made in that sprint â€” it was a planning/architecture sprint.

The next phase is structural: placement â†’ guided sessions â†’ exercise pattern engine.

---

## In scope (next implementation sprint)

- `PlacementAssessment` entity and 6-section structured assessment flow
- `PlacementResult` as the source of truth for CEFR level and per-skill levels
- Self-reported level from onboarding is temporary only â€” `PlacementResult` supersedes it
- First `LearningPath` and `LearningSession` generated after placement completes
- Today page showing the student's current session
- Ordered `SessionExercise` steps within a session
- Session reflection / AI summary after session completes
- Next session scheduling

---

## Out of scope (do not build in this sprint)

- More isolated activity types
- Call Mode / Pronunciation
- Real STT provider
- Admin CRUD for career profiles
- Email delivery
- Payments, organisations, multi-tenancy

---

## Architecture docs for this sprint

- `docs/architecture/placement-assessment-model.md`
- `docs/architecture/course-session-learning-model.md`
- `docs/architecture/exercise-pattern-library.md`

---

## Key rule

Do not add more isolated activity types. Build the course structure that organises existing ones.

When unsure, choose the option that makes SpeakPath feel more like a structured English class, not a card-based practice tool.

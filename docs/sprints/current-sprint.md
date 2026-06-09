# Current Sprint — SpeakPath

Last updated: 2026-06-09

---

## Current priority

**Placement Assessment MVP**, then **Guided Course (LearningSession / Today page)**.

---

## Current state

All four activity types (WritingScenario, ListeningComprehension, VocabularyPractice, SpeakingRolePlay) are implemented and verified end-to-end.

The architecture redesign sprint (course-session-placement-redesign-sprint) defined the new learning model and placement assessment model. No large code changes were made in that sprint — it was a planning/architecture sprint.

The next phase is structural: placement → guided sessions → exercise pattern engine.

---

## In scope (next implementation sprint)

- `PlacementAssessment` entity and 6-section structured assessment flow
- `PlacementResult` as the source of truth for CEFR level and per-skill levels
- Self-reported level from onboarding is temporary only — `PlacementResult` supersedes it
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

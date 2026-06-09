---
status: current
lastUpdated: 2026-06-09 13:11
owner: product
supersedes:
supersededBy:
---

# SpeakPath — Current Product State

Last updated: 2026-06-09

---

## What is built and verified

The following end-to-end flow is implemented and verified:

```
Admin logs in
→ Admin creates student (temp password shown once)
→ Student logs in
→ Student changes temporary password (enforced server-side)
→ Student completes onboarding (language pair, career profile, experience level)
→ Student reaches dashboard
→ Student starts an activity (Writing / Listening / Vocabulary / Speaking)
→ Student submits draft or recording
→ Student sees structured AI feedback
→ Student retries or continues to next activity
→ Student can revisit learning history
```

## Implemented activity types

| Type | Status |
|---|---|
| `WritingScenario` | ✅ implemented |
| `ListeningComprehension` | ✅ implemented (with TTS audio) |
| `VocabularyPractice` | ✅ implemented |
| `SpeakingRolePlay` | ✅ implemented (MVP — fake STT) |

All four activity types use the unified `/activity` path.
`/api/writing/*` endpoints have been removed. See `docs/decisions/activity-flow-migration.md`.

## Test suite baseline (as of course-session-placement-redesign-sprint)

```
dotnet test:     437 passed
npm run build:   passed
Playwright:      56 passed
```

## Admin capabilities

- Create students with temporary passwords
- Configure AI providers, model assignments, and prompt templates via Admin UI
- AI provider credentials stored securely in DB (never returned to client)
- AI usage logs accessible

## Known gaps / not yet built

- No placement assessment (the main next priority)
- No session-based lesson structure (`LearningSession` / `SessionExercise`)
- No Today page / guided course flow
- No real STT provider (SpeakingRolePlay uses `FakeSpeechToTextService`)
- No email delivery for temp passwords (admin copies manually)
- No admin CRUD for career profiles / learning tracks (seed data only)
- No audio cleanup job (50-file soft ceiling in place as mitigation)

See `docs/backlog/deferred-work.md` for the full deferred work list.

## Next recommended work

1. **Placement Assessment MVP** — 6-section structured assessment, `PlacementAssessment` entity, `PlacementResult` as CEFR source of truth
2. **Guided Course / LearningSession** — `LearningSession` → `SessionExercise` layer, Today page, ordered session exercises, session reflection

See `docs/sprints/current-sprint.md` for the active sprint scope.

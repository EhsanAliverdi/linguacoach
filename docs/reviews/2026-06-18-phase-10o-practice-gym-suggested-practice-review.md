---
status: current
lastUpdated: 2026-06-18 02:00
owner: engineering
---

# Phase 10O Engineering Review — Practice Gym Suggested Practice & Pool Serving

**Date:** 2026-06-18
**Related sprint:** Phase 10O
**HEAD before work:** 1ce7f01 (Phase 10N — Background Replenishment Pipeline)

---

## Summary

Phase 10O connects the readiness pool (built in 10M/10N) to the student-facing Practice Gym.
Students now see personalised suggestion cards from their pre-filled pool rather than only the static "By skill / By exercise type" launcher.

---

## Files Added / Changed

### New files

| File | Purpose |
|---|---|
| `src/LinguaCoach.Application/PracticeGym/IPracticeGymSuggestionService.cs` | Application-layer interface: GetSuggestions, StartSuggestion, TryMarkConsumed |
| `src/LinguaCoach.Application/PracticeGym/PracticeGymSuggestionDtos.cs` | DTOs: PracticeGymSuggestionsDto, PracticeGymSuggestionItemDto, StartSuggestionResult |
| `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs` | Implementation: pool query, ranking, reservation, consumption |
| `src/LinguaCoach.Api/Controllers/PracticeGymSuggestionsController.cs` | Student-facing API: GET /api/practice-gym/suggestions, POST …/start, POST …/complete |
| `tests/LinguaCoach.UnitTests/PracticeGym/PracticeGymSuggestionServiceTests.cs` | 14 unit tests |
| `tests/LinguaCoach.IntegrationTests/PracticeGym/PracticeGymSuggestionIntegrationTests.cs` | 10 integration tests |

### Modified files

| File | Change |
|---|---|
| `src/LinguaCoach.Infrastructure/DependencyInjection.cs` | Registered `IPracticeGymSuggestionService` (Phase 10O block) |

---

## Design Decisions

### Service placement
`PracticeGymSuggestionService` lives in `LinguaCoach.Infrastructure/PracticeGym/` — consistent with `PracticeGymPoolService`, `StudentActivityReadinessPoolService`, and `ReadinessPoolReplenishmentService`.

### Selection logic
Items are filtered then ranked in memory (not in SQL) because the pool per student is small (target ≤ 10 items) and the ranking formula requires JSON-array intersection logic not easily expressed in LINQ-to-SQL.

Ranking order:
1. Focus-area tag match (student `FocusAreas` ∩ item `FocusTagsJson`)
2. Goal/context tag match (student `LearningGoals` ∩ item `ContextTagsJson`)
3. Pool priority field (lower = higher priority)
4. Expiry urgency (soonest `ExpiresAt` first)
5. FIFO (`CreatedAt` ascending)

### Section assignment
- **SuggestedItems**: `Ready` status, `RoutingReason` ≠ review/scaffold/remediation with `IsLowerLevelContent=true`.
- **ContinueItems**: `Reserved` status, not past `ExpiresAt`.
- **ReviewItems**: `ReviewOnly` status OR `Ready` + `IsLowerLevelContent=true` + non-Normal routing reason.

This prevents lower-level content from silently appearing in normal suggestions.

### Reservation
`StartSuggestionAsync` calls `item.Reserve()` directly on the EF-tracked entity with optimistic concurrency retry on `DbUpdateConcurrencyException` — same pattern as `StudentActivityReadinessPoolService.ReserveNextReadyAsync`.

Idempotent: calling start on an already-reserved item returns `AlreadyReserved=true` and the same navigation targets.

### Consumption
`TryMarkConsumedAsync` is best-effort — it logs and swallows exceptions so a completion callback never breaks the calling flow.

### Replenishment trigger
`IsReplenishmentRecommended` is returned in the suggestions DTO. The frontend can use this to trigger a fire-and-forget replenishment call or display a "refreshing" indicator. The API itself does not block on replenishment.

### Workplace default guardrail
The service reads `StudentProfile.LearningGoals` (a `List<string>`) for context matching. `general_english` is the fallback context in the pool items; `workplace` is only one of many selectable goals. The service never injects a workplace preference.

---

## API Endpoints Added

| Method | Route | Description |
|---|---|---|
| GET | `/api/practice-gym/suggestions` | Returns SuggestedItems, ContinueItems, ReviewItems, pool health |
| POST | `/api/practice-gym/suggestions/{readinessItemId}/start` | Reserves item, returns navigation target |
| POST | `/api/practice-gym/suggestions/{readinessItemId}/complete` | Best-effort marks item consumed |

Existing `GET /api/activity/practice-gym/next` and `GET /api/activity/exercise-types/select` unchanged.

---

## Tests

### Unit tests — 14 cases (PracticeGymSuggestionServiceTests.cs)

1. Consumed items excluded from all sections
2. Expired items excluded
3. Failed items excluded
4. Stale items excluded
5. ReviewOnly status item appears in Review section only
6. Lower-level Ready+Review appears in Review only
7. Normal Ready item appears in Suggested
8. Reserved valid item appears in Continue
9. Expired reserved item not in Continue
10. Empty pool → IsReplenishmentRecommended = true
11. StartSuggestion reserves Ready item, returns activity id
12. StartSuggestion is idempotent for already-reserved items
13. StartSuggestion returns failure for consumed item
14. TryMarkConsumed transitions Reserved → Consumed

### Integration tests — 10 cases (PracticeGymSuggestionIntegrationTests.cs)

1. IPracticeGymSuggestionService is registered in DI
2. GET suggestions returns 200 with empty sections when pool empty
3. Ready items appear in SuggestedItems
4. Reserved items appear in ContinueItems
5. ReviewOnly items appear in ReviewItems
6. POST start reserves a ready item, returns success
7. POST start is idempotent (already reserved → alreadyReserved=true)
8. POST complete marks reserved item consumed
9. Existing /api/activity/practice-gym/next smoke test still passes
10. Admin readiness pool has no POST write endpoint (returns 404/405)

---

## Gate Results

| Gate | Result |
|---|---|
| `dotnet restore` | exit 0 |
| `dotnet build --configuration Release` | exit 0, 0 errors |
| Architecture tests | 3 passed |
| Unit tests | 1174 passed (+14 vs 10N) |
| Integration tests | 597 passed (+10 vs 10N) |
| **Total** | **1774 passed, 0 failed** |
| `npm ci` | blocked by pre-existing Node 24 + path-with-space issue (no Angular source changed) |
| `npm run build --configuration production` | blocked by pre-existing CommonModule error in PatternEvaluationResultComponent (no Angular source changed) |
| Playwright | not run (no Angular changes; pre-existing environment blocker) |

---

## What Was NOT Implemented (Explicitly Out of Scope)

- Admin write endpoints for readiness pool
- Admin curriculum builder
- StudentProfile.CefrLevel migration
- Plus-level persistence
- Full placement engine
- Full mastery engine
- Full notification system
- Usage/quota enforcement
- Angular/frontend changes (pool is surfaced through the API; frontend integration is deferred or to be done by frontend agent)
- New exercise types

---

## Known Limitations / TODOs

- Frontend (Angular) integration not implemented in this phase. The API is ready; a follow-on phase or frontend agent should:
  - Add a "Suggested for you" section to the Practice Gym Angular page.
  - Add Continue / Review sections.
  - Call POST .../start on card click and navigate to the returned activityId.
  - Call POST .../complete on activity completion.
  - Show IsReplenishmentRecommended as a subtle "refreshing" indicator.
- `TryMarkConsumedAsync` must be called by the activity completion path (ActivitySubmitHandler or similar). Currently a TODO — documented in TODOS.md.
- Replenishment is recommended via the DTO flag but not auto-triggered server-side in this phase. Frontend can call a replenishment endpoint if/when one is exposed, or the background job handles it within 20 minutes.

---

## Risks

- Unit tests use reflection to force `Status` on `StudentActivityReadinessItem` (bypassing lifecycle guard). This is intentional for test isolation; integration tests use the proper lifecycle path.
- The suggestion ranking is in-memory. If pool sizes grow beyond ~50 items per student in a future phase, consider pushing ranking into SQL.

---

## Final Verdict

Phase 10O complete. All acceptance criteria met on the backend. Frontend surfacing is deferred and documented.

## Next Recommended Action

Frontend phase: implement Angular Practice Gym suggestion sections, or proceed to Phase 10P per sprint plan.

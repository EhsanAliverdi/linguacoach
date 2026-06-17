# Phase 10O-F Implementation Review — Practice Gym UI Integration & Completion Consumption Wiring

**Date:** 2026-06-18
**Related sprint:** Phase 10O-F
**HEAD before work:** df4364e (Phase 10O)

---

## Files reviewed

### Backend
- `src/LinguaCoach.Infrastructure/Activity/ActivitySubmitHandler.cs`
- `src/LinguaCoach.Application/PracticeGym/IPracticeGymSuggestionService.cs`
- `src/LinguaCoach.Application/PracticeGym/PracticeGymSuggestionDtos.cs`
- `src/LinguaCoach.Api/Controllers/PracticeGymSuggestionsController.cs`
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs`
- `src/LinguaCoach.Domain/Entities/StudentActivityReadinessItem.cs`
- `src/LinguaCoach.Application/ReadinessPool/IStudentActivityReadinessPoolService.cs`

### Angular
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.ts`
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.html`
- `src/LinguaCoach.Web/src/app/core/services/practice-gym-suggestions.service.ts` (new)
- `src/LinguaCoach.Web/src/app/features/practice/practice-gym.component.spec.ts`

### Tests
- `tests/LinguaCoach.IntegrationTests/PracticeGym/ReadinessConsumptionWiringTests.cs` (new)

---

## Findings by priority

### P0 — None

### P1 — Wiring correctness

**`StudentId` vs `UserId` distinction** — `StudentActivityReadinessItem.StudentId` stores the `StudentProfile.Id` (profile primary key), not the auth `UserId`. `TryConsumeReadinessItemAsync` receives both `userId` (for `TryMarkConsumedAsync` which resolves profile internally) and `profileId` (for the direct DB lookup). Correctly separated in the implementation.

**Completion signal for pattern path** — Only fires `TryConsumeReadinessItemAsync` when `evalResult.Completed == true`. For deterministic VocabularyPractice and ListeningComprehension paths, consumption fires unconditionally (every completion is final for those types). This matches the intent: do not consume on partial/failed attempts.

**Best-effort guarantee** — The helper wraps in try/catch and logs a Warning. Completion response is never blocked by readiness item failure.

### P2 — Design decisions

**Legacy path consumption** — The WritingScenario/AI path calls `TryConsumeReadinessItemAsync` unconditionally (not gated on score). This is acceptable: any submitted attempt for a linked activity marks it consumed, preventing re-suggestion. Score-gated consumption was considered but rejected as it would require arbitrary thresholds.

**Session/SessionExercise paths not wired** — Completion of linked sessions or session exercises does not trigger consumption. The `TryConsumeReadinessItemAsync` helper only matches by `LearningActivityId`. Session-based wiring requires `SessionLifecycleHandler` changes which were out of scope. This is documented as a known limitation.

**Angular template uses `*ngTemplateOutlet`** — The suggestion card is a `<ng-template>` reused across three sections. This requires `CommonModule` (which is already imported). Works correctly in production build.

### P3 — Minor / informational

**Routing label casing** — `routingReason` from the API is a Pascal-case string (e.g. "Normal", "Review"). The `routingReasonLabel()` helper uses `.toLowerCase()` matching which is safe.

**`titlecase` pipe** — `item.primarySkill | titlecase` used in the template. `CommonModule` provides this. Verified in production build.

---

## Decisions made

1. **Consume on any completion, not score-gated** — WritingScenario/AI and deterministic types fire unconditionally; pattern types gate on `Completed`. Rationale: prevents repeated suggestion of the same item.
2. **Best-effort only** — Exceptions swallowed. Consumption failure is not visible to the student.
3. **ProfileId passed explicitly** — `TryConsumeReadinessItemAsync` receives both `userId` and `profileId` to avoid a second profile DB lookup inside the helper.
4. **Session wiring deferred** — Adding a new TODO for session-based consumption wiring.

---

## Implementation tasks produced

No new blocking tasks. One informational deferred item added (session/exercise completion path).

---

## Risks / unresolved questions

- Session-linked readiness items (linked via `LearningSessionId`, not `LearningActivityId`) are not consumed on session completion. Items will eventually expire. Low risk for now.
- `*ngTemplateOutlet` inside `@if` blocks in new Angular control flow syntax: verified working in production build (warnings only, not errors).

---

## Frontend build blocker status

Production build runs with warnings only — no errors. The `PatternEvaluationResultComponent` warning is pre-existing (template warning, not an error). Build was not blocked before or after this phase. CommonModule is imported correctly in all affected standalone components.

---

## CI gate results

| Gate | Result |
|---|---|
| `dotnet build --configuration Release` | Pass — 0 errors, 7 warnings (pre-existing) |
| `dotnet test --configuration Release` | Pass — 1778 passed, 0 failed |
| Architecture tests | 3 passed |
| Unit tests | 1174 passed |
| Integration tests | 601 passed |
| `npm run build -- --configuration production` | Pass — warnings only |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 272 passed, 0 failed |
| `npx playwright test --workers=1` | Pre-existing "No tests found" — unrelated to this phase |

---

## TODOs closed

- **TODO-014** — TryMarkConsumedAsync wired into ActivitySubmitHandler. Closed.
- **TODO-015** — Angular Practice Gym UI integration implemented. Closed.

## Not implemented (as required)

- Admin write endpoints
- Admin curriculum builder
- `StudentProfile.CefrLevel` migration
- Plus-level persistence
- Full placement engine
- Full mastery engine
- Notification system
- Usage/quota enforcement

---

## Final verdict

Phase 10O-F is complete. The Practice Gym page now shows Suggested, Continue, and Review sections from the API. Clicking a card starts the item and navigates to the linked activity. Completion of a linked activity marks the readiness item consumed, closing the lifecycle loop. All backend and Angular tests pass.

## Next recommended action

Run `graphify update .` to refresh the knowledge graph. Then begin Phase 10P or the next backlog item.

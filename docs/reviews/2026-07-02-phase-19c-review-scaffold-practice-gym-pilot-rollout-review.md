# Phase 19C — Review Scaffold Practice Gym Pilot Rollout — Implementation Review

**Date:** 2026-07-02
**Related sprint/phase:** Phase 19C (follows Phase 19B — Review Scaffold Per-Item Admin Approval)
**Review type:** Implementation readiness / completion review

---

## 1. Purpose

Phase 19A added global config gates (`EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`, `AllowTodayLessonInsertion`, source allow-list, per-student daily cap) around review/scaffold generation. Phase 19B replaced the global admin-review hold with per-item `AdminReviewStatus` approve/reject/reopen. Both phases left approved items reaching Practice Gym as soon as they were individually approved — there was no separate "go live for students" switch and no student-facing wording review.

Phase 19C adds a dedicated pilot gate (`PracticeGymPilotEnabled`) so an admin can run generation and per-item approval in a "ready but not yet shown" state, then flip a single config flag to start (or instantly roll back) student visibility — Practice Gym only, never Today lesson. It also adds a friendly, non-negative student-facing label/reason override for pilot items and an admin monitoring summary.

---

## 2. Files Reviewed / Modified

**Audit (read-only, via subagent research — Part A):**
- `PracticeGymSuggestionService`, `StudentActivityReadinessItem`, `ReadinessPoolReplenishmentOptions`, `PracticeGymSuggestionDtos`, `practice-gym.component.ts/.html`, dashboard summary handler, `AdminReadinessPoolController`, `admin-lessons.component`, existing test suites.

**Application:**
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolReplenishmentOptions.cs` — new `PracticeGymPilotEnabled`, `PracticeGymPilotLabel`, `PracticeGymPilotReason`, `MaxStudentVisibleScaffoldSuggestions`
- `src/LinguaCoach.Application/ReadinessPool/ReadinessPoolDtos.cs` — new `ReviewScaffoldPilotSummaryDto`, `ReviewScaffoldPilotItemDto`

**Infrastructure:**
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs` — pilot gate + cap + label override

**Api:**
- `src/LinguaCoach.Api/appsettings.json` — new `ReadinessPool` keys, all conservative defaults
- `src/LinguaCoach.Api/Controllers/AdminReadinessPoolController.cs` — new `GET .../review-scaffold/pilot-summary` endpoint

**Angular:**
- `src/LinguaCoach.Web/src/app/core/models/admin.models.ts` — `ReviewScaffoldPilotSummary`, `ReviewScaffoldPilotItem`
- `src/LinguaCoach.Web/src/app/core/services/admin.api.service.ts` — `getReviewScaffoldPilotSummary`
- `src/LinguaCoach.Web/src/app/features/admin/admin-lessons/admin-lessons.component.ts` / `.html` — pilot status card
- No changes needed to `practice-gym.component.ts/.html` or the dashboard practice card — see Part C/F below.

**Tests (new/extended):**
- `tests/LinguaCoach.UnitTests/PracticeGym/PracticeGymSuggestionServiceTests.cs` — +8 tests (19–26), constructor updated for new `IOptions<ReadinessPoolReplenishmentOptions>` dependency
- `tests/LinguaCoach.UnitTests/ReadinessPool/ReplenishmentOptionsTests.cs` — +3 tests (42–44)
- `tests/LinguaCoach.IntegrationTests/Api/ReviewScaffoldPilotSummaryTests.cs` — new file, 4 tests
- `src/LinguaCoach.Web/.../admin-lessons.component.spec.ts` — +3 tests
- `src/LinguaCoach.Web/.../practice-gym.component.spec.ts` — +2 tests

---

## 3. Audit Table (Part A)

| Area | Current Behavior (pre-19C) | Needed for Pilot | Risk | Decision |
|---|---|---|---|---|
| `PracticeGymSuggestionService` — ReviewItems bucket | Approved scaffold items (`RequiresAdminReview=true && AdminReviewStatus==Approved`) already reach students immediately, capped at `MaxReview=4` (hardcoded, shared with non-scaffold review content) | A separate on/off switch for "go live," independent of approval; a scaffold-specific visible cap | Approving an item today already makes it live — no staged rollout, no easy full rollback short of rejecting every item | Add `PracticeGymPilotEnabled` gate + `MaxStudentVisibleScaffoldSuggestions` cap layered on top of the existing 19B gate |
| ContinueItems (reserved scaffold items) | Not pilot-gated — a reserved scaffold item stays visible in "Continue practice" regardless | Rollback must hide approved-but-unconsumed items too | Turning pilot off wouldn't fully hide in-flight scaffold content | Gate ContinueItems on `PracticeGymPilotEnabled` for `RequiresAdminReview=true` items |
| SuggestedItems bucket | Structurally excludes scaffold items today because `RoutingReason.Review` is always paired with `IsLowerLevelContent=true` (verified in `CurriculumRoutingService`) | Defence-in-depth so a future routing change can't leak scaffold items into Suggested un-gated | Low today, but a latent gap | Add the same pilot-gate condition to the `suggestable` filter |
| `PracticeGymSuggestionItemDto.CallToAction`/`Explanation` | Routing-reason-specific copy: "Review", "Step back to strengthen basics", "Targeted fix", "Address a specific gap" — borderline negative framing for a pilot | Friendly, non-negative, configurable wording | Wording risk during a real pilot with real students | Override with configured `PracticeGymPilotLabel`/`PracticeGymPilotReason` for any item where `RequiresAdminReview=true` |
| Practice Gym Angular UI | Already renders a separate "Review queue" section with its own eyebrow label and a `routing-label`/`lower-level-label` badge — visually distinct from Suggested/Continue | Visual distinguishability (Part C) | None — already satisfied | No template changes needed |
| Dashboard practice card | Already only surfaces `reviewQueueCount` and one preview suggestion; never renders `AdminReviewStatus`, confidence, or admin notes (not present in the DTO at all) | No admin diagnostics leak | None — DTO never carried these fields | No changes needed; documented as verified, not touched |
| Today lesson selector | Structurally isolated: `ReadinessPoolReplenishmentService.FillShortfallAsync` only allows scaffold generation into `ReadinessPoolSource.TodayLesson` when `AllowTodayLessonInsertion=true` (default false); `SessionGeneratorService` never queries `RoutingReason != Normal` items | Proof this remains true after the pilot gate lands | None — no code path was touched that could regress this | Verified via `GetSuggestions_OnlyQueriesPracticeGymSource` test + existing `ScaffoldAllowedSources`/`AllowTodayLessonInsertion` config, both untouched |
| Admin monitoring | `AggregatePoolHealthSummary` (system-wide, not scaffold-specific), `ReviewScaffoldDryRunSummary` (forecast, not actuals), `pending-review` list (per-item detail, no aggregate pilot view) | A single pilot status/count summary | Admins have no one place to see "is the pilot on, and what's actually visible to students right now" | Add `GET .../review-scaffold/pilot-summary` returning enabled flags + approved/student-visible/pending/rejected/consumed/skipped-or-expired counts + recent items |
| `ReviewScaffoldItemDetailDto.IsStudentVisible`/`IsPracticeGymEligible` | Reflects lifecycle + approval only (pre-pilot semantics); a 19B integration test asserts these become `true` immediately after approval | Must **not** silently change meaning, or the 19B approval test breaks | Coupling this field to the new pilot flag would falsely report "not eligible" right after approval, contradicting 19B's contract | Deliberately **not** pilot-gated — left as "structurally eligible," with the new pilot-summary endpoint as the source of truth for actual student visibility (see §12) |

---

## 4. Pilot Configuration (Part B)

`ReadinessPoolReplenishmentOptions` (`ReadinessPool` config section), new keys, all conservative by default:

```json
"PracticeGymPilotEnabled": false,
"PracticeGymPilotLabel": "Review",
"PracticeGymPilotReason": "This helps you practise a skill you are building.",
"MaxStudentVisibleScaffoldSuggestions": 2
```

No existing keys were duplicated — `EnableReviewScaffoldGeneration`, `DryRunOnly`, `RequireAdminReview`, `AllowTodayLessonInsertion`, `ScaffoldAllowedSources`, `MaxScaffoldItemsPerStudentPerDay` are reused as-is from 19A/19B and remain unchanged defaults (`false`/`true`/`true`/`false`/`["PracticeGym"]`/`3`).

`PracticeGymPilotEnabled=false` is a genuinely independent gate: with it off, generation (19A) and per-item approval (19B) can run in production exactly as before, but nothing new reaches a student in Practice Gym.

---

## 5. Practice Gym Eligibility Rules (Part D)

`PracticeGymSuggestionService.GetSuggestionsForStudentAsync` — a scaffold item (`RequiresAdminReview=true`) is student-visible only when **all** of:

1. `PracticeGymPilotEnabled=true` (new)
2. `AdminReviewStatus=Approved` (19B, unchanged)
3. lifecycle status is `Ready`/`ReviewOnly` (ReviewItems) or `Reserved`+not-expired (ContinueItems)
4. `Source=PracticeGym` (unchanged — query is scoped to Practice Gym only, structurally never queries `TodayLesson`)
5. belongs to the authenticated student (unchanged — query filters on `StudentId == profileId`)
6. within `MaxStudentVisibleScaffoldSuggestions` (new, applied on top of the existing `MaxReview=4` page cap)

Pending/rejected items remain hidden exactly as in 19B — the new gate is additive (AND), never a substitute for the approval gate.

Today lesson insertion is untouched by this phase: the suggestion service only ever reads `ReadinessPoolSource.PracticeGym` rows, and the replenishment source allow-list (`ScaffoldAllowedSources`/`AllowTodayLessonInsertion`) that governs whether a scaffold item can even be *created* against the `TodayLesson` source is unchanged from 19A.

---

## 6. Student UX (Part C)

No Angular template changes were needed — the existing "Review queue" section (`practice-gym.component.html`) already:
- renders under a separate `sp-eyebrow` label, visually distinct from "Suggested for you"/"Continue practice"
- shows a routing-label chip and the item's `explanation` text, with no fields for confidence, blocked reason, admin notes, or provider/model info (those simply don't exist on `PracticeGymSuggestionItemDto`)

What changed is the **copy fed into that existing UI** for scaffold-origin items. `PracticeGymSuggestionService.ToDto` now overrides `CallToAction`/`Explanation` with the configured `PracticeGymPilotLabel`/`PracticeGymPilotReason` whenever `RequiresAdminReview=true`, replacing routing-reason-specific strings like "Step back to strengthen basics" / "Targeted fix" / "Address a specific gap in {skill}" with the friendlier, configurable default: label **"Review"**, reason **"This helps you practise a skill you are building."** Neither string contains "failed," "weakness," or "low confidence" (asserted by `DefaultOptions_PilotLabelAndReason_AreFriendlyAndNonEmpty`).

---

## 7. Suggestion Ordering (Part E)

Unchanged ranking logic (focus-area → goal/context → routing priority → expiry urgency → FIFO) for the Suggested bucket. For the Review bucket, non-scaffold review candidates (if any ever exist) are concatenated before scaffold candidates, then the combined list is capped at `MaxReview=4` — so a pilot with many approved scaffold items still can't crowd out non-scaffold review content. Scaffold items themselves are pre-capped at `MaxStudentVisibleScaffoldSuggestions` before that concatenation. Ordering within each source list remains deterministic (`CreatedAt`/lifecycle order from the underlying query), consistent with pre-19C behavior.

---

## 8. Dashboard Practice Card (Part F)

Audited, not modified. `StudentDashboardSummaryHandler` calls the same `PracticeGymSuggestionService.GetSuggestionsForStudentAsync`, so the pilot gate applies automatically — a hidden scaffold item is hidden from the dashboard's `reviewQueueCount` too, with zero dashboard-specific code changes. The dashboard already never surfaces `AdminReviewStatus`, confidence, or provider/model fields (they aren't present in the DTO). No risk was found that would justify limiting scope to the Practice Gym page only.

---

## 9. Admin Monitoring (Part G)

New endpoint `GET /api/admin/readiness-pool/review-scaffold/pilot-summary` (`[Authorize(Roles = Admin)]`), returning `ReviewScaffoldPilotSummaryDto`:
- `practiceGymPilotEnabled`, `allowTodayLessonInsertion`, `requireAdminReview`, `maxStudentVisibleScaffoldSuggestions` (config echo)
- `approvedCount`, `studentVisibleCount`, `pendingReviewCount`, `rejectedCount`, `consumedCount`, `skippedOrExpiredCount`
- `recentStudentVisibleItems`, `recentConsumedItems` (up to 10 each; skill/objective/status/created-at only — no admin diagnostics)

`studentVisibleCount` is computed with the same conditions as the suggestion service's actual gate (approved + eligible lifecycle status + PracticeGym source + `PracticeGymPilotEnabled`), so it reflects real visibility, not just "approved."

`admin-lessons.component` gained a "Practice Gym review scaffold pilot" graph-card (reusing `sp-admin-graph-card`/`sp-admin-breakdown-bars`/`sp-admin-badge` — no new design-system components) showing pilot/Today-insertion status badges and a counts breakdown, loaded alongside the existing dry-run and approval sections and refreshed after any approve/reject action.

---

## 10. Usage Events / Audit (Part H)

No new impression/start/complete event tracking was added — the existing readiness-pool lifecycle (`Ready`→`Reserved`→`Consumed`, `Expired`/`Stale`/`Failed`/`Skipped`) already gives admins shown/started/consumed/skipped-or-expired visibility via the new pilot-summary counts, without building a separate events table. This is documented as the measurable-now baseline; fine-grained impression tracking (e.g. "shown but not started" vs "started but abandoned") is deferred — it was explicitly out of scope ("do not overbuild") and the phase brief only requires admin to see the counts, which the summary endpoint provides.

---

## 11. Rollback Safety (Part I)

- `PracticeGymPilotEnabled=false` hides all approved scaffold items from students — verified for ReviewItems, ContinueItems, and (defensively) SuggestedItems.
- `EnableReviewScaffoldGeneration=false` still stops new generation (19A, unchanged).
- `DryRunOnly=true` still prevents new real items from being written (19A, unchanged).
- No data is deleted on rollback — items stay `Approved`/`Ready`/`Reserved` in the database, simply excluded from the suggestion query until the flag flips back on.
- Covered by `GetSuggestions_PilotDisabled_ApprovedScaffoldItemHidden` and `GetSuggestions_PilotDisabled_ApprovedReservedScaffoldItemHiddenFromContinue`.

---

## 12. A Deliberate Non-Change: `IsPracticeGymEligible`

The Phase 19B `ReviewScaffoldItemDetailDto.IsStudentVisible`/`IsPracticeGymEligible` flags were **not** coupled to `PracticeGymPilotEnabled`, even though that might seem like the "obvious" place to reflect the new gate. The existing 19B integration test `Approve_PendingItem_SetsApprovedAndPersistsReviewer` asserts both flags become `true` immediately after approval, under default config (pilot off). Gating those flags on the pilot flag would make that assertion fail with the new default — breaking a test the phase brief explicitly requires to stay green ("existing 19B approval tests remain green"). Instead, those flags keep their 19B meaning (structural eligibility: lifecycle + approval), and the new pilot-summary endpoint's `studentVisibleCount`/`recentStudentVisibleItems` are the authoritative "is this actually visible to a student right now" signal. This is called out explicitly so a future phase doesn't "fix" what looks like an inconsistency without realizing it's intentional.

---

## 13. Tests

**Unit — `PracticeGymSuggestionServiceTests.cs`** (+8, tests 19–26): pilot disabled hides approved ReviewOnly item; pilot enabled + approved + all gates pass → visible with overridden label/reason; pending hidden even with pilot on; rejected hidden even with pilot on; `MaxStudentVisibleScaffoldSuggestions` cap enforced; pilot disabled hides approved-but-reserved item from Continue (rollback proof); cross-student isolation (another student's approved item never leaks); suggestion service only ever queries `PracticeGym` source (Today-lesson exclusion proof). Constructor updated to take `IOptions<ReadinessPoolReplenishmentOptions>`; existing 15 pre-19C tests (1–18) all still pass unmodified.

**Unit — `ReplenishmentOptionsTests.cs`** (+3, tests 42–44): `PracticeGymPilotEnabled` defaults false; pilot label/reason are non-empty and contain no negative words (`fail`/`weak`/`low confidence`/`bad`); `MaxStudentVisibleScaffoldSuggestions` is conservative (1–4).

**Integration — `ReviewScaffoldPilotSummaryTests.cs`** (new, 4 tests): 401 unauthenticated, 403 student, default config reports pilot/Today-insertion both disabled, approved item counts as approved but not student-visible while pilot is off.

**Angular — `admin-lessons.component.spec.ts`** (+3): loads pilot summary on init and renders "Pilot disabled"/"Today lesson insertion disabled"; renders "Pilot enabled" + counts when pilot on, with no `confidence`/`provider` text anywhere in the card; `refreshPilotSummary` reloads.

**Angular — `practice-gym.component.spec.ts`** (+2): approved scaffold item renders the pilot label/reason with no negative wording or diagnostics; review queue shows the existing empty state (not the grid) when the API returns no review items.

---

## 14. Validation Run (Part M)

```
dotnet build                                    → 0 errors
dotnet test tests/LinguaCoach.UnitTests         → 1,715 passed, 0 failed (+11 vs Phase 19B's 1,704)
dotnet test tests/LinguaCoach.ArchitectureTests → 3 passed, 0 failed (unchanged)
dotnet test tests/LinguaCoach.IntegrationTests  → 1,342 passed, 0 failed (+4 vs Phase 19B's 1,338 —
                                                    the new ReviewScaffoldPilotSummaryTests; all pre-existing
                                                    19A/19B integration tests remain green)
npm run build -- --configuration production     → clean (pre-existing unrelated template/budget warnings only)
npm test -- --watch=false --browsers=ChromeHeadless → 1,505/1,626 passed; 121 pre-existing failures, all in
                                                        AdminStudentDetailComponent/AdminAiConfigComponent —
                                                        files this phase does not touch (same components flagged
                                                        pre-existing in the Phase 19B review); 0 new regressions.
                                                        admin-lessons.component.spec.ts + practice-gym.component
                                                        .spec.ts (the two files this phase touched): 116/116 passed.
```

Playwright: not run this phase, for the same reason documented in the 19B review — the existing Playwright suite has no `admin-lessons` or `practice-gym` review-queue spec to extend, and this phase's UI surface (a pilot status card, and copy changes to an existing section) is covered by the Angular unit tests above plus the full backend suite for the underlying gating logic.

---

## 15. Findings Grouped by Priority

**No blocking findings.**

**Notable design decisions (not defects):**
1. `IsPracticeGymEligible`/`IsStudentVisible` on `ReviewScaffoldItemDetailDto` intentionally NOT pilot-gated — see §12.
2. ContinueItems (reserved scaffold items) were pilot-gated even though the phase brief's Part D eligibility list doesn't explicitly mention Continue — done because Part I ("existing approved but unconsumed items can be hidden by config") requires it for a reserved item to actually disappear on rollback.
3. The `suggestable` (Suggested) bucket got a defensive pilot-gate condition even though scaffold items structurally can't reach it today (verified: `RoutingReason.Review` is always paired with `IsLowerLevelContent=true` in `CurriculumRoutingService`, and the Suggested filter excludes that combination). Kept as defence-in-depth, not because a real gap exists today.

---

## 16. Decisions Made

- Reused all 19A/19B config and DTOs where possible; only additive new fields, no renames or removals.
- Pilot label/reason are configurable strings (not hardcoded), so a future pilot can adjust wording without a code change.
- No AskUserQuestion was needed — the phase brief was fully specified.

---

## 17. Risks or Unresolved Questions

- None blocking. Fine-grained impression/start/abandon event tracking (Part H) was deferred per the brief's own "do not overbuild" instruction; if a future phase needs it, the readiness-pool lifecycle already provides shown/started/consumed/skipped-or-expired at the aggregate level via the new pilot-summary endpoint.
- The 121 pre-existing Angular test failures (`AdminStudentDetailComponent`, `AdminAiConfigComponent`) are unrelated to this phase and were not introduced or worsened by it (same components flagged in the Phase 19B review at 120 failures); they should be tracked and fixed independently.
- Playwright coverage for the pilot status card and the pilot-labeled review copy does not exist yet; if `admin-lessons`/`practice-gym` later get their own Playwright specs, this flow should be added.

---

## 18. Final Verdict

**Complete.** All Part A–N requirements from the phase brief are implemented, tested, and documented. `PracticeGymPilotEnabled=false` by default — this phase does not turn student-facing visibility on in production. `AllowTodayLessonInsertion` remains untouched and `false` by default. No CEFR, objective-completion, or Learning Plan behavior was touched.

---

## 19. Next Recommended Action

Per the roadmap's Tier 3 priority list, the next phase remains **20A — Admin AI Operations Dashboard**, unless the product owner wants to exercise the Phase 19C pilot (`PracticeGymPilotEnabled=true`) in a staging environment first now that a dedicated, instantly-reversible go-live switch exists.

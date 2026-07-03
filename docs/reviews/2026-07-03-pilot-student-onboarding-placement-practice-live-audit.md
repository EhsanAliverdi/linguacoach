---
status: current
lastUpdated: 2026-07-03
owner: engineering
supersedes:
supersededBy:
---

> **Implementation status:** fixes for the findings below were implemented the same day — see
> `docs/reviews/2026-07-03-pilot-student-fixes-implementation-summary.md` for what shipped,
> what was reverted (Fix 8), and outstanding manual DB steps.

# Live Production Audit — Onboarding Gating, Placement Status, and Practice Gym Activity Integrity

**Date:** 2026-07-03
**Related sprint/feature:** Follow-up to Phase 20H (`docs/reviews/2026-07-03-phase-20h-live-pilot-stabilization-readiness-practice-gym-review.md`)
**Trigger:** User-directed live QA audit of `pilot.student.20e@speakpath.app` on `https://speakpath.app`, requested to explain reported inconsistencies before any fix work begins.

## Files reviewed

- `src/LinguaCoach.Infrastructure/Dashboard/DashboardQueryHandler.cs`
- `src/LinguaCoach.Infrastructure/Onboarding/OnboardingV2QueryHandler.cs`
- `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs`
- `src/LinguaCoach.Api/Controllers/PlacementController.cs`, `StudentPlacementController.cs`
- `src/LinguaCoach.Web/src/app/features/student/profile/profile.component.ts`
- `src/LinguaCoach.Web/src/app/core/guards/placement.guard.ts`
- `src/LinguaCoach.Infrastructure/PracticeGym/PracticeGymSuggestionService.cs` (referenced from prior Phase 20H review)
- Live production responses/DOM for `https://speakpath.app/profile`, `/dashboard`, `/practice`, `/activity?activityId=c10722ac-a4a9-468b-aefd-27b82213d806`

## Method

Logged in live as `pilot.student.20e@speakpath.app` via headless browser (gstack `browse`). Read rendered UI text, DOM structure, and network calls directly — no fixtures, no local dev server. Password had been rotated since the last documented session; current password obtained from the user during this session (not persisted here).

## Findings, grouped by priority

### P0 — Practice Gym activity is not completable (functional break)

Clicking the "Giving Structured Explanations" (SpeakingPractice, B2, `general_english`/`day_to_day`) card on `/practice` opens `activity?activityId=c10722ac-a4a9-468b-aefd-27b82213d806`, which renders as **"Crafting Clear Technical Explanations"** — a B1 **Vocabulary** phrase-matching exercise, not a Speaking activity. The card's advertised type/level/title do not match the activity actually opened.

Worse: the matching exercise itself is broken. DOM inspection of the practice step shows both the "Phrase" and "Meaning" columns contain only their header `<p>` and an empty Angular `*ngFor` comment placeholder (`<!---->`) — zero phrase/meaning pairs rendered. "Check matches" stays permanently `disabled`. The exercise cannot be completed by any student who reaches it. No network request accompanies the transition to the practice step (data was expected to already be present client-side from the initial `GET /api/activity/{id}` call), meaning the activity's stored exercise payload itself is missing pair data — not a client rendering defect.

**Repro:** Log in as `pilot.student.20e@speakpath.app` → `/practice` → click any of the "Giving Structured Explanations" cards → Start practice → step 2 renders empty.

### P0 — Card metadata does not match opened activity content/type

Same repro as above. The Practice Gym suggestion card's declared `SpeakingPractice`/B2/topic tags do not correspond to the actual materialized activity (`Vocabulary`/B1/phrase-match). This is distinct from the Phase 20H duplicate-data fix, which only verified that no two cards share the same `(objective, patternKey, activityType)` — it did not verify that a card's *displayed* metadata matches its *linked* activity's actual type/content.

### P1 — Unprompted Farsi (Persian) text shown to a student with no support language selected

The AI-generated lesson content for the above activity includes a "Watch out for" translation line in Persian: `فکر کنید که چطور این عبارات به مخاطب شما کمک می‌کنند تا جریان توضیحات شما را دنبال کند.` — despite `/profile → Support language` being explicitly "None selected" and `Translation help` set to "Not set." This appears to be either a hardcoded/leftover default from unrelated AI-generation testing, or a content-generation defect where a translation field is populated and rendered unconditionally regardless of the student's `SupportLanguageCode`/translation preference.

### P1 — Placement status disagrees between `/profile` and `/dashboard`

- `/profile` reads the Phase 14A "adaptive placement" system (`GET /api/student/placement/current` → `PlacementAssessmentService.GetLatestAssessmentAsync`, `src/LinguaCoach.Infrastructure/Placement/PlacementAssessmentService.cs:490-497`), which returns the latest `PlacementAssessments` row by `CreatedAt`. For this student that row is stuck at `status: InProgress`.
- `/dashboard` and the placement route guard read `StudentProfile.LifecycleStage` (legacy field), which is already at a completed stage (e.g. `CourseReady`/`ActiveLearning`) with an estimated level of B2.
- **Root cause:** two independently-maintained placement signals (legacy `LifecycleStage` vs. adaptive `PlacementAssessments.Status`) are not kept in sync. This student's B2 level was established through a path that updated `LifecycleStage` but never finalized the corresponding adaptive assessment row.
- Confirms as live symptom: `GET /api/placement/result` returns `400`, consistent with the adaptive-system endpoint having no completed assessment to report, while `GET /api/student/dashboard/summary` returns 200 with a ready level sourced from `LifecycleStage`.

### P1 — "Continue placement" button is not itself broken; the guard's correct-but-conflicting check makes it look like a dead loop

`profile.component.ts:533` (`navigateToPlacement()`) correctly calls `router.navigate(['/placement'])` — this is not a no-op or hardcoded redirect. The `/placement` route guard, `placementAccessGuard` (`src/LinguaCoach.Web/src/app/core/guards/placement.guard.ts:34-62`), checks `LifecycleStage`, sees it already at a completed stage, and immediately redirects back to `/dashboard`. From the user's perspective this reads as "the button does nothing" / "loops back to dashboard," but the actual defect is upstream: `/profile`'s "in progress" copy is driven by the stale adaptive-assessment status (see previous finding), while the guard correctly trusts the accurate `LifecycleStage`. Fixing the placement-status sync issue should make this button behave consistently (either hide/relabel "Continue placement" when `LifecycleStage` is already complete, or actually route to a resumable adaptive assessment when it is not).

### P2 — Onboarding gate does not cover the newer preference fields

`DashboardQueryHandler.HandleAsync` (`src/LinguaCoach.Infrastructure/Dashboard/DashboardQueryHandler.cs:36-37`) and `CefrAssessmentHandler.cs:49-50` gate dashboard/lesson access on the coarse `OnboardingStatus` enum (`NotStarted`/`InProgress`/`Complete`) — this gate is real and functioning as designed. However, `StudentProfile.LearningGoals`, `FocusAreas`, and `SupportLanguageCode` (added later, via the `StudentLearningPreferences` migration) are a separate, optional profile-preferences form, not part of the `OnboardingStatus` gate. `OnboardingV2QueryHandler.HandleAsync` (`src/LinguaCoach.Infrastructure/Onboarding/OnboardingV2QueryHandler.cs:39-43`) lazily backfills a `StudentOnboardingProgress` row as `CreateCompleted(...)` for any v1-complete student who lacks one — without ever prompting for the newer preference fields.

**This explains the reported "how did this user see lessons without finishing onboarding" concern:** the student legitimately has `OnboardingStatus.Complete` from before goals/focus/support-language existed as fields, so dashboard/lesson/practice access is correctly gated and correctly unlocked — but the newer preference fields were never backfilled or required, leaving `/profile` looking like onboarding never happened even though, by the system's actual gate, it did.

**Product question, not yet decided:** should students in this state (`OnboardingStatus.Complete`, no learning-preferences row) be force-prompted to fill in goals/focus/support-language before continuing? Or are these fields intentionally optional/supplementary post-onboarding? This determines whether P2 needs a code fix or just a one-time data backfill/reminder banner.

## Decisions made

None yet — this document is a findings report only, per user instruction ("just log in and check the issue and behavior... then we will discuss"). No code was changed in this session.

## AskUserQuestion answers

None asked as part of the technical review; two clarifying questions were asked earlier in the session to obtain the pilot student's current (rotated) password, unrelated to the findings above.

## Implementation tasks produced

None yet — pending discussion and prioritization with the user.

## Risks / unresolved questions

- Whether the P0 broken-activity issue (`c10722ac-a4a9-468b-aefd-27b82213d806`) is isolated to this one materialized activity/pattern (`vocabulary` "phrase match" type activities generated under the `general_english`/`day_to_day` objective) or affects a broader class of AI-generated vocabulary activities — not yet checked against other students or other materialized activities of the same pattern.
- Whether the Farsi text is a one-off leftover from prior QA/seed data for this specific student, or a live AI-generation defect that would recur for any student — not yet checked against a freshly-onboarded student with no support language set.
- Whether card-metadata/activity-type mismatch (P0, second finding) is limited to this activity or a systemic issue in how Practice Gym cards render metadata from `PracticeGymSuggestionService` vs. the linked `LearningActivity`'s actual persisted type — needs code-level investigation before scoping a fix.
- The product decision on P2 (should learning-preferences be mandatory) is unresolved and blocks knowing whether that item needs a code fix, a data backfill, or is working as intended.

## Final verdict

**Not pilot-ready in current state.** Two P0 functional breaks confirmed live (uncompletable Practice Gym activity; card metadata mismatched with actual opened activity) plus two P1 UX-trust issues (unprompted foreign-language text; contradictory placement status across two pages) that would visibly confuse or block a real student today, on top of the Phase 20H fixes from earlier the same day. The onboarding-gating question (P2) is not a bug but an unresolved product design gap worth a decision before continuing the pilot.

## Next recommended action

Discuss and prioritize with the user before further changes:
1. Confirm scope of the P0 broken-activity/card-mismatch bugs (one activity vs. systemic) — likely needs a quick DB/query check across all `LearningActivity` rows of the same pattern.
2. Decide fix approach for the Farsi-text leak (strip translation content when no support language is set, vs. investigate why it was generated/stored at all).
3. Decide the correct single source of truth for placement completion (`LifecycleStage` vs. `PlacementAssessments.Status`) and reconcile them going forward.
4. Decide whether learning-preferences (goals/focus/support-language) should become a mandatory, gated step for pilot students, or remain optional.

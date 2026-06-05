# SpeakPath Product Backlog

Status labels: `Not started` · `Planned` · `Blocked` · `Done`

Items are grouped by theme. Each item is a discrete unit of work; sub-bullets are acceptance criteria or notes.

---

## Student learning memory (in sprint: student-learning-memory-adaptive-curriculum-sprint)

- [x] Extend `UserLearningSummary` with 6 new JSON fields. `Planned`
  - `journey_summary`, `strong_skills_json`, `weak_skills_json`,
    `recurring_mistakes_json`, `covered_scenarios_json`, `next_focus_json`
  - Domain method `ApplyDelta(MemoryUpdateDeltaDto)` enforces list caps
- [x] Add `StudentSkillProfile` table (skill_key, is_weak per student). `Planned`
  - 10 skill keys: grammar_accuracy, formal_tone, sentence_clarity, message_structure,
    workplace_vocabulary, concise_writing, softening_language, summarising_information,
    clarifying_questions, escalation_language
- [x] Add `fingerprint_json` column to `LearningModule`. `Planned`
  - Fields: communicationMode, scenarioType, audience, tone, difficulty, grammarFocus, vocabularyTheme
- [x] Add xmin concurrency token to `LearningPath`. `Planned`
- [x] Extract `AiExecutionService` shared fallback pattern. `Planned`
- [x] Extract `LearningPathDtoBuilder` shared DTO builder. `Planned`
- [x] Add `student_memory_update` AI prompt to seed data. `Planned`
- [x] Implement `StudentMemoryService` with best-effort update. `Planned`
- [x] Wire memory update into `ActivitySubmitHandler` (8s timeout). `Planned`
- [x] Add `learning_path_generate_adaptive` AI prompt. `Planned`
- [x] Implement `AdaptivePathGeneratorHandler`. `Planned`
- [x] `POST /api/learning-path/generate-next` endpoint. `Planned`
- [x] `GET /api/learning-path/memory` endpoint. `Planned`
- [x] `GET /api/admin/students/{id}/learning-memory` endpoint. `Planned`
- [x] "Your learning focus" panel on dashboard / /my-path. `Planned`
- [x] Module card enrichment (focusSkill, reason, difficulty). `Planned`
- [x] "Generate next modules" button with loading / error states. `Planned`
- [ ] Add staleness flag / alert when memory update fails repeatedly. `Not started`
  - If `UserLearningSummary.UpdatedAt` is > 7 days old and student has recent attempts,
    surface an admin alert or background refresh attempt
- [ ] Add numeric skill score tracking (0–100) to `StudentSkillProfile`. `Not started`
  - Deferred from current sprint — add after validating the is_weak approach
- [ ] Admin curriculum map editor. `Not started`
  - Currently curriculum is seeded/static for Workplace Writing B1/B1+/B2
  - Future: admin can add career-specific curriculum maps
- [ ] Move memory update to background job. `Not started`
  - Currently synchronous with 8-second timeout in ActivitySubmitHandler
  - When student volume grows, move to LinguaCoach.Worker queue

---

## Progress and activity tracking

- [ ] Implement real practice streak tracking. `Not started`
  - Persist a streak counter per student in the database
  - Increment on each day a new ActivityAttempt is submitted
  - Reset if a calendar day is skipped
- [ ] Track minutes practised this week. `Not started`
  - Derive from ActivityAttempt.CreatedAt timestamps
  - Approximate based on activity type (e.g. WritingScenario ≈ 8 min)
- [ ] Track total activities completed per student. `Not started`
  - Count of `ActivityAttempt` rows per `StudentProfileId`
- [ ] Replace dashboard stat tile placeholders with real backend data. `Not started`
  - Dashboard API (`GET /api/dashboard`) must return: `streakDays`, `minutesThisWeek`, `activitiesDone`
  - Remove `—` placeholders once endpoint delivers values
- [ ] Add progress history data for the Progress page. `Not started`
  - `GET /api/progress` or extend dashboard endpoint
  - Return recent ActivityAttempts with score, date, activity type
- [ ] Add per-skill progress values. `Not started`
  - Return progress percentage for: Writing, Speaking, Listening, Vocabulary, Pronunciation
  - Writing is the only active skill currently; others return `null` until implemented
  - UI must show `null`/`0` skills as "Not started" — never fake data

---

## Coach insights

- [ ] Store and retrieve latest AI coach feedback summaries. `Not started`
  - After each ActivityAttempt, persist `whatYouDidWell`, `mainMistakes`, `toneExplanation` to a `CoachInsight` or similar table
  - Retrieve most recent N coach messages per student
- [ ] Show "Latest from your coach" using real recent ActivityAttempt feedback. `Not started`
  - Dashboard coach card currently shows placeholder text
  - Replace with last feedback `feedbackSummary` or first item of `whatYouDidWell`
  - Include score, activity title, and timestamp
- [ ] Show latest completed activity with score and tone summary. `Not started`
  - The completed activity card inside the coach card is currently a placeholder
  - Bind to the most recent `ActivityAttempt` for the student
- [ ] Add coaching trend insights over time. `Not started`
  - Future: "Your tone is improving" / "Grammar errors decreasing" type summaries
  - Deferred until sufficient attempt history exists (suggest 5+ attempts minimum)

---

## Streak system

- [ ] Add daily practice streak persistence. `Not started`
  - Store `LastPracticeDate` and `CurrentStreak` on `StudentProfile` or a separate `StreakRecord` table
  - Increment on new ActivityAttempt, reset if gap > 1 calendar day
- [ ] Add weekly streak calendar data to the API. `Not started`
  - Return an array of 7 booleans (`[M, T, W, T, F, S, S]`) for the current ISO week
  - Dashboard streak calendar will render filled dots for `true` days
- [ ] Add streak reset/continuation logic. `Not started`
  - Grace period: streak continues if the student practises before midnight of the missed day's timezone
  - Timezone must be stored with the student profile
- [ ] Replace empty streak calendar placeholder with real data. `Not started`
  - Currently all 7 dots are empty with "Coming soon" caption
  - Remove caption and bind to real weekly data once available

---

## Future activity types

- [ ] Implement SpeakingRolePlay activity type. `Not started`
  - Backend: new `ActivityType` value, prompt template, AI handler
  - Frontend: new phase UI (audio recording or text simulation), new skill badge colour
  - Keep Speaking card as "Coming soon" until fully implemented
- [ ] Implement ListeningComprehension activity type. `Not started`
  - Backend: audio clip or transcript generation, comprehension questions
  - Frontend: media player UI or transcript viewer
  - Keep Listening card as "Coming soon" until fully implemented
- [ ] Implement VocabularyPractice activity type. `Not started`
  - Backend: flashcard-style or gap-fill prompt generation
  - Frontend: new interaction pattern (tap to reveal, match, fill)
  - Keep Vocabulary card as "Coming soon" until fully implemented
- [ ] Implement PronunciationPractice activity type. `Not started`
  - Backend: target word/sentence selection, pronunciation scoring via AI or speech API
  - Frontend: microphone UI, waveform or score display
  - Keep Pronunciation card as "Coming soon" until fully implemented
- [ ] Implement ReadingTask activity type. `Not started`
  - Backend: workplace text generation, comprehension questions
  - Frontend: reading + Q&A layout
  - Keep Reading card (if surfaced in UI) as "Coming soon" until implemented
- [ ] Keep all unimplemented skill cards visually present but disabled. `Planned`
  - Current: 4 skill cards show "Coming soon" with reduced opacity
  - Do not remove them; they communicate the product roadmap to testers
  - Remove "Coming soon" label only when the backend feature is fully wired

---

## Profile page

- [ ] Replace placeholder profile rows with real user/profile data. `Not started`
  - Learning goal: read from `StudentProfile.LearningGoal` or `LearningTrack.Name`
  - Current level: read from CEFR assessment result or `StudentProfile.CefrLevel`
  - Practising: read from `LanguagePair.TargetName` + skill focus
  - Career context: read from `CareerProfile.Name`
- [ ] Add editable learning preferences if needed. `Not started`
  - Deferred — read-only display is sufficient for pilot phase
- [ ] Add language pair and career context display. `Not started`
  - Show "Persian → English · Document Controller" or equivalent
  - Read from `StudentProfile` joined to `LanguagePair` and `CareerProfile`
- [ ] Add account/security links if needed. `Not started`
  - Change password link at minimum
  - Privacy / data deletion for compliance if required later

---

## Progress page

- [ ] Replace placeholder stat tiles with real ActivityAttempt summaries. `Not started`
  - Day streak: from streak system (see Streak system section)
  - Activities done: count of `ActivityAttempt` rows
  - Avg score: mean of `ActivityAttempt.Score` values (exclude null)
- [ ] Add skill progress bars with real values. `Not started`
  - Writing progress: derive from number of completed Writing activities vs. module target
  - Other skills: return `0` or `null` with "Not started" label until implemented
- [ ] Add module completion history. `Not started`
  - Show completed modules with completion date
  - Show in-progress module with current activity count
- [ ] Add recent scores list with improvement trend. `Not started`
  - Show last 5–10 ActivityAttempts: title, score, skill badge, date
  - Optionally show trend arrow (improving / stable / needs work) if 3+ results available

---

## My Path improvements

- [ ] Add richer module details from real progress data. `Not started`
  - `isCurrent`, `completedActivities`, `totalActivities` already returned by API
  - Ensure `moduleStatus()` in `LearningPathComponent` reflects real data correctly
- [ ] Add ability to tap a module card to view its activities. `Not started`
  - Currently module cards link to `/activity` generically
  - Future: `/my-path/modules/:moduleId` showing the activity list for that module
- [ ] Add path regeneration or adjustment capability. `Not started`
  - Admin or AI trigger to regenerate path based on progress
  - Not needed for pilot phase — defer

---

## Design system follow-ups

- [x] Extract repeated app shell into layout components. `Done`
  - Shell HTML removed from all page components
  - `StudentAppLayoutComponent` owns sidebar, header, bottom nav
  - `AdminAppLayoutComponent` owns left sidebar, header
  - `PublicLayoutComponent` owns centered background wrapper
  - Pages render content only — no shell duplication remains
- [ ] Extract `StatCard` component. `Planned`
  - Repeated 3× on dashboard and progress page
  - Input: `icon`, `value`, `label`, `color`, `bg`
- [ ] Extract `SkillCard` component. `Planned`
  - Repeated on dashboard and progress page
  - Input: `skill`, `level`, `pct`, `active`, `routerLink`
- [ ] Extract `ModuleCard` component. `Planned`
  - Used on My Path page; candidate for Activity selection page later
- [ ] Extract `CoachCard` / `CoachMessage` component. `Not started`
  - Used on dashboard right column and feedback phase
- [ ] Extract `ScoreRing` component. `Not started`
  - SVG ring used in My Path header and feedback phase
  - Input: `value`, `size`, `stroke`, `color`
- [ ] Keep `sp-*` utility classes documented in `speakpath-design-system.md`. `Planned`
  - Update whenever new classes are added to `styles.css`
- [ ] Add screenshot/visual reference notes for future agents. `Not started`
  - Annotated screenshots of prototype screens in `docs/design/references/`
  - Reference prototype JSX files when making UI decisions

---

## Admin dashboard — real data

- [ ] Replace KPI card placeholders with real counts. `Not started`
  - "Total students" already live from `GET /api/admin/students` count
  - "Onboarded" already live from same endpoint (filter by `onboardingStatus === 'Complete'`)
  - "Activities tracked": requires `GET /api/admin/stats` endpoint returning `totalActivityAttempts`
  - "AI provider": hardcoded "Configured" — wire to check if at least one provider has a non-null API key
- [ ] Implement real usage analytics. `Not started`
  - AI token usage per provider per day: log `promptTokens` + `completionTokens` from AI responses
  - Cost estimate per student: requires provider pricing table in DB
  - Expose via `GET /api/admin/usage?from=&to=`
- [ ] Implement activity completion trends. `Not started`
  - `GET /api/admin/analytics/activity-completion` returning daily counts over a date range
  - Chart on admin analytics page once data exists
- [ ] Implement feedback quality review. `Not started`
  - Score distribution histogram across all ActivityAttempts
  - Average score per skill type, per career profile
  - Flag unusually low-quality feedback (score = null more than X% of attempts)
- [ ] Add system health / API health card to admin dashboard. `Not started`
  - Real-time ping to each configured AI provider (or display last test result from ai-config)
  - Show green/amber/red per provider
  - Do not fake — only show if a recent test result is stored
- [ ] Build admin settings page. `Not started`
  - Route: `/admin/settings`
  - Planned content: platform name/branding config, pilot programme dates, allowed email domains
  - Currently a placeholder / disabled nav item
- [ ] Improve admin student list page. `Not started`
  - Add search/filter by email
  - Add sort by joined date or onboarding status
  - Add pagination if student count exceeds 25
  - Add ability to view individual student's learning path and activity history
- [ ] Add admin student learning memory view. `Not started`
  - Backend endpoint and Angular API client exist: `GET /api/admin/students/{id}/learning-memory`
  - Deferred from Student Learning Memory UI phase because there is no admin student detail page yet
  - Show compact memory only: journey summary, strengths, weaknesses, recurring mistakes, next focus, covered scenario count, skill profile
- [ ] Design system: use `sp-admin-*` classes consistently across all admin components. `Planned`
  - `admin-prompts` and `admin-careers` still use raw Tailwind for their form/table bodies
  - Migrate incrementally — do not break functionality
  - Document final admin class list in `frontend-layout-system.md`
- [ ] Improve admin mobile drawer. `Planned`
  - Current drawer works but has no swipe-to-close gesture
  - Could add route-change auto-close (RouterEvents subscription in AdminAppLayoutComponent)
  - Could add keyboard Escape to close
- [ ] Add admin mobile bottom navigation or persistent tab bar as alternative to drawer. `Not started`
  - Drawer pattern is sufficient for a desktop-first admin tool used on mobile rarely
  - If admin mobile usage is significant, consider a simplified bottom tab bar for admin

---

## Learning path progression (post Learning Path Progression sprint)

- [ ] Persist explicit module completion confirmation to a dedicated `ModuleCompletion` table instead of `CompletedAt` column, to support future multi-path learners. `Not started`
- [ ] Show per-module score history chart (last 5 attempts per module). `Not started`
- [ ] Module completion certificates or achievement badges. `Not started`
- [ ] Admin / teacher progress view per student (current module, focus area, average score). `Not started`
- [ ] Long-term trend chart: score progression over 10+ attempts. `Not started`
- [ ] Spaced repetition for repeated mistakes (re-surface activities in weak categories). `Not started`
- [ ] Auto-advance module when ready without explicit student confirmation (optional preference). `Not started`
- [ ] Notify student when module is ready to complete (push notification or dashboard badge). `Not started`

---

## Learning experience improvements (post Learning Experience sprint)

- [ ] Richer attempt history page showing all attempts side by side. `Not started`
  - Route: `/activity/history/:activityId`
  - Show all `ActivityAttempt` rows for a given activity, ordered by date
  - Each row: attempt number, score, date, first few words of submission
- [ ] Side-by-side diff viewer for attempts. `Not started`
  - Compare attempt N with attempt N-1 visually
  - Highlight what changed between submissions
  - Deferred — requires richer attempt history page first
- [ ] Inline sentence-level comment annotations. `Not started`
  - AI returns comments anchored to specific sentence positions
  - UI renders inline margin annotations (like Google Docs)
  - Requires new AI prompt output format
- [ ] Teacher / admin review of AI feedback quality. `Not started`
  - Admin can browse recent `ActivityAttempt` feedback JSONs
  - Admin can flag poor feedback for prompt review
  - Requires admin UI extension
- [ ] Skill-based progress analytics from `changes.category` data. `Not started`
  - Aggregate `changes.category` values from recent attempts per student
  - Show: "Grammar is your most common issue this week"
  - Requires data aggregation query on `FeedbackJson` or new field on `ActivityAttempt`
- [ ] Vocabulary extraction from writing attempt mistakes. `Not started`
  - Extract vocabulary from `vocabularyIssues` and `changes` with category=vocabulary
  - Add to student's vocabulary list for spaced repetition
  - Requires vocabulary tracking feature
- [ ] Speaking and listening activity types. `Not started`
  - See future activity types section above
- [ ] Client-side LCS-based visual diff for richer comparison. `Not started`
  - Compute diff between student's draft and improved version in the browser
  - Highlight word-level insertions and deletions
  - Lower priority — server-side `changes` list already covers this

---

## Legacy database cleanup

> ⚠️ These items require explicit confirmation before execution. Do not run without a backup.

- [ ] T19: Confirm no active FK dependency on `SourceWritingScenarioId` in `LearningActivity`. `Planned`
  - Verify all current activities use `aiGeneratedContentJson` not `SourceWritingScenarioId`
  - Query: `SELECT COUNT(*) FROM learning_activities WHERE source_writing_scenario_id IS NOT NULL`
- [ ] Backfill any required legacy scenario content into `aiGeneratedContentJson`. `Blocked`
  - Blocked on T19 confirmation above
- [ ] Export and back up `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Export to CSV or S3 before any schema changes
- [ ] Remove `SourceWritingScenarioId` FK from `LearningActivity`. `Not started`
  - EF Core migration: drop column and FK constraint
  - Remove `WritingScenario` domain entity and all references
- [ ] Drop legacy `writing_scenarios` and `writing_submissions` tables. `Not started`
  - Only after backup confirmed and no active references
  - Requires explicit confirmation from user before execution

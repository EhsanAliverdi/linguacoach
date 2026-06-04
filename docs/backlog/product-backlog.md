# SpeakPath Product Backlog

Status labels: `Not started` · `Planned` · `Blocked` · `Done`

Items are grouped by theme. Each item is a discrete unit of work; sub-bullets are acceptance criteria or notes.

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

- [ ] Extract repeated app shell into a reusable `AppShellComponent`. `Planned`
  - Currently the sidebar + topbar + bottom nav HTML is duplicated across:
    - `dashboard.component.html`
    - `activity-lesson.component.html`
    - `learning-path.component.html`
    - `progress.component.ts` (inline template)
    - `profile.component.ts` (inline template)
  - Extract to a shared `AppShellComponent` with `[activeRoute]` input
  - Use Angular `<router-outlet>` or `@ContentChild` for page body
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

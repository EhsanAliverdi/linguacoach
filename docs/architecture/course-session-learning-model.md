---
status: current
lastUpdated: 2026-06-09 13:11
owner: architecture
supersedes:
supersededBy:
---

# Course Session Learning Model

## Overview

SpeakPath moves from an "activity generator" to an **AI-powered workplace English class platform**.

The product must feel like a structured English class, not a collection of isolated practice cards.

---

## Current Model (legacy)

```
LearningPath
  → LearningModule
    → LearningActivity
      → ActivityAttempt
```

This model supports isolated activity types but does not represent a lesson or a teaching sequence.

---

## New Model

```
LearningPath
  → LearningModule
    → LearningSession          ← new: represents one lesson/class
      → SessionExercise        ← new: one step within the lesson
        → LearningActivity     ← existing, reused
          → ActivityAttempt    ← existing, reused
```

The existing `LearningActivity` and `ActivityAttempt` tables are preserved and reused. Sessions and exercises are a new layer that organises them into teaching sequences.

---

## Entity: LearningSession

Represents one complete lesson — the equivalent of one English class.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| LearningModuleId | Guid | FK to LearningModule |
| Title | string | e.g. "Explaining a Delay Professionally" |
| Topic | string | e.g. "Professional delay communication" |
| DurationMinutes | int | 10 / 15 / 20 / 30 |
| FocusSkill | string | Primary skill: Listening, Writing, Speaking, Vocabulary |
| SecondarySkillsJson | string | JSON array of secondary skills |
| SessionGoal | string | One sentence goal for the student |
| Order | int | Order within module |
| Status | SessionStatus | NotStarted, InProgress, Completed |
| StartedAtUtc | DateTime? | |
| CompletedAtUtc | DateTime? | |
| GeneratedFromMemorySnapshotJson | string? | Compact memory state used during generation |
| CreatedAtUtc | DateTime | |

### SessionStatus enum

```
NotStarted
InProgress
Completed
```

---

## Entity: SessionExercise

Represents one step (one exercise) within a lesson, ordered and structured.

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| LearningSessionId | Guid | FK to LearningSession |
| Order | int | Step number within lesson |
| ExercisePatternKey | string | e.g. "listen_and_gap_fill" |
| PrimarySkill | string | |
| SecondarySkillsJson | string | JSON array |
| EstimatedMinutes | int | |
| Instructions | string | Student-facing instructions for this step |
| LearningActivityId | Guid? | Nullable: linked when activity is generated |
| Status | ExerciseStatus | |
| CompletedAtUtc | DateTime? | |

### ExerciseStatus enum

```
NotStarted
InProgress
Completed
Skipped
```

---

## Teaching Sequence

A session is generated following a proven teaching flow.

The teaching sequence is inspired by communicative language teaching (CLT) principles:

1. **Warm-up / Context** — activate prior knowledge, introduce topic
2. **Input** — listening or reading a workplace message
3. **Noticing / Language Focus** — vocabulary, phrases, grammar patterns
4. **Controlled Practice** — gap-fill, match, sentence completion
5. **Semi-controlled Practice** — guided writing or structured speaking
6. **Productive Workplace Task** — email, Teams reply, meeting message, or recorded explanation
7. **Feedback / Reflection** — AI coach summary, next focus

Not all sessions include every step. Duration controls how many steps are included.

---

## Example Session: 15-minute lesson

**Topic:** Explaining a delay professionally

| Step | Pattern | Skills | Minutes |
|---|---|---|---|
| 1 | phrase_match | Vocabulary | 2 |
| 2 | listen_and_answer | Listening + Vocabulary | 4 |
| 3 | gap_fill_with_workplace_phrase | Vocabulary + Grammar | 3 |
| 4 | teams_chat_simulation | Writing + Tone | 4 |
| 5 | lesson_reflection | Reflection | 2 |

---

## Session Duration Rules

Students choose preferred lesson duration during onboarding. This controls how many exercises are generated per session.

### 10-minute session

- 1 short input exercise (listening or reading)
- 1 vocabulary/phrase exercise
- 1 productive task (writing or speaking)
- Quick AI reflection

### 15-minute session

- Input exercise
- Vocabulary/phrase focus
- Controlled practice
- Writing or speaking task
- AI feedback

### 20-minute session

- Warm-up / context
- Listening or reading
- Vocabulary / collocation
- Grammar / phrase focus
- Writing
- Speaking (optional)
- Reflection

### 30-minute session

- Full mini-class with all steps
- Listening
- Reading
- Vocabulary
- Grammar / language focus
- Writing
- Speaking
- Review / reflection

---

## Micro Lessons

Every session should begin with a short teaching moment before the first practice exercise. This is called a **micro lesson**.

Inspired by the "teach before practise" principle used in communicative language teaching and effective language learning apps.

Micro lesson types:
- `micro_lesson_phrases` — introduce the target phrases the student will use in this session
- `micro_lesson_dialogue` — model a short workplace dialogue to set the tone before speaking/writing tasks
- `micro_lesson_mistake` — show a common recurring mistake from the student's memory before a correction exercise

Micro lessons are short (2 minutes), read-only or listen-only, and do not require a student submission.

They are implemented as a `SessionExercise` with no linked `LearningActivity` (no output required).

---

## Weekly Plan

Students choose a practice frequency during onboarding (e.g. 3 days per week, 5 days per week).

The session generator uses this to:
- pre-generate session slots for the week (e.g. Mon / Wed / Fri)
- display a weekly plan on the Today page
- mark sessions as completed when done

The weekly plan is stored as session schedule metadata on `LearningPath` or a lightweight `WeeklyPlan` structure (design TBD in Phase 2).

The Today page anchors the student's daily habit. It shows:
- today's session (start / continue)
- remaining sessions this week
- days completed vs planned this week

This replaces the streak placeholder with a meaningful weekly rhythm.

---

## Session Generator Rules

The session generator is backend-owned. AI must not decide the lesson structure.

The backend must select exercises using:

- student's preferred session duration
- student CEFR level / `LanguageDifficulty` (from placement result or learning memory)
- student `DomainComplexity` / `WorkplaceSeniority` (from professional experience level + role familiarity)
- career context
- current module topic
- student learning memory (weaknesses, strong skills, covered scenarios)
- vocabulary queue (items due for review)
- previously completed exercises (avoid repetition)
- module fingerprint (avoid same scenario type + skill + audience combination)
- weekly plan slot (which day of the week, how many sessions remain)

**Domain complexity rule:** when selecting a workplace scenario topic for any exercise pattern, the topic's domain complexity must not exceed the student's `WorkplaceSeniority` by more than one level — unless the session includes a `micro_lesson_*` step that introduces the concept first.

See: [professional-experience-domain-complexity.md](professional-experience-domain-complexity.md)

The AI is called only to generate content within an exercise (e.g. generate the audio script, generate the gap-fill, generate the email prompt). The AI does not choose which exercise pattern to use.

All AI content generation calls must include `{{DomainComplexity}}`, `{{ProfessionalExperienceLevel}}`, and `{{RoleFamiliarity}}` as prompt variables alongside `{{CEFRLevel}}` and `{{CareerContext}}`.

---

## Relationship to Existing Architecture

- `LearningPath` is unchanged.
- `LearningModule` is unchanged.
- `LearningActivity` and `ActivityAttempt` are reused.
- `SessionExercise.LearningActivityId` links a step to the existing activity/attempt system.
- The Practice Gym continues to use `LearningActivity` directly without a session.
- AI usage tracking is unchanged.

---

## Migrations

Two new tables are added:

- `learning_sessions`
- `session_exercises`

These are additive and non-breaking.

---

## Product Navigation (future)

```
Today         ← shows today's lesson session
My Course     ← LearningPath → modules → sessions
Practice      ← Practice Gym (on-demand activity types)
Vocabulary    ← VocabularyQueue / StudentVocabularyItem
Progress      ← skill progress, session history
Profile       ← preferences, onboarding data
```

---

## Today Page Requirements

The Today page replaces the current activity-card dashboard as the primary student entry point.

It must show:

- today's lesson (LearningSession)
- topic
- duration
- step-by-step exercise list
- current step / progress indicator
- continue / resume button
- completion celebration
- next session preview

---

## Call Mode (future P2)

Call Mode is a Practice Gym mode where the AI speaks first and the student responds by voice across multiple turns.

It is inspired by the phone-style AI conversation experience from apps like Langua and TalkPal, but focused entirely on workplace scenarios.

Call Mode is **not** a guided lesson session type. It belongs in the Practice Gym.

Example Call Mode scenarios:
- Manager calls to ask for a project status update
- Customer complaint call
- Mock job interview
- Meeting follow-up call
- Explaining a delay by phone

Call Mode uses `call_mode_single_turn` or `call_mode_multi_turn` exercise patterns.

Post-call output:
- full transcript
- per-turn coaching feedback
- vocabulary suggestions
- tone summary
- score

Call Mode requires real STT (Speech-to-Text) integration. It is not feasible with the fake STT provider. It is deferred to a future sprint after real STT is wired.

---

## Compatibility with Practice Gym

The Practice Gym continues to work through the existing `/activity?type=...` route.

Practice Gym activities are not part of any session and are not assigned an `ExercisePattern`.

They contribute to `ActivityAttempt` history and skill tracking.

They do not advance the guided course.

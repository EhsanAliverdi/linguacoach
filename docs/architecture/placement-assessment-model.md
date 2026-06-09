---
status: current
lastUpdated: 2026-06-09 13:11
owner: architecture
supersedes:
supersededBy:
---

# Placement Assessment Model

## Purpose

SpeakPath must not rely on self-reported CEFR levels.

After basic onboarding, every new student completes a structured placement assessment before their guided course begins. The placement result determines:

- estimated overall CEFR level
- per-skill level breakdown
- strengths and weaknesses
- first course / module recommendation
- recommended session duration

Placement takes approximately 15–20 minutes and covers all core skills in a workplace context.

---

## Teaching Principles

The placement is inspired by placement-test pedagogy. It does not copy IELTS, PTE, or any branded assessment. It uses original workplace English scenarios.

Guiding principles:
- Tasks are realistic and career-relevant (not abstract test items)
- Each section is short (2–4 minutes)
- The student is never told their raw score during assessment
- AI evaluates each section and produces a holistic result
- The assessment is encouraging, not stressful
- **Placement assesses English proficiency only — it does not assess professional expertise**
- Tasks use `BasicWorkplace` or `JuniorRole` domain complexity by default
- Higher-complexity scenarios are only used if the student explicitly indicates 5+ years of experience, and even then the task prompt explains any advanced concepts
- A student must never score lower because of unfamiliar job knowledge

See: [professional-experience-domain-complexity.md](professional-experience-domain-complexity.md)

---

## Assessment Sections

### Section 1 — Quick Self-Check (1–2 minutes)

A short confidence and context questionnaire before any language tasks.

Collected data:
- self-estimated comfort level (1–5 scale): email writing, spoken explanations, reading documents, listening in meetings
- main workplace communication challenge (free text, short)
- optional: self-estimated CEFR level

This is not scored. It informs the AI's holistic judgement and adds qualitative context.

Note: `ProfessionalExperienceLevel` and `RoleFamiliarity` are collected during onboarding Stage 1, before placement begins. They are available to the placement AI prompt but are not re-collected here. The placement prompt uses them only to calibrate scenario domain complexity, not to score professional expertise.

### Section 2 — Vocabulary and Grammar Quick Check (3–4 minutes)

Pattern types used:
- `choose_best_phrase` (2–3 items)
- `collocation_match` (2–3 items)
- `error_correction` (1–2 items)

Items escalate from A2 to B2 difficulty to find the ceiling.

Scored: vocabulary accuracy, grammar accuracy.

### Section 3 — Reading a Workplace Message (3 minutes)

Pattern type: `read_and_answer`

Student reads a short email or Teams message (100–150 words) and answers 2–3 comprehension questions.

Scored: reading comprehension accuracy.

### Section 4 — Listening to a Workplace Message (3–4 minutes)

Pattern type: `listen_and_answer`

Student listens to a short workplace audio (manager update, brief instruction, 60–90 seconds) and answers 2–3 comprehension questions.

Scored: listening comprehension accuracy.

### Section 5 — Writing a Short Workplace Response (4–5 minutes)

Pattern type: `email_reply` or `teams_chat_simulation`

Student writes a response to a short workplace message. Word limit: 80–120 words.

Scored by AI: grammar, vocabulary range, tone, workplace appropriateness, message completeness.

### Section 6 — Speaking a Short Response (3–4 minutes)

Pattern type: `spoken_response_from_prompt`

Student records a 30–60 second spoken response to a workplace prompt (e.g. explain what you do at work, describe a recent task).

Scored by AI (via transcript): vocabulary range, clarity, sentence structure, workplace relevance.

### Section 7 — AI Placement Result (backend, not a student-facing section)

After all sections are submitted, the backend calls AI with compact section summaries.

AI returns a structured placement result.

---

## Entities

### PlacementAssessment

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| StudentProfileId | Guid | |
| Status | PlacementStatus | NotStarted, InProgress, Completed |
| StartedAtUtc | DateTime? | |
| CompletedAtUtc | DateTime? | |
| ResultJson | string? | Serialised PlacementResult |
| CreatedAtUtc | DateTime | |

### PlacementStatus enum

```
NotStarted
InProgress
Completed
```

### PlacementSection

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| PlacementAssessmentId | Guid | |
| SectionKey | string | e.g. "self_check", "vocab_grammar", "reading", "listening", "writing", "speaking" |
| Order | int | |
| Status | SectionStatus | |
| ResponseJson | string? | Student's raw responses |
| SectionScoreJson | string? | Per-section AI or deterministic score |
| CompletedAtUtc | DateTime? | |

### PlacementResult

Produced after all sections complete. Stored in `PlacementAssessment.ResultJson`.

```json
{
  "estimatedOverallLevel": "B1",
  "skillLevels": {
    "grammar": "A2+",
    "vocabulary": "B1",
    "listening": "B1",
    "reading": "B1+",
    "writing": "A2+",
    "speaking": "A2+",
    "workplaceTone": "A2"
  },
  "strengths": ["reading comprehension", "vocabulary recognition"],
  "weaknesses": ["formal tone in writing", "speaking clarity"],
  "recommendedStartingCourse": "Workplace Writing B1",
  "recommendedSessionDuration": 15,
  "placementNotes": "Student demonstrates strong passive vocabulary but limited active production."
}
```

---

## Integration with Learning Memory and Weekly Plan

After placement completes:

1. `StudentSkillProfile` is initialised with per-skill weak flags based on placement `skillLevels`.
2. `UserLearningSummary` is updated with initial strengths, weaknesses, and CEFR level.
3. The first `LearningPath` is generated using placement result as the seed context.
4. The student's `PreferredSessionDurationMinutes` (from onboarding) is used to pre-generate the first week's lesson sessions.
5. The student's onboarding stage advances to `CourseReady`.
6. The Today page displays the first generated lesson session, anchoring the student's weekly habit immediately.

The `PlacementResult.recommendedSessionDuration` may override the student's onboarding preference if the placement evidence suggests a different duration is more appropriate. The student can adjust this preference later from their Profile page.

---

## Placement is Separate from the Guided LearningPath

**PlacementAssessment is not a LearningModule.**

The full lifecycle flow is:

```
StudentProfile
  → Onboarding
  → PlacementAssessment     ← separate entity, not a LearningModule
  → PlacementResult
  → Generate first LearningPath / LearningModule / LearningSession
```

A `LearningPath` does not exist before placement completes. Placement generates the data needed to create it.

Placement **may be displayed** in the UI as a step before "My Course" (e.g. "Step 1: Find Your Level"), but it is persisted as a `PlacementAssessment` row — not as a `LearningModule` row.

Do not add `ModuleType = Placement` to `LearningModule`. That design is superseded.

Placement is not skippable. The student cannot access their guided course until `PlacementAssessment.Status = Completed`.

---

## Student Lifecycle after Placement

```
OnboardingRequired
  → PlacementRequired       (onboarding completed, placement not started)
    → PlacementInProgress
      → PlacementCompleted
        → CourseReady
```

The student cannot start their guided course until placement is completed.

They may access the Practice Gym during placement (optional product decision).

---

## AI Prompt Design

The placement AI prompt must:
- receive compact section summaries (not raw responses)
- receive student's self-check data
- receive career context
- return structured JSON matching `PlacementResult`
- not use IELTS/PTE/Cambridge band descriptors verbatim (use CEFR labels A1–C2 only)

The prompt template is stored in PostgreSQL as: `placement_assessment_evaluate`

---

## Handling Incomplete Placement

If the student exits mid-assessment:
- status remains `InProgress`
- completed sections are saved
- on return, the student continues from the first incomplete section
- if the student has only completed sections 1–3, AI can produce a partial result (lower confidence flag)

---

## Out of Scope

- Adaptive item selection (fixed section structure for now)
- Pronunciation scoring in placement
- Real-time speaking transcription (uses existing FakeSpeechToTextService in dev)
- Peer/teacher review of placement
- Re-taking placement (admin can reset to PlacementRequired via lifecycle reset)

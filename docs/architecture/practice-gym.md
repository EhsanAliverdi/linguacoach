---
status: current
lastUpdated: 2026-06-09 13:11
owner: architecture
supersedes:
supersededBy:
---

# Practice Gym

## Purpose

The Practice Gym is a secondary, on-demand practice space separate from the guided course.

While the guided course delivers structured lessons aligned to the student's placement, level, and career context, the Practice Gym lets students practise any skill on demand without following the lesson sequence.

---

## Position in Product

The Practice Gym is not the main product. The main product is the guided course (Today / My Course).

The Practice Gym exists because:
- Students sometimes want to practise one skill immediately (e.g. write an email before a real meeting)
- Vocabulary review should be available on demand
- Listening practice may be done independently of the lesson schedule
- It serves as a sandbox that keeps existing activity flows working

---

## Navigation Placement

In the future navigation model:

```
Today         ← primary (today's lesson)
My Course     ← guided course structure
Practice      ← Practice Gym (this section)
Vocabulary    ← vocabulary queue
Progress      ← skill progress
Profile       ← preferences
```

The Practice tab replaces the current activity-card dashboard layout.

---

## Contents of Practice Gym

| Skill | Activity Type | Status |
|---|---|---|
| Writing | WritingScenario | Live |
| Vocabulary | VocabularyPractice | Live |
| Listening | ListeningComprehension | Live |
| Speaking | SpeakingRolePlay | Live (MVP) |
| Reading | ReadingTask | Not started |
| Teams Chat | TeamsChatSimulation | Planned |
| Pronunciation | PronunciationPractice | Not started |

---

## How Existing Routes Are Preserved

The current `/activity?type=...` route continues to work as the Practice Gym entry point.

No changes to existing routing are required for the Practice Gym concept. The dashboard activity cards simply move under a Practice tab.

---

## Relationship to Guided Course

Practice Gym activities:
- are tracked in `ActivityAttempt`
- contribute to `StudentSkillProfile` and learning memory
- do not advance sessions or modules in the guided course
- may feed vocabulary into the student's vocabulary queue

Guided course activities:
- are always part of a `SessionExercise` within a `LearningSession`
- advance session progress
- are selected by the session generator based on teaching sequence

The two paths share the same `LearningActivity` and `ActivityAttempt` infrastructure.

---

## TeamsChatSimulation in Practice Gym

`TeamsChatSimulation` can be used in both:
- Practice Gym: student selects a Teams chat scenario on demand
- Guided lesson session: Teams chat step is included in a teaching sequence

In the Practice Gym, the student picks a workplace scenario topic and gets a Teams-style chat exercise.

The same content format and evaluation logic is reused in both contexts.

---

## Out of Scope

- Practice Gym progress tracking separate from the course (all attempts go into the same history)
- Practice Gym recommendations or scheduling (ad-hoc only)
- Pronunciation in Practice Gym (still not started)
- Reading in Practice Gym (not started)

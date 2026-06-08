# Professional Experience Level & Domain Complexity

## The Problem

English level (CEFR) alone is not enough to select appropriate workplace tasks.

Two students can both be B1 English speakers but have completely different professional backgrounds:

- B1 English + junior software engineer (6 months in role)
- B1 English + senior software engineer (10 years in role)

If SpeakPath gives both students the same workplace exercises, the junior student may fail or disengage — not because of English weakness, but because the exercise uses professional concepts they have never encountered.

**SpeakPath must teach English through workplace contexts, but must not accidentally test professional domain knowledge beyond the student's experience level.**

---

## Two Difficulty Dimensions

Every generated session and exercise must consider two independent dimensions:

| Dimension | What it measures | Source |
|---|---|---|
| `LanguageDifficulty` | English proficiency (CEFR level) | Placement assessment result |
| `DomainComplexity` | Workplace / professional complexity | Onboarding: experience level + role familiarity |

These dimensions are independent. A student can have high domain complexity and low language difficulty (experienced professional who is an early-stage English learner), or vice versa.

---

## DomainComplexity Levels

```
BasicWorkplace           — universal situations, no role-specific knowledge needed
JuniorRole               — entry tasks, clearly defined work, supervised context
IndependentContributor   — self-directed work, cross-team communication, moderate complexity
SeniorSpecialist         — strategic topics, risk, trade-offs, stakeholder management
LeadOrManager            — people management, delivery ownership, organisational influence
```

These are used by the session generator when selecting workplace scenario topics.

---

## New Onboarding Fields

### ProfessionalExperienceLevel

Collected during onboarding Stage 1 (basic preferences).

```
NoProfessionalExperience
EntryLevelOrGraduate
Junior_0_2Years
MidLevel_2_5Years
Senior_5_10Years
LeadOrManager_10PlusYears
```

Maps to `DomainComplexity` as follows:

| ProfessionalExperienceLevel | Default DomainComplexity |
|---|---|
| NoProfessionalExperience | BasicWorkplace |
| EntryLevelOrGraduate | BasicWorkplace |
| Junior_0_2Years | JuniorRole |
| MidLevel_2_5Years | IndependentContributor |
| Senior_5_10Years | SeniorSpecialist |
| LeadOrManager_10PlusYears | LeadOrManager |

This mapping is a starting default. Placement and learning memory may adjust it.

### RoleFamiliarity

Collected alongside ProfessionalExperienceLevel.

```
NewToRole
UnderstandsBasics
CurrentlyWorkingInRole
ExperiencedInRole
ManagesOrTrainsOthers
```

`RoleFamiliarity` refines domain scenario selection. A student who is `Senior_5_10Years` but `NewToRole` (e.g. a career changer) should receive `IndependentContributor`-level scenarios, not `SeniorSpecialist`.

Combined mapping:

| ProfessionalExperienceLevel | RoleFamiliarity | Effective DomainComplexity |
|---|---|---|
| Senior_5_10Years | NewToRole | IndependentContributor |
| Senior_5_10Years | UnderstandsBasics | IndependentContributor |
| Senior_5_10Years | CurrentlyWorkingInRole | SeniorSpecialist |
| MidLevel_2_5Years | NewToRole | JuniorRole |
| Junior_0_2Years | ManagesOrTrainsOthers | IndependentContributor |

---

## StudentProfile / CareerProfile Fields

The following fields should be present on `StudentProfile` or associated `CareerProfile`:

| Field | Type | Notes |
|---|---|---|
| CareerContext | string | e.g. "Document Controller" |
| TargetRole | string? | Role they are working toward, if different |
| Industry | string? | e.g. "Construction", "Software", "Finance" |
| ProfessionalExperienceLevel | enum | See above |
| RoleFamiliarity | enum | See above |
| WorkplaceSeniority | DomainComplexity | Computed from experience + familiarity |
| CommonWorkSituations | string? | JSON array of typical daily situations |
| AvoidedDomainComplexity | string? | JSON array of concept categories to avoid until explicitly taught |

`WorkplaceSeniority` is computed by the backend using the mapping table above. It is not directly set by the student.

`AvoidedDomainComplexity` is optionally populated from the session generator when a concept is used for the first time, marking it as "introduced" so it can be reused in later sessions.

---

## Domain Complexity by Role — Examples

### Junior Software Engineer (Junior_0_2Years / CurrentlyWorkingInRole)

**DomainComplexity:** `JuniorRole`

Appropriate scenario topics:
- asking for clarification about a ticket or task
- saying you are blocked
- giving a daily stand-up update
- confirming requirements with a colleague
- replying to code review feedback politely
- asking about task priority
- explaining a simple delay

Avoid until explicitly taught:
- architecture trade-offs
- stakeholder negotiation
- production rollback and incident response
- technical debt negotiation
- mentoring juniors
- estimation risk and variance

### Senior Software Engineer (Senior_5_10Years / CurrentlyWorkingInRole)

**DomainComplexity:** `SeniorSpecialist`

Appropriate scenario topics:
- explaining technical risk in plain language
- negotiating scope with a product manager
- updating stakeholders on delivery progress
- describing a production incident and resolution
- recommending a technical approach in a meeting
- discussing delivery trade-offs
- mentoring a junior colleague

### Junior Project Planner (Junior_0_2Years / CurrentlyWorkingInRole)

**DomainComplexity:** `JuniorRole`

Appropriate scenario topics:
- updating task progress in a meeting
- asking for missing dates from a subcontractor
- confirming meeting action items in writing
- explaining that a schedule item has changed

Avoid until explicitly taught:
- full baseline variance analysis
- contractual delay claims
- executive steering committee reporting
- recovery schedule negotiation

### Senior Document Controller (MidLevel_2_5Years / ExperiencedInRole)

**DomainComplexity:** `IndependentContributor`

Appropriate scenario topics:
- chasing a late submittal professionally
- explaining a revision history issue
- coordinating between design and construction teams
- escalating an unresolved document status
- writing a non-conformance note

---

## AI Prompt Rules

All AI generation prompts for lesson content must include both dimensions.

### Required prompt variables

```
{{CEFRLevel}}                     — e.g. "B1"
{{DomainComplexity}}              — e.g. "JuniorRole"
{{ProfessionalExperienceLevel}}   — e.g. "Junior_0_2Years"
{{RoleFamiliarity}}               — e.g. "CurrentlyWorkingInRole"
{{CareerContext}}                 — e.g. "Document Controller"
{{Industry}}                      — e.g. "Construction"
```

### Required prompt instruction (to add to all content generation prompts)

```
Do not introduce workplace concepts beyond the student's professional experience level
(DomainComplexity: {{DomainComplexity}}) unless the exercise explicitly teaches the concept
first in simple language via a micro-lesson.

If a workplace concept is necessary but may be unfamiliar to a {{ProfessionalExperienceLevel}}
professional, include a short explanation before asking the student to respond.

Prefer workplace situations that a {{ProfessionalExperienceLevel}} professional in the role of
{{CareerContext}} would encounter in their first year of working at this level.
```

This instruction must be added to:
- `activity_generate_writing`
- `activity_generate_listening`
- `activity_generate_speaking_roleplay`
- `placement_assessment_evaluate`
- `learning_path_generate`
- `learning_path_generate_adaptive`
- All future session/exercise content generation prompts

---

## Placement Assessment Behaviour

Placement assesses English proficiency, not professional expertise.

It must not score a student down for being unfamiliar with advanced workplace concepts.

Placement task selection rules:
- Default to `BasicWorkplace` / `JuniorRole` domain complexity for all assessment tasks
- Use domain-light scenarios (universal workplace situations: meetings, emails, updates, requests)
- Only use higher-complexity scenarios if the student explicitly indicates `Senior_5_10Years` or `LeadOrManager_10PlusYears` experience
- Even then, explain any complex concepts in the task prompt before asking the student to respond

Example: a listening task about a "production rollback" may confuse a junior developer. Placement should use "a system going down" phrasing instead.

---

## Session Generator Rules (addition)

The session generator must use `WorkplaceSeniority` (effective DomainComplexity) when selecting scenario topics for exercises.

Updated input set for session generator:

- CEFR level (from placement)
- skill weaknesses (from StudentSkillProfile)
- career context
- professional experience level
- role familiarity
- effective DomainComplexity (WorkplaceSeniority)
- preferred session duration
- learning memory (weaknesses, covered scenarios)
- vocabulary queue
- weekly plan slot

Pattern selection rule addition:

> When selecting a workplace scenario topic for any pattern, the topic's domain complexity must not exceed the student's `WorkplaceSeniority` by more than one level, unless the session includes a `micro_lesson_*` step that introduces the concept first.

---

## Practice Gym Domain Complexity Override

The Practice Gym can optionally allow the student to choose a difficulty mode:

| Mode | DomainComplexity used |
|---|---|
| Simple workplace | BasicWorkplace |
| Normal for my role (default) | Student's WorkplaceSeniority |
| Challenge me | One level above WorkplaceSeniority |

The default is always the student's `WorkplaceSeniority`. The override is opt-in and session-scoped (does not change the stored profile).

---

## Domain Complexity and Learning Memory

When a higher-complexity scenario is used for the first time (e.g. the student progresses from JuniorRole to IndependentContributor exercises):

1. A `micro_lesson_*` step is generated first to introduce the concept.
2. The concept is marked in `AvoidedDomainComplexity` as "introduced" so it can appear in later sessions without a micro lesson.
3. If the student performs well, the session generator may start including that concept at the appropriate frequency.
4. If the student struggles (low score on that exercise), the concept is flagged in `UserLearningSummary.RecurringMistakes` with a domain complexity note.

---

## Out of Scope

- Automatic levelling-up of DomainComplexity (this sprint is design only)
- Industry-specific domain complexity taxonomies (e.g. legal, medical — future backlog)
- Admin ability to override a student's DomainComplexity manually (future: admin student detail page)
- Student-facing display of their DomainComplexity level (internal concept only for now)

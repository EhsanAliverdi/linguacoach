# SpeakPath / LinguaCoach Agent Instructions

## Purpose

This file defines how AI coding agents such as Codex should work inside this repository.

Read this file before making changes.

The public product is **SpeakPath**.

The internal repository/project name may still contain **LinguaCoach**, especially in .NET project names, namespaces, migrations, and older docs. That is acceptable internally.

User-facing UI, page titles, product copy, emails, landing pages, and public documentation should use **SpeakPath**.

---

# 1. Product Identity

## Product Name

Public name:

```text
SpeakPath
```

Internal/repo name:

```text
LinguaCoach
```

Do not rename .NET projects, namespaces, migrations, or infrastructure unless explicitly requested.

Do rename user-facing text from LinguaCoach to SpeakPath where appropriate.

---

## Product Positioning

SpeakPath is not a generic language app.

SpeakPath is **professional communication coaching software for immigrant professionals**.

The first wedge is:

```text
Persian-speaking professionals in Australia improving workplace English.
```

The first role-specific path is:

```text
Document Controller / admin-document-control worker.
```

Core positioning:

```text
Professional dignity software, not a generic language app.
```

The user is not buying “English lessons”.

The user is buying the ability to:

```text
send workplace emails,
speak in meetings,
follow up professionally,
explain delays clearly,
ask for clarification politely,
and communicate at work without needing a colleague to fix every message.
```

---

## First User Persona

The first user is:

```text
A Persian-speaking immigrant professional in Australia,
around A2-B1 English level,
working or trying to work in an admin, project support, or document-control role,
who can survive daily English but struggles with professional workplace communication.
```

Specific pain:

```text
They ask colleagues, friends, Google Translate, or ChatGPT to fix important emails.
They feel embarrassed or less professional.
They avoid speaking in meetings.
They are worried their English makes them look less competent than they are.
```

SpeakPath should help them practise real workplace communication.

---

# 2. Product Promise

The product promise is:

```text
Practise the workplace message before you send it.
Sound clear, polite, and professional.
Build confidence for real work situations.
```

Good product copy examples:

```text
Practise the email before you send it.
Sound clear, polite, and professional.
Build confidence for real workplace situations.
Your next practice: follow up a pending document approval.
Learn the phrases you actually need at work.
Stop depending on colleagues to fix every message.
```

Avoid generic copy:

```text
Learn English fast.
AI-powered English learning.
Improve your grammar with AI.
Chat with your AI tutor.
Become fluent in 30 days.
```

---

# 3. Tech Stack

Backend:

```text
.NET 10 Web API
ASP.NET Identity
JWT authentication
Entity Framework Core
PostgreSQL
Clean Architecture
Modular Monolith
```

Frontend:

```text
Angular
Tailwind CSS
Mobile-first responsive UI
```

Infrastructure:

```text
Docker
Docker Compose
GitHub Actions
VPS deployment
speakpath.app
```

AI:

```text
AI provider abstraction
Prompt templates
Token budgets
AI usage logging
Small context packets only
```

---

# 4. Architecture Rules

## Clean Architecture

Maintain the current architecture:

```text
src/LinguaCoach.Domain
src/LinguaCoach.Application
src/LinguaCoach.Infrastructure
src/LinguaCoach.Persistence
src/LinguaCoach.Api
src/LinguaCoach.Worker
src/LinguaCoach.Web
```

Rules:

```text
Domain must not depend on EF Core.
Domain must not depend on ASP.NET Identity.
Domain must not depend on Infrastructure.
Domain must not depend on API.
Application defines interfaces and use cases.
Infrastructure implements external providers.
Persistence owns EF Core, Identity mapping, migrations, and database configuration.
API owns controllers, middleware, auth wiring, CORS, health checks, and HTTP concerns.
Angular owns UI only.
```

Do not put business logic in controllers.

Do not put EF Core attributes in Domain entities.

Do not add infrastructure dependencies into Domain.

---

## Domain Model Rules

Domain entities should enforce meaningful invariants.

Examples:

```text
StudentProfile should not mutate after onboarding is complete.
LanguagePair should not allow same source and target language.
VocabularyEntry should protect valid mastery states and counters.
SpeakingSession should protect valid state transitions.
AiPrompt should enforce token budget validity.
```

Domain exceptions must be handled by the API layer and should not become uncontrolled 500 responses.

---

## Persistence Rules

Persistence uses PostgreSQL.

Use EF Core configurations in Persistence.

Use deterministic seed data where needed.

Do not edit old migrations unless there is a strong reason. Prefer additive migrations.

Seed data is allowed for the initial product slice:

```text
Persian language
English language
Persian → English language pair
Workplace English learning track
Document Controller career profile
Initial writing exercise prompt/template
```

Persian and English are allowed as seed data, but do not hard-code Persian or English as assumptions in business logic.

---

# 5. Language-Pair Architecture

The MVP is:

```text
Persian → English
```

But the architecture must support:

```text
Any source language → any target language
```

Do not hard-code:

```text
IsPersianUser
LearningEnglish
PersianEnglishOnly
```

Prefer:

```text
SourceLanguage
TargetLanguage
LanguagePair
SourceLanguageDirection
TargetLanguageDirection
```

Prompt variables should use:

```text
{{SourceLanguageName}}
{{TargetLanguageName}}
{{SourceLanguageCode}}
{{TargetLanguageCode}}
```

UI should be ready for:

```text
Source language: RTL or LTR
Target language: RTL or LTR
Mixed Persian/English content
```

Do not overbuild full i18n unless explicitly requested.

---

# 6. AI Architecture Rules

AI is expensive and must be controlled.

AI must not be the system planner.

The backend owns:

```text
lesson selection
vocabulary selection
review scheduling
progress calculation
context reduction
scenario selection
token budget enforcement
```

AI should only:

```text
generate or evaluate one small task at a time
return structured JSON
explain or correct user text
```

Correct flow:

```text
PostgreSQL
→ LearningPlanner
→ AiContextBuilder
→ IAiProvider
→ validated JSON response
→ saved result
→ UI display
```

Incorrect flow:

```text
Send full user history to AI and ask it what to do next.
```

---

## AI Context Rules

Do not send full history.

Do not send full vocabulary list.

Do not send all previous attempts.

Do not send full curriculum.

Send compact packets only.

Example writing feedback context:

```json
{
  "sourceLanguage": "Persian",
  "targetLanguage": "English",
  "careerProfile": "Document Controller",
  "level": "A2",
  "scenario": "Follow up pending document approval",
  "targetVocabulary": [
    "pending approval",
    "revised version",
    "could you please review"
  ],
  "recentWeaknesses": [
    "too direct tone",
    "confuses approve and approval"
  ],
  "userDraft": "..."
}
```

AI context should be bounded by token budgets.

---

## AI Cost Rules

Every AI call must have:

```text
feature key
provider
model
input token count if available
output token count if available
estimated cost if available
duration
status
error if failed
user id where applicable
```

No AI call should happen silently.

No AI call should be added without considering:

```text
Can this be done by SQL/code?
Can this be cached?
Can this be generated once and reused?
Can this use a cheaper model?
Can the prompt be smaller?
```

Tests and CI must not depend on real AI providers.

Use fake/mock providers in tests.

Real provider must use environment variables/secrets only.

Never commit API keys.

---

# 7. Vocabulary and Learning Rules

Seeing a word is not the same as learning it.

Track different stages:

```text
New
Seen
Recognised
Practised
Weak
Learning
Mastered
Retired
```

Track separate skill dimensions:

```text
recognition
recall
usage
```

The backend should choose vocabulary using the database and LearningPlanner.

The AI should not randomly choose vocabulary from a large list.

A lesson should use a controlled mix:

```text
new words
weak/review words
mastered words in natural context
```

Avoid boring repetition, but allow intentional varied repetition.

Examples of good workplace vocabulary/phrases for Document Controller:

```text
pending approval
revised version
document register
could you please review
when you have a chance
the latest revision
submitted for review
awaiting confirmation
missing information
follow up today
```

---

# 8. Speaking and Conversation Rules

The product will eventually support speaking, but do not start with real-time voice.

MVP speaking strategy:

```text
push-to-talk
turn-based sessions
browser speech recognition where practical
browser text-to-speech where practical
no audio upload in MVP
no stored user audio in MVP
```

A speaking session should be structured:

```text
one scenario
one learning goal
max turns
target phrases
rubric
small context packet
structured JSON feedback
```

Do not build uncontrolled endless chat.

Do not send the full conversation history to AI.

Send only:

```text
scenario
current turn
previous turn summary
current transcript
target phrases
recent weakness summary
rubric
```

---

# 9. Product Quality Gate

A task is not complete just because tests pass.

A task is complete only when:

```text
The user flow works in a real browser or is clearly not browser-facing.
There are no obvious dead buttons.
Loading states exist.
Empty states exist.
Success states exist.
Error states are visible.
The UI is mobile-friendly.
The product copy is understandable to a first-time user.
Backend tests pass.
Angular tests/build pass if frontend changed.
No secrets are committed.
No scope creep beyond the approved task.
```

For browser-facing work, validate the actual flow.

Do not declare a frontend feature complete based only on unit tests.

---

# 10. Current Critical Flow

The current product must support this flow before roadmap features continue:

```text
Admin logs in
→ Admin creates student
→ Student logs in
→ Student changes temporary password
→ Student completes onboarding
→ Student reaches dashboard
→ Student starts writing exercise
→ Student submits draft
→ Student sees useful feedback
```

Do not continue roadmap features until this flow is stable, understandable, and demo-ready.

---

# 11. Current Rescue Sprint Priority

The current priority is product stabilisation and polish.

Do not add new product features until the existing product is usable.

Current rescue priorities:

```text
Fix blockers.
Stabilise the core flow.
Improve UX and copy.
Improve visual quality.
Add minimum E2E smoke test.
Prepare for first real user demo.
```

Do not continue with broad roadmap work until these are done.

---

# 12. Scope Rules

Do not add these unless explicitly requested:

```text
public registration
payments
organisations
real-time voice
audio upload
new AI providers
full admin AI management
full CEFR assessment
full spaced repetition product
large dashboard analytics
teacher/classroom features
mobile native app
```

Do not overbuild.

Do not add new architecture when a UI/UX or flow fix is needed.

Do not create a new framework inside the app.

---

# 13. UI/UX Rules

Use Tailwind CSS.

The UI should feel:

```text
modern SaaS
warm
trustworthy
professional
calm
mobile-first
language-learning focused
workplace-confidence focused
```

Avoid:

```text
raw Angular scaffold look
childish gamification
generic AI app vibes
dense enterprise dashboard clutter
vague filler copy
unstyled forms
dead-end pages
hidden errors
excessive gradients
emoji-heavy UI
```

Every page should answer:

```text
Where am I?
What should I do next?
Why does this matter?
What happens if something goes wrong?
```

---

## Design Direction

SpeakPath should feel like:

```text
a practical workplace communication coach
a modern SaaS product
a safe place to practise before speaking/writing at work
```

Not like:

```text
Duolingo clone
generic ChatGPT wrapper
corporate LMS
raw admin panel
```

Visual direction:

```text
warm neutral background
clean white cards
subtle borders
generous spacing
clear typography
professional accent colour
mobile-first layout
clear primary action
```

Use restrained colour.

Prefer:

```text
slate / zinc / stone neutrals
blue / indigo / teal / emerald accents
clear red/amber error states
clear green success states
```

Do not randomly mix many colours.

---

# 14. UI/UX Skill Usage

When doing UI, UX, design review, redesign, layout, component styling, landing page, dashboard, onboarding, admin UI, or form polish work, use the Codex skill:

```text
ui-ux-pro-max
```

However, generic visual recommendations from that skill must be filtered through SpeakPath product identity.

SpeakPath is not a childish language game and not a generic AI chatbot.

SpeakPath should feel:

```text
modern SaaS
warm
trustworthy
professional
mobile-first
focused on workplace communication confidence
designed for immigrant professionals
```

Avoid:

```text
childish gamification
excessive gradients
emoji-heavy UI
generic AI startup landing page copy
dense enterprise dashboard clutter
raw Angular scaffold styling
```

Critical flow to preserve:

```text
Admin creates student
→ Student logs in
→ Student changes password
→ Student completes onboarding
→ Dashboard
→ Writing exercise
→ Feedback
```

Any UI redesign must preserve and improve this flow.

---

# 15. Design System Minimum

When redesigning, prefer reusable patterns.

Minimum UI patterns:

```text
Page shell
Card
Button
Form field
Status message
Dashboard card
Page header
```

Button variants:

```text
primary
secondary
ghost
danger
disabled/loading
```

Status components:

```text
loading
error
success
empty
AI unavailable
permission denied
session expired
```

Forms should have:

```text
label
helper text
validation error
loading/disabled state
success state where appropriate
```

Dashboard should include:

```text
recommended next action
learning goal
career context
skill focus
progress placeholder
coming soon section
```

---

# 16. Page Expectations

## Public Landing Page

Must communicate:

```text
SpeakPath helps professionals practise workplace communication.
The first path is workplace English for Document Controllers.
It is role-specific, not generic English.
Access is admin-created during pilot/MVP.
```

Should include:

```text
hero
problem statement
how it works
first scenario preview
login CTA
```

---

## Login Page

Must:

```text
use SpeakPath branding
explain product in one sentence
show clear errors
not feel like an internal admin panel
```

---

## Admin Create Student

Must:

```text
explain that admin creates pilot users
show success clearly
show student credentials clearly if applicable
offer next step after creating student
validate fields
avoid losing created credentials before admin can copy them
```

---

## Change Password

Must:

```text
explain why password change is required
show validation
show success
guide to onboarding
```

---

## Onboarding

Must:

```text
explain why each step matters
show progress
allow recovery/back where practical
show selected values
avoid dead ends
work after refresh
```

---

## Dashboard

Must not be a generic menu.

Must show:

```text
user goal
career context
recommended next action
writing exercise CTA
progress/learning placeholder
what is coming later
```

---

## Writing Exercise

Must:

```text
show a realistic workplace scenario
explain task clearly
show target phrases/vocabulary
provide a comfortable writing area
prevent duplicate submission
show feedback in a structured readable way
handle missing AI configuration gracefully
```

---

# 17. Auth and Security Rules

Do not commit secrets.

JWT signing key must come from environment variables/secrets outside Development.

Production must fail startup if JWT key is missing, too short, or equals placeholder.

MustChangePassword must be enforced server-side before first real user.

Students with temporary passwords should not access onboarding, dashboard, reference data, writing exercise, or other protected app features until password is changed.

Admin-only endpoints must require Admin role.

Student endpoints must require authentication.

No public registration unless explicitly requested.

Invalid login should not leak whether user exists.

---

# 18. Deployment Rules

Domain:

```text
speakpath.app
```

Deployment should support:

```text
HTTPS
API health check
frontend route refresh
/api routing
Docker Compose
GitHub Actions
VPS deployment
```

Production checks should verify:

```text
web responds
/api or API route responds
/health returns actual API health, not SPA HTML
login route loads
unauthenticated protected API returns 401, not 500
```

Do not rely only on “workflow succeeded”.

---

# 19. Testing Rules

Use tests intentionally.

Backend:

```text
unit tests for domain and application logic
integration tests for API endpoints
tests for auth/authorization boundaries
tests for AI response parsing and validation
tests for missing AI config behaviour
```

Frontend:

```text
component tests for key forms
guard tests
interceptor tests
route behaviour tests where practical
```

E2E:

```text
At minimum, add a Playwright smoke journey before first real user.
```

Minimum E2E happy path:

```text
admin login
→ create student
→ student login
→ change password
→ onboarding
→ dashboard
→ writing exercise
→ submit draft
→ feedback
→ refresh
→ session remains valid
```

Do not overbuild test suites at the cost of product progress, but do not skip critical browser-flow validation.

---

# 20. Git and Commit Rules

Keep changes scoped.

Before editing, understand the current task.

After editing, report:

```text
changed files
what changed
tests run
build result
known risks
what was not done
```

Use clear commits.

Do not include local-only tool settings unless required.

Do not commit secrets.

Do not commit generated junk.

---

# 21. Review Rules

Use lightweight self-checks by default.

Run full review only for risky work:

```text
auth/security
AI provider/prompt changes
new migrations
major frontend flow
deployment
before ship
```

Lightweight self-check should verify:

```text
build passes
tests pass
scope is clean
architecture boundaries preserved
no secrets
no obvious user-flow breakage
```

---

# 22. Documentation Rules

Every major sprint must create or update a sprint document:

```text
docs/sprints/<sprint-name>.md
```

Every major architecture change must create or update an architecture document:

```text
docs/architecture/<topic>.md
```

These documents must be committed with the code so that future AI sessions (Claude, Codex, or other agents) can read them and continue from the same context without needing the full conversation history.

A sprint document must include:

```text
Sprint name
Current state
Product goal
Architecture decision
In scope
Out of scope
API changes
Prompt keys
DB changes
Frontend changes
Test plan
Implementation tasks
Risks and mitigations
Future follow-up items
```

An architecture document must explain:

```text
Why the component exists and what problem it solves
How it relates to other components
How it works (flow, not just names)
How it handles failure
How future extensions fit without breaking the design
Key invariants that must not be violated
```

Do not write documentation as an afterthought.

Write it before coding complex features.

---

# 23. Working With Existing Plans

Use these as source of truth:

```text
CLAUDE.md
AGENTS.md
docs/implementation-roadmap.md
README.md
approved product/design docs if present
current code
```

If roadmap and current product reality conflict, prioritise:

```text
working user flow
security
product quality
approved rescue sprint
```

Do not blindly continue old roadmap tasks if the current product is broken.

---

# 24. How To Start Any UI/UX Task

Before editing UI, produce:

```md
## UI/UX Audit

### Blockers

### Functional Issues

### UX/Design Issues

### Polish Issues

### Proposed Fix Order
```

Then ask for approval unless the user has already approved a specific phase.

For Phase 1/2 rescue work, fix blockers before visual polish.

---

# 25. How To Start Any Backend Task

Before editing backend, confirm:

```text
Which endpoint/service is affected?
Is this a product blocker or architecture work?
Does it touch auth/security?
Does it require a migration?
Does it affect API contracts?
What tests should change?
```

Do not change backend architecture for cosmetic frontend issues.

---

# 26. Current Product Rescue Instruction

The product has reached broad feature completion, but the actual deployed app was judged not demo-ready.

The current work mode is:

```text
Product Rescue / Stabilisation Sprint
```

This means:

```text
No new roadmap features.
Fix current product blockers.
Stabilise the first user flow.
Improve product clarity.
Improve UI quality.
Prepare for first real user/demo.
```

Approved Phase 1 blockers:

```text
Fix API health probes and /health routing.
Add dashboard cefrLevel response.
Restore sessions across refresh and clear auth consistently.
Enforce MustChangePassword server-side.
Validate AI configuration and show controlled unavailable states.
```

After Phase 1, continue only with approval.

---

# 27. Final Principle

Do not optimise for “tasks completed”.

Optimise for:

```text
A real user can understand it.
A real user can complete the flow.
A real user can get value.
The product feels credible.
The system remains safe and maintainable.
```

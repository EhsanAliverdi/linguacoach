## gstack (REQUIRED — global install)

**Before doing ANY work, verify gstack is installed:**

```bash
test -d ~/.claude/skills/gstack/bin && echo "GSTACK_OK" || echo "GSTACK_MISSING"
```

If GSTACK_MISSING: STOP. Do not proceed. Tell the user:

> gstack is required for all AI-assisted work in this repo.
> Install it:
> ```bash
> git clone --depth 1 https://github.com/garrytan/gstack.git ~/.claude/skills/gstack
> cd ~/.claude/skills/gstack && ./setup --team
> ```
> Then restart your AI coding tool.

Do not skip skills, ignore gstack errors, or work around missing gstack.

Using gstack skills: After install, skills like /qa, /ship, /review, /investigate,
and /browse are available. Use /browse for all web browsing.
Use ~/.claude/skills/gstack/... for gstack file paths (the global path).

**Never use `mcp__claude-in-chrome__*` tools. Use `/browse` from gstack for all web browsing.**

### Available gstack skills

- `/office-hours`
- `/plan-ceo-review`
- `/plan-eng-review`
- `/plan-design-review`
- `/design-consultation`
- `/design-shotgun`
- `/design-html`
- `/review`
- `/ship`
- `/land-and-deploy`
- `/canary`
- `/benchmark`
- `/browse`
- `/connect-chrome`
- `/qa`
- `/qa-only`
- `/design-review`
- `/setup-browser-cookies`
- `/setup-deploy`
- `/setup-gbrain`
- `/retro`
- `/investigate`
- `/document-release`
- `/document-generate`
- `/codex`
- `/cso`
- `/autoplan`
- `/plan-devex-review`
- `/devex-review`
- `/careful`
- `/freeze`
- `/guard`
- `/unfreeze`
- `/gstack-upgrade`
- `/learn`

---

## Project: LinguaCoach

LinguaCoach is an AI-powered language learning SaaS platform.

### MVP

- Persian speakers learning English.

### Future architecture

- Any source language to any target language.
- Do not hard-code Persian or English into domain logic.
- Use `SourceLanguage`, `TargetLanguage`, `LanguagePair`, `SourceLanguageDirection`, and `TargetLanguageDirection`.

### Tech stack

- **Backend:** .NET 10 Web API
- **Frontend:** Angular
- **Database:** PostgreSQL
- **UI:** Tailwind CSS
- **Architecture:** Clean Architecture + Modular Monolith
- **Deployment:** Docker + CI/CD
- **AI workflow:** gstack

### Important product requirements

- Browser-based and mobile-first.
- Admin creates users. No public registration initially.
- Student logs in and completes onboarding.
- Student selects source language and target language.
- Initial seeded language pair: Persian to English.
- Student selects learning goal, career context, and skill focus.
- Example: Workplace English for a Document Controller.
- AI tests CEFR level.
- System tracks skill progress, mistakes, topic mastery, spaced repetition, progress snapshots, and AI usage/cost.
- Admin manages AI providers, model assignments, prompt templates, learning tracks, and career profiles.
- AI providers must be abstracted.
- AI prompts must be stored in PostgreSQL and versioned.
- AI usage and cost must be logged from day one.

### Architecture rules

- Use Clean Architecture.
- Do not put business logic in API controllers.
- Domain must not depend on EF Core.
- Application defines interfaces.
- Infrastructure implements external services.
- Persistence owns EF Core and PostgreSQL migrations.
- Use PostgreSQL from day one.
- Use Tailwind CSS for the Angular UI.
- Use Docker and CI/CD from day one.
- Do not create a random structure manually. First use gstack planning.

## Skill routing

When the user's request matches an available skill, invoke it via the Skill tool. When in doubt, invoke the skill.

Key routing rules:
- Product ideas/brainstorming → invoke /office-hours
- Strategy/scope → invoke /plan-ceo-review
- Architecture → invoke /plan-eng-review
- Design system/plan review → invoke /design-consultation or /plan-design-review
- Full review pipeline → invoke /autoplan
- Bugs/errors → invoke /investigate
- QA/testing site behavior → invoke /qa or /qa-only
- Code review/diff check → invoke /review
- Visual polish → invoke /design-review
- Ship/deploy/PR → invoke /ship or /land-and-deploy
- Save progress → invoke /context-save
- Resume context → invoke /context-restore
- Author a backlog-ready spec/issue → invoke /spec

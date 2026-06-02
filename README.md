# LinguaCoach

AI-powered language learning SaaS — role-specific workplace English for immigrant professionals.

**Current status:** Month 1 skeleton complete (T1–T6). Backend API + Angular frontend running end-to-end.

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Node.js | 22.x |
| npm | 10.x+ |
| Docker Desktop | 4.x+ |
| PostgreSQL (optional) | 17 (provided by Docker Compose) |

---

## Running with Docker Compose (recommended)

This starts the API and a PostgreSQL database together. EF Core migrations run automatically on API startup.

```bash
# Optional: set a real JWT key (required outside Development)
export JWT_KEY="your-secret-key-at-least-32-chars"

docker compose up --build
```

| Service | URL |
|---------|-----|
| API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| OpenAPI (Development only) | http://localhost:8080/openapi/v1.json |
| PostgreSQL | localhost:5433 (host port; 5432 inside Docker network) |

To stop and remove volumes:

```bash
docker compose down -v
```

---

## Running locally without Docker

### 1. Start PostgreSQL

You need a running PostgreSQL instance. The default connection string expects:

```
Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres
```

Override via environment variable or `appsettings.Development.json`:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=linguacoach_dev;Username=postgres;Password=postgres"
```

### 2. Run the API

```bash
cd src/LinguaCoach.Api
dotnet run
```

EF Core migrations run automatically on startup. The API listens on `https://localhost:7xxx` and `http://localhost:5xxx` (ports shown in terminal output).

---

## Running tests

Tests use an in-memory SQLite database — no PostgreSQL required.

```bash
dotnet test
```

Expected output: **251 tests, 0 failures** (162 unit + 89 integration).

---

## Environment variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | Yes (non-Docker) | localhost dev string in `appsettings.json` | PostgreSQL connection string |
| `JWT_KEY` | Yes (non-Development) | Placeholder in `appsettings.json` | JWT signing secret — must be ≥ 32 chars and not the placeholder outside Development |
| `Jwt__Issuer` | No | `linguacoach` | JWT issuer |
| `Jwt__Audience` | No | `linguacoach` | JWT audience |
| `Jwt__ExpiryHours` | No | `24` | JWT access token lifetime in hours |
| `AI__WritingFeedback__Provider` | Yes for production writing feedback | Development only: `OpenAI` | `OpenAI` or `Gemini` |
| `AI__WritingFeedback__Model` | Yes for production writing feedback | Development only: `gpt-4o-mini` | Model for writing feedback, for example `gpt-4o-mini` or `gemini-2.0-flash` |
| `OPENAI_API_KEY` | Yes when OpenAI is selected | None | OpenAI API key for writing feedback |
| `GEMINI_API_KEY` | Yes when Gemini is selected | None | Gemini API key for writing feedback |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` in Docker, `Development` locally | Controls OpenAPI exposure and JWT placeholder guard |

> **Security:** The `appsettings.json` JWT key (`CHANGE_ME_IN_PRODUCTION_USE_A_SECRET_AT_LEAST_32_CHARS`) is valid only in `Development`. The API refuses to start with that placeholder in any other environment.

---

## Project structure

```
src/
  LinguaCoach.Domain/          Domain entities, enums, exceptions — no EF/Identity dependency
  LinguaCoach.Application/     Interfaces and use-case contracts
  LinguaCoach.Persistence/     EF Core, migrations, Identity, PostgreSQL
  LinguaCoach.Infrastructure/  JWT, handlers, no-op STT/TTS stubs
  LinguaCoach.Api/             Controllers, Program.cs
tests/
  LinguaCoach.UnitTests/       Domain logic tests (FluentAssertions + xUnit)
  LinguaCoach.IntegrationTests/ API endpoint tests (WebApplicationFactory + SQLite)
docs/
  implementation-roadmap.md   T1–T12 task plan
```

---

## API endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/login` | None | Login → JWT token |
| POST | `/api/auth/change-password` | JWT | Change password (clears MustChangePassword) |
| POST | `/api/admin/students` | Admin JWT | Create student account |
| PATCH | `/api/onboarding` | JWT | Submit one onboarding step |
| GET | `/api/onboarding/status` | JWT | Current onboarding state |
| GET | `/api/dashboard` | JWT | Dashboard (requires completed onboarding) |
| GET | `/api/reference/language-pairs` | JWT | Active language pairs |
| GET | `/api/reference/tracks?languagePairId=` | JWT | Learning tracks for a language pair |
| GET | `/api/reference/career-profiles?languagePairId=` | JWT | Career profiles for a language pair |
| GET | `/health` | None | Health check |

---

## Running the Angular frontend

The frontend lives in `src/LinguaCoach.Web`. It runs on `http://localhost:4200` and calls the API at `http://localhost:5038`.

```bash
# Terminal 1 — start the API (with local PostgreSQL running)
cd src/LinguaCoach.Api
dotnet run

# Terminal 2 — start the Angular dev server
cd src/LinguaCoach.Web
npm install    # first time only
npm start
```

Open `http://localhost:4200` in your browser. The API must be running for the login to work.

> The API HTTP port is `5038` (see `src/LinguaCoach.Api/Properties/launchSettings.json`). If your setup uses a different port, update `src/LinguaCoach.Web/src/environments/environment.ts`.

### First-time flow

1. Start both API and Angular.
2. Log in at `http://localhost:4200/login` with the seeded admin credentials:
   - Docker Compose defaults: `admin@linguacoach.local` / `Admin1234!`
   - Production: values supplied through `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD`
3. Open **Students** and select **Create student**. Give the student their email and temporary password.
4. Log out, then log in with the student credentials.
5. Change the temporary password when prompted.
6. Complete onboarding (4 steps).
7. Reach the dashboard and start the assessment, writing, or speaking flow.

### AI configuration

Writing feedback can use OpenAI or Gemini. Configure the provider and model through environment
variables or .NET configuration. API keys must come from environment variables or secrets; they are
not stored in PostgreSQL.

OpenAI local example:

```bash
export AI__WritingFeedback__Provider="OpenAI"
export AI__WritingFeedback__Model="gpt-4o-mini"
export OPENAI_API_KEY="your-openai-key"
```

Gemini local example:

```bash
export AI__WritingFeedback__Provider="Gemini"
export AI__WritingFeedback__Model="gemini-2.0-flash"
export GEMINI_API_KEY="your-gemini-key"
```

In Development only, missing provider/model defaults to `OpenAI` / `gpt-4o-mini`. In Production,
missing provider, model, or the selected provider API key returns a controlled AI-unavailable response
instead of a generic server error.

Production uses the same secret names in `/opt/linguacoach/.env` or a secrets manager:

```text
AI__WritingFeedback__Provider=Gemini
AI__WritingFeedback__Model=gemini-2.0-flash
GEMINI_API_KEY=your-gemini-key
OPENAI_API_KEY=your-openai-key-if-using-openai
```

The GitHub deployment currently reads production secrets from the VPS `.env` file through
`docker-compose.prod.yml`. Do not commit API keys.

The existing admin AI Config screen is not the source of truth for the MVP writing feedback provider.
Full provider management UI is intentionally deferred.

---

## CI/CD

GitHub Actions runs on every push to `main` and on pull requests:
- `dotnet restore`
- `dotnet build`
- `dotnet test`

Angular build step and deployment are placeholders until T6 and the Fly.io target are confirmed. See `.github/workflows/ci.yml`.

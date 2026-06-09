---
status: current
lastUpdated: 2026-06-09 13:56
owner: deployment
supersedes:
supersededBy:
---

# Production AI feedback configuration

SpeakPath writing feedback uses the existing AI provider abstraction and can run through OpenAI or Gemini.

## Required environment variables

Production writing feedback requires a selected provider and model:

```text
AI__WritingFeedback__Provider=OpenAI
AI__WritingFeedback__Model=gpt-4o-mini
```

or:

```text
AI__WritingFeedback__Provider=Gemini
AI__WritingFeedback__Model=gemini-2.0-flash
```

Then configure the matching API key:

```text
OPENAI_API_KEY
GEMINI_API_KEY
```

The API also accepts configuration keys `OpenAI:ApiKey` and `Gemini:ApiKey`, but production should use environment variables/secrets. API keys are not stored in the database.

Development can fall back to `OpenAI` / `gpt-4o-mini` if provider/model are missing. Production does not use provider/model defaults.

The startup seed ensures there is one active default writing feedback prompt when it is missing. This avoids requiring admin prompt setup before the first demo.

## Admin AI feature routing

Admin AI Config exposes one row for each active runtime feature key. Each row supports:

- primary provider
- primary model
- fallback provider
- fallback model
- fallback enabled toggle

Fallback routing is stored in `AiProviderConfig` and used by `AiExecutionService` / `AiProviderResolver` when the primary provider fails. API keys are never returned to the frontend; the provider catalog only returns whether a key is stored.

Current runtime feature keys include:

- `writing.exercise`
- `learning_path_generate`
- `learning_path_generate_adaptive`
- `activity_generate_writing`
- `activity_evaluate_writing`
- `activity_generate_listening`
- `activity_generate_speaking_roleplay`
- `activity_evaluate_speaking_roleplay`
- `vocabulary_extract_from_attempt`
- `student_memory_update`
- `placement_assessment_evaluate`

## VPS setup

The production compose file passes `OPENAI_API_KEY` from the VPS env file:

```text
/opt/linguacoach/.env
```

Add or update:

```text
AI__WritingFeedback__Provider=Gemini
AI__WritingFeedback__Model=gemini-2.0-flash
GEMINI_API_KEY=your_gemini_api_key_here
```

Then redeploy or restart the API container:

```bash
docker compose -f /opt/linguacoach/docker-compose.prod.yml --env-file /opt/linguacoach/.env up -d --remove-orphans
```

## GitHub deployment

The GitHub deployment copies `docker-compose.prod.yml` and runs Docker Compose with the VPS `.env` file. GitHub Actions does not need Gemini/OpenAI keys unless a future workflow writes the VPS `.env` file.

Keep keys in the VPS `.env` file, GitHub secrets, or a secrets manager. Do not commit them.

## Missing configuration behavior

If provider/model or the selected provider API key is missing:

- The API still starts.
- Writing exercise loading still works.
- Writing draft submission returns `503 Service Unavailable`.
- The response body includes:

```json
{
  "code": "ai_unavailable",
  "error": "AI feedback is not configured or is temporarily unavailable."
}
```

The frontend displays that message instead of a generic feedback failure.

# Production AI feedback configuration

SpeakPath writing feedback uses the existing OpenAI provider abstraction.

## Required environment variable

Production writing feedback requires:

```text
OPENAI_API_KEY
```

The API reads this value from either:

```text
OpenAI:ApiKey
OPENAI_API_KEY
```

If no model is assigned for writing feedback, the API uses the safe default:

```text
gpt-4o-mini
```

The startup seed also ensures there is one active default writing feedback prompt and one default `writing.exercise` provider config when they are missing. This avoids requiring admin AI setup before the first demo.

## VPS setup

The production compose file passes `OPENAI_API_KEY` from the VPS env file:

```text
/opt/linguacoach/.env
```

Add or update:

```text
OPENAI_API_KEY=your_openai_api_key_here
```

Then redeploy or restart the API container:

```bash
docker compose -f /opt/linguacoach/docker-compose.prod.yml --env-file /opt/linguacoach/.env up -d --remove-orphans
```

## GitHub deployment

The GitHub deployment copies `docker-compose.prod.yml` and runs Docker Compose with the VPS `.env` file. GitHub Actions does not need the OpenAI key unless a future workflow writes the VPS `.env` file.

Keep the key in the VPS `.env` file or a secrets manager. Do not commit it.

## Missing key behavior

If `OPENAI_API_KEY` is missing:

- The API still starts.
- Writing exercise loading still works.
- Writing draft submission returns `503 Service Unavailable`.
- The response body includes:

```json
{
  "code": "ai_unavailable",
  "error": "Writing feedback is temporarily unavailable while SpeakPath AI feedback is being configured. Your draft was not submitted. Please try again later."
}
```

The frontend displays that message instead of a generic feedback failure.

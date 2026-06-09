# Production Gemini Smoke Test

This document defines the manual smoke-test procedure for verifying that SpeakPath production works correctly with **Gemini-backed writing feedback**.

Use this test before allowing trusted tester access or after any AI configuration change in production.

**Important:** This is a manual verification procedure. Do not automate these steps in CI/CD. Real API keys must never be committed to version control.

---

## 1. Required VPS Environment Variables

These variables must exist on the VPS at `/opt/linguacoach/.env`.

### Must configure

```bash
# Gemini API key (REQUIRED)
GEMINI_API_KEY=AIza...  # Your actual Gemini API key

# AI provider selection (REQUIRED for Gemini)
AI__WritingFeedback__Provider=Gemini
AI__WritingFeedback__Model=gemini-2.5-flash   # or gemini-2.0-flash, gemini-2.5-pro, etc.
```

### Other required vars (should already exist)

```bash
JWT_KEY=<long-random-secret-at-least-32-chars>
DB_HOST=shared-postgres
DB_PORT=5432
DB_NAME=linguacoach
DB_USER=linguacoach
DB_PASSWORD=<password>
SEED_ADMIN_EMAIL=admin@speakpath.app
SEED_ADMIN_PASSWORD=<strong-password>
DOMAIN=speakpath.app
```

### Verification steps

```bash
ssh <vps-user>@speakpath.app
cat /opt/linguacoach/.env | grep -E "GEMINI|AI__Writing"
```

**Expected result:**
- `GEMINI_API_KEY` is set to a non-empty value starting with `AIza`
- `AI__WritingFeedback__Provider=Gemini`
- `AI__WritingFeedback__Model` is set to a valid Gemini model name (e.g., `gemini-2.5-flash`)

---

## 2. Admin AI Config UI Steps

### Login as admin

1. Navigate to `https://speakpath.app/login`
2. Login with `admin@speakpath.app` credentials

### Navigate to Admin AI Config

1. Click "Admin" in navigation
2. Click "AI Config" link (or navigate to `/admin/ai-config`)

### Verify Gemini provider appears in catalog

- Check that "gemini" appears in the provider list
- Check that Gemini models are listed: `gemini-2.5-pro`, `gemini-2.5-flash`, `gemini-2.5-flash-lite`

### Configure feature routing to Gemini

1. In "Feature routing" section, find `writing.exercise`
2. Change provider dropdown to `gemini`
3. Change model dropdown to `gemini-2.5-flash` (or your chosen model)
4. Wait for auto-save confirmation ("Saved" text appears)

### Set Gemini API key via UI (optional but recommended)

1. In "Provider credentials" section, find Gemini row
2. Click "Set key" or "Update key"
3. Paste your Gemini API key
4. Click "Save"
5. Verify success message

**Expected result:**
- Feature routing shows `writing.exercise → gemini / gemini-2.5-flash`
- No error messages
- Database now has `AiProviderConfigs` entry with `FeatureKey='writing.exercise'`, `ProviderName='gemini'`, `ModelName='gemini-2.5-flash'`

---

## 3. Gemini Provider Test

### Run provider test

1. In Admin AI Config page, find Gemini provider row
2. Click "Test connection" button
3. Wait for test to complete (spins for ~10-30 seconds depending on model count)

**Expected result:**
- All Gemini models show green checkmarks ✅
- Each model shows response time in milliseconds
- No red X marks ❌
- Response times are reasonable (<5 seconds per model)

**If it fails:**
- Check browser DevTools Network tab for failed POST to `/api/admin/ai-providers/gemini/test`
- Check API logs (see Section 8 below)
- Verify API key is valid and not expired

---

## 4. Student Writing Feedback Test

### Admin creates a student (if not already done)

1. In Admin panel, go to "Create Student"
2. Enter email: `test-student@example.com`
3. Set temporary password: `TempPass123!`
4. Click "Create Student"
5. **Copy the credentials** shown

### Student logs in

1. Open incognito/private browser window
2. Navigate to `https://speakpath.app/login`
3. Login with student credentials

### Student changes password

- Follow the forced password change flow
- Set a new permanent password

### Student completes onboarding (if not already done)

1. Select source language: Persian
2. Select target language: English
3. Select career profile: Document Controller
4. Complete any other onboarding steps

### Student reaches dashboard

- Should see "Writing Exercise" CTA

### Student starts writing exercise

1. Click "Start Writing Exercise" or similar CTA
2. Verify scenario loads (e.g., "Follow up pending document approval")
3. Verify target vocabulary/phrases are shown

### Student submits draft

Write a realistic draft email (2-4 paragraphs). Example:

```
Hi John,

I want to know about the document you send last week. Please review it and tell me if it is ok or not.

Thanks
```

1. Click "Submit" or "Get Feedback"
2. Wait for AI response (~5-15 seconds)

### Verify feedback appears

Check that structured feedback displays with:
- Overall score (0-100)
- Corrected version
- Explanation of improvements
- Vocabulary suggestions
- Tone/politeness feedback

**Expected result:**
- Feedback appears within 10-20 seconds
- Feedback is structured and actionable
- Feedback references workplace communication best practices
- No error messages
- No generic "something went wrong" messages

---

## 5. AiUsageLog Verification

After successful submission (from Step 4), verify the database recorded the usage:

```sql
SELECT 
  id,
  student_profile_id,
  provider_name,
  model_name,
  input_tokens,
  output_tokens,
  estimated_cost_usd,
  created_at
FROM ai_usage_logs
ORDER BY created_at DESC
LIMIT 5;
```

**Expected result:**
- At least one new row exists
- `provider_name` = `'gemini'`
- `model_name` = `'gemini-2.5-flash'` (or whichever model you configured)
- `input_tokens` > 0 (typically 200-800 for writing exercise)
- `output_tokens` > 0 (typically 100-500 for feedback)
- `estimated_cost_usd` >= 0 (may be 0 if pricing not configured for this model)
- `created_at` is recent (within last few minutes)

---

## 6. Missing/Invalid Gemini Key Test

This test verifies that the system handles missing or invalid API keys gracefully without exposing sensitive information.

### Test A: Invalid API key

1. In Admin AI Config, update Gemini API key to an invalid value:
   - Click "Update key" for Gemini
   - Enter: `AIza_INVALID_KEY_12345`
   - Click "Save"

2. As student, submit another writing draft

**Expected result:**
- Frontend shows controlled error message:
  ```
  AI feedback is not configured or is temporarily unavailable.
  ```
- No raw exception details shown to user
- HTTP status code: `502 Bad Gateway` or `503 Service Unavailable`
- Response body includes:
  ```json
  {
    "code": "ai_unavailable",
    "error": "AI feedback is not configured or is temporarily unavailable.",
    "detail": "<technical detail>"
  }
  ```

### Test B: Remove API key entirely

1. On VPS, temporarily rename the env var:
   ```bash
   ssh <vps-user>@speakpath.app
   cd /opt/linguacoach
   # Edit .env and comment out GEMINI_API_KEY line
   nano .env
   # Restart API container
   docker compose -f docker-compose.prod.yml --env-file .env up -d api
   ```

2. Also remove DB-stored key (if set via Admin UI):
   ```sql
   DELETE FROM ai_provider_credentials WHERE provider_name = 'gemini';
   ```

3. As student, submit another writing draft

**Expected result:**
- Same controlled error message as Test A
- No stack trace or internal error exposed
- API logs show warning about missing API key

4. **Restore the key** after testing:
   ```bash
   # Restore GEMINI_API_KEY in .env
   docker compose -f docker-compose.prod.yml --env-file .env up -d api
   ```

---

## 7. API Key Exposure Checks

Verify that no API keys leak to the frontend or network traffic.

### Manual test steps

1. Open browser DevTools (F12)
2. Go to Network tab
3. As student, load the writing exercise page
4. Submit a draft
5. Inspect all network requests:
   - Check request/response headers
   - Check request/response bodies
   - Look for any field containing `AIza`, `sk-`, `key`, `apiKey`, etc.

6. Check JavaScript bundles:
   - In DevTools Sources tab, search for `AIza` or `gemini` in loaded JS files
   - Verify no API keys are hardcoded

**Expected result:**
- No API keys appear in any network request/response
- No API keys appear in JavaScript source
- API keys are only sent server-to-server (API → Gemini API)
- Frontend only receives structured feedback JSON, no credentials

---

## 8. Health/Canary Checks

Verify that core infrastructure endpoints remain healthy.

### Manual test steps

```bash
# Check health endpoint
curl -i https://speakpath.app/health

# Check API responds (requires authentication)
curl -i https://speakpath.app/api/writing/exercise \
  -H "Authorization: Bearer <valid-jwt-token>"

# Check unauthenticated protected endpoint returns 401
curl -i https://speakpath.app/api/writing/exercise

# Check login page loads
curl -i https://speakpath.app/login
```

**Expected results:**

| Endpoint | Expected Status | Expected Body |
|----------|----------------|---------------|
| `GET /health` | `200 OK` | Healthy status (not SPA HTML) |
| `GET /api/writing/exercise` (with auth) | `200 OK` | Writing exercise DTO |
| `GET /api/writing/exercise` (no auth) | `401 Unauthorized` | Error message |
| `GET /login` | `200 OK` | Login page HTML |

---

## 9. Logs and Debugging

### VPS-level logs

```bash
# SSH to VPS
ssh <vps-user>@speakpath.app

# View API container logs
docker logs linguacoach-api --tail 100 --follow

# View recent logs only
docker logs linguacoach-api --tail 50

# Search for specific errors
docker logs linguacoach-api 2>&1 | grep -i "gemini\|error\|exception"
```

### What to look for in logs

**Success indicators:**
```
Gemini call complete: key=writing.exercise.v1 input=XXX output=YYY cost=$0.000ZZZ
```

**Failure indicators:**
```
Gemini API key is not configured.
Gemini API returned 401/403/429/500 ...
Gemini response did not include text content.
AI writing feedback provider and model must be configured.
```

### Browser DevTools

- **Console tab**: Check for JavaScript errors
- **Network tab**: Check for failed API calls (red entries)
- Inspect request/response payloads for detailed error messages

---

## 10. Readiness Checklist

Before allowing first trusted tester, verify ALL of these:

### Critical blockers (MUST PASS)

- [ ] `GEMINI_API_KEY` is set in VPS `.env` or via Admin UI
- [ ] `AI__WritingFeedback__Provider=Gemini` is configured
- [ ] `AI__WritingFeedback__Model` is set to valid Gemini model
- [ ] Admin AI Config shows Gemini in provider catalog
- [ ] Feature routing shows `writing.exercise → gemini / <model>`
- [ ] Provider test passes for at least one Gemini model
- [ ] Student can submit writing draft and receive feedback
- [ ] Feedback is structured, readable, and actionable
- [ ] `AiUsageLog` records `provider_name='gemini'` and correct model
- [ ] Missing/invalid key produces controlled error (not crash/stack trace)
- [ ] No API keys exposed in frontend/network traffic
- [ ] `/health` returns 200 OK
- [ ] Login page loads
- [ ] Unauthenticated API calls return 401, not 500

### Product quality (SHOULD PASS)

- [ ] Writing exercise scenario is realistic workplace context
- [ ] Target vocabulary/phrases are shown before submission
- [ ] Feedback includes corrected version, explanation, and suggestions
- [ ] Loading states visible during AI call
- [ ] Error states are clear and actionable
- [ ] UI is mobile-friendly and responsive
- [ ] Product copy uses "SpeakPath" branding (not "LinguaCoach")
- [ ] No dead buttons or broken links

### Security (MUST PASS)

- [ ] No API keys committed to git
- [ ] No API keys in frontend JavaScript bundles
- [ ] No API keys in network request/response bodies
- [ ] JWT authentication enforced on protected endpoints
- [ ] Admin-only endpoints require Admin role
- [ ] Student credentials handled securely

---

## 11. Go/No-Go Decision

### Green light (proceed) if:

- ✅ All critical blockers pass
- ✅ All security checks pass
- ✅ At least 80% of product quality checks pass
- ✅ You have personally completed the full student flow end-to-end

### Yellow light (proceed with caution) if:

- ⚠️ Some product quality issues remain (but core flow works)
- ⚠️ Minor UX polish needed (but no dead ends)
- ⚠️ Some edge cases not yet tested

### Red light (do NOT proceed) if:

- ❌ Any critical blocker fails
- ❌ Any security check fails
- ❌ Core student flow is broken
- ❌ API crashes or exposes sensitive data

---

## Post-Test Actions

### If all tests pass:

1. Restore valid Gemini API key if you invalidated it during testing
2. Restart API container to ensure clean state
3. Document the working configuration
4. Invite first trusted tester with clear instructions
5. Monitor logs closely during first real usage

### If tests fail:

1. Identify root cause from logs
2. Fix configuration or code issue
3. Re-run relevant tests
4. Do NOT proceed until all critical blockers are resolved

---

## Valid Gemini Models

As defined in `AiProviderConfig.cs`:

- `gemini-2.5-pro`
- `gemini-2.5-flash`
- `gemini-2.5-flash-lite`
- `gemini-2.0-flash`
- `gemini-2.0-flash-lite`
- `gemini-1.5-pro`
- `gemini-1.5-flash`

**Recommended for production:** `gemini-2.5-flash` (good balance of quality and cost)

---

## Estimated Time to Complete Full Smoke Test

- Environment variable verification: 5 minutes
- Admin UI configuration: 10 minutes
- Provider test: 5 minutes
- Student flow end-to-end: 15 minutes
- Database verification: 5 minutes
- Error handling tests: 10 minutes
- Security verification: 10 minutes
- Health/canary checks: 5 minutes

**Total: ~65 minutes** (round up to 1.5 hours with buffer)

---

## Security Reminder

**Never commit real API keys or passwords to version control.**

This document contains placeholders such as `<vps-user>`, `<strong-password>`, and `<valid-jwt-token>` intentionally. Replace these with actual values only in your secure environment (VPS `.env` file, secrets manager, or GitHub secrets).

Real secrets must be managed through:
- VPS `.env` file (secured with proper file permissions)
- GitHub repository secrets (for CI/CD deployment)
- Secrets manager (if using one)

If you accidentally commit a secret:
1. Rotate the key immediately
2. Follow your incident response procedure
3. Update this document if needed

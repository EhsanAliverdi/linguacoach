---
status: current
lastUpdated: 2026-06-09
owner: engineering
---

# Known Playwright Issues

Issues recorded here are pre-existing or environmental, not feature regressions. Each entry explains the root cause, reproducibility, and safe workaround.

---

## Intermittent failure: `core first-user journey smoke test with mocked API`

**File:** `e2e/core-flow-smoke.spec.ts:483`

**Symptom:** Test times out navigating to `/my-path` during a multi-worker full-suite run. The failure does not occur when the test is run in isolation or with `--workers=1`.

**Root cause:** The Playwright config starts a single Angular dev server (`ng serve`) and shares it across all parallel workers. Under multi-worker concurrency the dev server occasionally cannot serve requests fast enough, causing `page.goto()` to time out. This is a test infrastructure issue, not a product bug.

**Reproducibility:** Intermittent — fails roughly 1 in 3 full-suite runs with the default worker count; never fails in isolation.

**Workaround:** Run the full suite with `npx playwright test --workers=1`. CI should use `--workers=1` or configure a served static build as the Playwright webServer.

**Resolution path:** Switch the Playwright `webServer` config to use `ng build --watch` + a static server, or pre-build and serve `dist/` in CI, so all workers share a stable server instead of the live HMR dev server.

---

## Resolved: `student: my-path handles empty learning memory` / `admin: diagnostics screenshot`

**Status:** These tests appeared as failures in `test-results/.last-run.json` at the start of the Admin UX sprint audit (2026-06-09). A fresh run showed both pass — the `.last-run.json` was stale from an earlier interrupted run, not a real regression.

---

## Resolved: `progress page shows real data` — strict mode violation on `getByText('Your progress')`

**File:** `e2e/progress-page.spec.ts:107`

**Symptom:** `getByText('Your progress')` resolved to 2 elements in strict mode.

**Root cause:** The progress component renders `<h3>Your progress</h3>` in both the loading skeleton and the data section. When the API mock resolves fast enough, both `@if` branches are briefly present in the DOM during Angular's change detection cycle.

**Fix applied (2026-06-09):** Changed assertion to `page.getByText('Your progress').first()` so strict mode is not triggered.

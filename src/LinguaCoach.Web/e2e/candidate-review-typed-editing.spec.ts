import { expect, test, Page } from '@playwright/test';

// ── Phase 4.5 — typed multimodal candidate schemas: focused browser coverage for the Candidate
// Review page's new type-aware editor and the edit → approve → publish flow. Entirely mocked at
// the network layer (page.route), matching import-stt-operations.spec.ts's convention — never
// calls the real backend or a real AI/STT provider. The genuine end-to-end backend flow (typed
// validation gate, publish into a real Resource Bank row, real persistence) is covered instead by
// AdminResourceCandidateTypedContentEndpointTests.cs (integration test, real HTTP + real SQLite).
// This spec proves the Angular UI wiring: correct typed form renders, edits are sent as
// TypedContentJson, and the approve/publish buttons react to server state. ──────────────────────

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const RUN_ID = 'run-typed-1';
const CANDIDATE_ID = 'cand-typed-1';

function candidateDto(overrides: Record<string, unknown> = {}) {
  return {
    candidateId: CANDIDATE_ID, resourceRawRecordId: 'raw-1', resourceImportRunId: RUN_ID, cefrResourceSourceId: 'source-1',
    candidateType: 'VocabularyEntry', canonicalText: 'hello', normalizedJson: '{"word":"hello"}', languageCode: 'en',
    cefrLevel: 'A1', cefrConfidence: null, primarySkill: null, subskill: null, difficultyBand: null,
    contextTagsJson: '[]', focusTagsJson: '[]', grammarTagsJson: null, vocabularyTagsJson: null,
    pronunciationTagsJson: null, activitySuitabilityTagsJson: null, safetyTagsJson: null, licenseTagsJson: null,
    qualityScore: null, contentFingerprint: 'fp-1', aiAnalysisJson: null, validationStatus: 'Passed',
    reviewStatus: 'PendingReview', rejectReason: null, adminNotes: null, createdAt: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', isPublished: false, publishedAtUtc: null, publishedEntityType: null,
    publishedEntityId: null, publishedByUserId: null, canAttemptPublish: true, publishBlockReason: null,
    typedContentJson: '{"word":"hello","definition":"a greeting"}', contentValidationErrors: [],
    ...overrides,
  };
}

async function mockAdmin(page: Page) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });

  await page.route(`**/api/admin/resource-import-runs/${RUN_ID}`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        runId: RUN_ID, cefrResourceSourceId: 'source-1', sourceName: 'Test Source', startedAtUtc: '2026-01-01T00:00:00Z',
        completedAtUtc: '2026-01-01T00:01:00Z', status: 'Completed', importedByUserId: null, importMode: 'Csv',
        fileName: 'words.csv', fileHash: 'hash', sourceVersion: null, parserVersion: '1', aiModelUsed: null,
        totalRecordCount: 1, succeededCount: 1, rejectedCount: 0, warningCount: 0, errorSummary: null, notes: null,
      }),
    });
  });

  let currentCandidate = candidateDto();

  await page.route(`**/api/admin/resource-candidates/summary**`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        totalCount: 1, publishedCount: 0, passedCount: 1, needsReviewCount: 0, blockedCount: 0,
        publishableCount: 1, rejectedCount: 0, skippedCount: 0, pendingReviewCount: 1,
      }),
    });
  });

  await page.route(`**/api/admin/resource-candidates?**`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ items: [currentCandidate], totalCount: 1, overallTotalCount: 1 }),
    });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/content`, async route => {
    const request = route.request();
    const body = request.postDataJSON() as { typedContentJson?: string };
    if (!body.typedContentJson || !JSON.parse(body.typedContentJson).word) {
      await route.fulfill({
        status: 400, contentType: 'application/json',
        body: JSON.stringify({ error: 'Candidate content failed typed schema validation', fieldErrors: [{ fieldName: 'word', message: 'Word is required.' }] }),
      });
      return;
    }
    const typed = JSON.parse(body.typedContentJson);
    currentCandidate = candidateDto({ typedContentJson: JSON.stringify(typed) });
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(currentCandidate) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/validate`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'Passed', errors: [], warnings: [] }) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/approve`, async route => {
    currentCandidate = candidateDto({ ...currentCandidate, reviewStatus: 'Approved' });
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(currentCandidate) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/publish`, async route => {
    currentCandidate = candidateDto({
      ...currentCandidate, isPublished: true, publishedEntityType: 'CefrVocabularyEntry', publishedEntityId: 'bank-item-1',
    });
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ success: true, publishedEntityType: 'CefrVocabularyEntry', publishedEntityId: 'bank-item-1', publishedAtUtc: '2026-01-01T00:02:00Z', errors: [] }),
    });
  });

  // Catch-all for other admin API calls the layout/shell makes on load.
  await page.route('**/api/admin/**', async route => {
    if (route.request().url().includes('/resource-candidates') || route.request().url().includes('/resource-import-runs')) {
      await route.fallback();
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
}

async function adminLogin(page: Page) {
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(/\/admin/, { timeout: 10000 });
  await page.waitForTimeout(600);
}

test('admin: typed Vocabulary editor edits, approves, and publishes a candidate', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/runs/${RUN_ID}`);

  await expect(page.getByText('hello', { exact: true }).first()).toBeVisible();

  // Open the row's dropdown actions and choose Edit.
  const trigger = page.getByRole('button', { name: 'Row actions' });
  await trigger.scrollIntoViewIfNeeded();
  await trigger.click();
  await page.locator('.sp-adm-actions-menu').waitFor({ state: 'visible' });
  await page.locator('.sp-adm-actions-menu .sp-adm-action-item', { hasText: 'Edit' }).click();

  // The typed Vocabulary form renders — Word/Definition fields, not a raw JSON textarea.
  await expect(page.getByText('Word', { exact: true })).toBeVisible();
  await expect(page.getByText('Definition', { exact: true })).toBeVisible();
  await expect(page.getByText('Content JSON')).toHaveCount(0);

  const wordField = page.locator('sp-admin-form-field:has-text("Word") textarea').first();
  await wordField.fill('greeting');
  const definitionField = page.locator('sp-admin-form-field:has-text("Definition") textarea').first();
  await definitionField.fill('a friendly hello');

  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText('Candidate content updated.')).toBeVisible();

  // Approve & Publish (the inline row button — enabled since content is now valid).
  await page.getByRole('button', { name: /Approve & Publish/ }).click();
  await expect(page.getByText(/Published as CefrVocabularyEntry/)).toBeVisible();
});

test('admin: an invalid typed edit shows the field-level server error and does not close the modal', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/runs/${RUN_ID}`);

  const trigger = page.getByRole('button', { name: 'Row actions' });
  await trigger.scrollIntoViewIfNeeded();
  await trigger.click();
  await page.locator('.sp-adm-actions-menu').waitFor({ state: 'visible' });
  await page.locator('.sp-adm-actions-menu .sp-adm-action-item', { hasText: 'Edit' }).click();

  const wordField = page.locator('sp-admin-form-field:has-text("Word") textarea').first();
  await wordField.fill('');

  await page.getByRole('button', { name: 'Save changes' }).click();

  await expect(page.getByText('Word is required.')).toBeVisible();
  await expect(page.getByText('Save changes')).toBeVisible(); // modal still open
});

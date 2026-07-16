import { expect, test, Page } from '@playwright/test';

// ── Phase 4.6 — media review and downstream discovery: focused browser coverage for the flow
// "open a Listening candidate → preview imported audio → review transcript → approve → publish →
// open the Resource Bank item → verify audio and transcript are visible." Entirely mocked at the
// network layer (page.route), matching candidate-review-typed-editing.spec.ts's/
// import-stt-operations.spec.ts's convention — never calls a real backend/storage. The genuine
// end-to-end backend flow (real HTTP, real SQLite, real publish, real Resource Bank query) is
// covered instead by AdminResourceBankMediaEndpointTests.cs (integration test, real API host). ──

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const RUN_ID = 'run-media-1';
const CANDIDATE_ID = 'cand-media-1';
const RESOURCE_ID = 'bank-media-1';

function candidateDto(overrides: Record<string, unknown> = {}) {
  return {
    candidateId: CANDIDATE_ID, resourceRawRecordId: 'raw-1', resourceImportRunId: RUN_ID, cefrResourceSourceId: 'source-1',
    candidateType: 'ListeningPassage', canonicalText: 'Morning News', normalizedJson: '{"title":"Morning News","transcript":"Good morning."}',
    languageCode: 'en', cefrLevel: 'A1', cefrConfidence: null, primarySkill: 'listening', subskill: null, difficultyBand: null,
    contextTagsJson: '[]', focusTagsJson: '[]', grammarTagsJson: null, vocabularyTagsJson: null,
    pronunciationTagsJson: null, activitySuitabilityTagsJson: null, safetyTagsJson: null, licenseTagsJson: null,
    qualityScore: null, contentFingerprint: 'fp-1', aiAnalysisJson: null, validationStatus: 'Passed',
    reviewStatus: 'PendingReview', rejectReason: null, adminNotes: null, createdAt: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', isPublished: false, publishedAtUtc: null, publishedEntityType: null,
    publishedEntityId: null, publishedByUserId: null, canAttemptPublish: true, publishBlockReason: null,
    typedContentJson: '{"title":"Morning News","transcript":"Good morning."}', contentValidationErrors: [],
    ...overrides,
  };
}

function previewDto() {
  return {
    candidateId: CANDIDATE_ID, candidateType: 'ListeningPassage', title: 'Morning News', languageCode: 'en',
    canonicalText: 'Morning News', normalizedContent: { title: 'Morning News', transcript: 'Good morning.' },
    renderedPreviewModel: { kind: 'ListeningPassage', title: 'Morning News', transcript: 'Good morning.', hasAudio: true },
    source: {
      sourceId: 'source-1', sourceName: 'Test Source', licenseType: 'CC-BY-4.0', sourceUrl: null, downloadUrl: null,
      attributionText: null, allowsStudentDisplay: true, allowsCommercialUse: true,
    },
    cefrLevel: 'A1', cefrConfidence: null, primarySkill: 'listening', subskill: null, difficultyBand: null,
    tags: { contextTags: [], focusTags: [], grammarTags: [], vocabularyTags: [], pronunciationTags: [], activitySuitabilityTags: [] },
    qualityScore: null, safetyIssues: [], validationStatus: 'Passed', validationErrors: [], validationWarnings: [],
    reviewStatus: 'PendingReview', contentFingerprint: 'fp-1', duplicateIndicators: [], aiAnalysisSummary: null,
    aiAnalysisDetailsJson: null,
    rawRecordSummary: { rawRecordId: 'raw-1', extractionStatus: 'Parsed', excerpt: 'audio-asset' },
    importRunSummary: { importRunId: RUN_ID, sourceId: 'source-1', startedAtUtc: '2026-01-01T00:00:00Z', completedAtUtc: '2026-01-01T00:01:00Z', status: 'Completed' },
    canPreview: true, previewWarnings: [], adminOnlyActivityMetadataJson: null,
    media: {
      state: 'Ok', fileName: 'audio.mp3', mediaType: 'audio/mpeg', sizeBytes: 45000, durationSeconds: 87.5,
      provenanceOrigin: 'AITranscribed', provenanceConfidence: 0.92, providerName: 'openai', modelName: 'whisper-1',
    },
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
        completedAtUtc: '2026-01-01T00:01:00Z', status: 'Completed', importedByUserId: null, importMode: 'Json',
        fileName: 'listening-assets', fileHash: 'hash', sourceVersion: null, parserVersion: '1', aiModelUsed: null,
        totalRecordCount: 1, succeededCount: 1, rejectedCount: 0, warningCount: 0, errorSummary: null, notes: null,
      }),
    });
  });

  let currentCandidate = candidateDto();
  let published = false;

  await page.route(`**/api/admin/resource-candidates/summary**`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        totalCount: 1, publishedCount: published ? 1 : 0, passedCount: 1, needsReviewCount: 0, blockedCount: 0,
        publishableCount: published ? 0 : 1, rejectedCount: 0, skippedCount: 0, pendingReviewCount: published ? 0 : 1,
      }),
    });
  });

  await page.route(`**/api/admin/resource-candidates?**`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ items: [currentCandidate], totalCount: 1, overallTotalCount: 1 }),
    });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/preview`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(previewDto()) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/audio-url`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ url: 'https://example.test/fake-signed-audio.mp3', expiresAt: '2026-01-01T01:00:00Z' }),
    });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/validate`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'Passed', errors: [], warnings: [] }) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/approve`, async route => {
    currentCandidate = candidateDto({ reviewStatus: 'Approved' });
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(currentCandidate) });
  });

  await page.route(`**/api/admin/resource-candidates/${CANDIDATE_ID}/publish`, async route => {
    published = true;
    currentCandidate = candidateDto({ reviewStatus: 'Approved', isPublished: true, publishedEntityType: 'CefrListeningPassage', publishedEntityId: RESOURCE_ID });
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ success: true, publishedEntityType: 'CefrListeningPassage', publishedEntityId: RESOURCE_ID, publishedAtUtc: '2026-01-01T02:00:00Z', errors: [] }),
    });
  });

  await page.route(`**/api/admin/resource-bank/${RESOURCE_ID}`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        id: RESOURCE_ID, type: 'listening', title: 'Morning News', summary: 'Good morning.',
        cefrLevel: 'A1', skill: 'Listening', subskill: null, contextTags: [], focusTags: [], difficultyBand: null,
        sourceId: 'source-1', sourceName: 'Test Source', contentFingerprint: 'fp-1', status: 'Published',
        createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T02:00:00Z', sourceTable: 'CefrListeningPassage',
        detailRoute: null, linkedLearnCount: 0, linkedActivityCount: 0, linkedModuleCount: 0, isArchived: false,
        hasAudio: true, audioContentType: 'audio/mpeg', audioDurationSeconds: 87.5, imageUrl: null,
      }),
    });
  });

  await page.route(`**/api/admin/resource-bank/${RESOURCE_ID}/diagnostics`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });

  await page.route(`**/api/admin/resource-bank/${RESOURCE_ID}/audio-url`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ url: 'https://example.test/fake-published-audio.mp3', expiresAt: '2026-01-01T03:00:00Z' }),
    });
  });

  // Catch-all for other admin API calls the layout/shell makes on load — matches
  // candidate-review-typed-editing.spec.ts's convention.
  await page.route('**/api/admin/**', async route => {
    const url = route.request().url();
    if (url.includes('/resource-candidates') || url.includes('/resource-import-runs') || url.includes('/resource-bank')) {
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

test.describe('Candidate Review media review and downstream Resource Bank discovery', () => {
  test('preview audio+transcript, approve, publish, then verify audio+transcript on the Resource Bank item', async ({ page }) => {
    await mockAdmin(page);
    await adminLogin(page);

    await page.goto(`/admin/content/import/runs/${RUN_ID}`);
    await expect(page.getByText('Morning News', { exact: true }).first()).toBeVisible();

    // Open the row's dropdown actions and choose Preview.
    const trigger = page.getByRole('button', { name: 'Row actions' });
    await trigger.scrollIntoViewIfNeeded();
    await trigger.click();
    await page.locator('.sp-adm-actions-menu').waitFor({ state: 'visible' });
    await page.locator('.sp-adm-actions-menu .sp-adm-action-item', { hasText: 'Preview' }).click();

    // The preview drawer shows the imported audio + transcript + media metadata.
    await expect(page.getByText('Good morning.')).toBeVisible();
    await expect(page.locator('audio')).toHaveCount(1);
    await expect(page.getByText(/audio\.mp3/)).toBeVisible();
    await expect(page.getByText(/87\.5s/)).toBeVisible();
    await page.getByRole('button', { name: /close/i }).first().click().catch(() => {});

    // Approve & Publish via the inline row action.
    const rowTrigger = page.getByRole('button', { name: 'Row actions' });
    await rowTrigger.click();
    await page.locator('.sp-adm-actions-menu').waitFor({ state: 'visible' });
    await page.locator('.sp-adm-actions-menu .sp-adm-action-item', { hasText: 'Approve & Publish' }).click();
    await expect(page.getByText(/Published as CefrListeningPassage/)).toBeVisible();

    // Navigate to the published Resource Bank item and verify audio + transcript are visible.
    await page.goto(`/admin/resource-bank/${RESOURCE_ID}`);
    await expect(page.getByText('Morning News').first()).toBeVisible();
    await expect(page.getByText('Good morning.').first()).toBeVisible();
    await expect(page.locator('audio')).toHaveCount(1);
    await expect(page.getByText(/87\.5s/).first()).toBeVisible();
  });
});

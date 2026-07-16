import { expect, test, Page } from '@playwright/test';

// ── Phase 4.4C — focused browser coverage for the STT operation ledger summary. Entirely mocked
// at the network layer (page.route) — never calls the real backend or a real AI/STT provider. ──

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const PACKAGE_ID = 'pkg-stt-1';
const PLAN_ID = 'plan-stt-1';

function baseEstimate() {
  return {
    detectedGroups: [
      { groupKey: '(root)', description: '1 file(s)', fileCount: 1, sampleRelativePaths: ['audio.mp3'], proposedResourceType: 'listeningPassage', confidence: 0.9 },
    ],
    ambiguousGroups: [], unsupportedContentNotes: [],
    volume: { totalFiles: 1, filesByExtension: { '.mp3': 1 }, expectedCandidateCount: 1, expectedAudioFilesRequiringStt: 1, estimatedAudioMinutesRequiringStt: 5, expectedTtsCandidates: 0, estimatedTtsCharacters: 0, expectedImageAnalysisCount: 0, unmatchedFileCount: 0 },
    time: { estimatedDurationRangeDescription: '1 min', estimatedMinMinutes: 1, estimatedMaxMinutes: 1, assumptions: '' },
    cost: { expectedCost: 0.03, minCost: 0.03, maxCost: 0.03, currency: 'USD', breakdown: [], assumptions: [], providerModelAssumptions: '' },
    risks: [], proposedDecisions: [], samplingRoundsUsed: 1, structureConfidence: 0.9, structuredMappingPreviews: [],
  };
}

const PLAN = {
  planId: PLAN_ID, importPackageId: PACKAGE_ID, version: 1, status: 'Completed',
  processingMode: 'Direct', processingModeReason: null, estimate: baseEstimate(),
  approvedCostCeiling: 1, createdAtUtc: '2026-01-01T00:00:00Z', approvedAtUtc: '2026-01-01T00:00:00Z',
  approvedByUserId: 'admin-1', rejectedAtUtc: null, rejectionReason: null,
  pauseReason: null, changeReason: null,
  concurrencyStamp: 'stamp-1', isEditable: false,
  groupInstructions: [{ groupKey: '(root)', included: true, resourceType: 'listeningPassage', fieldMappings: {}, sampleRelativePaths: ['audio.mp3'] }],
  accruedCost: 0.03, accruedCostCurrency: 'USD', remainingCeiling: 0.97, ceilingAmendments: [],
};

async function mockAdmin(page: Page) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/manifest`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        importPackageId: PACKAGE_ID, status: 'Completed', isAccepted: true, rejectionReason: null,
        compressedSizeBytes: 100, expandedSizeBytes: 100, entryCount: 1,
        folderGroups: [{ folderPath: '', fileCount: 1, extensions: ['.mp3'] }],
        distinctExtensions: ['.mp3'], duplicateChecksumEntryCount: 0, unsupportedEntryCount: 0, suspiciousEntryCount: 0,
      }),
    });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(PLAN) });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan/${PLAN_ID}/stt-operations`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([{
        operationId: 'op-1', assetFileName: 'audio.mp3', assetRelativePath: 'audio.mp3',
        providerName: 'openai', modelName: 'whisper-1', status: 'Succeeded', attemptNumber: 2,
        resultReusable: true, calculatedCost: 0.03, currency: 'USD',
        startedAtUtc: '2026-01-01T00:00:00Z', completedAtUtc: '2026-01-01T00:01:00Z', safeErrorMessage: null,
        measuredAudioDurationSeconds: 300, audioDurationMeasurementStatus: 'Measured',
      }]),
    });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan/${PLAN_ID}/ai-operations`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([{
        operationId: 'ai-op-1', resourceCandidateId: 'cand-1', sourceLabel: 'hello', operationType: 'candidate_enrich',
        providerName: 'openai', modelName: 'gpt-4o-mini', status: 'Succeeded', attemptNumber: 1,
        resultReusable: true, inputTokens: 100, outputTokens: 50, calculatedCost: 0.02, currency: 'USD',
        startedAtUtc: '2026-01-01T00:00:00Z', completedAtUtc: '2026-01-01T00:01:00Z', safeErrorMessage: null,
      }]),
    });
  });

  // Catch-all for other admin API calls the layout/shell makes on load (notifications, sidebar
  // counts, etc.) — falls through to the specific import-packages handlers above, otherwise
  // returns an empty/benign response so the shell renders without hitting the real backend.
  await page.route('**/api/admin/**', async route => {
    if (route.request().url().includes('/import-packages/')) {
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

test('admin: completed STT operation shows provider/model, cost, attempts, and reused state', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/packages/${PACKAGE_ID}/plan`);

  await expect(page.getByText('STT operations', { exact: true })).toBeVisible();
  await expect(page.getByText('audio.mp3').first()).toBeVisible();
  await expect(page.getByText(/openai.*whisper-1/)).toBeVisible();
  await expect(page.getByText('Reused on retry — no extra charge').first()).toBeVisible();
  await expect(page.getByRole('cell', { name: 'USD 0.03' })).toBeVisible();
});

test('admin: completed AI enrichment operation shows provider/model, tokens, cost, and reused state', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/packages/${PACKAGE_ID}/plan`);

  await expect(page.getByText('AI operations', { exact: true })).toBeVisible();
  await expect(page.getByText('hello').first()).toBeVisible();
  await expect(page.getByText(/openai.*gpt-4o-mini/)).toBeVisible();
  await expect(page.getByText('100 in / 50 out')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'USD 0.02' })).toBeVisible();
});

import { expect, test, Page } from '@playwright/test';

// ── Phase 4.4C — focused browser coverage for the audited cost-ceiling amendment flow. Entirely
// mocked at the network layer (page.route) — never calls the real backend, and therefore never
// calls a real AI/STT provider. Navigates straight to the plan page for a seeded fake package
// rather than driving the full upload flow, matching the brief's "seeded/fake data" instruction. ──

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const PACKAGE_ID = 'pkg-cost-1';
const PLAN_ID = 'plan-cost-1';

function baseEstimate() {
  return {
    detectedGroups: [
      { groupKey: '(root)', description: '2 file(s)', fileCount: 2, sampleRelativePaths: ['audio1.mp3', 'audio2.mp3'], proposedResourceType: 'listeningPassage', confidence: 0.9 },
    ],
    ambiguousGroups: [], unsupportedContentNotes: [],
    volume: { totalFiles: 2, filesByExtension: { '.mp3': 2 }, expectedCandidateCount: 2, expectedAudioFilesRequiringStt: 2, estimatedAudioMinutesRequiringStt: 10, expectedTtsCandidates: 0, estimatedTtsCharacters: 0, expectedImageAnalysisCount: 0, unmatchedFileCount: 0 },
    time: { estimatedDurationRangeDescription: '2 min', estimatedMinMinutes: 1, estimatedMaxMinutes: 2, assumptions: '' },
    cost: { expectedCost: 0.06, minCost: 0.03, maxCost: 0.09, currency: 'USD', breakdown: [], assumptions: [], providerModelAssumptions: '' },
    risks: [], proposedDecisions: [], samplingRoundsUsed: 1, structureConfidence: 0.9, structuredMappingPreviews: [],
  };
}

let planState: any;

function resetPlanState() {
  planState = {
    planId: PLAN_ID, importPackageId: PACKAGE_ID, version: 1, status: 'PausedForCostApproval',
    processingMode: 'Direct', processingModeReason: null, estimate: baseEstimate(),
    approvedCostCeiling: 0.03, createdAtUtc: '2026-01-01T00:00:00Z', approvedAtUtc: '2026-01-01T00:00:00Z',
    approvedByUserId: 'admin-1', rejectedAtUtc: null, rejectionReason: null,
    pauseReason: 'Projected cost would exceed the approved ceiling.', changeReason: null,
    concurrencyStamp: 'stamp-1', isEditable: false,
    groupInstructions: [{ groupKey: '(root)', included: true, resourceType: 'listeningPassage', fieldMappings: {}, sampleRelativePaths: ['audio1.mp3', 'audio2.mp3'] }],
    accruedCost: 0.03, accruedCostCurrency: 'USD', remainingCeiling: 0, ceilingAmendments: [],
  };
}

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
        importPackageId: PACKAGE_ID, status: 'AwaitingMappingApproval', isAccepted: true, rejectionReason: null,
        compressedSizeBytes: 200, expandedSizeBytes: 200, entryCount: 2,
        folderGroups: [{ folderPath: '', fileCount: 2, extensions: ['.mp3'] }],
        distinctExtensions: ['.mp3'], duplicateChecksumEntryCount: 0, unsupportedEntryCount: 0, suspiciousEntryCount: 0,
      }),
    });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan/${PLAN_ID}/stt-operations`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([{
        operationId: 'op-1', assetFileName: 'audio1.mp3', assetRelativePath: 'audio1.mp3',
        providerName: 'openai', modelName: 'whisper-1', status: 'Succeeded', attemptNumber: 1,
        resultReusable: true, calculatedCost: 0.03, currency: 'USD',
        startedAtUtc: '2026-01-01T00:00:00Z', completedAtUtc: '2026-01-01T00:01:00Z', safeErrorMessage: null,
      }]),
    });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan/${PLAN_ID}/amend-ceiling`, async route => {
    const body = route.request().postDataJSON();
    if (body.expectedConcurrencyStamp !== planState.concurrencyStamp) {
      await route.fulfill({
        status: 409, contentType: 'application/json',
        body: JSON.stringify({ error: 'This Import Execution Plan was changed by someone else — reload it before saving or approving again.', currentConcurrencyStamp: planState.concurrencyStamp }),
      });
      return;
    }
    if (body.newApprovedCostCeiling <= planState.approvedCostCeiling) {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ error: 'The new ceiling must be greater than the current approved ceiling.' }) });
      return;
    }
    planState = {
      ...planState,
      status: 'Executing', pauseReason: null, approvedCostCeiling: body.newApprovedCostCeiling,
      remainingCeiling: body.newApprovedCostCeiling - planState.accruedCost,
      concurrencyStamp: 'stamp-2',
      ceilingAmendments: [
        ...planState.ceilingAmendments,
        { amendmentId: 'amend-1', previousCeiling: 0.03, newCeiling: body.newApprovedCostCeiling, currency: 'USD', reason: body.reason, administratorUserId: 'admin-1', createdAtUtc: '2026-01-01T00:05:00Z' },
      ],
    };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(planState) });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan`, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(planState) });
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(planState) });
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

test.beforeEach(() => resetPlanState());

test('admin: cost-paused package shows accrued cost, ceiling, and pause reason', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/packages/${PACKAGE_ID}/plan`);

  await expect(page.getByText('PausedForCostApproval')).toBeVisible();
  await expect(page.getByText(/Projected cost would exceed/)).toBeVisible();
  await expect(page.getByText('Cost details', { exact: true })).toBeVisible();
});

test('admin: amend ceiling with reason resumes the package and shows amendment history', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.goto(`/admin/content/import/packages/${PACKAGE_ID}/plan`);

  await page.getByRole('button', { name: 'Amend ceiling and resume' }).first().click();
  await expect(page.getByRole('dialog', { name: 'Amend cost ceiling and resume' })).toBeVisible();

  await page.locator('input[placeholder="e.g. 75.00"]').fill('5');
  await page.locator('textarea[placeholder="Why does this package need a higher ceiling?"]').fill('Customer approved a higher budget.');
  await page.locator('sp-admin-modal').getByRole('button', { name: 'Amend ceiling and resume' }).click();

  await expect(page.getByText('Executing')).toBeVisible({ timeout: 10000 });
  await expect(page.getByText('Amendment history')).toBeVisible();
  await expect(page.getByText('Customer approved a higher budget.')).toBeVisible();
});

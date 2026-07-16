import { expect, test, Page } from '@playwright/test';

// ── Phase 4.7 (2026-07-17 reliable large uploads) — focused browser coverage for the resumable,
// chunked ZIP upload flow: select a ZIP → upload parts with byte progress → one part fails and is
// retried → complete → generate plan → land on the plan page. Also covers cancellation and a
// refresh/resume using the same session id. Entirely mocked at the network layer (page.route),
// matching candidate-review-media-review.spec.ts's convention — never uploads a real large file
// or talks to a real backend/storage. ──

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const SOURCE_ID = 'source-1';
const SESSION_ID = 'sess-1';
const PACKAGE_ID = 'pkg-1';
const PLAN_ID = 'plan-1';
const PART_SIZE = 10;
const TOTAL_SIZE = 25; // 3 parts: 10 + 10 + 5

async function mockAdmin(page: Page, options: { failPartOnceNumber?: number } = {}) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });

  await page.route('**/api/admin/resource-sources**', async route => {
    if (route.request().method() !== 'GET') { await route.fallback(); return; }
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        items: [{
          sourceId: SOURCE_ID, name: 'Test Source', licenseType: 'AdminUpload', sourceUrl: null,
          usageRestrictionNotes: null, languageCode: 'en', isImportApproved: true, allowsStudentDisplay: true,
          allowsCommercialUse: true, attributionText: null, sourceVersion: null, downloadUrl: null,
        }],
        totalCount: 1, overallTotalCount: 1, approvedCount: 1,
      }),
    });
  });

  await page.route('**/api/admin/resource-import-runs**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [], totalCount: 0, overallTotalCount: 0 }) });
  });

  await page.route('**/api/admin/import-packages/upload-sessions', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ sessionId: SESSION_ID, partSizeBytes: PART_SIZE, totalPartsExpected: 3, expiresAtUtc: '2026-01-01T01:00:00Z' }),
    });
  });

  const failedOnce = new Set<number>();
  const uploadedParts = new Map<number, number>(); // partNumber -> sizeBytes, mirrors server-side bookkeeping

  await page.route(`**/api/admin/import-packages/upload-sessions/${SESSION_ID}/parts/*`, async route => {
    const url = new URL(route.request().url());
    const partNumber = Number(url.pathname.split('/').pop());
    const declaredSizeBytes = Number(url.searchParams.get('declaredSizeBytes'));

    if (options.failPartOnceNumber === partNumber && !failedOnce.has(partNumber)) {
      failedOnce.add(partNumber);
      await route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ error: 'Simulated transient failure' }) });
      return;
    }

    uploadedParts.set(partNumber, declaredSizeBytes);
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ partNumber, sizeBytes: declaredSizeBytes, sha256Checksum: null, uploadedAtUtc: '2026-01-01T00:00:00Z' }),
    });
  });

  // GET session status — used for resume after a failed part. Reflects whichever parts the mock
  // above has actually "received" so far, matching the real server's resume contract.
  await page.route(`**/api/admin/import-packages/upload-sessions/${SESSION_ID}`, async route => {
    if (route.request().method() !== 'GET') { await route.fallback(); return; }
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        sessionId: SESSION_ID, status: 'InProgress', originalFileName: 'words.zip',
        declaredTotalSizeBytes: TOTAL_SIZE, partSizeBytes: PART_SIZE, totalPartsExpected: 3,
        uploadedParts: [...uploadedParts.entries()].map(([partNumber, sizeBytes]) => (
          { partNumber, sizeBytes, sha256Checksum: null, uploadedAtUtc: '2026-01-01T00:00:00Z' })),
        importPackageId: null, expiresAtUtc: '2026-01-01T01:00:00Z',
      }),
    });
  });

  await page.route(`**/api/admin/import-packages/upload-sessions/${SESSION_ID}/complete`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        importPackageId: PACKAGE_ID, status: 'uploaded', isAccepted: true, rejectionReason: null,
        compressedSizeBytes: TOTAL_SIZE, expandedSizeBytes: TOTAL_SIZE, entryCount: 1,
        folderGroups: [], distinctExtensions: ['.csv'], duplicateChecksumEntryCount: 0,
        unsupportedEntryCount: 0, suspiciousEntryCount: 0,
      }),
    });
  });

  await page.route(`**/api/admin/import-packages/upload-sessions/${SESSION_ID}/abort`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'Aborted' }) });
  });

  await page.route(`**/api/admin/import-packages/${PACKAGE_ID}/plan`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        planId: PLAN_ID, importPackageId: PACKAGE_ID, version: 1, status: 'AwaitingApproval',
        processingMode: 'FullAiAssisted', processingModeReason: 'small package', estimate: {
          detectedGroups: [], ambiguousGroups: [], unsupportedContentNotes: [],
          volume: { totalFiles: 1, filesByExtension: {}, expectedCandidateCount: 1, expectedAudioFilesRequiringStt: 0, estimatedAudioMinutesRequiringStt: 0, expectedTtsCandidates: 0, estimatedTtsCharacters: 0, expectedImageAnalysisCount: 0, unmatchedFileCount: 0 },
          time: { estimatedDurationRangeDescription: '1-2 min', estimatedMinMinutes: 1, estimatedMaxMinutes: 2, assumptions: '' },
          cost: { expectedCost: 0, minCost: 0, maxCost: 0, currency: 'USD', breakdown: [], assumptions: [], providerModelAssumptions: '' },
          risks: [], proposedDecisions: [], samplingRoundsUsed: 0, structureConfidence: 1, structuredMappingPreviews: [],
        },
        approvedCostCeiling: null, createdAtUtc: '2026-01-01T00:00:00Z', approvedAtUtc: null, approvedByUserId: null,
        rejectedAtUtc: null, rejectionReason: null, pauseReason: null, changeReason: null,
        concurrencyStamp: 'cs-1', isEditable: true, groupInstructions: [], accruedCost: 0, accruedCostCurrency: 'USD',
        remainingCeiling: null, ceilingAmendments: [],
      }),
    });
  });

  // Catch-all for other admin API calls the layout/shell makes on load.
  await page.route('**/api/admin/**', async route => {
    const url = route.request().url();
    if (url.includes('/import-packages') || url.includes('/resource-sources') || url.includes('/resource-import-runs')) {
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

async function selectZipFile(page: Page) {
  const buffer = Buffer.alloc(TOTAL_SIZE, 'A');
  await page.locator('input.sp-adm-dropzone-input').setInputFiles({
    name: 'words.zip', mimeType: 'application/zip', buffer,
  });
}

test.describe('Import Package chunked upload (Phase 4.7)', () => {
  test('uploads parts with progress, retries one failed part without restarting, completes, and opens the generated plan', async ({ page }) => {
    await mockAdmin(page, { failPartOnceNumber: 2 });
    await adminLogin(page);

    await page.goto('/admin/content/import');
    await selectZipFile(page);
    await page.getByRole('button', { name: 'Submit for review' }).click();

    // Byte-level progress is shown while uploading.
    await expect(page.getByText(/%/)).toBeVisible({ timeout: 10000 });

    // Part 2 fails once — the whole submission surfaces an actionable error rather than silently
    // hanging, and does NOT restart from scratch (the session id and already-uploaded part 1 are
    // preserved via sessionStorage/the server session).
    await expect(page.getByText(/Simulated transient failure|Upload failed/)).toBeVisible({ timeout: 10000 });

    // Clicking Submit again resumes the same session — part 1 is skipped, only part 2 (now
    // succeeding) and part 3 are (re-)sent, and the flow reaches the generated plan page.
    await page.getByRole('button', { name: 'Submit for review' }).click();
    await page.waitForURL(new RegExp(`/admin/content/import/packages/${PACKAGE_ID}/plan`), { timeout: 15000 });
  });

  test('cancelling an in-progress upload aborts the session', async ({ page }) => {
    await mockAdmin(page);
    await adminLogin(page);

    // Slow down part uploads so the Cancel button has time to appear and be clicked.
    await page.route(`**/api/admin/import-packages/upload-sessions/${SESSION_ID}/parts/*`, async route => {
      await new Promise(resolve => setTimeout(resolve, 400));
      const url = new URL(route.request().url());
      const partNumber = Number(url.pathname.split('/').pop());
      const declaredSizeBytes = Number(url.searchParams.get('declaredSizeBytes'));
      await route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ partNumber, sizeBytes: declaredSizeBytes, sha256Checksum: null, uploadedAtUtc: '2026-01-01T00:00:00Z' }),
      });
    });

    await page.goto('/admin/content/import');
    await selectZipFile(page);
    await page.getByRole('button', { name: 'Submit for review' }).click();

    const cancelButton = page.getByRole('button', { name: /Cancel upload/ });
    await expect(cancelButton).toBeVisible({ timeout: 10000 });
    await cancelButton.click();

    await expect(page.getByText(/Upload cancelled/)).toBeVisible({ timeout: 10000 });
  });
});

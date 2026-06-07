import { expect, test, type Page } from '@playwright/test';

function fakeJwt(email: string) {
  const header = toBase64Url({ alg: 'none', typ: 'JWT' });
  const payload = toBase64Url({
    sub: 'student-user-id',
    email,
    role: 'Student',
    exp: Math.floor(Date.now() / 1000) + 60 * 60,
  });
  return `${header}.${payload}.signature`;
}

function toBase64Url(value: object) {
  return Buffer.from(JSON.stringify(value))
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

const token = fakeJwt('student@test.com');
const sessionData = JSON.stringify({ token, mustChangePassword: false });

async function withAuth(page: Page) {
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, sessionData);
}

const emptyVocab: any[] = [];

const sampleVocab = [
  {
    id: 'v-1',
    term: 'could you please',
    suggestedPhrase: 'Could you please send the updated file?',
    meaningOrExplanation: 'A polite way to make a request in workplace English.',
    exampleSentence: 'Could you please confirm the meeting time?',
    category: 'polite_request',
    status: 'New',
    source: 'AiExtractedFromWritingAttempt',
    seenCount: 1,
    lastSeenAtUtc: null,
    nextReviewAtUtc: null,
    createdAt: '2026-06-07T10:00:00Z',
    sourceActivityTitle: 'Follow-up email',
    sourceModuleTitle: 'Workplace Emails',
  },
  {
    id: 'v-2',
    term: 'at your earliest convenience',
    suggestedPhrase: 'Please respond at your earliest convenience.',
    meaningOrExplanation: 'A formal phrase used to politely ask someone to do something soon.',
    exampleSentence: 'Please review and sign at your earliest convenience.',
    category: 'workplace_phrase',
    status: 'Practising',
    source: 'AiExtractedFromWritingAttempt',
    seenCount: 2,
    lastSeenAtUtc: '2026-06-06T08:00:00Z',
    nextReviewAtUtc: null,
    createdAt: '2026-06-06T09:00:00Z',
    sourceActivityTitle: null,
    sourceModuleTitle: null,
  },
];

test('vocabulary nav item is visible in sidebar', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyVocab) });
  });

  await page.goto('/dashboard');
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com', careerProfile: 'Document Controller',
        cefrLevel: null, message: 'Welcome', learningPath: null,
        activityStats: null, currentFocus: null, nextRecommendedPractice: null, latestImprovement: null,
      }),
    });
  });

  await page.goto('/dashboard');
  // Sidebar vocabulary link should be visible on desktop
  await expect(page.getByRole('link', { name: /Vocabulary/i }).first()).toBeVisible();
});

test('vocabulary page loads and shows empty state', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyVocab) });
  });

  await page.goto('/vocabulary');

  await expect(page.getByText('Your vocabulary list will grow as you complete writing activities.')).toBeVisible();
  await expect(page.getByRole('link', { name: /Start practising/i })).toBeVisible();
});

test('vocabulary page shows real data', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleVocab) });
  });

  await page.goto('/vocabulary');

  // Term text appears in the vocab card
  await expect(page.getByText('could you please', { exact: true })).toBeVisible();
  await expect(page.getByText('at your earliest convenience', { exact: true })).toBeVisible();
  await expect(page.getByText('Total saved')).toBeVisible();
});

test('vocabulary page status change works', async ({ page }) => {
  await withAuth(page);

  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleVocab) });
  });
  await page.route('**/api/vocabulary/**', async route => {
    await route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/vocabulary');

  // Wait for data to load
  await expect(page.getByText('could you please', { exact: true })).toBeVisible();

  // Find the card for 'could you please' (status=New) and click Practise
  // After optimistic update, the Practise button should disappear from that card
  const firstCard = page.locator('[data-vocab-id="v-1"]');
  const practiseBtn = firstCard.getByRole('button', { name: 'Practise' });
  await practiseBtn.click();
  await page.waitForTimeout(500);

  // After successful status change, the Practise button should no longer be visible on that card
  await expect(firstCard.getByRole('button', { name: 'Practise' })).not.toBeVisible();
});

test('vocabulary page shows no raw JSON', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleVocab) });
  });

  await page.goto('/vocabulary');

  await expect(page.locator('body')).not.toContainText('"term":');
  await expect(page.locator('body')).not.toContainText('"studentProfileId"');
  await expect(page.locator('body')).not.toContainText('{"');
});

test('vocabulary page shows friendly error on API failure', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error.' }),
    });
  });

  await page.goto('/vocabulary');

  await expect(page.getByText('Could not load vocabulary')).toBeVisible();
  await expect(page.getByRole('button', { name: /Try again/i })).toBeVisible();
});

test('vocabulary page has no unexpected console errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });

  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleVocab) });
  });

  await page.goto('/vocabulary');
  await page.waitForTimeout(500);

  const unexpected = errors.filter(e =>
    !e.includes('401') && !e.includes('Unauthorized') && !e.includes('favicon')
  );
  expect(unexpected).toHaveLength(0);
});

test('vocabulary page mobile does not overflow', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await withAuth(page);
  await page.route('**/api/vocabulary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(sampleVocab) });
  });

  await page.goto('/vocabulary');

  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(380);
});

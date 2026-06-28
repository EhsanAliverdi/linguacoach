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

/** Inject a pre-authenticated session before the Angular app boots. */
async function withAuth(page: Page) {
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, sessionData);
}

const emptyProgressResponse = {
  summary: {
    activitiesCompleted: 0,
    totalAttempts: 0,
    retryAttempts: 0,
    averageScore: null,
    latestScore: null,
    bestScore: null,
    activitiesThisWeek: 0,
    modulesCompleted: 0,
    currentModuleProgress: null,
  },
  scoreTrend: [],
  skillProgress: { skills: [], topStrengths: [], weakestSkills: [] },
  learningFocus: null,
  moduleProgress: [],
};

const dataProgressResponse = {
  summary: {
    activitiesCompleted: 5,
    totalAttempts: 8,
    retryAttempts: 3,
    averageScore: 76,
    latestScore: 82,
    bestScore: 91,
    activitiesThisWeek: 2,
    modulesCompleted: 1,
    currentModuleProgress: {
      moduleId: 'mod-1',
      title: 'Workplace Emails',
      completedActivities: 2,
      totalRequired: 3,
      averageScore: 76,
      latestScore: 82,
      isReadyToComplete: false,
    },
  },
  scoreTrend: [
    { attemptDate: '2026-06-07T10:00:00Z', score: 82, activityTitle: 'Polite request message', moduleTitle: 'Workplace Emails', attemptNumber: 2 },
    { attemptDate: '2026-06-06T09:00:00Z', score: 74, activityTitle: 'Delay explanation email', moduleTitle: 'Workplace Emails', attemptNumber: 1 },
  ],
  skillProgress: {
    skills: [
      { skillKey: 'grammar_accuracy', skillLabel: 'Grammar accuracy', isWeak: false },
      { skillKey: 'formal_tone', skillLabel: 'Formal tone', isWeak: true },
    ],
    topStrengths: ['Grammar accuracy'],
    weakestSkills: ['Formal tone'],
  },
  learningFocus: {
    journeySummary: 'You are making solid progress on workplace emails.',
    nextRecommendedFocus: ['Practise formal tone in requests'],
    recurringMistakes: ['Overly casual greetings'],
    weakSkills: ['Formal tone'],
    strongSkills: ['Grammar accuracy'],
  },
  moduleProgress: [
    { moduleId: 'mod-1', title: 'Workplace Emails', status: 'current', completedActivities: 2, totalRequired: 3, averageScore: 76, latestScore: 82, isReadyToComplete: false, completedAt: null },
    { moduleId: 'mod-2', title: 'Meeting Communication', status: 'upcoming', completedActivities: 0, totalRequired: 3, averageScore: null, latestScore: null, isReadyToComplete: false, completedAt: null },
  ],
};

test('progress page loads and shows empty state', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyProgressResponse) });
  });

  await page.goto('/progress');

  await expect(page.getByText('Your progress will appear here after you complete your first activity.')).toBeVisible();
  await expect(page.getByRole('link', { name: /Start practising/i })).toBeVisible();
});

test('progress page shows real data — summary cards, scores, skills, modules', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dataProgressResponse) });
  });

  await page.goto('/progress');

  // Header (use first() — loading skeleton and data section both contain this heading)
  await expect(page.getByText('Your progress').first()).toBeVisible();
  await expect(page.getByText('Track your writing practice, skill growth, and next focus.')).toBeVisible();

  // Summary cards — activities completed label
  await expect(page.getByText('activities completed')).toBeVisible();

  // Score trend
  await expect(page.getByText('Polite request message')).toBeVisible();
  await expect(page.getByText('Delay explanation email')).toBeVisible();

  // Skill section
  await expect(page.getByText('Grammar accuracy')).toBeVisible();
  // "Formal tone" may appear twice (strengths/weakest chips) — check at least one is visible
  await expect(page.getByText('Formal tone').first()).toBeVisible();

  // Module progress
  await expect(page.getByText('Workplace Emails', { exact: true })).toBeVisible();
  await expect(page.getByText('Meeting Communication', { exact: true })).toBeVisible();

  // Learning focus
  await expect(page.getByText('You are making solid progress on workplace emails.')).toBeVisible();
  await expect(page.getByText('Practise formal tone in requests')).toBeVisible();
});

test('progress page shows no raw JSON', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dataProgressResponse) });
  });

  await page.goto('/progress');

  await expect(page.locator('body')).not.toContainText('"skillKey"');
  await expect(page.locator('body')).not.toContainText('"isWeak"');
  await expect(page.locator('body')).not.toContainText('"journeySummary"');
});

test('progress page shows friendly error when API fails', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error.' }),
    });
  });

  await page.goto('/progress');

  await expect(page.getByText('Could not load progress')).toBeVisible();
  await expect(page.getByRole('button', { name: /Try again/i })).toBeVisible();
});

test.skip('progress page has no unexpected console errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') errors.push(msg.text());
  });

  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dataProgressResponse) });
  });

  await page.goto('/progress');
  await page.waitForTimeout(500);

  const unexpected = errors.filter(e =>
    !e.includes('401') &&
    !e.includes('Unauthorized') &&
    !e.includes('favicon')
  );
  expect(unexpected).toHaveLength(0);
});

test('progress page mobile does not overflow', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await withAuth(page);
  await page.route('**/api/progress', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(dataProgressResponse) });
  });

  await page.goto('/progress');

  // Check that no horizontal overflow occurs
  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(380); // 375 + 5px tolerance
});

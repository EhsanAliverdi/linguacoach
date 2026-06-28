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

const mockProfile = {
  profileId: 'profile-1',
  userId: 'user-1',
  firstName: 'Jane',
  lastName: 'Doe',
  displayName: 'Jane Doe',
  preferredName: null,
  email: 'student@test.com',
  cefrLevel: 'B1',
  learningGoals: ['Day-to-day English'],
  customLearningGoal: null,
  focusAreas: ['Speaking'],
  customFocusArea: null,
  supportLanguageCode: 'fa',
  supportLanguageName: 'Persian',
  translationHelpPreference: 'WhenDifficult',
  preferredSessionDurationMinutes: 20,
  difficultyPreference: 'Balanced',
  learningPreferencesUpdatedAt: null,
};

const mockPlacement = {
  assessmentId: 'assess-1',
  studentProfileId: 'profile-1',
  status: 'Completed',
  startedAtUtc: '2026-06-01T10:00:00Z',
  completedAtUtc: '2026-06-01T10:30:00Z',
  expiredAtUtc: null,
  overallCefrLevel: 'B1',
  overallConfidence: 0.75,
  isProvisional: false,
  resultSummary: 'Good placement',
  source: 'adaptive',
  skillResults: [
    { skill: 'listening', estimatedCefrLevel: 'B1', confidence: 0.8, evidenceCount: 5, strengths: null, weaknesses: null, recommendedObjectiveKeys: [] },
    { skill: 'speaking', estimatedCefrLevel: 'A2', confidence: 0.7, evidenceCount: 4, strengths: null, weaknesses: null, recommendedObjectiveKeys: [] },
  ],
  learningPlanRegenerated: true,
  learningPlanRegenerationWarning: null,
  itemCount: 12,
  hasPlacement: true,
};

const mockNotifPrefs = [
  { category: 'Account', channel: 'InApp', isEnabled: true, isRequired: true },
  { category: 'Account', channel: 'Email', isEnabled: true, isRequired: true },
  { category: 'Learning', channel: 'InApp', isEnabled: true, isRequired: false },
  { category: 'Learning', channel: 'Email', isEnabled: true, isRequired: false },
];

async function mockProfileRoutes(page: Page) {
  await page.route('**/api/profile', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockProfile) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockPlacement) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }),
    });
  });
  await page.route('**/api/notifications/preferences', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockNotifPrefs) });
  });
}

test('profile page loads and shows account section', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.locator('body')).toContainText('Account');
  await expect(page.locator('body')).toContainText('student@test.com');
});

test('profile page shows CEFR level as read-only', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('level-section')).toBeVisible();
  await expect(page.getByTestId('level-section')).toContainText('B1');
  const inputs = await page.getByTestId('level-section').locator('input, select').count();
  expect(inputs).toBe(0);
});

test('profile page shows placement summary section', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('placement-summary-section')).toBeVisible();
  await expect(page.getByTestId('confirmed-badge')).toBeVisible();
  await expect(page.getByTestId('skill-breakdown')).toBeVisible();
});

test('profile page shows retake-not-available when retake is disabled', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('retake-not-available')).toBeVisible();
  await expect(page.getByTestId('retake-not-available')).toContainText('not available yet');
});

test('profile page shows learning goals section', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.locator('body')).toContainText('Learning goals');
  await expect(page.getByTestId('learning-goals-chips')).toBeVisible();
});

test('profile page shows support language selector', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('support-language-select')).toBeVisible();
});

test('profile page shows notification preferences section', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('notification-prefs-section')).toBeVisible();
  await expect(page.getByTestId('prefs-table')).toBeVisible();
});

test('profile save button is present', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('save-button')).toBeVisible();
});

test('profile page does not show raw JSON', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.locator('body')).not.toContainText('"profileId"');
  await expect(page.locator('body')).not.toContainText('"cefrLevel"');
  await expect(page.locator('body')).not.toContainText('{"');
});

test('profile page shows CEFR protection notice', async ({ page }) => {
  await withAuth(page);
  await mockProfileRoutes(page);
  await page.goto('/profile');

  await expect(page.getByTestId('level-section')).toContainText('updated through placement');
});

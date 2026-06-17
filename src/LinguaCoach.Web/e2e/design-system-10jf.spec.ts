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

async function withAuth(page: Page) {
  const token = fakeJwt('student@test.com');
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, JSON.stringify({ token, mustChangePassword: false }));
}

async function mockDashboard(page: Page) {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Document Controller',
        cefrLevel: 'B1',
        message: '',
        lifecycleStage: 'ActiveLearning',
        learningPath: {
          pathId: 'path-1',
          title: 'Workplace English',
          modulesCompleted: 0,
          totalModules: 2,
          currentModule: {
            moduleId: 'mod-1',
            title: 'Professional workplace communication',
            description: 'Practice concise project status updates.',
            order: 1,
            completedActivities: 0,
            totalActivities: 3,
          },
        },
        activityStats: { activitiesCompleted: 2, averageScore: 78, latestScore: 82 },
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
        streakDays: 3,
      }),
    });
  });
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: null,
        strongSkills: [],
        weakSkills: [],
        recurringMistakes: [],
        nextRecommendedFocus: [],
        coveredScenarioCount: 0,
        skillProfile: [],
      }),
    });
  });
  await page.route('**/api/placement/result', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
}

async function mockProfile(page: Page) {
  await page.route('**/api/profile', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        profileId: 'profile-1',
        userId: 'user-1',
        firstName: 'Jane',
        lastName: 'Doe',
        displayName: 'Jane Doe',
        preferredName: 'Jane',
        email: 'student@test.com',
        cefrLevel: 'B1',
        learningGoals: ['Day-to-day English', 'Workplace English'],
        customLearningGoal: null,
        focusAreas: ['Speaking'],
        customFocusArea: null,
        supportLanguageCode: 'fa',
        supportLanguageName: 'Persian',
        translationHelpPreference: 'WhenDifficult',
        preferredSessionDurationMinutes: 20,
        difficultyPreference: 'Balanced',
        learningPreferencesUpdatedAt: null,
      }),
    });
  });
  await page.route('**/api/profile/preferences', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
}

// ── Mobile bottom nav appears exactly once ─────────────────────────────────────

test('mobile bottom nav is present exactly once in the DOM', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const navbars = page.locator('[data-testid="mobile-bottomnav"]');
  await expect(navbars).toHaveCount(1);
});

test('mobile bottom nav is hidden on desktop viewport', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/dashboard');

  const nav = page.locator('[data-testid="mobile-bottomnav"]');
  await expect(nav).toBeHidden();
});

test('mobile bottom nav is visible on mobile viewport', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/dashboard');

  const nav = page.locator('[data-testid="mobile-bottomnav"]');
  await expect(nav).toBeVisible();
});

// ── Profile chip selected states ───────────────────────────────────────────────

test('profile page: selected learning goal chip has sp-pref-chip--on class', async ({ page }) => {
  await withAuth(page);
  await mockProfile(page);
  await page.goto('/profile');

  const chipsContainer = page.locator('[data-testid="learning-goals-chips"]');
  await expect(chipsContainer).toBeVisible();

  const dayToDayChip = page.locator('[data-testid="goal-chip-Day-to-day English"]');
  await expect(dayToDayChip).toBeVisible();
  await expect(dayToDayChip).toHaveClass(/sp-pref-chip--on/);
  await expect(dayToDayChip).toHaveAttribute('aria-pressed', 'true');
});

test('profile page: unselected learning goal chip does not have sp-pref-chip--on class', async ({ page }) => {
  await withAuth(page);
  await mockProfile(page);
  await page.goto('/profile');

  const travelChip = page.locator('[data-testid="goal-chip-Travel English"]');
  await expect(travelChip).toBeVisible();
  await expect(travelChip).not.toHaveClass(/sp-pref-chip--on/);
  await expect(travelChip).toHaveAttribute('aria-pressed', 'false');
});

test('profile page: clicking unselected chip selects it', async ({ page }) => {
  await withAuth(page);
  await mockProfile(page);
  await page.goto('/profile');

  const travelChip = page.locator('[data-testid="goal-chip-Travel English"]');
  await expect(travelChip).not.toHaveClass(/sp-pref-chip--on/);
  await travelChip.click();
  await expect(travelChip).toHaveClass(/sp-pref-chip--on/);
  await expect(travelChip).toHaveAttribute('aria-pressed', 'true');
});

test('profile page: difficulty Balanced chip is pre-selected', async ({ page }) => {
  await withAuth(page);
  await mockProfile(page);
  await page.goto('/profile');

  const balancedChip = page.locator('[data-testid="difficulty-balanced"]');
  await expect(balancedChip).toHaveClass(/sp-pref-chip--on/);
  await expect(balancedChip).toHaveAttribute('aria-pressed', 'true');
});

test('profile page: session length 20min chip is pre-selected', async ({ page }) => {
  await withAuth(page);
  await mockProfile(page);
  await page.goto('/profile');

  const chip20 = page.locator('[data-testid="session-length-20"]');
  await expect(chip20).toHaveClass(/sp-pref-chip--on/);
});

// ── Today page layout not broken on mobile ─────────────────────────────────────

test('today page layout is not broken on mobile', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/dashboard');

  await page.waitForLoadState('networkidle');

  // Dashboard content area renders
  await expect(page.locator('.sp-student-content')).toBeVisible();
  // No horizontal overflow
  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 2);
});

// ── Practice Gym usable on mobile ─────────────────────────────────────────────

test('Practice Gym heading visible on mobile', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/exercise-types', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/practice');

  await expect(page.getByTestId('practice-gym-heading')).toBeVisible();
});

// ── Desktop navigation works across all main pages ─────────────────────────────

test('desktop: Today page accessible via sidebar', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/dashboard');
  await page.waitForLoadState('networkidle');
  // Dashboard loaded — content area is visible (page wrapper exists)
  await expect(page.locator('.sp-student-content')).toBeVisible();
  await expect(page).toHaveURL(/\/dashboard/);
});

test('desktop: sidebar nav links are visible', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/dashboard');

  await expect(page.getByTestId('nav-today')).toBeVisible();
  await expect(page.getByTestId('nav-practice')).toBeVisible();
  await expect(page.getByTestId('nav-progress')).toBeVisible();
  await expect(page.getByTestId('nav-profile')).toBeVisible();
});

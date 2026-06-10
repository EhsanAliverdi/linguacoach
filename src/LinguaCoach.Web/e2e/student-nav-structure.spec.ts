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
}

// ── Desktop sidebar nav ────────────────────────────────────────────────────────

test('student sidebar shows Today, Journey, Practice, Progress, Profile', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const sidebar = page.locator('.sp-student-sidebar');
  await expect(sidebar.getByTestId('nav-today')).toBeVisible();
  await expect(sidebar.getByTestId('nav-journey')).toBeVisible();
  await expect(sidebar.getByTestId('nav-practice')).toBeVisible();
  await expect(sidebar.getByTestId('nav-progress')).toBeVisible();
  await expect(sidebar.getByTestId('nav-profile')).toBeVisible();

  await expect(sidebar.getByTestId('nav-today')).toContainText('Today');
  await expect(sidebar.getByTestId('nav-journey')).toContainText('Journey');
  await expect(sidebar.getByTestId('nav-practice')).toContainText('Practice');
  await expect(sidebar.getByTestId('nav-progress')).toContainText('Progress');
  await expect(sidebar.getByTestId('nav-profile')).toContainText('Profile');
});

test('student sidebar does not show Dashboard label', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const sidebar = page.locator('.sp-student-sidebar');
  await expect(sidebar.getByRole('link', { name: /^Dashboard$/i })).toHaveCount(0);
});

test('student sidebar does not show Vocabulary as a top-level nav item', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const sidebar = page.locator('.sp-student-sidebar');
  await expect(sidebar.getByRole('link', { name: /^Vocabulary$/i })).toHaveCount(0);
});

test('Practice nav item links to /practice', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const practiceLink = page.getByTestId('nav-practice');
  await expect(practiceLink).toHaveAttribute('href', '/practice');
});

// ── Mobile bottom nav ──────────────────────────────────────────────────────────

test('mobile bottom nav shows Today, Journey, Practice, Progress, Profile', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const bottomnav = page.getByTestId('mobile-bottomnav');
  await expect(bottomnav.getByTestId('mobile-nav-today')).toBeAttached();
  await expect(bottomnav.getByTestId('mobile-nav-journey')).toBeAttached();
  await expect(bottomnav.getByTestId('mobile-nav-practice')).toBeAttached();
  await expect(bottomnav.getByTestId('mobile-nav-progress')).toBeAttached();
  await expect(bottomnav.getByTestId('mobile-nav-profile')).toBeAttached();
});

test('mobile Practice FAB links to /practice', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.goto('/dashboard');

  const practiceFab = page.getByTestId('mobile-nav-practice');
  await expect(practiceFab).toHaveAttribute('href', '/practice');
});

// ── /practice route ───────────────────────────────────────────────────────────

test('/practice loads Practice Gym page and does not auto-start an activity', async ({ page }) => {
  await withAuth(page);

  let activityApiCalled = false;
  await page.route('**/api/activity**', () => { activityApiCalled = true; });

  await page.goto('/practice');

  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();
  await expect(page).toHaveURL(/\/practice/);
  expect(activityApiCalled).toBe(false);
});

// ── /journey route ────────────────────────────────────────────────────────────

test('/journey reaches the learning journey page', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/learning-path', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        pathId: 'path-1',
        title: 'Workplace English',
        modulesCompleted: 0,
        totalModules: 2,
        currentModuleId: 'mod-1',
        currentFocus: null,
        modules: [
          {
            moduleId: 'mod-1',
            title: 'Professional updates',
            description: 'Practice concise project status updates.',
            order: 1,
            completedActivities: 0,
            totalActivities: 3,
            focusSkill: 'Writing',
            difficulty: 'B1',
            reason: null,
            averageScore: null,
            isReadyToComplete: false,
          },
        ],
      }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ journeySummary: null, strongSkills: [], weakSkills: [], recurringMistakes: [], nextRecommendedFocus: [], coveredScenarioCount: 0, skillProfile: [] }),
    });
  });
  await page.route('**/api/placement/result', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ lifecycleStage: 'ActiveLearning', learningPath: null, activityStats: null, currentFocus: null, nextRecommendedPractice: null, latestImprovement: null }),
    });
  });

  await page.goto('/journey');

  // Should not land on a 404 or redirect away from journey-related content
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page).not.toHaveURL(/\/dashboard/);
  await expect(page).toHaveURL(/\/(journey|my-path)/);
});

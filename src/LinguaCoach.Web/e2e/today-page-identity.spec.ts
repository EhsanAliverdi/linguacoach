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

async function mockActiveLearningDashboard(page: Page) {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'Sara',
        careerProfile: 'Document Controller',
        cefrLevel: 'B1',
        message: '',
        lifecycleStage: 'ActiveLearning',
        learningPath: {
          pathId: 'path-1',
          title: 'Workplace English',
          modulesCompleted: 0,
          totalModules: 3,
          currentModule: {
            moduleId: 'mod-1',
            title: 'Professional workplace communication',
            description: 'Practice concise project status updates.',
            order: 1,
            completedActivities: 1,
            totalActivities: 3,
          },
        },
        activityStats: { activitiesCompleted: 5, averageScore: 81, latestScore: 85 },
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: 'session-abc',
        title: 'Explaining a Document Delay',
        topic: 'Professional delay communication',
        sessionGoal: 'Practice professional workplace communication.',
        durationMinutes: 15,
        focusSkill: 'Writing',
        status: 'notStarted',
        isResuming: false,
        exercises: [
          { exerciseId: 'ex-1', order: 0, kind: 'vocabularyWarmup', exercisePatternKey: 'phrase_match', primarySkill: 'Vocabulary', instructions: 'Match each phrase.', estimatedMinutes: 3, status: 'notStarted', learningActivityId: null },
        ],
      }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: 'You are building confidence with workplace communication.',
        strongSkills: ['Clear context'],
        weakSkills: ['Softening requests'],
        recurringMistakes: [],
        nextRecommendedFocus: ['Listening for deadlines'],
        coveredScenarioCount: 3,
        skillProfile: [],
      }),
    });
  });
}

// ── Page identity ──────────────────────────────────────────────────────────────

test('Today page loads from /dashboard', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('today-page')).toBeVisible();
  await expect(page).toHaveURL(/\/dashboard/);
});

test('Today page heading says Today\'s Lesson, not Dashboard', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('today-page-heading')).toBeVisible();
  await expect(page.getByTestId('today-page-heading')).toContainText("Today's Lesson");
  await expect(page.getByRole('heading', { name: /Dashboard/i })).toHaveCount(0);
});

test('Today\'s Lesson card is the primary CTA', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('dashboard-todays-lesson')).toBeVisible();
  await expect(page.getByTestId('todays-lesson-cta')).toBeVisible();
});

test('Today\'s Lesson CTA links to lesson route, not /activity', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  const cta = page.getByTestId('todays-lesson-cta');
  await expect(cta).toBeVisible();
  // Must link to /lesson/:id, NOT /activity
  const href = await cta.getAttribute('href');
  expect(href).toMatch(/\/lesson\//);
  expect(href).not.toMatch(/\/activity/);
});

// ── No old "Recommended next" behaviour ───────────────────────────────────────

test('Today page does not have a "Recommended next" section linking to /activity', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  // The old "Recommended next" gradient section must be gone
  await expect(page.getByTestId('dashboard-recommended-next')).toHaveCount(0);
  // No link with the label "Continue learning" that points to /activity
  const continueLearningLinks = page.getByRole('link', { name: /Continue learning/i });
  const count = await continueLearningLinks.count();
  for (let i = 0; i < count; i++) {
    const href = await continueLearningLinks.nth(i).getAttribute('href');
    expect(href).not.toBe('/activity');
    expect(href).not.toMatch(/^\/activity$/);
  }
});

// ── Practice Gym is secondary ─────────────────────────────────────────────────

test('Today page does not show Practice Gym card grid as main content', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  // The full Practice Gym card grid should not be on Today — it lives on /practice
  await expect(page.getByTestId('practice-gym-grid')).toHaveCount(0);
});

test('Today page has a secondary link to Practice Gym pointing to /practice', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('today-practice-link')).toBeVisible();
  await expect(page.getByTestId('today-practice-link')).toHaveAttribute('href', '/practice');
});

// ── Journey links ──────────────────────────────────────────────────────────────

test('Today page learning path section links to /journey', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('today-learning-path')).toBeVisible();
  await expect(page.getByTestId('today-view-full-journey')).toHaveAttribute('href', '/journey');
});

test('Today learning focus card links to /journey', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('today-learning-focus')).toBeVisible();
  await expect(page.getByTestId('today-view-journey')).toHaveAttribute('href', '/journey');
});

test('Today page has no links to /my-path', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);

  await page.goto('/dashboard');

  const myPathLinks = page.locator('a[href="/my-path"]');
  await expect(myPathLinks).toHaveCount(0);
});

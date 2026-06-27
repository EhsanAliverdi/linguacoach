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
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'Sara', cefrLevel: 'B1', supportLanguage: null },
        courseReadiness: {
          isLearningReady: true,
          lifecycleStatus: 'ActiveLearning',
          placementRequired: false,
          learningPlanExists: true,
        },
        todaySession: {
          status: 'Ready',
          sessionId: 'session-abc',
          title: 'Explaining a Document Delay',
          topic: 'Professional delay communication',
          sessionGoal: 'Practice professional workplace communication.',
          focusSkill: 'Writing',
          durationMinutes: 15,
          exerciseCount: 1,
          actionLabel: "Start today's lesson",
        },
        learningPlan: {
          pathTitle: 'Workplace English',
          currentObjective: 'Professional workplace communication',
          currentObjectiveDescription: 'Practice concise project status updates.',
          objectiveIndex: 1,
          totalObjectives: 3,
          modulesCompleted: 0,
          remainingObjectives: 2,
          completedActivities: 1,
          totalActivities: 3,
          progressPercent: 0,
        },
        practice: { status: 'Preparing', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: {
          skillProfile: [],
          strongSkills: ['Clear context'],
          weakSkills: ['Softening requests'],
          nextRecommendedFocus: ['Listening for deadlines'],
          journeySummary: 'You are building confidence with workplace communication.',
          activitiesCompleted: 5,
          streakDays: 0,
        },
        quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 5, reviewQueueCount: 0 },
        warnings: {
          missingLearningPlan: false,
          missingTodaySession: false,
          practiceUnavailable: false,
          placementIncomplete: false,
        },
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

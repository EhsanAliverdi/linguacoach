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
  await page.addInitScript((s) => sessionStorage.setItem('speakpath.auth', s), sessionData);
}

function status(partial: Record<string, unknown> = {}) {
  return {
    status: 'NotStarted',
    currentSectionKey: 'self_check',
    currentSectionOrder: 1,
    totalSections: 6,
    lifecycleStage: 'PlacementRequired',
    isCompleted: false,
    ...partial,
  };
}

const selfCheckSection = {
  status: 'InProgress',
  currentSectionOrder: 1,
  totalSections: 6,
  isCompleted: false,
  section: {
    key: 'self_check',
    order: 1,
    title: 'Quick self-check',
    instructions: 'Tell us how confident you feel right now.',
    sectionType: 'self_check',
    scored: false,
    questions: [
      { key: 'confidence_email', prompt: 'How confident are you writing a short work email?', type: 'rating' },
    ],
    passage: null, audioScript: null, writingPrompt: null, speakingPrompt: null,
  },
};

const result = {
  estimatedOverallLevel: 'B1',
  skillLevels: [
    { skill: 'Grammar accuracy', level: 'B1' },
    { skill: 'Workplace vocabulary', level: 'B1+' },
  ],
  strengths: ['vocabulary recognition'],
  weaknesses: ['formal tone in writing'],
  recommendedStartingCourse: 'Workplace English B1',
  recommendedSessionDuration: 15,
  placementNotes: 'You are around B1. We will build a course that matches your level.',
  isCompleted: true,
};

test('placement intro shows when not started', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-intro')).toBeVisible();
  await expect(page.getByText("Let's understand your English level")).toBeVisible();
  await expect(page.getByTestId('placement-begin')).toBeVisible();
});

test('placement section flow renders questions and continues', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });
  await page.route('**/api/placement/start', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'InProgress' })) });
  });
  await page.route('**/api/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(selfCheckSection) });
  });
  await page.route('**/api/placement/answers', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'InProgress', currentSectionOrder: 2, currentSectionKey: 'vocab_grammar' })) });
  });

  await page.goto('/placement');
  await page.getByTestId('placement-begin').click();

  await expect(page.getByTestId('placement-section')).toBeVisible();
  await expect(page.getByText('Quick self-check')).toBeVisible();
  await expect(page.getByText('Section 1 of 6')).toBeVisible();

  // Answer the rating question, then continue is enabled.
  await page.getByRole('button', { name: '3', exact: true }).click();
  await expect(page.getByTestId('placement-continue')).toBeEnabled();
});

test('placement result page shows level and skill breakdown', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'Completed', isCompleted: true, lifecycleStage: 'CourseReady' })) });
  });
  await page.route('**/api/placement/result', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(result) });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-result')).toBeVisible();
  await expect(page.getByTestId('placement-overall-level')).toHaveText('B1');
  await expect(page.getByText('Grammar accuracy')).toBeVisible();
  await expect(page.getByText('formal tone in writing')).toBeVisible();
  await expect(page.getByText('Workplace English B1')).toBeVisible();
  await expect(page.getByTestId('placement-continue-course')).toBeVisible();
});

test('dashboard shows placement CTA when placement is required', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Junior Software Engineer',
        cefrLevel: null,
        message: 'Complete your placement assessment to unlock your personalised course.',
        lifecycleStage: 'PlacementRequired',
        learningPath: null,
        activityStats: null,
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });

  await page.goto('/dashboard');

  await expect(page).toHaveURL(/\/dashboard/);
  await expect(page.getByTestId('dashboard-placement-required')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Start placement' })).toHaveAttribute('href', '/placement');
  await expect(page.getByTestId('dashboard-recommended-next')).toHaveCount(0);
});

// Regression: GET /api/placement/status returned 400 in production for students who
// completed onboarding (PlacementRequired) but had no assessment yet. The fix makes the
// backend return 200 NotStarted. This test confirms the frontend shows the intro/start page
// (not "Could not load your placement") when status is 200 NotStarted.
test('placement shows intro, not error, for PlacementRequired NotStarted student', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'NotStarted',
        currentSectionKey: 'self_check',
        currentSectionOrder: 1,
        totalSections: 6,
        lifecycleStage: 'PlacementRequired',
        isCompleted: false,
      }),
    });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-intro')).toBeVisible();
  await expect(page.getByText('Could not load your placement')).toHaveCount(0);
  await expect(page.getByTestId('placement-begin')).toBeVisible();
});

test('Start placement button calls /api/placement/start and transitions to section', async ({ page }) => {
  await withAuth(page);
  let startCalled = false;
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });
  await page.route('**/api/placement/start', async route => {
    startCalled = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(status({ status: 'InProgress', lifecycleStage: 'PlacementInProgress' })),
    });
  });
  await page.route('**/api/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(selfCheckSection) });
  });

  await page.goto('/placement');
  await expect(page.getByTestId('placement-begin')).toBeVisible();
  await page.getByTestId('placement-begin').click();

  await expect(page.getByTestId('placement-section')).toBeVisible();
  expect(startCalled).toBe(true);
});

test('PlacementRequired dashboard CTA routes to /placement and page loads intro', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Junior Software Engineer',
        cefrLevel: null,
        message: '',
        lifecycleStage: 'PlacementRequired',
        learningPath: null,
        activityStats: null,
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(status()),
    });
  });

  await page.goto('/dashboard');
  await expect(page.getByTestId('dashboard-placement-required')).toBeVisible();

  await page.getByRole('link', { name: 'Start placement' }).click();
  await expect(page).toHaveURL(/\/placement/);
  await expect(page.getByTestId('placement-intro')).toBeVisible();
  await expect(page.getByText('Could not load your placement')).toHaveCount(0);
});

test('placement does not expose correct answers in the DOM', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'InProgress' })) });
  });
  await page.route('**/api/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(selfCheckSection) });
  });

  await page.goto('/placement');
  await expect(page.getByTestId('placement-section')).toBeVisible();
  await expect(page.locator('body')).not.toContainText('correctOption');
});

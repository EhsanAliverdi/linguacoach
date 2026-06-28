import { expect, test, type Page } from '@playwright/test';

// ── Helpers ────────────────────────────────────────────────────────────────────

function toBase64Url(value: object) {
  return Buffer.from(JSON.stringify(value))
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

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

async function withAuth(page: Page) {
  const token = fakeJwt('student@test.com');
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, JSON.stringify({ token, mustChangePassword: false }));
}

const PLACEMENT_COMPLETED = {
  status: 'Completed',
  lifecycleStage: 'CourseReady',
  currentSectionKey: null,
  currentSectionOrder: 0,
  totalSections: 6,
};

const JOURNEY_WITH_PLAN = {
  currentCefrLevel: 'B1',
  currentLearningPhase: 'Consolidating',
  totalObjectives: 8,
  completionPercentage: 25,
  lastCompletedAt: '2026-05-15T10:00:00Z',
  planStatus: 'Active',
  currentObjective: {
    objectiveKey: 'obj-speaking-b1',
    title: 'Meetings and Presentations',
    skill: 'speaking',
    cefrLevel: 'B1',
    status: 'Current',
    sequenceNumber: 3,
    isReview: false,
    isBlocked: false,
    blockedByKey: null,
    lastEvaluatedAt: null,
    isMastered: false,
  },
  upcomingObjectives: [
    {
      objectiveKey: 'obj-listening-b1',
      title: 'Listening for detail',
      skill: 'listening',
      cefrLevel: 'B1',
      status: 'Ready',
      sequenceNumber: 4,
      isReview: false,
      isBlocked: false,
      blockedByKey: null,
      lastEvaluatedAt: null,
      isMastered: false,
    },
    {
      objectiveKey: 'obj-writing-b1',
      title: 'Report writing',
      skill: 'writing',
      cefrLevel: 'B1',
      status: 'Upcoming',
      sequenceNumber: 5,
      isReview: false,
      isBlocked: false,
      blockedByKey: null,
      lastEvaluatedAt: null,
      isMastered: false,
    },
  ],
  completedObjectives: [
    {
      objectiveKey: 'obj-vocab-a2',
      title: 'Core vocabulary',
      skill: 'vocabulary',
      cefrLevel: 'A2',
      status: 'Completed',
      sequenceNumber: 1,
      isReview: false,
      isBlocked: false,
      blockedByKey: null,
      lastEvaluatedAt: '2026-04-10T09:00:00Z',
      isMastered: false,
    },
    {
      objectiveKey: 'obj-reading-a2',
      title: 'Reading short texts',
      skill: 'reading',
      cefrLevel: 'A2',
      status: 'Completed',
      sequenceNumber: 2,
      isReview: false,
      isBlocked: false,
      blockedByKey: null,
      lastEvaluatedAt: '2026-05-15T10:00:00Z',
      isMastered: true,
    },
  ],
  reviewObjectives: [],
  milestones: [
    { type: 'placement_completed', label: 'Placement completed', occurredAt: '2026-04-01T09:00:00Z' },
    { type: 'first_objective_completed', label: '1st objective done', occurredAt: '2026-04-10T09:00:00Z' },
  ],
};

const JOURNEY_NO_PLAN = {
  currentCefrLevel: 'A2',
  currentLearningPhase: 'Preparing',
  totalObjectives: 0,
  completionPercentage: 0,
  lastCompletedAt: null,
  planStatus: 'None',
  currentObjective: null,
  upcomingObjectives: [],
  completedObjectives: [],
  reviewObjectives: [],
  milestones: [],
};

// ── Route mocking helpers ──────────────────────────────────────────────────────

async function mockCommonRoutes(page: Page, journey: object) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(PLACEMENT_COMPLETED),
    });
  });
  await page.route('**/api/student/learning-plan/journey', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(journey),
    });
  });
}

// ── Tests ──────────────────────────────────────────────────────────────────────

test.describe('Learning Journey page', () => {

  test('navigates to /journey and shows page heading', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    await expect(page.getByText('Your roadmap')).toBeVisible();
  });

  test('shows CEFR level and learning phase', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    await expect(page.getByTestId('journey-cefr')).toHaveText('B1');
    await expect(page.getByTestId('journey-phase')).toHaveText('Consolidating');
  });

  test('shows progress percentage', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    await expect(page.getByTestId('journey-progress-pct')).toContainText('25');
  });

  test('shows current objective with Continue Lesson button', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    const card = page.getByTestId('current-objective');
    await expect(card).toBeVisible();
    await expect(card).toContainText('Meetings and Presentations');
    const btn = page.getByTestId('continue-lesson-btn');
    await expect(btn).toBeVisible();
    await expect(btn).toHaveAttribute('href', /dashboard/);
  });

  test('Continue Lesson button navigates to dashboard', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    // Also mock the dashboard API so the navigation completes
    await page.route('**/api/student/dashboard/summary', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });
    await page.goto('/journey');
    const btn = page.getByTestId('continue-lesson-btn');
    await btn.click();
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('/my-path also loads the journey page', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/my-path');
    await expect(page.getByText('Your roadmap')).toBeVisible();
  });

  test('shows completed objectives section', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    const section = page.getByTestId('completed-objectives');
    await expect(section).toBeVisible();
    await expect(section).toContainText('Core vocabulary');
    await expect(section).toContainText('Reading short texts');
  });

  test('mastered badge appears on mastered objectives', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    const section = page.getByTestId('completed-objectives');
    await expect(section).toContainText('Mastered');
  });

  test('shows "up to date" message when review queue is empty', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    await expect(page.getByTestId('review-queue-empty')).toBeVisible();
    await expect(page.getByTestId('review-queue-empty')).toContainText("You're up to date");
  });

  test('shows upcoming timeline with objective titles', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    const list = page.getByTestId('timeline-list');
    await expect(list).toBeVisible();
    await expect(list).toContainText('Listening for detail');
    await expect(list).toContainText('Report writing');
  });

  test('shows milestones section', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_WITH_PLAN);
    await page.goto('/journey');
    const milestones = page.getByTestId('milestones');
    await expect(milestones).toBeVisible();
    await expect(milestones).toContainText('Placement completed');
  });

  test('shows preparing state when no plan exists', async ({ page }) => {
    await withAuth(page);
    await mockCommonRoutes(page, JOURNEY_NO_PLAN);
    await page.goto('/journey');
    const preparing = page.getByTestId('journey-preparing');
    await expect(preparing).toBeVisible();
    await expect(preparing).toContainText('learning plan is being prepared');
  });

  test('shows error state when API fails', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/placement/status', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(PLACEMENT_COMPLETED) });
    });
    await page.route('**/api/student/learning-plan/journey', async route => {
      await route.fulfill({ status: 500 });
    });
    await page.goto('/journey');
    await expect(page.getByTestId('journey-error')).toBeVisible();
    await expect(page.getByTestId('journey-retry')).toBeVisible();
  });

  test('retry button reloads journey after error', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/placement/status', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(PLACEMENT_COMPLETED) });
    });
    let callCount = 0;
    await page.route('**/api/student/learning-plan/journey', async route => {
      callCount++;
      if (callCount === 1) {
        await route.fulfill({ status: 500 });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(JOURNEY_WITH_PLAN),
        });
      }
    });
    await page.goto('/journey');
    await expect(page.getByTestId('journey-error')).toBeVisible();
    await page.getByTestId('journey-retry').click();
    await expect(page.getByTestId('journey-cefr')).toBeVisible();
    await expect(page.getByTestId('journey-error')).not.toBeVisible();
  });

});

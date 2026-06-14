/**
 * Phase 5B: Lesson → activity wiring Playwright tests.
 *
 * Covers: prepare on open, Open activity button appears, review step behaviour,
 * refresh preserves activityId, exercise progress persists, session flow.
 */
import { expect, test, type Page } from '@playwright/test';

// ── Helpers ──────────────────────────────────────────────────────────────────

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

const SESSION_ID = 'session-wire-001';
const ACTIVITY_ID = 'activity-aaa-111';
const REVIEW_ACTIVITY_ID = 'activity-review-999';

const BASE_EXERCISES = [
  {
    exerciseId: 'ex-w1',
    order: 0,
    kind: 'writingTask',
    exercisePatternKey: 'writing_response',
    primarySkill: 'Writing',
    instructions: 'Write a professional email explaining a 3-day document delay.',
    estimatedMinutes: 8,
    status: 'notStarted',
    learningActivityId: null,
  },
  {
    exerciseId: 'ex-r1',
    order: 1,
    kind: 'review',
    exercisePatternKey: 'lesson_reflection',
    primarySkill: 'Reflection',
    instructions: 'Reflect on what you practised in this lesson.',
    estimatedMinutes: 2,
    status: 'notStarted',
    learningActivityId: null,
  },
];

const SESSION_DETAIL = {
  sessionId: SESSION_ID,
  title: 'Wiring Test Session',
  topic: 'Professional communication',
  sessionGoal: 'Practice professional workplace communication.',
  durationMinutes: 10,
  focusSkill: 'Writing',
  status: 'inProgress',
  startedAtUtc: new Date().toISOString(),
  completedAtUtc: null,
  exercises: BASE_EXERCISES,
};

async function mockSessionDetail(page: Page, override?: Partial<typeof SESSION_DETAIL>) {
  const body = { ...SESSION_DETAIL, ...override };
  await page.route(`**/api/sessions/${SESSION_ID}`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
}

async function mockPrepare(page: Page, exerciseId: string, activityId: string, isReview = false) {
  await page.route(`**/api/sessions/${SESSION_ID}/exercises/${exerciseId}/prepare`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ activityId, activityType: isReview ? null : 'writingScenario', isReview }),
    });
  });
}

async function mockActivity(page: Page, activityId: string) {
  await page.route(`**/api/activity/${activityId}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        activityId,
        activityType: 'writingScenario',
        source: 'aiGenerated',
        title: 'Write about a delay',
        difficulty: 'B1',
        situation: 'You need to explain a delay professionally.',
        learningGoal: 'Use professional delay language.',
        targetPhrases: ['I wanted to update you'],
        targetVocabulary: ['delay'],
        exampleText: 'Dear team,',
        commonMistakeToAvoid: 'Do not be rude.',
        instructionInSourceLanguage: 'یک ایمیل حرفه‌ای بنویسید.',
      }),
    });
  });
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test('lesson page calls /prepare for first active exercise after Load activity is clicked', async ({ page }) => {
  await withAuth(page);

  let prepareCalled = false;
  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-w1/prepare`, async route => {
    prepareCalled = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ activityId: ACTIVITY_ID, activityType: 'writingScenario', isReview: false }),
    });
  });
  await mockSessionDetail(page);

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('retry-prepare-btn')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('retry-prepare-btn').click();
  await page.waitForTimeout(500);

  expect(prepareCalled).toBe(true);
});

test('open activity button appears after Load activity prepares the exercise', async ({ page }) => {
  await withAuth(page);
  await mockSessionDetail(page);
  await mockPrepare(page, 'ex-w1', ACTIVITY_ID);

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('retry-prepare-btn')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('retry-prepare-btn').click();

  await expect(page.getByTestId('open-activity-btn')).toBeVisible({ timeout: 5000 });
  await expect(page.getByTestId('open-activity-btn')).toContainText('Start module');
});

test('open activity button links to the module route for this exercise', async ({ page }) => {
  await withAuth(page);
  await mockSessionDetail(page);
  await mockPrepare(page, 'ex-w1', ACTIVITY_ID);

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('retry-prepare-btn')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('retry-prepare-btn').click();

  await expect(page.getByTestId('open-activity-btn')).toBeVisible({ timeout: 5000 });
  const href = await page.getByTestId('open-activity-btn').getAttribute('href');
  expect(href).toBe(`/module/session-${SESSION_ID}-ex-w1`);
});

test('review step shows review panel, not open activity button', async ({ page }) => {
  await withAuth(page);
  // Session with only review exercise active (first already done)
  const exercises = BASE_EXERCISES.map((e, i) => ({
    ...e,
    status: i === 0 ? 'completed' : 'notStarted',
  }));
  await mockSessionDetail(page, { exercises });

  await page.goto(`/lesson/${SESSION_ID}`);

  // Click the review exercise to make it active
  const items = page.getByTestId('exercise-item');
  await items.nth(1).click();

  await expect(page.getByTestId('review-panel')).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('open-activity-btn')).not.toBeVisible();
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible();
});

test('review step does not call /prepare', async ({ page }) => {
  await withAuth(page);
  const exercises = BASE_EXERCISES.map((e, i) => ({
    ...e,
    status: i === 0 ? 'completed' : 'notStarted',
  }));
  await mockSessionDetail(page, { exercises });

  let prepareCalled = false;
  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-r1/prepare`, async route => {
    prepareCalled = true;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ activityId: REVIEW_ACTIVITY_ID, isReview: true }) });
  });

  await page.goto(`/lesson/${SESSION_ID}`);
  const items = page.getByTestId('exercise-item');
  await items.nth(1).click();
  await page.waitForTimeout(400);

  expect(prepareCalled).toBe(false);
});

test('refresh preserves activityId — server-assigned learningActivityId shows open button without re-calling prepare', async ({ page }) => {
  await withAuth(page);
  // Server returns session where first exercise already has learningActivityId set
  const exercises = BASE_EXERCISES.map((e, i) =>
    i === 0 ? { ...e, learningActivityId: ACTIVITY_ID } : e
  );
  await mockSessionDetail(page, { exercises });

  let prepareCalled = false;
  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-w1/prepare`, async route => {
    prepareCalled = true;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ activityId: ACTIVITY_ID, isReview: false }) });
  });

  await page.goto(`/lesson/${SESSION_ID}`);

  // Open activity button should appear without prepare being called
  await expect(page.getByTestId('open-activity-btn')).toBeVisible({ timeout: 3000 });
  expect(prepareCalled).toBe(false);
});

test('marking review exercise complete advances to all-done state', async ({ page }) => {
  await withAuth(page);
  const exercises = BASE_EXERCISES.map((e, i) => ({
    ...e,
    status: i === 0 ? 'completed' : 'notStarted',
  }));
  await mockSessionDetail(page, { exercises });

  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-r1/complete`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        exerciseId: 'ex-r1',
        status: 'completed',
        completedAtUtc: new Date().toISOString(),
        sessionComplete: true,
      }),
    });
  });
  await page.route(`**/api/sessions/${SESSION_ID}/complete`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ sessionId: SESSION_ID, status: 'completed', completedAtUtc: new Date().toISOString() }),
    });
  });

  await page.goto(`/lesson/${SESSION_ID}`);
  const items = page.getByTestId('exercise-item');
  await items.nth(1).click();
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible({ timeout: 3000 });
  await page.getByTestId('complete-exercise-btn').click();

  await expect(page.getByTestId('lesson-complete-summary')).toBeVisible({ timeout: 5000 });
});

test('exercise with activity shows both Open activity and Mark complete buttons', async ({ page }) => {
  await withAuth(page);
  await mockSessionDetail(page);
  await mockPrepare(page, 'ex-w1', ACTIVITY_ID);

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('retry-prepare-btn')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('retry-prepare-btn').click();

  await expect(page.getByTestId('open-activity-btn')).toBeVisible({ timeout: 5000 });
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible();
});

test('marking exercise complete after activity assigned shows next exercise', async ({ page }) => {
  await withAuth(page);
  // First exercise has activity already set
  const exercises = BASE_EXERCISES.map((e, i) =>
    i === 0 ? { ...e, learningActivityId: ACTIVITY_ID } : e
  );
  await mockSessionDetail(page, { exercises });

  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-w1/complete`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        exerciseId: 'ex-w1',
        status: 'completed',
        completedAtUtc: new Date().toISOString(),
        sessionComplete: false,
      }),
    });
  });

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible({ timeout: 3000 });
  await page.getByTestId('complete-exercise-btn').click();

  // Review exercise should now be active
  await expect(page.getByTestId('review-panel')).toBeVisible({ timeout: 3000 });
});

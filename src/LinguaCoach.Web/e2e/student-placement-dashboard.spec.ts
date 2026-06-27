/**
 * Phase 14B — Smoke test: placement complete → CourseReady dashboard.
 *
 * Uses the addInitScript/session-storage pattern (same as all existing E2E tests).
 * All API calls are intercepted — no real backend or AI calls.
 */
import { expect, test, type Page } from '@playwright/test';

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

const ASSESSMENT_ID = 'aaaaaaaa-0000-0000-0000-000000000001';
const ITEM_ID_1 = 'bbbbbbbb-0000-0000-0000-000000000001';
const ITEM_ID_2 = 'bbbbbbbb-0000-0000-0000-000000000002';

const QUESTION_1 = {
  itemId: ITEM_ID_1,
  skill: 'grammar',
  targetCefrLevel: 'B1',
  itemType: 'multiple_choice',
  // Component parses choices via /\(([A-Z])\)/ — must use (A) format not A)
  prompt: 'She __ at the meeting yesterday. (A) is present (B) was present (C) are present (D) be present',
  answeredCount: 0,
  estimatedRemainingItems: 1,
};

const QUESTION_2 = {
  itemId: ITEM_ID_2,
  skill: 'vocabulary',
  targetCefrLevel: 'B1',
  itemType: 'multiple_choice',
  prompt: 'What does "delegate" mean in a workplace context? (A) Assign a task to someone else (B) Complete a task alone (C) Cancel a meeting (D) Argue with a colleague',
  answeredCount: 1,
  estimatedRemainingItems: 0,
};

const PLACEMENT_SUMMARY = {
  assessmentId: ASSESSMENT_ID,
  status: 'Completed',
  estimatedOverallLevel: 'B1',
  overallConfidence: 0.78,
  isProvisional: false,
  resultSummary: 'Estimated level: B1. Confidence: 78%.',
  hasPlacement: true,
  skillLevels: [
    { skill: 'grammar', level: 'B1', confidence: 0.80 },
    { skill: 'vocabulary', level: 'B1', confidence: 0.76 },
  ],
  learningPlanRegenerated: true,
  learningPlanWarning: null,
};

async function mockPlacementApis(page: Page) {
  // Placement status (used by placement guard and placement-access guard)
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ status: 'NotStarted', lifecycleStage: 'PlacementRequired', isCompleted: false }),
    });
  });

  // Placement config
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        placementRequiredBeforeLearning: true,
        allowSkipPlacement: false,
        allowPlacementRetake: false,
        autoStartPlacement: false,
      }),
    });
  });

  // No existing placement yet
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ hasPlacement: false }),
    });
  });

  // Start assessment
  await page.route('**/api/student/placement/start', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        assessmentId: ASSESSMENT_ID, status: 'InProgress',
        hasPlacement: true, estimatedOverallLevel: null,
      }),
    });
  });

  // Next item — stateful: first call returns Q1, second Q2
  let nextCallCount = 0;
  await page.route('**/api/student/placement/next**', async route => {
    nextCallCount++;
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify(nextCallCount <= 1 ? QUESTION_1 : QUESTION_2),
    });
  });

  // Respond — first call: incomplete; second call: assessment done
  let respondCallCount = 0;
  await page.route('**/api/student/placement/respond', async route => {
    respondCallCount++;
    if (respondCallCount < 2) {
      await route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({
          itemId: ITEM_ID_1, isCorrect: true, score: 1.0,
          evaluationNotes: 'Correct.', assessmentComplete: false,
          completionReason: null, nextItem: QUESTION_2, summary: null,
        }),
      });
    } else {
      await route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({
          itemId: ITEM_ID_2, isCorrect: true, score: 1.0,
          evaluationNotes: 'Correct.', assessmentComplete: true,
          completionReason: 'confidence_threshold_met', nextItem: null,
          summary: PLACEMENT_SUMMARY,
        }),
      });
    }
  });

  // Complete endpoint (explicit POST to finalise)
  await page.route('**/api/student/placement/complete', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify(PLACEMENT_SUMMARY),
    });
  });
}

async function mockDashboardApis(page: Page, lifecycleStage: 'CourseReady' | 'PlacementCompleted' = 'CourseReady') {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Software engineer',
        cefrLevel: 'B1',
        message: lifecycleStage === 'CourseReady'
          ? 'Your first lesson is being prepared.'
          : 'Your personalised course is being prepared. Practice Gym is available while you wait.',
        lifecycleStage,
        activityStats: null,
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
        learningPath: null,
        streakDays: 0,
      }),
    });
  });

  await page.route('**/api/placement/result', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        estimatedOverallLevel: 'B1',
        skillLevels: [
          { skill: 'Grammar', level: 'B1', confidence: 0.80 },
          { skill: 'Vocabulary', level: 'B1', confidence: 0.76 },
        ],
        strengths: ['grammar accuracy'],
        weaknesses: ['listening'],
        recommendedStartingCourse: 'Workplace English B1',
        recommendedSessionDuration: 15,
        placementNotes: 'Start at B1.',
        isCompleted: true,
      }),
    });
  });

  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: null, strongSkills: [], weakSkills: [],
        recurringMistakes: [], nextRecommendedFocus: [],
        coveredScenarioCount: 0, skillProfile: [],
      }),
    });
  });

  await page.route('**/api/notifications/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ count: 0, notifications: [], items: [] }) });
  });

  await page.route('**/api/practice-gym/suggestions', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        suggestedItems: [], continueItems: [], reviewItems: [],
        readyCount: 0, reviewOnlyCount: 0, reservedCount: 0,
        isReplenishmentRecommended: false, generatedAtUtc: new Date().toISOString(),
      }),
    });
  });

  // Session 404 is a valid business state — dashboard shows "preparing" state.
  // Returning 200 here to test the lesson card with real data.
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        sessionId: 'sess-00001',
        title: 'Your first lesson',
        topic: 'Workplace communication',
        sessionGoal: 'Write a clear project update email',
        durationMinutes: 15,
        focusSkill: 'writing',
        status: 'notStarted',
        isResuming: false,
        exercises: [],
      }),
    });
  });
}

// ── Test 1: Full placement flow → CourseReady dashboard ───────────────────────

test('placement smoke: welcome → two questions → done → CourseReady dashboard', async ({ page }) => {
  await withAuth(page);
  await mockPlacementApis(page);
  await mockDashboardApis(page, 'CourseReady');

  await page.goto('/placement');

  // Welcome state
  await expect(page.getByTestId('placement-welcome')).toBeVisible({ timeout: 8000 });

  // Begin assessment
  await page.getByTestId('placement-begin').click();

  // Question 1
  await expect(page.getByTestId('placement-question')).toBeVisible({ timeout: 6000 });
  await page.getByTestId('placement-choice-B').click();
  await page.getByTestId('placement-submit').click();

  // Question 2 (may appear after first submit if component shows next item inline)
  await expect(page.getByTestId('placement-question')).toBeVisible({ timeout: 6000 });
  await page.getByTestId('placement-choice-A').click();
  await page.getByTestId('placement-submit').click();

  // Done state
  await expect(page.getByTestId('placement-done')).toBeVisible({ timeout: 8000 });

  // Verify the dashboard button exists, then navigate via page.goto for a reliable full load.
  // SPA routing from /placement → /dashboard flips the StudentAppLayoutComponent instance
  // which can delay the /api/dashboard XHR past Playwright's interception window.
  await expect(page.getByTestId('placement-go-to-dashboard')).toBeVisible();
  await page.goto('/dashboard');

  // dashboard-course-ready card shown for CourseReady lifecycle
  await expect(page.getByTestId('today-page')).toBeVisible({ timeout: 6000 });
  await expect(page.getByTestId('dashboard-placement-required')).toHaveCount(0);
  await expect(page.getByTestId('dashboard-course-ready')).toBeVisible({ timeout: 4000 });
});

// ── Test 2: PlacementCompleted (preparing) — course-ready card still shown ────

test('placement smoke: PlacementCompleted lifecycle shows course-ready preparing card', async ({ page }) => {
  await withAuth(page);
  // Navigate directly to dashboard — simulates returning user in PlacementCompleted state
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ status: 'Completed', lifecycleStage: 'PlacementCompleted', isCompleted: true }),
    });
  });
  await mockDashboardApis(page, 'PlacementCompleted');

  await page.goto('/dashboard');
  await page.waitForURL(/\/dashboard/, { timeout: 8000 });

  await expect(page.getByTestId('today-page')).toBeVisible({ timeout: 6000 });
  // Placement-required card must not appear for PlacementCompleted students
  await expect(page.getByTestId('dashboard-placement-required')).toHaveCount(0);
  // Course-ready card must appear
  await expect(page.getByTestId('dashboard-course-ready')).toBeVisible({ timeout: 4000 });
});

// ── Test 3B: Dashboard smoke (Phase 15A) — session 404 → preparing state ────────

test('dashboard 15A: session 404 shows preparing state, not global error', async ({ page }) => {
  await withAuth(page);

  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Software engineer',
        cefrLevel: 'B1',
        message: 'Ready.',
        lifecycleStage: 'CourseReady',
        activityStats: { activitiesCompleted: 5, latestScore: 70, averageScore: 68 },
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
        learningPath: {
          pathId: 'p1', title: 'Business English',
          modulesCompleted: 1, totalModules: 4,
          currentModule: {
            moduleId: 'm1', title: 'Meetings', description: 'Lead meetings.',
            order: 2, completedActivities: 2, totalActivities: 6,
            isReadyToComplete: false, averageScore: null,
          },
        },
        streakDays: 2,
      }),
    });
  });

  await page.route('**/api/placement/result', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ estimatedOverallLevel: 'B1', skillLevels: [], strengths: [], weaknesses: [], isCompleted: true }),
    });
  });

  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ journeySummary: null, strongSkills: [], weakSkills: [], recurringMistakes: [], nextRecommendedFocus: [], coveredScenarioCount: 0, skillProfile: [] }) });
  });

  await page.route('**/api/notifications/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ count: 0, notifications: [], items: [] }) });
  });

  await page.route('**/api/practice-gym/suggestions', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ suggestedItems: [], continueItems: [], reviewItems: [], readyCount: 0, reviewOnlyCount: 0, reservedCount: 0, isReplenishmentRecommended: false, generatedAtUtc: new Date().toISOString() }),
    });
  });

  // 404 for today's session — this is the bug scenario being fixed
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ error: 'No session' }) });
  });

  await page.goto('/dashboard');

  // Dashboard must load, not show a global error
  await expect(page.getByTestId('today-page')).toBeVisible({ timeout: 8000 });

  // Lesson card shows "preparing" state, not an error
  const lessonCard = page.getByTestId('dashboard-todays-lesson');
  await expect(lessonCard).toBeVisible({ timeout: 4000 });
  await expect(lessonCard).toContainText('being prepared');

  // Stats render with real backend data
  await expect(page.getByTestId('stat-cefr')).toContainText('B1');
  await expect(page.getByTestId('stat-streak')).toContainText('2');

  // Learning plan card renders
  await expect(page.getByTestId('today-learning-path')).toBeVisible();
  await expect(page.getByTestId('today-learning-path')).toContainText('Meetings');

  // Practice card renders with "preparing" state
  await expect(page.getByTestId('dashboard-practice-card')).toBeVisible();
});

// ── Test 3: Placement guard blocks /journey for PlacementRequired students ─────

test('placement guard: PlacementRequired student navigating to /journey lands on /placement', async ({ page }) => {
  await withAuth(page);
  // Guard calls /api/placement/status
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ status: 'NotStarted', lifecycleStage: 'PlacementRequired', isCompleted: false }),
    });
  });
  // Placement page APIs so the component can render after redirect
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false,
        allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ hasPlacement: false }) });
  });

  await page.goto('/journey');
  await page.waitForURL(/\/placement/, { timeout: 8000 });
  await expect(page.getByTestId('placement-page')).toBeVisible({ timeout: 4000 });
});

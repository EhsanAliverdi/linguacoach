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
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasPlacement: false }) });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-welcome')).toBeVisible();
  await expect(page.getByText('Find your English level')).toBeVisible();
  await expect(page.getByTestId('placement-begin')).toBeVisible();
});

test('placement section flow renders questions and continues', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasPlacement: false }) });
  });
  await page.route('**/api/student/placement/start', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ assessmentId: 'assess-1', status: 'InProgress', hasPlacement: true, estimatedOverallLevel: null }) });
  });
  await page.route('**/api/student/placement/next**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({
        itemId: 'item-1', skill: 'grammar', targetCefrLevel: 'B1', itemType: 'multiple_choice',
        prompt: 'She __ at the meeting yesterday. (A) is present (B) was present (C) are present (D) be present',
        answeredCount: 0, estimatedRemainingItems: 5,
      }) });
  });

  await page.goto('/placement');
  await page.getByTestId('placement-begin').click();

  await expect(page.getByTestId('placement-question')).toBeVisible();
  await expect(page.getByTestId('placement-question-label')).toContainText('Question 1');

  // Select an answer — submit becomes enabled.
  await page.getByTestId('placement-choice-B').click();
  await expect(page.getByTestId('placement-submit')).toBeEnabled();
});

test('placement result page shows level and skill breakdown', async ({ page }) => {
  await withAuth(page);
  // Guard redirects CourseReady → /dashboard; use PlacementInProgress to stay on /placement.
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'InProgress', lifecycleStage: 'PlacementInProgress' })) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({
        assessmentId: 'assess-1', status: 'Completed', hasPlacement: true,
        overallCefrLevel: 'B1', isProvisional: false,
        skillResults: [
          { skill: 'grammar', estimatedCefrLevel: 'B1', confidence: 0.80 },
          { skill: 'vocabulary', estimatedCefrLevel: 'B1+', confidence: 0.76 },
        ],
        learningPlanRegenerated: true, learningPlanWarning: null,
      }) });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-done')).toBeVisible();
  await expect(page.getByTestId('placement-result-level')).toContainText('B1');
  await expect(page.getByText('Grammar')).toBeVisible();
  await expect(page.getByTestId('placement-go-to-dashboard')).toBeVisible();
});

test('dashboard shows placement CTA when placement is required', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'student@test.com', cefrLevel: null, supportLanguage: null },
        courseReadiness: { isLearningReady: false, lifecycleStatus: 'PlacementRequired', placementRequired: true, learningPlanExists: false },
        todaySession: { status: 'NotAvailable', sessionId: null, title: null, topic: null, sessionGoal: null, focusSkill: null, durationMinutes: null, exerciseCount: null, actionLabel: '' },
        learningPlan: { pathTitle: null, currentObjective: null, currentObjectiveDescription: null, objectiveIndex: 0, totalObjectives: 0, modulesCompleted: 0, remainingObjectives: 0, completedActivities: 0, totalActivities: 0, progressPercent: 0 },
        practice: { status: 'NotAvailable', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: null, streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: false, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
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
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ status: 'NotStarted', lifecycleStage: 'PlacementRequired', isCompleted: false }),
    });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasPlacement: false }) });
  });

  await page.goto('/placement');

  await expect(page.getByTestId('placement-welcome')).toBeVisible();
  await expect(page.getByText('Could not load your placement')).toHaveCount(0);
  await expect(page.getByTestId('placement-begin')).toBeVisible();
});

test('Start placement button calls /api/placement/start and transitions to section', async ({ page }) => {
  await withAuth(page);
  let startCalled = false;
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasPlacement: false }) });
  });
  await page.route('**/api/student/placement/start', async route => {
    startCalled = true;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ assessmentId: 'assess-1', status: 'InProgress', hasPlacement: true, estimatedOverallLevel: null }) });
  });
  await page.route('**/api/student/placement/next**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({
        itemId: 'item-1', skill: 'grammar', targetCefrLevel: 'B1', itemType: 'multiple_choice',
        prompt: 'She __ at the meeting. (A) is present (B) was present (C) are present (D) be present',
        answeredCount: 0, estimatedRemainingItems: 5,
      }) });
  });

  await page.goto('/placement');
  await expect(page.getByTestId('placement-begin')).toBeVisible();
  await page.getByTestId('placement-begin').click();

  await expect(page.getByTestId('placement-question')).toBeVisible();
  expect(startCalled).toBe(true);
});

test('PlacementRequired dashboard CTA routes to /placement and page loads intro', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'student@test.com', cefrLevel: null, supportLanguage: null },
        courseReadiness: { isLearningReady: false, lifecycleStatus: 'PlacementRequired', placementRequired: true, learningPlanExists: false },
        todaySession: { status: 'NotAvailable', sessionId: null, title: null, topic: null, sessionGoal: null, focusSkill: null, durationMinutes: null, exerciseCount: null, actionLabel: '' },
        learningPlan: { pathTitle: null, currentObjective: null, currentObjectiveDescription: null, objectiveIndex: 0, totalObjectives: 0, modulesCompleted: 0, remainingObjectives: 0, completedActivities: 0, totalActivities: 0, progressPercent: 0 },
        practice: { status: 'NotAvailable', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: null, streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: false, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
  });
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status()) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ hasPlacement: false }) });
  });

  await page.goto('/dashboard');
  await expect(page.getByTestId('dashboard-placement-required')).toBeVisible();

  await page.getByRole('link', { name: 'Start placement' }).click();
  await expect(page).toHaveURL(/\/placement/);
  await expect(page.getByTestId('placement-welcome')).toBeVisible();
  await expect(page.getByText('Could not load your placement')).toHaveCount(0);
});

test('placement does not expose correct answers in the DOM', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(status({ status: 'InProgress' })) });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }) });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ assessmentId: 'assess-1', status: 'InProgress', hasPlacement: true, estimatedOverallLevel: null }) });
  });
  await page.route('**/api/student/placement/next**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({
        itemId: 'item-1', skill: 'vocabulary', targetCefrLevel: 'B1', itemType: 'multiple_choice',
        prompt: 'What does "delegate" mean? (A) Assign a task (B) Complete alone (C) Cancel (D) Argue',
        answeredCount: 0, estimatedRemainingItems: 5,
      }) });
  });

  await page.goto('/placement');
  await expect(page.getByTestId('placement-question')).toBeVisible();
  await expect(page.locator('body')).not.toContainText('correctOption');
});

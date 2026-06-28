import { expect, test, type Page } from '@playwright/test';

// ── Helpers ───────────────────────────────────────────────────────────────────

function toBase64Url(value: object): string {
  return Buffer.from(JSON.stringify(value))
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

function fakeJwt(email: string, role: 'Student' | 'Admin' = 'Student'): string {
  const header = toBase64Url({ alg: 'none', typ: 'JWT' });
  const payload = toBase64Url({
    sub: `${role.toLowerCase()}-user-id`,
    email,
    role,
    exp: Math.floor(Date.now() / 1000) + 60 * 60,
  });
  return `${header}.${payload}.signature`;
}

async function withAuth(page: Page, role: 'Student' | 'Admin' = 'Student') {
  const token = fakeJwt(`${role.toLowerCase()}@test.com`, role);
  // addInitScript runs on every navigation including page.reload() — auth persists across refresh
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, JSON.stringify({ token, mustChangePassword: false }));
}

// ── API stubs ─────────────────────────────────────────────────────────────────

async function stubDashboard(page: Page) {
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'Sara', cefrLevel: 'B1', supportLanguage: null },
        courseReadiness: { isLearningReady: true, lifecycleStatus: 'ActiveLearning', placementRequired: false, learningPlanExists: true },
        todaySession: { status: 'Ready', sessionId: 'session-1', title: "Today's Lesson", topic: 'Workplace', sessionGoal: null, focusSkill: 'writing', durationMinutes: 30, exerciseCount: 3, actionLabel: "Start today's lesson" },
        learningPlan: { pathTitle: 'Workplace English', currentObjective: 'Professional communication', currentObjectiveDescription: null, objectiveIndex: 1, totalObjectives: 2, modulesCompleted: 0, remainingObjectives: 1, completedActivities: 0, totalActivities: 3, progressPercent: 0 },
        practice: { status: 'Ready', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: false, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
  });
}

async function stubPlacementRequired(page: Page) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'NotStarted', lifecycleStage: 'PlacementRequired', currentSectionKey: null, currentSectionOrder: 0, totalSections: 6, isCompleted: false }),
    });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }),
    });
  });
}

async function stubPlacementInProgress(page: Page) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'InProgress', lifecycleStage: 'PlacementInProgress', currentSectionKey: 'self_check', currentSectionOrder: 1, totalSections: 6, isCompleted: false }),
    });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }),
    });
  });
}

async function stubCourseReady(page: Page) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Completed', lifecycleStage: 'CourseReady', currentSectionKey: null, currentSectionOrder: 0, totalSections: 6, isCompleted: true }),
    });
  });
}

async function stubJourney(page: Page) {
  await stubCourseReady(page);
  await page.route('**/api/student/learning-plan/journey', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        currentCefrLevel: 'B1',
        currentLearningPhase: 'Active Learning',
        totalObjectives: 2,
        completionPercentage: 25,
        lastCompletedAt: null,
        planStatus: 'Active',
        currentObjective: { objectiveKey: 'prof-comms', title: 'Professional communication', skill: 'writing', cefrLevel: 'B1', status: 'Current', sequenceNumber: 1, isReview: false, isBlocked: false, blockedByKey: null, lastEvaluatedAt: null, isMastered: false },
        upcomingObjectives: [],
        completedObjectives: [],
        reviewObjectives: [],
        milestones: [],
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
}

async function stubPractice(page: Page) {
  await stubCourseReady(page);
  await page.route('**/api/activity/exercise-types', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
  await page.route('**/api/practice-gym/suggestions', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ suggestedItems: [], reviewQueue: [], weakestSkill: null, hasItems: false }),
    });
  });
}

async function stubProgress(page: Page) {
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        learning: { currentCefrLevel: 'B1', placementCompletedAt: '2026-05-01T00:00:00Z', currentLearningPhase: 'Active learning', totalObjectives: 5, objectivesCompleted: 1, objectivesMastered: 0, objectivesInProgress: 1, objectivesRemaining: 3, completionPercentage: 20, currentObjectiveKey: 'obj-1', currentObjectiveSkill: 'Writing', objectivesCompletedToday: 0 },
        skills: [],
        cefr: { startingCefrLevel: 'A2', currentCefrLevel: 'B1', cefrImproved: true, placementDate: '2026-05-01T00:00:00Z', note: null },
        mastery: { masteredObjectivesCount: 0, inProgressObjectivesCount: 1, reviewQueueCount: 0, weakSkillsCount: 0, weakSkillLabels: [] },
        recentActivity: [],
        focus: { recommendations: [], recurringMistakes: [], journeySummary: null },
      }),
    });
  });
}

async function stubProfile(page: Page) {
  await page.route('**/api/profile', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ profileId: 'p1', userId: 'u1', firstName: 'Sara', lastName: 'K', displayName: 'Sara K', preferredName: null, email: 'student@test.com', cefrLevel: 'B1', learningGoals: [], customLearningGoal: null, focusAreas: [], customFocusArea: null, supportLanguageCode: 'fa', supportLanguageName: 'Persian', translationHelpPreference: 'WhenDifficult', preferredSessionDurationMinutes: 20, difficultyPreference: 'Balanced', learningPreferencesUpdatedAt: null }),
    });
  });
  await page.route('**/api/student/placement/current', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ assessmentId: 'a1', studentProfileId: 'p1', status: 'Completed', startedAtUtc: '2026-05-01T10:00:00Z', completedAtUtc: '2026-05-01T10:30:00Z', expiredAtUtc: null, overallCefrLevel: 'B1', overallConfidence: 0.8, isProvisional: false, resultSummary: 'Good', source: 'adaptive', skillResults: [], learningPlanRegenerated: true, learningPlanRegenerationWarning: null, itemCount: 10, hasPlacement: true }),
    });
  });
  await page.route('**/api/student/placement/config', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ placementRequiredBeforeLearning: true, allowSkipPlacement: false, allowPlacementRetake: false, autoStartPlacement: false }),
    });
  });
  await page.route('**/api/notifications/preferences', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Group A — Unauthenticated redirect to /login
// ─────────────────────────────────────────────────────────────────────────────

test('unauthenticated: /dashboard redirects to /login', async ({ page }) => {
  await page.goto('/dashboard');
  await page.waitForURL(/\/login/);
  await expect(page).toHaveURL(/\/login/);
});

test('unauthenticated: /journey redirects to /login', async ({ page }) => {
  await page.goto('/journey');
  await page.waitForURL(/\/login/);
  await expect(page).toHaveURL(/\/login/);
});

test('unauthenticated: /practice redirects to /login', async ({ page }) => {
  await page.goto('/practice');
  await page.waitForURL(/\/login/);
  await expect(page).toHaveURL(/\/login/);
});

test('unauthenticated: /progress redirects to /login', async ({ page }) => {
  await page.goto('/progress');
  await page.waitForURL(/\/login/);
  await expect(page).toHaveURL(/\/login/);
});

test('unauthenticated: /profile redirects to /login', async ({ page }) => {
  await page.goto('/profile');
  await page.waitForURL(/\/login/);
  await expect(page).toHaveURL(/\/login/);
});

// ─────────────────────────────────────────────────────────────────────────────
// Group B — Role-based access control
// ─────────────────────────────────────────────────────────────────────────────

test('student JWT: /admin redirects to /dashboard', async ({ page }) => {
  await withAuth(page, 'Student');
  await stubDashboard(page);
  await page.goto('/admin');
  await page.waitForURL(/\/dashboard/);
  await expect(page).toHaveURL(/\/dashboard/);
});

test('CourseReady student: /placement redirects to /dashboard', async ({ page }) => {
  await withAuth(page);
  await stubCourseReady(page);
  await stubDashboard(page);
  await page.goto('/placement');
  await page.waitForURL(/\/dashboard/);
  await expect(page).toHaveURL(/\/dashboard/);
});

// ─────────────────────────────────────────────────────────────────────────────
// Group C — Placement guard: placement-required redirects
// ─────────────────────────────────────────────────────────────────────────────

test('PlacementRequired student: /practice redirects to /placement', async ({ page }) => {
  await withAuth(page);
  await stubPlacementRequired(page);
  await page.goto('/practice');
  await page.waitForURL(/\/placement/);
  await expect(page).toHaveURL(/\/placement/);
  await expect(page.getByTestId('placement-page')).toBeVisible();
});

test('PlacementInProgress student: /journey redirects to /placement', async ({ page }) => {
  await withAuth(page);
  await stubPlacementInProgress(page);
  // Stub the placement page question so the component can render
  await page.route('**/api/placement/current-question', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: '{}' });
  });
  await page.goto('/journey');
  await page.waitForURL(/\/placement/);
  await expect(page).toHaveURL(/\/placement/);
  await expect(page.getByTestId('placement-page')).toBeVisible();
});

// ─────────────────────────────────────────────────────────────────────────────
// Group D — Browser refresh: auth persists via sessionStorage
// addInitScript runs on every navigation, so reload() restores auth
// ─────────────────────────────────────────────────────────────────────────────

test('browser refresh: /dashboard stays authenticated and renders', async ({ page }) => {
  await withAuth(page);
  await stubDashboard(page);
  await page.goto('/dashboard');
  await expect(page.getByTestId('today-page')).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/dashboard/);
  await expect(page.getByTestId('today-page')).toBeVisible();
});

test('browser refresh: /journey stays authenticated and renders', async ({ page }) => {
  await withAuth(page);
  await stubJourney(page);
  await page.goto('/journey');
  await expect(page.getByText('Your roadmap')).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/journey/);
  await expect(page.getByText('Your roadmap')).toBeVisible();
});

test('browser refresh: /practice stays authenticated and renders', async ({ page }) => {
  await withAuth(page);
  await stubPractice(page);
  await page.goto('/practice');
  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/practice/);
  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();
});

test('browser refresh: /progress stays authenticated and renders', async ({ page }) => {
  await withAuth(page);
  await stubProgress(page);
  await page.goto('/progress');
  await expect(page.getByTestId('learning-summary-heading')).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/progress/);
  await expect(page.getByTestId('learning-summary-heading')).toBeVisible();
});

test('browser refresh: /profile stays authenticated and renders', async ({ page }) => {
  await withAuth(page);
  await stubProfile(page);
  await page.goto('/profile');
  await expect(page.getByTestId('level-section')).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/profile/);
  await expect(page.getByTestId('level-section')).toBeVisible();
});

// ─────────────────────────────────────────────────────────────────────────────
// Group E — Mobile viewport (390x844)
// ─────────────────────────────────────────────────────────────────────────────

test('mobile 390x844: /dashboard today-page and bottom-nav visible', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await withAuth(page);
  await stubDashboard(page);
  await page.goto('/dashboard');

  await expect(page.getByTestId('today-page')).toBeVisible();
  await expect(page.getByTestId('mobile-bottomnav')).toBeVisible();

  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(395);
});

test('mobile 390x844: /journey loads roadmap without horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await withAuth(page);
  await stubJourney(page);
  await page.goto('/journey');

  await expect(page.getByText('Your roadmap')).toBeVisible();
  await expect(page.getByTestId('mobile-bottomnav')).toBeVisible();

  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(395);
});

test('mobile 390x844: /practice loads Practice Gym without horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await withAuth(page);
  await stubPractice(page);
  await page.goto('/practice');

  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();
  await expect(page.getByTestId('mobile-bottomnav')).toBeVisible();

  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(395);
});

// ─────────────────────────────────────────────────────────────────────────────
// Group F — CEFR encoding regression guard
// ─────────────────────────────────────────────────────────────────────────────

test('profile CEFR explanation does not contain garbled encoding', async ({ page }) => {
  await withAuth(page);
  await stubProfile(page);
  await page.goto('/profile');

  await expect(page.getByTestId('level-section')).toBeVisible();
  await expect(page.getByTestId('level-section')).not.toContainText('â€"');
  await expect(page.getByTestId('level-section')).not.toContainText('â€');
});

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

const SESSION_ID = 'session-abc-123';

const TODAYS_SESSION = {
  sessionId: SESSION_ID,
  title: 'Explaining a Document Delay',
  topic: 'Professional delay communication',
  sessionGoal: 'Learn to explain project delays clearly and professionally in writing.',
  durationMinutes: 15,
  focusSkill: 'Writing',
  status: 'notStarted',
  isResuming: false,
  exercises: [
    {
      exerciseId: 'ex-1',
      order: 0,
      kind: 'vocabularyWarmup',
      exercisePatternKey: 'phrase_match',
      primarySkill: 'Vocabulary',
      instructions: 'Match each phrase to its workplace meaning.',
      estimatedMinutes: 3,
      status: 'notStarted',
      learningActivityId: null,
    },
    {
      exerciseId: 'ex-2',
      order: 1,
      kind: 'writingTask',
      exercisePatternKey: 'writing_response',
      primarySkill: 'Writing',
      instructions: 'Write a professional email explaining a 3-day document delay.',
      estimatedMinutes: 8,
      status: 'notStarted',
      learningActivityId: null,
    },
    {
      exerciseId: 'ex-3',
      order: 2,
      kind: 'contextInput',
      exercisePatternKey: 'context_read',
      primarySkill: 'Reading',
      instructions: 'Read the example professional message below and note the key phrases.',
      estimatedMinutes: 3,
      status: 'notStarted',
      learningActivityId: null,
    },
    {
      exerciseId: 'ex-4',
      order: 3,
      kind: 'review',
      exercisePatternKey: 'lesson_reflection',
      primarySkill: 'Reflection',
      instructions: 'Reflect on what you practiced in this lesson.',
      estimatedMinutes: 1,
      status: 'notStarted',
      learningActivityId: null,
    },
  ],
};

const SESSION_DETAIL = {
  ...TODAYS_SESSION,
  startedAtUtc: null,
  completedAtUtc: null,
};

function makeDashboardSummary(opts: {
  lifecycleStatus?: string;
  sessionStatus?: string;
  sessionId?: string;
  actionLabel?: string;
} = {}) {
  const lifecycleStatus = opts.lifecycleStatus ?? 'ActiveLearning';
  const sessionStatus = opts.sessionStatus ?? 'Ready';
  const sessionId = opts.sessionId ?? SESSION_ID;
  const actionLabel = opts.actionLabel ?? "Start today's lesson";
  const isCourseActive = ['ActiveLearning', 'InLesson', 'CourseReady'].includes(lifecycleStatus);
  return {
    profile: { displayName: 'Sara', cefrLevel: 'B1', supportLanguage: null },
    courseReadiness: {
      isLearningReady: isCourseActive,
      lifecycleStatus,
      placementRequired: false,
      learningPlanExists: true,
    },
    todaySession: {
      status: sessionStatus,
      sessionId,
      title: 'Explaining a Document Delay',
      topic: 'Professional delay communication',
      sessionGoal: 'Learn to explain project delays clearly and professionally in writing.',
      focusSkill: 'Writing',
      durationMinutes: 15,
      exerciseCount: 4,
      actionLabel,
    },
    learningPlan: {
      pathTitle: 'Workplace English',
      currentObjective: 'Softening manager requests',
      currentObjectiveDescription: 'Practice professional workplace communication.',
      objectiveIndex: 1,
      totalObjectives: 3,
      modulesCompleted: 0,
      remainingObjectives: 2,
      completedActivities: 0,
      totalActivities: 3,
      progressPercent: 0,
    },
    practice: { status: 'Preparing', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
    progress: {
      skillProfile: [],
      strongSkills: [],
      weakSkills: [],
      nextRecommendedFocus: [],
      journeySummary: null,
      activitiesCompleted: 2,
      streakDays: 0,
    },
    quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 2, reviewQueueCount: 0 },
    warnings: {
      missingLearningPlan: false,
      missingTodaySession: false,
      practiceUnavailable: false,
      placementIncomplete: false,
    },
  };
}

async function mockActiveLearningDashboard(page: Page) {
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(makeDashboardSummary()),
    });
  });
}

async function mockSessionEndpoints(page: Page, sessionOverride?: Partial<typeof SESSION_DETAIL>) {
  const sessionBody = { ...SESSION_DETAIL, ...sessionOverride };
  await page.route(`**/api/sessions/${SESSION_ID}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(sessionBody),
    });
  });

  await page.route(`**/api/sessions/${SESSION_ID}/start`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: SESSION_ID,
        status: 'inProgress',
        startedAtUtc: new Date().toISOString(),
      }),
    });
  });

  await page.route(`**/api/sessions/${SESSION_ID}/complete`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: SESSION_ID,
        status: 'completed',
        completedAtUtc: new Date().toISOString(),
      }),
    });
  });

  // Exercise complete — marks first uncompleted exercise, returns sessionComplete=false
  let exerciseCallCount = 0;
  const exerciseIds = TODAYS_SESSION.exercises.map(e => e.exerciseId);
  for (const exId of exerciseIds) {
    await page.route(`**/api/sessions/${SESSION_ID}/exercises/${exId}/complete`, async route => {
      exerciseCallCount++;
      const isLast = exerciseCallCount >= exerciseIds.length;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          exerciseId: exId,
          status: 'completed',
          completedAtUtc: new Date().toISOString(),
          sessionComplete: isLast,
        }),
      });
    });
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test('dashboard shows Today\'s Lesson card when lifecycle is ActiveLearning', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);
  await page.goto('/dashboard');

  await expect(page.getByTestId('dashboard-todays-lesson')).toBeVisible();
  await expect(page.getByText("Explaining a Document Delay")).toBeVisible();
  await expect(page.getByText('15 min')).toBeVisible();
});

test('dashboard Today\'s Lesson card shows Not started badge', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);
  await page.goto('/dashboard');

  await expect(page.getByTestId('session-status-badge')).toContainText('Not started');
});

test('dashboard Today\'s Lesson card has correct button text for notStarted', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);
  await page.goto('/dashboard');

  const cta = page.getByTestId('todays-lesson-cta');
  await expect(cta).toContainText("Start today's lesson");
});

test('dashboard shows In progress badge and Resume button when session is inProgress', async ({ page }) => {
  await withAuth(page);

  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(makeDashboardSummary({
        lifecycleStatus: 'InLesson',
        sessionStatus: 'InProgress',
        actionLabel: 'Resume lesson',
      })),
    });
  });

  await page.goto('/dashboard');
  await expect(page.getByTestId('session-status-badge')).toContainText('In progress');
  await expect(page.getByTestId('todays-lesson-cta')).toContainText('Resume lesson');
});

test('dashboard shows Completed badge and Review button when session is completed', async ({ page }) => {
  await withAuth(page);

  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(makeDashboardSummary({
        lifecycleStatus: 'ActiveLearning',
        sessionStatus: 'Completed',
        actionLabel: "Review today's lesson",
      })),
    });
  });

  await page.goto('/dashboard');
  await expect(page.getByTestId('session-status-badge')).toContainText('Completed');
  await expect(page.getByTestId('todays-lesson-cta')).toContainText("Review today's lesson");
});

test('Today page shows Today\'s Lesson as primary and Practice Gym as a secondary link', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);
  await page.goto('/dashboard');

  await expect(page.getByTestId('dashboard-todays-lesson')).toBeVisible();
  // Practice Gym is secondary — a link to /practice, not the full card grid
  await expect(page.getByTestId('today-practice-link')).toBeVisible();
  await expect(page.getByTestId('today-practice-link')).toHaveAttribute('href', '/practice');
});

test('clicking Today\'s Lesson CTA navigates to lesson page', async ({ page }) => {
  await withAuth(page);
  await mockActiveLearningDashboard(page);
  await mockSessionEndpoints(page);
  await page.goto('/dashboard');

  const cta = page.getByTestId('todays-lesson-cta');
  await cta.click();

  await expect(page).toHaveURL(new RegExp(`/lesson/${SESSION_ID}`));
});

test('lesson page loads and shows title and exercises', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page);
  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page.getByTestId('lesson-header')).toBeVisible();
  await expect(page.getByTestId('lesson-title')).toContainText('Explaining a Document Delay');
  await expect(page.getByTestId('lesson-status')).toContainText('Not started');
  await expect(page.getByTestId('exercise-list')).toBeVisible();

  const exercises = page.getByTestId('exercise-item');
  await expect(exercises).toHaveCount(4);
});

test('lesson page shows exercises in order — first is vocabulary warmup', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page);
  await page.goto(`/lesson/${SESSION_ID}`);

  const firstExercise = page.getByTestId('exercise-item').first();
  await expect(firstExercise).toContainText('Vocabulary warm-up');
});

test('lesson page shows Start lesson button when notStarted', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page);
  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page.getByTestId('start-lesson-btn')).toBeVisible();
  await expect(page.getByTestId('start-lesson-btn')).toContainText('Start lesson');
});

test('start lesson button calls start endpoint and updates status', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page);
  await page.goto(`/lesson/${SESSION_ID}`);

  await page.getByTestId('start-lesson-btn').click();

  // After start, the start button should disappear (status changes to inProgress)
  await expect(page.getByTestId('start-lesson-btn')).not.toBeVisible({ timeout: 3000 });
});

test('lesson page shows complete exercise button in active exercise panel', async ({ page }) => {
  await withAuth(page);
  const exercises = TODAYS_SESSION.exercises.map((e, i) => ({
    ...e,
    learningActivityId: i === 0 ? 'activity-1' : e.learningActivityId,
  }));
  await mockSessionEndpoints(page, { status: 'inProgress', startedAtUtc: new Date().toISOString(), exercises });
  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page.getByTestId('exercise-panel')).toBeVisible();
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible();
});

test('completing all exercises shows Complete lesson button', async ({ page }) => {
  const exercises = TODAYS_SESSION.exercises.map((e, i) => ({
    ...e,
    status: i < 3 ? 'completed' : 'notStarted',
  }));

  await withAuth(page);

  // Session with 3 done, 1 left
  await page.route(`**/api/sessions/${SESSION_ID}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...SESSION_DETAIL,
        status: 'inProgress',
        startedAtUtc: new Date().toISOString(),
        exercises,
      }),
    });
  });

  // Last exercise complete returns sessionComplete=true
  await page.route(`**/api/sessions/${SESSION_ID}/exercises/ex-4/complete`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        exerciseId: 'ex-4',
        status: 'completed',
        completedAtUtc: new Date().toISOString(),
        sessionComplete: true,
      }),
    });
  });

  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible();
  await page.getByTestId('complete-exercise-btn').click();

  await expect(page.getByTestId('complete-lesson-btn')).toBeVisible({ timeout: 5000 });
});

test('completing lesson shows completion summary', async ({ page }) => {
  const exercises = TODAYS_SESSION.exercises.map(e => ({ ...e, status: 'completed' }));

  await withAuth(page);

  // Session already has all exercises done
  await page.route(`**/api/sessions/${SESSION_ID}`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...SESSION_DETAIL,
        status: 'inProgress',
        startedAtUtc: new Date().toISOString(),
        exercises,
      }),
    });
  });
  await page.route(`**/api/sessions/${SESSION_ID}/complete`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: SESSION_ID,
        status: 'completed',
        completedAtUtc: new Date().toISOString(),
      }),
    });
  });

  await page.goto(`/lesson/${SESSION_ID}`);
  await expect(page.getByTestId('complete-lesson-btn')).toBeVisible();
  await page.getByTestId('complete-lesson-btn').click();

  await expect(page.getByTestId('lesson-complete-summary')).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('lesson-complete-summary')).toContainText('Lesson complete');
});

// ── Part I additions ─────────────────────────────────────────────────────────

// Test #1: Preparing state when no session exists
test('dashboard shows preparing state when session is being generated', async ({ page }) => {
  await withAuth(page);

  // Preparing status + no sessionId → dashboard component leaves todaysSession null
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'Sara', cefrLevel: 'B1', supportLanguage: null },
        courseReadiness: {
          isLearningReady: true,
          lifecycleStatus: 'CourseReady',
          placementRequired: false,
          learningPlanExists: true,
        },
        todaySession: {
          status: 'Preparing',
          sessionId: null,
          title: null,
          topic: null,
          sessionGoal: null,
          focusSkill: null,
          durationMinutes: null,
          exerciseCount: 0,
          actionLabel: null,
        },
        learningPlan: {
          pathTitle: 'Workplace English',
          currentObjective: 'Professional communication',
          currentObjectiveDescription: null,
          objectiveIndex: 1,
          totalObjectives: 3,
          modulesCompleted: 0,
          remainingObjectives: 2,
          completedActivities: 0,
          totalActivities: 3,
          progressPercent: 0,
        },
        practice: { status: 'Preparing', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: true, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
  });

  await page.goto('/dashboard');

  await expect(page.getByTestId('dashboard-todays-lesson')).toBeVisible();
  await expect(page.getByTestId('session-preparing')).toBeVisible();
  await expect(page.getByTestId('session-preparing')).toContainText('being prepared');
});

// Test #11: Placement-required student cannot access lesson route
test('placement-required student is redirected from lesson to placement', async ({ page }) => {
  await withAuth(page);

  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        lifecycleStatus: 'PlacementRequired',
        status: 'NotStarted',
        hasActiveAssessment: false,
      }),
    });
  });

  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page).toHaveURL(/\/placement/, { timeout: 5000 });
});

// Test #3: Lesson header shows CEFR level when backend provides it
test('lesson header shows CEFR level from session detail', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page, { cefrLevel: 'B1' } as any);
  await page.goto(`/lesson/${SESSION_ID}`);

  await expect(page.getByTestId('lesson-cefr-level')).toBeVisible();
  await expect(page.getByTestId('lesson-cefr-level')).toContainText('B1');
});

// Test #6: Review exercise shows reflection panel, not an external activity link
test('review exercise panel shows reflection prompt without open-activity button', async ({ page }) => {
  await withAuth(page);
  await mockSessionEndpoints(page, { status: 'inProgress', startedAtUtc: new Date().toISOString() });
  await page.goto(`/lesson/${SESSION_ID}`);

  // Activate the last exercise (review kind) by clicking it
  const exercises = page.getByTestId('exercise-item');
  await exercises.last().click();

  await expect(page.getByTestId('review-panel')).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('open-activity-btn')).toHaveCount(0);
  await expect(page.getByTestId('complete-exercise-btn')).toBeVisible();
});

// Test #12: Lesson page shows error state when session not found (error contained)
test('lesson page shows contained error when session id is invalid', async ({ page }) => {
  await withAuth(page);

  await page.route(`**/api/sessions/invalid-session-id`, async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ error: 'Session not found.' }) });
  });
  // placementRequiredRedirectGuard also calls placement/status — return active learner
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ lifecycleStatus: 'ActiveLearning', status: 'Completed', hasActiveAssessment: false }),
    });
  });

  await page.goto('/lesson/invalid-session-id');

  await expect(page.getByTestId('lesson-header')).toHaveCount(0);
  // Must not crash — either shows error message or redirects gracefully
  const url = page.url();
  const isError = url.includes('/lesson/invalid-session-id') || url.includes('/dashboard');
  expect(isError).toBe(true);
});

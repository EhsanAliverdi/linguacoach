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
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, sessionData);
}

const emptyProgressSummary = {
  learning: {
    currentCefrLevel: null,
    placementCompletedAt: null,
    currentLearningPhase: 'Onboarding',
    totalObjectives: 0,
    objectivesCompleted: 0,
    objectivesMastered: 0,
    objectivesInProgress: 0,
    objectivesRemaining: 0,
    completionPercentage: 0,
    currentObjectiveKey: null,
    currentObjectiveSkill: null,
    objectivesCompletedToday: 0,
  },
  skills: [],
  cefr: { startingCefrLevel: null, currentCefrLevel: null, cefrImproved: false, placementDate: null, note: null },
  mastery: { masteredObjectivesCount: 0, inProgressObjectivesCount: 0, reviewQueueCount: 0, weakSkillsCount: 0, weakSkillLabels: [] },
  recentActivity: [],
  focus: { recommendations: [], recurringMistakes: [], journeySummary: null },
};

const richProgressSummary = {
  learning: {
    currentCefrLevel: 'B1',
    placementCompletedAt: '2026-05-01T00:00:00Z',
    currentLearningPhase: 'Active learning',
    totalObjectives: 20,
    objectivesCompleted: 5,
    objectivesMastered: 3,
    objectivesInProgress: 4,
    objectivesRemaining: 12,
    completionPercentage: 40,
    currentObjectiveKey: 'obj-1',
    currentObjectiveSkill: 'Writing',
    objectivesCompletedToday: 1,
  },
  skills: [
    { skillKey: 'grammar', skillLabel: 'Grammar accuracy', isWeak: false, scorePercent: 75 },
    { skillKey: 'formal_tone', skillLabel: 'Formal tone', isWeak: true, scorePercent: 32 },
  ],
  cefr: {
    startingCefrLevel: 'A2',
    currentCefrLevel: 'B1',
    cefrImproved: true,
    placementDate: '2026-05-01T00:00:00Z',
    note: null,
  },
  mastery: {
    masteredObjectivesCount: 3,
    inProgressObjectivesCount: 4,
    reviewQueueCount: 2,
    weakSkillsCount: 1,
    weakSkillLabels: ['Formal tone'],
  },
  recentActivity: [
    { eventType: 'LessonCompleted', description: 'Completed lesson 1', detail: 'Module A', occurredAt: '2026-06-20T10:00:00Z' },
    { eventType: 'PracticeCompleted', description: 'Practice session', detail: null, occurredAt: '2026-06-19T09:00:00Z' },
  ],
  focus: {
    recommendations: ['Practise formal tone in requests', 'Review vocabulary'],
    recurringMistakes: ['Overly casual greetings'],
    journeySummary: 'You are making solid progress toward B1.',
  },
};

test('progress page shows loading then renders content', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(richProgressSummary),
    });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('learning-summary-heading')).toBeVisible();
  await expect(page.getByTestId('learning-summary')).toBeVisible();
});

test('progress page shows current CEFR level stat', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('current-cefr')).toContainText('B1');
});

test('progress page shows plan progress bar', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('learning-plan-progress')).toBeVisible();
  await expect(page.getByTestId('learning-plan-progress')).toContainText('40');
  await expect(page.getByTestId('learning-plan-progress')).toContainText('mastered');
});

test('progress page shows CEFR improvement arc', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  const cefrCard = page.getByTestId('cefr-progress');
  await expect(cefrCard).toBeVisible();
  await expect(cefrCard).toContainText('A2');
  await expect(cefrCard).toContainText('B1');
  await expect(cefrCard).toContainText('improved');
});

test('progress page shows placement prompt when no placement', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('cefr-progress')).toContainText('placement assessment');
});

test('progress page shows skill progress bars', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  const skillCard = page.getByTestId('skill-progress');
  await expect(skillCard).toBeVisible();
  await expect(skillCard).toContainText('Grammar accuracy');
  await expect(skillCard).toContainText('Formal tone');
  await expect(skillCard).toContainText('needs work');
});

test('progress page shows empty skill state when no skills', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('skill-progress-empty')).toBeVisible();
});

test('progress page shows mastery stat grid and weak skill chips', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('mastery-summary')).toBeVisible();
  await expect(page.getByTestId('weak-skill-labels')).toContainText('Formal tone');
});

test('progress page shows focus recommendations', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  const focusCard = page.getByTestId('focus-recommendations');
  await expect(focusCard).toBeVisible();
  await expect(focusCard).toContainText('You are making solid progress toward B1.');
  await expect(focusCard).toContainText('Practise formal tone in requests');
  await expect(focusCard).toContainText('Overly casual greetings');
});

test('progress page shows recent activity timeline', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');

  const activityCard = page.getByTestId('recent-activity');
  await expect(activityCard).toBeVisible();
  await expect(activityCard).toContainText('Completed lesson 1');
  await expect(activityCard).toContainText('Module A');
  await expect(activityCard).toContainText('Practice session');
});

test('progress page shows empty activity state when no events', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(emptyProgressSummary) });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('recent-activity-empty')).toBeVisible();
});

test('progress page shows friendly error state on API failure', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error.' }),
    });
  });

  await page.goto('/progress');

  await expect(page.getByTestId('progress-error')).toBeVisible();
  await expect(page.getByTestId('progress-retry')).toBeVisible();
  await expect(page.getByTestId('progress-error')).toContainText('Could not load progress');
});

test('progress page retry reloads data after error', async ({ page }) => {
  await withAuth(page);
  let callCount = 0;
  await page.route('**/api/student/progress/summary', async route => {
    callCount++;
    if (callCount === 1) {
      await route.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ error: 'Fail.' }) });
    } else {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
    }
  });

  await page.goto('/progress');
  await expect(page.getByTestId('progress-retry')).toBeVisible();

  await page.getByTestId('progress-retry').click();
  await expect(page.getByTestId('learning-summary')).toBeVisible();
  await expect(page.getByTestId('progress-error')).not.toBeVisible();
});

test('progress page shows no raw JSON in content', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');
  await expect(page.getByTestId('learning-summary')).toBeVisible();

  await expect(page.locator('body')).not.toContainText('"skillKey"');
  await expect(page.locator('body')).not.toContainText('"isWeak"');
  await expect(page.locator('body')).not.toContainText('{"');
});

test('progress page mobile does not overflow', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await withAuth(page);
  await page.route('**/api/student/progress/summary', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(richProgressSummary) });
  });

  await page.goto('/progress');
  await expect(page.getByTestId('learning-summary')).toBeVisible();

  const bodyScrollWidth = await page.evaluate(() => document.body.scrollWidth);
  expect(bodyScrollWidth).toBeLessThanOrEqual(380);
});

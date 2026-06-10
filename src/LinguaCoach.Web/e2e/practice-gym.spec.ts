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

async function mockPracticeRoute(page: Page) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Completed', lifecycleStage: 'ActiveLearning', currentSectionKey: null, currentSectionOrder: 0, totalSections: 6 }),
    });
  });
}

// ── Page identity ──────────────────────────────────────────────────────────────

test('/practice loads Practice Gym page', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByTestId('practice-gym-heading')).toBeVisible();
  await expect(page.getByTestId('practice-gym-heading')).toContainText('Practice Gym');
  await expect(page).toHaveURL('/practice');
});

test('Practice Gym page heading says "Practice Gym"', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();
});

test('/practice does not auto-redirect to /activity on load', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  const navigations: string[] = [];
  page.on('framenavigated', frame => {
    if (frame === page.mainFrame()) navigations.push(frame.url());
  });

  await page.goto('/practice');
  await page.waitForLoadState('networkidle');

  const activityNavigations = navigations.filter(u => u.includes('/activity'));
  expect(activityNavigations, `Should not navigate to /activity automatically, but got: ${activityNavigations.join(', ')}`).toHaveLength(0);
  await expect(page).toHaveURL('/practice');
});

// ── All 8 cards present ────────────────────────────────────────────────────────

test('Practice Gym shows a Vocabulary card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-vocabulary')).toBeVisible();
  await expect(page.getByTestId('practice-card-vocabulary')).toContainText('Vocabulary');
});

test('Practice Gym shows a Listening card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-listening')).toBeVisible();
  await expect(page.getByTestId('practice-card-listening')).toContainText('Listening');
});

test('Practice Gym shows a Writing card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-writing')).toBeVisible();
  await expect(page.getByTestId('practice-card-writing')).toContainText('Writing');
});

test('Practice Gym shows a Speaking card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('speaking-card')).toBeVisible();
  await expect(page.getByTestId('speaking-card')).toContainText('Speaking');
});

test('Practice Gym shows a Workplace Chat card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-workplace-chat')).toBeVisible();
  await expect(page.getByTestId('practice-card-workplace-chat')).toContainText('Workplace Chat');
});

test('Practice Gym shows an Email card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-email')).toBeVisible();
  await expect(page.getByTestId('practice-card-email')).toContainText('Email');
});

test('Practice Gym shows a Gap Fill card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-gap-fill')).toBeVisible();
  await expect(page.getByTestId('practice-card-gap-fill')).toContainText('Gap Fill');
});

test('Practice Gym shows a Phrase Match card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(page.getByTestId('practice-card-phrase-match')).toBeVisible();
  await expect(page.getByTestId('practice-card-phrase-match')).toContainText('Phrase Match');
});

// ── Functional card routing ────────────────────────────────────────────────────

test('Vocabulary card links to /vocabulary', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-vocabulary');
  await expect(card).toHaveAttribute('href', '/vocabulary');
});

test('Listening card links to /activity?type=ListeningComprehension', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-listening');
  await expect(card).toHaveAttribute('href', /type=ListeningComprehension/);
});

test('Writing card links to /activity?type=WritingScenario', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-writing');
  await expect(card).toHaveAttribute('href', /type=WritingScenario/);
});

test('Speaking card links to /activity?type=SpeakingRolePlay', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('speaking-card');
  await expect(card).toHaveAttribute('href', /type=SpeakingRolePlay/);
});

// ── Coming soon cards have no navigable links ──────────────────────────────────

test('Workplace Chat card is Coming soon and has no link', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-workplace-chat');
  await expect(card).toContainText('Coming soon');
  await expect(card.locator('a')).toHaveCount(0);
});

test('Email card is Coming soon and has no link', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-email');
  await expect(card).toContainText('Coming soon');
  await expect(card.locator('a')).toHaveCount(0);
});

test('Gap Fill card is Coming soon and has no link', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-gap-fill');
  await expect(card).toContainText('Coming soon');
  await expect(card.locator('a')).toHaveCount(0);
});

test('Phrase Match card is Coming soon and has no link', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = page.getByTestId('practice-card-phrase-match');
  await expect(card).toContainText('Coming soon');
  await expect(card.locator('a')).toHaveCount(0);
});

// ── Nav integration ────────────────────────────────────────────────────────────

test('Student nav Practice item opens /practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  // Also mock dashboard so Today page loads
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com', careerProfile: 'Engineer', cefrLevel: 'B1',
        message: '', lifecycleStage: 'ActiveLearning',
        learningPath: null, activityStats: { activitiesCompleted: 0, averageScore: null, latestScore: null },
        currentFocus: null, nextRecommendedPractice: null, latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ detail: 'No session' }) });
  });

  await page.goto('/dashboard');
  await page.getByTestId('nav-practice').click();

  await expect(page).toHaveURL('/practice');
  await expect(page.getByTestId('practice-gym-heading')).toBeVisible();
});

test('Vocabulary is not in the top-level student sidebar nav', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com', careerProfile: 'Engineer', cefrLevel: 'B1',
        message: '', lifecycleStage: 'ActiveLearning',
        learningPath: null, activityStats: { activitiesCompleted: 0, averageScore: null, latestScore: null },
        currentFocus: null, nextRecommendedPractice: null, latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ detail: 'No session' }) });
  });

  await page.goto('/dashboard');

  // Vocabulary must not appear as a nav link in the sidebar
  const sidebar = page.locator('.sp-student-sidebar');
  await expect(sidebar.getByRole('link', { name: /^Vocabulary$/i })).toHaveCount(0);
});

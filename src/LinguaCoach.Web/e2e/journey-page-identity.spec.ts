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

const ACTIVE_PATH = {
  pathId: 'path-1',
  title: 'Workplace English — B1',
  modulesCompleted: 0,
  totalModules: 3,
  currentModuleId: 'mod-1',
  currentFocus: null,
  modules: [
    {
      moduleId: 'mod-1',
      title: 'Professional workplace communication',
      description: 'Practice concise project status updates and meeting follow-ups.',
      order: 1,
      completedActivities: 1,
      totalActivities: 3,
      focusSkill: 'Writing',
      difficulty: 'B1',
      reason: 'Builds on your placement strengths.',
      averageScore: 78,
      isReadyToComplete: false,
      isCurrent: true,
      isCompleted: false,
    },
    {
      moduleId: 'mod-2',
      title: 'Listening for detail',
      description: 'Understand fast-paced meeting audio and extract key information.',
      order: 2,
      completedActivities: 0,
      totalActivities: 3,
      focusSkill: 'Listening',
      difficulty: 'B1',
      reason: null,
      averageScore: null,
      isReadyToComplete: false,
      isCurrent: false,
      isCompleted: false,
    },
  ],
};

const MEMORY_WITH_SUMMARY = {
  journeySummary: 'You are improving your professional communication skills.',
  strongSkills: ['Clear openings'],
  weakSkills: ['Softening requests'],
  recurringMistakes: [],
  nextRecommendedFocus: ['Listening for deadlines'],
  coveredScenarioCount: 3,
  skillProfile: [],
};

const EMPTY_MEMORY = {
  journeySummary: null,
  strongSkills: [],
  weakSkills: [],
  recurringMistakes: [],
  nextRecommendedFocus: [],
  coveredScenarioCount: 0,
  skillProfile: [],
};

// Memory with skills but no summary — triggers hasMemory() but shows the journeySummary fallback
const MEMORY_NO_SUMMARY = {
  journeySummary: null,
  strongSkills: ['Clear writing'],
  weakSkills: [],
  recurringMistakes: [],
  nextRecommendedFocus: [],
  coveredScenarioCount: 2,
  skillProfile: [],
};

async function mockJourneyPage(page: Page, opts: { memory?: 'populated' | 'empty' | 'no-summary' } = {}) {
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Completed', lifecycleStage: 'ActiveLearning', currentSectionKey: null, currentSectionOrder: 0, totalSections: 6 }),
    });
  });
  await page.route('**/api/learning-path', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(ACTIVE_PATH) });
  });
  await page.route('**/api/learning-path/memory', async route => {
    const mem = opts.memory === 'empty' ? EMPTY_MEMORY : opts.memory === 'no-summary' ? MEMORY_NO_SUMMARY : MEMORY_WITH_SUMMARY;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mem) });
  });
  await page.route('**/api/learning-path/continue**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ ...ACTIVE_PATH }) });
  });
}

// ── Page identity ──────────────────────────────────────────────────────────────

test('/journey loads the Learning Journey page', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  await expect(page.getByTestId('journey-page-heading')).toBeVisible();
  await expect(page.getByTestId('journey-page-heading')).toContainText('Learning Journey');
  await expect(page).toHaveURL(/\/(journey|my-path)/);
});

test('/my-path still loads the Learning Journey page (route compatibility)', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/my-path');

  await expect(page.getByTestId('journey-page-heading')).toBeVisible();
  await expect(page).toHaveURL(/\/my-path/);
});

// ── No stale writing-first copy ───────────────────────────────────────────────

test('Journey page does not contain "workplace writing" phrase', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page, { memory: 'no-summary' });

  await page.goto('/journey');

  await expect(page.locator('body')).not.toContainText('workplace writing');
});

test('Journey page memory fallback uses "workplace English"', async ({ page }) => {
  await withAuth(page);
  // Use no-summary memory: hasMemory() is true (has skills) but journeySummary is null,
  // so the fallback string renders instead of a real summary
  await mockJourneyPage(page, { memory: 'no-summary' });

  await page.goto('/journey');

  // The fallback text contains "workplace English" — verify via the specific fallback sentence
  await expect(page.getByText(/building a clearer picture of your workplace English/i)).toBeVisible();
});

// ── CTAs do not link to /activity ─────────────────────────────────────────────

test('"Continue today\'s lesson" button links to /dashboard', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  const btn = page.getByTestId('journey-continue-today');
  await expect(btn).toBeVisible();
  await expect(btn).toHaveAttribute('href', '/dashboard');
});

test('"Open Practice Gym" button links to /practice', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  const btn = page.getByTestId('journey-open-practice-gym');
  await expect(btn).toBeVisible();
  await expect(btn).toHaveAttribute('href', '/practice');
});

test('Journey page module action buttons do not link directly to /activity', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  // Collect all links in the module action area and verify none go to bare /activity
  const moduleActionLinks = page.locator('a[href="/activity"]');
  await expect(moduleActionLinks).toHaveCount(0);
});

// ── Mixed-skill framing ───────────────────────────────────────────────────────

test('Journey page shows Learning Journey heading, not My Path', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  await expect(page.getByRole('heading', { name: /Learning Journey/i })).toBeVisible();
  await expect(page.getByRole('heading', { name: /^My Path$/i })).toHaveCount(0);
});

test('Journey page shows a mixed-skill module (Listening) without writing-only framing', async ({ page }) => {
  await withAuth(page);
  await mockJourneyPage(page);

  await page.goto('/journey');

  // The path includes a Listening module — it should be visible
  await expect(page.getByText('Listening for detail')).toBeVisible();
});

import { expect, test, Page } from '@playwright/test';

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

const STUDENT = {
  studentProfileId: 'sp1', userId: '1', email: 'alice@corp.com', firstName: 'Alice', lastName: 'Nguyen',
  displayName: null, onboardingStatus: 'Complete', lifecycleStage: 'CourseReady', cefrLevel: 'B1',
  careerContext: 'Project coordination', learningGoal: 'Clear meeting updates',
  learningGoalDescription: 'Sound confident in stand-ups', difficultSituationsText: 'Pushing back on deadlines',
  preferredSessionDurationMinutes: 30, professionalExperienceLevel: 3,
  roleFamiliarity: 2, createdAt: '2026-01-15T00:00:00Z',
};

const MEMORY = {
  journeySummary: 'Making steady progress on workplace writing.',
  strongSkills: ['Clarity'],
  weakSkills: ['Tone'],
  recurringMistakes: ['Article omission'],
  nextRecommendedFocus: ['Formal email openings'],
  coveredScenarioCount: 4,
  skillProfile: [
    { skillKey: 'grammar_accuracy', skillLabel: 'Grammar accuracy', isWeak: false },
    { skillKey: 'formal_tone', skillLabel: 'Formal tone', isWeak: true },
  ],
};

async function mockAdmin(page: Page) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });
  await page.route('**/api/admin/students/*/learning-memory', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MEMORY) });
  });
  await page.route('**/api/admin/students*', async route => {
    if (route.request().method() !== 'GET') {
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ studentProfileId: 'x', userId: 'y' }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([STUDENT]) });
  });
  await page.route('**/api/admin/**', async route => {
    const url = route.request().url();
    if (url.includes('/api/admin/students')) {
      await route.fallback();
      return;
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
}

async function adminLogin(page: Page) {
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(/\/admin/, { timeout: 10000 });
  await page.waitForTimeout(600);
}

test('admin: student detail page shows profile and learning memory', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);

  await page.getByRole('link', { name: 'Students', exact: true }).click();
  await page.waitForURL(/\/admin\/students/, { timeout: 5000 });
  await page.getByRole('link', { name: 'View' }).first().click();
  await page.waitForURL(/\/admin\/students\/sp1/, { timeout: 5000 });

  await expect(page.getByRole('main')).toContainText('Alice Nguyen');
  await expect(page.getByRole('main')).toContainText('alice@corp.com');
  await expect(page.getByRole('main')).toContainText('CourseReady');
  await expect(page.getByRole('main')).toContainText('Project coordination');

  await expect(page.getByText('Making steady progress on workplace writing.')).toBeVisible();
  await expect(page.getByText('Article omission')).toBeVisible();
  await expect(page.getByText('Formal tone')).toBeVisible();
});

test('admin: student detail back link returns to students list', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);

  await page.goto('/admin/students/sp1');
  await page.waitForTimeout(300);

  await page.getByRole('link', { name: /Back to students/ }).click();
  await page.waitForURL(/\/admin\/students$/, { timeout: 5000 });
});

test('admin: student detail reset data modal works from detail page', async ({ page }) => {
  await mockAdmin(page);
  await page.route('**/api/admin/students/*/reset', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentId: STUDENT.studentProfileId,
        previousStage: 'CourseReady',
        newStage: 'OnboardingRequired',
        clearedItems: {
          onboardingAnswers: true, placementResults: false, coursesAndSessions: false,
          activityAttempts: false, vocabulary: false, learningMemory: false,
          audioFilesDeleted: 0, progressData: false,
        },
        resetLogId: 'reset-log-1',
        performedByAdminId: 'admin-1',
        performedAtUtc: '2026-06-14T10:00:00Z',
        correlationId: 'corr-1',
      }),
    });
  });
  await adminLogin(page);

  await page.goto('/admin/students/sp1');
  await page.waitForTimeout(300);

  await page.getByRole('button', { name: 'Reset data' }).click();
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  await expect(dialog).toBeVisible();

  await dialog.locator('textarea[name="reason"]').fill('QA needs a clean restart');
  await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  await dialog.locator('button[type="submit"]').click();

  await expect(dialog.getByText(/New stage: OnboardingRequired/)).toBeVisible();
});

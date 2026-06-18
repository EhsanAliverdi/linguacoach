import { expect, test, Page } from '@playwright/test';

// ── Shared JWT helpers (mirrors admin-screenshots.spec.ts) ────────────────────

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
  careerContext: 'Project coordination', learningGoal: 'Clear meeting updates', learningGoalDescription: null,
  difficultSituationsText: null, preferredSessionDurationMinutes: 30, professionalExperienceLevel: 3,
  roleFamiliarity: 2, createdAt: '2026-01-15T00:00:00Z',
};

async function mockAdmin(page: Page, resetHandler?: (route: import('@playwright/test').Route) => Promise<void>) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });
  await page.route('**/api/admin/students/*/reset', async route => {
    if (resetHandler) {
      await resetHandler(route);
      return;
    }
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
  await page.route('**/api/admin/students*', async route => {
    if (route.request().method() !== 'GET') {
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ studentProfileId: 'x', userId: 'y' }) });
      return;
    }
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([STUDENT]),
    });
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

async function gotoStudents(page: Page) {
  await page.getByRole('link', { name: 'Students', exact: true }).click();
  await page.waitForURL(/\/admin\/students/, { timeout: 5000 });
  await page.waitForTimeout(300);
}

/**
 * Opens the first row's sp-admin-table-actions dropdown, then clicks the
 * action item matching the given label.
 * Phase 10X-F: row actions moved into dropdown trigger (.sp-adm-actions-trigger).
 * Projected content buttons use text matching (not role=menuitem).
 */
async function clickRowAction(page: Page, label: string) {
  await page.locator('.sp-adm-actions-trigger').first().click();
  await page.locator('[role="menu"] button, [role="menu"] a').filter({ hasText: label }).first().click();
}

test('admin: reset data modal opens with restart-onboarding preset by default', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');

  const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('select[name="preset"]')).toHaveValue(/restartOnboarding/);
  await expect(dialog.locator('input[name="clearOnboardingAnswers"]')).toBeChecked();
  await expect(dialog.locator('input[name="clearPlacementResults"]')).not.toBeChecked();
  await expect(dialog.locator('input[name="clearVocabulary"]')).not.toBeChecked();
});

test('admin: reset data submit disabled until reason and confirm email are filled', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  const submit = dialog.locator('button[type="submit"]');
  await expect(submit).toBeDisabled();

  await dialog.locator('textarea[name="reason"]').fill('QA needs to rerun onboarding');
  await expect(submit).toBeDisabled();

  await dialog.locator('input[name="confirmEmail"]').fill('wrong@corp.com');
  await expect(submit).toBeDisabled();

  await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  await expect(submit).toBeEnabled();
});

test('admin: applying full-clean-reset preset checks all clear flags', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  await dialog.locator('select[name="preset"]').selectOption({ label: 'Full clean reset' });

  for (const flag of [
    'clearOnboardingAnswers', 'clearPlacementResults', 'clearCoursesAndSessions',
    'clearActivityAttempts', 'clearVocabulary', 'clearLearningMemory',
    'clearAudioFiles', 'clearProgressData',
  ]) {
    await expect(dialog.locator(`input[name="${flag}"]`)).toBeChecked();
  }
});

test('admin: successful reset shows new stage, cleared items and reset log id', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  await dialog.locator('textarea[name="reason"]').fill('Stuck mid-onboarding after crash');
  await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  await dialog.locator('button[type="submit"]').click();

  await expect(dialog.getByText(/New stage: OnboardingRequired/)).toBeVisible();
  await expect(dialog.getByText(/was CourseReady/)).toBeVisible();
  await expect(dialog.getByText(/Reset log: reset-log-1/)).toBeVisible();

  await dialog.getByRole('button', { name: 'Done' }).click();
  await expect(dialog).not.toBeVisible();
});

test('admin: reset failure shows error message', async ({ page }) => {
  await mockAdmin(page, async route => {
    await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ error: 'Reason is required.' }) });
  });
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  await dialog.locator('textarea[name="reason"]').fill('Stuck mid-onboarding after crash');
  await dialog.locator('input[name="confirmEmail"]').fill(STUDENT.email);
  await dialog.locator('button[type="submit"]').click();

  await expect(dialog.locator('.sp-admin-alert-error')).toBeVisible();
});

test('admin: reset data modal can be cancelled', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  await expect(dialog).toBeVisible();

  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog).not.toBeVisible();
});

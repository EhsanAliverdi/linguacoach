import { expect, test, Page, Locator } from '@playwright/test';

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
    const method = route.request().method();
    const url = route.request().url();
    if (method !== 'GET') {
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ studentProfileId: 'x', userId: 'y' }) });
      return;
    }
    // Student detail endpoint: /api/admin/students/{id} (not the list)
    if (/\/api\/admin\/students\/[^\/]+$/.test(url) && !url.includes('?')) {
      await route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ ...STUDENT, preferredName: null, supportLanguageCode: null, supportLanguageName: null, difficultyPreference: null, translationHelpPreference: null, focusAreas: null, customFocusArea: null, learningGoals: null, customLearningGoal: null, learningPreferencesUpdatedAt: null, onboardingProgress: null }),
      });
      return;
    }
    // Audit history endpoint
    if (url.includes('/audit-history')) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }
    // List endpoint — return PagedResponse shape
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ items: [STUDENT], totalCount: 1, page: 1, totalPages: 1 }),
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
 * Opens the first row action dropdown, then clicks the
 * action item matching the given label.
 * Projected content buttons use text matching (not role=menuitem).
 */
async function clickRowAction(page: Page, label: string) {
  await page.getByRole('button', { name: 'Row actions' }).first().click();
  await page.locator('[role="menu"] button, [role="menu"] a').filter({ hasText: label }).first().click();
}

function resetReason(dialog: Locator) {
  return dialog.locator('textarea[placeholder="Why is this reset being performed?"]');
}

function resetConfirmEmail(dialog: Locator) {
  return dialog
    .locator('sp-admin-form-field')
    .filter({ hasText: `Type the student email to confirm: ${STUDENT.email}` })
    .locator('input');
}

test('admin: reset data modal opens with restart-onboarding preset by default', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');

  const dialog = page.getByRole('dialog', { name: 'Reset student data' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('sp-admin-native-select[name="preset"] select')).toHaveValue(/restartOnboarding/);
  await expect(dialog.locator('sp-admin-checkbox[name="clearOnboardingAnswers"] input')).toBeChecked();
  await expect(dialog.locator('sp-admin-checkbox[name="clearPlacementResults"] input')).not.toBeChecked();
  await expect(dialog.locator('sp-admin-checkbox[name="clearVocabulary"] input')).not.toBeChecked();
});

test('admin: reset data submit disabled until reason and confirm email are filled', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  const submit = dialog.getByRole('button', { name: /Reset data/i });
  await expect(submit).toBeDisabled();

  await resetReason(dialog).fill('QA needs to rerun onboarding');
  await expect(submit).toBeDisabled();

  await resetConfirmEmail(dialog).fill('wrong@corp.com');
  await expect(submit).toBeDisabled();

  await resetConfirmEmail(dialog).fill(STUDENT.email);
  await expect(submit).toBeEnabled();
});

test('admin: applying full-clean-reset preset checks all clear flags', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  await dialog.locator('sp-admin-native-select[name="preset"] select').selectOption({ label: 'Full clean reset' });

  for (const flag of [
    'clearOnboardingAnswers', 'clearPlacementResults', 'clearCoursesAndSessions',
    'clearActivityAttempts', 'clearVocabulary', 'clearLearningMemory',
    'clearAudioFiles', 'clearProgressData',
  ]) {
    await expect(dialog.locator(`sp-admin-checkbox[name="${flag}"] input`)).toBeChecked();
  }
});

test('admin: successful reset shows new stage, cleared items and reset log id', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await gotoStudents(page);

  await clickRowAction(page, 'Reset data');
  const dialog = page.getByRole('dialog', { name: 'Reset student data' });

  await resetReason(dialog).fill('Stuck mid-onboarding after crash');
  await resetConfirmEmail(dialog).fill(STUDENT.email);
  await dialog.getByRole('button', { name: /Reset data/i }).click();

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

  await resetReason(dialog).fill('Stuck mid-onboarding after crash');
  await resetConfirmEmail(dialog).fill(STUDENT.email);
  await dialog.getByRole('button', { name: /Reset data/i }).click();

  await expect(dialog.getByText('Reason is required.')).toBeVisible();
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

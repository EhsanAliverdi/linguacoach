import { expect, test, type Page } from '@playwright/test';

function fakeStudentJwt(sub = 'student-user-id') {
  const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' }))
    .toString('base64url');
  const payload = Buffer.from(JSON.stringify({
    sub,
    email: 'student@example.com',
    role: 'Student',
    exp: Math.floor(Date.now() / 1000) + 3600,
  })).toString('base64url');
  return `${header}.${payload}.signature`;
}

async function mockExperienceStep(page: Page, experienceResult = { success: true }) {
  await page.route('**/api/onboarding/experience', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(experienceResult),
    });
  });
  // Intercept placement so the test doesn't need a full placement mock.
  await page.route('**/api/placement/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
}

async function withAuth(page: Page) {
  const token = fakeStudentJwt();
  await page.addInitScript((session) => {
    sessionStorage.setItem('speakpath.auth', session);
  }, JSON.stringify({ token, mustChangePassword: false }));
}

async function goToStep5(page: Page) {
  await withAuth(page);
  await page.goto('/onboarding/step-5');
}

test.describe('Onboarding step 5 — experience', () => {
  test('Step 5 page is reachable and shows experience heading', async ({ page }) => {
    await mockExperienceStep(page);
    await goToStep5(page);

    await expect(page.getByRole('heading', { name: /Tell us about your work experience/i })).toBeVisible();
  });

  test('Step 5 shows eyebrow "Step 5 of 5"', async ({ page }) => {
    await mockExperienceStep(page);
    await goToStep5(page);

    await expect(page.getByText('Step 5 of 5')).toBeVisible();
  });

  test('Step 5 shows experience level options', async ({ page }) => {
    await mockExperienceStep(page);
    await goToStep5(page);

    await expect(page.getByRole('button', { name: /Junior/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Mid-level/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Senior/i })).toBeVisible();
  });

  test('Step 5 shows familiarity level options', async ({ page }) => {
    await mockExperienceStep(page);
    await goToStep5(page);

    await expect(page.getByRole('button', { name: /Currently working in this role/i })).toBeVisible();
  });

  test('Clicking "Continue to assessment" calls PATCH /onboarding/experience and navigates to placement', async ({ page }) => {
    let calledExperience = false;

    await page.route('**/api/onboarding/experience', async route => {
      calledExperience = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true }),
      });
    });

    await page.route('**/api/placement/**', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await goToStep5(page);

    await page.getByRole('button', { name: 'Continue to assessment' }).click();
    await expect(page).toHaveURL(/\/placement/);
    expect(calledExperience).toBe(true);
  });

  test('"Skip for now" navigates to placement without calling experience endpoint', async ({ page }) => {
    let calledExperience = false;

    await page.route('**/api/onboarding/experience', async route => {
      calledExperience = true;
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await page.route('**/api/placement/**', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await goToStep5(page);

    await page.getByRole('button', { name: 'Skip for now' }).click();
    await expect(page).toHaveURL(/\/placement/);
    expect(calledExperience).toBe(false);
  });

  test('If experience API fails, still navigates to placement', async ({ page }) => {
    await page.route('**/api/onboarding/experience', async route => {
      await route.fulfill({ status: 500, contentType: 'application/json', body: '{"error":"server error"}' });
    });

    await page.route('**/api/placement/**', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    await goToStep5(page);

    await page.getByRole('button', { name: 'Continue to assessment' }).click();
    await expect(page).toHaveURL(/\/placement/);
  });

  test('Step 4 now shows "Step 4 of 5"', async ({ page }) => {
    await page.route('**/api/**', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });
    await withAuth(page);
    await page.goto('/onboarding/step-4');

    await expect(page.getByText('Step 4 of 5')).toBeVisible();
  });
});

import { test } from '@playwright/test';

async function mockAdminApi(page: any) {
  await page.route('**/api/auth/login', async (route: any) => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbi11c2VyLWlkIiwiZW1haWwiOiJhZG1pbkBleGFtcGxlLmNvbSIsInJvbGUiOiJBZG1pbiIsIm11c3RDaGFuZ2VQYXNzd29yZCI6ImZhbHNlIiwiZXhwIjo5OTk5OTk5OTk5fQ.sig',
        role: 'Admin',
        mustChangePassword: false,
      }),
    });
  });
  await page.route('**/api/admin/students', async (route: any) => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([
        { userId: '1', email: 'alice@corp.com', onboardingStatus: 'Complete', cefrLevel: 'B1', createdAt: '2026-01-15T00:00:00Z' },
        { userId: '2', email: 'bob@corp.com', onboardingStatus: 'Pending', cefrLevel: null, createdAt: '2026-05-20T00:00:00Z' },
        { userId: '3', email: 'carol@corp.com', onboardingStatus: 'Complete', cefrLevel: 'A2', createdAt: '2026-04-10T00:00:00Z' },
      ]),
    });
  });
}

test('admin dashboard screenshot', async ({ page }) => {
  await mockAdminApi(page);
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(/\/admin/, { timeout: 10000 });
  await page.waitForTimeout(800);
  await page.screenshot({ path: 'e2e/screenshots/admin-dash.png', fullPage: false });
});

test('admin create student screenshot', async ({ page }) => {
  await mockAdminApi(page);
  await page.goto('/login');
  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(/\/admin/, { timeout: 10000 });
  await page.getByRole('link', { name: /Create student/i }).click();
  await page.waitForURL(/create-student/, { timeout: 5000 });
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/admin-create.png', fullPage: false });
});

/**
 * Production admin screenshot capture.
 * Runs against https://speakpath.app — no mocks, real login.
 *
 * Usage (from src/LinguaCoach.Web):
 *   npx playwright test e2e/prod-admin-screenshots.spec.ts --config e2e/prod.playwright.config.ts
 *
 * Screenshots land in: e2e/screenshots/prod/
 */
import { test, Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

const BASE = 'https://speakpath.app';
const EMAIL = 'ehsan.aliverdi@gmail.com';
const PASSWORD = 'Eli$@212252';
const OUT = path.join(__dirname, 'screenshots', 'prod');

function shot(name: string) {
  return path.join(OUT, `${name}.png`);
}

async function login(page: Page) {
  await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
  await page.getByLabel(/email/i).fill(EMAIL);
  await page.getByLabel(/password/i).fill(PASSWORD);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL(/\/admin/, { timeout: 15000 });
  await page.waitForTimeout(1000);
}

async function nav(page: Page, route: string, waitFor?: string) {
  await page.goto(`${BASE}${route}`, { waitUntil: 'networkidle', timeout: 20000 });
  if (waitFor) await page.waitForSelector(waitFor, { timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(800);
}

test.beforeAll(() => {
  fs.mkdirSync(OUT, { recursive: true });
});

test.use({ storageState: undefined });

test('prod: capture all admin pages', async ({ page }) => {
  await login(page);

  // 1 — Dashboard
  await nav(page, '/admin');
  await page.screenshot({ path: shot('01-dashboard'), fullPage: true });

  // 2 — Students
  await nav(page, '/admin/students', 'table');
  await page.screenshot({ path: shot('02-students'), fullPage: true });

  // 3 — Create Student
  await nav(page, '/admin/create-student');
  await page.screenshot({ path: shot('03-create-student'), fullPage: true });

  // 4 — AI Config
  await nav(page, '/admin/ai-config');
  await page.screenshot({ path: shot('04-ai-config'), fullPage: true });

  // 5 — Prompts
  await nav(page, '/admin/prompts');
  await page.screenshot({ path: shot('05-prompts'), fullPage: true });

  // 6 — AI Usage (detail)
  await nav(page, '/admin/usage');
  await page.screenshot({ path: shot('06-ai-usage'), fullPage: true });

  // 7 — Usage Policies
  await nav(page, '/admin/usage-policies');
  await page.screenshot({ path: shot('07-usage-policies'), fullPage: true });

  // 8 — Usage & Analytics
  await nav(page, '/admin/usage-analytics');
  await page.screenshot({ path: shot('08-usage-analytics'), fullPage: true });

  // 9 — Lessons
  await nav(page, '/admin/lessons');
  await page.screenshot({ path: shot('09-lessons'), fullPage: true });

  // 10 — Curriculum
  await nav(page, '/admin/curriculum');
  await page.screenshot({ path: shot('10-curriculum'), fullPage: true });

  // 11 — Exercise Types
  await nav(page, '/admin/exercise-types');
  await page.screenshot({ path: shot('11-exercise-types'), fullPage: true });

  // 12 — Notifications
  await nav(page, '/admin/notifications');
  await page.screenshot({ path: shot('12-notifications'), fullPage: true });

  // 13 — Integrations
  await nav(page, '/admin/integrations');
  await page.screenshot({ path: shot('13-integrations'), fullPage: true });

  // 14 — Diagnostics
  await nav(page, '/admin/diagnostics');
  await page.screenshot({ path: shot('14-diagnostics'), fullPage: true });

  // 15 — Security
  await nav(page, '/admin/security');
  await page.screenshot({ path: shot('15-security'), fullPage: true });
});

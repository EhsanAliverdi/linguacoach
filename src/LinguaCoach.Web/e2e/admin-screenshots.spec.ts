import { test, Page } from '@playwright/test';

// ── Shared JWT helpers ────────────────────────────────────────────────────────

function fakeJwt(email: string, role: string): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  const payload = btoa(JSON.stringify({ sub: 'uid-1', email, role, exp: 9999999999 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${header}.${payload}.sig`;
}

async function mockAdmin(page: Page) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('admin@example.com', 'Admin'), role: 'Admin', mustChangePassword: false }),
    });
  });
  // Specific students route must be registered before any catch-all
  await page.route('**/api/admin/students', async route => {
    if (route.request().method() !== 'GET') {
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ studentProfileId: 'x', userId: 'y' }) });
      return;
    }
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify([
        { userId: '1', email: 'alice@corp.com', onboardingStatus: 'Complete', cefrLevel: 'B1', createdAt: '2026-01-15T00:00:00Z' },
        { userId: '2', email: 'bob@corp.com',   onboardingStatus: 'Pending',  cefrLevel: null, createdAt: '2026-05-20T00:00:00Z' },
        { userId: '3', email: 'carol@corp.com', onboardingStatus: 'Complete', cefrLevel: 'A2', createdAt: '2026-04-10T00:00:00Z' },
      ]),
    });
  });
  // AI Config routes
  await page.route('**/api/admin/ai-config/catalog', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([
      { providerName: 'OpenAI', models: ['gpt-4o', 'gpt-4o-mini'], isConfigured: true },
      { providerName: 'Anthropic', models: ['claude-sonnet-4-6', 'claude-haiku-4-5'], isConfigured: false },
    ]) });
  });
  await page.route('**/api/admin/ai-config/features', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([
      { id: '1', featureKey: 'writing.activity', providerName: 'OpenAI', modelName: 'gpt-4o' },
      { id: '2', featureKey: 'writing.feedback', providerName: 'OpenAI', modelName: 'gpt-4o' },
    ]) });
  });
  // Generic fallback for other admin routes
  await page.route('**/api/admin/**', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
  });
}

async function mockStudent(page: Page) {
  await page.route('**/api/auth/login', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: fakeJwt('student@example.com', 'Student'), role: 'Student', mustChangePassword: false }),
    });
  });
  await page.route('**/api/onboarding/status', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isComplete: true }) });
  });
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        learningPath: {
          pathId: 'p1', title: 'Workplace English for Document Controller — B1',
          totalModules: 5, modulesCompleted: 1,
          currentModule: { moduleId: 'm1', order: 2, title: 'Writing professional emails', description: 'Learn to write clear emails.', completedActivities: 1, totalActivities: 4 },
          modules: [],
        },
        recentActivities: [],
      }),
    });
  });
  await page.route('**/api/learning-path', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        pathId: 'p1', title: 'Workplace English for Document Controller — B1',
        totalModules: 5, modulesCompleted: 1,
        currentModule: { moduleId: 'm1', order: 2, title: 'Writing professional emails', description: 'Learn emails.', completedActivities: 1, totalActivities: 4 },
        modules: [
          { moduleId: 'm0', order: 1, title: 'Getting started', description: 'Introduction.', completedActivities: 3, totalActivities: 3 },
          { moduleId: 'm1', order: 2, title: 'Writing professional emails', description: 'Clear email writing.', completedActivities: 1, totalActivities: 4 },
          { moduleId: 'm2', order: 3, title: 'Meeting vocabulary', description: 'Workplace meetings.', completedActivities: 0, totalActivities: 5 },
        ],
      }),
    });
  });
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        activityId: 'act-1', activityType: 'writingScenario', source: 'aiGenerated',
        title: 'Follow-up email to a colleague', difficulty: 'B1',
        situation: 'Your colleague asked for an update on the project status.',
        targetPhrases: ['I wanted to follow up', 'Please let me know if you need anything'],
        targetVocabulary: [], exampleText: null, commonMistakeToAvoid: 'Avoid being too informal.',
        instructionInSourceLanguage: 'یک ایمیل حرفه‌ای به همکار خود بنویسید.',
        learningGoal: null,
      }),
    });
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

async function studentLogin(page: Page) {
  await page.goto('/login');
  await page.getByLabel('Email').fill('student@example.com');
  await page.getByLabel('Password').fill('Student@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.waitForURL(/\/dashboard/, { timeout: 10000 });
  await page.waitForTimeout(600);
}

// ── Admin page tests ──────────────────────────────────────────────────────────

test('admin: dashboard', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.screenshot({ path: 'e2e/screenshots/admin-01-dashboard.png' });
});

test('admin: students', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.getByRole('link', { name: 'Students', exact: true }).click();
  await page.waitForURL(/\/admin\/students/, { timeout: 5000 });
  await page.waitForTimeout(500);
  await page.screenshot({ path: 'e2e/screenshots/admin-02-students.png' });
});

test('admin: create-student', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.getByRole('link', { name: /Create student/i }).click();
  await page.waitForURL(/create-student/, { timeout: 5000 });
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/admin-03-create-student.png' });
});

test('admin: ai-config', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);
  await page.getByRole('link', { name: 'AI Config', exact: true }).click();
  await page.waitForURL(/ai-config/, { timeout: 5000 });
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'e2e/screenshots/admin-04-ai-config.png' });
});

// ── Student page tests ────────────────────────────────────────────────────────

test('login page', async ({ page }) => {
  await page.goto('/login');
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'e2e/screenshots/student-00-login.png' });
});

test('student: dashboard', async ({ page }) => {
  await mockStudent(page);
  await studentLogin(page);
  await page.screenshot({ path: 'e2e/screenshots/student-01-dashboard.png' });
});

test('student: activity (lesson state)', async ({ page }) => {
  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/activity');
  await page.waitForTimeout(1200);
  await page.screenshot({ path: 'e2e/screenshots/student-02-activity.png' });
});

test('student: my-path', async ({ page }) => {
  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/my-path');
  await page.waitForTimeout(600);
  await page.screenshot({ path: 'e2e/screenshots/student-03-my-path.png' });
});

test('student: progress', async ({ page }) => {
  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/progress');
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/student-04-progress.png' });
});

test('student: profile', async ({ page }) => {
  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/profile');
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/student-05-profile.png' });
});

test('admin: sidebar collapsed state', async ({ page }) => {
  await mockAdmin(page);
  // Pre-set collapsed state
  await page.addInitScript(() => {
    localStorage.setItem('speakpath.adminSidebarCollapsed', 'true');
  });
  await adminLogin(page);
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/admin-collapsed.png' });
});

test('student: sidebar collapsed state', async ({ page }) => {
  await mockStudent(page);
  await page.addInitScript(() => {
    localStorage.setItem('speakpath.sidebarCollapsed', 'true');
  });
  await studentLogin(page);
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/student-collapsed.png' });
});

test('landing page', async ({ page }) => {
  await page.goto('/');
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/landing-full.png', fullPage: true });
});

test('student: profile mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/profile');
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/mobile-profile.png', fullPage: false });
});

test('admin: mobile hamburger', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockAdmin(page);
  await adminLogin(page);
  await page.waitForTimeout(400);
  await page.screenshot({ path: 'e2e/screenshots/mobile-admin.png', fullPage: false });
  // Open drawer
  await page.getByRole('button', { name: /Open navigation/i }).click();
  await page.waitForTimeout(300);
  await page.screenshot({ path: 'e2e/screenshots/mobile-admin-drawer.png', fullPage: false });
});

test('admin: diagnostics page loads with status section', async ({ page }) => {
  await mockAdmin(page);
  // Mock diagnostics endpoints
  await page.route('**/api/admin/diagnostics/status', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        environment: 'Testing',
        version: '1.0.0',
        serverTimeUtc: new Date().toISOString(),
        uptimeSeconds: 120,
        logLevel: 'Information',
        diagnosticEventsEnabled: true,
        diagnosticEventCount: 42,
        database: { reachable: true },
        ai: { providerConfigured: true, activeProvider: 'OpenAI', activeModel: 'gpt-4o-mini' },
      }),
    });
  });
  await page.route('**/api/admin/diagnostics/events', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        enabled: true,
        total: 2,
        items: [
          { timestampUtc: new Date().toISOString(), level: 'Information', category: 'Activity.ActivityGetHandler', message: 'Next activity requested', correlationId: 'abc123', userId: null, path: '/api/activity/next', statusCode: null, elapsedMs: null },
          { timestampUtc: new Date().toISOString(), level: 'Warning', category: 'Activity.ActivityGetHandler', message: 'AI generation failed — using SystemFallback', correlationId: 'abc123', userId: null, path: '/api/activity/next', statusCode: null, elapsedMs: null },
        ],
      }),
    });
  });

  await adminLogin(page);
  await page.goto('/admin/diagnostics');
  await page.waitForTimeout(600);

  // Status section should be visible
  await page.waitForSelector('text=System status', { timeout: 5000 });
  await page.waitForSelector('text=Environment', { timeout: 3000 });
  await page.waitForSelector('text=Reachable', { timeout: 3000 });

  // Events section should be visible
  await page.waitForSelector('text=Recent events', { timeout: 3000 });

  await page.screenshot({ path: 'e2e/screenshots/admin-diagnostics.png', fullPage: true });
});

test('admin: diagnostics sidebar nav item present', async ({ page }) => {
  await mockAdmin(page);
  await adminLogin(page);

  // Diagnostics link should be in the DOM (may be collapsed/hidden in rail mode)
  await page.waitForSelector('[routerlink="/admin/diagnostics"]', { state: 'attached', timeout: 5000 });
});

test('student dashboard: no unexpected console errors', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      const text = msg.text();
      // Ignore known harmless errors (e.g. favicon 404, extension errors)
      if (!text.includes('favicon') && !text.includes('chrome-extension')) {
        consoleErrors.push(text);
      }
    }
  });

  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/dashboard');
  await page.waitForTimeout(800);

  if (consoleErrors.length > 0) {
    throw new Error(`Unexpected console errors on dashboard:\n${consoleErrors.join('\n')}`);
  }
});

test('activity page: no unexpected console errors', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      const text = msg.text();
      if (!text.includes('favicon') && !text.includes('chrome-extension')) {
        consoleErrors.push(text);
      }
    }
  });

  await mockStudent(page);
  await studentLogin(page);
  await page.goto('/activity');
  await page.waitForTimeout(800);

  if (consoleErrors.length > 0) {
    throw new Error(`Unexpected console errors on activity page:\n${consoleErrors.join('\n')}`);
  }
});

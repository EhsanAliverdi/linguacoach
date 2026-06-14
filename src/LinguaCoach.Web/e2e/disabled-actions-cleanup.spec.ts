import { expect, test, type Page } from '@playwright/test';

function fakeJwt(email: string, role: 'Student' | 'Admin' = 'Student') {
  const header = toBase64Url({ alg: 'none', typ: 'JWT' });
  const payload = toBase64Url({
    sub: role === 'Admin' ? 'admin-user-id' : 'student-user-id',
    email,
    role,
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

async function withAuth(page: Page, role: 'Student' | 'Admin' = 'Student') {
  const token = fakeJwt(role === 'Admin' ? 'admin@test.com' : 'student@test.com', role);
  await page.addInitScript((s) => sessionStorage.setItem('speakpath.auth', s), JSON.stringify({ token, mustChangePassword: false }));
}

async function mockDashboard(page: Page) {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        learningPath: {
          pathId: 'path-1',
          title: 'Workplace English for Document Controller - B1',
          modulesCompleted: 0,
          totalModules: 1,
          currentModule: {
            moduleId: 'module-1',
            title: 'Professional workplace communication',
            description: 'Practice clear workplace communication.',
            order: 1,
            completedActivities: 1,
            totalActivities: 3,
          },
        },
        lifecycleStage: 'CourseReady',
        activityStats: { activitiesCompleted: 3, averageScore: 82, latestScore: 88 },
        nextRecommendedPractice: 'Practise a workplace update next.',
      }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: 'You are building confidence with workplace messages.',
        strongSkills: ['Clear context'],
        weakSkills: ['Softening requests'],
        recurringMistakes: [],
        nextRecommendedFocus: ['Listening for deadlines'],
        coveredScenarioCount: 2,
        skillProfile: [],
      }),
    });
  });
  await page.route('**/api/vocabulary**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], summary: { total: 0, newCount: 0, practisingCount: 0, masteredCount: 0, archivedCount: 0 } }),
    });
  });
}

test('practice gym marks future skills as coming soon', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/exercise-types', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { key: 'listen_and_answer', displayName: 'Listen and Answer', description: '', primarySkill: 'listening', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_listen_and_answer', legacyActivityType: 'ListeningComprehension', exercisePatternKey: 'listen_and_answer', estimatedDurationMinutes: 4, requiresAudio: true, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
      ]),
    });
  });

  await page.goto('/practice');

  await expect(page.getByTestId('practice-card-ai-role-play')).toContainText('Coming soon');
  await expect(page.getByTestId('practice-card-listening')).not.toContainText('Coming soon');
});

test('practice gym vocabulary card links to the word cards module', async ({ page }) => {
  await withAuth(page);

  await page.goto('/practice');

  await expect(page.getByTestId('practice-card-vocabulary')).toHaveAttribute('href', '/module/gym-phrase_match');
});

test('dashboard vocabulary card shows prerequisite message when vocab items insufficient', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);
  await page.route('**/api/activity/next**', async route => {
    expect(route.request().url()).toContain('type=VocabularyPractice');
    await route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({
        error: 'Vocabulary practice unlocks after you save at least 3 vocabulary items from writing activities. Complete more writing activities to build your vocabulary bank.',
      }),
    });
  });

  await page.goto('/activity?type=VocabularyPractice');

  await expect(page.getByText(/Vocabulary practice unlocks/i)).toBeVisible();
  // Must not silently load WritingScenario
  await expect(page.locator('textarea#draft')).toHaveCount(0);
});

test('admin ai usage page loads usage data', async ({ page }) => {
  await withAuth(page, 'Admin');
  await page.route('**/api/admin/ai-usage/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalCalls: 7,
        successfulCalls: 6,
        failedCalls: 1,
        fallbackCalls: 2,
        totalCostUsd: 0.0123,
        successRate: 86,
        byProvider: [{ provider: 'qwen', calls: 7, successful: 6, fallback: 2, costUsd: 0.0123 }],
        byFeature: [{ feature: 'activity_evaluate_writing', calls: 7, successful: 6, costUsd: 0.0123 }],
      }),
    });
  });
  await page.route('**/api/admin/ai-usage/recent**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        total: 1,
        items: [{
          id: 'usage-1',
          createdAt: '2026-06-07T10:00:00Z',
          studentProfileId: 'student-1',
          featureKey: 'activity_evaluate_writing',
          provider: 'qwen',
          model: 'qwen-plus',
          isFallback: true,
          wasSuccessful: true,
          failureReason: null,
          inputTokens: 120,
          outputTokens: 80,
          costUsd: 0.0123,
          durationMs: 830,
          correlationId: 'corr-123',
        }],
      }),
    });
  });

  await page.goto('/admin/usage');

  await expect(page.getByRole('heading', { name: 'AI Usage' })).toBeVisible();
  await expect(page.getByText('Total calls')).toBeVisible();
  await expect(page.getByText('7').first()).toBeVisible();
  await expect(page.getByText('Activity Evaluate Writing').first()).toBeVisible();
  await expect(page.getByText('corr-123')).toBeVisible();
});

test('student cannot access admin-only routes', async ({ page }) => {
  await withAuth(page);
  await mockDashboard(page);

  await page.goto('/admin/usage');

  await expect(page).toHaveURL(/\/dashboard/);
  await expect(page.getByText('You do not have permission to use the admin area.')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'AI Usage' })).toHaveCount(0);
});

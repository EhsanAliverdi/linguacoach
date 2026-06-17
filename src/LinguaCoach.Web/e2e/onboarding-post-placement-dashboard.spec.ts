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

async function mockCourseReadyDashboard(page: Page) {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'student@test.com',
        careerProfile: 'Junior Software Engineer',
        cefrLevel: 'B1',
        message: 'You are on module 1 of 2.',
        lifecycleStage: 'CourseReady',
        learningPath: {
          pathId: 'path-1',
          title: 'Workplace English for Junior Software Engineer - B1',
          modulesCompleted: 0,
          totalModules: 2,
          currentModule: {
            moduleId: 'module-1',
            title: 'Clear engineering updates',
            description: 'Practice concise project status updates.',
            order: 1,
            completedActivities: 0,
            totalActivities: 3,
          },
        },
        activityStats: { activitiesCompleted: 0, averageScore: null, latestScore: null },
        currentFocus: null,
        nextRecommendedPractice: null,
        latestImprovement: null,
      }),
    });
  });

  await page.route('**/api/placement/result', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        estimatedOverallLevel: 'B1',
        skillLevels: [
          { skill: 'Writing', level: 'B1' },
          { skill: 'Listening', level: 'A2+' },
        ],
        strengths: ['clear technical vocabulary'],
        weaknesses: ['listening for deadlines'],
        recommendedStartingCourse: 'Workplace English B1',
        recommendedSessionDuration: 15,
        placementNotes: 'Start with clear updates and meeting comprehension.',
        isCompleted: true,
      }),
    });
  });

  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: null,
        strongSkills: [],
        weakSkills: [],
        recurringMistakes: [],
        nextRecommendedFocus: [],
        coveredScenarioCount: 0,
        skillProfile: [],
      }),
    });
  });
}

test('course-ready Today page shows placement summary and a secondary Practice link', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', msg => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });

  await withAuth(page);
  await mockCourseReadyDashboard(page);

  await page.goto('/dashboard');

  await expect(page.getByTestId('dashboard-course-ready')).toBeVisible();
  await expect(page.getByTestId('dashboard-starting-level')).toHaveText('B1');
  await expect(page.getByTestId('dashboard-course-ready').getByText('Listening')).toBeVisible();
  await expect(page.getByText('Workplace English B1')).toBeVisible();
  await expect(page.getByText('Guided lessons are coming next')).toBeVisible();
  // Today page has a secondary Practice Gym link, not the full card grid
  await expect(page.getByTestId('today-practice-link')).toBeVisible();
  expect(consoleErrors).toEqual([]);
});

test('Practice Gym page has skill cards for implemented skills and pronunciation is disabled', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ status: 'Completed', lifecycleStage: 'ActiveLearning', currentSectionKey: null, currentSectionOrder: 0, totalSections: 6 }),
    });
  });
  await page.route('**/api/activity/exercise-types', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { key: 'open_writing_task', displayName: 'Open Writing Task', description: '', primarySkill: 'writing', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'free_text_entry', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_open_writing_task', legacyActivityType: 'WritingScenario', exercisePatternKey: 'open_writing_task', estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
        { key: 'listen_and_answer', displayName: 'Listen and Answer', description: '', primarySkill: 'listening', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_listen_and_answer', legacyActivityType: 'ListeningComprehension', exercisePatternKey: 'listen_and_answer', estimatedDurationMinutes: 4, requiresAudio: true, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
        { key: 'speaking_roleplay_turn', displayName: 'Speaking Roleplay Turn', description: '', primarySkill: 'speaking', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'audio_response', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_speaking_roleplay_turn', legacyActivityType: 'SpeakingRolePlay', exercisePatternKey: 'speaking_roleplay_turn', estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
        { key: 'ai_role_play', displayName: 'AI Role Play', description: '', primarySkill: 'speaking', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'planned', isAvailableForGeneration: false, rendererKey: 'audio_response', evaluatorKey: 'ai_open_ended', generationPromptKey: null, legacyActivityType: 'SpeakingRolePlay', exercisePatternKey: null, estimatedDurationMinutes: 8, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false },
      ]),
    });
  });

  await page.goto('/practice');

  await expect(page.getByTestId('practice-format-open_writing_task')).not.toContainText('Coming soon');
  await expect(page.getByTestId('practice-format-listen_and_answer')).not.toContainText('Coming soon');
  await expect(page.getByTestId('practice-format-speaking_roleplay_turn')).not.toContainText('Coming soon');

  const aiRolePlay = page.getByTestId('practice-format-ai_role_play');
  await expect(aiRolePlay).toContainText('Coming soon');
  await expect(aiRolePlay.locator('a')).toHaveCount(0);
});

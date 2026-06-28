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

const practiceExerciseTypes = [
  { key: 'listen_and_answer', displayName: 'Listen and Answer', description: '', primarySkill: 'listening', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_listen_and_answer', legacyActivityType: 'ListeningComprehension', exercisePatternKey: 'listen_and_answer', estimatedDurationMinutes: 4, requiresAudio: true, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'open_writing_task', displayName: 'Open Writing Task', description: '', primarySkill: 'writing', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'free_text_entry', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_open_writing_task', legacyActivityType: 'WritingScenario', exercisePatternKey: 'open_writing_task', estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'speaking_roleplay_turn', displayName: 'Speaking Roleplay Turn', description: '', primarySkill: 'speaking', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'audio_response', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_speaking_roleplay_turn', legacyActivityType: 'SpeakingRolePlay', exercisePatternKey: 'speaking_roleplay_turn', estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'phrase_match', displayName: 'Phrase Match', description: '', primarySkill: 'vocabulary', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'matching_pairs', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_phrase_match', legacyActivityType: 'VocabularyPractice', exercisePatternKey: 'phrase_match', estimatedDurationMinutes: 3, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'gap_fill_workplace_phrase', displayName: 'Gap Fill Workplace Phrase', description: '', primarySkill: 'vocabulary', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'gap_fill', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_gap_fill_workplace_phrase', legacyActivityType: 'VocabularyPractice', exercisePatternKey: 'gap_fill_workplace_phrase', estimatedDurationMinutes: 4, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'email_reply', displayName: 'Email Reply', description: '', primarySkill: 'writing', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'email_reply', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_email_reply', legacyActivityType: 'WritingScenario', exercisePatternKey: 'email_reply', estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'teams_chat_simulation', displayName: 'Teams Chat Simulation', description: '', primarySkill: 'writing', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true, rendererKey: 'chat_reply', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_teams_chat_simulation', legacyActivityType: 'WritingScenario', exercisePatternKey: 'teams_chat_simulation', estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true },
  { key: 'ai_role_play', displayName: 'AI Role Play', description: '', primarySkill: 'speaking', secondarySkills: [], category: 'Pattern', isEnabled: true, implementationStatus: 'planned', isAvailableForGeneration: false, rendererKey: 'audio_response', evaluatorKey: 'ai_open_ended', generationPromptKey: null, legacyActivityType: 'SpeakingRolePlay', exercisePatternKey: null, estimatedDurationMinutes: 8, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false },
];

function formatCard(page: Page, key: string) {
  return page.getByTestId(`practice-format-${key}`);
}

async function mockPracticeRoute(page: Page) {
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
      body: JSON.stringify(practiceExerciseTypes),
    });
  });

  await page.route('**/api/activity/practice-gym/next?**', async route => {
    const url = new URL(route.request().url());
    const skill = url.searchParams.get('skill');
    const requested = url.searchParams.get('exerciseType');
    const key = requested ?? (skill === 'listening' ? 'listen_and_answer' : skill === 'writing' ? 'open_writing_task' : skill === 'speaking' ? 'speaking_roleplay_turn' : skill === 'vocabulary' ? 'phrase_match' : null);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(key
        ? { hasActivity: true, activityId: `activity-${key}`, exerciseType: key, primarySkill: skill, source: 'pool', poolItemId: 'pool-1', reason: null }
        : { hasActivity: false, activityId: null, exerciseType: null, primarySkill: null, source: null, poolItemId: null, reason: 'No ready Practice Gym exercise is available.' }),
    });
  });

  await page.route('**/api/practice-gym/suggestions', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        suggestedItems: [
          {
            readinessItemId: 'item-1',
            title: 'Listen and Answer',
            description: 'Practice listening comprehension',
            primarySkill: 'listening',
            secondarySkills: [],
            patternKey: 'listen_and_answer',
            activityType: null,
            targetCefrLevel: 'B2',
            studentCefrLevelSnapshot: 'B2',
            curriculumObjectiveKey: null,
            curriculumObjectiveTitle: null,
            contextTags: ['general_english'],
            focusTags: [],
            routingReason: 'Normal',
            isLowerLevelContent: false,
            difficultyBand: 2,
            estimatedDurationMinutes: 5,
            supportLanguageName: null,
            status: 'ready',
            callToAction: 'Start practice',
            explanation: 'Listening is your weakest skill',
            linkedLearningActivityId: null,
            linkedLearningSessionId: null,
            linkedSessionExerciseId: null,
          },
        ],
        continueItems: [],
        reviewItems: [],
      }),
    });
  });

}

// ── Page identity ──────────────────────────────────────────────────────────────

test('/practice loads Practice Gym page', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByTestId('practice-gym-heading')).toBeVisible();
  await expect(page.getByTestId('practice-gym-heading')).toContainText('Practice Gym');
  await expect(page).toHaveURL('/practice');
});

test('Practice Gym page heading says "Practice Gym"', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByRole('heading', { name: /Practice Gym/i })).toBeVisible();
});

test('/practice does not auto-redirect to /activity on load', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  const navigations: string[] = [];
  page.on('framenavigated', frame => {
    if (frame === page.mainFrame()) navigations.push(frame.url());
  });

  await page.goto('/practice');
  await page.waitForLoadState('networkidle');

  const activityNavigations = navigations.filter(u => u.includes('/activity'));
  expect(activityNavigations, `Should not navigate to /activity automatically, but got: ${activityNavigations.join(', ')}`).toHaveLength(0);
  await expect(page).toHaveURL('/practice');
});

// ── All 8 cards present ────────────────────────────────────────────────────────

test('Practice Gym shows a Vocabulary format card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'phrase_match')).toBeVisible();
  await expect(formatCard(page, 'phrase_match')).toContainText('Phrase Match');
});

test('Practice Gym shows a Listening card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'listen_and_answer')).toBeVisible();
  await expect(formatCard(page, 'listen_and_answer')).toContainText('Listen and Answer');
});

test('Practice Gym shows a Writing card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'open_writing_task')).toBeVisible();
  await expect(formatCard(page, 'open_writing_task')).toContainText('Open Writing Task');
});

test('Practice Gym shows a Speaking card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'speaking_roleplay_turn')).toBeVisible();
  await expect(formatCard(page, 'speaking_roleplay_turn')).toContainText('Speaking Roleplay Turn');
});

test('Practice Gym shows a Workplace Chat card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'teams_chat_simulation')).toBeVisible();
  await expect(formatCard(page, 'teams_chat_simulation')).toContainText('Teams Chat Simulation');
});

test('Practice Gym shows an Email card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'email_reply')).toBeVisible();
  await expect(formatCard(page, 'email_reply')).toContainText('Email Reply');
});

test('Practice Gym shows a Fill in the blanks card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'gap_fill_workplace_phrase')).toBeVisible();
  await expect(formatCard(page, 'gap_fill_workplace_phrase')).toContainText('Gap Fill Workplace Phrase');
});

test('Practice Gym shows a Matching card', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await expect(formatCard(page, 'phrase_match')).toBeVisible();
  await expect(formatCard(page, 'phrase_match')).toContainText('Phrase Match');
});

// ── Functional card routing ────────────────────────────────────────────────────

test('Vocabulary format opens a pool-backed phrase match activity', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await formatCard(page, 'phrase_match').click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-phrase_match/);
});

test('Listening skill card opens a pool-backed activity', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await formatCard(page, 'listen_and_answer').click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-listen_and_answer/);
});

test('Writing skill card opens a pool-backed activity', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await formatCard(page, 'open_writing_task').click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-open_writing_task/);
});

test('Speaking skill card opens a pool-backed activity', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  await formatCard(page, 'speaking_roleplay_turn').click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-speaking_roleplay_turn/);
});

// ── Coming soon cards have no navigable links ──────────────────────────────────

// ── Activated pattern cards — now functional ──────────────────────────────────

test('Workplace Chat card is functional and starts practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'teams_chat_simulation');
  await expect(card).not.toContainText('Coming soon');
  await card.click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-teams_chat_simulation/);
});

test('Email card is functional and starts practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'email_reply');
  await expect(card).not.toContainText('Coming soon');
  await card.click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-email_reply/);
});

test('Gap Fill card is functional and starts practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'gap_fill_workplace_phrase');
  await expect(card).not.toContainText('Coming soon');
  await card.click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-gap_fill_workplace_phrase/);
});

test('Phrase Match card is functional and starts practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'phrase_match');
  await expect(card).not.toContainText('Coming soon');
  await card.click();
  await expect(page).toHaveURL(/\/activity\?activityId=activity-phrase_match/);
});

test('AI role play card remains Coming soon and has no link', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'ai_role_play');
  await expect(card).toContainText('Coming soon');
  await expect(card.locator('a')).toHaveCount(0);
});

test('Speaking card does not mention pronunciation scoring', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);
  await page.goto('/practice');
  const card = formatCard(page, 'speaking_roleplay_turn');
  const cardText = await card.textContent();
  expect((cardText ?? '').toLowerCase()).not.toContain('pronunciation');
});

// ── Nav integration ────────────────────────────────────────────────────────────

test('Student nav Practice item opens /practice', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  // Also mock dashboard so Today page loads
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'student@test.com', cefrLevel: 'B1', supportLanguage: null },
        courseReadiness: { isLearningReady: true, lifecycleStatus: 'ActiveLearning', placementRequired: false, learningPlanExists: true },
        todaySession: { status: 'Ready', sessionId: 'session-1', title: "Today's Lesson", topic: null, sessionGoal: null, focusSkill: null, durationMinutes: null, exerciseCount: null, actionLabel: "Start" },
        learningPlan: { pathTitle: null, currentObjective: null, currentObjectiveDescription: null, objectiveIndex: 0, totalObjectives: 0, modulesCompleted: 0, remainingObjectives: 0, completedActivities: 0, totalActivities: 0, progressPercent: 0 },
        practice: { status: 'Ready', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: false, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
  });

  await page.goto('/dashboard');
  await page.getByTestId('nav-practice').click();

  await expect(page).toHaveURL('/practice');
  await expect(page.getByTestId('practice-gym-heading')).toBeVisible();
});

test('Vocabulary is not in the top-level student sidebar nav', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        profile: { displayName: 'student@test.com', cefrLevel: 'B1', supportLanguage: null },
        courseReadiness: { isLearningReady: true, lifecycleStatus: 'ActiveLearning', placementRequired: false, learningPlanExists: true },
        todaySession: { status: 'Ready', sessionId: 'session-1', title: "Today's Lesson", topic: null, sessionGoal: null, focusSkill: null, durationMinutes: null, exerciseCount: null, actionLabel: "Start" },
        learningPlan: { pathTitle: null, currentObjective: null, currentObjectiveDescription: null, objectiveIndex: 0, totalObjectives: 0, modulesCompleted: 0, remainingObjectives: 0, completedActivities: 0, totalActivities: 0, progressPercent: 0 },
        practice: { status: 'Ready', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
        progress: { skillProfile: [], strongSkills: [], weakSkills: [], nextRecommendedFocus: [], journeySummary: null, activitiesCompleted: 0, streakDays: 0 },
        quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
        warnings: { missingLearningPlan: false, missingTodaySession: false, practiceUnavailable: false, placementIncomplete: false },
      }),
    });
  });

  await page.goto('/dashboard');

  // Vocabulary must not appear as a nav link in the sidebar
  await expect(page.getByRole('link', { name: /^Vocabulary$/i })).toHaveCount(0);
});

// ── Adaptive suggestions section ───────────────────────────────────────────────

test('suggestions section renders recommended card with explanation', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByTestId('suggestions-section')).toBeVisible();
  await expect(page.getByTestId('suggestion-reason').first()).toContainText('Listening is your weakest skill');
});

test('review queue section is always visible with empty state when no items', async ({ page }) => {
  await withAuth(page);
  await mockPracticeRoute(page);

  await page.goto('/practice');

  await expect(page.getByTestId('review-section')).toBeVisible();
  await expect(page.getByTestId('review-queue-empty')).toBeVisible();
  await expect(page.getByTestId('review-queue-empty')).toContainText('all caught up');
});

// ── Part J: Practice completion smoke test ────────────────────────────────────

test('completing a practice activity navigates back to Practice Gym (practice completion smoke)', async ({ page }) => {
  await withAuth(page);

  const activityId = 'act-practice-smoke-1';
  const chatActivity = {
    activityId,
    activityType: 'writingScenario',
    source: 'pool',
    title: 'Reply in Teams',
    difficulty: 'B1',
    interactionMode: 'chatReply',
    exercisePatternKey: 'teams_chat_simulation',
    audioAvailable: false,
    audioUrl: null,
    audioContentType: null,
    audioDurationSeconds: null,
    audioUnavailableMessage: null,
    audioStatus: null,
    contentJson: JSON.stringify({
      scenario: 'Your manager asks for a quick update.',
      learningGoal: 'Reply professionally.',
      chatThread: [{ sender: 'Manager', message: 'Any update on the report?', timestamp: '09:00' }],
      targetPhrases: ['I wanted to update you'],
      wordLimit: 40,
    }),
    situation: null, learningGoal: null, targetPhrases: [], targetVocabulary: [],
    exampleText: null, commonMistakeToAvoid: null, instructionInSourceLanguage: null,
    instructions: null, practiceMode: null, vocabItems: null, scenario: null,
    speakerRole: null, listenerRole: null, transcriptAvailableAfterSubmit: null,
    listeningQuestions: null, responseTask: null, speakingScenario: null, studentRole: null,
    speakingListenerRole: null, speakingGoal: null, speakingPrompt: null, expectedPoints: null,
    suggestedPhrases: null, maxDurationSeconds: null, stageContent: null,
  };

  await page.route(`**/api/activity/${activityId}`, async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(chatActivity) });
  });
  await page.route(`**/api/activity/${activityId}/attempt`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        attemptId: 'attempt-practice-smoke-1', score: 85, coachSummary: 'Professional and clear reply.',
        focusFirst: false, changes: [], correctedText: null, whatYouDidWell: ['Good register.'],
        mainMistakes: [], grammarIssues: [], vocabularyIssues: [], toneIssues: [], clarityIssues: [],
        grammarExplanation: null, toneExplanation: null, vocabularyToRemember: [],
        miniLesson: null, nextImprovementStep: null, rewriteChallenge: null,
        nextPracticeSuggestion: null, feedbackInSourceLanguage: null, questionFeedback: null,
        transcript: null, responseFeedback: null, speakingStrengths: null,
        speakingImprovements: null, missingExpectedPoints: null, suggestedImprovedResponse: null,
        patternEvaluation: null,
      }),
    });
  });

  // Mock routes needed when /practice loads after returning
  await mockPracticeRoute(page);

  await page.goto(`/activity?activityId=${activityId}&returnTo=/practice`);

  // Teach phase
  await expect(page.getByTestId('teach-cta-btn')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('teach-cta-btn').click();

  // Practice phase: chat reply renderer
  await expect(page.getByTestId('chat-reply-renderer')).toBeVisible({ timeout: 5000 });
  await page.getByTestId('chat-reply-input').fill('I wanted to update you — the report is nearly done.');
  await page.getByTestId('chat-reply-submit-btn').click();

  // Feedback phase
  await expect(page.getByText('Professional and clear reply.')).toBeVisible({ timeout: 5000 });

  // Navigate back to Practice Gym
  await page.getByRole('button', { name: 'Next activity' }).click();

  await expect(page).toHaveURL('/practice', { timeout: 5000 });
  await expect(page.getByTestId('suggestions-section')).toBeVisible({ timeout: 5000 });
});

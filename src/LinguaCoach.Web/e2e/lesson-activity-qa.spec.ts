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

const SESSION_ID = 'session-qa-001';

const MIXED_SKILL_SESSION = {
  sessionId: SESSION_ID,
  title: 'Workplace Communication Mix',
  topic: 'Vocabulary, listening, and writing',
  sessionGoal: 'Practise vocabulary skills in a document controller context.',
  durationMinutes: 20,
  focusSkill: 'Vocabulary',
  status: 'notStarted',
  isResuming: false,
  exercises: [
    {
      exerciseId: 'ex-vocab',
      order: 0,
      kind: 'vocabularyWarmup',
      exercisePatternKey: 'phrase_match',
      primarySkill: 'Vocabulary',
      instructions: 'Match each phrase to its meaning.',
      estimatedMinutes: 3,
      status: 'notStarted',
      learningActivityId: null,
    },
    {
      exerciseId: 'ex-listen',
      order: 1,
      kind: 'listeningInput',
      exercisePatternKey: 'listen_and_answer',
      primarySkill: 'Listening',
      instructions: 'Listen to the audio and answer the questions.',
      estimatedMinutes: 5,
      status: 'notStarted',
      learningActivityId: null,
    },
    {
      exerciseId: 'ex-write',
      order: 2,
      kind: 'writingTask',
      exercisePatternKey: 'email_reply',
      primarySkill: 'Writing',
      instructions: 'Reply to the email professionally.',
      estimatedMinutes: 7,
      status: 'notStarted',
      learningActivityId: null,
    },
  ],
};

async function mockMixedSession(page: Page) {
  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({
        studentName: 'Sara', careerProfile: 'Document Controller', cefrLevel: 'B1',
        message: '', lifecycleStage: 'ActiveLearning',
        learningPath: {
          pathId: 'p1', title: 'WE', modulesCompleted: 0, totalModules: 2,
          currentModule: { moduleId: 'm1', title: 'Communication', description: '', order: 1,
            completedActivities: 0, totalActivities: 3 },
        },
        activityStats: { activitiesCompleted: 0, averageScore: 0, latestScore: 0 },
        currentFocus: null, nextRecommendedPractice: null, latestImprovement: null,
      }),
    });
  });
  await page.route('**/api/sessions/today', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MIXED_SKILL_SESSION) });
  });
  await page.route(`**/api/sessions/${SESSION_ID}`, async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ ...MIXED_SKILL_SESSION, startedAtUtc: null, completedAtUtc: null }),
    });
  });
  await page.route('**/api/learning-path/memory', async route => {
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ journeySummary: null, strongSkills: [], weakSkills: [],
        recurringMistakes: [], nextRecommendedFocus: [], coveredScenarioCount: 0, skillProfile: [] }),
    });
  });
}

function mockActivity(page: Page, activityBody: object) {
  return page.route('**/api/activity/next**', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify(activityBody),
  }));
}

const PHRASE_MATCH_ACTIVITY = {
  activityId: 'act-phrase-match',
  activityType: 'vocabularyPractice',
  source: 'aiGenerated',
  title: 'Phrase match warm-up',
  difficulty: 'B1',
  situation: null, learningGoal: null, targetPhrases: [], targetVocabulary: [],
  exampleText: null, commonMistakeToAvoid: null, instructionInSourceLanguage: null,
  instructions: 'Match each phrase to its meaning.',
  practiceMode: null, vocabItems: null, scenario: null,
  speakerRole: null, listenerRole: null, transcriptAvailableAfterSubmit: null,
  listeningQuestions: null, responseTask: null,
  audioAvailable: null, audioUrl: null, audioContentType: null,
  audioDurationSeconds: null, audioUnavailableMessage: null,
  speakingScenario: null, studentRole: null, speakingListenerRole: null,
  speakingGoal: null, speakingPrompt: null, expectedPoints: null,
  suggestedPhrases: null, maxDurationSeconds: null,
  interactionMode: 'matchingPairs',
  exercisePatternKey: 'phrase_match',
  contentJson: JSON.stringify({
    instructions: 'Match each phrase to its meaning.',
    pairs: [
      { id: '0', phrase: 'I would like to follow up', meaning: 'check on progress' },
      { id: '1', phrase: 'Please let me know', meaning: 'ask for information' },
    ],
  }),
};

const LISTEN_ACTIVITY_WITH_AUDIO = {
  activityId: 'act-listen',
  activityType: 'listeningComprehension',
  source: 'aiGenerated',
  title: 'Listen and answer',
  difficulty: 'B1',
  situation: null, learningGoal: null, targetPhrases: [], targetVocabulary: [],
  exampleText: null, commonMistakeToAvoid: null, instructionInSourceLanguage: null,
  instructions: 'Listen and answer the questions.',
  practiceMode: null, vocabItems: null,
  scenario: 'Your manager leaves a voicemail about a project update.',
  speakerRole: 'Manager', listenerRole: 'Professional',
  transcriptAvailableAfterSubmit: true,
  listeningQuestions: [{ id: 'q1', question: 'What does the manager want?', type: 'short_answer' }],
  responseTask: { prompt: 'Summarise the message.', expectedFocus: 'key action' },
  audioAvailable: true,
  audioUrl: '/api/activity/act-listen/audio',
  audioContentType: 'audio/mpeg',
  audioDurationSeconds: 12.5,
  audioUnavailableMessage: null,
  speakingScenario: null, studentRole: null, speakingListenerRole: null,
  speakingGoal: null, speakingPrompt: null, expectedPoints: null,
  suggestedPhrases: null, maxDurationSeconds: null,
  interactionMode: 'audioAndFreeText',
  exercisePatternKey: 'listen_and_answer',
  contentJson: null,
};

const LISTEN_ACTIVITY_NO_AUDIO = {
  ...LISTEN_ACTIVITY_WITH_AUDIO,
  audioAvailable: false,
  audioUrl: null,
  audioDurationSeconds: null,
  audioUnavailableMessage: 'Audio is temporarily unavailable. Please read the transcript below.',
};

// ── Tests ─────────────────────────────────────────────────────────────────────

test('lesson page mixed-skill session goal does not say writing-only', async ({ page }) => {
  await withAuth(page);
  await mockMixedSession(page);
  await page.goto(`/lesson/${SESSION_ID}`);
  const goalText = await page.getByTestId('session-goal').textContent() ?? '';
  expect(goalText.toLowerCase()).not.toContain('practise writing skills');
});

test('phrase_match activity renders with at least one pair visible', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, PHRASE_MATCH_ACTIVITY);
  await page.goto('/activity');
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible({ timeout: 5000 });
  const count = await page.locator('[data-testid^="phrase-"]').count();
  expect(count, 'phrase_match must render at least one pair — empty columns are a bug').toBeGreaterThan(0);
});

test('phrase_match fallback content still renders real pairs not empty columns', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, {
    ...PHRASE_MATCH_ACTIVITY,
    source: 'systemFallback',
    contentJson: JSON.stringify({
      instructions: 'Match the workplace phrases with their meanings.',
      pairs: [
        { id: '0', phrase: 'I would like to follow up', meaning: 'check on progress' },
        { id: '1', phrase: 'Please let me know', meaning: 'ask for information' },
      ],
    }),
  });
  await page.goto('/activity');
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible({ timeout: 5000 });
  const count = await page.locator('[data-testid^="phrase-"]').count();
  expect(count, 'fallback phrase_match must still show real pairs').toBeGreaterThan(0);
});

test('listen_and_answer with audio renders audio player with src', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, LISTEN_ACTIVITY_WITH_AUDIO);
  await page.goto('/activity');
  await expect(page.getByTestId('audio-free-text-renderer')).toBeVisible({ timeout: 5000 });
  const src = await page.locator('[data-testid="audio-player"]').getAttribute('src');
  expect(src, 'audio player must have a src — 0:00/0:00 with no src is a bug').toBeTruthy();
});

test('listen_and_answer with no audio shows unavailable message not a blank player', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, LISTEN_ACTIVITY_NO_AUDIO);
  await page.goto('/activity');
  await expect(page.getByTestId('audio-free-text-renderer')).toBeVisible({ timeout: 5000 });
  await expect(page.getByTestId('audio-unavailable')).toBeVisible();
  await expect(page.getByTestId('audio-unavailable')).toContainText('unavailable');
});

test('activity back button says Back to Today not Back to dashboard', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, PHRASE_MATCH_ACTIVITY);
  await page.goto('/activity');
  const backBtn = page.locator('button', { hasText: /back to/i }).first();
  await expect(backBtn).toBeVisible({ timeout: 5000 });
  const text = (await backBtn.textContent() ?? '').toLowerCase();
  expect(text, 'button must say "Back to Today" not "Back to dashboard"').not.toContain('dashboard');
  expect(text).toContain('today');
});

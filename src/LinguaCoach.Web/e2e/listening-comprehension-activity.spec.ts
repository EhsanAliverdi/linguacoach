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
  await page.addInitScript((s) => sessionStorage.setItem('speakpath.auth', s), JSON.stringify({ token, mustChangePassword: false }));
}

async function mockAudio(page: Page, path: string) {
  await page.route(`**${path}`, async route => {
    expect(route.request().headers()['authorization']).toContain('Bearer ');
    await route.fulfill({
      status: 200,
      contentType: 'audio/wav',
      body: 'RIFF____WAVEfmt ',
    });
  });
}

const listeningActivity = {
  activityId: 'listening-act-1',
  activityType: 'listeningComprehension',
  source: 'aiGenerated',
  title: 'Understand a project update',
  difficulty: 'B1',
  situation: null,
  learningGoal: null,
  targetPhrases: [],
  targetVocabulary: [],
  exampleText: null,
  commonMistakeToAvoid: null,
  instructionInSourceLanguage: null,
  instructions: 'Read the situation first. Then answer the questions as if you listened to the message.',
  practiceMode: null,
  vocabItems: null,
  scenario: 'Your manager leaves a short voice message about a project delay.',
  speakerRole: 'Manager',
  listenerRole: 'Document Controller',
  transcriptAvailableAfterSubmit: true,
  audioAvailable: true,
  audioUrl: '/api/activity/listening-act-1/audio',
  audioContentType: 'audio/wav',
  audioDurationSeconds: 12,
  audioUnavailableMessage: null,
  listeningQuestions: [
    { id: 'q1', question: 'What should you check?', type: 'short_answer' },
    { id: 'q2', question: 'How long is the delay?', type: 'short_answer' },
  ],
  responseTask: {
    prompt: 'Write a short reply confirming what you will do.',
    expectedFocus: 'confirm task and timeline',
  },
  stageContent: {
    schemaVersion: 'module_stage_v1',
    learn: {
      teachingTitle: 'Listening for action and deadline',
      explanation: 'Listen for the main idea, the action requested, and any deadline.',
      keyPoints: ['Focus on verbs', 'Note any dates or times'],
      examples: [{ phrase: 'by end of day', meaning: 'before today finishes', note: 'common deadline phrase' }],
      strategy: 'Listen for who, what, and when.',
      commonMistakes: ['Missing the deadline'],
      sourceLanguageSupport: null,
    },
    practice: {
      instructions: 'Read the situation first. Then answer the questions as if you listened to the message.',
      scenario: 'Your manager leaves a short voice message about a project delay.',
      task: 'Write a short reply confirming what you will do.',
      exerciseData: {
        speakerRole: 'Manager',
        listenerRole: 'Document Controller',
        audioScript: 'Hi, could you please check the latest delivery schedule? The supplier has confirmed a two-day delay.',
        transcriptAvailableAfterSubmit: true,
        questions: [
          { id: 'q1', question: 'What should you check?', type: 'short_answer' },
          { id: 'q2', question: 'How long is the delay?', type: 'short_answer' },
        ],
        responseTask: {
          prompt: 'Write a short reply confirming what you will do.',
          expectedFocus: 'confirm task and timeline',
        },
      },
    },
    feedbackPlan: {
      evaluationCriteria: ['Main idea understood', 'Requested action identified'],
      rubric: [],
      feedbackFocus: 'Main idea and requested action',
      successCriteria: [],
    },
  },
};

const listeningActivityWithoutAudio = {
  ...listeningActivity,
  audioAvailable: false,
  audioUrl: null,
  audioContentType: null,
  audioDurationSeconds: null,
  audioUnavailableMessage: 'Audio is temporarily unavailable. Complete this as a transcript-based listening practice.',
};

const listeningFeedback = {
  attemptId: 'attempt-1',
  score: 90,
  coachSummary: 'You understood the main workplace message and responded professionally.',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: [],
  mainMistakes: [],
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  clarityIssues: [],
  grammarExplanation: null,
  toneExplanation: null,
  vocabularyToRemember: [],
  miniLesson: 'Listen for the action, reason, and deadline.',
  nextImprovementStep: 'Underline the task and time, then answer again.',
  rewriteChallenge: null,
  nextPracticeSuggestion: null,
  feedbackInSourceLanguage: null,
  questionFeedback: [
    {
      questionId: 'q1',
      question: 'What should you check?',
      studentAnswer: 'the latest delivery schedule',
      expectedAnswerSummary: 'the latest delivery schedule',
      isCorrect: true,
      score: 100,
      feedback: 'You found the key information.',
    },
  ],
  transcript: 'Hi, could you please check the latest delivery schedule? The supplier has confirmed a two-day delay.',
  responseFeedback: 'Your reply confirms the task and keeps a professional tone.',
};

test('listening comprehension activity hides transcript before submit and reveals it after feedback', async ({ page }) => {
  await withAuth(page);
  await mockAudio(page, listeningActivity.audioUrl);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(listeningActivity) });
  });
  await page.route('**/api/activity/*/attempt', async route => {
    const body = route.request().postDataJSON();
    expect(body.answers).toEqual([
      { questionId: 'q1', answer: 'the latest delivery schedule' },
      { questionId: 'q2', answer: 'two days' },
    ]);
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(listeningFeedback) });
  });

  await page.goto('/activity');

  // Learn page: teaching content only, no exercise content
  await expect(page.getByText('Listening for action and deadline')).toBeVisible();
  await expect(page.getByText('Listen for the main idea, the action requested, and any deadline.')).toBeVisible();
  await expect(page.locator('audio')).toHaveCount(0);
  await expect(page.getByText('Transcript unlocks after you answer.')).not.toBeVisible();
  await expect(page.getByRole('button', { name: /Answer questions/i })).toHaveCount(0);
  await expect(page.getByText('supplier has confirmed a two-day delay')).not.toBeVisible();

  await page.getByRole('button', { name: /Start practice/i }).click();

  // Practice page: full exercise content
  await expect(page.getByText('Understand a project update')).toBeVisible();
  await expect(page.locator('audio')).toBeVisible();
  await expect(page.getByText('Transcript unlocks after you answer.')).toBeVisible();
  await expect(page.getByText('supplier has confirmed a two-day delay')).not.toBeVisible();

  await page.getByLabel('What should you check?').fill('the latest delivery schedule');
  await page.getByLabel('How long is the delay?').fill('two days');
  await page.getByLabel('Write a short reply confirming what you will do.').fill('Sure, I will check the schedule and send the update before 3 pm.');
  await page.getByRole('button', { name: /Check understanding/i }).click();

  await expect(page.getByText('You understood the main workplace message')).toBeVisible();
  await expect(page.getByText('supplier has confirmed a two-day delay')).toBeVisible();
  await expect(page.locator('body')).not.toContainText('"expectedAnswer"');
  await expect(page.locator('body')).not.toContainText('{"');
});

test('listening comprehension activity shows fallback note when audio is unavailable', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(listeningActivityWithoutAudio) });
  });

  await page.goto('/activity');

  await page.getByRole('button', { name: /Start practice/i }).click();

  await expect(page.locator('audio')).toHaveCount(0);
  await expect(page.getByText('Audio is temporarily unavailable')).toBeVisible();
  await expect(page.getByText('supplier has confirmed a two-day delay')).not.toBeVisible();
});

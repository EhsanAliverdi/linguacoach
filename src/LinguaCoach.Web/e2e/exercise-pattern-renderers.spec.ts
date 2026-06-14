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

const feedback = {
  attemptId: 'attempt-1',
  score: 82,
  coachSummary: 'Good work. Your answer is clear enough to continue.',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: ['You completed the task.'],
  mainMistakes: [],
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  clarityIssues: [],
  grammarExplanation: null,
  toneExplanation: null,
  vocabularyToRemember: [],
  miniLesson: null,
  nextImprovementStep: null,
  rewriteChallenge: null,
  nextPracticeSuggestion: null,
  feedbackInSourceLanguage: null,
};

function activity(overrides: Record<string, unknown>) {
  return {
    activityId: 'pattern-act-1',
    activityType: 'writingScenario',
    source: 'aiGenerated',
    title: 'Pattern activity',
    difficulty: 'B1',
    situation: null,
    learningGoal: null,
    targetPhrases: [],
    targetVocabulary: [],
    exampleText: null,
    commonMistakeToAvoid: null,
    instructionInSourceLanguage: null,
    instructions: null,
    practiceMode: null,
    vocabItems: null,
    scenario: null,
    speakerRole: null,
    listenerRole: null,
    transcriptAvailableAfterSubmit: null,
    listeningQuestions: null,
    responseTask: null,
    audioAvailable: null,
    audioUrl: null,
    audioContentType: null,
    audioDurationSeconds: null,
    audioUnavailableMessage: null,
    speakingScenario: null,
    studentRole: null,
    speakingListenerRole: null,
    speakingGoal: null,
    speakingPrompt: null,
    expectedPoints: null,
    suggestedPhrases: null,
    maxDurationSeconds: null,
    interactionMode: null,
    exercisePatternKey: null,
    contentJson: null,
    ...overrides,
  };
}

async function mockActivity(page: Page, body: object) {
  await page.route('**/api/activity/next**', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  }));
  await page.route('**/api/activity/*/attempt', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(feedback),
  }));
}

test('Activity with MatchingPairs renders correct component', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    activityType: 'vocabularyPractice',
    title: 'Match useful phrases',
    interactionMode: 'matchingPairs',
    exercisePatternKey: 'phrase_match',
    contentJson: JSON.stringify({
      instructions: 'Match each workplace phrase to its meaning.',
      pairs: [
        { phrase: 'I apologise for the delay', meaning: 'acknowledge a late task' },
        { phrase: 'Could you please confirm', meaning: 'ask for verification politely' },
      ],
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible();
  await expect(page.getByTestId('matching-pairs-renderer').getByText('I apologise for the delay').first()).toBeVisible();
  await page.getByTestId('phrase-phrase_0').click();
  await page.getByTestId('meaning-meaning_0').click();
  await page.getByTestId('phrase-phrase_1').click();
  await page.getByTestId('meaning-meaning_1').click();
  await page.getByTestId('matching-pairs-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('GapFill renders blanks and accepts input', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    activityType: 'vocabularyPractice',
    title: 'Complete the update',
    interactionMode: 'gapFill',
    exercisePatternKey: 'gap_fill_workplace_phrase',
    contentJson: JSON.stringify({
      instructions: 'Fill the blanks with professional phrases.',
      items: [
        { sentence: 'I _____ for the delay.', answer: 'apologise', distractors: ['confirm'] },
        { sentence: 'Could you please _____ the deadline?', answer: 'confirm' },
      ],
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('gap-fill-renderer')).toBeVisible();
  await page.getByTestId('gap-input-gap_1').fill('apologise');
  await page.getByTestId('gap-input-gap_2').fill('confirm');
  await page.getByTestId('gap-fill-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('ChatReply renders chat bubbles and reply box', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    title: 'Reply in Teams',
    interactionMode: 'chatReply',
    exercisePatternKey: 'teams_chat_simulation',
    contentJson: JSON.stringify({
      scenario: 'Your manager asks about a delayed document.',
      learningGoal: 'Apologise for the delay and give a clear new deadline.',
      chatThread: [
        { sender: 'Manager', message: 'Can you update me on the submittal?', timestamp: '09:14' },
      ],
      targetPhrases: ['I apologise for the delay'],
      wordLimit: 60,
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('chat-reply-renderer')).toBeVisible();
  await expect(page.getByTestId('chat-reply-goal')).toContainText('Apologise for the delay and give a clear new deadline.');
  await expect(page.getByTestId('chat-thread')).toContainText('Can you update me on the submittal?');
  await page.getByTestId('chat-reply-input').fill('I apologise for the delay. I will send the update by 3 pm.');
  await expect(page.getByText('13 words')).toBeVisible();
  await page.getByTestId('chat-reply-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('EmailReply renders subject and body fields and submits structured content', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    title: 'Reply to your manager',
    interactionMode: 'emailReply',
    exercisePatternKey: 'email_reply',
    contentJson: JSON.stringify({
      situation: 'Your manager emailed asking for a status update on the Q3 report.',
      audience: 'your manager',
      suggestedSubject: 'Re: Q3 report status',
      targetPhrases: ['I wanted to update you'],
      wordLimit: 80,
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('email-reply-renderer')).toBeVisible();
  await expect(page.getByTestId('email-reply-subject-input')).toHaveAttribute('placeholder', 'Re: Q3 report status');
  await page.getByTestId('email-reply-subject-input').fill('Re: Q3 report status');
  await page.getByTestId('email-reply-body-input').fill('I wanted to update you on the Q3 report. It will be ready by Friday.');
  await page.getByTestId('email-reply-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('AudioAndFreeText shows audio before answer fields', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    activityType: 'listeningComprehension',
    title: 'Listen and answer',
    interactionMode: 'audioAndFreeText',
    exercisePatternKey: 'listen_and_answer',
    audioUrl: '/api/activity/pattern-act-1/audio',
    contentJson: JSON.stringify({
      scenario: 'A manager leaves a short voice message.',
      questions: [{ id: 'q1', question: 'What is the message about?' }],
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('audio-free-text-renderer')).toBeVisible();
  const audioBox = page.getByTestId('audio-player-section');
  const questionBox = page.getByTestId('question-list');
  expect((await audioBox.boundingBox())!.y).toBeLessThan((await questionBox.boundingBox())!.y);
  await page.getByTestId('question-input-q1').fill('The delayed submittal.');
  await page.getByTestId('audio-free-text-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('AudioAndGapFill shows audio and blanks', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    activityType: 'listeningComprehension',
    title: 'Listen and fill gaps',
    interactionMode: 'audioAndGapFill',
    exercisePatternKey: 'listen_and_gap_fill',
    audioUrl: '/api/activity/pattern-act-1/audio',
    contentJson: JSON.stringify({
      scenario: 'A short workplace message.',
      gaps: [
        { id: '1', sentenceWithBlank: 'I _____ for the delay.', answer: 'apologise' },
      ],
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('audio-gap-fill-renderer')).toBeVisible();
  await expect(page.getByTestId('audio-player')).toBeVisible();
  await page.getByTestId('gap-input-1').fill('apologise');
  await page.getByTestId('audio-gap-fill-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

test('ReadOnly renders without submission form', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    title: 'Lesson reflection',
    interactionMode: 'readOnly',
    exercisePatternKey: 'lesson_reflection',
    contentJson: JSON.stringify({
      instructions: 'Take a moment to review the lesson.',
      reflectionPrompts: ['Which phrase will you use at work?'],
      keyPhrase: 'I apologise for the delay',
      lessonSummary: 'You practised professional delay language.',
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('read-only-renderer')).toBeVisible();
  await expect(page.getByText('Which phrase will you use at work?')).toBeVisible();
  await expect(page.getByTestId('free-text-submit-btn')).toHaveCount(0);
  await expect(page.getByTestId('matching-pairs-submit-btn')).toHaveCount(0);
});

test('Legacy writing activity falls back to FreeTextEntry renderer when raw content is present', async ({ page }) => {
  await withAuth(page);
  await mockActivity(page, activity({
    title: 'Legacy writing activity',
    contentJson: JSON.stringify({
      situation: 'Write a short professional update.',
      learningGoal: 'Keep it clear and polite.',
      targetPhrases: ['I wanted to update you'],
    }),
  }));

  await page.goto('/activity');
  await page.getByTestId('teach-cta-btn').click();
  await expect(page.getByTestId('free-text-renderer')).toBeVisible();
  await page.getByTestId('free-text-input').fill('I wanted to update you that the task is on track.');
  await page.getByTestId('free-text-submit-btn').click();
  await expect(page.getByText('Good work. Your answer is clear enough to continue.')).toBeVisible();
});

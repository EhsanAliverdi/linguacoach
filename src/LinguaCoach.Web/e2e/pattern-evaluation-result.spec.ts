import { expect, test, type Page } from '@playwright/test';

// ── Auth helpers ──────────────────────────────────────────────────────────────

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

// ── Shared activity + feedback builders ───────────────────────────────────────

function activity(overrides: Record<string, unknown>) {
  return {
    activityId: 'pattern-act-1',
    activityType: 'vocabularyPractice',
    source: 'aiGenerated',
    title: 'Pattern activity',
    difficulty: 'B1',
    situation: null, learningGoal: null, targetPhrases: [], targetVocabulary: [],
    exampleText: null, commonMistakeToAvoid: null, instructionInSourceLanguage: null,
    instructions: null, practiceMode: null, vocabItems: null, scenario: null,
    speakerRole: null, listenerRole: null, transcriptAvailableAfterSubmit: null,
    listeningQuestions: null, responseTask: null, audioAvailable: null, audioUrl: null,
    audioContentType: null, audioDurationSeconds: null, audioUnavailableMessage: null,
    speakingScenario: null, studentRole: null, speakingListenerRole: null,
    speakingGoal: null, speakingPrompt: null, expectedPoints: null,
    suggestedPhrases: null, maxDurationSeconds: null,
    interactionMode: null, exercisePatternKey: null, contentJson: null,
    ...overrides,
  };
}

function baseFeedback() {
  return {
    attemptId: 'attempt-1',
    score: null,
    coachSummary: null,
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
    miniLesson: null,
    nextImprovementStep: null,
    rewriteChallenge: null,
    nextPracticeSuggestion: null,
    feedbackInSourceLanguage: null,
    questionFeedback: null,
    transcript: null,
    responseFeedback: null,
    speakingStrengths: null,
    speakingImprovements: null,
    missingExpectedPoints: null,
    suggestedImprovedResponse: null,
    patternEvaluation: null,
  };
}

function patternEvaluation(overrides: Record<string, unknown>) {
  return {
    exercisePatternKey: null,
    markingMode: 'KeyedSelection',
    score: 100,
    maxScore: 100,
    percentage: 100,
    passed: true,
    completed: true,
    itemResults: [],
    coachSummary: null,
    corrections: [],
    suggestedImprovedAnswer: null,
    skillImpacts: [],
    memorySignals: [],
    ...overrides,
  };
}

async function mockPatternActivity(page: Page, activityBody: object, feedbackBody: object) {
  await page.route('**/api/activity/next**', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(activityBody),
  }));
  await page.route('**/api/activity/*/attempt', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(feedbackBody),
  }));
}

// ── A. MatchingPairs result UI ────────────────────────────────────────────────

test('phrase_match result shows score card and per-pair correct/incorrect state', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 50,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'phrase_match',
      markingMode: 'KeyedSelection',
      score: 1,
      maxScore: 2,
      percentage: 50,
      passed: false,
      completed: true,
      coachSummary: 'One pair was correct. Review the incorrect one.',
      itemResults: [
        { itemKey: 'phrase_0', studentAnswer: 'acknowledge a late task', correctAnswer: 'acknowledge a late task', acceptedAnswers: ['acknowledge a late task'], isCorrect: true, score: 1, maxScore: 1, feedback: null },
        { itemKey: 'phrase_1', studentAnswer: 'wrong meaning', correctAnswer: 'ask for verification politely', acceptedAnswers: ['ask for verification politely'], isCorrect: false, score: 0, maxScore: 1, feedback: null },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'matchingPairs', exercisePatternKey: 'phrase_match', contentJson: JSON.stringify({ pairs: [{ id: '0', phrase: 'I apologise for the delay', meaning: 'acknowledge a late task' }, { id: '1', phrase: 'Could you please confirm', meaning: 'ask for verification politely' }] }) }),
    fb);

  await page.goto('/activity');
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible();
  await page.getByTestId('phrase-0').click();
  await page.getByTestId('meaning-0').click();
  await page.getByTestId('phrase-1').click();
  await page.getByTestId('meaning-1').click();
  await page.getByTestId('matching-pairs-submit-btn').click();

  await expect(page.getByTestId('pattern-evaluation-result')).toBeVisible();
  await expect(page.getByTestId('pattern-score-card')).toBeVisible();
  await expect(page.getByTestId('pattern-score-card')).toContainText('50%');
  await expect(page.getByTestId('pattern-matching-pairs-result')).toBeVisible();
  await expect(page.getByTestId('pattern-coach-summary')).toContainText('One pair was correct');
});

test('phrase_match full score shows Great work label', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 100,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'phrase_match',
      percentage: 100,
      passed: true,
      coachSummary: 'All correct! Well done.',
      itemResults: [
        { itemKey: 'phrase_0', studentAnswer: 'acknowledge a late task', correctAnswer: 'acknowledge a late task', acceptedAnswers: [], isCorrect: true, score: 1, maxScore: 1, feedback: null },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'matchingPairs', exercisePatternKey: 'phrase_match', contentJson: JSON.stringify({ pairs: [{ id: '0', phrase: 'Phrase A', meaning: 'acknowledge a late task' }] }) }),
    fb);

  await page.goto('/activity');
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible();
  await page.getByTestId('phrase-0').click();
  await page.getByTestId('meaning-0').click();
  await page.getByTestId('matching-pairs-submit-btn').click();

  await expect(page.getByTestId('pattern-score-card')).toContainText('Great work');
});

// ── B. GapFill result UI ──────────────────────────────────────────────────────

test('gap_fill_workplace_phrase result shows per-gap correct/incorrect', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 50,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'gap_fill_workplace_phrase',
      markingMode: 'ExactMatch',
      score: 1,
      maxScore: 2,
      percentage: 50,
      passed: false,
      completed: true,
      coachSummary: 'One answer was correct.',
      itemResults: [
        { itemKey: 'gap_1', studentAnswer: 'apologise', correctAnswer: 'apologise', acceptedAnswers: ['apologise', 'apologize'], isCorrect: true, score: 1, maxScore: 1, feedback: null },
        { itemKey: 'gap_2', studentAnswer: 'wrong', correctAnswer: 'confirm', acceptedAnswers: ['confirm'], isCorrect: false, score: 0, maxScore: 1, feedback: null },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'gapFill', exercisePatternKey: 'gap_fill_workplace_phrase', contentJson: JSON.stringify({ items: [{ sentence: 'I _____ for the delay.', answer: 'apologise' }, { sentence: 'Could you please _____ the deadline?', answer: 'confirm' }] }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('gap-input-1').fill('apologise');
  await page.getByTestId('gap-input-2').fill('wrong');
  await page.getByTestId('gap-fill-submit-btn').click();

  await expect(page.getByTestId('pattern-gap-fill-result')).toBeVisible();
  await expect(page.getByTestId('pattern-gap-fill-result')).toContainText('apologise');
  await expect(page.getByTestId('pattern-gap-fill-result')).toContainText('confirm');
});

test('listen_and_gap_fill result shows gap feedback', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'listen_and_gap_fill',
      percentage: 100,
      passed: true,
      completed: true,
      itemResults: [
        { itemKey: '1', studentAnswer: 'apologise', correctAnswer: 'apologise', acceptedAnswers: [], isCorrect: true, score: 1, maxScore: 1, feedback: null },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'audioAndGapFill', exercisePatternKey: 'listen_and_gap_fill', audioUrl: '/api/activity/pattern-act-1/audio', contentJson: JSON.stringify({ gaps: [{ id: '1', sentenceWithBlank: 'I _____ for the delay.', answer: 'apologise' }] }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('gap-input-1').fill('apologise');
  await page.getByTestId('audio-gap-fill-submit-btn').click();

  await expect(page.getByTestId('pattern-gap-fill-result')).toBeVisible();
});

// ── C. Chat/Email AI result UI ────────────────────────────────────────────────

test('email_reply result shows coach summary and suggested improved answer', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 75,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'email_reply',
      markingMode: 'AiStructured',
      percentage: 75,
      passed: true,
      completed: true,
      coachSummary: 'Good structure. Tone could be more formal.',
      corrections: [
        { category: 'tone', original: 'please send', suggestion: 'Could you please send', explanation: 'More formal phrasing' },
      ],
      suggestedImprovedAnswer: 'Dear Mr. Smith,\n\nThank you for your email.',
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'freeTextEntry', exercisePatternKey: 'email_reply', contentJson: JSON.stringify({ situation: 'Reply to your manager', taskDescription: 'Write a professional reply.' }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('free-text-input').fill('Please send the document.');
  await page.getByTestId('free-text-submit-btn').click();

  await expect(page.getByTestId('pattern-evaluation-result')).toBeVisible();
  await expect(page.getByTestId('pattern-coach-summary')).toContainText('Good structure');
  await expect(page.getByTestId('pattern-chat-email-result')).toBeVisible();
  await expect(page.getByTestId('pattern-chat-email-result')).toContainText('Could you please send');
  await expect(page.getByTestId('pattern-improved-answer')).toBeVisible();
});

test('teams_chat_simulation shows chat/email-style feedback', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 80,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'teams_chat_simulation',
      markingMode: 'AiStructured',
      percentage: 80,
      passed: true,
      completed: true,
      coachSummary: 'Good tone for a chat message.',
      corrections: [
        { category: 'tone', original: 'send update', suggestion: 'I will send an update shortly', explanation: 'More professional' },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'chatReply', exercisePatternKey: 'teams_chat_simulation', contentJson: JSON.stringify({ chatThread: [{ sender: 'Manager', message: 'Update please?' }] }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('chat-reply-input').fill('I apologise for the delay. I will update you shortly.');
  await page.getByTestId('chat-reply-submit-btn').click();

  await expect(page.getByTestId('pattern-evaluation-result')).toBeVisible();
  await expect(page.getByTestId('pattern-chat-email-result')).toBeVisible();
  await expect(page.getByTestId('pattern-coach-summary')).toContainText('Good tone');
});

// ── D. Audio short-answer (listen_and_answer) result UI ───────────────────────

test('listen_and_answer result shows question-by-question feedback', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 80,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'listen_and_answer',
      markingMode: 'AiStructured',
      percentage: 80,
      passed: true,
      completed: true,
      coachSummary: 'Good comprehension overall.',
      itemResults: [
        { itemKey: 'q1', studentAnswer: 'project delay', correctAnswer: 'delivery delay', acceptedAnswers: [], isCorrect: true, score: 1, maxScore: 1, feedback: 'Close enough.' },
        { itemKey: 'q2', studentAnswer: 'manager', correctAnswer: 'project manager', acceptedAnswers: [], isCorrect: false, score: 0, maxScore: 1, feedback: 'Be more specific.' },
      ],
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'audioAndFreeText', exercisePatternKey: 'listen_and_answer', audioUrl: '/api/activity/pattern-act-1/audio', contentJson: JSON.stringify({ questions: [{ id: 'q1', question: 'What was the problem?' }, { id: 'q2', question: 'Who is responsible?' }] }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('question-input-q1').fill('project delay');
  await page.getByTestId('question-input-q2').fill('manager');
  await page.getByTestId('audio-free-text-submit-btn').click();

  await expect(page.getByTestId('pattern-listen-answer-result')).toBeVisible();
  await expect(page.getByTestId('pattern-listen-answer-result')).toContainText('project delay');
  await expect(page.getByTestId('pattern-listen-answer-result')).toContainText('Be more specific');
});

// ── E. Spoken response result — no pronunciation mention ──────────────────────

test('spoken_response_from_prompt result does not claim pronunciation scoring', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 72,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'spoken_response_from_prompt',
      markingMode: 'AiOpenEnded',
      percentage: 72,
      passed: true,
      completed: true,
      coachSummary: 'Clear and organised response.',
      corrections: [
        { category: 'speaking', original: null, suggestion: 'Use more formal vocabulary', explanation: 'Professional context requires formal language' },
      ],
      suggestedImprovedAnswer: 'I would like to inform you that the report will be delayed.',
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'freeTextEntry', exercisePatternKey: 'spoken_response_from_prompt', contentJson: JSON.stringify({ prompt: 'Explain the delay verbally.' }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('free-text-input').fill('The report is late because of issues.');
  await page.getByTestId('free-text-submit-btn').click();

  await expect(page.getByTestId('pattern-spoken-result')).toBeVisible();
  await expect(page.getByTestId('pattern-coach-summary')).toContainText('Clear and organised');

  // Must not mention pronunciation or accent
  const resultText = await page.getByTestId('pattern-evaluation-result').innerText();
  expect(resultText.toLowerCase()).not.toContain('pronunciation');
  expect(resultText.toLowerCase()).not.toContain('accent');
});

test('spoken_response_from_prompt suggested response labels it as coaching, not pronunciation target', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'spoken_response_from_prompt',
      markingMode: 'AiOpenEnded',
      percentage: 70,
      passed: true,
      completed: true,
      suggestedImprovedAnswer: 'I would like to let you know that the deadline has changed.',
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'freeTextEntry', exercisePatternKey: 'spoken_response_from_prompt', contentJson: JSON.stringify({ prompt: 'Discuss the deadline change.' }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('free-text-input').fill('Deadline changed.');
  await page.getByTestId('free-text-submit-btn').click();

  const improvedDetails = page.getByTestId('pattern-spoken-improved');
  await improvedDetails.click();
  const bodyText = await improvedDetails.innerText();
  // The disclaimer must frame "pronunciation target" as what it is NOT, and mention coaching
  expect(bodyText.toLowerCase()).toContain('coaching suggestion');
  expect(bodyText.toLowerCase()).not.toContain('improve your pronunciation');
});

// ── F. ReadOnly / lesson_reflection result UI ─────────────────────────────────

test('lesson_reflection renders read-only renderer with no submission form', async ({ page }) => {
  await withAuth(page);

  await mockPatternActivity(page,
    activity({ interactionMode: 'readOnly', exercisePatternKey: 'lesson_reflection', contentJson: JSON.stringify({ lessonSummary: 'You practised delay language.', reflectionPrompts: ['Which phrase will you use?'] }) }),
    baseFeedback());

  await page.goto('/activity');
  // Read-only renderer renders — no gap inputs or submit buttons
  await expect(page.getByTestId('read-only-renderer')).toBeVisible();
  await expect(page.getByTestId('gap-fill-submit-btn')).toHaveCount(0);
  await expect(page.getByTestId('matching-pairs-submit-btn')).toHaveCount(0);
  // Continue button is visible
  await expect(page.getByTestId('read-only-done-btn')).toBeVisible();
});

test('pattern-readonly-complete block renders when patternEvaluation result has isReadOnly key', async ({ page }) => {
  // Verify the component block by getting a lesson_reflection result back from a submit-based flow.
  // We simulate this by using a freeTextEntry activity that happens to return lesson_reflection
  // patternEvaluation — exercises the component branch.
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'lesson_reflection',
      markingMode: 'NoMarking',
      score: 0,
      maxScore: 0,
      percentage: 0,
      passed: true,
      completed: true,
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'freeTextEntry', exercisePatternKey: 'lesson_reflection', contentJson: JSON.stringify({ taskDescription: 'Reflect on the lesson.' }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('free-text-input').fill('I will use the delay phrase.');
  await page.getByTestId('free-text-submit-btn').click();

  await expect(page.getByTestId('pattern-readonly-complete')).toBeVisible();
  await expect(page.getByTestId('pattern-readonly-complete')).toContainText('Step complete');
  await expect(page.getByTestId('pattern-score-card')).not.toBeVisible();
});

// ── Today's Lesson return flow ────────────────────────────────────────────────

test('pattern activity Next activity navigates back via returnTo param', async ({ page }) => {
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 100,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'phrase_match',
      percentage: 100,
      passed: true,
      completed: true,
      coachSummary: 'All correct!',
    }),
  };

  await page.route('**/api/activity/pattern-act-1', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(activity({ activityId: 'pattern-act-1', interactionMode: 'matchingPairs', exercisePatternKey: 'phrase_match', contentJson: JSON.stringify({ pairs: [{ id: '0', phrase: 'Phrase A', meaning: 'Meaning A' }] }) })),
  }));
  await page.route('**/api/activity/pattern-act-1/attempt', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(fb),
  }));

  await page.goto('/activity?activityId=pattern-act-1&returnTo=/lesson/session-123');
  await expect(page.getByTestId('matching-pairs-renderer')).toBeVisible();

  await page.getByTestId('phrase-0').click();
  await page.getByTestId('meaning-0').click();
  await page.getByTestId('matching-pairs-submit-btn').click();

  await expect(page.getByTestId('pattern-evaluation-result')).toBeVisible();
  await page.getByRole('button', { name: /next activity/i }).click();

  await expect(page).toHaveURL(/\/lesson\/session-123/);
});

// ── Legacy non-pattern activities keep existing feedback UI ───────────────────

test('legacy writing activity does not show pattern-evaluation-result', async ({ page }) => {
  await withAuth(page);

  const legacyFb = {
    ...baseFeedback(),
    score: 72,
    coachSummary: 'Good overall structure.',
    whatYouDidWell: ['Clear opening sentence'],
    changes: [{ type: 'replace', original: 'please approve', suggested: 'Could you please approve', reason: 'More polite', category: 'tone', severity: 'medium' }],
    patternEvaluation: null,
  };

  // writingScenario + contentJson triggers FreeTextEntry renderer without an exercisePatternKey
  await page.route('**/api/activity/next**', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(activity({
      activityType: 'writingScenario',
      title: 'Legacy writing activity',
      contentJson: JSON.stringify({ situation: 'Write a professional email.', learningGoal: 'Practice formal tone.' }),
    })),
  }));
  await page.route('**/api/activity/*/attempt', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(legacyFb),
  }));

  await page.goto('/activity');
  await expect(page.getByTestId('free-text-renderer')).toBeVisible();
  await page.getByTestId('free-text-input').fill('Please approve the document.');
  await page.getByTestId('free-text-submit-btn').click();

  // Legacy result: coach summary and changes visible
  await expect(page.getByText('Good overall structure.')).toBeVisible();
  await expect(page.getByText('Could you please approve')).toBeVisible();

  // Pattern-aware block must NOT appear
  await expect(page.getByTestId('pattern-evaluation-result')).not.toBeVisible();
});

// ── Mobile viewport: no horizontal overflow ───────────────────────────────────

test('pattern result has no horizontal overflow on mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await withAuth(page);

  const fb = {
    ...baseFeedback(),
    score: 80,
    patternEvaluation: patternEvaluation({
      exercisePatternKey: 'email_reply',
      markingMode: 'AiStructured',
      percentage: 80,
      passed: true,
      completed: true,
      coachSummary: 'Well structured email with a professional tone.',
      corrections: [
        { category: 'grammar', original: 'I wanted follow up', suggestion: 'I wanted to follow up', explanation: 'Missing preposition' },
      ],
      suggestedImprovedAnswer: 'Dear Ms. Brown,\n\nI wanted to follow up on the document submitted last week.',
    }),
  };

  await mockPatternActivity(page,
    activity({ interactionMode: 'freeTextEntry', exercisePatternKey: 'email_reply', contentJson: JSON.stringify({ taskDescription: 'Write a follow-up email.' }) }),
    fb);

  await page.goto('/activity');
  await page.getByTestId('free-text-input').fill('I wanted follow up on the document.');
  await page.getByTestId('free-text-submit-btn').click();

  await expect(page.getByTestId('pattern-evaluation-result')).toBeVisible();

  // No horizontal scroll — page body width should equal viewport width
  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 2); // 2px tolerance
});

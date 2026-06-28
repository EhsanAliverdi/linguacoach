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

const token = fakeJwt('student@test.com');
const sessionData = JSON.stringify({ token, mustChangePassword: false });

async function withAuth(page: Page) {
  await page.addInitScript((s) => sessionStorage.setItem('speakpath.auth', s), sessionData);
}

const vocabActivity = {
  activityId: 'vocab-act-1',
  activityType: 'vocabularyPractice',
  source: 'aiGenerated',
  title: 'Practise polite requests',
  difficulty: 'B1',
  situation: null,
  learningGoal: null,
  targetPhrases: [],
  targetVocabulary: [],
  exampleText: null,
  commonMistakeToAvoid: null,
  instructionInSourceLanguage: null,
  instructions: 'Fill in the blank with the most professional phrase.',
  practiceMode: 'fill_blank',
  vocabItems: [
    {
      vocabularyItemId: 'item-1',
      term: 'could you please',
      prompt: '_____ send me the updated file?',
      hint: 'Use a polite request phrase.',
      explanation: 'A polite way to make a workplace request.',
    },
  ],
};

const vocabFeedback = {
  attemptId: 'attempt-1',
  score: 100,
  coachSummary: 'Perfect — you got it correct!',
  focusFirst: false,
  changes: [],
  correctedText: null,
  whatYouDidWell: ['Correct use of could you please'],
  mainMistakes: [],
  grammarIssues: [],
  vocabularyIssues: [],
  toneIssues: [],
  clarityIssues: [],
  grammarExplanation: null,
  toneExplanation: null,
  vocabularyToRemember: [],
  miniLesson: 'Use modal verbs to soften requests.',
  nextImprovementStep: 'Try these phrases in your next writing activity.',
  rewriteChallenge: null,
  nextPracticeSuggestion: null,
  feedbackInSourceLanguage: null,
};

test('vocabulary practice activity renders and submits', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabActivity) });
  });
  await page.route('**/api/activity/*/attempt', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabFeedback) });
  });

  await page.goto('/activity');

  // Learning state — shows vocab items
  await expect(page.getByText('Practise polite requests')).toBeVisible();
  await expect(page.getByText('Fill in the blank with the most professional phrase.')).toBeVisible();
  await expect(page.getByText('could you please')).toBeVisible();

  // Start practice
  await page.getByRole('button', { name: /Start practice/i }).click();

  // Practice state — fill-blank input
  await expect(page.getByText('_____ send me the updated file?')).toBeVisible();
  const input = page.getByPlaceholder('Type the missing phrase…');
  await input.fill('could you please');

  // Submit
  await page.getByRole('button', { name: /Check answers/i }).click();
  await page.waitForTimeout(300);

  // Feedback state
  await expect(page.getByText('Perfect — you got it correct!')).toBeVisible();
});

test('vocabulary practice shows hint when requested', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabActivity) });
  });

  await page.goto('/activity');
  await page.getByRole('button', { name: /Start practice/i }).click();
  await expect(page.getByText('_____ send me the updated file?')).toBeVisible();

  // Hint hidden initially
  await expect(page.getByText('Use a polite request phrase.')).not.toBeVisible();

  // Click hint toggle
  await page.getByRole('button', { name: /Show hint/i }).click();
  await expect(page.getByText('Use a polite request phrase.')).toBeVisible();
});

test('vocabulary practice does not show raw JSON', async ({ page }) => {
  await withAuth(page);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabActivity) });
  });
  await page.route('**/api/activity/*/attempt', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabFeedback) });
  });

  await page.goto('/activity');
  await page.getByRole('button', { name: /Start practice/i }).click();

  const input = page.getByPlaceholder('Type the missing phrase…');
  await input.fill('could you please');
  await page.getByRole('button', { name: /Check answers/i }).click();
  await page.waitForTimeout(300);

  await expect(page.locator('body')).not.toContainText('"vocabularyItemId"');
  await expect(page.locator('body')).not.toContainText('{"');
});

test.skip('vocabulary practice has no unexpected console errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });

  await withAuth(page);
  await page.route('**/api/activity/next', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(vocabActivity) });
  });

  await page.goto('/activity');
  await page.waitForTimeout(500);

  const unexpected = errors.filter(e =>
    !e.includes('401') && !e.includes('Unauthorized') && !e.includes('favicon')
  );
  expect(unexpected).toHaveLength(0);
});

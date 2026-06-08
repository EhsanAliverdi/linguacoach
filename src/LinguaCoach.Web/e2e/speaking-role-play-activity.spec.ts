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

const speakingActivity = {
  activityId: 'speaking-act-1',
  activityType: 'speakingRolePlay',
  source: 'aiGenerated',
  title: 'Explain a delay to your manager',
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
  speakingScenario: 'Your manager asks why a project task is delayed. Record a short professional response.',
  studentRole: 'Project Planner',
  speakingListenerRole: 'Manager',
  speakingGoal: 'Explain the delay clearly and politely.',
  speakingPrompt: 'Record a 30–60 second response explaining the delay, the reason, and the next action.',
  expectedPoints: ['mention the delay', 'give a brief reason', 'explain the next action'],
  suggestedPhrases: ['I wanted to update you on...', 'The delay is due to...'],
  maxDurationSeconds: 60,
};

const speakingFeedback = {
  attemptId: 'attempt-speaking-1',
  score: 72,
  coachSummary: 'Your response was clear and polite.',
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
  miniLesson: 'When explaining a delay: situation + reason + next action.',
  nextImprovementStep: 'Try again and include when you will send the revised timeline.',
  rewriteChallenge: null,
  nextPracticeSuggestion: null,
  feedbackInSourceLanguage: null,
  questionFeedback: null,
  transcript: 'I wanted to update you about the delay. The supplier is late, and I will send the revised timeline today.',
  responseFeedback: null,
  speakingStrengths: ['clear opening', 'polite tone'],
  speakingImprovements: ['mention the next action earlier'],
  missingExpectedPoints: [],
  suggestedImprovedResponse: 'I wanted to update you on the delivery delay...',
};

test.describe('SpeakingRolePlay activity', () => {
  test('dashboard Speaking card is active and links to SpeakingRolePlay', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/dashboard', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ studentName: 'Test', greetingMessage: '', weeklyStreak: 0, recentActivities: [] }),
    }));

    await page.goto('/dashboard');
    const speakingCard = page.getByTestId('speaking-card');
    await expect(speakingCard).toBeVisible();
    await expect(speakingCard).not.toHaveAttribute('aria-disabled');
    // Should not say "Coming soon"
    await expect(speakingCard).not.toContainText('Coming soon');
    await expect(speakingCard).toContainText('Speaking');
  });

  test('Pronunciation card remains Coming soon', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/dashboard', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ studentName: 'Test', greetingMessage: '', weeklyStreak: 0, recentActivities: [] }),
    }));

    await page.goto('/dashboard');
    const pronouncCard = page.locator('[data-testid="pronunciation-card"], .sp-card:has-text("Pronunciation")');
    if (await pronouncCard.count() > 0) {
      await expect(pronouncCard.first()).toContainText('Coming soon');
    }
  });

  test('/activity?type=SpeakingRolePlay renders speaking scenario', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/activity/next*', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingActivity),
    }));

    // Ensure MediaRecorder and mediaDevices are available so we reach the learning state
    await page.addInitScript(() => {
      if (!navigator.mediaDevices) {
        Object.defineProperty(navigator, 'mediaDevices', { value: { getUserMedia: async () => ({ getTracks: () => [] }) }, writable: true });
      }
      if (!(window as unknown as { MediaRecorder?: unknown }).MediaRecorder) {
        (window as unknown as { MediaRecorder: unknown }).MediaRecorder = class {
          state = 'inactive'; mimeType = 'audio/webm';
          ondataavailable = null; onstop = null;
          start() {} stop() {}
        };
      }
    });

    await page.goto('/activity?type=SpeakingRolePlay');
    await expect(page.getByText('Explain a delay to your manager').first()).toBeVisible({ timeout: 8000 });
    await expect(page.getByText('Explain the delay clearly and politely.').first()).toBeVisible();
    await expect(page.getByText('Start recording')).toBeVisible();
  });

  test('microphone unsupported state renders friendly message', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/activity/next*', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingActivity),
    }));

    // Remove MediaRecorder to simulate unsupported browser
    await page.addInitScript(() => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).MediaRecorder = undefined;
    });

    await page.goto('/activity?type=SpeakingRolePlay');
    await expect(page.getByText('Microphone not supported')).toBeVisible();
  });

  test('mocked MediaRecorder recording flow: record → stop → preview → feedback', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/activity/next*', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingActivity),
    }));
    await page.route('**/api/activity/*/speaking-attempt', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingFeedback),
    }));

    // Mock MediaRecorder and getUserMedia
    await page.addInitScript(() => {
      const mockStream = { getTracks: () => [{ stop: () => {} }] };

      if (!navigator.mediaDevices) {
        Object.defineProperty(navigator, 'mediaDevices', { value: {}, writable: true });
      }
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (navigator.mediaDevices as any).getUserMedia = async () => mockStream;

      class MockMediaRecorder extends EventTarget {
        state = 'inactive';
        mimeType = 'audio/mp4'; // Safari MIME type — verifies T5 fix
        ondataavailable: ((e: { data: Blob }) => void) | null = null;
        onstop: (() => void) | null = null;
        start() {
          this.state = 'recording';
          setTimeout(() => {
            if (this.ondataavailable) this.ondataavailable({ data: new Blob([new Uint8Array(100)], { type: 'audio/mp4' }) });
          }, 50);
        }
        stop() {
          this.state = 'inactive';
          if (this.onstop) this.onstop();
        }
      }
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).MediaRecorder = MockMediaRecorder;
    });

    await page.goto('/activity?type=SpeakingRolePlay');

    // Scenario loads
    await expect(page.getByText('Start recording')).toBeVisible();
    await page.getByText('Start recording').click();

    // Mic permission state → ready (getUserMedia mock resolves quickly)
    await expect(page.getByText('🔴 Record')).toBeVisible({ timeout: 5000 });

    // Click record
    const recordButton = page.getByText('🔴 Record');
    await expect(recordButton).toBeVisible();
    await recordButton.click();

    // Recording state
    await expect(page.getByText(/Stop recording/)).toBeVisible({ timeout: 2000 });
    await page.getByText(/Stop recording/).click();

    // Preview state — audio element should appear
    await expect(page.locator('audio')).toBeVisible({ timeout: 2000 });
    await expect(page.getByText('Submit recording')).toBeVisible();

    // Submit
    await page.getByText('Submit recording').click();

    // Feedback renders
    await expect(page.getByText('Your response was clear and polite.')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('I wanted to update you about the delay.')).toBeVisible();
    await expect(page.getByText('clear opening')).toBeVisible();
  });

  test('speaking feedback shows transcript and coach summary', async ({ page }) => {
    await withAuth(page);
    await page.route('**/api/activity/next*', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingActivity),
    }));
    await page.route('**/api/activity/*/speaking-attempt', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingFeedback),
    }));

    await page.addInitScript(() => {
      const mockStream = { getTracks: () => [{ stop: () => {} }] };
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (navigator.mediaDevices as any).getUserMedia = async () => mockStream;
      class MockMediaRecorder extends EventTarget {
        state = 'inactive'; mimeType = 'audio/webm';
        ondataavailable: ((e: { data: Blob }) => void) | null = null;
        onstop: (() => void) | null = null;
        start() { this.state = 'recording'; setTimeout(() => { if (this.ondataavailable) this.ondataavailable({ data: new Blob([new Uint8Array(100)]) }); }, 50); }
        stop() { this.state = 'inactive'; if (this.onstop) this.onstop(); }
      }
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).MediaRecorder = MockMediaRecorder;
    });

    await page.goto('/activity?type=SpeakingRolePlay');
    await page.getByText('Start recording').click();
    await page.getByText('🔴 Record').click();
    await expect(page.getByText(/Stop recording/)).toBeVisible({ timeout: 2000 });
    await page.getByText(/Stop recording/).click();
    await expect(page.getByText('Submit recording')).toBeVisible({ timeout: 2000 });
    await page.getByText('Submit recording').click();

    await expect(page.getByText('Your response was clear and polite.')).toBeVisible({ timeout: 5000 });
    await expect(page.getByText('Transcript')).toBeVisible();
    await expect(page.getByText('I wanted to update you about the delay.')).toBeVisible();
  });

  test('no unexpected console errors on speaking flow', async ({ page }) => {
    const errors: string[] = [];
    page.on('console', (msg) => { if (msg.type() === 'error') errors.push(msg.text()); });

    await withAuth(page);
    await page.route('**/api/activity/next*', (route) => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(speakingActivity),
    }));

    await page.addInitScript(() => {
      const mockStream = { getTracks: () => [{ stop: () => {} }] };
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (navigator.mediaDevices as any).getUserMedia = async () => mockStream;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).MediaRecorder = class { state='inactive'; mimeType='audio/webm'; ondataavailable=null; onstop=null; start(){}; stop(){} };
    });

    await page.goto('/activity?type=SpeakingRolePlay');
    await expect(page.getByText('Start recording')).toBeVisible();

    const filtered = errors.filter(e => !e.includes('favicon') && !e.includes('net::ERR'));
    expect(filtered, `Unexpected console errors: ${filtered.join('; ')}`).toHaveLength(0);
  });
});

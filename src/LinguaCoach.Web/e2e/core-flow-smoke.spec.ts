import { expect, test, type Page } from '@playwright/test';

const ids = {
  languagePairId: '11111111-1111-1111-1111-111111111111',
  learningTrackId: '22222222-2222-2222-2222-222222222222',
  careerProfileId: '33333333-3333-3333-3333-333333333333',
  activityId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
};

function fakeJwt(email: string, role: 'Admin' | 'Student') {
  const header = toBase64Url({ alg: 'none', typ: 'JWT' });
  const payload = toBase64Url({
    sub: `${role.toLowerCase()}-user-id`,
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

async function mockApi(page: Page) {
  let createdStudentEmail = '';
  let onboardingStep = 'None';

  await page.route('**/api/auth/login', async route => {
    const request = route.request();
    const body = request.postDataJSON() as { email: string };
    const isAdmin = body.email.includes('admin');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        token: fakeJwt(body.email, isAdmin ? 'Admin' : 'Student'),
        role: isAdmin ? 'Admin' : 'Student',
        mustChangePassword: !isAdmin,
      }),
    });
  });

  await page.route('**/api/auth/change-password', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/api/admin/students', async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
      return;
    }

    const body = route.request().postDataJSON() as { email: string };
    createdStudentEmail = body.email;
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        studentProfileId: 'student-profile-id',
        userId: 'student-user-id',
      }),
    });
  });

  await page.route('**/api/onboarding/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        currentStep: onboardingStep,
        isComplete: onboardingStep === 'Skill',
        languagePairId: onboardingStep === 'None' ? null : ids.languagePairId,
      }),
    });
  });

  await page.route('**/api/onboarding', async route => {
    const body = route.request().postDataJSON() as { step: string };
    onboardingStep =
      body.step === 'language' ? 'Language' :
      body.step === 'track' ? 'Track' :
      body.step === 'career' ? 'Career' :
      'Skill';

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        lastCompletedStep: onboardingStep,
        isComplete: onboardingStep === 'Skill',
      }),
    });
  });

  await page.route('**/api/reference/language-pairs', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: ids.languagePairId, sourceCode: 'fa', sourceName: 'Persian', targetCode: 'en', targetName: 'English' },
      ]),
    });
  });

  await page.route('**/api/reference/tracks**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: ids.learningTrackId, name: 'Workplace English', description: 'Professional communication practice.' },
      ]),
    });
  });

  await page.route('**/api/reference/career-profiles**', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: ids.careerProfileId, name: 'Document Controller', description: 'Project support and document control communication.' },
      ]),
    });
  });

  await page.route('**/api/dashboard', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        studentName: createdStudentEmail || 'student@example.com',
        careerProfile: 'Document Controller',
        cefrLevel: null,
        message: 'You are on module 1 of 5.',
        learningPath: {
          pathId: 'pppppppp-pppp-pppp-pppp-pppppppppppp',
          title: 'Workplace English for Document Controller — B1',
          modulesCompleted: 0,
          totalModules: 5,
          currentModule: {
            moduleId: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
            title: 'Professional email writing',
            description: 'Practice writing clear, polite workplace emails.',
            order: 1,
            completedActivities: 0,
            totalActivities: 3,
          },
        },
      }),
    });
  });

  await page.route('**/api/learning-path', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        pathId: 'pppppppp-pppp-pppp-pppp-pppppppppppp',
        title: 'Workplace English for Document Controller — B1',
        isActive: true,
        modulesCompleted: 0,
        totalModules: 5,
        modules: [
          {
            moduleId: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
            title: 'Professional email writing',
            description: 'Practice writing clear, polite workplace emails.',
            order: 1,
            completedActivities: 0,
            totalActivities: 3,
            isCurrent: true,
          },
        ],
      }),
    });
  });

  await page.route('**/api/activity/next', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        activityId: ids.activityId,
        activityType: 'writingScenario',
        source: 'aiGenerated',
        title: 'Follow-up email for a pending document approval',
        difficulty: 'B1',
        situation: 'You submitted an important document to your project manager 5 working days ago.',
        learningGoal: 'Practice following up professionally without sounding pushy.',
        targetPhrases: ['I wanted to follow up on', 'Please let me know'],
        targetVocabulary: ['pending', 'approval'],
        exampleText: 'Dear Mr. Ahmadi,\n\nI hope you are well. I wanted to follow up on the document I submitted last week.\n\nBest regards,\nSara',
        commonMistakeToAvoid: "Avoid 'Why haven't you approved it yet?' — this sounds rude.",
        instructionInSourceLanguage: 'یک ایمیل حرفه‌ای برای پیگیری تأیید سند بنویسید.',
      }),
    });
  });

  await page.route(`**/api/activity/${ids.activityId}/attempt`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        attemptId: 'attempt-id-001',
        score: 78,
        correctedText: 'Dear Mr. Ahmadi,\n\nI hope you are well. I wanted to follow up on the document I submitted last week.\n\nBest regards,\nSara',
        whatYouDidWell: ['Good use of formal greeting', 'Clear structure'],
        mainMistakes: ['Missing comma after salutation'],
        grammarExplanation: 'Always place a comma after the salutation in formal emails.',
        toneExplanation: 'Your tone was professional throughout.',
        vocabularyToRemember: ['at your earliest convenience'],
        rewriteChallenge: "Rewrite the opening using 'I hope this email finds you well'.",
        nextPracticeSuggestion: 'Try writing an email to explain a delay.',
        feedbackInSourceLanguage: 'ایمیل شما خوب بود اما می‌توانید رسمی‌تر بنویسید.',
      }),
    });
  });
}

test('core first-user journey smoke test with mocked API', async ({ page }) => {
  await mockApi(page);

  // ── Landing ──────────────────────────────────────────────────────────────────
  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Practise the workplace message/i })).toBeVisible();

  // ── Admin login ───────────────────────────────────────────────────────────────
  await page.getByRole('link', { name: /Sign in to SpeakPath/i }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();

  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/admin/);

  // ── Admin creates student ─────────────────────────────────────────────────────
  await page.getByRole('link', { name: /Create student/i }).click();
  await page.getByLabel('Student email').fill('student@example.com');
  await page.getByLabel('Temporary password').fill('Student123');
  await page.getByRole('button', { name: 'Create student' }).click();
  await expect(page.getByRole('heading', { name: 'Share these credentials' })).toBeVisible();

  // ── Student first login + password change ─────────────────────────────────────
  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.getByLabel('Email').fill('student@example.com');
  await page.getByLabel('Password').fill('Student123');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Create your password' })).toBeVisible();
  await page.locator('input[name="current"]').fill('Student123');
  await page.locator('input[name="new"]').fill('Student1234');
  await page.locator('input[name="confirm"]').fill('Student1234');
  await page.getByRole('button', { name: 'Set password and continue' }).click();

  // ── Onboarding — 4 steps ──────────────────────────────────────────────────────
  await expect(page.getByRole('heading', { name: 'Choose your language path' })).toBeVisible();
  await page.getByRole('button', { name: /Persian to English/i }).click();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Choose your learning track' })).toBeVisible();
  await page.getByRole('button', { name: /Workplace English/i }).click();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Choose your career context' })).toBeVisible();
  await page.getByRole('button', { name: /Document Controller/i }).click();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: 'Choose your first skill focus' })).toBeVisible();
  await page.getByRole('button', { name: /Writing/i }).click();
  await page.getByRole('button', { name: 'Complete setup' }).click();

  // ── Dashboard — verify learning path card ─────────────────────────────────────
  await expect(page).toHaveURL(/\/dashboard/);

  // Learning path section: title visible
  await expect(page.getByText('Workplace English for Document Controller — B1')).toBeVisible();

  // ── Navigate to activity ──────────────────────────────────────────────────────
  await page.getByRole('link', { name: /Continue learning/i }).click();
  await expect(page).toHaveURL(/\/activity/);

  // Activity lesson — learning phase
  await expect(page.getByRole('heading', { name: /Follow-up email/i })).toBeVisible();

  // Persian instruction is rendered before writing starts
  await expect(page.getByText(/یک ایمیل حرفه‌ای/)).toBeVisible();

  // Start writing
  await page.getByRole('button', { name: 'Start writing' }).click();

  // Writing phase — textarea appears, submit button present
  await expect(page.getByLabel('Write your response')).toBeVisible();
  await page.getByLabel('Write your response').fill('Dear Mr. Ahmadi, I wanted to follow up on the pending approval.');

  await page.getByRole('button', { name: /Get.*feedback/i }).click();

  // ── Feedback phase ────────────────────────────────────────────────────────────
  await expect(page.getByText('Overall score')).toBeVisible();
  await expect(page.getByText('78')).toBeVisible();
  await expect(page.getByText('What you did well')).toBeVisible();
  await expect(page.getByText(/Feedback in Persian/i)).toBeVisible();
  await expect(page.getByText('ایمیل شما خوب بود')).toBeVisible();

  // Next activity button is present
  await expect(page.getByRole('button', { name: /Continue to next|Next activity/i })).toBeVisible();
});

import { expect, test, type Page } from '@playwright/test';

const ids = {
  languagePairId: '11111111-1111-1111-1111-111111111111',
  learningTrackId: '22222222-2222-2222-2222-222222222222',
  careerProfileId: '33333333-3333-3333-3333-333333333333',
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
        message: 'Your personalised plan is ready.',
      }),
    });
  });

  await page.route('**/api/writing/exercise', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        scenarioTitle: 'Follow-up email for a pending document approval',
        scenarioDescription: 'Ask a project manager to review a document submitted five working days ago.',
        instructionInSourceLanguage: 'Please write a professional follow-up email.',
        targetPhrases: ['could you please review', 'pending approval'],
        targetVocabulary: ['pending approval', 'latest revision'],
      }),
    });
  });

  await page.route('**/api/writing/exercise/submit', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        submissionId: 'submission-id',
        overallScore: 78,
        correctedEmail: 'Dear John,\n\nCould you please review the latest revision when you have a chance?\n\nBest regards,',
        feedbackInSourceLanguage: 'This is clear and polite.',
        grammarIssues: [],
        vocabularyIssues: [],
        toneIssues: ['Make the request slightly more polite.'],
        suggestedPhrases: ['could you please review'],
        mistakesToTrack: [],
      }),
    });
  });
}

test('core first-user journey smoke test with mocked API', async ({ page }) => {
  await mockApi(page);

  await page.goto('/');
  await expect(page.getByRole('heading', { name: /Practise the workplace message/i })).toBeVisible();

  await page.getByRole('link', { name: /Sign in to SpeakPath/i }).click();
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();

  await page.getByLabel('Email').fill('admin@example.com');
  await page.getByLabel('Password').fill('Admin@1234');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/admin/);

  await page.getByRole('link', { name: /Create student/i }).click();
  await page.getByLabel('Student email').fill('student@example.com');
  await page.getByLabel('Temporary password').fill('Student123');
  await page.getByRole('button', { name: 'Create student' }).click();
  await expect(page.getByRole('heading', { name: 'Share these credentials' })).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.getByLabel('Email').fill('student@example.com');
  await page.getByLabel('Password').fill('Student123');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Create your password' })).toBeVisible();
  await page.locator('input[name="current"]').fill('Student123');
  await page.locator('input[name="new"]').fill('Student1234');
  await page.locator('input[name="confirm"]').fill('Student1234');
  await page.getByRole('button', { name: 'Set password and continue' }).click();

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

  await expect(page).toHaveURL(/\/dashboard/);
  await expect(page.getByRole('heading', { name: /Welcome back/i })).toBeVisible();

  await page.getByRole('link', { name: 'Start writing exercise' }).click();
  await expect(page.getByRole('heading', { name: /Follow-up email/i })).toBeVisible();
  await page.getByLabel('Write your email draft').fill('Dear John, could you please review the latest revision?');
  await page.getByRole('button', { name: 'Get writing feedback' }).click();
  await expect(page.getByRole('heading', { name: 'Review your workplace message' })).toBeVisible();
  await expect(page.getByText('78')).toBeVisible();

  await page.reload();
  await expect(page).toHaveURL(/\/writing/);
  await expect(page.getByRole('heading', { name: /Follow-up email/i })).toBeVisible();
});

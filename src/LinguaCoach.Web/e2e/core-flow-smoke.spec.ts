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
  let attemptCount = 0;
  let moduleCompleted = false;
  let generatedNextModules = false;
  const attempts: Array<{
    attemptId: string;
    attemptNumber: number;
    score: number;
    submittedContent: string;
    coachSummary: string;
  }> = [];

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

  await page.route('**/api/admin/students*', async route => {
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

  await page.route('**/api/onboarding/experience', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true }),
    });
  });

  await page.route('**/api/onboarding', async route => {
    const body = route.request().postDataJSON() as { step: string };
    onboardingStep =
      body.step === 'language' ? 'Language' :
      body.step === 'preference' ? 'Preference' :
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
        lifecycleStage: 'CourseReady',
        learningPath: {
          pathId: 'pppppppp-pppp-pppp-pppp-pppppppppppp',
          title: 'Workplace English for Document Controller — B1',
          modulesCompleted: moduleCompleted ? 1 : 0,
          totalModules: generatedNextModules ? 2 : 1,
          currentModule: {
            moduleId: generatedNextModules ? 'nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn' : 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
            title: generatedNextModules ? 'Concise progress updates' : 'Professional workplace communication',
            description: generatedNextModules ? 'Practice short status updates with clear next steps.' : 'Practice clear, polite workplace communication.',
            order: generatedNextModules ? 2 : 1,
            completedActivities: generatedNextModules ? 0 : Math.min(attemptCount, 3),
            totalActivities: 3,
          },
        },
      }),
    });
  });

  await page.route('**/api/placement/status', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'Completed',
        currentSectionKey: 'self_check',
        currentSectionOrder: 1,
        totalSections: 6,
        lifecycleStage: 'CourseReady',
        isCompleted: true,
      }),
    });
  });

  await page.route('**/api/placement/result', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        estimatedOverallLevel: 'B1',
        skillLevels: [{ skill: 'Writing', level: 'B1' }],
        strengths: ['clear workplace context'],
        weaknesses: ['formal tone'],
        recommendedStartingCourse: 'Workplace English B1',
        recommendedSessionDuration: 15,
        placementNotes: 'Ready for guided workplace English.',
        isCompleted: true,
      }),
    });
  });

  await page.route('**/api/learning-path/memory', async route => {
    const hasAttempts = attemptCount > 0;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        journeySummary: hasAttempts
          ? 'You are improving at workplace follow-up emails. Your next focus is softer requests and shorter status updates.'
          : null,
        strongSkills: hasAttempts ? ['Clear workplace context', 'Professional greeting'] : [],
        weakSkills: hasAttempts ? ['Too direct tone', 'Long sentences'] : [],
        recurringMistakes: hasAttempts ? ['Needs softer request language'] : [],
        nextRecommendedFocus: hasAttempts ? ['Softening requests', 'Concise progress updates'] : [],
        coveredScenarioCount: hasAttempts ? 1 : 0,
        skillProfile: hasAttempts
          ? [
              { skillKey: 'softening_language', skillLabel: 'Softening language', isWeak: true },
              { skillKey: 'message_structure', skillLabel: 'Message structure', isWeak: false },
            ]
          : [],
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
        modulesCompleted: moduleCompleted ? 1 : 0,
        totalModules: generatedNextModules ? 2 : 1,
        modules: [
          {
            moduleId: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
            title: 'Professional workplace communication',
            description: 'Practice clear, polite workplace communication.',
            order: 1,
            completedActivities: moduleCompleted ? 3 : attemptCount >= 2 ? 2 : attemptCount,
            totalActivities: 3,
            isCurrent: !moduleCompleted,
            isCompleted: moduleCompleted,
            isReadyToComplete: attemptCount >= 2 && !moduleCompleted,
            averageScore: attemptCount >= 2 ? 82 : attemptCount === 1 ? 78 : null,
            latestScore: attemptCount >= 2 ? 86 : attemptCount === 1 ? 78 : null,
          },
          ...(generatedNextModules ? [{
            moduleId: 'nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn',
            title: 'Concise progress updates',
            description: 'Practice short status updates with clear next steps.',
            order: 2,
            completedActivities: 0,
            totalActivities: 3,
            isCurrent: true,
            isCompleted: false,
            isReadyToComplete: false,
            averageScore: null,
            latestScore: null,
            focusSkill: 'concise_writing',
            reason: 'Recommended because your recent attempts used long sentences.',
            difficulty: 'B1+',
          }] : []),
        ],
      }),
    });
  });

  await page.route('**/api/learning-path/modules/mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm/complete', async route => {
    moduleCompleted = true;
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  await page.route('**/api/learning-path/modules/mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm/activities', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        moduleId: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
        title: 'Professional workplace communication',
        description: 'Practice clear, polite workplace communication.',
        completedActivities: attemptCount >= 2 ? 3 : attemptCount,
        totalRequired: 3,
        averageScore: attemptCount >= 2 ? 82 : attemptCount === 1 ? 78 : null,
        latestScore: attemptCount >= 2 ? 86 : attemptCount === 1 ? 78 : null,
        isReadyToComplete: attemptCount >= 2,
        isCompleted: moduleCompleted,
        activities: attempts.length ? [{
          activityId: ids.activityId,
          title: 'Follow-up email for a pending document approval',
          activityType: 'writingScenario',
          attemptCount: attempts.length,
          bestScore: Math.max(...attempts.map(a => a.score)),
          latestScore: attempts[attempts.length - 1].score,
          latestAttemptAt: '2026-06-07T02:00:00Z',
          hasFeedback: true,
        }] : [],
      }),
    });
  });

  await page.route('**/api/learning-path/generate-next', async route => {
    generatedNextModules = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        pathId: 'pppppppp-pppp-pppp-pppp-pppppppppppp',
        title: 'Workplace English for Document Controller - B1',
        isActive: true,
        modulesCompleted: 1,
        totalModules: 2,
        currentModule: {
          moduleId: 'nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn',
          title: 'Concise progress updates',
          description: 'Practice short status updates with clear next steps.',
          order: 2,
          completedActivities: 0,
          totalActivities: 3,
          isCurrent: true,
          isCompleted: false,
          isReadyToComplete: false,
          averageScore: null,
          latestScore: null,
          focusSkill: 'concise_writing',
          reason: 'Recommended because your recent attempts used long sentences.',
          difficulty: 'B1+',
        },
        modules: [
          {
            moduleId: 'mmmmmmmm-mmmm-mmmm-mmmm-mmmmmmmmmmmm',
            title: 'Professional workplace communication',
            description: 'Practice clear, polite workplace communication.',
            order: 1,
            completedActivities: 3,
            totalActivities: 3,
            isCurrent: false,
            isCompleted: true,
            isReadyToComplete: false,
            averageScore: 82,
            latestScore: 86,
          },
          {
            moduleId: 'nnnnnnnn-nnnn-nnnn-nnnn-nnnnnnnnnnnn',
            title: 'Concise progress updates',
            description: 'Practice short status updates with clear next steps.',
            order: 2,
            completedActivities: 0,
            totalActivities: 3,
            isCurrent: true,
            isCompleted: false,
            isReadyToComplete: false,
            averageScore: null,
            latestScore: null,
            focusSkill: 'concise_writing',
            reason: 'Recommended because your recent attempts used long sentences.',
            difficulty: 'B1+',
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
    const requestBody = route.request().postDataJSON() as { submittedContent: string };
    attemptCount += 1;
    const score = attemptCount === 1 ? 78 : 86;
    const coachSummary = attemptCount === 1
      ? 'Good effort - your message is clear but the tone needs polishing.'
      : 'This is clearer and more polite. The request now sounds professional.';
    attempts.push({
      attemptId: `attempt-id-00${attemptCount}`,
      attemptNumber: attemptCount,
      score,
      submittedContent: requestBody.submittedContent,
      coachSummary,
    });

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        attemptId: `attempt-id-00${attemptCount}`,
        score,
        coachSummary: 'Good effort — your message is clear but the tone needs polishing.',
        focusFirst: false,
        changes: [
          {
            type: 'replace',
            original: attemptCount === 1 ? 'please send' : 'can you approve',
            suggested: 'Could you please review',
            reason: 'Modal verbs make requests more polite.',
            category: 'tone',
            severity: 'high',
          },
        ],
        correctedText: 'Dear Mr. Ahmadi,\n\nI hope you are well. I wanted to follow up on the document I submitted last week.\n\nBest regards,\nSara',
        whatYouDidWell: ['Good use of formal greeting', 'Clear structure'],
        mainMistakes: ['Missing comma after salutation'],
        grammarIssues: [],
        vocabularyIssues: [],
        toneIssues: [],
        clarityIssues: [],
        grammarExplanation: 'Always place a comma after the salutation in formal emails.',
        toneExplanation: 'Your tone was professional throughout.',
        vocabularyToRemember: ['at your earliest convenience'],
        miniLesson: 'Use modal verbs like could and would to make requests polite.',
        nextImprovementStep: "Try rewriting your request sentence using 'Could you please...'",
        rewriteChallenge: "Rewrite the opening using 'I hope this email finds you well'.",
        nextPracticeSuggestion: 'Practise explaining a delay clearly and professionally.',
        feedbackInSourceLanguage: 'ایمیل شما خوب بود اما می‌توانید رسمی‌تر بنویسید.',
      }),
    });
  });

  await page.route(`**/api/activity/${ids.activityId}/attempts`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        activityId: ids.activityId,
        title: 'Follow-up email for a pending document approval',
        activityType: 'writingScenario',
        situation: 'You submitted an important document to your project manager 5 working days ago.',
        learningGoal: 'Practice following up professionally without sounding pushy.',
        targetPhrases: ['I wanted to follow up on', 'Please let me know'],
        attempts: attempts.map(a => ({
          attemptId: a.attemptId,
          attemptNumber: a.attemptNumber,
          submittedAt: '2026-06-07T02:00:00Z',
          score: a.score,
          coachSummary: a.coachSummary,
          focusFirst: false,
          changes: [{
            type: 'replace',
            original: a.attemptNumber === 1 ? 'please send' : 'can you approve',
            suggested: 'Could you please review',
            reason: 'A softer request keeps the message professional.',
            category: 'tone',
            severity: 'high',
          }],
          whatYouDidWell: ['Clear workplace context'],
          grammarIssues: [],
          vocabularyIssues: [],
          toneIssues: ['Request can be softened'],
          clarityIssues: [],
          miniLesson: 'Use modal verbs like could and would to soften workplace requests.',
          nextImprovementStep: 'Keep the follow-up short and add a clear next step.',
          suggestedImprovedVersion: 'Dear Mr. Ahmadi,\n\nI wanted to follow up on the pending approval. Could you please review it when you have a chance?\n\nBest regards,\nSara',
          nativeLanguageExplanation: 'Ø§ÛŒÙ…ÛŒÙ„ Ø´Ù…Ø§ Ø®ÙˆØ¨ Ø¨ÙˆØ¯ Ø§Ù…Ø§ Ù…ÛŒâ€ŒØªÙˆØ§Ù†ÛŒØ¯ Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ø±Ø§ Ù†Ø±Ù…â€ŒØªØ± Ø¨Ù†ÙˆÛŒØ³ÛŒØ¯.',
          submittedContent: a.submittedContent,
        })),
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
  await page.getByRole('link', { name: 'Students', exact: true }).click();
  await page.getByRole('main').getByRole('link', { name: /Create student/i }).click();
  await page.getByLabel('Student email').fill('student@example.com');
  await page.getByLabel('Temporary password').fill('Student123');
  await page.getByRole('button', { name: 'Create student' }).click();
  await expect(page).toHaveURL(/\/admin\/students/);
  await expect(page.getByText('Student created successfully')).toBeVisible();

  // ── Student first login + password change ─────────────────────────────────────
  await page.evaluate(() => sessionStorage.removeItem('speakpath.auth'));
  await page.goto('/login');
  await page.getByLabel('Email').fill('student@example.com');
  await page.getByLabel('Password').fill('Student123');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Create your password' })).toBeVisible();
  await page.locator('input[name="current"]').fill('Student123');
  await page.locator('input[name="new"]').fill('Student1234');
  await page.locator('input[name="confirm"]').fill('Student1234');
  await page.getByRole('button', { name: 'Set password and continue' }).click();

  // ── Onboarding — 5 steps ──────────────────────────────────────────────────────
  await expect(page.getByRole('heading', { name: 'Choose your language path' })).toBeVisible();
  await page.getByRole('button', { name: /Persian to English/i }).click();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: /How much time/i })).toBeVisible();
  await page.getByRole('button', { name: /15 minutes/i }).click();
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: /job, field, or target workplace context/i })).toBeVisible();
  await page.getByLabel('Your job, field, or workplace context').fill('Junior Software Engineer');
  await page.getByRole('button', { name: 'Continue' }).click();

  await expect(page.getByRole('heading', { name: /Why do you want to improve your English/i })).toBeVisible();
  await page.getByRole('button', { name: /Listening/i }).click();
  await page.getByRole('textbox').fill('میخوام بتونم ایمیل رسمی بنویسم');
  await page.getByRole('button', { name: 'Next' }).click();

  // ── Step 5 — experience (new) ────────────────────────────────────────────────
  await expect(page.getByRole('heading', { name: /Tell us about your work experience/i })).toBeVisible();
  await page.getByRole('button', { name: 'Continue to assessment' }).click();

  // ── Dashboard — verify Today page loaded ─────────────────────────────────────
  await expect(page).toHaveURL(/\/placement/);
  await page.goto('/dashboard');

  // Today page heading visible
  await expect(page.getByTestId('today-page')).toBeVisible();

  // ── Navigate to activity ──────────────────────────────────────────────────────
  await page.goto('/activity');
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
  await expect(page.getByText('78')).toBeVisible();
  await expect(page.getByText(/what you did well/i)).toBeVisible();

  // Change list (diff) should be visible
  await expect(page.getByText(/Suggested changes/i)).toBeVisible();

  // Persian explanation is hidden by default — toggle button should be visible
  await expect(page.getByRole('button', { name: /Show Persian explanation/i })).toBeVisible();
  // Click to reveal
  await page.getByRole('button', { name: /Show Persian explanation/i }).click();
  await expect(page.getByText('ایمیل شما خوب بود')).toBeVisible();

  // Action buttons
  await expect(page.getByRole('button', { name: /Improve my answer/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Next activity/i })).toBeVisible();

  // Retry/improve loop
  await page.getByRole('button', { name: /Improve my answer/i }).click();
  await expect(page.getByText(/Attempt 2/i)).toBeVisible();
  await page.getByLabel('Write your response').fill('Dear Mr. Ahmadi, I wanted to follow up on the pending approval. Could you please review it when you have a chance?');
  await page.getByRole('button', { name: /Get.*feedback/i }).click();
  await expect(page.getByText('86')).toBeVisible();
  await expect(page.getByText(/\+8/)).toBeVisible();

  // Attempt history shows both attempts and keeps native-language support hidden by default.
  await page.goto(`/activity/${ids.activityId}/history`);
  await expect(page.getByText('Attempt history')).toBeVisible();
  await expect(page.getByRole('button', { name: /Attempt 1/i })).toBeVisible();
  await expect(page.getByRole('button', { name: /Attempt 2/i })).toBeVisible();
  await expect(page.getByText('+8 from attempt 1')).toBeVisible();
  await expect(page.getByRole('button', { name: /Show Persian explanation/i })).toBeVisible();

  // Learning memory and module readiness update on My Path.
  await page.goto('/my-path');
  await expect(page.getByText('You are improving at workplace follow-up emails')).toBeVisible();
  await expect(page.getByText('Softening requests')).toBeVisible();
  await expect(page.getByText('This module is ready to complete')).toBeVisible();
  await page.getByRole('button', { name: /Complete this module/i }).click();
  await expect(page.getByText(/Module completed/i)).toBeVisible();

  // Continue path adds a non-duplicate adaptive module with reason/focus/difficulty.
  await page.getByRole('button', { name: /Continue my learning path/i }).click();
  await expect(page.getByText('New recommended modules have been added')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Concise progress updates' })).toBeVisible();
  await expect(page.getByText('Recommended because your recent attempts used long sentences.')).toBeVisible();
  await expect(page.getByText('Focus: Concise Writing')).toBeVisible();
  await expect(page.getByText('Level: B1+')).toBeVisible();

  // Dashboard focus summary reflects memory after attempts and no raw JSON leaks.
  await page.goto('/dashboard');
  await expect(page.getByText('Your current focus is Softening requests')).toBeVisible();
  await expect(page.locator('body')).not.toContainText('{');
});

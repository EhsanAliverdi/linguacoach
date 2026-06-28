import { type Page } from '@playwright/test';

export type DashboardState = 'CourseReady' | 'ActiveLearning' | 'PlacementRequired' | 'Preparing';

export function buildDashboardSummary(state: DashboardState = 'CourseReady') {
  const base = {
    profile: { displayName: 'Test Student', cefrLevel: 'B1', supportLanguage: null },
    courseReadiness: {
      isLearningReady: true,
      lifecycleStatus: 'CourseReady',
      placementRequired: false,
      learningPlanExists: true,
    },
    todaySession: {
      status: 'Ready',
      sessionId: 'session-1',
      title: "Today's Lesson",
      topic: 'Workplace communication',
      sessionGoal: 'Practice professional emails',
      focusSkill: 'writing',
      durationMinutes: 30,
      exerciseCount: 5,
      actionLabel: "Start today's lesson",
    },
    learningPlan: {
      pathTitle: 'Workplace English B1',
      currentObjective: 'Writing professional emails',
      currentObjectiveDescription: null,
      objectiveIndex: 1,
      totalObjectives: 5,
      modulesCompleted: 1,
      remainingObjectives: 4,
      completedActivities: 2,
      totalActivities: 10,
      progressPercent: 20,
    },
    practice: { status: 'Ready', suggestedItem: null, reviewQueueCount: 0, weakestSkill: null },
    progress: {
      skillProfile: [],
      strongSkills: [],
      weakSkills: [],
      nextRecommendedFocus: [],
      journeySummary: null,
      activitiesCompleted: 0,
      streakDays: 0,
    },
    quickStats: { currentCefr: 'B1', streakDays: 0, activitiesCompleted: 0, reviewQueueCount: 0 },
    warnings: {
      missingLearningPlan: false,
      missingTodaySession: false,
      practiceUnavailable: false,
      placementIncomplete: false,
    },
  };

  if (state === 'PlacementRequired') {
    return {
      ...base,
      courseReadiness: {
        isLearningReady: false,
        lifecycleStatus: 'PlacementRequired',
        placementRequired: true,
        learningPlanExists: false,
      },
      todaySession: { ...base.todaySession, status: 'NotAvailable', sessionId: null, title: null, topic: null },
      learningPlan: { ...base.learningPlan, totalObjectives: 0, currentObjective: null },
      warnings: { ...base.warnings, placementIncomplete: true },
    };
  }

  if (state === 'Preparing') {
    return {
      ...base,
      courseReadiness: {
        isLearningReady: true,
        lifecycleStatus: 'CourseReady',
        placementRequired: false,
        learningPlanExists: false,
      },
      todaySession: { ...base.todaySession, status: 'Preparing', sessionId: null, title: null, topic: null },
      warnings: { ...base.warnings, missingLearningPlan: true },
    };
  }

  if (state === 'ActiveLearning') {
    return {
      ...base,
      courseReadiness: {
        isLearningReady: true,
        lifecycleStatus: 'ActiveLearning',
        placementRequired: false,
        learningPlanExists: true,
      },
    };
  }

  return base;
}

export async function mockStudentDashboardSummary(page: Page, state: DashboardState = 'CourseReady') {
  await page.route('**/api/student/dashboard/summary', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(buildDashboardSummary(state)),
    });
  });
}

import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { placementRequiredRedirectGuard, placementAccessGuard } from './core/guards/placement.guard';
import { moduleRedirectGuard } from './core/guards/module-redirect.guard';
import { AuthService } from './core/services/auth.service';
import { PublicLayoutComponent } from './design-system/public/layouts/public-layout/public-layout.component';
import { StudentAppLayoutComponent } from './design-system/student/layouts/student-app-layout/student-app-layout.component';
import { OnboardingLayoutComponent } from './design-system/student/layouts/onboarding-layout/onboarding-layout.component';
import { AdminAppLayoutComponent } from './design-system/admin/layouts/admin-app-layout/admin-app-layout.component';

export const routes: Routes = [
  // ── Public (unauthenticated) ──────────────────────────────────────────
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/public/landing/landing.component').then(m => m.LandingComponent),
      },
      {
        path: 'login',
        loadComponent: () => import('./features/public/auth/login/login.component').then(m => m.LoginComponent),
      },
      {
        path: 'change-password',
        canActivate: [authGuard],
        loadComponent: () => import('./features/public/auth/change-password/change-password.component').then(m => m.ChangePasswordComponent),
      },
      {
        path: 'reset-password',
        loadComponent: () => import('./features/public/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent),
      },
    ],
  },

  // ── Admin ─────────────────────────────────────────────────────────────
  {
    path: 'admin',
    component: AdminAppLayoutComponent,
    canActivate: [adminGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./features/admin/admin-dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent),
      },
      {
        path: 'students',
        loadComponent: () => import('./features/admin/admin-students/admin-students.component').then(m => m.AdminStudentsComponent),
      },
      {
        path: 'students/:id',
        loadComponent: () => import('./features/admin/admin-student-detail/admin-student-detail.component').then(m => m.AdminStudentDetailComponent),
      },
      {
        path: 'create-student',
        loadComponent: () => import('./features/admin/create-student/create-student.component').then(m => m.CreateStudentComponent),
      },
      {
        path: 'ai-config',
        loadComponent: () => import('./features/admin/admin-ai-config/admin-ai-config.component').then(m => m.AdminAiConfigComponent),
      },
      {
        path: 'prompts',
        loadComponent: () => import('./features/admin/admin-prompts/admin-prompts.component').then(m => m.AdminPromptsComponent),
      },
      {
        path: 'exercise-types',
        loadComponent: () => import('./features/admin/admin-exercise-types/admin-exercise-types.component').then(m => m.AdminExerciseTypesComponent),
      },
      {
        path: 'usage',
        loadComponent: () => import('./features/admin/admin-ai-usage/admin-ai-usage.component').then(m => m.AdminAiUsageComponent),
      },
      {
        path: 'diagnostics',
        loadComponent: () => import('./features/admin/admin-diagnostics/admin-diagnostics.component').then(m => m.AdminDiagnosticsComponent),
      },
      {
        path: 'ai-operations',
        loadComponent: () => import('./features/admin/admin-ai-operations/admin-ai-operations.component').then(m => m.AdminAiOperationsComponent),
      },
      {
        path: 'settings/feature-gates',
        loadComponent: () => import('./features/admin/admin-feature-gates/admin-feature-gates.component').then(m => m.AdminFeatureGatesComponent),
      },
      {
        path: 'integrations',
        loadComponent: () => import('./features/admin/admin-integrations/admin-integrations.component').then(m => m.AdminIntegrationsComponent),
      },
      {
        path: 'curriculum',
        loadComponent: () => import('./features/admin/admin-curriculum/admin-curriculum.component').then(m => m.AdminCurriculumComponent),
      },
      {
        path: 'skill-graph',
        loadComponent: () => import('./features/admin/admin-skill-graph/admin-skill-graph.component').then(m => m.AdminSkillGraphComponent),
      },
      {
        path: 'usage-policies',
        loadComponent: () => import('./features/admin/admin-usage-policies/admin-usage-policies.component').then(m => m.AdminUsagePoliciesComponent),
      },
      {
        path: 'lessons',
        loadComponent: () => import('./features/admin/admin-delivery-health/admin-delivery-health.component').then(m => m.AdminDeliveryHealthComponent),
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/admin/admin-notifications/admin-notifications.component').then(m => m.AdminNotificationsComponent),
      },
      {
        path: 'security',
        loadComponent: () => import('./features/admin/admin-security/admin-security.component').then(m => m.AdminSecurityComponent),
      },
      {
        path: 'onboarding',
        loadComponent: () => import('./features/admin/admin-onboarding/admin-onboarding.component').then(m => m.AdminOnboardingComponent),
      },
      {
        path: 'onboarding/:templateId',
        loadComponent: () => import('./features/admin/admin-onboarding-editor/admin-onboarding-editor.component').then(m => m.AdminOnboardingEditorComponent),
      },
      {
        path: 'placement-items',
        loadComponent: () => import('./features/admin/admin-placement-items/admin-placement-items.component').then(m => m.AdminPlacementItemsComponent),
      },
      {
        path: 'placement-items/:itemId',
        loadComponent: () => import('./features/admin/admin-placement-item-editor/admin-placement-item-editor.component').then(m => m.AdminPlacementItemEditorComponent),
      },
      // Phase I2A — the legacy ActivityTemplate Form.io-pilot admin pages were removed; old
      // bookmarks/links redirect to H4's Exercise admin page instead (see
      // docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md).
      {
        path: 'activity-templates',
        redirectTo: () => '/admin/exercises',
      },
      {
        path: 'activity-templates/:templateId',
        redirectTo: () => '/admin/exercises',
      },
      // Phase I3 — Review Queue removed: it only ever covered PlacementItemDefinition review
      // (ActivityTemplate removed in I2A), which the standalone Placement Items page already
      // does with no unique capability lost. Old bookmarks redirect there.
      {
        path: 'review-queue',
        redirectTo: () => '/admin/placement-items',
      },
      {
        path: 'content/import',
        loadComponent: () => import('./features/admin/admin-content-import/admin-content-import.component').then(m => m.AdminContentImportComponent),
      },
      // Phase J4B (follow-up) — candidates for a selected import run get their own page instead of
      // expanding inline below the runs table on content/import.
      {
        path: 'content/import/runs/:runId',
        loadComponent: () => import('./features/admin/admin-import-run-candidates/admin-import-run-candidates.component').then(m => m.AdminImportRunCandidatesComponent),
      },
      // Mandatory Import Execution Plan addendum (2026-07-15) — every large-scale ZIP package
      // upload lands here for review/approval before any AI/STT/TTS/background processing begins.
      {
        path: 'content/import/packages/:packageId/plan',
        loadComponent: () => import('./features/admin/admin-import-package-plan/admin-import-package-plan.component').then(m => m.AdminImportPackagePlanComponent),
      },
      // Phase I1 — the standalone import/review/publish pages were merged into one unified
      // pipeline page at content/import; old bookmarks/links redirect there (see AGENTS.md I1).
      {
        path: 'resource-sources',
        redirectTo: () => '/admin/content/import',
      },
      {
        path: 'resource-import-runs',
        redirectTo: () => '/admin/content/import',
      },
      {
        path: 'resource-candidates',
        redirectTo: () => '/admin/content/import',
      },
      {
        path: 'resource-bank',
        loadComponent: () => import('./features/admin/admin-resource-bank-unified/admin-resource-bank-unified.component').then(m => m.AdminResourceBankUnifiedComponent),
      },
      {
        path: 'resource-bank/:id/edit',
        loadComponent: () => import('./features/admin/admin-resource-bank-edit/admin-resource-bank-edit.component').then(m => m.AdminResourceBankEditComponent),
      },
      {
        path: 'resource-bank/:id',
        loadComponent: () => import('./features/admin/admin-resource-bank-detail/admin-resource-bank-detail.component').then(m => m.AdminResourceBankDetailComponent),
      },
      {
        path: 'lesson-library',
        loadComponent: () => import('./features/admin/admin-lessons/admin-lessons.component').then(m => m.AdminLessonsComponent),
      },
      {
        path: 'lesson-library/:id/edit',
        loadComponent: () => import('./features/admin/admin-lesson-edit/admin-lesson-edit.component').then(m => m.AdminLessonEditComponent),
      },
      {
        path: 'lesson-library/:id',
        loadComponent: () => import('./features/admin/admin-lesson-detail/admin-lesson-detail.component').then(m => m.AdminLessonDetailComponent),
      },
      {
        path: 'exercises',
        loadComponent: () => import('./features/admin/admin-exercises/admin-exercises.component').then(m => m.AdminExercisesComponent),
      },
      {
        path: 'exercises/:id/edit',
        loadComponent: () => import('./features/admin/admin-exercise-edit/admin-exercise-edit.component').then(m => m.AdminExerciseEditComponent),
      },
      {
        path: 'exercises/:id',
        loadComponent: () => import('./features/admin/admin-exercise-detail/admin-exercise-detail.component').then(m => m.AdminExerciseDetailComponent),
      },
      {
        path: 'modules',
        loadComponent: () => import('./features/admin/admin-modules/admin-modules.component').then(m => m.AdminModulesComponent),
      },
      {
        path: 'modules/:id/edit',
        loadComponent: () => import('./features/admin/admin-module-edit/admin-module-edit.component').then(m => m.AdminModuleEditComponent),
      },
      {
        path: 'modules/:id',
        loadComponent: () => import('./features/admin/admin-module-detail/admin-module-detail.component').then(m => m.AdminModuleDetailComponent),
      },
      // Phase I4 Pass 2 — Learn Items/Activities renamed to Lessons/Exercises; the Lessons page
      // moved off /admin/lessons (now Today Delivery Health, see above) to /admin/lesson-library
      // to resolve the route-name collision. Old bookmarks/links redirect there.
      {
        path: 'learn-items',
        redirectTo: () => '/admin/lesson-library',
      },
      {
        path: 'activities',
        redirectTo: () => '/admin/exercises',
      },
      // Phase H9A — the typed resource-bank pages were removed; old bookmarks/links redirect to
      // the unified Resource Bank with a matching type filter (see admin-resource-bank-unified).
      {
        path: 'resource-banks/vocabulary',
        redirectTo: () => '/admin/resource-bank?type=vocabulary',
      },
      {
        path: 'resource-banks/grammar',
        redirectTo: () => '/admin/resource-bank?type=grammar',
      },
      {
        path: 'resource-banks/reading-references',
        redirectTo: () => '/admin/resource-bank?type=readingReference',
      },
      {
        path: 'resource-banks/reading-passages',
        redirectTo: () => '/admin/resource-bank?type=readingPassage',
      },
    ],
  },

  // ── Onboarding (student, authenticated) ──────────────────────────────
  {
    path: 'onboarding',
    component: OnboardingLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'resume', pathMatch: 'full' },
      { path: 'resume', loadComponent: () => import('./features/student/onboarding/onboarding-resume/onboarding-resume.component').then(m => m.OnboardingResumeComponent) },
      { path: 'v2', loadComponent: () => import('./features/student/onboarding/onboarding-v2/onboarding-v2.component').then(m => m.OnboardingV2Component) },
    ],
  },

  // ── Placement (authenticated, not yet completed) ──────────────────────
  {
    path: 'placement',
    component: OnboardingLayoutComponent,
    canActivate: [authGuard, placementAccessGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./features/student/placement/placement-cards/placement-cards.component').then(m => m.PlacementCardsComponent),
      },
      {
        path: ':skill',
        loadComponent: () => import('./features/student/placement/placement.component').then(m => m.PlacementComponent),
      },
    ],
  },

  // ── Student app (authenticated) ───────────────────────────────────────
  {
    path: '',
    component: StudentAppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/dashboard/dashboard/dashboard.component').then(m => m.DashboardComponent),
      },
      {
        path: 'my-path',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/journey/journey.component').then(m => m.JourneyComponent),
      },
      {
        path: 'journey',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/journey/journey.component').then(m => m.JourneyComponent),
      },
      {
        path: 'practice',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/practice/practice-gym.component').then(m => m.PracticeGymComponent),
      },
      {
        path: 'module/:moduleRunId',
        canActivate: [placementRequiredRedirectGuard, moduleRedirectGuard],
        children: [],
      },
      {
        path: 'activity',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/activity/activity-lesson/activity-lesson.component').then(m => m.ActivityLessonComponent),
      },
      {
        path: 'activity/:activityId/history',
        loadComponent: () => import('./features/student/activity/activity-history/activity-history.component').then(m => m.ActivityHistoryComponent),
      },
      {
        path: 'assessment',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/assessment/cefr-assessment/cefr-assessment.component').then(m => m.CefrAssessmentComponent),
      },
      {
        path: 'speaking',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/speaking/speaking-session/speaking-session.component').then(m => m.SpeakingSessionComponent),
      },
      {
        path: 'progress',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/progress/progress.component').then(m => m.ProgressComponent),
      },
      {
        path: 'vocabulary',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/vocabulary/vocabulary.component').then(m => m.VocabularyComponent),
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/student/profile/profile.component').then(m => m.ProfileComponent),
      },
      // Phase I2B — Today is module-only now; the legacy per-exercise lesson-runner page was
      // removed (nothing creates new LearningSession/SessionExercise rows anymore). Old
      // bookmarks/links redirect to the dashboard instead (see
      // docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md).
      {
        path: 'lesson/:sessionId',
        redirectTo: () => '/dashboard',
      },
    ],
  },

  { path: '**', redirectTo: () => inject(AuthService).isAuthenticated() ? '/dashboard' : '/login' },
];

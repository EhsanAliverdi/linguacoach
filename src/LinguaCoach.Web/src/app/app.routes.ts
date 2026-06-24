import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { placementRequiredRedirectGuard, placementAccessGuard } from './core/guards/placement.guard';
import { moduleRedirectGuard } from './core/guards/module-redirect.guard';
import { AuthService } from './core/services/auth.service';
import { PublicLayoutComponent } from './design-system/public/layouts/public-layout/public-layout.component';
import { StudentAppLayoutComponent } from './design-system/student/layouts/student-app-layout/student-app-layout.component';
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
        path: 'students/new',
        redirectTo: 'create-student',
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
        path: 'careers',
        redirectTo: 'curriculum',
      },
      {
        path: 'usage',
        loadComponent: () => import('./features/admin/admin-ai-usage/admin-ai-usage.component').then(m => m.AdminAiUsageComponent),
      },
      {
        path: 'ai-usage',
        redirectTo: 'usage',
      },
      {
        path: 'diagnostics',
        loadComponent: () => import('./features/admin/admin-diagnostics/admin-diagnostics.component').then(m => m.AdminDiagnosticsComponent),
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
        path: 'usage-policies',
        loadComponent: () => import('./features/admin/admin-usage-policies/admin-usage-policies.component').then(m => m.AdminUsagePoliciesComponent),
      },
      {
        path: 'lessons',
        loadComponent: () => import('./features/admin/admin-lessons/admin-lessons.component').then(m => m.AdminLessonsComponent),
      },
      {
        path: 'usage-analytics',
        loadComponent: () => import('./features/admin/admin-usage-analytics/admin-usage-analytics.component').then(m => m.AdminUsageAnalyticsComponent),
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/admin/admin-notifications/admin-notifications.component').then(m => m.AdminNotificationsComponent),
      },
      {
        path: 'security',
        loadComponent: () => import('./features/admin/admin-security/admin-security.component').then(m => m.AdminSecurityComponent),
      },
    ],
  },

  // ── Onboarding (student, authenticated) ──────────────────────────────
  {
    path: 'onboarding',
    component: StudentAppLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'resume', pathMatch: 'full' },
      { path: 'resume', loadComponent: () => import('./features/student/onboarding/onboarding-resume/onboarding-resume.component').then(m => m.OnboardingResumeComponent) },
      { path: 'step-1', loadComponent: () => import('./features/student/onboarding/step1-language/step1-language.component').then(m => m.Step1LanguageComponent) },
      { path: 'step-2', loadComponent: () => import('./features/student/onboarding/step2-track/step2-track.component').then(m => m.Step2TrackComponent) },
      { path: 'step-3', loadComponent: () => import('./features/student/onboarding/step3-career/step3-career.component').then(m => m.Step3CareerComponent) },
      { path: 'step-4', loadComponent: () => import('./features/student/onboarding/step4-skill/step4-skill.component').then(m => m.Step4SkillComponent) },
      { path: 'step-5', loadComponent: () => import('./features/student/onboarding/step5-experience/step5-experience.component').then(m => m.Step5ExperienceComponent) },
      { path: 'v2', loadComponent: () => import('./features/student/onboarding/onboarding-v2/onboarding-v2.component').then(m => m.OnboardingV2Component) },
    ],
  },

  // ── Placement (authenticated, not yet completed) ──────────────────────
  {
    path: 'placement',
    component: StudentAppLayoutComponent,
    canActivate: [authGuard, placementAccessGuard],
    children: [
      {
        path: '',
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
        loadComponent: () => import('./features/student/dashboard/dashboard/dashboard.component').then(m => m.DashboardComponent),
      },
      {
        path: 'my-path',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/learning-path/learning-path.component').then(m => m.LearningPathComponent),
      },
      {
        path: 'journey',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/learning-path/learning-path.component').then(m => m.LearningPathComponent),
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
        loadComponent: () => import('./features/student/assessment/cefr-assessment/cefr-assessment.component').then(m => m.CefrAssessmentComponent),
      },
      {
        path: 'speaking',
        loadComponent: () => import('./features/student/speaking/speaking-session/speaking-session.component').then(m => m.SpeakingSessionComponent),
      },
      {
        path: 'progress',
        loadComponent: () => import('./features/student/progress/progress.component').then(m => m.ProgressComponent),
      },
      {
        path: 'vocabulary',
        loadComponent: () => import('./features/student/vocabulary/vocabulary.component').then(m => m.VocabularyComponent),
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/student/profile/profile.component').then(m => m.ProfileComponent),
      },
      {
        path: 'lesson/:sessionId',
        canActivate: [placementRequiredRedirectGuard],
        loadComponent: () => import('./features/student/lesson/lesson.component').then(m => m.LessonComponent),
      },
    ],
  },

  { path: '**', redirectTo: () => inject(AuthService).isAuthenticated() ? '/dashboard' : '/login' },
];

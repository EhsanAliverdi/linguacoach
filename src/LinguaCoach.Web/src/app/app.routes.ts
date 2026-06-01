import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },

  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadComponent: () => import('./features/auth/change-password/change-password.component').then(m => m.ChangePasswordComponent),
  },

  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/create-student/create-student.component').then(m => m.CreateStudentComponent),
  },

  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () => import('./features/onboarding/onboarding-shell/onboarding-shell.component').then(m => m.OnboardingShellComponent),
    children: [
      { path: '', redirectTo: 'resume', pathMatch: 'full' },
      { path: 'resume', loadComponent: () => import('./features/onboarding/onboarding-resume/onboarding-resume.component').then(m => m.OnboardingResumeComponent) },
      { path: 'step-1', loadComponent: () => import('./features/onboarding/step1-language/step1-language.component').then(m => m.Step1LanguageComponent) },
      { path: 'step-2', loadComponent: () => import('./features/onboarding/step2-track/step2-track.component').then(m => m.Step2TrackComponent) },
      { path: 'step-3', loadComponent: () => import('./features/onboarding/step3-career/step3-career.component').then(m => m.Step3CareerComponent) },
      { path: 'step-4', loadComponent: () => import('./features/onboarding/step4-skill/step4-skill.component').then(m => m.Step4SkillComponent) },
    ],
  },

  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard/dashboard.component').then(m => m.DashboardComponent),
  },

  { path: '**', redirectTo: 'login' },
];

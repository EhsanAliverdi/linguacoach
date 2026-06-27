import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { PlacementService } from '../services/placement.service';

/**
 * Guards the main student app (dashboard, activities, etc.).
 * If the student still needs to complete placement, redirect them to /placement.
 * Lifecycle stages that require placement: PlacementRequired, PlacementInProgress.
 * If the status check fails (e.g. onboarding not finished), allow through so other
 * guards / pages handle it.
 */
export const placementRequiredRedirectGuard: CanActivateFn = () => {
  const placement = inject(PlacementService);
  const router = inject(Router);

  return placement.getStatus().pipe(
    map(status => {
      const stage = status.lifecycleStage;
      if (stage === 'PlacementRequired' || stage === 'PlacementInProgress' || status.status === 'InProgress') {
        return router.createUrlTree(['/placement']);
      }
      return true;
    }),
    catchError(() => of(true)),
  );
};

/**
 * Guards the /placement route.
 * Blocks pre-onboarding stages and redirects completed students to their dashboard.
 * Students who have passed placement cannot re-enter /placement (unless retake is enabled).
 */
export const placementAccessGuard: CanActivateFn = () => {
  const placement = inject(PlacementService);
  const router = inject(Router);

  return placement.getStatus().pipe(
    map(status => {
      const stage = status.lifecycleStage;

      // Block pre-onboarding — redirect to onboarding instead of /dashboard
      // to avoid a redirect loop with placementRequiredRedirectGuard.
      const blockedStages = ['Created', 'PasswordChangeRequired', 'OnboardingRequired', 'OnboardingInProgress'];
      if (blockedStages.includes(stage)) {
        return router.createUrlTree(['/onboarding/resume']);
      }

      // Redirect students who have already completed placement back to dashboard.
      // Phase 14A: PlacementConfig.AllowPlacementRetake would bypass this — enforced by the API.
      const completedStages = [
        'PlacementCompleted', 'CourseReady', 'InLesson', 'ActiveLearning', 'Paused', 'Archived',
      ];
      if (completedStages.includes(stage)) {
        return router.createUrlTree(['/dashboard']);
      }

      return true;
    }),
    catchError(() => of(true)),
  );
};

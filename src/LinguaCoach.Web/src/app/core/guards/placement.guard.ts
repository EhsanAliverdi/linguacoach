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
 * Guards the /placement route. If placement is already completed, send the student
 * to their dashboard instead of letting them retake it.
 */
export const placementAccessGuard: CanActivateFn = () => {
  const placement = inject(PlacementService);
  const router = inject(Router);

  return placement.getStatus().pipe(
    map(status => {
      // Block unauthenticated / pre-onboarding stages from hitting /placement
      const blockedStages = ['Created', 'PasswordChangeRequired', 'OnboardingRequired', 'OnboardingInProgress'];
      if (blockedStages.includes(status.lifecycleStage)) {
        return router.createUrlTree(['/dashboard']);
      }
      return true;
    }),
    catchError(() => of(true)),
  );
};

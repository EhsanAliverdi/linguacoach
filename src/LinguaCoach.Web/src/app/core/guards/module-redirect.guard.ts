import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { ActivityService } from '../services/activity.service';

/** Maps exact Practice Gym module keys to the canonical exerciseType route. */
const GYM_MODULES: Record<string, Record<string, string>> = {
  phrase_match: { exerciseType: 'phrase_match', returnTo: '/practice' },
  gap_fill_workplace_phrase: { exerciseType: 'gap_fill_workplace_phrase', returnTo: '/practice' },
  email_reply: { exerciseType: 'email_reply', returnTo: '/practice' },
  teams_chat_simulation: { exerciseType: 'teams_chat_simulation', returnTo: '/practice' },
};

/**
 * Resolves /module/:moduleRunId to the underlying /activity?... route.
 *
 * moduleRunId formats:
 *  - `gym-{key}` — a Practice Gym module, where `key` is one of GYM_MODULES.
 *
 * Phase I2B — the `session-{sessionId}-{exerciseId}` branch (a Today module backed by a
 * SessionExercise, routed through the now-deleted on-open activity-preparation endpoint) was
 * removed: Today is module-only now and nothing generates `moduleRunId`s in that shape anymore
 * (its only source was the deleted lesson-runner page). Any leftover/bookmarked `session-...`
 * link now falls through to the default `/dashboard` redirect below. See
 * docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
 */
export const moduleRedirectGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  const activityService = inject(ActivityService);
  const moduleRunId = route.paramMap.get('moduleRunId') ?? '';

  if (moduleRunId.startsWith('gym-')) {
    const key = moduleRunId.slice('gym-'.length);
    const params = GYM_MODULES[key];
    if (params) return router.createUrlTree(['/activity'], { queryParams: params });

    const skillKeys = new Set(['listening', 'speaking', 'writing', 'reading', 'vocabulary', 'grammar']);
    if (!skillKeys.has(key)) return router.parseUrl('/practice');

    return activityService.selectPracticeGymExerciseType(key).pipe(
      map(result => {
        const selected = result.selectedExerciseType;
        if (!result.hasSelection || !selected?.key) return router.parseUrl('/practice');
        return router.createUrlTree(['/activity'], {
          queryParams: { exerciseType: selected.key, returnTo: '/practice' },
        });
      }),
      catchError(() => of(router.parseUrl('/practice'))),
    );
  }

  return router.parseUrl('/dashboard');
};

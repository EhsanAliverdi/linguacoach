import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { ActivityService } from '../services/activity.service';
import { SessionService } from '../services/session.service';

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
 *  - `session-{sessionId}-{exerciseId}` — a Today module backed by a session exercise.
 *  - `gym-{key}` — a Practice Gym module, where `key` is one of GYM_MODULES.
 */
export const moduleRedirectGuard: CanActivateFn = (route) => {
  const router = inject(Router);
  const sessionService = inject(SessionService);
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

  if (moduleRunId.startsWith('session-')) {
    const rest = moduleRunId.slice('session-'.length);
    // UUIDs are 36 chars (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx). When both IDs
    // are UUIDs the combined string is 73 chars with the separator at index 36.
    // Using lastIndexOf('-') would incorrectly split inside the exerciseId UUID,
    // so we detect the UUID-pair format and split at the known boundary instead.
    const UUID_PAIR = /^([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})-([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;
    const uuidMatch = rest.match(UUID_PAIR);
    const sessionId = uuidMatch ? uuidMatch[1] : rest.slice(0, rest.lastIndexOf('-'));
    const exerciseId = uuidMatch ? uuidMatch[2] : rest.slice(rest.lastIndexOf('-') + 1);
    if (!sessionId || !exerciseId) return router.parseUrl('/dashboard');

    return sessionService.getById(sessionId).pipe(
      switchMap(session => {
        const exercise = session.exercises.find(e => e.exerciseId === exerciseId);
        if (!exercise) return of(router.parseUrl(`/lesson/${sessionId}`));

        if (exercise.learningActivityId) {
          return of(router.createUrlTree(['/activity'], {
            queryParams: { activityId: exercise.learningActivityId, returnTo: `/lesson/${sessionId}` },
          }));
        }

        if (exercise.kind === 'review') {
          return of(router.parseUrl(`/lesson/${sessionId}`));
        }

        return sessionService.prepareExercise(sessionId, exerciseId).pipe(
          map(result => router.createUrlTree(['/activity'], {
            queryParams: { activityId: result.activityId, returnTo: `/lesson/${sessionId}` },
          })),
        );
      }),
    );
  }

  return router.parseUrl('/dashboard');
};

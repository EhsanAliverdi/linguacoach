import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of, switchMap } from 'rxjs';
import { SessionService } from '../services/session.service';

/** Maps Practice Gym module keys to the canonical exerciseType route. */
const GYM_MODULES: Record<string, Record<string, string>> = {
  // Temporary skill-card mapping until dynamic skill choice lands.
  listening: { exerciseType: 'listen_and_answer', returnTo: '/practice' },
  speaking: { exerciseType: 'speaking_roleplay_turn', returnTo: '/practice' },
  writing: { exerciseType: 'open_writing_task', returnTo: '/practice' },
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
  const moduleRunId = route.paramMap.get('moduleRunId') ?? '';

  if (moduleRunId.startsWith('gym-')) {
    const key = moduleRunId.slice('gym-'.length);
    const params = GYM_MODULES[key];
    if (!params) return router.parseUrl('/practice');
    return router.createUrlTree(['/activity'], { queryParams: params });
  }

  if (moduleRunId.startsWith('session-')) {
    const rest = moduleRunId.slice('session-'.length);
    const separatorIndex = rest.lastIndexOf('-');
    const sessionId = rest.slice(0, separatorIndex);
    const exerciseId = rest.slice(separatorIndex + 1);
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

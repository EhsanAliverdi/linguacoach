import { TestBed } from '@angular/core/testing';
import { Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { moduleRedirectGuard } from './module-redirect.guard';
import { ActivityService } from '../services/activity.service';

describe('moduleRedirectGuard', () => {
  let router: jasmine.SpyObj<Router>;
  let activityService: jasmine.SpyObj<ActivityService>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'parseUrl']);
    router.createUrlTree.and.callFake((commands, extras) => ({ commands, extras }) as any);
    router.parseUrl.and.callFake((url) => ({ url }) as any);
    activityService = jasmine.createSpyObj('ActivityService', ['selectPracticeGymExerciseType']);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivityService, useValue: activityService },
      ],
    });
  });

  function activate(moduleRunId: string) {
    return TestBed.runInInjectionContext(() =>
      moduleRedirectGuard({ paramMap: convertToParamMap({ moduleRunId }) } as any, {} as any));
  }

  it('redirects gym modules to /activity with the matching pattern', () => {
    const result = activate('gym-phrase_match');
    expect(router.createUrlTree).toHaveBeenCalledWith(['/activity'], {
      queryParams: { exerciseType: 'phrase_match', returnTo: '/practice' },
    });
    expect(result).toEqual({ commands: ['/activity'], extras: { queryParams: { exerciseType: 'phrase_match', returnTo: '/practice' } } } as any);
  });

  it('selects gym skill modules through the ExerciseType registry API', (done) => {
    activityService.selectPracticeGymExerciseType.and.returnValue(of({
      hasSelection: true,
      selectedExerciseType: { key: 'listen_and_answer' },
      reason: null,
    } as any));

    const result$ = activate('gym-listening') as any;
    result$.subscribe(() => {
      expect(activityService.selectPracticeGymExerciseType).toHaveBeenCalledWith('listening');
      expect(router.createUrlTree).toHaveBeenCalledWith(['/activity'], {
        queryParams: { exerciseType: 'listen_and_answer', returnTo: '/practice' },
      });
      done();
    });
  });

  it('does not route gym skill modules when no exercise type is eligible', (done) => {
    activityService.selectPracticeGymExerciseType.and.returnValue(of({
      hasSelection: false,
      selectedExerciseType: null,
      reason: 'No ready Practice Gym exercise is available.',
    } as any));

    const result$ = activate('gym-reading') as any;
    result$.subscribe(() => {
      expect(router.parseUrl).toHaveBeenCalledWith('/practice');
      done();
    });
  });

  it('redirects unknown gym keys to /practice', () => {
    activate('gym-unknown');
    expect(router.parseUrl).toHaveBeenCalledWith('/practice');
  });

  // Phase I2B — the `session-{sessionId}-{exerciseId}` branch was removed along with the legacy
  // lesson-runner page; any leftover session- shaped moduleRunId now falls through to /dashboard.
  it('redirects a leftover session-shaped moduleRunId to /dashboard', () => {
    activate('session-sess1-ex1');
    expect(router.parseUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('redirects an unrecognized moduleRunId to /dashboard', () => {
    activate('unknown-shape');
    expect(router.parseUrl).toHaveBeenCalledWith('/dashboard');
  });
});

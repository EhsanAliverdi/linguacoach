import { TestBed } from '@angular/core/testing';
import { Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { moduleRedirectGuard } from './module-redirect.guard';
import { SessionService } from '../services/session.service';
import { ActivityService } from '../services/activity.service';

describe('moduleRedirectGuard', () => {
  let router: jasmine.SpyObj<Router>;
  let sessionService: jasmine.SpyObj<SessionService>;
  let activityService: jasmine.SpyObj<ActivityService>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'parseUrl']);
    router.createUrlTree.and.callFake((commands, extras) => ({ commands, extras }) as any);
    router.parseUrl.and.callFake((url) => ({ url }) as any);
    sessionService = jasmine.createSpyObj('SessionService', ['getById', 'prepareExercise']);
    activityService = jasmine.createSpyObj('ActivityService', ['selectPracticeGymExerciseType']);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: SessionService, useValue: sessionService },
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

  it('redirects a session exercise that already has an activity', (done) => {
    sessionService.getById.and.returnValue(of({
      sessionId: 'sess1', exercises: [
        { exerciseId: 'ex1', learningActivityId: 'act1', kind: 'writingTask' },
      ],
    } as any));

    const result$ = activate('session-sess1-ex1') as any;
    result$.subscribe((tree: any) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/activity'], {
        queryParams: { activityId: 'act1', returnTo: '/lesson/sess1' },
      });
      done();
    });
  });

  it('prepares the exercise activity when none exists yet', (done) => {
    sessionService.getById.and.returnValue(of({
      sessionId: 'sess1', exercises: [
        { exerciseId: 'ex1', learningActivityId: null, kind: 'writingTask' },
      ],
    } as any));
    sessionService.prepareExercise.and.returnValue(of({ activityId: 'act2' } as any));

    const result$ = activate('session-sess1-ex1') as any;
    result$.subscribe(() => {
      expect(sessionService.prepareExercise).toHaveBeenCalledWith('sess1', 'ex1');
      expect(router.createUrlTree).toHaveBeenCalledWith(['/activity'], {
        queryParams: { activityId: 'act2', returnTo: '/lesson/sess1' },
      });
      done();
    });
  });

  it('redirects review exercises without an activity to the lesson page', (done) => {
    sessionService.getById.and.returnValue(of({
      sessionId: 'sess1', exercises: [
        { exerciseId: 'ex1', learningActivityId: null, kind: 'review' },
      ],
    } as any));

    const result$ = activate('session-sess1-ex1') as any;
    result$.subscribe(() => {
      expect(router.parseUrl).toHaveBeenCalledWith('/lesson/sess1');
      done();
    });
  });

  it('correctly splits session and exercise when both IDs are standard UUIDs', (done) => {
    const sessionId = '550e8400-e29b-41d4-a716-446655440000';
    const exerciseId = '110e8400-e29b-11d4-a716-446655440000';
    sessionService.getById.and.returnValue(of({
      sessionId,
      exercises: [{ exerciseId, learningActivityId: 'act1', kind: 'writingTask' }],
    } as any));

    const result$ = activate(`session-${sessionId}-${exerciseId}`) as any;
    result$.subscribe(() => {
      expect(sessionService.getById).toHaveBeenCalledWith(sessionId);
      expect(router.createUrlTree).toHaveBeenCalledWith(['/activity'], {
        queryParams: { activityId: 'act1', returnTo: `/lesson/${sessionId}` },
      });
      done();
    });
  });
});

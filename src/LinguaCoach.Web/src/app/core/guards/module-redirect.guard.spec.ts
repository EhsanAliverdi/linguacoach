import { TestBed } from '@angular/core/testing';
import { Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { moduleRedirectGuard } from './module-redirect.guard';
import { SessionService } from '../services/session.service';

describe('moduleRedirectGuard', () => {
  let router: jasmine.SpyObj<Router>;
  let sessionService: jasmine.SpyObj<SessionService>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'parseUrl']);
    router.createUrlTree.and.callFake((commands, extras) => ({ commands, extras }) as any);
    router.parseUrl.and.callFake((url) => ({ url }) as any);
    sessionService = jasmine.createSpyObj('SessionService', ['getById', 'prepareExercise']);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: SessionService, useValue: sessionService },
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
      queryParams: { pattern: 'phrase_match', returnTo: '/practice' },
    });
    expect(result).toEqual({ commands: ['/activity'], extras: { queryParams: { pattern: 'phrase_match', returnTo: '/practice' } } } as any);
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
});

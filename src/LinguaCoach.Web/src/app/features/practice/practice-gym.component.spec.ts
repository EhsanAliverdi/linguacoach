import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PracticeGymComponent } from './practice-gym.component';
import { ActivityService } from '../../core/services/activity.service';

const readyListening: any = {
  key: 'listen_and_answer', displayName: 'Listen and Answer', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_listen_and_answer',
  estimatedDurationMinutes: 4, requiresAudio: true, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: true,
};

const readyReading: any = {
  key: 'reading_multiple_choice_single', displayName: 'Reading Multiple Choice Single', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_multiple_choice_single', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_reading_multiple_choice_single',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

describe('PracticeGymComponent', () => {
  let fixture: ComponentFixture<PracticeGymComponent>;
  let component: PracticeGymComponent;
  let activityService: jasmine.SpyObj<ActivityService>;
  let router: Router;

  beforeEach(async () => {
    activityService = jasmine.createSpyObj('ActivityService', ['getExerciseTypes', 'getPracticeGymNext']);
    activityService.getExerciseTypes.and.returnValue(of([readyListening, readyReading]));

    await TestBed.configureTestingModule({
      imports: [PracticeGymComponent],
      providers: [
        { provide: ActivityService, useValue: activityService },
        provideRouter([]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PracticeGymComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
  });

  it('clicking Listening calls the pool-aware start flow and opens the returned activity', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-123',
      exerciseType: 'listen_and_answer',
      primarySkill: 'listening',
      source: 'pool',
      poolItemId: 'pool-1',
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-123', returnTo: '/practice' },
    });
  });

  it('on-demand fallback source still opens the returned activity', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-456',
      exerciseType: 'listen_and_answer',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-456', returnTo: '/practice' },
    });
  });

  it('no eligible result does not route and shows a safe message', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: false,
      activityId: null,
      exerciseType: null,
      primarySkill: null,
      source: null,
      poolItemId: null,
      reason: 'No ready Practice Gym exercise is available for reading yet.',
    }));

    component.selectSkill('reading');

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('No ready Practice Gym exercise');
  });

  it('selection failure does not route', () => {
    activityService.getPracticeGymNext.and.returnValue(throwError(() => new Error('offline')));

    component.selectSkill('listening');

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('temporarily unavailable');
  });

  it('reading_multiple_choice_single is ready and available in Practice Gym', () => {
    expect(component.hasSkillAvailable('reading')).toBeTrue();
    expect(component.skillStatusText('reading')).toBe('Available');
    expect(component.isAvailable('reading_multiple_choice_single')).toBeTrue();
    expect(component.statusText('reading_multiple_choice_single')).toBe('Available');
  });

  it('clicking Reading calls the pool-aware start flow and opens the returned activity', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-789',
      exerciseType: 'reading_multiple_choice_single',
      primarySkill: 'reading',
      source: 'pool',
      poolItemId: 'pool-2',
      reason: null,
    }));

    component.selectSkill('reading');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'reading' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-789', returnTo: '/practice' },
    });
  });
});

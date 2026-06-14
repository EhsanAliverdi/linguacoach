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

const plannedReading: any = {
  key: 'reading_multiple_choice_single', displayName: 'Reading Multiple Choice Single', primarySkill: 'reading', secondarySkills: [],
  category: 'PTE Reading', isEnabled: true, implementationStatus: 'planned', isAvailableForGeneration: false,
  rendererKey: 'reading_multiple_choice_single', evaluatorKey: 'reading_multiple_choice_single', generationPromptKey: 'activity_generate_reading_multiple_choice_single',
  estimatedDurationMinutes: 8, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

describe('PracticeGymComponent', () => {
  let fixture: ComponentFixture<PracticeGymComponent>;
  let component: PracticeGymComponent;
  let activityService: jasmine.SpyObj<ActivityService>;
  let router: Router;

  beforeEach(async () => {
    activityService = jasmine.createSpyObj('ActivityService', ['getExerciseTypes', 'selectPracticeGymExerciseType']);
    activityService.getExerciseTypes.and.returnValue(of([readyListening, plannedReading]));

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

  it('clicking Listening calls selection flow and routes with exerciseType', () => {
    activityService.selectPracticeGymExerciseType.and.returnValue(of({
      hasSelection: true,
      selectedExerciseType: {
        key: 'listen_and_answer', displayName: 'Listen and Answer', primarySkill: 'listening', secondarySkills: [],
        rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', isAvailableForGeneration: true,
      },
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.selectPracticeGymExerciseType).toHaveBeenCalledWith('listening');
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { exerciseType: 'listen_and_answer', returnTo: '/practice' },
    });
  });

  it('no eligible result does not route and shows a safe message', () => {
    activityService.selectPracticeGymExerciseType.and.returnValue(of({
      hasSelection: false,
      selectedExerciseType: null,
      reason: 'No ready Practice Gym exercise is available for reading yet.',
    }));

    component.selectSkill('reading');

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('No ready Practice Gym exercise');
  });

  it('selection failure does not route', () => {
    activityService.selectPracticeGymExerciseType.and.returnValue(throwError(() => new Error('offline')));

    component.selectSkill('listening');

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('temporarily unavailable');
  });

  it('planned exercise type card remains unavailable', () => {
    expect(component.hasSkillAvailable('reading')).toBeFalse();
    expect(component.skillStatusText('reading')).toBe('Coming soon');
  });
});

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

const readyReadingMulti: any = {
  key: 'reading_multiple_choice_multi', displayName: 'Reading Multiple Choice Multiple', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_multiple_choice_multi', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_reading_multiple_choice_multi',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyReadingFillInBlanks: any = {
  key: 'reading_fill_in_blanks', displayName: 'Reading Fill in Blanks', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reading_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyReorderParagraphs: any = {
  key: 'reorder_paragraphs', displayName: 'Reorder Paragraphs', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reorder_paragraphs', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reorder_paragraphs',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyReadingWritingFillInBlanks: any = {
  key: 'reading_writing_fill_in_blanks', displayName: 'Reading and Writing Fill in Blanks', primarySkill: 'reading', secondarySkills: ['writing'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_writing_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reading_writing_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readySummarizeWrittenText: any = {
  key: 'summarize_written_text', displayName: 'Summarize Written Text', primarySkill: 'writing', secondarySkills: ['reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'free_text_entry', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_summarize_written_text',
  estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyWriteEssay: any = {
  key: 'write_essay', displayName: 'Write Essay', primarySkill: 'writing', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'free_text_entry', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_write_essay',
  estimatedDurationMinutes: 10, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyListeningMultipleChoiceSingle: any = {
  key: 'listening_multiple_choice_single', displayName: 'Listening Multiple Choice Single', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_multiple_choice_single', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_listening_multiple_choice_single',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyListeningMultipleChoiceMulti: any = {
  key: 'listening_multiple_choice_multi', displayName: 'Listening Multiple Choice Multiple', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_multiple_choice_multi', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_listening_multiple_choice_multi',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyListeningFillInBlanks: any = {
  key: 'listening_fill_in_blanks', displayName: 'Listening Fill in Blanks', primarySkill: 'listening', secondarySkills: ['writing'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_listening_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readySelectMissingWord: any = {
  key: 'select_missing_word', displayName: 'Select Missing Word', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'select_missing_word', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_select_missing_word',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

const readyHighlightCorrectSummary: any = {
  key: 'highlight_correct_summary', displayName: 'Highlight Correct Summary', primarySkill: 'listening', secondarySkills: ['reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'highlight_correct_summary', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_highlight_correct_summary',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false, supportsPracticeGym: true, supportsTodayLesson: false,
};

describe('PracticeGymComponent', () => {
  let fixture: ComponentFixture<PracticeGymComponent>;
  let component: PracticeGymComponent;
  let activityService: jasmine.SpyObj<ActivityService>;
  let router: Router;

  beforeEach(async () => {
    activityService = jasmine.createSpyObj('ActivityService', ['getExerciseTypes', 'getPracticeGymNext']);
    activityService.getExerciseTypes.and.returnValue(of([readyListening, readyReading, readyReadingMulti, readyReadingFillInBlanks, readyReorderParagraphs, readyReadingWritingFillInBlanks, readySummarizeWrittenText, readyWriteEssay, readyListeningMultipleChoiceSingle, readyListeningMultipleChoiceMulti, readyListeningFillInBlanks, readySelectMissingWord, readyHighlightCorrectSummary]));

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

  it('reading_multiple_choice_multi is ready and available in Practice Gym', () => {
    expect(component.isAvailable('reading_multiple_choice_multi')).toBeTrue();
    expect(component.statusText('reading_multiple_choice_multi')).toBe('Available');
  });

  it('clicking Reading can return reading_multiple_choice_multi and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-multi-1',
      exerciseType: 'reading_multiple_choice_multi',
      primarySkill: 'reading',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('reading');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-multi-1', returnTo: '/practice' },
    });
  });

  it('reading_fill_in_blanks is ready and available in Practice Gym', () => {
    expect(component.isAvailable('reading_fill_in_blanks')).toBeTrue();
    expect(component.statusText('reading_fill_in_blanks')).toBe('Available');
  });

  it('clicking Reading can return reading_fill_in_blanks and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-fib-1',
      exerciseType: 'reading_fill_in_blanks',
      primarySkill: 'reading',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('reading');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-fib-1', returnTo: '/practice' },
    });
  });

  it('reorder_paragraphs is ready and available in Practice Gym', () => {
    expect(component.isAvailable('reorder_paragraphs')).toBeTrue();
    expect(component.statusText('reorder_paragraphs')).toBe('Available');
  });

  it('clicking Reading can return reorder_paragraphs and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-rp-1',
      exerciseType: 'reorder_paragraphs',
      primarySkill: 'reading',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('reading');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-rp-1', returnTo: '/practice' },
    });
  });

  it('reading_writing_fill_in_blanks is ready and available in Practice Gym', () => {
    expect(component.isAvailable('reading_writing_fill_in_blanks')).toBeTrue();
    expect(component.statusText('reading_writing_fill_in_blanks')).toBe('Available');
  });

  it('clicking Reading can return reading_writing_fill_in_blanks and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-rwfib-1',
      exerciseType: 'reading_writing_fill_in_blanks',
      primarySkill: 'reading',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('reading');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-rwfib-1', returnTo: '/practice' },
    });
  });

  it('summarize_written_text is ready and available in Practice Gym', () => {
    expect(component.isAvailable('summarize_written_text')).toBeTrue();
    expect(component.statusText('summarize_written_text')).toBe('Available');
  });

  it('clicking Writing can return summarize_written_text and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-swt-1',
      exerciseType: 'summarize_written_text',
      primarySkill: 'writing',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('writing');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'writing' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-swt-1', returnTo: '/practice' },
    });
  });

  it('write_essay is ready and available in Practice Gym', () => {
    expect(component.isAvailable('write_essay')).toBeTrue();
    expect(component.statusText('write_essay')).toBe('Available');
  });

  it('clicking Writing can return write_essay and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-we-1',
      exerciseType: 'write_essay',
      primarySkill: 'writing',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('writing');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'writing' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-we-1', returnTo: '/practice' },
    });
  });

  it('listening_multiple_choice_single is ready and available in Practice Gym', () => {
    expect(component.isAvailable('listening_multiple_choice_single')).toBeTrue();
    expect(component.statusText('listening_multiple_choice_single')).toBe('Available');
  });

  it('clicking Listening can return listening_multiple_choice_single and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-lmcs-1',
      exerciseType: 'listening_multiple_choice_single',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-lmcs-1', returnTo: '/practice' },
    });
  });

  it('listening_multiple_choice_multi is ready and available in Practice Gym', () => {
    expect(component.isAvailable('listening_multiple_choice_multi')).toBeTrue();
    expect(component.statusText('listening_multiple_choice_multi')).toBe('Available');
  });

  it('clicking Listening can return listening_multiple_choice_multi and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-lmcm-1',
      exerciseType: 'listening_multiple_choice_multi',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-lmcm-1', returnTo: '/practice' },
    });
  });

  it('listening_fill_in_blanks is ready and available in Practice Gym', () => {
    expect(component.isAvailable('listening_fill_in_blanks')).toBeTrue();
    expect(component.statusText('listening_fill_in_blanks')).toBe('Available');
  });

  it('clicking Listening can return listening_fill_in_blanks and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-lfib-1',
      exerciseType: 'listening_fill_in_blanks',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-lfib-1', returnTo: '/practice' },
    });
  });

  it('select_missing_word is ready and available in Practice Gym', () => {
    expect(component.isAvailable('select_missing_word')).toBeTrue();
    expect(component.statusText('select_missing_word')).toBe('Available');
  });

  it('clicking Listening can return select_missing_word and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-smw-1',
      exerciseType: 'select_missing_word',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-smw-1', returnTo: '/practice' },
    });
  });

  it('highlight_correct_summary is ready and available in Practice Gym', () => {
    expect(component.isAvailable('highlight_correct_summary')).toBeTrue();
    expect(component.statusText('highlight_correct_summary')).toBe('Available');
  });

  it('clicking Listening can return highlight_correct_summary and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true,
      activityId: 'activity-hcs-1',
      exerciseType: 'highlight_correct_summary',
      primarySkill: 'listening',
      source: 'onDemandFallback',
      poolItemId: null,
      reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-hcs-1', returnTo: '/practice' },
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { PracticeGymComponent } from './practice-gym.component';
import { ActivityService } from '../../../core/services/activity.service';
import {
  PracticeGymSuggestionsService,
  PracticeGymSuggestionsResponse,
  PracticeGymSuggestionItem,
} from '../../../core/services/practice-gym-suggestions.service';

const emptySuggestions: PracticeGymSuggestionsResponse = {
  suggestedItems: [], continueItems: [], reviewItems: [],
  readyCount: 0, reviewOnlyCount: 0, reservedCount: 0,
  isReplenishmentRecommended: false, generatedAtUtc: new Date().toISOString(),
};

function makeSuggestionItem(overrides: Partial<PracticeGymSuggestionItem> = {}): PracticeGymSuggestionItem {
  return {
    readinessItemId: 'item-1',
    title: 'Test Practice',
    description: 'A test practice item',
    primarySkill: 'listening',
    secondarySkills: [],
    patternKey: 'listen_and_answer',
    activityType: null,
    targetCefrLevel: 'B2',
    studentCefrLevelSnapshot: 'B2',
    curriculumObjectiveKey: null,
    curriculumObjectiveTitle: null,
    contextTags: ['general_english'],
    focusTags: [],
    routingReason: 'Normal',
    isLowerLevelContent: false,
    difficultyBand: 2,
    estimatedDurationMinutes: 5,
    supportLanguageName: null,
    status: 'ready',
    callToAction: 'Start practice',
    explanation: 'Recommended for your level',
    linkedLearningActivityId: 'act-123',
    linkedLearningSessionId: null,
    linkedSessionExerciseId: null,
    ...overrides,
  };
}

const readyListening: any = {
  key: 'listen_and_answer', displayName: 'Listen and Answer', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'audio_and_free_text', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_listen_and_answer',
  estimatedDurationMinutes: 4, requiresAudio: true, requiresImage: false,
  defaultItemsPerPractice: 3, minItemsPerPractice: 2, maxItemsPerPractice: 5,
};

const readyReading: any = {
  key: 'reading_multiple_choice_single', displayName: 'Reading Multiple Choice Single', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_multiple_choice_single', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_reading_multiple_choice_single',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 4, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyReadingMulti: any = {
  key: 'reading_multiple_choice_multi', displayName: 'Reading Multiple Choice Multiple', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_multiple_choice_multi', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_reading_multiple_choice_multi',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 4, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyReadingFillInBlanks: any = {
  key: 'reading_fill_in_blanks', displayName: 'Reading Fill in Blanks', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reading_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 7,
};

const readyReorderParagraphs: any = {
  key: 'reorder_paragraphs', displayName: 'Reorder Paragraphs', primarySkill: 'reading', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reorder_paragraphs', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reorder_paragraphs',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 4, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyReadingWritingFillInBlanks: any = {
  key: 'reading_writing_fill_in_blanks', displayName: 'Reading and Writing Fill in Blanks', primarySkill: 'reading', secondarySkills: ['writing'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'reading_writing_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_reading_writing_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 7,
};

const readySummarizeWrittenText: any = {
  key: 'summarize_written_text', displayName: 'Summarize Written Text', primarySkill: 'writing', secondarySkills: ['reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'free_text_entry', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_summarize_written_text',
  estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 1,
};

const readyWriteEssay: any = {
  key: 'write_essay', displayName: 'Write Essay', primarySkill: 'writing', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'free_text_entry', evaluatorKey: 'ai_structured', generationPromptKey: 'activity_generate_write_essay',
  estimatedDurationMinutes: 10, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 1,
};

const readyListeningMultipleChoiceSingle: any = {
  key: 'listening_multiple_choice_single', displayName: 'Listening Multiple Choice Single', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_multiple_choice_single', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_listening_multiple_choice_single',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 4, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyListeningMultipleChoiceMulti: any = {
  key: 'listening_multiple_choice_multi', displayName: 'Listening Multiple Choice Multiple', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_multiple_choice_multi', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_listening_multiple_choice_multi',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 4, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyListeningFillInBlanks: any = {
  key: 'listening_fill_in_blanks', displayName: 'Listening Fill in Blanks', primarySkill: 'listening', secondarySkills: ['writing'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'listening_fill_in_blanks', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_listening_fill_in_blanks',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 7,
};

const readySelectMissingWord: any = {
  key: 'select_missing_word', displayName: 'Select Missing Word', primarySkill: 'listening', secondarySkills: [],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'select_missing_word', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_select_missing_word',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 7,
};

const readyHighlightCorrectSummary: any = {
  key: 'highlight_correct_summary', displayName: 'Highlight Correct Summary', primarySkill: 'listening', secondarySkills: ['reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'highlight_correct_summary', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_highlight_correct_summary',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 3, minItemsPerPractice: 2, maxItemsPerPractice: 5,
};

const readyHighlightIncorrectWords: any = {
  key: 'highlight_incorrect_words', displayName: 'Highlight Incorrect Words', primarySkill: 'listening', secondarySkills: ['reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'highlight_incorrect_words', evaluatorKey: 'keyed_selection', generationPromptKey: 'activity_generate_highlight_incorrect_words',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 3, minItemsPerPractice: 2, maxItemsPerPractice: 5,
};

const readyAnswerShortQuestion: any = {
  key: 'answer_short_question', displayName: 'Answer Short Question', primarySkill: 'speaking', secondarySkills: ['listening'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'answer_short_question', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_answer_short_question',
  estimatedDurationMinutes: 6, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 8,
};

const readyReadAloud: any = {
  key: 'read_aloud', displayName: 'Read Aloud', primarySkill: 'speaking', secondarySkills: ['pronunciation', 'reading'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'read_aloud', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_read_aloud',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 2, minItemsPerPractice: 1, maxItemsPerPractice: 3,
};

const readyRepeatSentence: any = {
  key: 'repeat_sentence', displayName: 'Repeat Sentence', primarySkill: 'speaking', secondarySkills: ['listening', 'pronunciation'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'repeat_sentence', evaluatorKey: 'exact_match', generationPromptKey: 'activity_generate_repeat_sentence',
  estimatedDurationMinutes: 5, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 5, minItemsPerPractice: 3, maxItemsPerPractice: 6,
};

const readyRespondToSituation: any = {
  key: 'respond_to_situation', displayName: 'Respond to Situation', primarySkill: 'speaking', secondarySkills: ['communication', 'listening'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'respond_to_situation', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_respond_to_situation',
  estimatedDurationMinutes: 6, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 2,
};

const readyDescribeImage: any = {
  key: 'describe_image', displayName: 'Describe Image', primarySkill: 'speaking', secondarySkills: ['vocabulary', 'communication'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'describe_image', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_describe_image',
  estimatedDurationMinutes: 6, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 1,
};

const readyRetellLecture: any = {
  key: 'retell_lecture', displayName: 'Retell Lecture', primarySkill: 'listening', secondarySkills: ['speaking', 'summarizing', 'communication'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'retell_lecture', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_retell_lecture',
  estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 1,
};

const readySummarizeGroupDiscussion: any = {
  key: 'summarize_group_discussion', displayName: 'Summarize Group Discussion', primarySkill: 'listening', secondarySkills: ['speaking', 'summarizing', 'communication'],
  category: 'Pattern', isEnabled: true, implementationStatus: 'ready', isAvailableForGeneration: true,
  rendererKey: 'summarize_group_discussion', evaluatorKey: 'ai_open_ended', generationPromptKey: 'activity_generate_summarize_group_discussion',
  estimatedDurationMinutes: 7, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 1, minItemsPerPractice: 1, maxItemsPerPractice: 1,
};

const plannedFormat: any = {
  key: 'some_future_format', displayName: 'Some Future Format', primarySkill: 'speaking', secondarySkills: [],
  category: 'Pattern', isEnabled: false, implementationStatus: 'planned', isAvailableForGeneration: false,
  rendererKey: '', evaluatorKey: '', generationPromptKey: '',
  estimatedDurationMinutes: 0, requiresAudio: false, requiresImage: false,
  defaultItemsPerPractice: 0, minItemsPerPractice: 0, maxItemsPerPractice: 0,
};

const ALL_READY = [
  readyListening, readyReading, readyReadingMulti, readyReadingFillInBlanks,
  readyReorderParagraphs, readyReadingWritingFillInBlanks, readySummarizeWrittenText,
  readyWriteEssay, readyListeningMultipleChoiceSingle, readyListeningMultipleChoiceMulti,
  readyListeningFillInBlanks, readySelectMissingWord, readyHighlightCorrectSummary,
  readyHighlightIncorrectWords, readyAnswerShortQuestion, readyReadAloud, readyRepeatSentence,
  readyRespondToSituation, readyDescribeImage, readyRetellLecture, readySummarizeGroupDiscussion,
];

describe('PracticeGymComponent', () => {
  let fixture: ComponentFixture<PracticeGymComponent>;
  let component: PracticeGymComponent;
  let activityService: jasmine.SpyObj<ActivityService>;
  let suggestionsService: jasmine.SpyObj<PracticeGymSuggestionsService>;
  let router: Router;

  beforeEach(async () => {
    activityService = jasmine.createSpyObj('ActivityService', ['getExerciseTypes', 'getPracticeGymNext']);
    activityService.getExerciseTypes.and.returnValue(of(ALL_READY));

    suggestionsService = jasmine.createSpyObj('PracticeGymSuggestionsService', ['getSuggestions', 'startSuggestion', 'completeSuggestion']);
    suggestionsService.getSuggestions.and.returnValue(of(emptySuggestions));

    await TestBed.configureTestingModule({
      imports: [PracticeGymComponent],
      providers: [
        { provide: ActivityService, useValue: activityService },
        { provide: PracticeGymSuggestionsService, useValue: suggestionsService },
        provideRouter([]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PracticeGymComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture.detectChanges();
  });

  // â”€â”€ Catalog-driven card grid â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('shows the practice heading', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="practice-gym-heading"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('Practice Gym');
  });

  it('renders a skill section for each skill present in the catalog', () => {
    const sections = fixture.nativeElement.querySelectorAll('[data-testid^="practice-skill-section-"]');
    expect(sections.length).toBeGreaterThanOrEqual(4); // listening, reading, writing, speaking
  });

  it('renders a runnable card for answer_short_question', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-answer_short_question"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('renders a runnable card for read_aloud', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-read_aloud"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('repeat_sentence is ready and available in Practice Gym', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-repeat_sentence"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('shows item count for repeat_sentence', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-repeat_sentence"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('5');
  });

  it('describe_image is ready and available in Practice Gym', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-describe_image"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('shows item count for describe_image', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-describe_image"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('1');
  });

  it('retell_lecture is ready and available in Practice Gym', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-retell_lecture"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('shows item count for retell_lecture', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-retell_lecture"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('1');
  });

  it('summarize_group_discussion is ready and available in Practice Gym', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-summarize_group_discussion"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('shows item count for summarize_group_discussion', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-summarize_group_discussion"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('1');
  });

  it('planned format is shown as locked (not a button)', async () => {
    activityService.getExerciseTypes.and.returnValue(of([...ALL_READY, plannedFormat]));
    const newFixture = TestBed.createComponent(PracticeGymComponent);
    newFixture.detectChanges();
    await newFixture.whenStable();
    const card = newFixture.nativeElement.querySelector('[data-testid="practice-format-some_future_format"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).not.toBe('button');
  });

  it('shows item count for answer_short_question', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-answer_short_question"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('5');
  });

  it('shows secondary skill chips for listening_fill_in_blanks', () => {
    const chips = fixture.nativeElement.querySelector('[data-testid="format-chips-listening_fill_in_blanks"]');
    expect(chips).toBeTruthy();
  });

  it('shows secondary skill chips for reading_writing_fill_in_blanks', () => {
    const chips = fixture.nativeElement.querySelector('[data-testid="format-chips-reading_writing_fill_in_blanks"]');
    expect(chips).toBeTruthy();
  });

  it('shows loading state before catalog arrives', async () => {
    const loadingFixture = TestBed.createComponent(PracticeGymComponent);
    // Do not call detectChanges so ngOnInit has not resolved
    expect(loadingFixture.componentInstance.loadState()).toBe('loading');
  });

  it('shows error state when catalog load fails', async () => {
    activityService.getExerciseTypes.and.returnValue(throwError(() => new Error('net')));
    const errFixture = TestBed.createComponent(PracticeGymComponent);
    errFixture.detectChanges();
    await errFixture.whenStable();
    expect(errFixture.componentInstance.loadState()).toBe('error');
    const el = errFixture.nativeElement.querySelector('[data-testid="practice-error"]');
    expect(el).toBeTruthy();
  });

  it('shows empty state when catalog is empty', async () => {
    activityService.getExerciseTypes.and.returnValue(of([]));
    const emptyFixture = TestBed.createComponent(PracticeGymComponent);
    emptyFixture.detectChanges();
    await emptyFixture.whenStable();
    const el = emptyFixture.nativeElement.querySelector('[data-testid="practice-empty"]');
    expect(el).toBeTruthy();
  });

  it('startFormat navigates to /activity with the returned activityId', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'act-asq-1', exerciseType: 'answer_short_question',
      primarySkill: 'speaking', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    const card = component.skillGroups()
      .flatMap(g => g.cards)
      .find(c => c.key === 'answer_short_question')!;
    component.startFormat(card);

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'speaking', exerciseType: 'answer_short_question' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'act-asq-1', returnTo: '/practice' },
    });
  });

  it('respond_to_situation is ready and available in Practice Gym', () => {
    const card = fixture.nativeElement.querySelector('[data-testid="practice-format-respond_to_situation"]');
    expect(card).toBeTruthy();
    expect(card.tagName.toLowerCase()).toBe('button');
  });

  it('shows item count for respond_to_situation', () => {
    const countEl = fixture.nativeElement.querySelector('[data-testid="format-count-respond_to_situation"]');
    expect(countEl).toBeTruthy();
    expect(countEl.textContent).toContain('1');
  });

  it('clicking respond_to_situation navigates to /activity with the returned activityId', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'act-rts-1', exerciseType: 'respond_to_situation',
      primarySkill: 'speaking', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    const card = component.skillGroups()
      .flatMap(g => g.cards)
      .find(c => c.key === 'respond_to_situation')!;
    component.startFormat(card);

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'speaking', exerciseType: 'respond_to_situation' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'act-rts-1', returnTo: '/practice' },
    });
  });

  it('clicking repeat_sentence navigates to /activity with the returned activityId', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'act-rs-1', exerciseType: 'repeat_sentence',
      primarySkill: 'speaking', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    const card = component.skillGroups()
      .flatMap(g => g.cards)
      .find(c => c.key === 'repeat_sentence')!;
    component.startFormat(card);

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'speaking', exerciseType: 'repeat_sentence' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'act-rs-1', returnTo: '/practice' },
    });
  });

  it('startFormat shows message when no activity available', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: false, activityId: null, exerciseType: null,
      primarySkill: null, source: null, poolItemId: null,
      reason: 'No ready exercise available.',
    }));

    const card = component.skillGroups().flatMap(g => g.cards).find(c => c.key === 'answer_short_question')!;
    component.startFormat(card);

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('No ready exercise');
  });

  it('startFormat shows fallback message on API error', () => {
    activityService.getPracticeGymNext.and.returnValue(throwError(() => new Error('fail')));

    const card = component.skillGroups().flatMap(g => g.cards).find(c => c.key === 'listen_and_answer')!;
    component.startFormat(card);

    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.selectionMessage()).toContain('temporarily unavailable');
  });

  it('startFormat is a no-op for a locked/planned card', () => {
    activityService.getExerciseTypes.and.returnValue(of([...ALL_READY, plannedFormat]));
    const newFixture = TestBed.createComponent(PracticeGymComponent);
    newFixture.detectChanges();
    const comp = newFixture.componentInstance;

    const lockedCard = comp.skillGroups().flatMap(g => g.cards).find(c => c.key === 'some_future_format');
    if (lockedCard) comp.startFormat(lockedCard);

    expect(activityService.getPracticeGymNext).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('skillGroups are ordered: listening before reading before writing before speaking', () => {
    const skills = component.skillGroups().map(g => g.skill);
    const li = skills.indexOf('listening');
    const re = skills.indexOf('reading');
    const wr = skills.indexOf('writing');
    const sp = skills.indexOf('speaking');
    expect(li).toBeLessThan(re);
    expect(re).toBeLessThan(wr);
    expect(wr).toBeLessThan(sp);
  });

  // â”€â”€ Backward-compat: selectSkill / hasSkillAvailable / isAvailable â”€â”€â”€â”€â”€â”€â”€â”€

  it('clicking Listening calls the pool-aware start flow and opens the returned activity', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'activity-123', exerciseType: 'listen_and_answer',
      primarySkill: 'listening', source: 'pool', poolItemId: 'pool-1', reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-123', returnTo: '/practice' },
    });
  });

  it('on-demand fallback source still opens the returned activity', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'activity-456', exerciseType: 'listen_and_answer',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    component.selectSkill('listening');

    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-456', returnTo: '/practice' },
    });
  });

  it('no eligible result does not route and shows a safe message', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: false, activityId: null, exerciseType: null, primarySkill: null,
      source: null, poolItemId: null, reason: 'No ready Practice Gym exercise is available for reading yet.',
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
      hasActivity: true, activityId: 'activity-789', exerciseType: 'reading_multiple_choice_single',
      primarySkill: 'reading', source: 'pool', poolItemId: 'pool-2', reason: null,
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
      hasActivity: true, activityId: 'activity-multi-1', exerciseType: 'reading_multiple_choice_multi',
      primarySkill: 'reading', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-fib-1', exerciseType: 'reading_fill_in_blanks',
      primarySkill: 'reading', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-rp-1', exerciseType: 'reorder_paragraphs',
      primarySkill: 'reading', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-rwfib-1', exerciseType: 'reading_writing_fill_in_blanks',
      primarySkill: 'reading', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-swt-1', exerciseType: 'summarize_written_text',
      primarySkill: 'writing', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-we-1', exerciseType: 'write_essay',
      primarySkill: 'writing', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-lmcs-1', exerciseType: 'listening_multiple_choice_single',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-lmcm-1', exerciseType: 'listening_multiple_choice_multi',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-lfib-1', exerciseType: 'listening_fill_in_blanks',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
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
      hasActivity: true, activityId: 'activity-smw-1', exerciseType: 'select_missing_word',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
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

  it('highlight_incorrect_words is ready and available in Practice Gym', () => {
    expect(component.isAvailable('highlight_incorrect_words')).toBeTrue();
    expect(component.statusText('highlight_incorrect_words')).toBe('Available');
  });

  it('clicking Listening can return highlight_incorrect_words and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'activity-hiw-1', exerciseType: 'highlight_incorrect_words',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-hiw-1', returnTo: '/practice' },
    });
  });

  it('clicking Listening can return highlight_correct_summary and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'activity-hcs-1', exerciseType: 'highlight_correct_summary',
      primarySkill: 'listening', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    component.selectSkill('listening');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'listening' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-hcs-1', returnTo: '/practice' },
    });
  });

  // â”€â”€ answer_short_question â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('answer_short_question is ready and available in Practice Gym', () => {
    expect(component.isAvailable('answer_short_question')).toBeTrue();
    expect(component.statusText('answer_short_question')).toBe('Available');
    expect(component.hasSkillAvailable('speaking')).toBeTrue();
  });

  it('clicking Speaking can return answer_short_question and routes correctly', () => {
    activityService.getPracticeGymNext.and.returnValue(of({
      hasActivity: true, activityId: 'activity-asq-1', exerciseType: 'answer_short_question',
      primarySkill: 'speaking', source: 'onDemandFallback', poolItemId: null, reason: null,
    }));

    component.selectSkill('speaking');

    expect(activityService.getPracticeGymNext).toHaveBeenCalledWith({ skill: 'speaking' });
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'activity-asq-1', returnTo: '/practice' },
    });
  });

  // â”€â”€ Suggestions: Suggested for you / Continue / Review â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('loads suggestions on init', () => {
    expect(suggestionsService.getSuggestions).toHaveBeenCalled();
  });

  it('shows empty state when no suggestions exist', async () => {
    suggestionsService.getSuggestions.and.returnValue(of(emptySuggestions));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const el = f.nativeElement.querySelector('[data-testid="suggestions-empty"]');
    expect(el).toBeTruthy();
  });

  it('shows suggestions-error when API fails', async () => {
    suggestionsService.getSuggestions.and.returnValue(throwError(() => new Error('net')));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const el = f.nativeElement.querySelector('[data-testid="suggestions-error"]');
    expect(el).toBeTruthy();
  });

  it('renders suggested-for-you section when suggestedItems present', async () => {
    const item = makeSuggestionItem();
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const section = f.nativeElement.querySelector('[data-testid="suggestions-section"]');
    expect(section).toBeTruthy();
    const card = f.nativeElement.querySelector('[data-testid="suggestion-card-item-1"]');
    expect(card).toBeTruthy();
  });

  it('renders continue section only when continueItems present', async () => {
    const item = makeSuggestionItem({ readinessItemId: 'cont-1', status: 'reserved' });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, continueItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    expect(f.nativeElement.querySelector('[data-testid="continue-section"]')).toBeTruthy();
    expect(f.nativeElement.querySelector('[data-testid="suggestions-section"]')).toBeNull();
  });

  it('renders review section only when reviewItems present', async () => {
    const item = makeSuggestionItem({ readinessItemId: 'rev-1', routingReason: 'Review', isLowerLevelContent: true });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, reviewItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    expect(f.nativeElement.querySelector('[data-testid="review-section"]')).toBeTruthy();
  });

  it('clicking start calls startSuggestion and navigates to activity', async () => {
    const item = makeSuggestionItem();
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    suggestionsService.startSuggestion.and.returnValue(of({
      success: true, failureReason: null,
      learningActivityId: 'act-123', learningSessionId: null, sessionExerciseId: null,
      alreadyReserved: false,
    }));
    // Use the fixture already created by beforeEach (router already spied)
    suggestionsService.getSuggestions.calls.reset();
    (router.navigate as jasmine.Spy).calls.reset();

    // Re-init to pick up the new suggestions response
    component.ngOnInit();
    fixture.detectChanges();
    await fixture.whenStable();

    const btn = fixture.nativeElement.querySelector('[data-testid="suggestion-start-item-1"]');
    btn.click();
    await fixture.whenStable();

    expect(suggestionsService.startSuggestion).toHaveBeenCalledWith('item-1');
    expect(router.navigate).toHaveBeenCalledWith(['/activity'], {
      queryParams: { activityId: 'act-123', returnTo: '/practice' },
    });
  });

  it('shows routing label for normal item', async () => {
    const item = makeSuggestionItem({ routingReason: 'Normal', isLowerLevelContent: false });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const label = f.nativeElement.querySelector('[data-testid="routing-label"]');
    expect(label?.textContent).toContain('Recommended for your current goal');
  });

  it('shows lower-level label for scaffold item', async () => {
    const item = makeSuggestionItem({ routingReason: 'Scaffold', isLowerLevelContent: true });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const label = f.nativeElement.querySelector('[data-testid="lower-level-label"]');
    expect(label?.textContent).toContain('Step back to strengthen basics');
  });

  it('existing by-skill sections still render after suggestions load', async () => {
    suggestionsService.getSuggestions.and.returnValue(of(emptySuggestions));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const sections = f.nativeElement.querySelectorAll('[data-testid^="practice-skill-section-"]');
    expect(sections.length).toBeGreaterThanOrEqual(4);
  });

  it('manual practice sections render even when suggestions error', async () => {
    suggestionsService.getSuggestions.and.returnValue(throwError(() => new Error('net')));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const sections = f.nativeElement.querySelectorAll('[data-testid^="practice-skill-section-"]');
    expect(sections.length).toBeGreaterThanOrEqual(4);
  });

  it('renders explanation text on suggestion card when explanation is set', async () => {
    const item = makeSuggestionItem({ explanation: 'Listening is your weakest skill' });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const reason = f.nativeElement.querySelector('[data-testid="suggestion-reason"]');
    expect(reason?.textContent).toContain('Listening is your weakest skill');
  });

  it('does not render explanation element when explanation is empty', async () => {
    const item = makeSuggestionItem({ explanation: '' });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const reason = f.nativeElement.querySelector('[data-testid="suggestion-reason"]');
    expect(reason).toBeNull();
  });

  it('shows review queue section with empty state when suggestions loaded but no review items', async () => {
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, suggestedItems: [makeSuggestionItem()] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const reviewSection = f.nativeElement.querySelector('[data-testid="review-section"]');
    expect(reviewSection).toBeTruthy();
    const emptyState = f.nativeElement.querySelector('[data-testid="review-queue-empty"]');
    expect(emptyState).toBeTruthy();
    expect(emptyState?.textContent).toContain('all caught up');
  });

  it('shows review items when review queue is non-empty', async () => {
    const reviewItem = makeSuggestionItem({ readinessItemId: 'rev-1', routingReason: 'Review' });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, reviewItems: [reviewItem] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const grid = f.nativeElement.querySelector('[data-testid="review-grid"]');
    expect(grid).toBeTruthy();
    const emptyState = f.nativeElement.querySelector('[data-testid="review-queue-empty"]');
    expect(emptyState).toBeNull();
  });

  it('shows retry button in suggestions error state', async () => {
    suggestionsService.getSuggestions.and.returnValue(throwError(() => new Error('net')));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const retryBtn = f.nativeElement.querySelector('[data-testid="suggestions-retry"]');
    expect(retryBtn).toBeTruthy();
  });

  // ── Phase 19C: review scaffold pilot labels ────────────────────────────────

  it('renders the pilot-friendly review label and reason for an approved scaffold item', async () => {
    const item = makeSuggestionItem({
      readinessItemId: 'scaffold-1',
      routingReason: 'Review',
      isLowerLevelContent: true,
      callToAction: 'Review',
      explanation: 'This helps you practise a skill you are building.',
    });
    suggestionsService.getSuggestions.and.returnValue(of({ ...emptySuggestions, reviewItems: [item] }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();

    const card = f.nativeElement.querySelector('[data-testid="suggestion-card-scaffold-1"]');
    expect(card.textContent).toContain('Review');
    expect(card.textContent).toContain('This helps you practise a skill you are building.');

    // No negative wording and no internal diagnostics ever rendered.
    const lower = card.textContent.toLowerCase();
    expect(lower).not.toContain('failed');
    expect(lower).not.toContain('weakness');
    expect(lower).not.toContain('low confidence');
    expect(lower).not.toContain('provider');
    expect(lower).not.toContain('admin');
  });

  it('review queue is empty (hidden) when the API returns no review items', async () => {
    suggestionsService.getSuggestions.and.returnValue(of({
      ...emptySuggestions, suggestedItems: [makeSuggestionItem()], reviewItems: [],
    }));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();

    const grid = f.nativeElement.querySelector('[data-testid="review-grid"]');
    expect(grid).toBeNull();
    const emptyState = f.nativeElement.querySelector('[data-testid="review-queue-empty"]');
    expect(emptyState).toBeTruthy();
  });

  it('retry button triggers a fresh suggestions load', async () => {
    suggestionsService.getSuggestions.and.returnValue(throwError(() => new Error('net')));
    const f = TestBed.createComponent(PracticeGymComponent);
    f.detectChanges();
    await f.whenStable();
    const callsBefore = suggestionsService.getSuggestions.calls.count();

    const retryBtn = f.nativeElement.querySelector('[data-testid="suggestions-retry"]');
    retryBtn.click();
    f.detectChanges();

    expect(suggestionsService.getSuggestions.calls.count()).toBeGreaterThan(callsBefore);
  });
});



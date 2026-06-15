import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminExerciseTypesComponent } from './admin-exercise-types.component';
import { AdminService } from '../../../core/services/admin.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';

function makeType(overrides: Partial<ExerciseTypeDefinition> = {}): ExerciseTypeDefinition {
  return {
    key: 'reading_fill_in_blanks',
    displayName: 'Reading Fill in Blanks',
    description: 'desc',
    primarySkill: 'reading',
    secondarySkills: [],
    category: 'Pattern',
    isEnabled: true,
    implementationStatus: 'ready',
    isAvailableForGeneration: true,
    rendererKey: 'r',
    evaluatorKey: 'e',
    generationPromptKey: 'g',
    legacyActivityType: null,
    exercisePatternKey: 'reading_fill_in_blanks',
    estimatedDurationMinutes: 5,
    requiresAudio: false,
    requiresImage: false,
    supportsPracticeGym: true,
    supportsTodayLesson: false,
    minItemsPerPractice: 3,
    defaultItemsPerPractice: 4,
    maxItemsPerPractice: 6,
    minOptionsPerItem: 3,
    defaultOptionsPerItem: 4,
    maxOptionsPerItem: 5,
    ...overrides,
  };
}

describe('AdminExerciseTypesComponent', () => {
  let admin: jasmine.SpyObj<AdminService>;

  beforeEach(() => {
    admin = jasmine.createSpyObj('AdminService', ['listExerciseTypes', 'updateExerciseType']);
    admin.listExerciseTypes.and.returnValue(of([makeType()]));
    admin.updateExerciseType.and.callFake((key, _req) => of(makeType({ key })));

    TestBed.configureTestingModule({
      imports: [AdminExerciseTypesComponent],
      providers: [{ provide: AdminService, useValue: admin }],
    });
  });

  it('displays seeded count fields', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('[aria-label="min items"]')).toBeTruthy();
    expect(html.querySelector('[aria-label="max options"]')).toBeTruthy();
  });

  it('submits valid count edits through the patch flow', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const type = c.exerciseTypes()[0];

    c.saveCounts(type);

    expect(admin.updateExerciseType).toHaveBeenCalledWith('reading_fill_in_blanks', jasmine.objectContaining({
      minItemsPerPractice: 3,
      maxItemsPerPractice: 6,
    }));
  });

  it('rejects invalid range and does not call the API', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const type = c.exerciseTypes()[0];
    type.minItemsPerPractice = 9;

    expect(c.countError(type)).toBeTruthy();

    c.saveCounts(type);
    expect(admin.updateExerciseType).not.toHaveBeenCalled();
  });

  it('flags negative values', () => {
    const type = makeType({ minOptionsPerItem: -1 });
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    expect(fixture.componentInstance.countError(type)).toBe('No negative values.');
  });
});

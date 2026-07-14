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

  it('renders page header and table', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-page-header')).toBeTruthy();
    expect(html.querySelector('sp-admin-table')).toBeTruthy();
  });

  it('renders filter bar with search and selects', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-filter-bar')).toBeTruthy();
    expect(html.querySelector('[aria-label="Search exercise types"]')).toBeTruthy();
    expect(html.querySelector('sp-admin-select')).toBeTruthy();
  });

  it('filters rows by search query', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.searchQuery.set('nonexistent');
    fixture.detectChanges();
    expect(c.filteredExerciseTypes().length).toBe(0);

    c.searchQuery.set('reading');
    fixture.detectChanges();
    expect(c.filteredExerciseTypes().length).toBe(1);
  });

  it('filters rows by status', () => {
    admin.listExerciseTypes.and.returnValue(of([
      makeType({ key: 'a', isEnabled: true }),
      makeType({ key: 'b', isEnabled: false }),
    ]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.statusFilter.set('enabled');
    fixture.detectChanges();
    expect(c.filteredExerciseTypes().length).toBe(1);
    expect(c.filteredExerciseTypes()[0].key).toBe('a');
  });

  it('openConfig populates configForm and opens slide-over', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const type = c.exerciseTypes()[0];

    c.openConfig(type);

    expect(c.configOpen()).toBeTrue();
    expect((c.configForm() as ExerciseTypeDefinition).key).toBe('reading_fill_in_blanks');
  });

  it('configCountError returns null for valid form', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    expect(c.configCountError(makeType())).toBeNull();
  });

  it('configCountError returns error when min > max', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const invalid = makeType({ minItemsPerPractice: 9, maxItemsPerPractice: 6 });
    expect(c.configCountError(invalid)).toBeTruthy();
  });

  it('onRowAction configure opens the config slide-over', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const type = c.exerciseTypes()[0];

    c.onRowAction('configure', type);
    expect(c.configOpen()).toBeTrue();
  });

  it('saveConfig calls updateExerciseType with form values', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    const type = c.exerciseTypes()[0];
    c.openConfig(type);

    c.saveConfig();

    expect(admin.updateExerciseType).toHaveBeenCalledWith('reading_fill_in_blanks', jasmine.objectContaining({
      minItemsPerPractice: 3,
      maxItemsPerPractice: 6,
    }));
  });

  it('pagination resets to 1 when onSearch is called', () => {
    admin.listExerciseTypes.and.returnValue(of(
      Array.from({ length: 25 }, (_, i) => makeType({ key: `type_${i}`, displayName: `Type ${i}` }))
    ));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.page.set(2);
    const fakeEvent = { target: { value: 'Type 1' } } as unknown as Event;
    c.onSearch(fakeEvent);
    expect(c.page()).toBe(1);
  });

  // ── REDESIGN-4 KPI strip and icon tile ─────────────────────────────────────

  it('typeSummary total matches loaded exercise types count', () => {
    admin.listExerciseTypes.and.returnValue(of([makeType(), makeType({ key: 'b' })]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.typeSummary().total).toBe(2);
  });

  it('typeSummary enabled counts only enabled types', () => {
    admin.listExerciseTypes.and.returnValue(of([
      makeType({ key: 'a', isEnabled: true }),
      makeType({ key: 'b', isEnabled: false }),
    ]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.typeSummary().enabled).toBe(1);
  });

  it('typeSummary ready counts only ready types', () => {
    admin.listExerciseTypes.and.returnValue(of([
      makeType({ key: 'a', implementationStatus: 'ready' }),
      makeType({ key: 'b', implementationStatus: 'not_implemented' }),
    ]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.typeSummary().ready).toBe(1);
  });

  it('typeSummary skills counts unique primary skills', () => {
    admin.listExerciseTypes.and.returnValue(of([
      makeType({ key: 'a', primarySkill: 'reading' }),
      makeType({ key: 'b', primarySkill: 'writing' }),
      makeType({ key: 'c', primarySkill: 'reading' }),
    ]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.typeSummary().skills).toBe(2);
  });

  it('renders sp-admin-kpi-card elements for the summary strip', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    const cards = html.querySelectorAll('sp-admin-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('summary strip has aria-label "Exercise types summary"', () => {
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('[aria-label="Exercise types summary"]')).toBeTruthy();
  });

  it('shows "Not runnable yet" label for non-ready type', () => {
    admin.listExerciseTypes.and.returnValue(of([makeType({ implementationStatus: 'not_implemented' })]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Not runnable yet');
  });

  it('does not show "Not runnable yet" label for ready type', () => {
    admin.listExerciseTypes.and.returnValue(of([makeType({ implementationStatus: 'ready' })]));
    const fixture = TestBed.createComponent(AdminExerciseTypesComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Not runnable yet');
  });
});

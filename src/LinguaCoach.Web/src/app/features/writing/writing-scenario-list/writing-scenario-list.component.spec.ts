import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { WritingScenarioListComponent } from './writing-scenario-list.component';
import { WritingService } from '../../../core/services/writing.service';
import { WritingScenarioDto } from '../../../core/models/writing.models';

const scenarios: WritingScenarioDto[] = [
  {
    id: 'aaaaaaaa-1111-2222-3333-444444444444',
    title: 'Follow up on a pending document approval',
    situation: 'You submitted a document 5 days ago.',
    learningGoal: 'Learn to follow up professionally.',
    difficulty: 'B1',
    targetPhrases: ['I wanted to follow up on'],
    targetVocabulary: ['pending', 'approval'],
  },
  {
    id: 'bbbbbbbb-1111-2222-3333-444444444444',
    title: 'Ask for missing information politely',
    situation: 'A colleague sent an incomplete report.',
    learningGoal: 'Request missing information diplomatically.',
    difficulty: 'A2',
    targetPhrases: ['Could you please send'],
    targetVocabulary: ['missing', 'clarification'],
  },
];

describe('WritingScenarioListComponent', () => {
  let writingService: jasmine.SpyObj<WritingService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    writingService = jasmine.createSpyObj('WritingService', ['getScenarios']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    writingService.getScenarios.and.returnValue(of(scenarios));

    TestBed.configureTestingModule({
      imports: [WritingScenarioListComponent],
      providers: [
        { provide: WritingService, useValue: writingService },
        { provide: Router, useValue: router },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(WritingScenarioListComponent);
    fixture.detectChanges();
    return fixture;
  }

  // ── Loading and display ───────────────────────────────────────────────────

  it('calls getScenarios on init and renders scenario titles', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(writingService.getScenarios).toHaveBeenCalled();
    expect(fixture.componentInstance.state()).toBe('loaded');
    expect(fixture.nativeElement.textContent).toContain('Follow up on a pending document approval');
    expect(fixture.nativeElement.textContent).toContain('Ask for missing information politely');
  }));

  it('shows learning goal for each scenario', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Learn to follow up professionally.');
    expect(fixture.nativeElement.textContent).toContain('Request missing information diplomatically.');
  }));

  it('shows difficulty label for each scenario', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Intermediate');
    expect(fixture.nativeElement.textContent).toContain('Elementary');
  }));

  it('navigates to exercise route when a scenario is selected', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.selectScenario(scenarios[0]);

    expect(router.navigate).toHaveBeenCalledWith(['/writing/exercise', scenarios[0].id]);
  }));

  it('navigates to dashboard when back button is clicked', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.backToDashboard();

    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  }));

  // ── Error handling ────────────────────────────────────────────────────────

  it('shows error message when getScenarios fails', fakeAsync(() => {
    writingService.getScenarios.and.returnValue(throwError(() => ({
      error: { error: 'Could not load scenarios at this time.' },
    })));

    const fixture = create();
    tick();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('error');
    expect(fixture.nativeElement.textContent).toContain('Could not load scenarios at this time.');
  }));

  // ── difficultyLabel helper ────────────────────────────────────────────────

  it('maps known CEFR codes to readable labels', () => {
    const fixture = create();
    const c = fixture.componentInstance;
    expect(c.difficultyLabel('A1')).toBe('Beginner');
    expect(c.difficultyLabel('A2')).toBe('Elementary');
    expect(c.difficultyLabel('B1')).toBe('Intermediate');
    expect(c.difficultyLabel('B2')).toBe('Upper intermediate');
    expect(c.difficultyLabel('C1')).toBe('Advanced');
    expect(c.difficultyLabel('X9')).toBe('X9');
  });
});

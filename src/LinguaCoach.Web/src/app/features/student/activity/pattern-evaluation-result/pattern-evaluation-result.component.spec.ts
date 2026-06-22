import { TestBed } from '@angular/core/testing';
import { PatternEvaluationResultComponent } from './pattern-evaluation-result.component';
import { PatternEvaluationDto } from '../../../../core/models/activity.models';

function makeResult(overrides: Partial<PatternEvaluationDto> = {}): PatternEvaluationDto {
  return {
    exercisePatternKey: 'gap_fill_workplace_phrase',
    markingMode: 'ExactMatch',
    score: 0,
    maxScore: 5,
    percentage: 0,
    passed: false,
    completed: true,
    coachSummary: null,
    itemResults: [],
    corrections: [],
    suggestedImprovedAnswer: null,
    skillImpacts: [],
    memorySignals: [],
    ...overrides,
  };
}

describe('PatternEvaluationResultComponent â€” score-aware feedback', () => {
  let component: PatternEvaluationResultComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PatternEvaluationResultComponent] });
    const fixture = TestBed.createComponent(PatternEvaluationResultComponent);
    component = fixture.componentInstance;
  });

  // â”€â”€ scoreBandLabel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('returns "Excellent" for 100%', () => {
    component.result = makeResult({ percentage: 100, passed: true });
    expect(component.scoreBandLabel()).toBe('Excellent');
  });

  it('returns "Excellent" for 90%', () => {
    component.result = makeResult({ percentage: 90, passed: true });
    expect(component.scoreBandLabel()).toBe('Excellent');
  });

  it('returns "Good work" for 89%', () => {
    component.result = makeResult({ percentage: 89, passed: true });
    expect(component.scoreBandLabel()).toBe('Good work');
  });

  it('returns "Good work" for 70%', () => {
    component.result = makeResult({ percentage: 70 });
    expect(component.scoreBandLabel()).toBe('Good work');
  });

  it('returns "Keep going" for 69%', () => {
    component.result = makeResult({ percentage: 69 });
    expect(component.scoreBandLabel()).toBe('Keep going');
  });

  it('returns "Keep going" for 40%', () => {
    component.result = makeResult({ percentage: 40 });
    expect(component.scoreBandLabel()).toBe('Keep going');
  });

  it('returns "Needs review" for 39%', () => {
    component.result = makeResult({ percentage: 39 });
    expect(component.scoreBandLabel()).toBe('Needs review');
  });

  it('returns "Needs review" for 0%', () => {
    component.result = makeResult({ percentage: 0 });
    expect(component.scoreBandLabel()).toBe('Needs review');
  });

  // â”€â”€ scoreBandInstruction â€” must not say "Improve your answer" at 100% â”€â”€â”€â”€â”€â”€

  it('does NOT say "Improve your answer" at 100%', () => {
    component.result = makeResult({ percentage: 100, passed: true });
    expect(component.scoreBandInstruction()).not.toContain('Improve your answer');
  });

  it('does NOT say "Improve your answer" at 90%', () => {
    component.result = makeResult({ percentage: 90, passed: true });
    expect(component.scoreBandInstruction()).not.toContain('Improve your answer');
  });

  it('shows positive wording at 100%', () => {
    component.result = makeResult({ percentage: 100, passed: true });
    const instruction = component.scoreBandInstruction();
    expect(instruction.toLowerCase()).toMatch(/next challenge|keep practising|excellent|ready/);
  });

  it('shows positive wording at 90%', () => {
    component.result = makeResult({ percentage: 90, passed: true });
    const instruction = component.scoreBandInstruction();
    expect(instruction.toLowerCase()).toMatch(/next challenge|keep practising|excellent|ready/);
  });

  it('shows minor improvement wording at 75%', () => {
    component.result = makeResult({ percentage: 75 });
    const instruction = component.scoreBandInstruction();
    expect(instruction.toLowerCase()).toMatch(/small|improvement|suggested/);
  });

  it('shows review/retry wording at 50%', () => {
    component.result = makeResult({ percentage: 50 });
    const instruction = component.scoreBandInstruction();
    expect(instruction.toLowerCase()).toMatch(/review|correction/);
  });

  it('shows retry wording at 20%', () => {
    component.result = makeResult({ percentage: 20 });
    const instruction = component.scoreBandInstruction();
    expect(instruction.toLowerCase()).toMatch(/retry|review|correction/);
  });

  // â”€â”€ showImprovementPrompt â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('showImprovementPrompt is false at 100%', () => {
    component.result = makeResult({ percentage: 100, passed: true });
    expect(component.showImprovementPrompt).toBeFalse();
  });

  it('showImprovementPrompt is false at 90%', () => {
    component.result = makeResult({ percentage: 90, passed: true });
    expect(component.showImprovementPrompt).toBeFalse();
  });

  it('showImprovementPrompt is true at 89%', () => {
    component.result = makeResult({ percentage: 89 });
    expect(component.showImprovementPrompt).toBeTrue();
  });

  it('showImprovementPrompt is true at 0%', () => {
    component.result = makeResult({ percentage: 0 });
    expect(component.showImprovementPrompt).toBeTrue();
  });

  // â”€â”€ scoreRingColour â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('uses success colour at 90%+', () => {
    component.result = makeResult({ percentage: 100 });
    expect(component.scoreRingColour()).toContain('success');
  });

  it('uses vocabulary colour at 70â€“89%', () => {
    component.result = makeResult({ percentage: 80 });
    expect(component.scoreRingColour()).toContain('vocabulary');
  });

  it('uses warn colour at 40â€“69%', () => {
    component.result = makeResult({ percentage: 55 });
    expect(component.scoreRingColour()).toContain('warn');
  });

  it('uses speaking colour below 40%', () => {
    component.result = makeResult({ percentage: 30 });
    expect(component.scoreRingColour()).toContain('speaking');
  });

  // â”€â”€ showScoreCard â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('showScoreCard is true when maxScore > 0 and not lesson_reflection', () => {
    component.result = makeResult({ maxScore: 5, percentage: 80 });
    expect(component.showScoreCard).toBeTrue();
  });

  it('showScoreCard is false for lesson_reflection', () => {
    component.result = makeResult({ exercisePatternKey: 'lesson_reflection', maxScore: 5 });
    expect(component.showScoreCard).toBeFalse();
  });
});


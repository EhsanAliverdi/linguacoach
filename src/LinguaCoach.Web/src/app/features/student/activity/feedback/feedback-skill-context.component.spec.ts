import { TestBed } from '@angular/core/testing';
import { FeedbackSkillContextComponent } from './feedback-skill-context.component';

describe('FeedbackSkillContextComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackSkillContextComponent] });
  });

  function create(primarySkill: string | null, exerciseType: string | null = null, difficulty: string | null = null) {
    const fixture = TestBed.createComponent(FeedbackSkillContextComponent);
    fixture.componentInstance.primarySkill = primarySkill;
    fixture.componentInstance.exerciseType = exerciseType;
    fixture.componentInstance.difficulty = difficulty;
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing when all inputs are null', () => {
    const { nativeElement } = create(null, null, null);
    expect(nativeElement.querySelector('[data-testid="feedback-skill-context"]')).toBeNull();
  });

  it('renders context when primarySkill is set', () => {
    const { nativeElement } = create('writing');
    expect(nativeElement.querySelector('[data-testid="feedback-skill-context"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="skill-primary"]').textContent.trim()).toBe('writing');
  });

  it('renders exerciseType badge when set', () => {
    const { nativeElement } = create(null, 'email_reply');
    expect(nativeElement.querySelector('[data-testid="skill-exercise-type"]').textContent.trim()).toBe('email_reply');
  });

  it('renders difficulty badge when set', () => {
    const { nativeElement } = create(null, null, 'B2');
    expect(nativeElement.querySelector('[data-testid="skill-difficulty"]').textContent.trim()).toBe('B2');
  });

  it('renders all badges when all inputs are set', () => {
    const { nativeElement } = create('writing', 'email_reply', 'B1');
    expect(nativeElement.querySelector('[data-testid="skill-primary"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="skill-exercise-type"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="skill-difficulty"]')).toBeTruthy();
  });
});

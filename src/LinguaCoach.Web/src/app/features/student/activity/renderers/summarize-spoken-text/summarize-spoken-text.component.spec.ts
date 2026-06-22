import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SummarizeSpokenTextComponent, SummarizeSpokenTextContent } from './summarize-spoken-text.component';

describe('SummarizeSpokenTextComponent', () => {
  let fixture: ComponentFixture<SummarizeSpokenTextComponent>;
  let component: SummarizeSpokenTextComponent;

  const baseContent: SummarizeSpokenTextContent = {
    learningGoal: 'Summarise spoken updates',
    instructions: 'Listen, then write your summary.',
    scenario: 'A manager leaves a short voice update.',
    audioScript: 'Revenue grew twelve percent this quarter.',
    audioUrl: null,
    prompt: 'Write a summary of 50-70 words in your own words.',
    summaryRequirements: ['Cover the main idea', 'Use your own words'],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SummarizeSpokenTextComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SummarizeSpokenTextComponent);
    component = fixture.componentInstance;
    component.content = { ...baseContent };
    fixture.detectChanges();
  });

  function el(testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  it('renders the audio player, prompt, requirements and textarea', () => {
    expect(el('summarize-spoken-text-audio-player')).toBeTruthy();
    expect(el('summarize-spoken-text-prompt')?.textContent).toContain('50-70 words');
    expect(el('summarize-spoken-text-requirements')?.textContent).toContain('Cover the main idea');
    expect(el('summarize-spoken-text-input')).toBeTruthy();
  });

  it('does not render keyPoints, model summary or success checklist before submit', () => {
    const html: string = fixture.nativeElement.innerHTML;
    expect(html).not.toContain('keyPoints');
    expect(html).not.toContain('modelSummary');
    expect(html).not.toContain('successChecklist');
  });

  it('disables submit until the textarea has content', () => {
    expect(component.canSubmit).toBe(false);
    component.summaryText = '   ';
    expect(component.canSubmit).toBe(false);
    component.summaryText = 'Revenue grew and hiring will increase.';
    expect(component.canSubmit).toBe(true);
  });

  it('emits { summaryText } trimmed on submit', () => {
    let emitted: { summaryText: string } | undefined;
    component.submitted.subscribe(v => (emitted = v));

    component.summaryText = '  My summary.  ';
    component.submit();

    expect(emitted).toEqual({ summaryText: 'My summary.' });
  });

  it('does not emit when summary is empty', () => {
    let emitted = false;
    component.submitted.subscribe(() => (emitted = true));

    component.summaryText = '   ';
    component.submit();

    expect(emitted).toBe(false);
  });
});

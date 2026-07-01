import { TestBed } from '@angular/core/testing';
import { FeedbackAiDisclaimerComponent } from './feedback-ai-disclaimer.component';

describe('FeedbackAiDisclaimerComponent', () => {
  function create(text?: string) {
    const fixture = TestBed.createComponent(FeedbackAiDisclaimerComponent);
    if (text !== undefined) fixture.componentInstance.text = text;
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackAiDisclaimerComponent] });
  });

  it('renders the default disclaimer text', () => {
    const fixture = create();
    const el: HTMLElement = fixture.nativeElement.querySelector('[data-testid="ai-disclaimer"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('AI-assisted');
  });

  it('renders a custom text when provided', () => {
    const fixture = create('Custom disclaimer.');
    const el: HTMLElement = fixture.nativeElement.querySelector('[data-testid="ai-disclaimer"]');
    expect(el.textContent?.trim()).toBe('Custom disclaimer.');
  });
});

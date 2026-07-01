import { TestBed } from '@angular/core/testing';
import { FeedbackSupportLangComponent } from './feedback-support-lang.component';

describe('FeedbackSupportLangComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackSupportLangComponent] });
  });

  function create(text: string | null) {
    const fixture = TestBed.createComponent(FeedbackSupportLangComponent);
    fixture.componentInstance.text = text;
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing when text is null', () => {
    const { nativeElement } = create(null);
    expect(nativeElement.querySelector('[data-testid="feedback-support-lang"]')).toBeNull();
  });

  it('renders toggle button when text is provided', () => {
    const { nativeElement } = create('Some explanation.');
    expect(nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]')).toBeTruthy();
  });

  it('shows "Show" label initially', () => {
    const { nativeElement } = create('Explanation here.');
    const btn: HTMLElement = nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]');
    expect(btn.textContent).toContain('Show');
  });

  it('does not show content until toggle is clicked', () => {
    const { nativeElement } = create('Hidden text.');
    expect(nativeElement.querySelector('[data-testid="support-lang-content"]')).toBeNull();
  });

  it('shows content after toggle click', () => {
    const fixture = create('Visible text.');
    fixture.nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]').click();
    fixture.detectChanges();
    const content: HTMLElement = fixture.nativeElement.querySelector('[data-testid="support-lang-content"]');
    expect(content).toBeTruthy();
    expect(content.textContent).toContain('Visible text.');
  });

  it('hides content after second click', () => {
    const fixture = create('Toggle me.');
    const btn: HTMLElement = fixture.nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]');
    btn.click();
    fixture.detectChanges();
    btn.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="support-lang-content"]')).toBeNull();
  });

  it('label text is generic — does not mention a specific language', () => {
    const { nativeElement } = create('کمک فارسی');
    const btn: HTMLElement = nativeElement.querySelector('[data-testid="btn-support-lang-toggle"]');
    expect(btn.textContent?.toLowerCase()).not.toContain('persian');
    expect(btn.textContent?.toLowerCase()).not.toContain('فارسی');
  });
});

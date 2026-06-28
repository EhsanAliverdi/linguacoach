import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SummarizeGroupDiscussionComponent, SummarizeGroupDiscussionContent } from './summarize-group-discussion.component';

const MOCK_CONTENT: SummarizeGroupDiscussionContent = {
  learningGoal: 'Practise summarizing a group discussion',
  instructions: 'Read the discussion and write a summary.',
  scenario: 'Two students plan a weekend trip.',
  items: [
    {
      id: 'disc1',
      discussionTitle: 'Planning a Weekend Trip',
      discussionTopic: 'Choosing a destination for the weekend',
      audioScript: 'Ali: I think we should go to the mountains. Sara: I prefer the beach. Ali: Let\'s vote.',
      audioUrl: null,
      contextLabel: 'Social',
      speakers: [
        { name: 'Ali', role: 'Student', viewpoint: null },
        { name: 'Sara', role: 'Student', viewpoint: null },
      ],
      focusAreas: ['main points', 'speaker views', 'outcome'],
    },
  ],
};

describe('SummarizeGroupDiscussionComponent', () => {
  let fixture: ComponentFixture<SummarizeGroupDiscussionComponent>;
  let component: SummarizeGroupDiscussionComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SummarizeGroupDiscussionComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SummarizeGroupDiscussionComponent);
    component = fixture.componentInstance;
    component.content = MOCK_CONTENT;
    fixture.detectChanges();
  });

  it('renders the discussion title', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-title-disc1"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('Planning a Weekend Trip');
  });

  it('renders the discussion topic', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-topic-disc1"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('Choosing a destination');
  });

  it('shows unavailable state with audio script when audioUrl is null', () => {
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable).toBeTruthy();
    expect(unavailable.textContent).toContain('Ali');
  });

  it('does not show native audio player when audioUrl is null', () => {
    const playerSection = fixture.nativeElement.querySelector('[data-testid="audio-player-section"]');
    expect(playerSection).toBeFalsy();
  });

  it('renders speaker list when speakers are present', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-speakers-disc1"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('Ali');
    expect(el.textContent).toContain('Sara');
  });

  it('renders context label', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-context-label-disc1"]');
    expect(el).toBeTruthy();
    expect(el.textContent).toContain('Social');
  });

  it('renders text input for student response', () => {
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-input-disc1"]');
    expect(el).toBeTruthy();
    expect(el.tagName.toLowerCase()).toBe('textarea');
  });

  it('submit button is disabled when no input', () => {
    const btn = fixture.nativeElement.querySelector('[data-testid="summarize-group-discussion-submit-btn"]');
    expect(btn.disabled).toBeTrue();
  });

  it('submit button enables after typing', async () => {
    component.onInput('disc1', 'Ali wanted mountains, Sara preferred beach.');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="summarize-group-discussion-submit-btn"]');
    expect(btn.disabled).toBeFalse();
  });

  it('emits items keyed by itemId on submit', () => {
    component.onInput('disc1', 'Ali wanted mountains, Sara preferred beach. They voted.');
    const emitted: any[] = [];
    component.submitted.subscribe(v => emitted.push(v));
    component.submit();
    expect(emitted.length).toBe(1);
    expect(emitted[0].items[0].itemId).toBe('disc1');
    expect(emitted[0].items[0].answerText).toContain('mountains');
  });

  it('does not emit when all inputs are blank', () => {
    const emitted: any[] = [];
    component.submitted.subscribe(v => emitted.push(v));
    component.submit();
    expect(emitted.length).toBe(0);
  });

  it('disabled prop disables textarea', async () => {
    component.disabled = true;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="sgd-input-disc1"]');
    expect(el.disabled).toBeTrue();
  });

  it('renders audio player section when audioUrl is provided', async () => {
    component.content = {
      ...MOCK_CONTENT,
      items: [{ ...MOCK_CONTENT.items[0], audioUrl: 'https://example.com/audio.mp3' }],
    };
    fixture.detectChanges();
    const playerSection = fixture.nativeElement.querySelector('[data-testid="audio-player-section"]');
    expect(playerSection).toBeTruthy();
  });

  it('does not show unavailable state when audioUrl is provided', async () => {
    component.content = {
      ...MOCK_CONTENT,
      items: [{ ...MOCK_CONTENT.items[0], audioUrl: 'https://example.com/audio.mp3' }],
    };
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable).toBeFalsy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AudioPlayerComponent } from './audio-player.component';

describe('AudioPlayerComponent', () => {
  let fixture: ComponentFixture<AudioPlayerComponent>;
  let component: AudioPlayerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AudioPlayerComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AudioPlayerComponent);
    component = fixture.componentInstance;
  });

  // --- Available state ---

  it('shows native audio player when audioUrl is set', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    const section = fixture.nativeElement.querySelector('[data-testid="audio-player-section"]');
    expect(section).toBeTruthy();
    const audio = fixture.nativeElement.querySelector('[data-testid="audio-player"]');
    expect(audio).toBeTruthy();
  });

  it('shows label above audio player', () => {
    component.audioUrl = '/api/activity/abc/audio';
    component.label = 'Lecture Audio';
    fixture.detectChanges();
    const section = fixture.nativeElement.querySelector('[data-testid="audio-player-section"]');
    expect(section.textContent).toContain('Lecture Audio');
  });

  it('shows helpText below audio player when provided', () => {
    component.audioUrl = '/api/activity/abc/audio';
    component.helpText = 'Listen carefully.';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Listen carefully.');
  });

  // --- Unavailable state ---

  it('shows unavailable state when audioUrl is null', () => {
    component.audioUrl = null;
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable).toBeTruthy();
    const section = fixture.nativeElement.querySelector('[data-testid="audio-player-section"]');
    expect(section).toBeFalsy();
  });

  it('shows default unavailable message when no message provided', () => {
    component.audioUrl = null;
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable.textContent).toContain('temporarily unavailable');
  });

  it('shows custom unavailable message when provided', () => {
    component.audioUrl = null;
    component.audioUnavailableMessage = 'Custom unavailability reason.';
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable.textContent).toContain('Custom unavailability reason.');
  });

  it('shows audioScript in unavailable state when provided', () => {
    component.audioUrl = null;
    component.audioScript = 'Read this sentence aloud.';
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable.textContent).toContain('Read this sentence aloud.');
  });

  it('shows pending message when audioStatus is pending', () => {
    component.audioUrl = null;
    component.audioStatus = 'pending';
    fixture.detectChanges();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="audio-unavailable"]');
    expect(unavailable.textContent).toContain('being prepared');
  });

  // --- Loading state ---

  it('shows loading indicator after loadstart fires', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onLoadStart();
    fixture.detectChanges();
    const loading = fixture.nativeElement.querySelector('[data-testid="audio-loading"]');
    expect(loading).toBeTruthy();
    expect(loading.textContent).toContain('Loading');
  });

  it('hides loading indicator when audio is ready', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onLoadStart();
    fixture.detectChanges();
    component.onCanPlay();
    fixture.detectChanges();
    const loading = fixture.nativeElement.querySelector('[data-testid="audio-loading"]');
    expect(loading).toBeFalsy();
  });

  // --- Failed state ---

  it('shows failed state when audio error fires', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    const failed = fixture.nativeElement.querySelector('[data-testid="audio-failed"]');
    expect(failed).toBeTruthy();
    expect(failed.textContent).toContain('could not be loaded');
  });

  it('shows retry button in failed state', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    const retryBtn = fixture.nativeElement.querySelector('[data-testid="audio-retry-btn"]');
    expect(retryBtn).toBeTruthy();
  });

  it('shows audioScript as fallback in failed state when provided', () => {
    component.audioUrl = '/api/activity/abc/audio';
    component.audioScript = 'Fallback script text.';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fallback script text.');
  });

  it('does not show native audio player in failed state', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    const audio = fixture.nativeElement.querySelector('[data-testid="audio-player"]');
    expect(audio).toBeFalsy();
  });

  // --- Retry ---

  it('clears failed state and increments retryKey on retry', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    const keyBefore = component.retryKey;
    component.retry();
    fixture.detectChanges();
    expect(component.audioState).toBe('idle');
    expect(component.retryKey).toBe(keyBefore + 1);
  });

  it('shows native audio player again after retry', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    component.retry();
    fixture.detectChanges();
    const audio = fixture.nativeElement.querySelector('[data-testid="audio-player"]');
    expect(audio).toBeTruthy();
  });

  it('hides failed state after retry', () => {
    component.audioUrl = '/api/activity/abc/audio';
    fixture.detectChanges();
    component.onError();
    fixture.detectChanges();
    component.retry();
    fixture.detectChanges();
    const failed = fixture.nativeElement.querySelector('[data-testid="audio-failed"]');
    expect(failed).toBeFalsy();
  });
});

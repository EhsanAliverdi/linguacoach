import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AudioResponseComponent, AudioResponseContent } from './audio-response.component';
import { RecordedAudio } from '../voice-recorder/voice-recorder.component';

const CONTENT: AudioResponseContent = {
  prompt: 'Describe your morning routine.',
  situation: 'You are in a language lesson.',
};

describe('AudioResponseComponent', () => {
  let fixture: ComponentFixture<AudioResponseComponent>;
  let component: AudioResponseComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AudioResponseComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(AudioResponseComponent);
    component = fixture.componentInstance;
    component.content = CONTENT;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not show submit button before recording', () => {
    const btn = fixture.nativeElement.querySelector('[data-testid="submit-audio-btn"]');
    expect(btn).toBeNull();
  });

  it('should show submit button after recording', () => {
    const audio: RecordedAudio = {
      blob: new Blob(['audio'], { type: 'audio/webm' }),
      mimeType: 'audio/webm',
      durationSeconds: 3,
      previewUrl: 'blob:fake',
    };
    component.onRecorded(audio);
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('[data-testid="submit-audio-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should emit submitted with blob when submit() called after recording', () => {
    const emitted: any[] = [];
    component.submitted.subscribe(v => emitted.push(v));

    const audio: RecordedAudio = {
      blob: new Blob(['audio'], { type: 'audio/webm' }),
      mimeType: 'audio/webm',
      durationSeconds: 5.2,
      previewUrl: 'blob:fake',
    };
    component.onRecorded(audio);
    component.submit();

    expect(emitted.length).toBe(1);
    expect(emitted[0].mimeType).toBe('audio/webm');
    expect(emitted[0].durationSeconds).toBeCloseTo(5.2);
    expect(emitted[0].blob).toBe(audio.blob);
  });

  it('should not emit when submit() called without recording', () => {
    const emitted: any[] = [];
    component.submitted.subscribe(v => emitted.push(v));

    component.submit();

    expect(emitted.length).toBe(0);
  });

  it('should not emit when disabled', () => {
    const emitted: any[] = [];
    component.submitted.subscribe(v => emitted.push(v));
    component.disabled = true;

    const audio: RecordedAudio = {
      blob: new Blob(['audio'], { type: 'audio/webm' }),
      mimeType: 'audio/webm',
      durationSeconds: 2,
      previewUrl: 'blob:fake',
    };
    component.onRecorded(audio);
    component.submit();

    expect(emitted.length).toBe(0);
  });

  it('should show submit button label as "Submitting…" when disabled after recording', () => {
    const audio: RecordedAudio = {
      blob: new Blob(['audio'], { type: 'audio/webm' }),
      mimeType: 'audio/webm',
      durationSeconds: 1,
      previewUrl: 'blob:fake',
    };
    component.onRecorded(audio);
    component.disabled = true;
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('[data-testid="submit-audio-btn"]');
    expect(btn?.textContent?.trim()).toBe('Submitting…');
  });
});

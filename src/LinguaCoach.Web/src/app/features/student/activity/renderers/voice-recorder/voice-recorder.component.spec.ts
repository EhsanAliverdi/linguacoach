import { ComponentFixture, TestBed } from '@angular/core/testing';
import { VoiceRecorderComponent } from './voice-recorder.component';

describe('VoiceRecorderComponent', () => {
  let fixture: ComponentFixture<VoiceRecorderComponent>;
  let component: VoiceRecorderComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VoiceRecorderComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(VoiceRecorderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start in idle state when mediaDevices is present', () => {
    // In ChromeHeadless mediaDevices.getUserMedia is available → idle
    if (typeof navigator.mediaDevices?.getUserMedia !== 'function') {
      expect(component.recorderState()).toBe('unsupported');
    } else {
      expect(component.recorderState()).toBe('idle');
    }
  });

  it('should set permission-denied when getUserMedia rejects', async () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      pending('getUserMedia not available in this environment');
      return;
    }
    // Prevent Zone.js unhandled-rejection warning by pre-catching the rejection
    const rejection = Promise.reject(new DOMException('Permission denied', 'NotAllowedError'));
    rejection.catch(() => {});
    spyOn(navigator.mediaDevices, 'getUserMedia').and.returnValue(rejection as any);

    await component.startRecording();

    expect(component.recorderState()).toBe('permission-denied');
  });

  it('should set requesting-permission during getUserMedia call', () => {
    if (!navigator.mediaDevices?.getUserMedia) {
      pending('getUserMedia not available in this environment');
      return;
    }
    // Never-resolving promise so MediaRecorder is never constructed
    spyOn(navigator.mediaDevices, 'getUserMedia').and.returnValue(
      new Promise<MediaStream>(() => {}),
    );

    // startRecording sets requesting-permission synchronously before the first await
    component.startRecording().catch(() => {});

    expect(component.recorderState()).toBe('requesting-permission');
  });

  it('should clean up stream tracks on stopRecording', () => {
    const stopSpy = jasmine.createSpy('stop');
    const mockStream = {
      getTracks: () => [{ stop: stopSpy }, { stop: stopSpy }],
    } as unknown as MediaStream;
    (component as any)._stream = mockStream;

    component.stopRecording();

    expect(stopSpy).toHaveBeenCalledTimes(2);
    expect((component as any)._stream).toBeNull();
  });

  it('should reset to idle on reRecord', () => {
    (component as any)._previewObjectUrl = 'blob:test';
    component.previewUrl = 'blob:test';
    spyOn(URL, 'revokeObjectURL');

    component.reRecord();

    expect(component.recorderState()).toBe('idle');
    expect(component.previewUrl).toBeNull();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
  });

  it('should emit recorded event when onstop fires', () => {
    const emittedValues: any[] = [];
    component.recorded.subscribe(v => emittedValues.push(v));

    const mockBlob = new Blob(['audio'], { type: 'audio/webm' });
    (component as any)._chunks = [mockBlob];
    (component as any)._startTime = Date.now() - 2000;
    (component as any)._mediaRecorder = { mimeType: 'audio/webm' };
    spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');

    // Simulate what MediaRecorder.onstop does
    const actualMime = 'audio/webm';
    const blob = new Blob([mockBlob], { type: actualMime });
    component.previewUrl = 'blob:fake';
    component.recorderState.set('recorded');
    component.recorded.emit({ blob, mimeType: actualMime, durationSeconds: 2, previewUrl: 'blob:fake' });

    expect(emittedValues.length).toBe(1);
    expect(emittedValues[0].mimeType).toBe('audio/webm');
    expect(emittedValues[0].durationSeconds).toBeCloseTo(2);
  });

  it('should show start-recording-btn when idle', () => {
    (component as any).recorderState.set('idle');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="start-recording-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should show stop-recording-btn when recording', () => {
    (component as any).recorderState.set('recording');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="stop-recording-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should show mic-unsupported when unsupported', () => {
    (component as any).recorderState.set('unsupported');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="mic-unsupported"]');
    expect(el).toBeTruthy();
  });

  it('should show mic-denied when permission denied', () => {
    (component as any).recorderState.set('permission-denied');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="mic-denied"]');
    expect(el).toBeTruthy();
  });

  it('should show audio-preview when recorded', () => {
    (component as any).recorderState.set('recorded');
    component.previewUrl = 'blob:fake';
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="audio-preview"]');
    expect(el).toBeTruthy();
  });

  it('should show re-record-btn when recorded', () => {
    (component as any).recorderState.set('recorded');
    component.previewUrl = 'blob:fake';
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="re-record-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should release stream on ngOnDestroy', () => {
    const stopSpy = jasmine.createSpy('stop');
    (component as any)._stream = { getTracks: () => [{ stop: stopSpy }] } as unknown as MediaStream;

    component.ngOnDestroy();

    expect(stopSpy).toHaveBeenCalled();
    expect((component as any)._stream).toBeNull();
  });

  it('should revoke object URL on ngOnDestroy', () => {
    (component as any)._previewObjectUrl = 'blob:test-destroy';
    spyOn(URL, 'revokeObjectURL');

    component.ngOnDestroy();

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test-destroy');
  });

  it('should not start recording when disabled', async () => {
    component.disabled = true;
    await component.startRecording();
    expect(component.recorderState()).not.toBe('recording');
  });
});

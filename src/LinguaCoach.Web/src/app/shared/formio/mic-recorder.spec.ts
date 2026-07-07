import { MicRecorder, MicRecorderState } from './mic-recorder';

class FakeMediaRecorder {
  static isTypeSupported(type: string): boolean {
    return type === 'audio/webm;codecs=opus';
  }

  state: 'inactive' | 'recording' = 'inactive';
  mimeType: string;
  ondataavailable: ((e: { data: Blob }) => void) | null = null;
  onstop: (() => void) | null = null;

  constructor(public stream: MediaStream, options?: { mimeType?: string }) {
    this.mimeType = options?.mimeType ?? 'audio/webm';
  }

  start(): void {
    this.state = 'recording';
  }

  stop(): void {
    this.state = 'inactive';
    this.ondataavailable?.({ data: new Blob(['x'], { type: this.mimeType }) });
    this.onstop?.();
  }
}

function fakeStream(): MediaStream {
  const track = { stop: jasmine.createSpy('stop') };
  return { getTracks: () => [track] } as unknown as MediaStream;
}

describe('MicRecorder', () => {
  let originalMediaRecorder: any;
  let originalMediaDevices: any;

  function setMediaDevices(value: any): void {
    // navigator.mediaDevices is a getter-only accessor on the real Navigator prototype in
    // headless Chrome — plain assignment throws; redefine the own property instead.
    Object.defineProperty(navigator, 'mediaDevices', { value, configurable: true, writable: true });
  }

  beforeEach(() => {
    originalMediaRecorder = (window as any).MediaRecorder;
    originalMediaDevices = (navigator as any).mediaDevices;
    (window as any).MediaRecorder = FakeMediaRecorder;
    setMediaDevices({
      getUserMedia: jasmine.createSpy('getUserMedia').and.returnValue(Promise.resolve(fakeStream())),
    });
    (URL as any).createObjectURL = jasmine.createSpy('createObjectURL').and.returnValue('blob:fake');
    (URL as any).revokeObjectURL = jasmine.createSpy('revokeObjectURL');
  });

  afterEach(() => {
    (window as any).MediaRecorder = originalMediaRecorder;
    setMediaDevices(originalMediaDevices);
  });

  function makeSut() {
    const states: MicRecorderState[] = [];
    const recordedCalls: any[] = [];
    const recorder = new MicRecorder(
      (s) => states.push(s),
      (audio) => recordedCalls.push(audio),
    );
    return { recorder, states, recordedCalls };
  }

  it('starts in idle when getUserMedia/MediaRecorder are supported', () => {
    const { recorder } = makeSut();
    expect(recorder.state).toBe('idle');
  });

  it('starts in unsupported when getUserMedia is missing', () => {
    setMediaDevices(undefined);
    const { recorder } = makeSut();
    expect(recorder.state).toBe('unsupported');
  });

  it('start() requests permission then transitions to recording', async () => {
    const { recorder, states } = makeSut();
    await recorder.start();
    expect(states).toEqual(['requesting-permission', 'recording']);
    expect(recorder.state).toBe('recording');
  });

  it('start() transitions to permission-denied when getUserMedia rejects', async () => {
    (navigator as any).mediaDevices.getUserMedia = jasmine.createSpy('getUserMedia')
      .and.returnValue(Promise.reject(new Error('denied')));
    const { recorder, states } = makeSut();
    await recorder.start();
    expect(states).toEqual(['requesting-permission', 'permission-denied']);
  });

  it('stop() transitions to recorded and emits the recorded audio with mimeType/duration/previewUrl', async () => {
    const { recorder, states, recordedCalls } = makeSut();
    await recorder.start();
    recorder.stop();

    expect(states).toEqual(['requesting-permission', 'recording', 'recorded']);
    expect(recorder.state).toBe('recorded');
    expect(recordedCalls.length).toBe(1);
    expect(recordedCalls[0].mimeType).toBe('audio/webm;codecs=opus');
    expect(recordedCalls[0].previewUrl).toBe('blob:fake');
    expect(recordedCalls[0].blob instanceof Blob).toBeTrue();
  });

  it('stop() releases the microphone stream tracks', async () => {
    const track = { stop: jasmine.createSpy('stop') };
    (navigator as any).mediaDevices.getUserMedia = jasmine.createSpy('getUserMedia')
      .and.returnValue(Promise.resolve({ getTracks: () => [track] }));
    const { recorder } = makeSut();
    await recorder.start();
    recorder.stop();
    expect(track.stop).toHaveBeenCalled();
  });

  it('reset() revokes the preview URL and returns to idle', async () => {
    const { recorder, states } = makeSut();
    await recorder.start();
    recorder.stop();
    recorder.reset();

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:fake');
    expect(states[states.length - 1]).toBe('idle');
    expect(recorder.state).toBe('idle');
  });

  it('destroy() stops an in-progress recording and releases the stream', async () => {
    const track = { stop: jasmine.createSpy('stop') };
    (navigator as any).mediaDevices.getUserMedia = jasmine.createSpy('getUserMedia')
      .and.returnValue(Promise.resolve({ getTracks: () => [track] }));
    const { recorder } = makeSut();
    await recorder.start();
    recorder.destroy();
    expect(track.stop).toHaveBeenCalled();
  });
});

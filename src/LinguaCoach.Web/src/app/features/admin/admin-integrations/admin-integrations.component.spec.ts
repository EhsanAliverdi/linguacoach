import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminIntegrationsComponent } from './admin-integrations.component';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
} from '../../../core/services/admin-integrations.service';

const STORAGE: StorageSettings = {
  provider: 'minio',
  endpoint: 'http://minio:9000',
  bucketName: 'speakpath',
  accessKey: 'configured',
  secretKey: 'configured',
  useSsl: false,
  signedUrlExpiryMinutes: 60,
};

const STORAGE_TEST_OK: StorageTestResult = { ok: true, lastCheckedUtc: '2026-06-19T10:00:00Z', error: null };

function makeSvc() {
  return {
    getStorage: jasmine.createSpy('getStorage').and.returnValue(of(STORAGE)),
    testStorage: jasmine.createSpy('testStorage').and.returnValue(of(STORAGE_TEST_OK)),
  };
}

describe('AdminIntegrationsComponent', () => {
  let fixture: ComponentFixture<AdminIntegrationsComponent>;
  let component: AdminIntegrationsComponent;
  let svc: ReturnType<typeof makeSvc>;

  async function setup() {
    svc = makeSvc();
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [provideRouter([]), { provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Integrations');
  });

  it('calls getStorage on init', async () => {
    await setup();
    expect(svc.getStorage).toHaveBeenCalledTimes(1);
  });

  it('renders the integration cards grid', async () => {
    await setup();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Object Storage');
    expect(text).toContain('SMTP / Email');
    expect(text).toContain('Webhook');
    expect(text).toContain('Analytics');
    expect(text).toContain('Admin API');
  });

  it('shows connected badge when storage credentials present', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Connected');
  });

  it('shows credentials-missing badge when keys absent', async () => {
    svc = makeSvc();
    svc.getStorage.and.returnValue(of({ ...STORAGE, accessKey: null, secretKey: null }));
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [provideRouter([]), { provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Credentials missing');
  });

  it('calls testStorage on testConnection', fakeAsync(async () => {
    await setup();
    component.testConnection();
    tick();
    expect(svc.testStorage).toHaveBeenCalledTimes(1);
    expect(component.storageTest()?.ok).toBeTrue();
  }));

  it('sets storageTest to failed result on test error', fakeAsync(async () => {
    await setup();
    svc.testStorage.and.returnValue(throwError(() => ({ error: { error: 'Connection refused' } })));
    component.testConnection();
    tick();
    expect(component.storageTest()?.ok).toBeFalse();
  }));

  it('shows error state when storage fails to load', async () => {
    svc = makeSvc();
    svc.getStorage.and.returnValue(throwError(() => ({ error: { error: 'Storage down' } })));
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [provideRouter([]), { provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.storageError()).toContain('Storage down');
  });

  it('does not render lesson buffer or background jobs sections', async () => {
    await setup();
    const text = fixture.nativeElement.textContent;
    expect(text).not.toContain('Lesson Buffer Settings');
    expect(text).not.toContain('Background Jobs');
    expect(text).not.toContain('Recent batches');
    expect(text).not.toContain('Readiness pool');
  });
});

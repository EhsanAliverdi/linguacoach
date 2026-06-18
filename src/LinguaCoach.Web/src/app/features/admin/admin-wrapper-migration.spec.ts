import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { of } from 'rxjs';
import {
  SpAdminLayoutComponent,
  SpAdminSidebarComponent,
  SpAdminHeaderComponent,
  SpAdminToastOutletComponent,
} from '../../admin';
import { AuthService } from '../../core/services/auth.service';
import { signal } from '@angular/core';

@Component({
  standalone: true,
  imports: [
    RouterLink, RouterLinkActive, RouterOutlet,
    SpAdminLayoutComponent, SpAdminSidebarComponent,
    SpAdminHeaderComponent, SpAdminToastOutletComponent,
  ],
  template: `
    <sp-admin-layout [collapsed]="false">
      <sp-admin-sidebar slot="sidebar" [collapsed]="false">
        <nav>
          <a routerLink="/admin" routerLinkActive="sp-admin-nav-item-active" class="sp-admin-nav-item">Dashboard</a>
          <a routerLink="/admin/students" routerLinkActive="sp-admin-nav-item-active" class="sp-admin-nav-item">Students</a>
        </nav>
      </sp-admin-sidebar>
      <sp-admin-header slot="header">
        <button class="sp-admin-hamburger">Menu</button>
        <button class="sp-layout-header-action">Toggle</button>
        <div class="sp-admin-header-user">
          <button class="sp-admin-avatar">A</button>
        </div>
      </sp-admin-header>
      <div class="sp-admin-content-body">Page content</div>
      <sp-admin-toast-outlet />
    </sp-admin-layout>
  `,
})
class ShellTestHostComponent {}
import { AdminAiConfigComponent } from './admin-ai-config/admin-ai-config.component';
import { AdminAiUsageComponent } from './admin-ai-usage/admin-ai-usage.component';
import { AdminDashboardComponent } from './admin-dashboard/admin-dashboard.component';
import { AdminDiagnosticsComponent } from './admin-diagnostics/admin-diagnostics.component';
import { AdminIntegrationsComponent } from './admin-integrations/admin-integrations.component';
import { AdminPromptsComponent } from './admin-prompts/admin-prompts.component';
import { AdminStudentsComponent } from './admin-students/admin-students.component';
import { AdminApiService } from '../../core/services/admin.api.service';
import { AiUsageService } from '../../core/services/ai-usage.service';
import { DiagnosticsService } from '../../core/services/diagnostics.service';
import { AdminIntegrationsService } from '../../core/services/admin-integrations.service';
import { ToastService } from '../../core/services/toast.service';

function query(host: HTMLElement, selector: string): Element | null {
  return host.querySelector(selector);
}

describe('admin wrapper migration', () => {
  it('dashboard renders with admin wrapper components', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents', 'getStats']);
    adminApi.listStudents.and.returnValue(of([]));
    adminApi.getStats.and.returnValue(of({ totalActivityAttempts: 0 }));

    TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
  });

  it('students page renders filter and pagination wrappers', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of([]));
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);

    TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: adminApi },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(query(fixture.nativeElement, 'sp-admin-filter-bar')).toBeTruthy();
  });

  it('AI Config page renders with wrapper cards', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listAiCategories', 'listAiProviders']);
    adminApi.listAiCategories.and.returnValue(of([
      { categoryKey: 'llm.default', displayName: 'Default LLM', providerName: 'fake', modelName: null, voiceName: null },
      { categoryKey: 'tts.listening', displayName: 'Listening TTS', providerName: 'fake', modelName: null, voiceName: null },
    ]));
    adminApi.listAiProviders.and.returnValue(of([
      { providerName: 'fake', hasApiKey: false, apiEndpoint: null, models: ['fake'], modelTests: [] },
    ]));

    TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminAiConfigComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('sp-admin-card').length).toBeGreaterThanOrEqual(3);
  });

  it('AI Usage page renders table and card wrappers', () => {
    const svc = jasmine.createSpyObj('AiUsageService', ['getSummary', 'getRecent']);
    svc.getSummary.and.returnValue(of({
      totalCalls: 1,
      successfulCalls: 1,
      failedCalls: 0,
      fallbackCalls: 0,
      successRate: 100,
      totalInputTokens: 1,
      totalOutputTokens: 1,
      totalCostUsd: 0,
      byProvider: [],
      byFeature: [],
    }));
    svc.getRecent.and.returnValue(of({ items: [] }));

    TestBed.configureTestingModule({
      imports: [AdminAiUsageComponent],
      providers: [{ provide: AiUsageService, useValue: svc }],
    });

    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(query(fixture.nativeElement, 'sp-admin-card')).toBeTruthy();
  });

  it('Prompts page renders wrapper table actions', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listPrompts']);
    adminApi.listPrompts.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminPromptsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(query(fixture.nativeElement, 'sp-admin-empty-state')).toBeTruthy();
  });

  it('Integrations page renders wrapper cards', () => {
    const svc = jasmine.createSpyObj('AdminIntegrationsService', ['getStorage', 'getGenerationSettings', 'getBatches']);
    svc.getStorage.and.returnValue(of({ provider: 'none', endpoint: null, bucketName: null, accessKey: null, secretKey: null, useSsl: false, signedUrlExpiryMinutes: 15 }));
    svc.getGenerationSettings.and.returnValue(of({
      readyLessonBufferSize: 5,
      refillThreshold: 2,
      refillBatchSize: 2,
      maxGenerationAttempts: 2,
      generationTimeoutSeconds: 60,
      ttsTimeoutSeconds: 30,
      maxConcurrentGenerationJobs: 1,
      maxConcurrentTtsJobs: 1,
      practiceGymReadyExercisesPerType: 2,
      practiceGymRefillThresholdPerType: 1,
      practiceGymRefillCountPerType: 1,
      enableBackgroundGeneration: true,
      enableTtsGeneration: false,
    }));
    svc.getBatches.and.returnValue(of({
      summary: { queued: 0, running: 0, failed: 0, lastSuccessfulGenerationUtc: null },
      readyBufferPerStudent: [],
      batches: [],
    }));

    TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    });

    const fixture = TestBed.createComponent(AdminIntegrationsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('sp-admin-card').length).toBeGreaterThanOrEqual(3);
  });

  it('dashboard renders KPI grid cards', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents', 'getStats']);
    adminApi.listStudents.and.returnValue(of([]));
    adminApi.getStats.and.returnValue(of({ totalActivityAttempts: 42 }));

    TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();

    const kpiGrid = fixture.nativeElement.querySelector('.sp-admin-kpi-grid');
    expect(kpiGrid).not.toBeNull();
    // Phase 10X-G: KPI cards now use the sp-admin-stat-card wrapper.
    const kpiCards = fixture.nativeElement.querySelectorAll('sp-admin-stat-card');
    expect(kpiCards.length).toBeGreaterThanOrEqual(4);
  });

  it('Diagnostics page still renders wrapper header', () => {
    const svc = jasmine.createSpyObj('DiagnosticsService', ['getStatus', 'getEvents']);
    svc.getStatus.and.returnValue(of({
      environment: 'Test',
      version: '1',
      uptimeSeconds: 1,
      logLevel: 'Information',
      database: { reachable: true, latencyMs: 1 },
      ai: { providerConfigured: false, activeProvider: null, activeModel: null },
      diagnosticEventsEnabled: true,
      diagnosticEventCount: 0,
      serverTimeUtc: new Date().toISOString(),
    }));
    svc.getEvents.and.returnValue(of({ items: [], total: 0 }));

    TestBed.configureTestingModule({
      imports: [AdminDiagnosticsComponent],
      providers: [{ provide: DiagnosticsService, useValue: svc }],
    });

    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
  });
});

describe('admin shell visual structure (10X-C)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('renders shell with sidebar, header and content slots', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sp-admin-shell')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-sidebar')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-header')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-main')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-content')).not.toBeNull();
  });

  it('sidebar is not collapsed by default', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sp-admin-sidebar');
    expect(sidebar.classList).not.toContain('sp-sidebar-collapsed');
  });

  it('header renders hamburger and desktop toggle buttons', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sp-admin-hamburger')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-layout-header-action')).not.toBeNull();
  });

  it('header renders avatar button in user zone', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sp-admin-header-user')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-avatar')).not.toBeNull();
  });

  it('main area is not collapsed when layout collapsed=false', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('.sp-admin-main');
    expect(main).not.toBeNull();
    expect(main.classList).not.toContain('sp-main-collapsed');
  });

  it('nav items are present in sidebar', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const navItems = fixture.nativeElement.querySelectorAll('.sp-admin-nav-item');
    expect(navItems.length).toBeGreaterThanOrEqual(2);
  });
});

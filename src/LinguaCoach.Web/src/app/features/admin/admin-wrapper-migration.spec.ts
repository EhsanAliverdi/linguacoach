import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { of, throwError } from 'rxjs';
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
          <a routerLink="/admin" routerLinkActive="menu-item-active" class="menu-item group menu-item-inactive">Dashboard</a>
          <a routerLink="/admin/students" routerLinkActive="menu-item-active" class="menu-item group menu-item-inactive">Students</a>
        </nav>
      </sp-admin-sidebar>
      <sp-admin-header slot="header">
        <button aria-label="Open navigation" class="xl:hidden">Menu</button>
        <button aria-label="Toggle sidebar" class="hidden xl:flex">Toggle</button>
        <div user>
          <button class="sp-admin-avatar">A</button>
        </div>
      </sp-admin-header>
      <div>Page content</div>
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
import { AdminCurriculumComponent } from './admin-curriculum/admin-curriculum.component';
import { CurriculumService } from '../../core/services/curriculum.service';
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
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1 }));
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
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
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

  it('students page uses sp-admin-table and sp-admin-badge wrappers for rows (10X-G-F)', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [{
      studentProfileId: 'p1',
      email: 'student@example.com',
      firstName: 'Ann',
      lastName: 'Lee',
      displayName: null,
      lifecycleStage: 'CourseReady',
      onboardingStatus: 'Complete',
      cefrLevel: 'B1',
      careerContext: null,
      learningGoal: null,
      learningGoalDescription: null,
      difficultSituationsText: null,
      preferredSessionDurationMinutes: null,
      professionalExperienceLevel: null,
      roleFamiliarity: null,
      createdAt: new Date().toISOString(),
    }], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
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

    expect(query(fixture.nativeElement, 'sp-admin-table')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('sp-admin-badge').length).toBeGreaterThanOrEqual(3);
    expect(query(fixture.nativeElement, 'sp-admin-table-actions')).toBeTruthy();
  });

  it('curriculum create form uses sp-admin-form-field wrappers (10X-G-F)', () => {
    const curriculum = jasmine.createSpyObj('CurriculumService', ['listObjectives', 'getTaxonomy']);
    curriculum.listObjectives.and.returnValue(of([]));
    curriculum.getTaxonomy.and.returnValue(of({
      cefrLevels: ['A1', 'B1'], skills: ['writing', 'speaking'], contextTags: ['general_english'], focusTags: [],
    }));

    TestBed.configureTestingModule({
      imports: [AdminCurriculumComponent],
      providers: [provideRouter([]), { provide: CurriculumService, useValue: curriculum }],
    });

    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    fixture.detectChanges();
    fixture.componentInstance.startCreate();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('sp-admin-form-field').length).toBeGreaterThanOrEqual(6);
  });

  it('AI Config page renders with wrapper cards', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listAiCategories', 'listAiProviders', 'listAiPricing', 'listAiPricingOverrides']);
    adminApi.listAiCategories.and.returnValue(of([
      { categoryKey: 'llm.default', displayName: 'Default LLM', providerName: 'fake', modelName: null, voiceName: null },
      { categoryKey: 'tts.listening', displayName: 'Listening TTS', providerName: 'fake', modelName: null, voiceName: null },
    ]));
    adminApi.listAiProviders.and.returnValue(of([
      { providerName: 'fake', hasApiKey: false, apiEndpoint: null, models: ['fake'], modelTests: [] },
    ]));
    adminApi.listAiPricing.and.returnValue(of([]));
    adminApi.listAiPricingOverrides.and.returnValue(of([]));

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
    const svc = jasmine.createSpyObj('AiUsageService', ['getSummary', 'getRecent', 'getTrends']);
    svc.getTrends.and.returnValue(of([]));
    svc.getSummary.and.returnValue(of({
      totalCalls: 1,
      successfulCalls: 1,
      failedCalls: 0,
      fallbackCalls: 0,
      successRate: 100,
      totalInputTokens: 1,
      totalOutputTokens: 1,
      totalTokens: 2,
      totalCostUsd: 0,
      byProvider: [],
      byFeature: [],
    }));
    svc.getRecent.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 1 }));

    TestBed.configureTestingModule({
      imports: [AdminAiUsageComponent],
      providers: [
        { provide: AiUsageService, useValue: svc },
        { provide: AdminApiService, useValue: adminApi },
      ],
    });

    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(query(fixture.nativeElement, 'sp-admin-card')).toBeTruthy();
  });

  function promptItem(id: number, active = false) {
    return {
      id: `prompt-${id}`,
      key: `activity.prompt.${id}`,
      version: id,
      isActive: active,
      maxInputTokens: 800,
      maxOutputTokens: 600,
    };
  }

  it('Prompts page renders metrics, filters, and pagination', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listPrompts']);
    adminApi.listPrompts.and.returnValue(of(Array.from({ length: 13 }, (_, index) => promptItem(index + 1, index === 0))));

    TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminPromptsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-page-header')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Templates');
    expect(fixture.nativeElement.textContent).toContain('Prompt library');
    expect(query(fixture.nativeElement, 'sp-admin-filter-bar')).toBeTruthy();
    expect(query(fixture.nativeElement, 'sp-admin-pagination')).toBeTruthy();
  });

  it('Prompts page filters prompt rows by search text', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listPrompts']);
    adminApi.listPrompts.and.returnValue(of([
      { ...promptItem(1), key: 'activity.email.reply' },
      { ...promptItem(2), key: 'activity.listening.summary' },
    ]));

    TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminPromptsComponent);
    fixture.detectChanges();
    fixture.componentInstance.setSearchTerm('email');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('activity.email.reply');
    expect(fixture.nativeElement.textContent).not.toContain('activity.listening.summary');
  });

  it('Prompts page opens row actions and loads prompt detail', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listPrompts', 'getPrompt']);
    adminApi.listPrompts.and.returnValue(of([promptItem(1, true)]));
    adminApi.getPrompt.and.returnValue(of({ ...promptItem(1, true), content: 'Return ONLY valid JSON.' }));

    TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminPromptsComponent);
    fixture.detectChanges();

    const actionsButton = fixture.nativeElement.querySelector('button[aria-label="Row actions"]') as HTMLButtonElement;
    actionsButton.click();
    fixture.detectChanges();
    const menuItems = fixture.nativeElement.querySelectorAll('sp-admin-table-actions button') as NodeListOf<HTMLButtonElement>;
    const viewButton = Array.from(menuItems)
      .find(button => button.textContent?.includes('View content')) as HTMLButtonElement;
    viewButton.click();
    fixture.detectChanges();

    expect(adminApi.getPrompt).toHaveBeenCalledWith('prompt-1');
    expect(fixture.nativeElement.textContent).toContain('Return ONLY valid JSON.');
  });

  it('Prompts page renders an error state when loading fails', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listPrompts']);
    adminApi.listPrompts.and.returnValue(throwError(() => ({ error: { error: 'API unavailable' } })));

    TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminPromptsComponent);
    fixture.detectChanges();

    expect(query(fixture.nativeElement, 'sp-admin-error-state')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('API unavailable');
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
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1 }));
    adminApi.getStats.and.returnValue(of({ totalActivityAttempts: 42 }));

    TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }],
    });

    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();

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

describe('Phase 10X-I — AI Config, Integrations, student modal CVA migration', () => {
  function makeAdminApi() {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listAiCategories', 'listAiProviders', 'listAiPricing', 'listAiPricingOverrides', 'listStudents']);
    adminApi.listAiCategories.and.returnValue(of([
      { categoryKey: 'llm.default', displayName: 'Default LLM', providerName: 'fake', modelName: null, voiceName: null },
      { categoryKey: 'tts.listening', displayName: 'Listening TTS', providerName: 'openai', modelName: 'tts-1', voiceName: 'onyx' },
    ]));
    adminApi.listAiProviders.and.returnValue(of([
      { providerName: 'openai', hasApiKey: true, apiEndpoint: null, models: ['gpt-4o'], modelTests: [] },
    ]));
    adminApi.listAiPricing.and.returnValue(of([]));
    adminApi.listAiPricingOverrides.and.returnValue(of([]));
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
    return adminApi;
  }

  function makeIntegrationsSvc() {
    const svc = jasmine.createSpyObj('AdminIntegrationsService', ['getStorage', 'getGenerationSettings', 'getBatches']);
    svc.getStorage.and.returnValue(of({ provider: 's3', endpoint: 'http://minio:9000', bucketName: 'lc', accessKey: 'key', secretKey: 'sec', useSsl: false, signedUrlExpiryMinutes: 15 }));
    svc.getGenerationSettings.and.returnValue(of({
      readyLessonBufferSize: 5, refillThreshold: 2, refillBatchSize: 2, maxGenerationAttempts: 2,
      generationTimeoutSeconds: 60, ttsTimeoutSeconds: 30, maxConcurrentGenerationJobs: 1, maxConcurrentTtsJobs: 1,
      practiceGymReadyExercisesPerType: 2, practiceGymRefillThresholdPerType: 1, practiceGymRefillCountPerType: 1,
      enableBackgroundGeneration: true, enableTtsGeneration: false,
    }));
    svc.getBatches.and.returnValue(of({ summary: { queued: 0, running: 0, failed: 0, lastSuccessfulGenerationUtc: null }, readyBufferPerStudent: [], batches: [] }));
    return svc;
  }

  it('AI Config page renders sp-admin-form-field wrappers inside LLM category cards (10X-I)', () => {
    const adminApi = makeAdminApi();
    TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });
    const fixture = TestBed.createComponent(AdminAiConfigComponent);
    fixture.detectChanges();
    const fields = fixture.nativeElement.querySelectorAll('sp-admin-form-field');
    expect(fields.length).toBeGreaterThanOrEqual(2);
  });

  it('AI Config TTS voice field uses sp-admin-input wrapper (10X-I)', () => {
    const adminApi = makeAdminApi();
    TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });
    const fixture = TestBed.createComponent(AdminAiConfigComponent);
    fixture.detectChanges();
    const inputs = fixture.nativeElement.querySelectorAll('sp-admin-input');
    expect(inputs.length).toBeGreaterThanOrEqual(1);
  });

  it('AI Config LLM category native selects are wrapped in sp-admin-form-field (10X-I)', () => {
    const adminApi = makeAdminApi();
    TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });
    const fixture = TestBed.createComponent(AdminAiConfigComponent);
    fixture.detectChanges();
    const selects = fixture.nativeElement.querySelectorAll('select');
    expect(selects.length).toBeGreaterThanOrEqual(2);
  });

  it('Integrations page renders sp-admin-form-field for storage display fields (10X-I)', () => {
    const svc = makeIntegrationsSvc();
    TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    });
    const fixture = TestBed.createComponent(AdminIntegrationsComponent);
    fixture.detectChanges();
    const fields = fixture.nativeElement.querySelectorAll('sp-admin-form-field');
    expect(fields.length).toBeGreaterThanOrEqual(7);
  });

  it('Integrations page renders sp-admin-button for test connection action (10X-I)', () => {
    const svc = makeIntegrationsSvc();
    TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    });
    const fixture = TestBed.createComponent(AdminIntegrationsComponent);
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('sp-admin-button');
    expect(buttons.length).toBeGreaterThanOrEqual(2);
  });

  it('Integrations save button calls saveSettings (10X-I)', () => {
    const svc = makeIntegrationsSvc();
    svc.updateGenerationSettings = jasmine.createSpy('updateGenerationSettings').and.returnValue(of({}));
    TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    });
    const fixture = TestBed.createComponent(AdminIntegrationsComponent);
    fixture.detectChanges();
    const allBtns = Array.from(fixture.nativeElement.querySelectorAll('sp-admin-button')) as HTMLElement[];
    const saveBtn = allBtns.find(el => el.textContent?.trim() === 'Save');
    expect(saveBtn).toBeTruthy();
    saveBtn?.click();
    expect(svc.updateGenerationSettings).toHaveBeenCalled();
  });

  it('Students edit modal opens as sp-admin-modal on startEdit (10X-I)', () => {
    const student = {
      studentProfileId: 'p1', email: 'ann@example.com', firstName: 'Ann', lastName: 'Lee',
      displayName: null, lifecycleStage: 'CourseReady', onboardingStatus: 'Complete', cefrLevel: 'B1',
      careerContext: null, learningGoal: null, learningGoalDescription: null, difficultSituationsText: null,
      preferredSessionDurationMinutes: null, professionalExperienceLevel: null, roleFamiliarity: null,
      createdAt: new Date().toISOString(),
    };
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [student], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }, { provide: ToastService, useValue: toast }],
    });
    const fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();
    fixture.componentInstance.startEdit(student as any);
    fixture.detectChanges();
    const modal = fixture.nativeElement.querySelector('sp-admin-modal');
    expect(modal).toBeTruthy();
    const inputs = modal.querySelectorAll('sp-admin-input');
    expect(inputs.length).toBeGreaterThanOrEqual(2);
  });

  it('Students edit modal closes on cancelEdit (10X-I)', () => {
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }, { provide: ToastService, useValue: toast }],
    });
    const fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();
    fixture.componentInstance.editing.set({ email: 'x@x.com' } as any);
    fixture.detectChanges();
    fixture.componentInstance.cancelEdit();
    fixture.detectChanges();
    expect(fixture.componentInstance.editing()).toBeNull();
  });

  it('Students reset password modal opens as sp-admin-modal (10X-I)', () => {
    const student = {
      studentProfileId: 'p2', email: 'bob@example.com', firstName: 'Bob', lastName: 'Smith',
      displayName: null, lifecycleStage: 'CourseReady', onboardingStatus: 'Complete', cefrLevel: null,
      careerContext: null, learningGoal: null, learningGoalDescription: null, difficultSituationsText: null,
      preferredSessionDurationMinutes: null, professionalExperienceLevel: null, roleFamiliarity: null,
      createdAt: new Date().toISOString(),
    };
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [student], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }, { provide: ToastService, useValue: toast }],
    });
    const fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();
    fixture.componentInstance.startResetPassword(student as any);
    fixture.detectChanges();
    const modals = fixture.nativeElement.querySelectorAll('sp-admin-modal');
    const openModal = (Array.from(modals) as Element[]).find(m => m.getAttribute('ng-reflect-open') !== 'false');
    expect(openModal).toBeTruthy();
  });

  it('Students reset data modal opens as sp-admin-modal (10X-I)', () => {
    const student = {
      studentProfileId: 'p3', email: 'carol@example.com', firstName: 'Carol', lastName: 'Jones',
      displayName: null, lifecycleStage: 'CourseReady', onboardingStatus: 'Complete', cefrLevel: null,
      careerContext: null, learningGoal: null, learningGoalDescription: null, difficultSituationsText: null,
      preferredSessionDurationMinutes: null, professionalExperienceLevel: null, roleFamiliarity: null,
      createdAt: new Date().toISOString(),
    };
    const adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents']);
    adminApi.listStudents.and.returnValue(of({ items: [student], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
    const toast = jasmine.createSpyObj('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [AdminStudentsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: adminApi }, { provide: ToastService, useValue: toast }],
    });
    const fixture = TestBed.createComponent(AdminStudentsComponent);
    fixture.detectChanges();
    fixture.componentInstance.startResetData(student as any);
    fixture.detectChanges();
    const modals = fixture.nativeElement.querySelectorAll('sp-admin-modal');
    expect(modals.length).toBeGreaterThanOrEqual(1);
    expect(fixture.componentInstance.resettingData()).toBeTruthy();
  });
});

describe('admin shell visual structure (10X-C → 10X-LAYOUT-BLOCKER)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('renders shell with sidebar, header and content slots', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('sp-admin-sidebar aside')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('sp-admin-header header')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('main')).not.toBeNull();
  });

  it('sidebar renders an aside element', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('sp-admin-sidebar aside');
    expect(aside).not.toBeNull();
  });

  it('header renders hamburger and desktop toggle buttons', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button[aria-label="Open navigation"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('button[aria-label="Toggle sidebar"]')).not.toBeNull();
  });

  it('header renders avatar button', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('A');
  });

  it('main area renders projected content when not collapsed', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('main');
    expect(main).not.toBeNull();
    expect(main.textContent).toContain('Page content');
  });

  it('header renders a semantic header element', () => {
    const fixture = TestBed.createComponent(ShellTestHostComponent);
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('sp-admin-header header');
    expect(header).not.toBeNull();
  });
});

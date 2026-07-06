import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminOnboardingComponent } from './admin-onboarding.component';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  StudentFlowTemplateDetailDto,
  StudentFlowTemplateSummaryDto,
} from '../../../core/models/admin-onboarding.models';

const TEMPLATE_DETAIL: StudentFlowTemplateDetailDto = {
  templateId: 'template-1',
  name: 'Default onboarding',
  description: null,
  status: 'Draft',
  activeVersionId: null,
  versions: [],
};

const TEMPLATE_SUMMARY: StudentFlowTemplateSummaryDto = {
  templateId: 'template-1',
  name: 'Default onboarding',
  description: null,
  status: 'Draft',
  activeVersionId: null,
  versionCount: 1,
  updatedAt: '2026-01-01T00:00:00Z',
};

function makeService(templates: StudentFlowTemplateSummaryDto[] = [TEMPLATE_SUMMARY]) {
  return {
    listTemplates: jasmine.createSpy('listTemplates').and.returnValue(of(templates)),
    createTemplate: jasmine.createSpy('createTemplate').and.returnValue(of(TEMPLATE_DETAIL)),
    archive: jasmine.createSpy('archive').and.returnValue(of(void 0)),
  };
}

describe('AdminOnboardingComponent (list page)', () => {
  let fixture: ComponentFixture<AdminOnboardingComponent>;
  let component: AdminOnboardingComponent;
  let svc: ReturnType<typeof makeService>;
  let router: Router;

  async function setup(templates: StudentFlowTemplateSummaryDto[] = [TEMPLATE_SUMMARY]) {
    svc = makeService(templates);
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingComponent],
      providers: [provideRouter([]), { provide: AdminOnboardingService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page heading', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Onboarding');
  });

  it('calls listTemplates on init', async () => {
    await setup();
    expect(svc.listTemplates).toHaveBeenCalledTimes(1);
  });

  it('populates templates signal after load', async () => {
    await setup([TEMPLATE_SUMMARY]);
    expect(component.templates().length).toBe(1);
  });

  it('shows error state when listTemplates fails', async () => {
    svc = makeService();
    svc.listTemplates.and.returnValue(throwError(() => ({ error: { error: 'Server error' } })));
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingComponent],
      providers: [provideRouter([]), { provide: AdminOnboardingService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toBeTruthy();
  });

  it('openCreateForm opens the create form', async () => {
    await setup();
    component.openCreateForm();
    expect(component.createFormOpen()).toBeTrue();
  });

  it('createTemplate does nothing when name is blank', async () => {
    await setup();
    component.openCreateForm();
    component.newTemplateName = '   ';
    component.createTemplate();
    expect(svc.createTemplate).not.toHaveBeenCalled();
  });

  it('createTemplate navigates to the editor page on success', fakeAsync(async () => {
    await setup();
    const navigateSpy = spyOn(router, 'navigate');
    component.openCreateForm();
    component.newTemplateName = 'New template';
    component.createTemplate();
    tick();
    expect(svc.createTemplate).toHaveBeenCalledWith({ name: 'New template', description: undefined });
    expect(navigateSpy).toHaveBeenCalledWith(['/admin/onboarding', 'template-1']);
  }));

  it('editTemplate navigates to the editor page', async () => {
    await setup();
    const navigateSpy = spyOn(router, 'navigate');
    component.editTemplate('template-1');
    expect(navigateSpy).toHaveBeenCalledWith(['/admin/onboarding', 'template-1']);
  });

  it('archive calls service and reloads the list', fakeAsync(async () => {
    await setup();
    component.archive('template-1');
    tick();
    expect(svc.archive).toHaveBeenCalledWith('template-1');
    expect(svc.listTemplates).toHaveBeenCalledTimes(2);
  }));

  it('statusTone returns success for Published', async () => {
    await setup();
    expect(component.statusTone('Published')).toBe('success');
  });

  it('statusTone returns warning for Draft', async () => {
    await setup();
    expect(component.statusTone('Draft')).toBe('warning');
  });

  it('statusTone returns neutral for Archived', async () => {
    await setup();
    expect(component.statusTone('Archived')).toBe('neutral');
  });
});

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminOnboardingEditorComponent } from './admin-onboarding-editor.component';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  StudentFlowTemplateDetailDto,
  StudentFlowTemplateVersionDto,
} from '../../../core/models/admin-onboarding.models';

const DRAFT_VERSION: StudentFlowTemplateVersionDto = {
  versionId: 'v1',
  templateId: 'template-1',
  versionNumber: 1,
  formIoSchemaJson: '{"display":"form","components":[]}',
  scoringRulesJson: null,
  status: 'Draft',
  publishedAt: null,
  updatedAt: '2026-01-01T00:00:00Z',
};

const TEMPLATE_DETAIL: StudentFlowTemplateDetailDto = {
  templateId: 'template-1',
  name: 'Default onboarding',
  description: null,
  status: 'Draft',
  activeVersionId: null,
  versions: [DRAFT_VERSION],
};

function makeService() {
  return {
    getTemplate: jasmine.createSpy('getTemplate').and.returnValue(of(TEMPLATE_DETAIL)),
    saveDraft: jasmine.createSpy('saveDraft').and.returnValue(of(DRAFT_VERSION)),
    publish: jasmine.createSpy('publish').and.returnValue(of({ ...DRAFT_VERSION, status: 'Published' })),
    archive: jasmine.createSpy('archive').and.returnValue(of(void 0)),
  };
}

describe('AdminOnboardingEditorComponent', () => {
  let fixture: ComponentFixture<AdminOnboardingEditorComponent>;
  let component: AdminOnboardingEditorComponent;
  let svc: ReturnType<typeof makeService>;
  let router: Router;

  async function setup() {
    svc = makeService();
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingEditorComponent],
      providers: [
        provideRouter([]),
        { provide: AdminOnboardingService, useValue: svc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ templateId: 'template-1' }) } },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingEditorComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads the template by route param on init', async () => {
    await setup();
    expect(svc.getTemplate).toHaveBeenCalledWith('template-1');
    expect(component.template()?.templateId).toBe('template-1');
  });

  it('seeds draftSchema from the current draft version', async () => {
    await setup();
    expect(component.draftSchema()).toEqual({ display: 'form', components: [] });
  });

  it('draftVersion computed returns the Draft-status version', async () => {
    await setup();
    expect(component.draftVersion()?.status).toBe('Draft');
  });

  it('shows error state when getTemplate fails', async () => {
    svc = makeService();
    svc.getTemplate.and.returnValue(throwError(() => ({ error: { error: 'Not found' } })));
    await TestBed.configureTestingModule({
      imports: [AdminOnboardingEditorComponent],
      providers: [
        provideRouter([]),
        { provide: AdminOnboardingService, useValue: svc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ templateId: 'template-1' }) } },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminOnboardingEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toBeTruthy();
  });

  it('saveDraft calls service with stringified schema and scoring rules', fakeAsync(async () => {
    await setup();
    component.scoringRulesJson.set('{"assessment_q1":{"correctAnswerKey":"b"}}');
    component.saveDraft();
    tick();
    expect(svc.saveDraft).toHaveBeenCalledWith('template-1', jasmine.objectContaining({
      formIoSchemaJson: jasmine.any(String),
      scoringRulesJson: '{"assessment_q1":{"correctAnswerKey":"b"}}',
    }));
  }));

  it('saveDraft sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.saveDraft();
    tick();
    expect(component.actionSuccess()).toBe('Draft saved.');
  }));

  it('saveDraft sets actionError on failure', fakeAsync(async () => {
    await setup();
    svc.saveDraft.and.returnValue(throwError(() => ({ error: { error: 'Invalid schema' } })));
    component.saveDraft();
    tick();
    expect(component.actionError()).toContain('Invalid schema');
  }));

  it('publish calls service with templateId', fakeAsync(async () => {
    await setup();
    component.publish();
    tick();
    expect(svc.publish).toHaveBeenCalledWith('template-1');
  }));

  it('archive calls service and navigates back to the list', fakeAsync(async () => {
    await setup();
    const navigateSpy = spyOn(router, 'navigate');
    component.archive();
    tick();
    expect(svc.archive).toHaveBeenCalledWith('template-1');
    expect(navigateSpy).toHaveBeenCalledWith(['/admin/onboarding']);
  }));

  it('openPreview/closePreview toggle previewOpen', async () => {
    await setup();
    component.openPreview();
    expect(component.previewOpen()).toBeTrue();
    component.closePreview();
    expect(component.previewOpen()).toBeFalse();
  });

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

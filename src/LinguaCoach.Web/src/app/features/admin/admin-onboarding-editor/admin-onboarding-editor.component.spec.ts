import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminOnboardingEditorComponent } from './admin-onboarding-editor.component';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import {
  OnboardingFieldMappingDto,
  StudentFlowTemplateDetailDto,
  StudentFlowTemplateVersionDto,
} from '../../../core/models/admin-onboarding.models';

const FIELD_MAPPING: OnboardingFieldMappingDto[] = [
  { key: 'preferred_name', profileField: 'PreferredName', description: 'name', required: true, expectedShape: 'Text' },
  { key: 'session_duration', profileField: 'PreferredSessionDurationMinutes', description: 'duration', required: false, expectedShape: 'Number' },
];

const DRAFT_VERSION: StudentFlowTemplateVersionDto = {
  versionId: 'v1',
  templateId: 'template-1',
  versionNumber: 1,
  formIoSchemaJson: '{"display":"form","components":[]}',
  scoringRulesJson: null,
  rendererKind: 'FormIo',
  status: 'Draft',
  publishedAt: null,
  updatedAt: '2026-01-01T00:00:00Z',
  authoringSchemaJson: null,
};

const LEGACY_DRAFT_VERSION: StudentFlowTemplateVersionDto = {
  ...DRAFT_VERSION,
  formIoSchemaJson: JSON.stringify({
    display: 'form',
    components: [{ type: 'radio', key: 'assessment_q1', label: 'Pick one', values: [{ label: 'a', value: 'a' }, { label: 'b', value: 'b' }] }],
  }),
  scoringRulesJson: '{"assessment_q1":{"correctAnswerKey":"b"}}',
  authoringSchemaJson: null,
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
    getProfileFieldMapping: jasmine.createSpy('getProfileFieldMapping').and.returnValue(of(FIELD_MAPPING)),
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
    // The live Form.io builder normalizes the schema on render (adds a submit button component),
    // so assert on the authored shape rather than deep-equality of the whole tree.
    expect(component.draftSchema().display).toBe('form');
    expect((component.draftSchema().components ?? []).every((c: any) => c.type === 'button' || c.key)).toBeTrue();
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

  it('saveDraft calls service with stringified schema and authoringSchemaJson', fakeAsync(async () => {
    await setup();
    component.saveDraft();
    tick();
    expect(svc.saveDraft).toHaveBeenCalledWith('template-1', jasmine.objectContaining({
      formIoSchemaJson: jasmine.any(String),
      authoringSchemaJson: jasmine.any(String),
    }));
  }));

  it('needsReauthoring is set when scoringRulesJson exists but authoringSchemaJson does not (legacy draft)', async () => {
    svc = makeService();
    svc.getTemplate.and.returnValue(of({ ...TEMPLATE_DETAIL, versions: [LEGACY_DRAFT_VERSION] }));
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

    expect(component.needsReauthoring()).toBeTrue();
  });

  it('scoredSummary is 0 of 0 for an empty schema', async () => {
    await setup();
    expect(component.scoredSummary()).toEqual({ scored: 0, total: 0 });
  });

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

  it('openFieldMapping loads the mapping list and opens the panel', fakeAsync(async () => {
    await setup();
    component.openFieldMapping();
    tick();
    expect(svc.getProfileFieldMapping).toHaveBeenCalledTimes(1);
    expect(component.fieldMappingOpen()).toBeTrue();
    expect(component.fieldMapping().length).toBe(2);
  }));

  it('openFieldMapping does not refetch on a second open', fakeAsync(async () => {
    await setup();
    component.openFieldMapping();
    tick();
    component.closeFieldMapping();
    component.openFieldMapping();
    tick();
    expect(svc.getProfileFieldMapping).toHaveBeenCalledTimes(1);
  }));

  it('fieldMappingRows marks a key missing when the schema does not contain it', fakeAsync(async () => {
    await setup();
    component.builderRef = undefined; // openFieldMapping would otherwise resync draftSchema from the live builder
    component.draftSchema.set({ components: [] });
    component.openFieldMapping();
    tick();
    const rows = component.fieldMappingRows();
    expect(rows.every(r => !r.present)).toBeTrue();
    expect(component.missingRequiredCount()).toBe(1); // only preferred_name is required
  }));

  it('fieldMappingRows marks a key present when the schema contains a matching component key', fakeAsync(async () => {
    await setup();
    component.builderRef = undefined; // openFieldMapping would otherwise resync draftSchema from the live builder
    component.draftSchema.set({ components: [{ type: 'textfield', key: 'preferred_name' }] });
    component.openFieldMapping();
    tick();
    const rows = component.fieldMappingRows();
    expect(rows.find(r => r.key === 'preferred_name')?.present).toBeTrue();
    expect(component.missingRequiredCount()).toBe(0);
  }));

  it('closeFieldMapping closes the panel', fakeAsync(async () => {
    await setup();
    component.openFieldMapping();
    tick();
    component.closeFieldMapping();
    expect(component.fieldMappingOpen()).toBeFalse();
  }));
});

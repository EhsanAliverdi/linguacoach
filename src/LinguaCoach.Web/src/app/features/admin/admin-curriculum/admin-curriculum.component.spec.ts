import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminCurriculumComponent } from './admin-curriculum.component';
import { CurriculumService } from '../../../core/services/curriculum.service';
import type {
  AdminCurriculumObjectiveDto,
  CurriculumTaxonomyDto,
  AdminRoutingPreviewResult,
} from '../../../core/services/curriculum.service';

function makeObjective(overrides: Partial<AdminCurriculumObjectiveDto> = {}): AdminCurriculumObjectiveDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    key: 'a1.speaking.greetings',
    title: 'Greetings',
    description: 'Basic greetings',
    cefrLevel: 'A1',
    primarySkill: 'speaking',
    secondarySkillsJson: '[]',
    contextTagsJson: '["general_english"]',
    focusTagsJson: '[]',
    prerequisiteKeysJson: '[]',
    recommendedOrder: 1,
    difficultyBand: 1,
    isActive: true,
    isReviewable: false,
    isExamInspired: false,
    teachingNotes: null,
    examplePrompts: null,
    adminUpdatedAt: null,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

function makeTaxonomy(): CurriculumTaxonomyDto {
  return {
    cefrLevels: ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'],
    skills: ['speaking', 'writing', 'reading', 'listening', 'vocabulary', 'grammar'],
    contextTags: ['general_english', 'workplace', 'travel'],
  };
}

function makePreviewResult(): AdminRoutingPreviewResult {
  return {
    targetCefrLevel: 'A1',
    curriculumObjectiveKey: 'a1.speaking.greetings',
    curriculumObjectiveTitle: 'Greetings',
    contextTags: ['general_english'],
    focusTags: [],
    difficultyBand: 1,
    routingReason: 'Best match',
    isLowerLevelContent: false,
    explanation: null,
    fallbackUsed: false,
    noExactObjectiveFound: false,
    warnings: [],
  };
}

describe('AdminCurriculumComponent', () => {
  let svc: jasmine.SpyObj<CurriculumService>;

  beforeEach(() => {
    svc = jasmine.createSpyObj('CurriculumService', [
      'listObjectives',
      'getObjective',
      'getTaxonomy',
      'createObjective',
      'updateObjective',
      'activateObjective',
      'deactivateObjective',
      'previewRouting',
    ]);

    svc.listObjectives.and.returnValue(of([makeObjective()]));
    svc.getTaxonomy.and.returnValue(of(makeTaxonomy()));
    svc.getObjective.and.returnValue(of(makeObjective()));
    svc.createObjective.and.returnValue(of(makeObjective()));
    svc.updateObjective.and.returnValue(of(makeObjective()));
    svc.activateObjective.and.returnValue(of(makeObjective({ isActive: true })));
    svc.deactivateObjective.and.returnValue(of(makeObjective({ isActive: false })));
    svc.previewRouting.and.returnValue(of(makePreviewResult()));

    TestBed.configureTestingModule({
      imports: [AdminCurriculumComponent],
      providers: [{ provide: CurriculumService, useValue: svc }],
    });
  });

  // ── List view ──────────────────────────────────────────────────────────────

  it('renders list view on init and calls listObjectives', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    fixture.detectChanges();
    expect(svc.listObjectives).toHaveBeenCalled();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('sp-admin-page-header')).toBeTruthy();
    expect(html.querySelector('sp-admin-filter-bar')).toBeTruthy();
    expect(html.querySelector('sp-admin-table')).toBeTruthy();
    expect(html.textContent).toContain('Greetings');
  });

  it('shows objective key in list', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('a1.speaking.greetings');
  });

  it('shows CEFR level in list', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.textContent).toContain('A1');
  });

  // ── Filter triggers re-fetch ───────────────────────────────────────────────

  it('applies cefrLevel filter and calls listObjectives with that param', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();
    svc.listObjectives.calls.reset();

    comp.filterCefr = 'B1';
    comp.load();
    fixture.detectChanges();

    expect(svc.listObjectives).toHaveBeenCalledWith('B1', undefined, jasmine.anything());
  });

  it('applies skill filter and calls listObjectives with that param', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();
    svc.listObjectives.calls.reset();

    comp.filterSkill = 'writing';
    comp.load();
    fixture.detectChanges();

    expect(svc.listObjectives).toHaveBeenCalledWith(undefined, 'writing', jasmine.anything());
  });

  // ── Deactivate / Activate ─────────────────────────────────────────────────

  it('calls deactivateObjective with the correct key', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.deactivate('a1.speaking.greetings');
    expect(svc.deactivateObjective).toHaveBeenCalledWith('a1.speaking.greetings');
  });

  it('calls activateObjective with the correct key', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.activate('a1.speaking.greetings');
    expect(svc.activateObjective).toHaveBeenCalledWith('a1.speaking.greetings');
  });

  // ── Create form navigation ─────────────────────────────────────────────────

  it('switches view to create when startCreate is called', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.startCreate();
    fixture.detectChanges();

    expect(comp.view()).toBe('create');
  });

  // ── Edit navigation ────────────────────────────────────────────────────────

  it('switches view to edit when startEdit is called', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.startEdit(makeObjective());
    fixture.detectChanges();

    expect(comp.view()).toBe('edit');
  });

  it('populates form fields from objective in startEdit', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.startEdit(makeObjective({ title: 'Custom Title', cefrLevel: 'B2' }));
    expect(comp.form.title).toBe('Custom Title');
    expect(comp.form.cefrLevel).toBe('B2');
  });

  // ── Routing preview ───────────────────────────────────────────────────────

  it('switches view to preview via view signal', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.view.set('preview');
    fixture.detectChanges();

    expect(comp.view()).toBe('preview');
  });

  it('calls previewRouting and sets previewResult signal', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.view.set('preview');
    comp.runPreview();
    fixture.detectChanges();

    expect(svc.previewRouting).toHaveBeenCalled();
    expect(comp.previewResult()).not.toBeNull();
    expect(comp.previewResult()?.curriculumObjectiveKey).toBe('a1.speaking.greetings');
  });

  it('preview result does not default to workplace as sole context tag', () => {
    svc.previewRouting.and.returnValue(of({ ...makePreviewResult(), contextTags: ['general_english'] }));

    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    comp.view.set('preview');
    comp.runPreview();
    fixture.detectChanges();

    const tags: string[] = comp.previewResult()?.contextTags ?? [];
    expect(tags.length === 1 && tags[0] === 'workplace').toBeFalse();
  });

  // ── Error state ───────────────────────────────────────────────────────────

  it('sets globalError signal on listObjectives failure', () => {
    svc.listObjectives.and.returnValue(throwError(() => new Error('Server error')));

    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    fixture.detectChanges();

    expect((fixture.componentInstance as any).globalError()).toBeTruthy();
  });

  // ── Taxonomy ──────────────────────────────────────────────────────────────

  it('loads taxonomy and populates CEFR levels', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    expect(svc.getTaxonomy).toHaveBeenCalled();
    const tax = comp.taxonomy();
    expect(tax?.cefrLevels).toContain('A1');
    expect(tax?.cefrLevels).toContain('C2');
  });

  it('loads taxonomy and populates skills', () => {
    const fixture = TestBed.createComponent(AdminCurriculumComponent);
    const comp = fixture.componentInstance as any;
    fixture.detectChanges();

    const tax = comp.taxonomy();
    expect(tax?.skills).toContain('speaking');
    expect(tax?.skills).toContain('writing');
  });
});

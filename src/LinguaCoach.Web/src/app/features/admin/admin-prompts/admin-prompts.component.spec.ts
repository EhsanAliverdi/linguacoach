import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminPromptsComponent } from './admin-prompts.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { PromptTemplateItem, PromptTemplateDetail } from '../../../core/models/admin.models';

const PROMPT_ACTIVE: PromptTemplateItem = {
  id: 'id-1',
  key: 'writing.exercise.feedback.v1',
  version: 1,
  isActive: true,
  maxInputTokens: 800,
  maxOutputTokens: 600,
};

const PROMPT_INACTIVE: PromptTemplateItem = {
  id: 'id-2',
  key: 'speaking.practice.score.v1',
  version: 1,
  isActive: false,
  maxInputTokens: 400,
  maxOutputTokens: 300,
};

const PROMPT_DETAIL: PromptTemplateDetail = {
  ...PROMPT_ACTIVE,
  content: 'You are a helpful language coach. {{input}}',
};

function makeAdminApi(prompts: PromptTemplateItem[] = [PROMPT_ACTIVE]) {
  return {
    listPrompts: jasmine.createSpy('listPrompts').and.returnValue(of(prompts)),
    getPrompt: jasmine.createSpy('getPrompt').and.returnValue(of(PROMPT_DETAIL)),
    createPromptVersion: jasmine.createSpy('createPromptVersion').and.returnValue(of(PROMPT_DETAIL)),
    activatePrompt: jasmine.createSpy('activatePrompt').and.returnValue(of(undefined)),
    deactivatePrompt: jasmine.createSpy('deactivatePrompt').and.returnValue(of(undefined)),
  };
}

describe('AdminPromptsComponent', () => {
  let fixture: ComponentFixture<AdminPromptsComponent>;
  let component: AdminPromptsComponent;
  let adminApi: ReturnType<typeof makeAdminApi>;

  async function setup(prompts: PromptTemplateItem[] = [PROMPT_ACTIVE]) {
    adminApi = makeAdminApi(prompts);
    await TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPromptsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page title', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Prompts');
  });

  it('calls listPrompts on init', async () => {
    await setup();
    expect(adminApi.listPrompts).toHaveBeenCalledTimes(1);
  });

  it('renders a row per prompt', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
  });

  it('renders active badge for active prompt', async () => {
    await setup([PROMPT_ACTIVE]);
    expect(fixture.nativeElement.textContent).toContain('Active');
  });

  it('renders inactive badge for inactive prompt', async () => {
    await setup([PROMPT_INACTIVE]);
    expect(fixture.nativeElement.textContent).toContain('Inactive');
  });

  it('renders token budget label', async () => {
    await setup([PROMPT_ACTIVE]);
    expect(fixture.nativeElement.textContent).toContain('800');
    expect(fixture.nativeElement.textContent).toContain('600');
  });

  it('filters by search term', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    component.setSearchTerm('writing');
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
  });

  it('resets page to 1 on search', async () => {
    await setup([PROMPT_ACTIVE]);
    component.page.set(3);
    component.setSearchTerm('x');
    expect(component.page()).toBe(1);
  });

  it('filters by active status', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    component.setStatusFilter('active');
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
  });

  it('filters by inactive status', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    component.setStatusFilter('inactive');
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
  });

  it('resets page to 1 on status filter change', async () => {
    await setup([PROMPT_ACTIVE]);
    component.page.set(2);
    component.setStatusFilter('active');
    expect(component.page()).toBe(1);
  });

  it('shows empty state when no prompts match filter', async () => {
    await setup([PROMPT_ACTIVE]);
    component.setSearchTerm('zzznomatch');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('match');
  });

  it('shows error state on load failure', async () => {
    adminApi = makeAdminApi();
    adminApi.listPrompts.and.returnValue(throwError(() => ({ error: { error: 'Server error' } })));
    await TestBed.configureTestingModule({
      imports: [AdminPromptsComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPromptsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('could not load');
  });

  it('calls activatePrompt and reloads', fakeAsync(async () => {
    await setup([PROMPT_INACTIVE]);
    component.activate(PROMPT_INACTIVE);
    tick();
    expect(adminApi.activatePrompt).toHaveBeenCalledWith('id-2');
    expect(adminApi.listPrompts).toHaveBeenCalledTimes(2);
  }));

  it('calls deactivatePrompt and reloads', fakeAsync(async () => {
    await setup([PROMPT_ACTIVE]);
    component.deactivate(PROMPT_ACTIVE);
    tick();
    expect(adminApi.deactivatePrompt).toHaveBeenCalledWith('id-1');
    expect(adminApi.listPrompts).toHaveBeenCalledTimes(2);
  }));

  it('calls getPrompt when viewing detail', fakeAsync(async () => {
    await setup([PROMPT_ACTIVE]);
    component.openView(PROMPT_ACTIVE);
    tick();
    expect(adminApi.getPrompt).toHaveBeenCalledWith('id-1');
    expect(component.detail()).toEqual(PROMPT_DETAIL);
  }));

  it('opens edit panel via openCreate', async () => {
    await setup();
    expect(component.showEditPanel()).toBeFalse();
    component.openCreate();
    expect(component.showEditPanel()).toBeTrue();
    expect(component.editRow()).toBeNull();
  });

  it('opens edit panel via openEdit', async () => {
    await setup([PROMPT_ACTIVE]);
    component.openEdit(PROMPT_ACTIVE);
    expect(component.showEditPanel()).toBeTrue();
    expect(component.editRow()).toEqual(PROMPT_ACTIVE);
  });

  it('calls createPromptVersion with form values on create', fakeAsync(async () => {
    await setup();
    component.openCreate();
    component.newKey = 'test.key.v1';
    component.newContent = 'Test prompt content';
    component.newMaxInput = 500;
    component.newMaxOutput = 400;
    component.createVersion();
    tick();
    expect(adminApi.createPromptVersion).toHaveBeenCalledWith({
      key: 'test.key.v1',
      content: 'Test prompt content',
      maxInputTokens: 500,
      maxOutputTokens: 400,
    });
  }));

  it('calls createPromptVersion using editRow key on edit', fakeAsync(async () => {
    await setup([PROMPT_ACTIVE]);
    component.openEdit(PROMPT_ACTIVE);
    component.newContent = 'Updated prompt content';
    component.newMaxInput = 800;
    component.newMaxOutput = 600;
    component.createVersion();
    tick();
    expect(adminApi.createPromptVersion).toHaveBeenCalledWith({
      key: PROMPT_ACTIVE.key,
      content: 'Updated prompt content',
      maxInputTokens: 800,
      maxOutputTokens: 600,
    });
  }));

  it('sets formError when content is missing', async () => {
    await setup();
    component.openCreate();
    component.newKey = 'test.key';
    component.newContent = '';
    component.createVersion();
    expect(component.formError()).toBeTruthy();
  });

  it('sets formError when key is missing on create', async () => {
    await setup();
    component.openCreate();
    component.newKey = '';
    component.newContent = 'some content';
    component.createVersion();
    expect(component.formError()).toBeTruthy();
  });

  it('computes uniqueKeyCount correctly', async () => {
    await setup([PROMPT_ACTIVE, { ...PROMPT_INACTIVE, key: PROMPT_ACTIVE.key }]);
    expect(component.uniqueKeyCount()).toBe(1);
  });

  it('computes activeCount correctly', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    expect(component.activeCount()).toBe(1);
  });

  // ── KPI strip ────────────────────────────────────────────────────────────────

  it('renders sp-admin-kpi-card elements for the summary strip', async () => {
    await setup([PROMPT_ACTIVE]);
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('summary strip has aria-label "Prompt template summary"', async () => {
    await setup([PROMPT_ACTIVE]);
    const strip = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Prompt template summary"]');
    expect(strip).toBeTruthy();
  });

  it('page header subtitle includes template count', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // uniqueKeyCount() — both have different keys, so 2 templates
    expect(text).toContain('2 templates');
  });

  // ── Category ─────────────────────────────────────────────────────────────────

  it('promptCategory extracts first key segment capitalised (dot key)', async () => {
    await setup();
    expect(component.promptCategory('writing.exercise.feedback.v1')).toBe('Writing');
    expect(component.promptCategory('speaking.practice.score.v1')).toBe('Speaking');
  });

  it('promptCategory returns Other for underscore activity_ prefix', async () => {
    await setup();
    expect(component.promptCategory('activity_evaluate_answer_short_q')).toBe('Other');
  });

  it('promptCategory maps system_ prefix to Curriculum', async () => {
    await setup();
    expect(component.promptCategory('system_build_lesson_plan')).toBe('Curriculum');
  });

  it('promptCategory maps placement_ prefix to Assessment', async () => {
    await setup();
    expect(component.promptCategory('placement_evaluate_writing')).toBe('Assessment');
  });

  it('promptCategory maps memory_ prefix to Memory', async () => {
    await setup();
    expect(component.promptCategory('memory_build_profile')).toBe('Memory');
  });

  it('renders category badge in table row', async () => {
    await setup([PROMPT_ACTIVE]);
    const badges = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-badge');
    const badgeTexts = Array.from(badges).map(b => b.textContent?.trim());
    expect(badgeTexts.some(t => t === 'Writing')).toBeTrue();
  });

  it('categoryFilter filters rows by derived category', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    component.setCategoryFilter('Writing');
    expect(component.filteredPrompts().length).toBe(1);
    expect(component.filteredPrompts()[0].key).toBe('writing.exercise.feedback.v1');
  });

  it('setCategoryFilter resets page to 1', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    component.page.set(2);
    component.setCategoryFilter('Writing');
    expect(component.page()).toBe(1);
  });

  it('categoryFilterOptions computed from loaded prompts', async () => {
    await setup([PROMPT_ACTIVE, PROMPT_INACTIVE]);
    const opts = component.categoryFilterOptions();
    const labels = opts.map(o => o.label);
    expect(labels).toContain('Writing');
    expect(labels).toContain('Speaking');
  });

  it('categoryTone returns known tone for Writing', async () => {
    await setup();
    expect(component.categoryTone('Writing')).toBe('info');
  });

  it('categoryTone returns neutral for unknown category', async () => {
    await setup();
    expect(component.categoryTone('Zoology')).toBe('neutral');
  });

  // ── "latest" badge ───────────────────────────────────────────────────────────

  it('isLatestVersion returns true for the highest version of a key', async () => {
    const v1: PromptTemplateItem = { id: 'k-v1', key: 'foo.bar', version: 1, isActive: false, maxInputTokens: 100, maxOutputTokens: 100 };
    const v2: PromptTemplateItem = { id: 'k-v2', key: 'foo.bar', version: 2, isActive: true,  maxInputTokens: 100, maxOutputTokens: 100 };
    await setup([v1, v2]);
    expect(component.isLatestVersion(v2)).toBeTrue();
    expect(component.isLatestVersion(v1)).toBeFalse();
  });

  // ── promptVars ───────────────────────────────────────────────────────────────

  it('promptVars extracts unique variable names from content', async () => {
    await setup();
    const vars = component.promptVars('Hello {{name}}, your score is {{score}}. Good luck {{name}}!');
    expect(vars).toEqual(['name', 'score']);
  });

  it('promptVars returns empty array for content with no variables', async () => {
    await setup();
    expect(component.promptVars('No variables here.')).toEqual([]);
  });

  // ── slide-over state ─────────────────────────────────────────────────────────

  it('closeView clears viewPrompt and detail', fakeAsync(async () => {
    await setup([PROMPT_ACTIVE]);
    component.openView(PROMPT_ACTIVE);
    tick();
    component.closeView();
    expect(component.viewPrompt()).toBeNull();
    expect(component.detail()).toBeNull();
  }));

  it('closeEdit clears showEditPanel and editRow', async () => {
    await setup([PROMPT_ACTIVE]);
    component.openEdit(PROMPT_ACTIVE);
    component.closeEdit();
    expect(component.showEditPanel()).toBeFalse();
    expect(component.editRow()).toBeNull();
  });
});

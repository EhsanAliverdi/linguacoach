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
  content: 'You are a helpful language coach. {input}',
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

  it('renders the page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Prompt Templates');
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
    component.viewDetail('id-1');
    tick();
    expect(adminApi.getPrompt).toHaveBeenCalledWith('id-1');
    expect(component.detail()).toEqual(PROMPT_DETAIL);
  }));

  it('shows form on toggleForm', async () => {
    await setup();
    expect(component.showForm()).toBeFalse();
    component.toggleForm();
    fixture.detectChanges();
    expect(component.showForm()).toBeTrue();
  });

  it('calls createPromptVersion with form values', fakeAsync(async () => {
    await setup();
    component.toggleForm();
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

  it('sets formError when key or content is missing', async () => {
    await setup();
    component.newKey = '';
    component.newContent = '';
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
});

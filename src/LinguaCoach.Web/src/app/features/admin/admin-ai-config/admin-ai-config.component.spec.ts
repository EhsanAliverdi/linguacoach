import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAiConfigComponent } from './admin-ai-config.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiConfigCategoryItem, AiModelPricingItem, AiModelPricingOverrideItem, AiProviderCatalogItem } from '../../../core/models/admin.models';

const CAT_LLM: AiConfigCategoryItem = {
  id: 'cat-1',
  categoryKey: 'llm.default',
  displayName: 'Default LLM',
  providerName: 'anthropic',
  modelName: 'claude-sonnet-4-6',
  voiceName: null,
};

const CAT_TTS: AiConfigCategoryItem = {
  id: 'cat-2',
  categoryKey: 'tts.listening',
  displayName: 'TTS Listening',
  providerName: 'openai',
  modelName: 'tts-1',
  voiceName: 'onyx',
};

const CAT_UNSET: AiConfigCategoryItem = {
  id: 'cat-3',
  categoryKey: 'llm.evaluation',
  displayName: 'Evaluation LLM',
  providerName: null,
  modelName: null,
  voiceName: null,
};

const PRICING_ROWS: AiModelPricingItem[] = [
  { providerName: 'anthropic', modelName: 'claude-sonnet-4-6', inputPer1KTokens: 0.003, outputPer1KTokens: 0.015, currency: 'USD', source: 'Configuration', isConfigured: true },
  { providerName: 'openai', modelName: 'gpt-4o', inputPer1KTokens: 0.0025, outputPer1KTokens: 0.01, currency: 'USD', source: 'Configuration', isConfigured: true },
];

const PROVIDER: AiProviderCatalogItem = {
  providerName: 'anthropic',
  models: ['claude-sonnet-4-6', 'claude-haiku-4-5-20251001'],
  hasApiKey: true,
  apiEndpoint: null,
  modelTests: [
    { modelName: 'claude-sonnet-4-6', ok: true, latencyMs: 320, error: null, testedAt: '2026-06-19T10:00:00Z' },
    { modelName: 'claude-haiku-4-5-20251001', ok: false, latencyMs: 0, error: 'Auth failed', testedAt: '2026-06-19T10:01:00Z' },
    { modelName: 'claude-opus-4-8', ok: false, latencyMs: 0, error: null, testedAt: '0001-01-01T00:00:00' },
  ],
};

function makeAdminApi(
  categories: AiConfigCategoryItem[] = [CAT_LLM, CAT_TTS],
  catalog: AiProviderCatalogItem[] = [PROVIDER],
  pricing: AiModelPricingItem[] = PRICING_ROWS,
) {
  return {
    listAiCategories: jasmine.createSpy('listAiCategories').and.returnValue(of(categories)),
    listAiProviders: jasmine.createSpy('listAiProviders').and.returnValue(of(catalog)),
    listAiPricing: jasmine.createSpy('listAiPricing').and.returnValue(of(pricing)),
    listAiPricingOverrides: jasmine.createSpy('listAiPricingOverrides').and.returnValue(of([])),
    updateAiCategory: jasmine.createSpy('updateAiCategory').and.returnValue(of(CAT_LLM)),
    testAiCategory: jasmine.createSpy('testAiCategory').and.returnValue(of({ ok: true, latencyMs: 200, error: null })),
    createAiPricingOverride: jasmine.createSpy('createAiPricingOverride').and.returnValue(of({})),
    updateAiPricingOverride: jasmine.createSpy('updateAiPricingOverride').and.returnValue(of({})),
    deactivateAiPricingOverride: jasmine.createSpy('deactivateAiPricingOverride').and.returnValue(of(void 0)),
    setProviderApiKey: jasmine.createSpy('setProviderApiKey').and.returnValue(of(PROVIDER)),
    setProviderEndpoint: jasmine.createSpy('setProviderEndpoint').and.returnValue(of(PROVIDER)),
    testProvider: jasmine.createSpy('testProvider').and.returnValue(of(PROVIDER)),
    addProviderModel: jasmine.createSpy('addProviderModel').and.returnValue(of(PROVIDER)),
    testProviderModel: jasmine.createSpy('testProviderModel').and.returnValue(of(PROVIDER)),
  };
}

describe('AdminAiConfigComponent', () => {
  let fixture: ComponentFixture<AdminAiConfigComponent>;
  let component: AdminAiConfigComponent;
  let adminApi: ReturnType<typeof makeAdminApi>;

  async function setup(
    categories: AiConfigCategoryItem[] = [CAT_LLM, CAT_TTS],
    catalog: AiProviderCatalogItem[] = [PROVIDER],
    tab?: 'llm' | 'tts' | 'credentials' | 'pricing' | 'rate-limits',
  ) {
    adminApi = makeAdminApi(categories, catalog);
    await TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    if (tab) { component.activeTab.set(tab); }
    fixture.detectChanges();
  }

  it('renders the page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('AI Configuration');
  });

  it('calls listAiCategories and listAiProviders on init', async () => {
    await setup();
    expect(adminApi.listAiCategories).toHaveBeenCalledTimes(1);
    expect(adminApi.listAiProviders).toHaveBeenCalledTimes(1);
  });

  it('shows loading state before data arrives', () => {
    adminApi = makeAdminApi();
    // Don't call setup — create manually with synchronous-pending observable
    // loading() starts as true; just check the signal default
    // We test the rendered state via the loading signal directly
    TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    });
    fixture = TestBed.createComponent(AdminAiConfigComponent);
    component = fixture.componentInstance;
    expect(component.loading()).toBeTrue();
  });

  it('shows error state on load failure', async () => {
    adminApi = makeAdminApi();
    adminApi.listAiCategories.and.returnValue(throwError(() => ({ error: { error: 'Server down' } })));
    await TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('unavailable');
  });

  it('renders LLM category rows', async () => {
    await setup([CAT_LLM, CAT_UNSET], [PROVIDER]);
    expect(fixture.nativeElement.textContent).toContain('Default LLM');
    expect(fixture.nativeElement.textContent).toContain('Evaluation LLM');
  });

  it('renders TTS category rows', async () => {
    await setup([CAT_TTS], [], 'tts');
    expect(fixture.nativeElement.textContent).toContain('TTS Listening');
  });

  it('renders configured badge for set category', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    expect(fixture.nativeElement.textContent).toContain('Configured');
  });

  it('renders not-set badge for unset category', async () => {
    await setup([CAT_UNSET], []);
    expect(fixture.nativeElement.textContent).toContain('Not set');
  });

  it('renders TTS disabled badge for unset TTS category', async () => {
    await setup([{ ...CAT_TTS, providerName: null }], [], 'tts');
    expect(fixture.nativeElement.textContent).toContain('TTS disabled');
  });

  it('renders provider rows', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    expect(fixture.nativeElement.textContent).toContain('anthropic');
  });

  it('renders key-stored badge for provider with key', async () => {
    await setup([CAT_LLM], [PROVIDER], 'credentials');
    expect(fixture.nativeElement.textContent).toContain('Key stored');
  });

  it('renders env-var badge for provider without key', async () => {
    await setup([CAT_LLM], [{ ...PROVIDER, hasApiKey: false }], 'credentials');
    expect(fixture.nativeElement.textContent).toContain('Env var');
  });

  it('renders model test chips for each model', async () => {
    await setup([CAT_LLM], [PROVIDER], 'credentials');
    expect(fixture.nativeElement.textContent).toContain('claude-sonnet-4-6');
    expect(fixture.nativeElement.textContent).toContain('claude-haiku-4-5-20251001');
  });

  it('calls updateAiCategory on saveCategory', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const cs = component.llmCategories()[0];
    component.saveCategory(cs);
    tick();
    expect(adminApi.updateAiCategory).toHaveBeenCalledWith('llm.default', jasmine.objectContaining({ providerName: 'anthropic' }));
  }));

  it('calls testAiCategory on testCategory', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const cs = component.llmCategories()[0];
    component.testCategory(cs);
    tick();
    expect(adminApi.testAiCategory).toHaveBeenCalledWith('llm.default');
    expect(cs.testResult).toContain('OK');
  }));

  it('calls testProvider on runTest', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    component.runTest(ps);
    tick();
    expect(adminApi.testProvider).toHaveBeenCalledWith('anthropic');
  }));

  it('calls setProviderApiKey on saveKey', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    ps.editingKey = true;
    ps.editKeyValue = 'sk-test';
    component.saveKey(ps);
    tick();
    expect(adminApi.setProviderApiKey).toHaveBeenCalledWith('anthropic', 'sk-test');
    expect(ps.editingKey).toBeFalse();
  }));

  it('calls clearKey with null on clearKey', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    component.clearKey(ps);
    tick();
    expect(adminApi.setProviderApiKey).toHaveBeenCalledWith('anthropic', null);
  }));

  it('calls addProviderModel on addModel', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    ps.newModelName = 'claude-new-model';
    component.addModel(ps);
    tick();
    expect(adminApi.addProviderModel).toHaveBeenCalledWith('anthropic', 'claude-new-model');
  }));

  it('does not call addProviderModel when modelName is blank', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    ps.newModelName = '  ';
    component.addModel(ps);
    tick();
    expect(adminApi.addProviderModel).not.toHaveBeenCalled();
  }));

  it('calls testProviderModel on testOneModel', fakeAsync(async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    ps.newModelName = 'claude-new-model';
    component.testOneModel(ps);
    tick();
    expect(adminApi.testProviderModel).toHaveBeenCalledWith('anthropic', 'claude-new-model');
  }));

  it('modelChipTone returns neutral for untested model', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const untested = PROVIDER.modelTests[2];
    expect(component.modelChipTone(untested)).toBe('neutral');
  });

  it('modelChipTone returns success for passing model', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    expect(component.modelChipTone(PROVIDER.modelTests[0])).toBe('success');
  });

  it('modelChipTone returns danger for failing model', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    expect(component.modelChipTone(PROVIDER.modelTests[1])).toBe('danger');
  });

  it('toggleKeyEdit toggles editingKey', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const ps = component.providers()[0];
    expect(ps.editingKey).toBeFalse();
    component.toggleKeyEdit(ps);
    expect(ps.editingKey).toBeTrue();
    component.toggleKeyEdit(ps);
    expect(ps.editingKey).toBeFalse();
  });

  it('llmCategories returns only llm. prefixed categories', async () => {
    await setup([CAT_LLM, CAT_TTS, CAT_UNSET], [PROVIDER]);
    const keys = component.llmCategories().map(cs => cs.item.categoryKey);
    expect(keys).toContain('llm.default');
    expect(keys).toContain('llm.evaluation');
    expect(keys).not.toContain('tts.listening');
  });

  it('ttsCategories returns only tts. prefixed categories', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER]);
    const keys = component.ttsCategories().map(cs => cs.item.categoryKey);
    expect(keys).toContain('tts.listening');
    expect(keys).not.toContain('llm.default');
  });

  // ── Pricing panel ──────────────────────────────────────────────────────────

  it('calls listAiPricing on init', async () => {
    await setup();
    expect(adminApi.listAiPricing).toHaveBeenCalledTimes(1);
  });

  it('populates pricing signal after load', async () => {
    await setup();
    expect(component.pricing().length).toBe(2);
  });

  it('pricingByProvider groups rows by provider', async () => {
    await setup();
    const groups = component.pricingByProvider();
    const providers = groups.map(g => g.provider);
    expect(providers).toContain('anthropic');
    expect(providers).toContain('openai');
  });

  it('renders pricing section heading', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Model Pricing');
  });

  it('renders read-only configuration note', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER], 'pricing');
    expect(fixture.nativeElement.textContent).toContain('Config pricing');
  });

  it('renders model names in pricing table', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER], 'pricing');
    expect(fixture.nativeElement.textContent).toContain('claude-sonnet-4-6');
    expect(fixture.nativeElement.textContent).toContain('gpt-4o');
  });

  it('renders empty state when no pricing rows', async () => {
    adminApi = makeAdminApi([CAT_LLM], [PROVIDER], []);
    await TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    component.activeTab.set('pricing');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No pricing configured');
  });

  it('does not render edit buttons in pricing section', async () => {
    await setup();
    const pricingSection = (fixture.nativeElement as HTMLElement)
      .querySelector('sp-admin-card[title="Model Pricing"], sp-admin-card');
    // No edit/save buttons should appear for pricing rows
    const text = fixture.nativeElement.textContent as string;
    // Pricing section note present, no "Edit pricing" or "Save pricing" text
    expect(text).not.toContain('Edit pricing');
    expect(text).not.toContain('Save pricing');
  });

  // ── Pricing override panel ─────────────────────────────────────────────────

  it('calls listAiPricingOverrides on init', async () => {
    await setup();
    expect(adminApi.listAiPricingOverrides).toHaveBeenCalledTimes(1);
  });

  it('overrides signal is empty on init when no overrides exist', async () => {
    await setup();
    expect(component.overrides().length).toBe(0);
  });

  it('renders Pricing overrides section heading', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER], 'pricing');
    expect(fixture.nativeElement.textContent).toContain('Pricing overrides');
  });

  it('renders empty state when no overrides', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER], 'pricing');
    expect(fixture.nativeElement.textContent).toContain('No overrides');
  });

  it('renders Add override button when form is closed', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER], 'pricing');
    expect(fixture.nativeElement.textContent).toContain('Add override');
  });

  it('openCreateOverride sets overrideForm to create mode', async () => {
    await setup();
    component.openCreateOverride();
    expect(component.overrideForm()).not.toBeNull();
    expect(component.overrideForm()!.mode).toBe('create');
  });

  it('cancelOverrideForm clears the form', async () => {
    await setup();
    component.openCreateOverride();
    component.cancelOverrideForm();
    expect(component.overrideForm()).toBeNull();
  });

  it('saveOverride with empty modelName sets error', async () => {
    await setup();
    component.openCreateOverride();
    const f = component.overrideForm()!;
    component['overrideForm'].set({ ...f, providerName: '', modelName: '', inputPricePer1KTokens: 0.002, outputPricePer1KTokens: 0.008 });
    component.saveOverride();
    expect(component.overrideForm()!.error).toContain('Select a model');
  });

  it('saveOverride with negative price sets error', async () => {
    await setup();
    component.openCreateOverride();
    const f = component.overrideForm()!;
    component['overrideForm'].set({ ...f, providerName: 'openai', modelName: 'gpt-4o', inputPricePer1KTokens: -1, outputPricePer1KTokens: 0.008 });
    component.saveOverride();
    expect(component.overrideForm()!.error).toContain('Prices must be');
  });

  it('openEditOverride sets form to edit mode with override data', async () => {
    await setup();
    const override: AiModelPricingOverrideItem = {
      id: 'test-id', providerName: 'openai', modelName: 'gpt-4o',
      inputPricePer1KTokens: 0.005, outputPricePer1KTokens: 0.015,
      currency: 'USD', isActive: true,
      effectiveFromUtc: '2026-01-01T00:00:00Z', effectiveToUtc: null,
      notes: 'test note', createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: null, createdByAdminUserId: null, updatedByAdminUserId: null,
    };
    component.openEditOverride(override);
    const f = component.overrideForm()!;
    expect(f.mode).toBe('edit');
    expect(f.id).toBe('test-id');
    expect(f.inputPricePer1KTokens).toBe(0.005);
    expect(f.notes).toBe('test note');
  });

  // ── REDESIGN-5 KPI strip and configSummary ────────────────────────────────

  it('renders kpi tile cards for the summary strip', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER]);
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('.sp-aic-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('summary strip has aria-label "AI configuration summary"', async () => {
    await setup([CAT_LLM, CAT_TTS], [PROVIDER]);
    const strip = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="AI configuration summary"]');
    expect(strip).toBeTruthy();
  });

  it('configSummary.llmConfigured counts configured LLM categories', async () => {
    await setup([CAT_LLM, CAT_UNSET], [PROVIDER]);
    expect(component.configSummary().llmConfigured).toBe(1);
    expect(component.configSummary().llmTotal).toBe(2);
  });

  it('configSummary.ttsConfigured counts configured TTS categories', async () => {
    await setup([CAT_TTS], [PROVIDER]);
    expect(component.configSummary().ttsConfigured).toBe(1);
    expect(component.configSummary().ttsTotal).toBe(1);
  });

  it('configSummary.providersWithKey counts providers with stored key', async () => {
    await setup([CAT_LLM], [PROVIDER, { ...PROVIDER, providerName: 'openai', hasApiKey: false }]);
    expect(component.configSummary().providersWithKey).toBe(1);
  });

  it('configSummary.pricingModels counts pricing rows', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    expect(component.configSummary().pricingModels).toBe(PRICING_ROWS.length);
  });

  it('renders configured badge on LLM categories card header', async () => {
    await setup([CAT_LLM, CAT_UNSET], [PROVIDER]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1/2 configured');
  });

  it('renders Rate limits card with "Backend not available yet"', async () => {
    await setup([CAT_LLM], [PROVIDER], 'rate-limits');
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Backend not available yet');
    expect(el.querySelector('[aria-label="Rate limits not implemented"]')).toBeTruthy();
  });

  it('API keys are not displayed in any rendered text', async () => {
    await setup([CAT_LLM], [PROVIDER]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('sk-proj-');
    expect(text).not.toContain('AIza');
    expect(text).not.toContain('sk-ant-');
  });

  it('overrides with active entry renders Edit and Deactivate buttons', async () => {
    const override: AiModelPricingOverrideItem = {
      id: 'ov-1', providerName: 'openai', modelName: 'gpt-4o',
      inputPricePer1KTokens: 0.002, outputPricePer1KTokens: 0.008,
      currency: 'USD', isActive: true,
      effectiveFromUtc: '2026-01-01T00:00:00Z', effectiveToUtc: null,
      notes: null, createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: null, createdByAdminUserId: null, updatedByAdminUserId: null,
    };
    adminApi = makeAdminApi();
    adminApi.listAiPricingOverrides.and.returnValue(of([override]));
    await TestBed.configureTestingModule({
      imports: [AdminAiConfigComponent],
      providers: [{ provide: AdminApiService, useValue: adminApi }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    component.activeTab.set('pricing');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
    expect(fixture.nativeElement.textContent).toContain('Deactivate');
  });
});

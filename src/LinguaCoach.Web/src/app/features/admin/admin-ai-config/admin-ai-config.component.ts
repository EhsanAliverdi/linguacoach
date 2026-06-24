import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AiConfigCategoryItem, AiModelPricingItem, AiModelPricingOverrideItem,
  AiProviderCatalogItem, ModelTestStatus,
  CreatePricingOverrideRequest, UpdatePricingOverrideRequest,
} from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminSlideOverComponent,
} from '../../../design-system/admin';

type AiConfigTab = 'llm' | 'tts' | 'credentials' | 'pricing' | 'rate-limits';

interface CategoryState {
  item: AiConfigCategoryItem;
  saving: boolean;
  saved: boolean;
  error: string;
  editingProvider: string | null;
  editingModel: string | null;
  editingVoice: string | null;
  testBusy: boolean;
  testResult: string;
}

interface OverrideFormState {
  mode: 'create' | 'edit';
  id: string | null;
  providerName: string;
  modelName: string;
  inputPricePer1KTokens: number | null;
  outputPricePer1KTokens: number | null;
  currency: string;
  effectiveFromUtc: string;
  effectiveToUtc: string;
  notes: string;
  busy: boolean;
  error: string;
}

interface ProviderState {
  catalog: AiProviderCatalogItem;
  editingKey: boolean;
  editKeyValue: string;
  saveKeyBusy: boolean;
  saveKeyError: string;
  editingEndpoint: boolean;
  editEndpointValue: string;
  saveEndpointBusy: boolean;
  saveEndpointError: string;
  testBusy: boolean;
  newModelName: string;
  addModelBusy: boolean;
  addModelError: string;
}

const CATEGORY_DESCRIPTIONS: Record<string, string> = {
  'llm.default':    'Fallback for all LLM features without a category-specific override.',
  'llm.generation': 'Builds content: writing, listening, speaking, role-play, email reply.',
  'llm.evaluation': 'Evaluates attempts: scoring, feedback, placement assessment.',
  'llm.memory':     'Builds and updates the student learning path and memory profile.',
  'tts.listening':  'Audio for learning activities. Supports openai, gemini, qwen voices.',
  'tts.placement':  'Audio for placement assessment. Anthropic has no TTS API.',
};

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    SpAdminAlertComponent, SpAdminBadgeComponent, SpAdminCardComponent,
    SpAdminCodePillComponent, SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent, SpAdminButtonComponent,
    SpAdminEmptyStateComponent, SpAdminErrorStateComponent,
    SpAdminFormFieldComponent, SpAdminInputComponent,
    SpAdminLoadingStateComponent, SpAdminSlideOverComponent,
  ],
  templateUrl: './admin-ai-config.component.html',
  styleUrl: './admin-ai-config.component.css',
})
export class AdminAiConfigComponent implements OnInit {
  categories = signal<CategoryState[]>([]);
  providers = signal<ProviderState[]>([]);
  pricing = signal<AiModelPricingItem[]>([]);
  overrides = signal<AiModelPricingOverrideItem[]>([]);
  overrideForm = signal<OverrideFormState | null>(null);
  deactivateBusy = signal<string | null>(null);
  loading = signal(true);
  loadError = signal('');
  testAllBusy = signal(false);

  configuringCategory = signal<CategoryState | null>(null);

  readonly tabs: { key: AiConfigTab; label: string }[] = [
    { key: 'llm',         label: 'LLM Categories' },
    { key: 'tts',         label: 'Text-to-Speech' },
    { key: 'credentials', label: 'Provider Credentials' },
    { key: 'pricing',     label: 'Model Pricing' },
    { key: 'rate-limits', label: 'Rate Limits' },
  ];
  activeTab = signal<AiConfigTab>('llm');

  readonly overrideTableHeaders = ['Model', 'Group', 'Input /K', 'Output /K', 'Effective', 'Note'];

  readonly configSummary = computed(() => {
    const cats = this.categories();
    const llm = cats.filter(cs => cs.item.categoryKey.startsWith('llm.'));
    const tts = cats.filter(cs => cs.item.categoryKey.startsWith('tts.'));
    const isSet = (cs: CategoryState) => !!cs.item.providerName && cs.item.providerName !== 'fake';
    return {
      llmConfigured: llm.filter(isSet).length,
      llmTotal: llm.length,
      ttsConfigured: tts.filter(isSet).length,
      ttsTotal: tts.length,
      providersWithKey: this.providers().filter(ps => ps.catalog.hasApiKey).length,
      pricingModels: this.pricing().length,
    };
  });

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    forkJoin({
      categories: this.adminApi.listAiCategories(),
      catalog: this.adminApi.listAiProviders(),
      pricing: this.adminApi.listAiPricing(),
      overrides: this.adminApi.listAiPricingOverrides(),
    }).subscribe({
      next: ({ categories, catalog, pricing, overrides }) => {
        this.categories.set(categories.map(item => ({
          item,
          saving: false, saved: false, error: '',
          editingProvider: item.providerName,
          editingModel: item.modelName,
          editingVoice: item.voiceName,
          testBusy: false,
          testResult: '',
        })));
        this.providers.set(catalog.map(c => ({
          catalog: c,
          editingKey: false, editKeyValue: '',
          saveKeyBusy: false, saveKeyError: '',
          editingEndpoint: false, editEndpointValue: '',
          saveEndpointBusy: false, saveEndpointError: '',
          testBusy: false,
          newModelName: '',
          addModelBusy: false,
          addModelError: '',
        })));
        this.pricing.set(pricing);
        this.overrides.set(overrides);
        this.loading.set(false);
      },
      error: err => {
        this.loadError.set(err.error?.error ?? 'Could not load AI configuration.');
        this.loading.set(false);
      },
    });
  }

  // ── Configure drawer ───────────────────────────────────────────────────────

  openConfigureDrawer(cs: CategoryState): void {
    this.configuringCategory.set(cs);
  }

  closeConfigureDrawer(): void {
    this.configuringCategory.set(null);
  }

  saveConfigureDrawer(): void {
    const cs = this.configuringCategory();
    if (!cs) return;
    this.saveCategory(cs);
  }

  isTtsKey(key: string): boolean {
    return key.startsWith('tts.');
  }

  isConfigured(cs: CategoryState): boolean {
    return !!cs.item.providerName && cs.item.providerName !== 'fake';
  }

  // ── Override management ────────────────────────────────────────────────────

  private toDateLocal(utcStr: string | null): string {
    if (!utcStr) return '';
    return utcStr.slice(0, 10);
  }

  onOverrideModelChange(f: OverrideFormState, modelName: string): void {
    for (const group of this.pricingByProvider()) {
      if (group.rows.some(r => r.modelName === modelName)) {
        f.providerName = group.provider;
        return;
      }
    }
  }

  baseConfigPricing(modelName: string): { input: string; output: string } | null {
    const row = this.pricing().find(r => r.modelName === modelName);
    if (!row) return null;
    return {
      input: row.inputPer1KTokens.toFixed(5),
      output: row.outputPer1KTokens.toFixed(5),
    };
  }

  openCreateOverride(): void {
    this.overrideForm.set({
      mode: 'create', id: null,
      providerName: '', modelName: '',
      inputPricePer1KTokens: null, outputPricePer1KTokens: null,
      currency: 'USD',
      effectiveFromUtc: '',
      effectiveToUtc: '', notes: '',
      busy: false, error: '',
    });
  }

  openEditOverride(o: AiModelPricingOverrideItem): void {
    this.overrideForm.set({
      mode: 'edit', id: o.id,
      providerName: o.providerName, modelName: o.modelName,
      inputPricePer1KTokens: o.inputPricePer1KTokens,
      outputPricePer1KTokens: o.outputPricePer1KTokens,
      currency: o.currency,
      effectiveFromUtc: this.toDateLocal(o.effectiveFromUtc),
      effectiveToUtc: this.toDateLocal(o.effectiveToUtc),
      notes: o.notes ?? '',
      busy: false, error: '',
    });
  }

  cancelOverrideForm(): void {
    this.overrideForm.set(null);
  }

  saveOverride(): void {
    const f = this.overrideForm();
    if (!f) return;
    if (!f.modelName.trim()) {
      this.overrideForm.set({ ...f, error: 'Select a model.' });
      return;
    }
    if (f.inputPricePer1KTokens === null || f.outputPricePer1KTokens === null ||
        f.inputPricePer1KTokens < 0 || f.outputPricePer1KTokens < 0) {
      this.overrideForm.set({ ...f, error: 'Prices must be >= 0.' });
      return;
    }
    this.overrideForm.set({ ...f, busy: true, error: '' });

    const toUtc = (d: string): string => d ? new Date(d).toISOString() : '';

    if (f.mode === 'create') {
      const cmd: CreatePricingOverrideRequest = {
        providerName: f.providerName.trim() || '',
        modelName: f.modelName.trim(),
        inputPricePer1KTokens: f.inputPricePer1KTokens,
        outputPricePer1KTokens: f.outputPricePer1KTokens,
        currency: f.currency || 'USD',
        effectiveFromUtc: toUtc(f.effectiveFromUtc),
        effectiveToUtc: f.effectiveToUtc ? toUtc(f.effectiveToUtc) : null,
        notes: f.notes || null,
      };
      this.adminApi.createAiPricingOverride(cmd).subscribe({
        next: created => { this.overrides.update(list => [created, ...list]); this.overrideForm.set(null); },
        error: err => this.overrideForm.set({ ...f, busy: false, error: err.error?.error ?? err.error?.title ?? 'Failed to create override.' }),
      });
    } else {
      const cmd: UpdatePricingOverrideRequest = {
        inputPricePer1KTokens: f.inputPricePer1KTokens,
        outputPricePer1KTokens: f.outputPricePer1KTokens,
        currency: f.currency || 'USD',
        effectiveFromUtc: toUtc(f.effectiveFromUtc),
        effectiveToUtc: f.effectiveToUtc ? toUtc(f.effectiveToUtc) : null,
        notes: f.notes || null,
      };
      this.adminApi.updateAiPricingOverride(f.id!, cmd).subscribe({
        next: updated => { this.overrides.update(list => list.map(o => o.id === updated.id ? updated : o)); this.overrideForm.set(null); },
        error: err => this.overrideForm.set({ ...f, busy: false, error: err.error?.error ?? err.error?.title ?? 'Failed to update override.' }),
      });
    }
  }

  deactivateOverride(id: string): void {
    if (!confirm('Deactivate this pricing override?')) return;
    this.deactivateBusy.set(id);
    this.adminApi.deactivateAiPricingOverride(id).subscribe({
      next: () => { this.overrides.update(list => list.map(o => o.id === id ? { ...o, isActive: false } : o)); this.deactivateBusy.set(null); },
      error: () => this.deactivateBusy.set(null),
    });
  }

  pricingByProvider(): { provider: string; rows: AiModelPricingItem[] }[] {
    const map = new Map<string, AiModelPricingItem[]>();
    for (const row of this.pricing()) {
      const list = map.get(row.providerName) ?? [];
      list.push(row);
      map.set(row.providerName, list);
    }
    return Array.from(map.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([provider, rows]) => ({ provider, rows }));
  }

  llmCategories(): CategoryState[] {
    return this.categories().filter(cs => cs.item.categoryKey.startsWith('llm.'));
  }

  ttsCategories(): CategoryState[] {
    return this.categories().filter(cs => cs.item.categoryKey.startsWith('tts.'));
  }

  categoryDesc(key: string): string {
    return CATEGORY_DESCRIPTIONS[key] ?? '';
  }

  modelsFor(providerName: string): string[] {
    return this.providers().find(p => p.catalog.providerName === providerName)?.catalog.models ?? [];
  }

  private static readonly TTS_MODELS: Record<string, string[]> = {
    openai: ['tts-1', 'tts-1-hd'],
    gemini: ['gemini-2.5-flash-preview-tts', 'gemini-2.5-pro-preview-tts', 'gemini-3.1-flash-tts-preview'],
    qwen: ['cosyvoice-v2'],
  };

  ttsModelsFor(providerName: string): string[] {
    const staticModels = AdminAiConfigComponent.TTS_MODELS[providerName] ?? [];
    const providerModels = this.modelsFor(providerName).filter(m => this.isTtsModel(providerName, m));
    return Array.from(new Set([...staticModels, ...providerModels]));
  }

  onCategoryProviderChange(cs: CategoryState, provider: string | null): void {
    cs.editingProvider = provider;
    if (!provider || provider === 'fake') {
      cs.editingModel = null;
    } else if (!cs.editingModel || !this.modelsFor(provider).includes(cs.editingModel)) {
      cs.editingModel = this.modelsFor(provider)[0] ?? null;
    }
  }

  onTtsProviderChange(cs: CategoryState, provider: string | null): void {
    cs.editingProvider = provider;
    if (!provider || provider === 'fake') {
      cs.editingModel = null;
      cs.editingVoice = null;
    } else if (!cs.editingModel || !this.ttsModelsFor(provider).includes(cs.editingModel)) {
      cs.editingModel = this.ttsModelsFor(provider)[0] ?? null;
    }
  }

  private isTtsModel(providerName: string, modelName: string): boolean {
    const provider = providerName.toLowerCase();
    const model = modelName.toLowerCase();
    if (provider === 'openai') return model.startsWith('tts-');
    if (provider === 'gemini') return model.includes('-tts');
    if (provider === 'qwen') return model === 'cosyvoice-v2';
    return false;
  }

  saveCategory(cs: CategoryState): void {
    cs.saving = true; cs.error = '';
    this.adminApi.updateAiCategory(cs.item.categoryKey, {
      providerName: cs.editingProvider || null,
      modelName: cs.editingModel || null,
      voiceName: cs.editingVoice || null,
    }).subscribe({
      next: updated => {
        cs.item = updated;
        cs.editingProvider = updated.providerName;
        cs.editingModel = updated.modelName;
        cs.editingVoice = updated.voiceName;
        cs.saving = false; cs.saved = true;
        this.categories.update(list => [...list]);
        setTimeout(() => { cs.saved = false; this.closeConfigureDrawer(); }, 1000);
      },
      error: err => { cs.error = err.error?.error ?? 'Failed.'; cs.saving = false; },
    });
  }

  testCategory(cs: CategoryState): void {
    cs.testBusy = true;
    cs.testResult = '';
    this.adminApi.testAiCategory(cs.item.categoryKey).subscribe({
      next: result => {
        cs.testBusy = false;
        cs.testResult = result.ok ? `OK (${result.latencyMs}ms)` : (result.error ?? 'Test failed.');
      },
      error: err => {
        cs.testBusy = false;
        cs.testResult = err.error?.error ?? 'Test failed.';
      },
    });
  }

  testAllConnections(): void {
    this.testAllBusy.set(true);
    const providers = this.providers();
    let remaining = providers.length;
    if (remaining === 0) { this.testAllBusy.set(false); return; }
    for (const ps of providers) {
      this.adminApi.testProvider(ps.catalog.providerName).subscribe({
        next: updated => {
          ps.catalog = updated;
          remaining--;
          if (remaining === 0) this.testAllBusy.set(false);
        },
        error: () => {
          remaining--;
          if (remaining === 0) this.testAllBusy.set(false);
        },
      });
    }
  }

  // ── Model chip helpers ─────────────────────────────────────────────────────

  hasBeenTested(m: ModelTestStatus): boolean {
    return m.testedAt !== '0001-01-01T00:00:00' && m.testedAt !== null;
  }

  modelChipTone(m: ModelTestStatus): 'neutral' | 'success' | 'danger' {
    if (!this.hasBeenTested(m)) return 'neutral';
    return m.ok ? 'success' : 'danger';
  }

  modelChipTitle(m: ModelTestStatus): string {
    if (!this.hasBeenTested(m)) return 'Not tested yet';
    if (m.ok) return `OK -- ${m.latencyMs}ms`;
    return m.error ?? 'Failed';
  }

  // ── Provider credentials ───────────────────────────────────────────────────

  keyPlaceholder(provider: string): string {
    return ({ openai: 'sk-...', gemini: 'AIza...', anthropic: 'sk-ant-...', qwen: 'sk-...' } as Record<string, string>)[provider] ?? '...';
  }

  hasEndpointConfig(provider: string): boolean {
    return provider === 'qwen';
  }

  toggleKeyEdit(ps: ProviderState): void {
    ps.editingKey = !ps.editingKey;
    ps.editKeyValue = '';
    ps.saveKeyError = '';
  }

  toggleQwenConfig(ps: ProviderState): void {
    ps.editingKey = !ps.editingKey;
    ps.editKeyValue = '';
    ps.editEndpointValue = ps.catalog.apiEndpoint ?? '';
    ps.saveKeyError = '';
    ps.saveEndpointError = '';
  }

  saveQwenConfig(ps: ProviderState): void {
    ps.saveKeyBusy = true;
    ps.saveKeyError = '';
    ps.saveEndpointError = '';
    this.adminApi.setProviderApiKey(ps.catalog.providerName, ps.editKeyValue || null).subscribe({
      next: updated => {
        ps.catalog = updated;
        ps.saveKeyBusy = false;
        ps.saveEndpointBusy = true;
        this.adminApi.setProviderEndpoint(ps.catalog.providerName, ps.editEndpointValue || null).subscribe({
          next: updated2 => { ps.catalog = updated2; ps.editingKey = false; ps.saveEndpointBusy = false; },
          error: err => { ps.saveEndpointError = err.error?.error ?? 'Failed to save endpoint.'; ps.saveEndpointBusy = false; },
        });
      },
      error: err => { ps.saveKeyError = err.error?.error ?? 'Failed to save key.'; ps.saveKeyBusy = false; },
    });
  }

  saveKey(ps: ProviderState): void {
    ps.saveKeyBusy = true; ps.saveKeyError = '';
    this.adminApi.setProviderApiKey(ps.catalog.providerName, ps.editKeyValue || null).subscribe({
      next: updated => { ps.catalog = updated; ps.editingKey = false; ps.saveKeyBusy = false; },
      error: err => { ps.saveKeyError = err.error?.error ?? 'Failed.'; ps.saveKeyBusy = false; },
    });
  }

  clearKey(ps: ProviderState): void {
    ps.saveKeyBusy = true;
    this.adminApi.setProviderApiKey(ps.catalog.providerName, null).subscribe({
      next: updated => { ps.catalog = updated; ps.editingKey = false; ps.saveKeyBusy = false; },
      error: err => { ps.saveKeyError = err.error?.error ?? 'Failed.'; ps.saveKeyBusy = false; },
    });
  }

  saveEndpoint(ps: ProviderState): void {
    ps.saveEndpointBusy = true; ps.saveEndpointError = '';
    this.adminApi.setProviderEndpoint(ps.catalog.providerName, ps.editEndpointValue || null).subscribe({
      next: updated => { ps.catalog = updated; ps.editingEndpoint = false; ps.saveEndpointBusy = false; },
      error: err => { ps.saveEndpointError = err.error?.error ?? 'Failed.'; ps.saveEndpointBusy = false; },
    });
  }

  clearEndpoint(ps: ProviderState): void {
    ps.saveEndpointBusy = true;
    this.adminApi.setProviderEndpoint(ps.catalog.providerName, null).subscribe({
      next: updated => { ps.catalog = updated; ps.editingEndpoint = false; ps.saveEndpointBusy = false; },
      error: err => { ps.saveEndpointError = err.error?.error ?? 'Failed.'; ps.saveEndpointBusy = false; },
    });
  }

  runTest(ps: ProviderState): void {
    ps.testBusy = true;
    this.adminApi.testProvider(ps.catalog.providerName).subscribe({
      next: updated => { ps.catalog = updated; ps.testBusy = false; },
      error: err => {
        ps.catalog = {
          ...ps.catalog,
          modelTests: ps.catalog.modelTests.map(m => ({
            ...m, ok: false,
            error: err.error?.error ?? 'Request failed.',
            testedAt: new Date().toISOString(),
          }))
        };
        ps.testBusy = false;
      },
    });
  }

  addModel(ps: ProviderState): void {
    const modelName = ps.newModelName.trim();
    if (!modelName) return;
    ps.addModelBusy = true;
    ps.addModelError = '';
    this.adminApi.addProviderModel(ps.catalog.providerName, modelName).subscribe({
      next: updated => { ps.catalog = updated; ps.newModelName = ''; ps.addModelBusy = false; },
      error: err => { ps.addModelError = err.error?.error ?? 'Failed to add model.'; ps.addModelBusy = false; },
    });
  }

  testOneModel(ps: ProviderState): void {
    const modelName = ps.newModelName.trim();
    if (!modelName) return;
    ps.testBusy = true;
    ps.addModelError = '';
    this.adminApi.testProviderModel(ps.catalog.providerName, modelName).subscribe({
      next: updated => { ps.catalog = updated; ps.testBusy = false; },
      error: err => { ps.addModelError = err.error?.error ?? 'Model test failed.'; ps.testBusy = false; },
    });
  }
}

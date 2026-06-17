import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiConfigCategoryItem, AiProviderCatalogItem, ModelTestStatus } from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminCardComponent,
  SpAdminPageHeaderComponent,
} from '../../../admin';

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
  'llm.default': 'Fallback for all LLM features that do not have a category-specific override.',
  'llm.generation': 'Generates activity content: writing, listening, speaking, role-play, email reply.',
  'llm.evaluation': 'Evaluates student attempts: scoring, feedback, placement assessment.',
  'llm.memory': 'Builds and updates the student learning path and memory profile.',
  'tts.listening': 'Text-to-speech for listening activities. Supports openai (voice e.g. onyx), gemini (voice e.g. Kore), qwen (voice e.g. longxiaochun_v2).',
  'tts.placement': 'Text-to-speech for placement assessment. Supports openai, gemini, and qwen providers. Anthropic has no TTS API.',
};

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule, SpAdminBadgeComponent, SpAdminCardComponent, SpAdminPageHeaderComponent],
  template: `
    <sp-admin-page-header title="AI Configuration" subtitle="Category-level AI provider config, TTS voices, and provider credentials" />

    @if (loading()) {
      <div class="sp-admin-table-loading">Loading…</div>
    } @else {

      <!-- ── Section 1: LLM Categories ─────────────────────────────────── -->
      <sp-admin-card title="LLM Categories" class="sp-admin-section-wrap">
        <h2 class="text-base font-semibold text-slate-900 mb-1">LLM Categories</h2>
        <p class="text-sm text-slate-500 mb-4">
          Set a provider and model per category. Resolution order: category-specific → Default LLM → 503 error.
        </p>

        <div class="grid gap-4 sm:grid-cols-2">
          @for (cs of llmCategories(); track cs.item.categoryKey) {
            <div [class]="'rounded-xl border bg-white shadow-sm p-5 ' + (cs.item.providerName && cs.item.providerName !== 'fake' ? 'border-slate-200' : 'border-amber-200')">
              <div class="flex items-start justify-between gap-3 mb-3">
                <div>
                  <div class="text-sm font-bold text-slate-900">{{ cs.item.displayName }}</div>
                  <div class="text-xs font-mono text-slate-400 mt-0.5">{{ cs.item.categoryKey }}</div>
                </div>
                @if (cs.item.providerName && cs.item.providerName !== 'fake') {
                  <sp-admin-badge tone="success">Configured</sp-admin-badge>
                } @else {
                  <sp-admin-badge tone="warning">Not set</sp-admin-badge>
                }
              </div>

              <p class="text-xs text-slate-500 mb-4">{{ categoryDesc(cs.item.categoryKey) }}</p>

              <div class="grid grid-cols-2 gap-3">
                <label>
                  <span class="block text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Provider</span>
                  <select [(ngModel)]="cs.editingProvider" (ngModelChange)="onCategoryProviderChange(cs, $event)" class="sp-ai-select">
                    <option [ngValue]="null">— inherit —</option>
                    <option value="fake">fake (disable)</option>
                    @for (p of providers(); track p.catalog.providerName) {
                      <option [value]="p.catalog.providerName">{{ p.catalog.providerName }}</option>
                    }
                  </select>
                </label>

                <label>
                  <span class="block text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Model</span>
                  <select [(ngModel)]="cs.editingModel" [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'" class="sp-ai-select sp-ai-model-select">
                    <option [ngValue]="null">— inherit —</option>
                    @for (m of modelsFor(cs.editingProvider ?? ''); track m) {
                      <option [value]="m">{{ m }}</option>
                    }
                  </select>
                </label>
              </div>

              <div class="mt-3 flex items-center gap-3">
                <button (click)="saveCategory(cs)" [disabled]="cs.saving"
                  class="rounded-lg bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:opacity-50">
                  {{ cs.saving ? 'Saving…' : 'Save' }}
                </button>
                <button (click)="testCategory(cs)" [disabled]="cs.testBusy"
                  class="rounded-lg border border-slate-300 px-4 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                  {{ cs.testBusy ? 'Testing...' : 'Test' }}
                </button>
                @if (cs.saved) { <span class="text-xs text-emerald-600">Saved</span> }
                @if (cs.error) { <span class="text-xs text-red-500">{{ cs.error }}</span> }
                @if (cs.testResult) { <span class="text-xs text-slate-600">{{ cs.testResult }}</span> }
              </div>
            </div>
          }
        </div>
      </sp-admin-card>

      <!-- ── Section 2: TTS Categories ─────────────────────────────────── -->
      <sp-admin-card title="Text-to-Speech" class="sp-admin-section-wrap">
        <h2 class="text-base font-semibold text-slate-900 mb-1">Text-to-Speech</h2>
        <p class="text-sm text-slate-500 mb-4">
          TTS is independent of LLM config. Supports openai, gemini, and qwen. Anthropic has no TTS API. Leave blank to disable TTS (returns 503).
        </p>

        <div class="grid gap-4 sm:grid-cols-2">
          @for (cs of ttsCategories(); track cs.item.categoryKey) {
            <div [class]="'rounded-xl border bg-white shadow-sm p-5 ' + (cs.item.providerName && cs.item.providerName !== 'fake' ? 'border-slate-200' : 'border-amber-200')">
              <div class="flex items-start justify-between gap-3 mb-3">
                <div>
                  <div class="text-sm font-bold text-slate-900">{{ cs.item.displayName }}</div>
                  <div class="text-xs font-mono text-slate-400 mt-0.5">{{ cs.item.categoryKey }}</div>
                </div>
                @if (cs.item.providerName && cs.item.providerName !== 'fake') {
                  <sp-admin-badge tone="success">Configured</sp-admin-badge>
                } @else {
                  <sp-admin-badge tone="warning">TTS disabled</sp-admin-badge>
                }
              </div>

              <p class="text-xs text-slate-500 mb-4">{{ categoryDesc(cs.item.categoryKey) }}</p>

              <div class="grid grid-cols-3 gap-3">
                <label>
                  <span class="block text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Provider</span>
                  <select [(ngModel)]="cs.editingProvider" (ngModelChange)="onTtsProviderChange(cs, $event)" class="sp-ai-select">
                    <option [ngValue]="null">— disable —</option>
                    <option value="openai">openai</option>
                    <option value="gemini">gemini</option>
                    <option value="qwen">qwen</option>
                  </select>
                </label>

                <label>
                  <span class="block text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Model</span>
                  <select [(ngModel)]="cs.editingModel" [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'" class="sp-ai-select sp-ai-model-select">
                    <option [ngValue]="null">— default —</option>
                    @for (m of ttsModelsFor(cs.editingProvider ?? ''); track m) {
                      <option [value]="m">{{ m }}</option>
                    }
                  </select>
                </label>

                <label>
                  <span class="block text-xs font-semibold uppercase tracking-wide text-slate-500 mb-1">Voice</span>
                  <input type="text" [(ngModel)]="cs.editingVoice"
                    [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'"
                    placeholder="e.g. onyx"
                    class="sp-ai-select sp-ai-model-select" />
                </label>
              </div>

              <div class="mt-3 flex items-center gap-3">
                <button (click)="saveCategory(cs)" [disabled]="cs.saving"
                  class="rounded-lg bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:opacity-50">
                  {{ cs.saving ? 'Saving…' : 'Save' }}
                </button>
                <button (click)="testCategory(cs)" [disabled]="cs.testBusy"
                  class="rounded-lg border border-slate-300 px-4 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                  {{ cs.testBusy ? 'Testing...' : 'Test audio' }}
                </button>
                @if (cs.saved) { <span class="text-xs text-emerald-600">Saved</span> }
                @if (cs.error) { <span class="text-xs text-red-500">{{ cs.error }}</span> }
                @if (cs.testResult) { <span class="text-xs text-slate-600">{{ cs.testResult }}</span> }
              </div>
            </div>
          }
        </div>
      </sp-admin-card>

      <!-- ── Section 3: Provider credentials ────────────────────────────── -->
      <sp-admin-card title="Provider credentials" class="sp-admin-section-wrap">
        <h2 class="text-base font-semibold text-slate-900 mb-1">Provider credentials</h2>
        <p class="text-sm text-slate-500 mb-4">
          One API key per provider applies to all features using it.
          "Test connection" checks every model with the stored key.
        </p>

        <div class="space-y-4">
          @for (ps of providers(); track ps.catalog.providerName) {
            <div class="rounded-xl border border-slate-200 bg-white shadow-sm p-5">

              <!-- Header row: provider name + status badges + action buttons -->
              <div class="flex items-center justify-between gap-4 mb-4">
                <div class="flex items-center gap-3">
                  <span class="text-sm font-bold text-slate-800 capitalize w-24">{{ ps.catalog.providerName }}</span>
                  @if (ps.catalog.hasApiKey) {
                    <sp-admin-badge tone="success">Key stored</sp-admin-badge>
                  } @else {
                    <sp-admin-badge tone="neutral">Using env var</sp-admin-badge>
                  }
                  @if (hasEndpointConfig(ps.catalog.providerName) && ps.catalog.apiEndpoint) {
                    <sp-admin-badge tone="info">Endpoint set</sp-admin-badge>
                  }
                </div>
                <div class="flex items-center gap-2">
                  @if (hasEndpointConfig(ps.catalog.providerName)) {
                    <!-- Qwen: single Configure button opens unified form -->
                    <button (click)="toggleQwenConfig(ps)" class="text-xs font-medium text-indigo-600 hover:underline">
                      Configure
                    </button>
                  } @else {
                    <button (click)="toggleKeyEdit(ps)" class="text-xs font-medium text-indigo-600 hover:underline">
                      {{ ps.catalog.hasApiKey ? 'Update key' : 'Set key' }}
                    </button>
                  }
                  <button (click)="runTest(ps)" [disabled]="ps.testBusy"
                    class="inline-flex items-center gap-1.5 text-xs font-medium rounded-lg border border-slate-300 px-3 py-1.5 hover:bg-slate-50 disabled:opacity-50 transition-colors">
                    @if (ps.testBusy) {
                      <span class="animate-spin h-3 w-3 border-2 border-slate-400 border-t-transparent rounded-full"></span>
                      Testing all models…
                    } @else {
                      Test connection
                    }
                  </button>
                </div>
              </div>

              <!-- Model test chips -->
              <div class="flex flex-wrap gap-2">
                @for (m of ps.catalog.modelTests; track m.modelName) {
                  <div [class]="modelChipClass(m)" [title]="modelChipTitle(m)">
                    <span [class]="modelDotClass(m)"></span>
                    <span class="font-mono">{{ m.modelName }}</span>
                    @if (hasBeenTested(m)) {
                      @if (m.ok) { <span class="opacity-60">{{ m.latencyMs }}ms</span> }
                      @else { <span>✗</span> }
                    }
                  </div>
                }
              </div>

              <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                <div class="flex-1 min-w-56">
                  <label class="block text-xs text-slate-500 mb-1">Add model</label>
                  <input type="text" [(ngModel)]="ps.newModelName"
                    placeholder="provider model name"
                    class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400" />
                </div>
                <button (click)="addModel(ps)" [disabled]="ps.addModelBusy || !ps.newModelName.trim()"
                  class="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                  {{ ps.addModelBusy ? 'Adding...' : 'Add' }}
                </button>
                <button (click)="testOneModel(ps)" [disabled]="ps.testBusy || !ps.newModelName.trim()"
                  class="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
                  Test model
                </button>
                @if (ps.addModelError) { <p class="w-full text-xs text-red-600">{{ ps.addModelError }}</p> }
              </div>

              <!-- Standard key edit form (non-Qwen providers) -->
              @if (!hasEndpointConfig(ps.catalog.providerName) && ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                  <div class="flex-1 min-w-64">
                    <label class="block text-xs text-slate-500 mb-1">
                      API Key <span class="text-slate-400 ml-1">— blank clears and falls back to env var</span>
                    </label>
                    <input type="password" [(ngModel)]="ps.editKeyValue"
                      [placeholder]="keyPlaceholder(ps.catalog.providerName)"
                      class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400" />
                  </div>
                  <button (click)="saveKey(ps)" [disabled]="ps.saveKeyBusy"
                    class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50">
                    {{ ps.saveKeyBusy ? 'Saving…' : 'Save' }}
                  </button>
                  @if (ps.catalog.hasApiKey) {
                    <button (click)="clearKey(ps)"
                      class="rounded-lg border border-red-300 px-4 py-2 text-sm text-red-600 hover:bg-red-50">
                      Clear key
                    </button>
                  }
                  <button (click)="ps.editingKey = false" class="text-xs text-slate-400 hover:underline">Cancel</button>
                  @if (ps.saveKeyError) { <p class="w-full text-xs text-red-600">{{ ps.saveKeyError }}</p> }
                </div>
              }

              <!-- Qwen unified config form -->
              @if (hasEndpointConfig(ps.catalog.providerName) && ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 space-y-3">
                  <p class="text-xs text-slate-500">
                    Qwen uses a workspace-specific endpoint. Find these values in Alibaba Cloud Model Studio → your workspace.
                  </p>
                  <div class="grid gap-3 sm:grid-cols-2">
                    <div>
                      <label class="block text-xs font-semibold text-slate-500 mb-1">
                        API Key <span class="font-normal text-slate-400">— sk-… from your workspace</span>
                      </label>
                      <input type="password" [(ngModel)]="ps.editKeyValue"
                        placeholder="sk-…"
                        class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400" />
                    </div>
                    <div>
                      <label class="block text-xs font-semibold text-slate-500 mb-1">
                        API Host <span class="font-normal text-slate-400">— workspace base URL</span>
                      </label>
                      <input type="text" [(ngModel)]="ps.editEndpointValue"
                        placeholder="https://ws-xxx.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1"
                        class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400" />
                      <p class="text-xs text-slate-400 mt-1">OpenAI-compatible endpoint from your workspace. Leave blank to use global DashScope.</p>
                    </div>
                  </div>
                  <div class="flex flex-wrap gap-3 items-center pt-1">
                    <button (click)="saveQwenConfig(ps)" [disabled]="ps.saveKeyBusy || ps.saveEndpointBusy"
                      class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50">
                      {{ (ps.saveKeyBusy || ps.saveEndpointBusy) ? 'Saving…' : 'Save' }}
                    </button>
                    <button (click)="ps.editingKey = false" class="text-xs text-slate-400 hover:underline">Cancel</button>
                    @if (ps.saveKeyError || ps.saveEndpointError) {
                      <p class="text-xs text-red-600">{{ ps.saveKeyError || ps.saveEndpointError }}</p>
                    }
                  </div>
                  <!-- Current values summary -->
                  @if (ps.catalog.hasApiKey || ps.catalog.apiEndpoint) {
                    <div class="rounded-lg bg-slate-50 border border-slate-200 p-3 space-y-1 text-xs text-slate-500">
                      @if (ps.catalog.hasApiKey) {
                        <div><span class="font-semibold">Key:</span> stored ✓</div>
                      }
                      @if (ps.catalog.apiEndpoint) {
                        <div class="flex gap-1"><span class="font-semibold shrink-0">Endpoint:</span> <span class="font-mono truncate">{{ ps.catalog.apiEndpoint }}</span></div>
                      }
                    </div>
                  }
                </div>
              }

            </div>
          }
        </div>
      </sp-admin-card>

    }
  `,
  styles: [`
    .sp-admin-section-wrap { display: block; margin-bottom: 24px; }
    .sp-ai-select{width:100%;border:1px solid #CBD5E1;border-radius:9px;padding:7px 9px;font-size:13px;background:#fff;color:#0F172A;}
    .sp-ai-select:disabled{background:#F8FAFC;color:#94A3B8;}
    .sp-ai-model-select{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;}
  `],
})
export class AdminAiConfigComponent implements OnInit {
  categories = signal<CategoryState[]>([]);
  providers = signal<ProviderState[]>([]);
  loading = signal(true);

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    forkJoin({ categories: this.adminApi.listAiCategories(), catalog: this.adminApi.listAiProviders() }).subscribe({
      next: ({ categories, catalog }) => {
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
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
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
    const provider = cs.editingProvider || null;
    const model = cs.editingModel || null;
    const voice = cs.editingVoice || null;
    this.adminApi.updateAiCategory(cs.item.categoryKey, {
      providerName: provider,
      modelName: model,
      voiceName: voice,
    }).subscribe({
      next: updated => {
        cs.item = updated;
        cs.editingProvider = updated.providerName;
        cs.editingModel = updated.modelName;
        cs.editingVoice = updated.voiceName;
        cs.saving = false; cs.saved = true;
        setTimeout(() => cs.saved = false, 2000);
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

  // ── Model chip helpers ─────────────────────────────────────────────────────

  hasBeenTested(m: ModelTestStatus): boolean {
    return m.testedAt !== '0001-01-01T00:00:00' && m.testedAt !== null;
  }

  modelChipClass(m: ModelTestStatus): string {
    const base = 'inline-flex items-center gap-1.5 text-xs rounded-lg border px-2 py-1';
    if (!this.hasBeenTested(m)) return `${base} border-slate-200 bg-slate-50 text-slate-500`;
    return m.ok
      ? `${base} border-emerald-200 bg-emerald-50 text-emerald-700`
      : `${base} border-red-200 bg-red-50 text-red-600`;
  }

  modelDotClass(m: ModelTestStatus): string {
    if (!this.hasBeenTested(m)) return 'w-1.5 h-1.5 rounded-full bg-slate-300';
    return m.ok ? 'w-1.5 h-1.5 rounded-full bg-emerald-500' : 'w-1.5 h-1.5 rounded-full bg-red-500';
  }

  modelChipTitle(m: ModelTestStatus): string {
    if (!this.hasBeenTested(m)) return 'Not tested yet';
    if (m.ok) return `OK — ${m.latencyMs}ms`;
    return m.error ?? 'Failed';
  }

  // ── Provider credentials ───────────────────────────────────────────────────

  keyPlaceholder(provider: string): string {
    return ({ openai: 'sk-…', gemini: 'AIza…', anthropic: 'sk-ant-…', qwen: 'sk-…' } as Record<string, string>)[provider] ?? '…';
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
    // Save key and endpoint in sequence; both fields optional (blank = keep existing / use env)
    ps.saveKeyBusy = true;
    ps.saveKeyError = '';
    ps.saveEndpointError = '';
    this.adminApi.setProviderApiKey(ps.catalog.providerName, ps.editKeyValue || null).subscribe({
      next: updated => {
        ps.catalog = updated;
        ps.saveKeyBusy = false;
        // Now save endpoint
        ps.saveEndpointBusy = true;
        this.adminApi.setProviderEndpoint(ps.catalog.providerName, ps.editEndpointValue || null).subscribe({
          next: updated2 => {
            ps.catalog = updated2;
            ps.editingKey = false;
            ps.saveEndpointBusy = false;
          },
          error: err => { ps.saveEndpointError = err.error?.error ?? 'Failed to save endpoint.'; ps.saveEndpointBusy = false; },
        });
      },
      error: err => { ps.saveKeyError = err.error?.error ?? 'Failed to save key.'; ps.saveKeyBusy = false; },
    });
  }

  toggleEndpointEdit(ps: ProviderState): void {
    ps.editingEndpoint = !ps.editingEndpoint;
    ps.editEndpointValue = ps.catalog.apiEndpoint ?? '';
    ps.saveEndpointError = '';
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
      next: updated => {
        ps.catalog = updated;
        ps.newModelName = '';
        ps.addModelBusy = false;
      },
      error: err => {
        ps.addModelError = err.error?.error ?? 'Failed to add model.';
        ps.addModelBusy = false;
      },
    });
  }

  testOneModel(ps: ProviderState): void {
    const modelName = ps.newModelName.trim();
    if (!modelName) return;
    ps.testBusy = true;
    ps.addModelError = '';
    this.adminApi.testProviderModel(ps.catalog.providerName, modelName).subscribe({
      next: updated => {
        ps.catalog = updated;
        ps.testBusy = false;
      },
      error: err => {
        ps.addModelError = err.error?.error ?? 'Model test failed.';
        ps.testBusy = false;
      },
    });
  }
}

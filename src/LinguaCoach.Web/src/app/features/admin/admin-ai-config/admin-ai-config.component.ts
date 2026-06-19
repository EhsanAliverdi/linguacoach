import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiConfigCategoryItem, AiProviderCatalogItem, ModelTestStatus } from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
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
  imports: [CommonModule, FormsModule, SpAdminAlertComponent, SpAdminBadgeComponent, SpAdminCardComponent, SpAdminCodePillComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminButtonComponent, SpAdminErrorStateComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminLoadingStateComponent],
  template: `
    <sp-admin-page-header title="AI Configuration" subtitle="Category-level AI provider config, TTS voices, and provider credentials" />

    <sp-admin-page-body>
    @if (loading()) {
      <sp-admin-loading-state message="Loading AI configuration" />
    } @else if (loadError()) {
      <sp-admin-error-state title="AI configuration unavailable" [message]="loadError()" />
    } @else {
      <!-- ── Section 1: LLM Categories ─────────────────────────────────── -->
      <sp-admin-card title="LLM Categories">
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
                <sp-admin-form-field label="Provider">
                  <select [(ngModel)]="cs.editingProvider" (ngModelChange)="onCategoryProviderChange(cs, $event)" class="sp-adm-native-select">
                    <option [ngValue]="null">— inherit —</option>
                    <option value="fake">fake (disable)</option>
                    @for (p of providers(); track p.catalog.providerName) {
                      <option [value]="p.catalog.providerName">{{ p.catalog.providerName }}</option>
                    }
                  </select>
                </sp-admin-form-field>

                <sp-admin-form-field label="Model">
                  <select [(ngModel)]="cs.editingModel" [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'" class="sp-adm-native-select sp-adm-mono">
                    <option [ngValue]="null">— inherit —</option>
                    @for (m of modelsFor(cs.editingProvider ?? ''); track m) {
                      <option [value]="m">{{ m }}</option>
                    }
                  </select>
                </sp-admin-form-field>
              </div>

              <div class="mt-3 flex items-center gap-3">
                <sp-admin-button size="sm" [loading]="cs.saving" [disabled]="cs.saving" (click)="saveCategory(cs)">Save</sp-admin-button>
                <sp-admin-button size="sm" variant="secondary" [loading]="cs.testBusy" [disabled]="cs.testBusy" (click)="testCategory(cs)">Test</sp-admin-button>
                @if (cs.saved) { <span class="text-xs text-emerald-600">Saved</span> }
                @if (cs.error) { <span class="text-xs text-red-500">{{ cs.error }}</span> }
                @if (cs.testResult) { <span class="text-xs text-slate-600">{{ cs.testResult }}</span> }
              </div>
            </div>
          }
        </div>
      </sp-admin-card>

      <!-- ── Section 2: TTS Categories ─────────────────────────────────── -->
      <sp-admin-card title="Text-to-Speech">
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
                <sp-admin-form-field label="Provider">
                  <select [(ngModel)]="cs.editingProvider" (ngModelChange)="onTtsProviderChange(cs, $event)" class="sp-adm-native-select">
                    <option [ngValue]="null">— disable —</option>
                    <option value="openai">openai</option>
                    <option value="gemini">gemini</option>
                    <option value="qwen">qwen</option>
                  </select>
                </sp-admin-form-field>

                <sp-admin-form-field label="Model">
                  <select [(ngModel)]="cs.editingModel" [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'" class="sp-adm-native-select sp-adm-mono">
                    <option [ngValue]="null">— default —</option>
                    @for (m of ttsModelsFor(cs.editingProvider ?? ''); track m) {
                      <option [value]="m">{{ m }}</option>
                    }
                  </select>
                </sp-admin-form-field>

                <sp-admin-form-field label="Voice">
                  <sp-admin-input
                    [(ngModel)]="cs.editingVoice"
                    [disabled]="!cs.editingProvider || cs.editingProvider === 'fake'"
                    placeholder="e.g. onyx"
                  />
                </sp-admin-form-field>
              </div>

              <div class="mt-3 flex items-center gap-3">
                <sp-admin-button size="sm" [loading]="cs.saving" [disabled]="cs.saving" (click)="saveCategory(cs)">Save</sp-admin-button>
                <sp-admin-button size="sm" variant="secondary" [loading]="cs.testBusy" [disabled]="cs.testBusy" (click)="testCategory(cs)">Test audio</sp-admin-button>
                @if (cs.saved) { <span class="text-xs text-emerald-600">Saved</span> }
                @if (cs.error) { <span class="text-xs text-red-500">{{ cs.error }}</span> }
                @if (cs.testResult) { <span class="text-xs text-slate-600">{{ cs.testResult }}</span> }
              </div>
            </div>
          }
        </div>
      </sp-admin-card>

      <!-- ── Section 3: Provider credentials ────────────────────────────── -->
      <sp-admin-card title="Provider credentials">
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
                  <sp-admin-code-pill [value]="ps.catalog.providerName" tone="neutral" />
                  @if (ps.catalog.hasApiKey) {
                    <sp-admin-badge tone="success" [dot]="true">Key stored</sp-admin-badge>
                  } @else {
                    <sp-admin-badge tone="neutral" [dot]="true">Env var</sp-admin-badge>
                  }
                  @if (hasEndpointConfig(ps.catalog.providerName) && ps.catalog.apiEndpoint) {
                    <sp-admin-badge tone="info">Endpoint set</sp-admin-badge>
                  }
                </div>
                <div class="flex items-center gap-2">
                  @if (hasEndpointConfig(ps.catalog.providerName)) {
                    <sp-admin-button variant="neutral" appearance="ghost" size="sm" (click)="toggleQwenConfig(ps)">Configure</sp-admin-button>
                  } @else {
                    <sp-admin-button variant="neutral" appearance="ghost" size="sm" (click)="toggleKeyEdit(ps)">{{ ps.catalog.hasApiKey ? 'Update key' : 'Set key' }}</sp-admin-button>
                  }
                  <sp-admin-button variant="neutral" appearance="outline" size="sm" [loading]="ps.testBusy" [disabled]="ps.testBusy" (click)="runTest(ps)">Test connection</sp-admin-button>
                </div>
              </div>

              <!-- Model test chips -->
              <div class="flex flex-wrap gap-2">
                @for (m of ps.catalog.modelTests; track m.modelName) {
                  <sp-admin-badge [tone]="modelChipTone(m)" [dot]="true" [title]="modelChipTitle(m)">
                    <span class="font-mono">{{ m.modelName }}</span>
                    @if (hasBeenTested(m) && m.ok) { <span class="opacity-60 ml-1">{{ m.latencyMs }}ms</span> }
                    @if (hasBeenTested(m) && !m.ok) { <span class="ml-1">✗</span> }
                  </sp-admin-badge>
                }
              </div>

              <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                <div class="flex-1 min-w-56">
                  <sp-admin-form-field label="Add model">
                    <sp-admin-input
                      [(ngModel)]="ps.newModelName"
                      placeholder="provider model name"
                    />
                  </sp-admin-form-field>
                </div>
                <sp-admin-button variant="secondary" size="sm" [disabled]="ps.addModelBusy || !ps.newModelName.trim()" [loading]="ps.addModelBusy" (click)="addModel(ps)">Add</sp-admin-button>
                <sp-admin-button variant="secondary" size="sm" [disabled]="ps.testBusy || !ps.newModelName.trim()" (click)="testOneModel(ps)">Test model</sp-admin-button>
                @if (ps.addModelError) { <p class="w-full text-xs text-red-600">{{ ps.addModelError }}</p> }
              </div>

              <!-- Standard key edit form (non-Qwen providers) -->
              @if (!hasEndpointConfig(ps.catalog.providerName) && ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                  <div class="flex-1 min-w-64">
                    <sp-admin-form-field label="API Key" hint="blank clears and falls back to env var">
                      <sp-admin-input
                        type="password"
                        [(ngModel)]="ps.editKeyValue"
                        [placeholder]="keyPlaceholder(ps.catalog.providerName)"
                      />
                    </sp-admin-form-field>
                  </div>
                  <sp-admin-button [loading]="ps.saveKeyBusy" [disabled]="ps.saveKeyBusy" (click)="saveKey(ps)">Save</sp-admin-button>
                  @if (ps.catalog.hasApiKey) {
                    <sp-admin-button variant="danger" (click)="clearKey(ps)">Clear key</sp-admin-button>
                  }
                  <sp-admin-button variant="neutral" appearance="ghost" size="sm" (click)="ps.editingKey = false">Cancel</sp-admin-button>
                  @if (ps.saveKeyError) { <sp-admin-alert variant="error" class="w-full">{{ ps.saveKeyError }}</sp-admin-alert> }
                </div>
              }

              <!-- Qwen unified config form -->
              @if (hasEndpointConfig(ps.catalog.providerName) && ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 space-y-3">
                  <p class="text-xs text-slate-500">
                    Qwen uses a workspace-specific endpoint. Find these values in Alibaba Cloud Model Studio → your workspace.
                  </p>
                  <div class="grid gap-3 sm:grid-cols-2">
                    <sp-admin-form-field label="API Key" hint="sk-… from your workspace">
                      <sp-admin-input
                        type="password"
                        [(ngModel)]="ps.editKeyValue"
                        placeholder="sk-…"
                      />
                    </sp-admin-form-field>
                    <sp-admin-form-field label="API Host" hint="OpenAI-compatible endpoint. Leave blank to use global DashScope.">
                      <sp-admin-input
                        [(ngModel)]="ps.editEndpointValue"
                        placeholder="https://ws-xxx.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1"
                      />
                    </sp-admin-form-field>
                  </div>
                  <div class="flex flex-wrap gap-3 items-center pt-1">
                    <sp-admin-button [loading]="ps.saveKeyBusy || ps.saveEndpointBusy" [disabled]="ps.saveKeyBusy || ps.saveEndpointBusy" (click)="saveQwenConfig(ps)">Save</sp-admin-button>
                    <sp-admin-button variant="neutral" appearance="ghost" size="sm" (click)="ps.editingKey = false">Cancel</sp-admin-button>
                    @if (ps.saveKeyError || ps.saveEndpointError) {
                      <sp-admin-alert variant="error" class="w-full">{{ ps.saveKeyError || ps.saveEndpointError }}</sp-admin-alert>
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
    </sp-admin-page-body>
  `,
  styles: [`
    .sp-adm-native-select{width:100%;height:44px;border:1px solid #E5E7EB;border-radius:8px;padding:0 16px;font-size:13px;background:#fff;color:#1A2130;box-sizing:border-box;}
    .sp-adm-native-select:disabled{opacity:0.55;cursor:not-allowed;background:#F9FAFB;color:#9CA3AF;}
    .sp-adm-native-select:focus{outline:none;border-color:#93C5FD;box-shadow:0 0 0 2px rgba(59,130,246,.1);}
    .sp-adm-mono{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;}
  `],
})
export class AdminAiConfigComponent implements OnInit {
  categories = signal<CategoryState[]>([]);
  providers = signal<ProviderState[]>([]);
  loading = signal(true);
  loadError = signal('');

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
      error: err => {
        this.loadError.set(err.error?.error ?? 'Could not load AI configuration.');
        this.loading.set(false);
      },
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

  modelChipTone(m: ModelTestStatus): 'neutral' | 'success' | 'danger' {
    if (!this.hasBeenTested(m)) return 'neutral';
    return m.ok ? 'success' : 'danger';
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

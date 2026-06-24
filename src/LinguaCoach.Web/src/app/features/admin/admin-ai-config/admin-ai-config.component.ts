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
  SpAdminKpiCardComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
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
  imports: [CommonModule, FormsModule, SpAdminAlertComponent, SpAdminBadgeComponent, SpAdminCardComponent, SpAdminCodePillComponent, SpAdminKpiCardComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminButtonComponent, SpAdminEmptyStateComponent, SpAdminErrorStateComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminLoadingStateComponent],
  template: `
    <sp-admin-page-header title="AI Configuration" subtitle="Category-level AI provider config, TTS voices, and provider credentials." />

    <!-- ── AI Config KPI strip ── -->
    @if (!loading() && !loadError()) {
      <div class="sp-aic-kpi-strip" aria-label="AI configuration summary">
        <sp-admin-kpi-card label="LLM configured" [variant]="configSummary().llmConfigured === configSummary().llmTotal ? 'green' : 'amber'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/></svg>
          {{ configSummary().llmConfigured }}/{{ configSummary().llmTotal }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="TTS configured" [variant]="configSummary().ttsConfigured > 0 ? 'teal' : 'slate'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 18v-6a9 9 0 0 1 18 0v6"/><path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z"/></svg>
          {{ configSummary().ttsConfigured }}/{{ configSummary().ttsTotal }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Providers with key" [variant]="configSummary().providersWithKey > 0 ? 'indigo' : 'slate'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/></svg>
          {{ configSummary().providersWithKey }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Pricing models" [variant]="configSummary().pricingModels > 0 ? 'violet' : 'slate'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
          {{ configSummary().pricingModels }}
        </sp-admin-kpi-card>
      </div>
    }

    <sp-admin-page-body>
    @if (loading()) {
      <sp-admin-loading-state message="Loading AI configuration" />
    } @else if (loadError()) {
      <sp-admin-error-state title="AI configuration unavailable" [message]="loadError()" />
    } @else {
      <!-- ── Tab bar ───────────────────────────────────────────────────── -->
      <div class="sp-aic-tabs" role="tablist" aria-label="AI configuration sections">
        @for (t of tabs; track t.key) {
          <button
            type="button"
            role="tab"
            class="sp-aic-tab"
            [class.sp-aic-tab--active]="activeTab() === t.key"
            [attr.aria-selected]="activeTab() === t.key"
            (click)="activeTab.set(t.key)">{{ t.label }}</button>
        }
      </div>

      <!-- ── Section 1: LLM Categories ─────────────────────────────────── -->
      @if (activeTab() === 'llm') {
      <sp-admin-card title="LLM Categories">
        <div slot="actions">
          <sp-admin-badge [tone]="configSummary().llmConfigured === configSummary().llmTotal ? 'success' : 'warning'">
            {{ configSummary().llmConfigured }}/{{ configSummary().llmTotal }} configured
          </sp-admin-badge>
        </div>
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

      }

      <!-- ── Section 2: TTS Categories ─────────────────────────────────── -->
      @if (activeTab() === 'tts') {
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

      }

      <!-- ── Section 3: Provider credentials ────────────────────────────── -->
      @if (activeTab() === 'credentials') {
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

      <!-- ── Section 4: Model Pricing ─────────────────────────────────────── -->
      @if (activeTab() === 'pricing') {
      <sp-admin-card title="Model Pricing">
        <!-- Config pricing (read-only) -->
        <p class="text-sm font-semibold text-slate-700 mb-2">Config pricing</p>
        <p class="text-xs text-slate-500 mb-4">Read from appsettings.json. Add DB overrides below to change runtime pricing without redeploying.</p>

        @if (pricing().length === 0) {
          <sp-admin-empty-state title="No pricing configured" message="Add pricing entries to appsettings.json under OpenAI:Pricing, Gemini:Pricing, or Anthropic:Pricing." />
        } @else {
          @for (group of pricingByProvider(); track group.provider) {
            <div class="mb-6">
              <div class="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">{{ group.provider }}</div>
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-200 text-left text-xs text-slate-500">
                    <th class="pb-2 pr-4 font-medium">Model</th>
                    <th class="pb-2 pr-4 font-medium text-right">Input / 1K</th>
                    <th class="pb-2 pr-4 font-medium text-right">Output / 1K</th>
                    <th class="pb-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of group.rows; track row.modelName) {
                    <tr class="border-b border-slate-100 last:border-0">
                      <td class="py-2 pr-4 font-mono text-xs text-slate-800">{{ row.modelName }}</td>
                      <td class="py-2 pr-4 text-right text-slate-700">\${{ row.inputPer1KTokens.toFixed(5) }}</td>
                      <td class="py-2 pr-4 text-right text-slate-700">\${{ row.outputPer1KTokens.toFixed(5) }}</td>
                      <td class="py-2">
                        @if (row.isConfigured) {
                          <sp-admin-badge tone="success">Configured</sp-admin-badge>
                        } @else {
                          <sp-admin-badge tone="warning">Missing</sp-admin-badge>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }

        <!-- DB overrides -->
        <div class="mt-6 pt-6 border-t border-slate-200">
          <div class="flex items-center justify-between mb-4">
            <div>
              <p class="text-sm font-semibold text-slate-700">Pricing overrides</p>
              <p class="text-xs text-slate-500">DB overrides take precedence over config at runtime. Active overrides with a valid effective range are applied.</p>
            </div>
            @if (!overrideForm()) {
              <sp-admin-button size="sm" (click)="openCreateOverride()">Add override</sp-admin-button>
            }
          </div>

          <!-- Create / edit form -->
          @if (overrideForm(); as f) {
            <div class="rounded-xl border border-blue-200 bg-blue-50 p-5 mb-4">
              <p class="text-sm font-semibold text-slate-800 mb-4">{{ f.mode === 'create' ? 'New override' : 'Edit override' }}</p>
              <div class="grid gap-3 sm:grid-cols-2 mb-3">
                <sp-admin-form-field label="Provider">
                  <sp-admin-input [(ngModel)]="f.providerName" placeholder="openai / gemini / anthropic" [disabled]="f.mode === 'edit'" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Model">
                  <sp-admin-input [(ngModel)]="f.modelName" placeholder="gpt-4o" [disabled]="f.mode === 'edit'" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Input price / 1K tokens">
                  <sp-admin-input type="number" [(ngModel)]="f.inputPricePer1KTokens" placeholder="0.002" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Output price / 1K tokens">
                  <sp-admin-input type="number" [(ngModel)]="f.outputPricePer1KTokens" placeholder="0.008" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Currency">
                  <sp-admin-input [(ngModel)]="f.currency" placeholder="USD" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Effective from (UTC)">
                  <input type="datetime-local" [(ngModel)]="f.effectiveFromUtc" class="sp-adm-native-select" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Effective to (UTC, optional)">
                  <input type="datetime-local" [(ngModel)]="f.effectiveToUtc" class="sp-adm-native-select" />
                </sp-admin-form-field>
                <sp-admin-form-field label="Notes (optional)">
                  <sp-admin-input [(ngModel)]="f.notes" placeholder="e.g. Q3 rate change" />
                </sp-admin-form-field>
              </div>
              <div class="flex items-center gap-3">
                <sp-admin-button [loading]="f.busy" [disabled]="f.busy" (click)="saveOverride()">{{ f.mode === 'create' ? 'Create' : 'Save' }}</sp-admin-button>
                <sp-admin-button variant="neutral" appearance="ghost" size="sm" [disabled]="f.busy" (click)="cancelOverrideForm()">Cancel</sp-admin-button>
                @if (f.error) { <span class="text-xs text-red-600">{{ f.error }}</span> }
              </div>
            </div>
          }

          <!-- Overrides table -->
          @if (overrides().length === 0) {
            <sp-admin-empty-state title="No overrides" message="No DB pricing overrides exist. Config pricing is used." />
          } @else {
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-200 text-left text-xs text-slate-500">
                  <th class="pb-2 pr-3 font-medium">Provider</th>
                  <th class="pb-2 pr-3 font-medium">Model</th>
                  <th class="pb-2 pr-3 font-medium text-right">Input / 1K</th>
                  <th class="pb-2 pr-3 font-medium text-right">Output / 1K</th>
                  <th class="pb-2 pr-3 font-medium">From</th>
                  <th class="pb-2 pr-3 font-medium">Status</th>
                  <th class="pb-2 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                @for (o of overrides(); track o.id) {
                  <tr class="border-b border-slate-100 last:border-0" [class.opacity-40]="!o.isActive">
                    <td class="py-2 pr-3 font-mono text-xs">{{ o.providerName }}</td>
                    <td class="py-2 pr-3 font-mono text-xs">{{ o.modelName }}</td>
                    <td class="py-2 pr-3 text-right">\${{ o.inputPricePer1KTokens.toFixed(5) }}</td>
                    <td class="py-2 pr-3 text-right">\${{ o.outputPricePer1KTokens.toFixed(5) }}</td>
                    <td class="py-2 pr-3 text-xs text-slate-500">{{ o.effectiveFromUtc | date:'yyyy-MM-dd HH:mm' : 'UTC' }}</td>
                    <td class="py-2 pr-3">
                      @if (o.isActive) {
                        <sp-admin-badge tone="success">Active</sp-admin-badge>
                      } @else {
                        <sp-admin-badge tone="neutral">Inactive</sp-admin-badge>
                      }
                    </td>
                    <td class="py-2">
                      <div class="flex items-center gap-2">
                        @if (o.isActive) {
                          <sp-admin-button variant="neutral" appearance="ghost" size="sm" (click)="openEditOverride(o)">Edit</sp-admin-button>
                          <sp-admin-button variant="danger" appearance="ghost" size="sm"
                            [loading]="deactivateBusy() === o.id"
                            [disabled]="deactivateBusy() === o.id"
                            (click)="deactivateOverride(o.id)">Deactivate</sp-admin-button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </sp-admin-card>

      }

      <!-- ── Section 5: Rate Limits — Not implemented ─────────────────────── -->
      @if (activeTab() === 'rate-limits') {
      <sp-admin-card title="Rate limits and quotas">
        <div class="sp-aic-not-impl" aria-label="Rate limits not implemented">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="color:#8B85A0;flex-shrink:0"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
          <div>
            <div class="sp-aic-not-impl-title">Backend not available yet</div>
            <div class="sp-aic-not-impl-body">
              Real-time rate limit usage (requests per minute, tokens per day, daily cost cap) requires a backend endpoint that is not yet implemented.
              Cost and token usage totals are visible on the <a href="/admin/usage" style="color:var(--sp-admin-primary,#5B4BE8);text-decoration:underline">AI Usage</a> page.
            </div>
          </div>
        </div>
      </sp-admin-card>
      }

    }
    </sp-admin-page-body>
  `,
  styles: [`
    .sp-aic-kpi-strip {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      padding: 16px 24px 0;
    }
    @media (max-width: 800px) { .sp-aic-kpi-strip { grid-template-columns: repeat(2, 1fr); } }
    .sp-aic-tabs {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
      border-bottom: 1.5px solid var(--sp-admin-border, #ECE9F5);
      margin-bottom: 20px;
    }
    .sp-aic-tab {
      appearance: none;
      background: transparent;
      border: none;
      border-bottom: 2px solid transparent;
      margin-bottom: -1.5px;
      padding: 10px 14px;
      font-size: 13.5px;
      font-weight: 600;
      font-family: inherit;
      color: var(--sp-admin-muted, #8B85A0);
      cursor: pointer;
      transition: color .12s ease, border-color .12s ease;
    }
    .sp-aic-tab:hover { color: var(--sp-admin-text, #211B36); }
    .sp-aic-tab--active {
      color: var(--sp-admin-primary, #5B4BE8);
      border-bottom-color: var(--sp-admin-primary, #5B4BE8);
    }
    .sp-aic-not-impl {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 4px 0;
    }
    .sp-aic-not-impl-title { font-size: 13px; font-weight: 600; color: #8B85A0; margin-bottom: 4px; }
    .sp-aic-not-impl-body { font-size: 13px; color: #8B85A0; line-height: 1.5; }
    .sp-adm-native-select{width:100%;height:36px;border:1.5px solid #E2DEF0;border-radius:8px;padding:0 12px;font-size:13.5px;background:#fff;color:#211B36;box-sizing:border-box;font-family:inherit;}
    .sp-adm-native-select:disabled{opacity:0.55;cursor:not-allowed;background:#FBFAFE;}
    .sp-adm-native-select:focus{outline:none;border-color:#5B4BE8;}
    .sp-adm-mono{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;}
  `],
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

  readonly tabs: { key: AiConfigTab; label: string }[] = [
    { key: 'llm', label: 'LLM Categories' },
    { key: 'tts', label: 'Text-to-Speech' },
    { key: 'credentials', label: 'Provider Credentials' },
    { key: 'pricing', label: 'Model Pricing' },
    { key: 'rate-limits', label: 'Rate Limits' },
  ];
  activeTab = signal<AiConfigTab>('llm');

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

  // ── Override management ────────────────────────────────────────────────────

  private toDatetimeLocal(utcStr: string | null): string {
    if (!utcStr) return '';
    const d = new Date(utcStr);
    return d.toISOString().slice(0, 16);
  }

  private fromDatetimeLocal(local: string): string {
    if (!local) return '';
    return new Date(local).toISOString();
  }

  openCreateOverride(): void {
    const now = new Date();
    now.setSeconds(0, 0);
    this.overrideForm.set({
      mode: 'create', id: null,
      providerName: '', modelName: '',
      inputPricePer1KTokens: null, outputPricePer1KTokens: null,
      currency: 'USD',
      effectiveFromUtc: now.toISOString().slice(0, 16),
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
      effectiveFromUtc: this.toDatetimeLocal(o.effectiveFromUtc),
      effectiveToUtc: this.toDatetimeLocal(o.effectiveToUtc),
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
    if (!f.providerName.trim() || !f.modelName.trim()) {
      this.overrideForm.set({ ...f, error: 'Provider and model are required.' });
      return;
    }
    if (f.inputPricePer1KTokens === null || f.outputPricePer1KTokens === null ||
        f.inputPricePer1KTokens < 0 || f.outputPricePer1KTokens < 0) {
      this.overrideForm.set({ ...f, error: 'Prices must be >= 0.' });
      return;
    }
    this.overrideForm.set({ ...f, busy: true, error: '' });

    if (f.mode === 'create') {
      const cmd: CreatePricingOverrideRequest = {
        providerName: f.providerName.trim(),
        modelName: f.modelName.trim(),
        inputPricePer1KTokens: f.inputPricePer1KTokens,
        outputPricePer1KTokens: f.outputPricePer1KTokens,
        currency: f.currency || 'USD',
        effectiveFromUtc: this.fromDatetimeLocal(f.effectiveFromUtc),
        effectiveToUtc: f.effectiveToUtc ? this.fromDatetimeLocal(f.effectiveToUtc) : null,
        notes: f.notes || null,
      };
      this.adminApi.createAiPricingOverride(cmd).subscribe({
        next: created => {
          this.overrides.update(list => [created, ...list]);
          this.overrideForm.set(null);
        },
        error: err => this.overrideForm.set({ ...f, busy: false, error: err.error?.error ?? err.error?.title ?? 'Failed to create override.' }),
      });
    } else {
      const cmd: UpdatePricingOverrideRequest = {
        inputPricePer1KTokens: f.inputPricePer1KTokens,
        outputPricePer1KTokens: f.outputPricePer1KTokens,
        currency: f.currency || 'USD',
        effectiveFromUtc: this.fromDatetimeLocal(f.effectiveFromUtc),
        effectiveToUtc: f.effectiveToUtc ? this.fromDatetimeLocal(f.effectiveToUtc) : null,
        notes: f.notes || null,
      };
      this.adminApi.updateAiPricingOverride(f.id!, cmd).subscribe({
        next: updated => {
          this.overrides.update(list => list.map(o => o.id === updated.id ? updated : o));
          this.overrideForm.set(null);
        },
        error: err => this.overrideForm.set({ ...f, busy: false, error: err.error?.error ?? err.error?.title ?? 'Failed to update override.' }),
      });
    }
  }

  deactivateOverride(id: string): void {
    if (!confirm('Deactivate this pricing override?')) return;
    this.deactivateBusy.set(id);
    this.adminApi.deactivateAiPricingOverride(id).subscribe({
      next: () => {
        this.overrides.update(list => list.map(o => o.id === id ? { ...o, isActive: false } : o));
        this.deactivateBusy.set(null);
      },
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

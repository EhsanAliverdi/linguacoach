import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiProviderConfigItem, AiProviderCatalogItem, ProviderTestResult } from '../../../core/models/admin.models';

interface ProviderState {
  catalog: AiProviderCatalogItem;
  editingKey: boolean;
  editKeyValue: string;
  saveKeyBusy: boolean;
  saveKeyError: string;
  testBusy: boolean;
  testResult: ProviderTestResult | null;
}

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (loading()) {
      <p class="text-sm text-slate-400 py-6">Loading…</p>
    } @else {

      <!-- ── Section 1: Feature routing ──────────────────────────────── -->
      <div class="mb-8">
        <h2 class="text-base font-semibold text-slate-900 mb-1">Feature routing</h2>
        <p class="text-sm text-slate-500 mb-4">
          Choose which provider and model handles each feature. Changes take effect on the next call.
        </p>

        <div class="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          @for (c of configs(); track c.id) {
            <div class="flex items-center gap-4 px-5 py-4">
              <!-- feature label -->
              <div class="w-44 shrink-0">
                <div class="text-xs font-semibold text-indigo-600 uppercase tracking-wide">{{ c.featureKey }}</div>
              </div>

              <!-- provider dropdown -->
              <select
                [value]="c.providerName"
                (change)="onFeatureProviderChange(c, $any($event.target).value)"
                class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm w-32 focus:outline-none focus:ring-2 focus:ring-indigo-400">
                @for (p of providers(); track p.catalog.providerName) {
                  <option [value]="p.catalog.providerName">{{ p.catalog.providerName }}</option>
                }
              </select>

              <!-- model dropdown -->
              <select
                [value]="c.modelName"
                (change)="onFeatureModelChange(c, $any($event.target).value)"
                class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-mono w-56 focus:outline-none focus:ring-2 focus:ring-indigo-400">
                @for (m of modelsFor(c.providerName); track m) {
                  <option [value]="m">{{ m }}</option>
                }
              </select>

              <!-- save indicator -->
              @if (savedFeatureId() === c.id) {
                <span class="text-xs text-green-600">Saved</span>
              }
              @if (featureError()[c.id]) {
                <span class="text-xs text-red-500">{{ featureError()[c.id] }}</span>
              }
            </div>
          }
        </div>
      </div>

      <!-- ── Section 2: Provider credentials ────────────────────────── -->
      <div>
        <h2 class="text-base font-semibold text-slate-900 mb-1">Provider credentials</h2>
        <p class="text-sm text-slate-500 mb-4">
          One API key per provider — applies to all features using that provider.
          Keys stored here override environment variables.
        </p>

        <div class="space-y-3">
          @for (ps of providers(); track ps.catalog.providerName) {
            <div class="rounded-xl border border-slate-200 bg-white shadow-sm p-5">

              <!-- provider header -->
              <div class="flex items-start justify-between gap-4">
                <div class="flex items-center gap-3">
                  <span class="text-sm font-semibold text-slate-800 capitalize w-20">{{ ps.catalog.providerName }}</span>

                  <!-- key status badge -->
                  @if (ps.catalog.hasApiKey) {
                    <span class="inline-flex items-center gap-1 text-xs font-medium text-green-700 bg-green-50 border border-green-200 rounded-full px-2 py-0.5">
                      <span class="w-1.5 h-1.5 rounded-full bg-green-500"></span> Key stored
                    </span>
                  } @else {
                    <span class="inline-flex items-center gap-1 text-xs font-medium text-slate-500 bg-slate-100 border border-slate-200 rounded-full px-2 py-0.5">
                      <span class="w-1.5 h-1.5 rounded-full bg-slate-400"></span> Using env var
                    </span>
                  }

                  <!-- last test badge -->
                  @if (ps.catalog.lastTestedAt) {
                    @if (ps.catalog.lastTestOk) {
                      <span class="inline-flex items-center gap-1 text-xs font-medium text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-full px-2 py-0.5">
                        ✓ Connected
                      </span>
                    } @else {
                      <span class="inline-flex items-center gap-1 text-xs font-medium text-red-600 bg-red-50 border border-red-200 rounded-full px-2 py-0.5"
                            [title]="ps.catalog.lastTestError ?? ''">
                        ✗ Failed
                      </span>
                    }
                  }
                </div>

                <!-- action buttons -->
                <div class="flex items-center gap-2 shrink-0">
                  <button (click)="toggleKeyEdit(ps)"
                    class="text-xs text-indigo-600 hover:underline">
                    {{ ps.catalog.hasApiKey ? 'Update key' : 'Set key' }}
                  </button>
                  <button (click)="runTest(ps)"
                    [disabled]="ps.testBusy"
                    class="inline-flex items-center gap-1.5 text-xs font-medium rounded-lg border border-slate-300 px-3 py-1.5 hover:bg-slate-50 disabled:opacity-50 transition-colors">
                    @if (ps.testBusy) {
                      <span class="animate-spin h-3 w-3 border-2 border-slate-400 border-t-transparent rounded-full"></span>
                      Testing…
                    } @else {
                      Test connection
                    }
                  </button>
                </div>
              </div>

              <!-- test result inline -->
              @if (ps.testResult) {
                <div class="mt-3 rounded-lg px-3 py-2 text-xs"
                     [class]="ps.testResult.ok
                       ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                       : 'bg-red-50 text-red-600 border border-red-200'">
                  @if (ps.testResult.ok) {
                    ✓ Connected successfully in {{ ps.testResult.latencyMs }}ms
                  } @else {
                    ✗ {{ ps.testResult.error }}
                  }
                </div>
              }

              <!-- key edit panel -->
              @if (ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                  <div class="flex-1 min-w-64">
                    <label class="block text-xs text-slate-500 mb-1">
                      API Key
                      <span class="text-slate-400 ml-1">— leave blank to clear and fall back to env var</span>
                    </label>
                    <input type="password" [(ngModel)]="ps.editKeyValue"
                      [placeholder]="keyPlaceholder(ps.catalog.providerName)"
                      class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-400" />
                  </div>
                  <button (click)="saveKey(ps)"
                    [disabled]="ps.saveKeyBusy"
                    class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50 transition-colors">
                    {{ ps.saveKeyBusy ? 'Saving…' : 'Save' }}
                  </button>
                  @if (ps.catalog.hasApiKey) {
                    <button (click)="clearKey(ps)"
                      class="rounded-lg border border-red-300 px-4 py-2 text-sm text-red-600 hover:bg-red-50 transition-colors">
                      Clear key
                    </button>
                  }
                  <button (click)="ps.editingKey = false" class="text-xs text-slate-400 hover:underline">Cancel</button>
                  @if (ps.saveKeyError) {
                    <p class="w-full text-xs text-red-600">{{ ps.saveKeyError }}</p>
                  }
                </div>
              }

              <!-- available models list -->
              <div class="mt-3 flex flex-wrap gap-1.5">
                @for (m of ps.catalog.models; track m) {
                  <span class="text-xs font-mono text-slate-400 bg-slate-50 border border-slate-200 rounded px-1.5 py-0.5">{{ m }}</span>
                }
              </div>

            </div>
          }
        </div>
      </div>

    }
  `,
})
export class AdminAiConfigComponent implements OnInit {
  configs = signal<AiProviderConfigItem[]>([]);
  providers = signal<ProviderState[]>([]);
  loading = signal(true);
  savedFeatureId = signal<string | null>(null);
  featureError = signal<Record<string, string>>({});

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    forkJoin({ configs: this.adminApi.listAiConfigs(), catalog: this.adminApi.listAiProviders() }).subscribe({
      next: ({ configs, catalog }) => {
        this.configs.set(configs);
        this.providers.set(catalog.map(c => ({
          catalog: c,
          editingKey: false,
          editKeyValue: '',
          saveKeyBusy: false,
          saveKeyError: '',
          testBusy: false,
          testResult: null,
        })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  modelsFor(providerName: string): string[] {
    return this.providers().find(p => p.catalog.providerName === providerName)?.catalog.models ?? [];
  }

  // ── Feature routing ────────────────────────────────────────────────────────

  onFeatureProviderChange(c: AiProviderConfigItem, newProvider: string): void {
    const models = this.modelsFor(newProvider);
    const newModel = models[0] ?? c.modelName;
    this.saveFeature(c, newProvider, newModel);
  }

  onFeatureModelChange(c: AiProviderConfigItem, newModel: string): void {
    this.saveFeature(c, c.providerName, newModel);
  }

  private saveFeature(c: AiProviderConfigItem, providerName: string, modelName: string): void {
    this.adminApi.updateAiConfig(c.id, providerName, modelName).subscribe({
      next: updated => {
        this.configs.update(cs => cs.map(x => x.id === c.id ? updated : x));
        this.savedFeatureId.set(c.id);
        this.featureError.update(e => ({ ...e, [c.id]: '' }));
        setTimeout(() => this.savedFeatureId.set(null), 2000);
      },
      error: err => {
        this.featureError.update(e => ({ ...e, [c.id]: err.error?.error ?? 'Failed to save.' }));
      },
    });
  }

  // ── Provider credentials ───────────────────────────────────────────────────

  keyPlaceholder(provider: string): string {
    return { openai: 'sk-…', gemini: 'AIza…', anthropic: 'sk-ant-…' }[provider] ?? '…';
  }

  toggleKeyEdit(ps: ProviderState): void {
    ps.editingKey = !ps.editingKey;
    ps.editKeyValue = '';
    ps.saveKeyError = '';
    ps.testResult = null;
  }

  saveKey(ps: ProviderState): void {
    ps.saveKeyBusy = true;
    ps.saveKeyError = '';
    this.adminApi.setProviderApiKey(ps.catalog.providerName, ps.editKeyValue || null).subscribe({
      next: updated => {
        ps.catalog = updated;
        ps.editingKey = false;
        ps.saveKeyBusy = false;
      },
      error: err => {
        ps.saveKeyError = err.error?.error ?? 'Failed to save key.';
        ps.saveKeyBusy = false;
      },
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
    ps.testResult = null;
    this.adminApi.testProvider(ps.catalog.providerName).subscribe({
      next: result => {
        ps.testResult = result;
        ps.testBusy = false;
        // Refresh catalog entry so last-test badges update
        this.adminApi.listAiProviders().subscribe(catalog => {
          const updated = catalog.find(c => c.providerName === ps.catalog.providerName);
          if (updated) ps.catalog = updated;
        });
      },
      error: err => {
        ps.testResult = { ok: false, latencyMs: 0, error: err.error?.error ?? 'Request failed.' };
        ps.testBusy = false;
      },
    });
  }
}

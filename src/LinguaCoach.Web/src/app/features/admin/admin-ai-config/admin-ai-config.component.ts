import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiProviderConfigItem, AiProviderCatalogItem, ModelTestStatus } from '../../../core/models/admin.models';

interface ProviderState {
  catalog: AiProviderCatalogItem;
  editingKey: boolean;
  editKeyValue: string;
  saveKeyBusy: boolean;
  saveKeyError: string;
  testBusy: boolean;
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
          Which provider and model handles each feature. Auto-saves on change.
        </p>

        <div class="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          @for (c of configs(); track c.id) {
            <div class="flex items-center gap-3 px-5 py-3.5">
              <div class="w-44 shrink-0 text-xs font-semibold text-indigo-600 uppercase tracking-wide">
                {{ c.featureKey }}
              </div>

              <select [value]="c.providerName"
                (change)="onFeatureProviderChange(c, $any($event.target).value)"
                class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm w-32 focus:outline-none focus:ring-2 focus:ring-indigo-400">
                @for (p of providers(); track p.catalog.providerName) {
                  <option [value]="p.catalog.providerName">{{ p.catalog.providerName }}</option>
                }
              </select>

              <select [value]="c.modelName"
                (change)="onFeatureModelChange(c, $any($event.target).value)"
                class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-mono w-60 focus:outline-none focus:ring-2 focus:ring-indigo-400">
                @for (m of modelsFor(c.providerName); track m) {
                  <option [value]="m">{{ m }}</option>
                }
              </select>

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
          One API key per provider applies to all features using it.
          "Test connection" checks every model with the stored key.
        </p>

        <div class="space-y-4">
          @for (ps of providers(); track ps.catalog.providerName) {
            <div class="rounded-xl border border-slate-200 bg-white shadow-sm p-5">

              <!-- Header -->
              <div class="flex items-center justify-between gap-4 mb-4">
                <div class="flex items-center gap-3">
                  <span class="text-sm font-bold text-slate-800 capitalize w-24">
                    {{ ps.catalog.providerName }}
                  </span>
                  @if (ps.catalog.hasApiKey) {
                    <span class="inline-flex items-center gap-1 text-xs font-medium text-green-700 bg-green-50 border border-green-200 rounded-full px-2 py-0.5">Key stored</span>
                  } @else {
                    <span class="inline-flex items-center gap-1 text-xs font-medium text-slate-500 bg-slate-100 border border-slate-200 rounded-full px-2 py-0.5">Using env var</span>
                  }
                </div>

                <div class="flex items-center gap-2">
                  <button (click)="toggleKeyEdit(ps)"
                    class="text-xs font-medium text-indigo-600 hover:underline">
                    {{ ps.catalog.hasApiKey ? 'Update key' : 'Set key' }}
                  </button>
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

              <!-- Per-model status chips -->
              <div class="flex flex-wrap gap-2">
                @for (m of ps.catalog.modelTests; track m.modelName) {
                  <div [class]="modelChipClass(m)"
                       [title]="modelChipTitle(m)">
                    <span [class]="modelDotClass(m)"></span>
                    <span class="font-mono">{{ m.modelName }}</span>
                    @if (hasBeenTested(m)) {
                      @if (m.ok) {
                        <span class="opacity-60">{{ m.latencyMs }}ms</span>
                      } @else {
                        <span>✗</span>
                      }
                    }
                  </div>
                }
              </div>

              <!-- API key edit panel -->
              @if (ps.editingKey) {
                <div class="mt-4 pt-4 border-t border-slate-100 flex flex-wrap gap-3 items-end">
                  <div class="flex-1 min-w-64">
                    <label class="block text-xs text-slate-500 mb-1">
                      API Key
                      <span class="text-slate-400 ml-1">— blank clears and falls back to env var; clears test results</span>
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
                  @if (ps.saveKeyError) {
                    <p class="w-full text-xs text-red-600">{{ ps.saveKeyError }}</p>
                  }
                </div>
              }

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
        this.providers.set(this.toStates(catalog));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private toStates(catalog: AiProviderCatalogItem[]): ProviderState[] {
    return catalog.map(c => ({
      catalog: c,
      editingKey: false, editKeyValue: '',
      saveKeyBusy: false, saveKeyError: '',
      testBusy: false,
    }));
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

  // ── Feature routing ────────────────────────────────────────────────────────

  modelsFor(providerName: string): string[] {
    return this.providers().find(p => p.catalog.providerName === providerName)?.catalog.models ?? [];
  }

  onFeatureProviderChange(c: AiProviderConfigItem, newProvider: string): void {
    const newModel = this.modelsFor(newProvider)[0] ?? c.modelName;
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
      error: err => this.featureError.update(e => ({ ...e, [c.id]: err.error?.error ?? 'Failed.' })),
    });
  }

  // ── Provider credentials ───────────────────────────────────────────────────

  keyPlaceholder(provider: string): string {
    return ({ openai: 'sk-…', gemini: 'AIza…', anthropic: 'sk-ant-…' } as Record<string, string>)[provider] ?? '…';
  }

  toggleKeyEdit(ps: ProviderState): void {
    ps.editingKey = !ps.editingKey;
    ps.editKeyValue = '';
    ps.saveKeyError = '';
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
        // On error show all models as failed with the error message
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
}

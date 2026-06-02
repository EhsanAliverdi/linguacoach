import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiProviderConfigItem, AiProviderCatalogItem } from '../../../core/models/admin.models';

type Panel = 'model' | 'apikey' | null;

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2 class="text-lg font-bold text-slate-900 mb-2">AI Provider Configuration</h2>
    <p class="text-sm text-slate-500 mb-5">
      Configure the provider, model, and API key for each feature.
      API keys stored here override environment variables.
      Changes take effect on the next AI call.
    </p>

    @if (loading()) {
      <p class="text-sm text-slate-400">Loading…</p>
    } @else {
      <div class="space-y-3">
        @for (c of configs(); track c.id) {
          <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">

            <!-- Header row -->
            <div class="flex items-start justify-between">
              <div>
                <div class="text-xs font-medium text-indigo-600 uppercase tracking-wide mb-1">{{ c.featureKey }}</div>
                <div class="text-sm text-slate-800">
                  <span class="capitalize">{{ c.providerName }}</span> /
                  <span class="font-mono">{{ c.modelName }}</span>
                </div>
                <div class="mt-1 text-xs" [class]="c.hasStoredApiKey ? 'text-green-600' : 'text-slate-400'">
                  {{ c.hasStoredApiKey ? 'API key stored in DB' : 'Using environment variable' }}
                </div>
              </div>
              @if (openPanel() === null || openPanelId() !== c.id) {
                <div class="flex gap-3">
                  <button (click)="openModelPanel(c)" class="text-xs text-indigo-600 hover:underline">Edit model</button>
                  <button (click)="openApiKeyPanel(c)" class="text-xs text-slate-500 hover:underline">
                    {{ c.hasStoredApiKey ? 'Update key' : 'Set API key' }}
                  </button>
                </div>
              }
            </div>

            <!-- Model edit panel -->
            @if (openPanelId() === c.id && openPanel() === 'model') {
              <div class="mt-4 pt-4 border-t border-slate-100">
                <div class="flex flex-wrap gap-3 items-end">
                  <div>
                    <label class="block text-xs text-slate-500 mb-1">Provider</label>
                    <select [(ngModel)]="editProvider" (ngModelChange)="onProviderChange()"
                      class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-36 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                      @for (p of catalog(); track p.providerName) {
                        <option [value]="p.providerName">{{ p.providerName }}</option>
                      }
                    </select>
                  </div>
                  <div>
                    <label class="block text-xs text-slate-500 mb-1">Model</label>
                    <select [(ngModel)]="editModel"
                      class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-64 font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500">
                      @for (m of modelsForEditProvider(); track m) {
                        <option [value]="m">{{ m }}</option>
                      }
                    </select>
                  </div>
                  <button (click)="saveModel(c.id)"
                    class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 transition-colors">
                    Save
                  </button>
                  <button (click)="closePanel()" class="text-xs text-slate-400 hover:underline">Cancel</button>
                </div>
                @if (panelSuccess()) { <p class="mt-2 text-xs text-green-600">Saved.</p> }
                @if (panelError()) { <p class="mt-2 text-xs text-red-600">{{ panelError() }}</p> }
              </div>
            }

            <!-- API key panel -->
            @if (openPanelId() === c.id && openPanel() === 'apikey') {
              <div class="mt-4 pt-4 border-t border-slate-100">
                <div class="flex flex-wrap gap-3 items-end">
                  <div class="flex-1 min-w-64">
                    <label class="block text-xs text-slate-500 mb-1">
                      API Key
                      <span class="text-slate-400 ml-1">(leave blank to clear and use env var)</span>
                    </label>
                    <input type="password" [(ngModel)]="editApiKey" placeholder="sk-… or AIza… or sk-ant-…"
                      class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
                  </div>
                  <button (click)="saveApiKey(c.id)"
                    class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 transition-colors">
                    Save
                  </button>
                  @if (c.hasStoredApiKey) {
                    <button (click)="clearApiKey(c.id)"
                      class="rounded-lg border border-red-300 px-4 py-2 text-sm text-red-600 hover:bg-red-50 transition-colors">
                      Clear key
                    </button>
                  }
                  <button (click)="closePanel()" class="text-xs text-slate-400 hover:underline">Cancel</button>
                </div>
                @if (panelSuccess()) { <p class="mt-2 text-xs text-green-600">Key saved.</p> }
                @if (panelError()) { <p class="mt-2 text-xs text-red-600">{{ panelError() }}</p> }
              </div>
            }

          </div>
        }
      </div>

      <!-- Provider catalog reference -->
      <div class="mt-6 border-t border-slate-100 pt-5">
        <h3 class="text-sm font-semibold text-slate-700 mb-3">Available Providers &amp; Models</h3>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
          @for (p of catalog(); track p.providerName) {
            <div class="rounded-lg border border-slate-200 bg-slate-50 p-3">
              <div class="text-xs font-semibold text-slate-600 uppercase tracking-wide mb-2">{{ p.providerName }}</div>
              <ul class="space-y-0.5">
                @for (m of p.models; track m) {
                  <li class="text-xs font-mono text-slate-500">{{ m }}</li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class AdminAiConfigComponent implements OnInit {
  configs = signal<AiProviderConfigItem[]>([]);
  catalog = signal<AiProviderCatalogItem[]>([]);
  loading = signal(true);

  openPanelId = signal<string | null>(null);
  openPanel = signal<Panel>(null);
  panelSuccess = signal(false);
  panelError = signal('');

  editProvider = '';
  editModel = '';
  editApiKey = '';

  readonly modelsForEditProvider = computed(
    () => this.catalog().find(p => p.providerName === this.editProvider)?.models ?? []
  );

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    forkJoin({ configs: this.adminApi.listAiConfigs(), catalog: this.adminApi.listAiProviders() }).subscribe({
      next: ({ configs, catalog }) => { this.configs.set(configs); this.catalog.set(catalog); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openModelPanel(c: AiProviderConfigItem): void {
    this.openPanelId.set(c.id);
    this.openPanel.set('model');
    this.editProvider = c.providerName;
    this.editModel = c.modelName;
    this.panelSuccess.set(false);
    this.panelError.set('');
  }

  openApiKeyPanel(c: AiProviderConfigItem): void {
    this.openPanelId.set(c.id);
    this.openPanel.set('apikey');
    this.editApiKey = '';
    this.panelSuccess.set(false);
    this.panelError.set('');
  }

  closePanel(): void {
    this.openPanelId.set(null);
    this.openPanel.set(null);
  }

  onProviderChange(): void {
    const models = this.catalog().find(p => p.providerName === this.editProvider)?.models ?? [];
    if (!models.includes(this.editModel)) this.editModel = models[0] ?? '';
  }

  saveModel(id: string): void {
    this.adminApi.updateAiConfig(id, this.editProvider, this.editModel).subscribe({
      next: updated => {
        this.configs.update(cs => cs.map(c => c.id === id ? updated : c));
        this.panelSuccess.set(true);
        setTimeout(() => this.closePanel(), 1200);
      },
      error: err => this.panelError.set(err.error?.error ?? 'Failed to save.'),
    });
  }

  saveApiKey(id: string): void {
    this.adminApi.updateAiApiKey(id, this.editApiKey || null).subscribe({
      next: updated => {
        this.configs.update(cs => cs.map(c => c.id === id ? updated : c));
        this.panelSuccess.set(true);
        setTimeout(() => this.closePanel(), 1200);
      },
      error: err => this.panelError.set(err.error?.error ?? 'Failed to save API key.'),
    });
  }

  clearApiKey(id: string): void {
    this.adminApi.updateAiApiKey(id, null).subscribe({
      next: updated => {
        this.configs.update(cs => cs.map(c => c.id === id ? updated : c));
        this.panelSuccess.set(true);
        setTimeout(() => this.closePanel(), 1200);
      },
      error: err => this.panelError.set(err.error?.error ?? 'Failed to clear API key.'),
    });
  }
}

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiProviderConfigItem, AiProviderCatalogItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2 class="text-lg font-bold text-slate-900 mb-2">AI Provider Configuration</h2>
    <p class="text-sm text-slate-500 mb-5">
      Choose the AI provider and model for each feature. Changes take effect on the next AI call.
    </p>

    @if (loading()) {
      <p class="text-sm text-slate-400">Loading…</p>
    } @else {
      <div class="space-y-3">
        @for (c of configs(); track c.id) {
          <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
            <div class="flex items-start justify-between">
              <div>
                <div class="text-xs font-medium text-indigo-600 uppercase tracking-wide mb-1">{{ c.featureKey }}</div>
                @if (editingId() !== c.id) {
                  <div class="text-sm text-slate-800">
                    <span class="capitalize">{{ c.providerName }}</span> /
                    <span class="font-mono">{{ c.modelName }}</span>
                  </div>
                }
              </div>
              @if (editingId() !== c.id) {
                <button (click)="startEdit(c)" class="text-xs text-indigo-600 hover:underline">Edit</button>
              }
            </div>

            @if (editingId() === c.id) {
              <div class="mt-3 flex flex-wrap gap-3 items-end">
                <div>
                  <label class="block text-xs text-slate-500 mb-1">Provider</label>
                  <select
                    [(ngModel)]="editProvider"
                    (ngModelChange)="onProviderChange()"
                    class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-36 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                    @for (p of catalog(); track p.providerName) {
                      <option [value]="p.providerName">{{ p.providerName }}</option>
                    }
                  </select>
                </div>

                <div>
                  <label class="block text-xs text-slate-500 mb-1">Model</label>
                  <select
                    [(ngModel)]="editModel"
                    class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-64 font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500">
                    @for (m of modelsForEditProvider(); track m) {
                      <option [value]="m">{{ m }}</option>
                    }
                  </select>
                </div>

                <button
                  (click)="save(c.id)"
                  class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 transition-colors">
                  Save
                </button>
                <button (click)="editingId.set(null)" class="text-xs text-slate-400 hover:underline">Cancel</button>
              </div>

              @if (saveSuccess() === c.id) {
                <p class="mt-2 text-xs text-green-600">Saved. New model takes effect on next AI call.</p>
              }
              @if (saveError()) {
                <p class="mt-2 text-xs text-red-600">{{ saveError() }}</p>
              }
            }
          </div>
        }
      </div>

      <div class="mt-6 border-t border-slate-100 pt-5">
        <h3 class="text-sm font-semibold text-slate-700 mb-3">Available Providers & Models</h3>
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
  editingId = signal<string | null>(null);
  saveSuccess = signal<string | null>(null);
  saveError = signal('');
  editProvider = '';
  editModel = '';

  readonly modelsForEditProvider = computed(() => {
    const entry = this.catalog().find(p => p.providerName === this.editProvider);
    return entry?.models ?? [];
  });

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    forkJoin({
      configs: this.adminApi.listAiConfigs(),
      catalog: this.adminApi.listAiProviders(),
    }).subscribe({
      next: ({ configs, catalog }) => {
        this.configs.set(configs);
        this.catalog.set(catalog);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startEdit(c: AiProviderConfigItem): void {
    this.editingId.set(c.id);
    this.editProvider = c.providerName;
    this.editModel = c.modelName;
    this.saveSuccess.set(null);
    this.saveError.set('');
  }

  onProviderChange(): void {
    const models = this.catalog().find(p => p.providerName === this.editProvider)?.models ?? [];
    if (!models.includes(this.editModel)) {
      this.editModel = models[0] ?? '';
    }
  }

  save(id: string): void {
    this.adminApi.updateAiConfig(id, this.editProvider, this.editModel).subscribe({
      next: updated => {
        this.configs.update(cs => cs.map(c => c.id === id ? updated : c));
        this.saveSuccess.set(id);
        setTimeout(() => { this.editingId.set(null); this.saveSuccess.set(null); }, 1500);
      },
      error: err => this.saveError.set(err.error?.error ?? 'Failed to save AI configuration.'),
    });
  }
}

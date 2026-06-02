import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AiProviderConfigItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h2 class="text-lg font-bold text-slate-900 mb-2">AI Provider Configuration</h2>
    <p class="text-sm text-slate-500 mb-2">Choose the OpenAI model used by each feature. Model changes take effect on the next AI call.</p>
    <p class="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-5">
      This release supports OpenAI only. Claude and Gemini require provider adapters before they can be selected here.
    </p>

    <div class="space-y-3">
      @for (c of configs(); track c.id) {
        <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
          <div class="flex items-start justify-between">
            <div>
              <div class="text-xs font-medium text-indigo-600 uppercase tracking-wide mb-1">{{ c.featureKey }}</div>
              @if (editingId() !== c.id) {
                <div class="text-sm text-slate-800">{{ c.providerName }} / <span class="font-mono">{{ c.modelName }}</span></div>
              }
            </div>
            @if (editingId() !== c.id) {
              <button (click)="startEdit(c)" class="text-xs text-indigo-600 hover:underline">Edit</button>
            }
          </div>
          @if (editingId() === c.id) {
            <div class="mt-3 flex gap-3 items-end">
              <div>
                <label class="block text-xs text-slate-500 mb-1">Provider</label>
                <select [(ngModel)]="editProvider" class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-32 focus:outline-none focus:ring-2 focus:ring-indigo-500">
                  <option value="openai">openai</option>
                </select>
              </div>
              <div>
                <label class="block text-xs text-slate-500 mb-1">Model</label>
                <input [(ngModel)]="editModel" class="rounded-lg border border-slate-300 px-3 py-2 text-sm w-44 font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500" />
              </div>
              <button (click)="save(c.id)" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 transition-colors">Save</button>
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
  `,
})
export class AdminAiConfigComponent implements OnInit {
  configs = signal<AiProviderConfigItem[]>([]);
  editingId = signal<string | null>(null);
  saveSuccess = signal<string | null>(null);
  saveError = signal('');
  editProvider = ''; editModel = '';

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listAiConfigs().subscribe({ next: c => this.configs.set(c) });
  }

  startEdit(c: AiProviderConfigItem): void {
    this.editingId.set(c.id);
    this.editProvider = c.providerName;
    this.editModel = c.modelName;
    this.saveSuccess.set(null);
    this.saveError.set('');
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

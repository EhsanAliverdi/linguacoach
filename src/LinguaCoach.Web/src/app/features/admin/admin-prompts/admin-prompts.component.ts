import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { PromptTemplateItem, PromptTemplateDetail } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-prompts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-lg font-bold text-slate-900">Prompt Templates</h2>
      <button (click)="showForm.set(!showForm())" class="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 transition-colors">
        + New version
      </button>
    </div>

    @if (showForm()) {
      <div class="bg-white rounded-xl border border-slate-200 p-5 mb-5 shadow-sm">
        <h3 class="font-medium text-slate-800 mb-3 text-sm">Create new prompt version</h3>
        <div class="space-y-3">
          <input [(ngModel)]="newKey" placeholder="key (e.g. writing.exercise.v2)" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          <textarea [(ngModel)]="newContent" rows="6" placeholder="Prompt content with {{'{{variable}}'}} placeholders" class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono resize-none focus:outline-none focus:ring-2 focus:ring-indigo-500"></textarea>
          <div class="flex gap-3">
            <input [(ngModel)]="newMaxInput" type="number" placeholder="Max input tokens" class="w-32 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
            <input [(ngModel)]="newMaxOutput" type="number" placeholder="Max output tokens" class="w-32 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>
          @if (formError()) { <p class="text-xs text-red-600">{{ formError() }}</p> }
          <button (click)="createVersion()" class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white hover:bg-green-700 transition-colors">
            Create
          </button>
        </div>
      </div>
    }

    @if (detail()) {
      <div class="bg-white rounded-xl border border-indigo-200 p-5 mb-5 shadow-sm">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-medium text-indigo-600 uppercase tracking-wide">{{ detail()!.key }} v{{ detail()!.version }}</span>
          <button (click)="detail.set(null)" class="text-xs text-slate-400 hover:text-slate-600">Close</button>
        </div>
        <pre class="text-xs text-slate-700 bg-slate-50 rounded-lg p-3 overflow-auto max-h-48 whitespace-pre-wrap font-mono">{{ detail()!.content }}</pre>
      </div>
    }

    <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-slate-50 border-b border-slate-200">
          <tr>
            <th class="text-left px-4 py-3 font-medium text-slate-600">Key</th>
            <th class="text-left px-4 py-3 font-medium text-slate-600">Version</th>
            <th class="text-left px-4 py-3 font-medium text-slate-600">Status</th>
            <th class="text-left px-4 py-3 font-medium text-slate-600">Tokens</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody>
          @for (p of prompts(); track p.id) {
            <tr class="border-b border-slate-100 last:border-0 hover:bg-slate-50">
              <td class="px-4 py-3 font-mono text-xs text-slate-700">{{ p.key }}</td>
              <td class="px-4 py-3 text-slate-600">v{{ p.version }}</td>
              <td class="px-4 py-3">
                <span class="inline-block rounded-full px-2 py-0.5 text-xs font-medium {{ p.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500' }}">
                  {{ p.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
              <td class="px-4 py-3 text-xs text-slate-500">{{ p.maxInputTokens }}/{{ p.maxOutputTokens }}</td>
              <td class="px-4 py-3 flex gap-2 justify-end">
                <button (click)="viewDetail(p.id)" class="text-xs text-indigo-600 hover:underline">View</button>
                @if (p.isActive) {
                  <button (click)="deactivate(p)" class="text-xs text-amber-600 hover:underline">Deactivate</button>
                } @else {
                  <button (click)="activate(p)" class="text-xs text-green-600 hover:underline">Activate</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class AdminPromptsComponent implements OnInit {
  prompts = signal<PromptTemplateItem[]>([]);
  detail = signal<PromptTemplateDetail | null>(null);
  showForm = signal(false);
  formError = signal('');
  newKey = ''; newContent = ''; newMaxInput = 800; newMaxOutput = 600;

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.adminApi.listPrompts().subscribe({ next: p => this.prompts.set(p) });
  }

  viewDetail(id: string): void {
    this.adminApi.getPrompt(id).subscribe({ next: d => this.detail.set(d) });
  }

  activate(p: PromptTemplateItem): void {
    this.adminApi.activatePrompt(p.id).subscribe({ next: () => this.load() });
  }

  deactivate(p: PromptTemplateItem): void {
    this.adminApi.deactivatePrompt(p.id).subscribe({ next: () => this.load() });
  }

  createVersion(): void {
    if (!this.newKey.trim() || !this.newContent.trim()) {
      this.formError.set('Key and content are required.');
      return;
    }
    this.adminApi.createPromptVersion({
      key: this.newKey, content: this.newContent,
      maxInputTokens: this.newMaxInput, maxOutputTokens: this.newMaxOutput
    }).subscribe({
      next: () => { this.showForm.set(false); this.newKey = ''; this.newContent = ''; this.formError.set(''); this.load(); },
      error: err => this.formError.set(err.error?.error ?? 'Failed to create.'),
    });
  }
}

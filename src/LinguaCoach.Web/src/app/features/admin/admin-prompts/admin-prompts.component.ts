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
    <div class="sp-admin-page-header">
      <div class="sp-admin-header-row">
        <div>
          <h1 class="sp-admin-page-title">Prompt Templates</h1>
          <p class="sp-admin-page-sub">Manage and version AI prompt templates</p>
        </div>
        <button (click)="showForm.set(!showForm())" class="sp-admin-btn-primary">+ New version</button>
      </div>
    </div>

    @if (showForm()) {
      <div class="sp-admin-form-card sp-admin-mb">
        <h3 class="sp-admin-section-title">Create new prompt version</h3>
        <div class="sp-admin-field-grid">
          <label class="sp-admin-field sp-admin-wide">
            <span class="sp-admin-field-label">Key</span>
            <input [(ngModel)]="newKey" placeholder="key (e.g. writing.exercise.v2)" class="sp-input" />
          </label>
          <label class="sp-admin-field sp-admin-wide">
            <span class="sp-admin-field-label">Content</span>
            <textarea [(ngModel)]="newContent" rows="6" placeholder="Prompt content with {{'{{variable}}'}} placeholders" class="sp-input sp-admin-mono"></textarea>
          </label>
          <label class="sp-admin-field">
            <span class="sp-admin-field-label">Max input tokens</span>
            <input [(ngModel)]="newMaxInput" type="number" class="sp-input" />
          </label>
          <label class="sp-admin-field">
            <span class="sp-admin-field-label">Max output tokens</span>
            <input [(ngModel)]="newMaxOutput" type="number" class="sp-input" />
          </label>
        </div>
        @if (formError()) { <p class="sp-admin-text-error">{{ formError() }}</p> }
        <div class="sp-admin-action-row">
          <button (click)="createVersion()" class="sp-admin-btn-primary">Create</button>
        </div>
      </div>
    }

    @if (detail()) {
      <div class="sp-admin-form-card sp-admin-mb">
        <div class="sp-admin-header-row">
          <span class="sp-admin-subsection-title">{{ detail()!.key }} v{{ detail()!.version }}</span>
          <button (click)="detail.set(null)" class="sp-admin-link-button">Close</button>
        </div>
        <pre class="sp-admin-prompt-preview">{{ detail()!.content }}</pre>
      </div>
    }

    <div class="sp-admin-table-card">
      <table class="sp-admin-table">
        <thead>
          <tr>
            <th>Key</th>
            <th>Version</th>
            <th>Status</th>
            <th>Tokens</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (p of prompts(); track p.id) {
            <tr>
              <td class="sp-admin-table-mono">{{ p.key }}</td>
              <td>v{{ p.version }}</td>
              <td>
                <span class="sp-admin-badge" [class.sp-admin-badge-green]="p.isActive" [class.sp-admin-badge-slate]="!p.isActive">
                  {{ p.isActive ? 'Active' : 'Inactive' }}
                </span>
              </td>
              <td class="sp-admin-table-muted">{{ p.maxInputTokens }}/{{ p.maxOutputTokens }}</td>
              <td class="sp-admin-row-actions">
                <button (click)="viewDetail(p.id)" class="sp-admin-link-button">View</button>
                @if (p.isActive) {
                  <button (click)="deactivate(p)" class="sp-admin-link-button sp-admin-text-amber">Deactivate</button>
                } @else {
                  <button (click)="activate(p)" class="sp-admin-link-button sp-admin-text-success-link">Activate</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
      @if (prompts().length === 0) {
        <p class="sp-admin-empty-row">No prompt templates yet.</p>
      }
    </div>
  `,
  styles: [`
    .sp-admin-header-row{display:flex;align-items:start;justify-content:space-between;gap:12px;flex-wrap:wrap;}
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-link-button{border:none;background:none;padding:0;font:inherit;font-size:12.5px;font-weight:800;cursor:pointer;color:#4338CA;}
    .sp-admin-mb{margin-bottom:20px;}
    .sp-admin-mono{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;resize:vertical;}
    .sp-admin-prompt-preview{font-size:12px;color:#334155;background:#F8FAFC;border-radius:8px;padding:12px;overflow:auto;max-height:220px;white-space:pre-wrap;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;margin:0;}
    .sp-admin-row-actions{display:flex;gap:10px;justify-content:flex-end;}
    .sp-admin-text-amber{color:#D97706;}
    .sp-admin-text-success-link{color:#16A34A;}
  `],
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

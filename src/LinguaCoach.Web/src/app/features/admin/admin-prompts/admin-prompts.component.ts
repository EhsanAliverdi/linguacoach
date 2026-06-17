import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { PromptTemplateItem, PromptTemplateDetail } from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminFormFieldComponent,
  SpAdminPageHeaderComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-prompts',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminFormFieldComponent,
    SpAdminPageHeaderComponent,
    SpAdminTableComponent,
  ],
  template: `
    <sp-admin-page-header title="Prompt Templates" subtitle="Manage and version AI prompt templates">
      <sp-admin-button (click)="showForm.set(!showForm())">New version</sp-admin-button>
    </sp-admin-page-header>

    @if (showForm()) {
      <sp-admin-card title="Create new prompt version">
        <div class="sp-admin-field-grid">
          <sp-admin-form-field label="Key" class="sp-admin-wide">
            <input [(ngModel)]="newKey" placeholder="key (e.g. writing.exercise.v2)" class="sp-input" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Content" class="sp-admin-wide">
            <textarea [(ngModel)]="newContent" rows="6" placeholder="Prompt content with {{'{{variable}}'}} placeholders" class="sp-input sp-admin-mono"></textarea>
          </sp-admin-form-field>
          <sp-admin-form-field label="Max input tokens">
            <input [(ngModel)]="newMaxInput" type="number" class="sp-input" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max output tokens">
            <input [(ngModel)]="newMaxOutput" type="number" class="sp-input" />
          </sp-admin-form-field>
        </div>
        @if (formError()) { <p class="sp-admin-text-error">{{ formError() }}</p> }
        <div class="sp-admin-action-row">
          <sp-admin-button (click)="createVersion()">Create</sp-admin-button>
        </div>
      </sp-admin-card>
    }

    @if (detail()) {
      <sp-admin-card [title]="detail()!.key + ' v' + detail()!.version">
        <sp-admin-button slot="actions" variant="ghost" size="sm" (click)="detail.set(null)">Close</sp-admin-button>
        <pre class="sp-admin-prompt-preview">{{ detail()!.content }}</pre>
      </sp-admin-card>
    }

    @if (prompts().length === 0) {
      <sp-admin-empty-state message="No prompt templates yet." />
    } @else {
      <sp-admin-table>
      <table>
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
                <sp-admin-badge [tone]="p.isActive ? 'success' : 'neutral'">
                  {{ p.isActive ? 'Active' : 'Inactive' }}
                </sp-admin-badge>
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
      </sp-admin-table>
    }
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

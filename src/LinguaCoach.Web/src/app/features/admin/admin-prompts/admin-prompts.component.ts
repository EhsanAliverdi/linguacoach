import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { PromptTemplateItem, PromptTemplateDetail } from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminStatCardComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

type PromptStatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-admin-prompts',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCodePillComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminStatCardComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
  ],
  template: `
    <sp-admin-page-header title="Prompt Templates" subtitle="Manage and version AI prompt templates">
      <sp-admin-button (click)="toggleForm()">{{ showForm() ? 'Cancel' : 'New version' }}</sp-admin-button>
    </sp-admin-page-header>

    <sp-admin-page-body>
    @if (showForm()) {
      <sp-admin-card title="Create new prompt version" variant="section" padding="md" [headerDivider]="true">
        <div class="sp-admin-field-grid">
          <sp-admin-form-field label="Key" class="sp-admin-wide">
            <sp-admin-input [(ngModel)]="newKey" placeholder="key (e.g. writing.exercise.v2)" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Content" class="sp-admin-wide">
            <sp-admin-textarea [(ngModel)]="newContent" [rows]="8" placeholder="Prompt content with {{'{{variable}}'}} placeholders" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max input tokens">
            <sp-admin-number-input [(ngModel)]="newMaxInput" [min]="1" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max output tokens">
            <sp-admin-number-input [(ngModel)]="newMaxOutput" [min]="1" />
          </sp-admin-form-field>
        </div>
        @if (formError()) { <sp-admin-alert variant="error">{{ formError() }}</sp-admin-alert> }
        <div class="sp-admin-action-row">
          <sp-admin-button (click)="createVersion()" [loading]="creating()">Create</sp-admin-button>
        </div>
      </sp-admin-card>
    }

    @if (detail()) {
      <sp-admin-card [title]="detail()!.key + ' v' + detail()!.version" variant="section" padding="md" [headerDivider]="true">
        <sp-admin-button slot="actions" variant="neutral" appearance="ghost" size="sm" (click)="detail.set(null)">Close</sp-admin-button>
        <pre class="sp-admin-prompt-preview">{{ detail()!.content }}</pre>
      </sp-admin-card>
    }

    <div class="sp-admin-metric-grid" aria-label="Prompt template summary">
      <sp-admin-stat-card tone="primary" size="md" label="Templates" [value]="uniqueKeyCount()" />
      <sp-admin-stat-card tone="success" size="md" label="Active versions" [value]="activeCount()" />
      <sp-admin-stat-card tone="neutral" size="md" label="Total versions" [value]="prompts().length" />
      <sp-admin-stat-card tone="info" size="md" label="Avg token budget" [value]="averageTokenBudget()" />
    </div>

    <sp-admin-card title="Prompt library" variant="section" padding="none" [headerDivider]="true">
      <sp-admin-filter-bar layout="responsive" density="compact">
        <sp-admin-form-field search label="Search prompts" size="sm">
          <sp-admin-input [ngModel]="searchTerm()" (ngModelChange)="setSearchTerm($event)" size="sm" placeholder="Search by key" />
        </sp-admin-form-field>
        <sp-admin-form-field filters label="Status" size="sm">
          <sp-admin-select
            [ngModel]="statusFilter()"
            (ngModelChange)="setStatusFilter($event)"
            [options]="statusFilterOptions"
            size="sm" />
        </sp-admin-form-field>
        <sp-admin-button actions variant="neutral" appearance="outline" size="sm" (click)="load()" [loading]="loading()">Refresh</sp-admin-button>
      </sp-admin-filter-bar>

      @if (loading()) {
        <sp-admin-loading-state message="Loading prompt templates" />
      } @else if (loadError()) {
        <div class="sp-admin-state-wrap">
          <sp-admin-error-state title="Prompt templates could not load" [message]="loadError()" />
          <sp-admin-button variant="primary" appearance="outline" size="sm" (click)="load()">Try again</sp-admin-button>
        </div>
      } @else if (filteredPrompts().length === 0) {
        <sp-admin-empty-state [message]="emptyMessage()" />
    } @else {
        <sp-admin-table variant="data" density="compact" minWidth="860px">
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Version</th>
                <th>Status</th>
                <th>Token budget</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (p of pagedPrompts(); track p.id) {
                <tr>
                  <td><sp-admin-code-pill [value]="p.key" tone="neutral" [maxLength]="48" /></td>
                  <td class="sp-admin-num">v{{ p.version }}</td>
                  <td>
                    <sp-admin-badge [tone]="p.isActive ? 'success' : 'neutral'" [dot]="true">
                      {{ p.isActive ? 'Active' : 'Inactive' }}
                    </sp-admin-badge>
                  </td>
                  <td class="sp-admin-table-muted sp-admin-num">{{ tokenBudgetLabel(p) }}</td>
                  <td class="sp-admin-actions">
                    <sp-admin-table-actions>
                      <button role="menuitem" type="button" class="sp-adm-action-item" (click)="viewDetail(p.id)">View content</button>
                      @if (p.isActive) {
                        <button role="menuitem" type="button" class="sp-adm-action-item" (click)="deactivate(p)" [disabled]="busyPromptId() === p.id">Deactivate</button>
                      } @else {
                        <button role="menuitem" type="button" class="sp-adm-action-item" (click)="activate(p)" [disabled]="busyPromptId() === p.id">Activate</button>
                      }
                    </sp-admin-table-actions>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </sp-admin-table>
        @if (totalPages() > 1) {
          <sp-admin-pagination [page]="page()" [totalPages]="totalPages()" (pageChange)="page.set($event)" />
        }
      }
    </sp-admin-card>
    </sp-admin-page-body>
  `,
  styles: [`
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-metric-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px;}
    .sp-admin-prompt-preview{font-size:12px;color:#334155;background:#F8FAFC;border-radius:8px;padding:12px;overflow:auto;max-height:220px;white-space:pre-wrap;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;margin:0;}
    .sp-admin-state-wrap{display:grid;gap:12px;padding:16px;}
    @media (max-width: 1100px){.sp-admin-metric-grid{grid-template-columns:repeat(2,minmax(0,1fr));}}
    @media (max-width: 640px){.sp-admin-metric-grid{grid-template-columns:1fr;}}
  `],
})
export class AdminPromptsComponent implements OnInit {
  prompts = signal<PromptTemplateItem[]>([]);
  detail = signal<PromptTemplateDetail | null>(null);
  showForm = signal(false);
  formError = signal('');
  loading = signal(false);
  loadError = signal('');
  creating = signal(false);
  busyPromptId = signal<string | null>(null);
  searchTerm = signal('');
  statusFilter = signal<PromptStatusFilter>('all');
  page = signal(1);
  readonly pageSize = 12;
  newKey = ''; newContent = ''; newMaxInput = 800; newMaxOutput = 600;

  readonly statusFilterOptions = [
    { value: 'all', label: 'All statuses' },
    { value: 'active', label: 'Active' },
    { value: 'inactive', label: 'Inactive' },
  ];

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void { this.load(); }

  activeCount = computed(() => this.prompts().filter(p => p.isActive).length);
  uniqueKeyCount = computed(() => new Set(this.prompts().map(p => p.key)).size);
  averageTokenBudget = computed(() => {
    const budgets = this.prompts()
      .map(p => (p.maxInputTokens ?? 0) + (p.maxOutputTokens ?? 0))
      .filter(total => total > 0);
    if (!budgets.length) return 'n/a';
    const average = Math.round(budgets.reduce((sum, value) => sum + value, 0) / budgets.length);
    return average.toLocaleString();
  });
  filteredPrompts = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();
    return this.prompts().filter(p => {
      const matchesSearch = !term || p.key.toLowerCase().includes(term);
      const matchesStatus =
        status === 'all' ||
        (status === 'active' && p.isActive) ||
        (status === 'inactive' && !p.isActive);
      return matchesSearch && matchesStatus;
    });
  });
  totalPages = computed(() => Math.max(1, Math.ceil(this.filteredPrompts().length / this.pageSize)));
  pagedPrompts = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredPrompts().slice(start, start + this.pageSize);
  });
  emptyMessage = computed(() => this.prompts().length === 0
    ? 'No prompt templates yet.'
    : 'No prompt templates match the current filters.');

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.adminApi.listPrompts().subscribe({
      next: p => {
        this.prompts.set(p);
        this.page.set(1);
        this.loading.set(false);
      },
      error: err => {
        this.loadError.set(err.error?.error ?? 'Refresh the page or try again.');
        this.loading.set(false);
      },
    });
  }

  viewDetail(id: string): void {
    this.busyPromptId.set(id);
    this.adminApi.getPrompt(id).subscribe({
      next: d => {
        this.detail.set(d);
        this.busyPromptId.set(null);
      },
      error: () => {
        this.loadError.set('Prompt content could not be loaded.');
        this.busyPromptId.set(null);
      },
    });
  }

  activate(p: PromptTemplateItem): void {
    this.busyPromptId.set(p.id);
    this.adminApi.activatePrompt(p.id).subscribe({
      next: () => { this.busyPromptId.set(null); this.load(); },
      error: () => { this.loadError.set('Prompt version could not be activated.'); this.busyPromptId.set(null); },
    });
  }

  deactivate(p: PromptTemplateItem): void {
    this.busyPromptId.set(p.id);
    this.adminApi.deactivatePrompt(p.id).subscribe({
      next: () => { this.busyPromptId.set(null); this.load(); },
      error: () => { this.loadError.set('Prompt version could not be deactivated.'); this.busyPromptId.set(null); },
    });
  }

  toggleForm(): void {
    this.showForm.set(!this.showForm());
    this.formError.set('');
  }

  setSearchTerm(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
  }

  setStatusFilter(status: PromptStatusFilter): void {
    this.statusFilter.set(status);
    this.page.set(1);
  }

  createVersion(): void {
    if (!this.newKey.trim() || !this.newContent.trim()) {
      this.formError.set('Key and content are required.');
      return;
    }
    this.creating.set(true);
    this.adminApi.createPromptVersion({
      key: this.newKey, content: this.newContent,
      maxInputTokens: this.newMaxInput, maxOutputTokens: this.newMaxOutput
    }).subscribe({
      next: () => {
        this.showForm.set(false);
        this.newKey = '';
        this.newContent = '';
        this.formError.set('');
        this.creating.set(false);
        this.load();
      },
      error: err => {
        this.formError.set(err.error?.error ?? 'Failed to create.');
        this.creating.set(false);
      },
    });
  }

  tokenBudgetLabel(prompt: PromptTemplateItem): string {
    const input = prompt.maxInputTokens?.toLocaleString() ?? 'n/a';
    const output = prompt.maxOutputTokens?.toLocaleString() ?? 'n/a';
    return `${input} in / ${output} out`;
  }
}

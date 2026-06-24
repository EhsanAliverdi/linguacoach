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
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
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
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
  ],
  template: `
    <sp-admin-page-header title="Prompts" [subtitle]="'Manage and version AI prompt templates · ' + uniqueKeyCount() + ' templates'">
      <sp-admin-button (click)="openCreate()">New version</sp-admin-button>
    </sp-admin-page-header>

    <sp-admin-page-body>

    <!-- KPI strip -->
    <div class="sp-pt-kpi-strip" aria-label="Prompt template summary">
      <sp-admin-kpi-card label="Templates" variant="indigo" layout="tile">
        <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
        {{ uniqueKeyCount() }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Active versions" [variant]="activeCount() > 0 ? 'green' : 'slate'" layout="tile">
        <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
        {{ activeCount() }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Total versions" variant="violet" layout="tile">
        <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/></svg>
        {{ prompts().length }}
      </sp-admin-kpi-card>
      <sp-admin-kpi-card label="Avg token budget" variant="amber" layout="tile">
        <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>
        {{ averageTokenBudget() }}
      </sp-admin-kpi-card>
    </div>

    <!-- Prompt library -->
    <sp-admin-card title="Prompt library" variant="section" padding="none" [headerDivider]="true">
      <sp-admin-filter-bar layout="responsive" density="compact">
        <sp-admin-form-field search label="Search prompts" size="sm">
          <sp-admin-input [ngModel]="searchTerm()" (ngModelChange)="setSearchTerm($event)" size="sm" placeholder="Search by key…" />
        </sp-admin-form-field>
        <sp-admin-form-field filters label="Category" size="sm">
          <sp-admin-select
            [ngModel]="categoryFilter()"
            (ngModelChange)="setCategoryFilter($event)"
            [options]="categoryFilterOptions()"
            size="sm"
            placeholder="All categories" />
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
            <colgroup>
              <col style="width:34%"/>
              <col style="width:11%"/>
              <col style="width:9%"/>
              <col style="width:13%"/>
              <col style="width:18%"/>
              <col style="width:15%"/>
            </colgroup>
            <thead>
              <tr>
                <th>KEY</th>
                <th>CATEGORY</th>
                <th>VERSION</th>
                <th>STATUS</th>
                <th class="sp-admin-th-right">TOKEN BUDGET</th>
                <th class="sp-admin-th-right">ACTIONS</th>
              </tr>
            </thead>
            <tbody>
              @for (p of pagedPrompts(); track p.id) {
                <tr class="sp-adm-row-click" (click)="openView(p)">
                  <!-- KEY -->
                  <td>
                    <div class="sp-adm-key-cell">
                      <sp-admin-code-pill [value]="p.key" tone="primary" [maxLength]="48" />
                      @if (p.isActive && isLatestVersion(p)) {
                        <sp-admin-badge tone="info" size="sm">latest</sp-admin-badge>
                      }
                    </div>
                  </td>
                  <!-- CATEGORY -->
                  <td>
                    <sp-admin-badge [tone]="categoryTone(promptCategory(p.key))">{{ promptCategory(p.key) }}</sp-admin-badge>
                  </td>
                  <!-- VERSION -->
                  <td class="sp-admin-num sp-adm-version">v{{ p.version }}</td>
                  <!-- STATUS -->
                  <td>
                    <sp-admin-badge [tone]="p.isActive ? 'success' : 'neutral'" [dot]="true">
                      {{ p.isActive ? 'Active' : 'Inactive' }}
                    </sp-admin-badge>
                  </td>
                  <!-- TOKEN BUDGET -->
                  <td class="sp-admin-num sp-adm-token-budget">{{ tokenBudgetLabel(p) }}</td>
                  <!-- ACTIONS -->
                  <td class="sp-admin-actions" (click)="$event.stopPropagation()">
                    <sp-admin-table-actions>
                      <button role="menuitem" type="button" class="sp-adm-action-item" (click)="openView(p)">View content</button>
                      <button role="menuitem" type="button" class="sp-adm-action-item" (click)="openEdit(p)">Edit</button>
                      @if (p.isActive) {
                        <button role="menuitem" type="button" class="sp-adm-action-item sp-adm-action-danger" (click)="deactivate(p)" [disabled]="busyPromptId() === p.id">Deactivate</button>
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

    <!-- VIEW slide-over -->
    <sp-admin-slide-over
      [open]="!!viewPrompt()"
      [title]="viewPrompt()?.key ?? ''"
      [subtitle]="viewSlideOverSubtitle()"
      size="lg"
      [loading]="detailLoading()"
      loadingMessage="Loading prompt"
      [error]="detailError()"
      (closed)="closeView()">

      @if (detail()) {
        <div class="sp-adm-so-meta-grid">
          <div class="sp-adm-so-meta-item">
            <div class="sp-adm-so-meta-lbl">CATEGORY</div>
            <div class="sp-adm-so-meta-val">{{ promptCategory(detail()!.key) }}</div>
          </div>
          <div class="sp-adm-so-meta-item">
            <div class="sp-adm-so-meta-lbl">STATUS</div>
            <div class="sp-adm-so-meta-val" [class.sp-adm-active]="detail()!.isActive" [class.sp-adm-inactive]="!detail()!.isActive">
              <span class="sp-adm-dot" [class.sp-adm-dot-active]="detail()!.isActive"></span>
              {{ detail()!.isActive ? 'Active' : 'Inactive' }}
            </div>
          </div>
          <div class="sp-adm-so-meta-item">
            <div class="sp-adm-so-meta-lbl">TOKEN IN</div>
            <div class="sp-adm-so-meta-val">{{ (detail()!.maxInputTokens ?? 0) | number }}</div>
          </div>
          <div class="sp-adm-so-meta-item">
            <div class="sp-adm-so-meta-lbl">TOKEN OUT</div>
            <div class="sp-adm-so-meta-val">{{ (detail()!.maxOutputTokens ?? 0) | number }}</div>
          </div>
        </div>

        @if (promptVars(detail()!.content).length > 0) {
          <div class="sp-adm-so-section">
            <div class="sp-adm-so-section-title">Variables <span class="sp-adm-muted">({{ promptVars(detail()!.content).length }})</span></div>
            <div class="sp-adm-var-pills">
              @for (v of promptVars(detail()!.content); track v) {
                <code class="sp-adm-var-pill">{{ '{{' + v + '}}' }}</code>
              }
            </div>
          </div>
        }

        <div class="sp-adm-so-section">
          <div class="sp-adm-so-section-title">Prompt template</div>
          <pre class="sp-adm-prompt-body">{{ detail()!.content }}</pre>
        </div>
      }

      <div slot="footer">
        <sp-admin-button (click)="openEditFromView()">Edit</sp-admin-button>
        @if (viewPrompt()?.isActive) {
          <sp-admin-button variant="danger" appearance="ghost" (click)="deactivateFromView()">Deactivate v{{ viewPrompt()?.version }}</sp-admin-button>
        }
        <sp-admin-button variant="neutral" appearance="ghost" (click)="closeView()">Close</sp-admin-button>
      </div>
    </sp-admin-slide-over>

    <!-- EDIT / CREATE slide-over -->
    <sp-admin-slide-over
      [open]="showEditPanel()"
      [title]="editRow() ? 'Edit · ' + editRow()!.key : 'New prompt version'"
      [subtitle]="editRow() ? 'v' + editRow()!.version + ' · ' + promptCategory(editRow()!.key) : ''"
      size="lg"
      [stackIndex]="viewPrompt() ? 1 : 0"
      (closed)="closeEdit()">

      <div class="sp-adm-edit-body">
        @if (editRow()) {
          <sp-admin-alert variant="warning">Saving creates a new version and deactivates the current active version.</sp-admin-alert>
        }
        <div class="sp-admin-field-grid">
          @if (!editRow()) {
            <sp-admin-form-field label="Key" class="sp-admin-wide">
              <sp-admin-input [(ngModel)]="newKey" placeholder="key (e.g. writing.exercise.v2)" />
            </sp-admin-form-field>
          }
          <sp-admin-form-field label="Token budget — Input">
            <sp-admin-number-input [(ngModel)]="newMaxInput" [min]="1" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Token budget — Output">
            <sp-admin-number-input [(ngModel)]="newMaxOutput" [min]="1" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Prompt body" class="sp-admin-wide">
            <sp-admin-textarea [(ngModel)]="newContent" [rows]="16" placeholder="Prompt content with {{ '{{variable}}' }} placeholders" class="sp-adm-mono-textarea" />
          </sp-admin-form-field>
        </div>
        @if (formError()) {
          <sp-admin-alert variant="error">{{ formError() }}</sp-admin-alert>
        }
      </div>

      <div slot="footer">
        <sp-admin-button (click)="createVersion()" [loading]="creating()">
          {{ editRow() ? 'Save as new version' : 'Create' }}
        </sp-admin-button>
        <sp-admin-button variant="neutral" appearance="ghost" (click)="closeEdit()">Cancel</sp-admin-button>
      </div>
    </sp-admin-slide-over>
  `,
  styles: [`
    .sp-admin-wide { grid-column: 1 / -1; }
    .sp-pt-kpi-strip {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 14px;
      padding: 16px 24px 0;
    }
    .sp-admin-state-wrap { display: grid; gap: 12px; padding: 16px; }
    .sp-admin-th-right { text-align: right !important; }
    .sp-adm-row-click { cursor: pointer; }
    .sp-adm-key-cell { display: flex; align-items: center; gap: 8px; }
    .sp-adm-version { font-size: 13.5px; font-weight: 700; }
    .sp-adm-token-budget { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 12.5px; color: var(--sp-admin-text-muted, #64748B); }
    .sp-adm-action-danger { color: #ef4444 !important; }

    /* Slide-over meta grid */
    .sp-adm-so-meta-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 10px;
      margin-bottom: 20px;
    }
    .sp-adm-so-meta-item {
      padding: 12px;
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      border-radius: 9px;
    }
    .sp-adm-so-meta-lbl {
      font-size: 10.5px;
      font-weight: 800;
      letter-spacing: .08em;
      text-transform: uppercase;
      color: var(--sp-admin-text-muted, #64748B);
      margin-bottom: 5px;
    }
    .sp-adm-so-meta-val {
      font-size: 14px;
      font-weight: 800;
      color: var(--sp-admin-text, #0F172A);
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .sp-adm-active { color: #13B07C; }
    .sp-adm-inactive { color: var(--sp-admin-text-muted, #64748B); }
    .sp-adm-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--sp-admin-text-muted, #64748B);
      flex-shrink: 0;
    }
    .sp-adm-dot-active { background: #13B07C; }

    /* Slide-over sections */
    .sp-adm-so-section { margin-bottom: 20px; }
    .sp-adm-so-section-title {
      font-size: 12px;
      font-weight: 700;
      color: var(--sp-admin-text, #0F172A);
      margin-bottom: 8px;
    }
    .sp-adm-muted { color: var(--sp-admin-text-muted, #64748B); font-weight: 500; }

    /* Variable pills */
    .sp-adm-var-pills { display: flex; flex-wrap: wrap; gap: 6px; }
    .sp-adm-var-pill {
      font-size: 12px;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      color: #B45CF0;
      background: #F2E9FF;
      padding: 3px 9px;
      border-radius: 6px;
    }

    /* Prompt body code block */
    .sp-adm-prompt-body {
      background: #1E1B3A;
      border-radius: 10px;
      padding: 16px;
      font-size: 12.5px;
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      color: #E8E4FF;
      line-height: 1.8;
      white-space: pre-wrap;
      max-height: 360px;
      overflow-y: auto;
      margin: 0;
    }

    /* Edit panel */
    .sp-adm-edit-body { display: flex; flex-direction: column; gap: 16px; }
    :host ::ng-deep .sp-adm-mono-textarea textarea {
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 12.5px;
      line-height: 1.7;
    }

    @media (max-width: 1100px) { .sp-pt-kpi-strip { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 640px)  { .sp-pt-kpi-strip { grid-template-columns: 1fr; } }
    @media (max-width: 600px)  { .sp-adm-so-meta-grid { grid-template-columns: repeat(2, 1fr); } }
  `],
})
export class AdminPromptsComponent implements OnInit {
  prompts = signal<PromptTemplateItem[]>([]);
  detail = signal<PromptTemplateDetail | null>(null);
  viewPrompt = signal<PromptTemplateItem | null>(null);
  editRow = signal<PromptTemplateItem | null>(null);
  showEditPanel = signal(false);
  formError = signal('');
  loading = signal(false);
  loadError = signal('');
  detailLoading = signal(false);
  detailError = signal('');
  creating = signal(false);
  busyPromptId = signal<string | null>(null);
  searchTerm = signal('');
  statusFilter = signal<PromptStatusFilter>('all');
  categoryFilter = signal('');
  page = signal(1);
  readonly pageSize = 12;
  newKey = '';
  newContent = '';
  newMaxInput = 800;
  newMaxOutput = 600;

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
    const avg = Math.round(budgets.reduce((sum, v) => sum + v, 0) / budgets.length);
    return avg.toLocaleString();
  });

  filteredPrompts = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const status = this.statusFilter();
    const cat = this.categoryFilter();
    return this.prompts().filter(p => {
      const matchesSearch = !term || p.key.toLowerCase().includes(term);
      const matchesStatus =
        status === 'all' ||
        (status === 'active' && p.isActive) ||
        (status === 'inactive' && !p.isActive);
      const matchesCategory = !cat || this.promptCategory(p.key) === cat;
      return matchesSearch && matchesStatus && matchesCategory;
    });
  });

  readonly categoryFilterOptions = computed(() => {
    const cats = new Set(this.prompts().map(p => this.promptCategory(p.key)));
    return Array.from(cats).sort().map(c => ({ value: c, label: c }));
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filteredPrompts().length / this.pageSize)));
  pagedPrompts = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredPrompts().slice(start, start + this.pageSize);
  });
  emptyMessage = computed(() =>
    this.prompts().length === 0
      ? 'No prompt templates yet.'
      : 'No prompt templates match the current filters.'
  );

  viewSlideOverSubtitle = computed(() => {
    const p = this.viewPrompt();
    if (!p) return '';
    return `${this.promptCategory(p.key)} · v${p.version}`;
  });

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.adminApi.listPrompts().subscribe({
      next: p => { this.prompts.set(p); this.page.set(1); this.loading.set(false); },
      error: err => {
        this.loadError.set(err.error?.error ?? 'Refresh the page or try again.');
        this.loading.set(false);
      },
    });
  }

  openView(p: PromptTemplateItem): void {
    this.viewPrompt.set(p);
    this.detail.set(null);
    this.detailError.set('');
    this.detailLoading.set(true);
    this.adminApi.getPrompt(p.id).subscribe({
      next: d => { this.detail.set(d); this.detailLoading.set(false); },
      error: () => {
        this.detailError.set('Prompt content could not be loaded.');
        this.detailLoading.set(false);
      },
    });
  }

  closeView(): void {
    this.viewPrompt.set(null);
    this.detail.set(null);
    this.detailError.set('');
  }

  openEdit(p: PromptTemplateItem): void {
    this.editRow.set(p);
    this.newContent = '';
    this.newMaxInput = p.maxInputTokens ?? 800;
    this.newMaxOutput = p.maxOutputTokens ?? 600;
    this.formError.set('');
    this.showEditPanel.set(true);
  }

  openEditFromView(): void {
    const p = this.viewPrompt();
    if (!p) return;
    const content = this.detail()?.content ?? '';
    this.editRow.set(p);
    this.newContent = content;
    this.newMaxInput = p.maxInputTokens ?? 800;
    this.newMaxOutput = p.maxOutputTokens ?? 600;
    this.formError.set('');
    this.showEditPanel.set(true);
  }

  openCreate(): void {
    this.editRow.set(null);
    this.newKey = '';
    this.newContent = '';
    this.newMaxInput = 800;
    this.newMaxOutput = 600;
    this.formError.set('');
    this.showEditPanel.set(true);
  }

  closeEdit(): void {
    this.showEditPanel.set(false);
    this.editRow.set(null);
    this.formError.set('');
  }

  deactivateFromView(): void {
    const p = this.viewPrompt();
    if (!p) return;
    this.closeView();
    this.deactivate(p);
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

  setSearchTerm(term: string): void { this.searchTerm.set(term); this.page.set(1); }
  setStatusFilter(status: PromptStatusFilter): void { this.statusFilter.set(status); this.page.set(1); }
  setCategoryFilter(cat: string): void { this.categoryFilter.set(cat); this.page.set(1); }

  promptCategory(key: string): string {
    // handles both dot-delimited (writing.exercise.v1) and underscore-delimited (activity_evaluate_answer)
    const first = key.split(/[._]/)[0] ?? key;
    if (!first) return 'Other';
    const capitalised = first.charAt(0).toUpperCase() + first.slice(1);
    // map common prefixes to category names matching the JSX
    const MAP: Record<string, string> = {
      Activity: 'Other',
      System: 'Curriculum',
      Memory: 'Memory',
      Placement: 'Assessment',
      Writing: 'Writing',
      Speaking: 'Speaking',
      Listening: 'Listening',
      Vocabulary: 'Vocabulary',
      Feedback: 'Feedback',
    };
    return MAP[capitalised] ?? capitalised;
  }

  isLatestVersion(p: PromptTemplateItem): boolean {
    const siblings = this.prompts().filter(s => s.key === p.key);
    const maxVersion = Math.max(...siblings.map(s => s.version));
    return p.version === maxVersion;
  }

  private static readonly CATEGORY_TONES: Record<string, 'info' | 'success' | 'warning' | 'danger' | 'neutral'> = {
    Writing:    'info',
    Speaking:   'warning',
    Feedback:   'success',
    Assessment: 'danger',
    Listening:  'info',
    Vocabulary: 'warning',
    Grammar:    'neutral',
    Memory:     'neutral',
    Curriculum: 'info',
    Other:      'neutral',
  };

  categoryTone(cat: string): 'info' | 'success' | 'warning' | 'danger' | 'neutral' {
    return AdminPromptsComponent.CATEGORY_TONES[cat] ?? 'neutral';
  }

  promptVars(content: string): string[] {
    const matches = content.matchAll(/\{\{(\w+)\}\}/g);
    return [...new Set([...matches].map(m => m[1]))];
  }

  createVersion(): void {
    if (!this.newContent.trim()) {
      this.formError.set('Prompt body is required.');
      return;
    }
    const key = this.editRow()?.key ?? this.newKey.trim();
    if (!key) {
      this.formError.set('Key is required.');
      return;
    }
    this.creating.set(true);
    this.adminApi.createPromptVersion({
      key,
      content: this.newContent,
      maxInputTokens: this.newMaxInput,
      maxOutputTokens: this.newMaxOutput,
    }).subscribe({
      next: () => {
        this.closeEdit();
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

  // kept for backward compat with existing spec
  viewDetail(id: string): void { this.openView(this.prompts().find(p => p.id === id) ?? { id, key: '', version: 0, isActive: false, maxInputTokens: null, maxOutputTokens: null }); }
  toggleForm(): void { this.showEditPanel() ? this.closeEdit() : this.openCreate(); }
  showForm = computed(() => this.showEditPanel() && !this.editRow());
}

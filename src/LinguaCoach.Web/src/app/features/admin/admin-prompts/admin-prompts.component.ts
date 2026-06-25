import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { PromptTemplateItem, PromptTemplateDetail } from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminButtonGroupComponent,
  SpAdminButtonGroupAction,
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
  SpAdminRowAction,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminStackComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
  SpAdminVersionSelectorComponent,
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
    SpAdminButtonGroupComponent,
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
    SpAdminStackComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
    SpAdminVersionSelectorComponent,
  ],
  templateUrl: './admin-prompts.component.html',
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

  viewSiblings = computed(() => {
    const p = this.viewPrompt();
    if (!p) return [];
    return this.prompts()
      .filter(s => s.key === p.key)
      .sort((a, b) => a.version - b.version);
  });

  viewSlideOverSubtitle = computed(() => {
    const p = this.viewPrompt();
    if (!p) return '';
    const siblings = this.viewSiblings();
    const count = siblings.length;
    return `${this.promptCategory(p.key)} · ${count} version${count !== 1 ? 's' : ''}`;
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

  onVersionChange(v: { id: string }): void {
    const sibling = this.viewSiblings().find(s => s.id === v.id);
    if (sibling) this.switchVersion(sibling);
  }

  switchVersion(sibling: PromptTemplateItem): void {
    if (sibling.id === this.viewPrompt()?.id) return;
    this.viewPrompt.set(sibling);
    this.detail.set(null);
    this.detailError.set('');
    this.detailLoading.set(true);
    this.adminApi.getPrompt(sibling.id).subscribe({
      next: d => { this.detail.set(d); this.detailLoading.set(false); },
      error: () => { this.detailError.set('Prompt content could not be loaded.'); this.detailLoading.set(false); },
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

  viewFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'edit', label: 'Edit', variant: 'primary', appearance: 'solid', icon: 'edit', iconColor: '#fff' },
      ...(this.viewPrompt()?.isActive ? [{ id: 'deactivate', label: `Deactivate v${this.viewPrompt()?.version}`, variant: 'danger' as const, appearance: 'ghost' as const }] : []),
      { id: 'close', label: 'Close', variant: 'neutral', appearance: 'outline' },
    ];
  }

  onViewFooterAction(actionId: string): void {
    switch (actionId) {
      case 'edit':       this.openEditFromView(); break;
      case 'deactivate': this.deactivateFromView(); break;
      case 'close':      this.closeView(); break;
    }
  }

  editFooterActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'save', label: this.editRow() ? 'Save as new version' : 'Create', variant: 'primary', appearance: 'solid', loading: this.creating() },
      { id: 'cancel', label: 'Cancel', variant: 'neutral', appearance: 'outline' },
    ];
  }

  onEditFooterAction(actionId: string): void {
    switch (actionId) {
      case 'save':   this.createVersion(); break;
      case 'cancel': this.closeEdit(); break;
    }
  }

  promptRowActions(p: PromptTemplateItem): SpAdminRowAction[] {
    return [
      { id: 'view',       label: 'View',       icon: 'view' },
      { id: 'edit',       label: 'Edit',       icon: 'edit' },
      p.isActive
        ? { id: 'deactivate', label: 'Deactivate', icon: 'deactivate', tone: 'danger', disabled: this.busyPromptId() === p.id }
        : { id: 'activate',   label: 'Activate',   icon: 'activate',   disabled: this.busyPromptId() === p.id },
    ];
  }

  onPromptRowAction(p: PromptTemplateItem, actionId: string): void {
    switch (actionId) {
      case 'view':       this.openView(p); break;
      case 'edit':       this.openEdit(p); break;
      case 'deactivate': this.deactivate(p); break;
      case 'activate':   this.activate(p); break;
    }
  }

  setSearchTerm(term: string): void { this.searchTerm.set(term); this.page.set(1); }
  setStatusFilter(status: PromptStatusFilter): void { this.statusFilter.set(status); this.page.set(1); }
  setCategoryFilter(cat: string): void { this.categoryFilter.set(cat); this.page.set(1); }

  promptCategory(key: string): string {
    const first = key.split(/[._]/)[0] ?? key;
    if (!first) return 'Other';
    const capitalised = first.charAt(0).toUpperCase() + first.slice(1);
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

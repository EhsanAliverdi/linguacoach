import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminResourceBankService,
  AdminResourceSourceService,
} from '../../../core/services/admin-resource-import.service';
import {
  ResourceBankGrammarListItemDto,
  ResourceBankGrammarDetailDto,
  AdminResourceSourceDto,
  RESOURCE_BANK_CEFR_LEVELS,
} from '../../../core/models/admin-resource-import.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminDrawerComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';

const PAGE_SIZE = 20;

/**
 * Phase E5 — read-only browse/search for the published CefrGrammarProfileEntry bank.
 * Browse/search only — all mutation happens through the Resource Candidates page (Phase E4).
 */
@Component({
  selector: 'app-admin-resource-bank-grammar',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminDrawerComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
  ],
  templateUrl: './admin-resource-bank-grammar.component.html',
})
export class AdminResourceBankGrammarComponent implements OnInit {
  items = signal<ResourceBankGrammarListItemDto[]>([]);
  loading = signal(true);
  error = signal('');

  searchQuery = signal('');
  cefrLevelFilter = signal<string>('all');
  sourceFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  sources = signal<AdminResourceSourceDto[]>([]);
  readonly sourceOptions = computed(() =>
    [{ value: 'all', label: 'All sources' }, ...this.sources().map(s => ({ value: s.sourceId, label: s.name }))]);

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  detail = signal<ResourceBankGrammarDetailDto | null>(null);
  detailLoading = signal(false);
  detailError = signal('');

  constructor(
    private bankSvc: AdminResourceBankService,
    private sourceSvc: AdminResourceSourceService,
  ) {}

  ngOnInit(): void {
    this.sourceSvc.list(1, 200).subscribe({
      next: result => this.sources.set(result.items),
      error: () => this.sources.set([]),
    });
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    const sourceId = this.sourceFilter() === 'all' ? undefined : this.sourceFilter();
    this.bankSvc.listGrammar(this.page(), this.pageSize, this.searchQuery() || undefined, cefrLevel, sourceId).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load grammar bank entries.'); },
    });
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onSourceFilterChange(value: string): void {
    this.sourceFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openDrawer(item: ResourceBankGrammarListItemDto): void {
    this.detail.set(null);
    this.detailError.set('');
    this.drawerOpen.set(true);
    this.detailLoading.set(true);
    this.bankSvc.getGrammarDetail(item.id).subscribe({
      next: result => { this.detail.set(result); this.detailLoading.set(false); },
      error: err => { this.detailLoading.set(false); this.detailError.set(err.error?.error ?? 'Could not load entry detail.'); },
    });
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  rowActions(_item: ResourceBankGrammarListItemDto): SpAdminRowAction[] {
    return [{ id: 'view', label: 'View', icon: 'view', tone: 'default' }];
  }

  onRowAction(actionId: string, item: ResourceBankGrammarListItemDto): void {
    if (actionId === 'view') this.openDrawer(item);
  }
}

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { AdminLearnItemService } from '../../../core/services/admin-learn-item.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
  RESOURCE_BANK_CEFR_LEVELS,
} from '../../../core/models/admin-resource-import.models';
import {
  SpAdminAlertComponent,
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

/** Phase H3 — UnifiedResourceBankItemType's member names match PublishedResourceType (backend
 *  Domain enum) and LearnItemResourceLinkInput's ResourceType 1:1 — no translation table needed. */
const RESOURCE_TYPE_TO_LEARN_ITEM_TYPE: Record<UnifiedResourceBankItemType, string> = {
  vocabulary: 'Vocabulary',
  grammar: 'Grammar',
  readingReference: 'ReadingReference',
  readingPassage: 'ReadingPassage',
};

const PAGE_SIZE = 20;

/**
 * Phase H1 — unified admin-facing Resource Bank read model. Aggregates the four typed published
 * bank tables (vocabulary/grammar/reading-references/reading-passages) into one filtered view, so
 * an admin sees "one Resource Bank with typed rows" instead of four separate pages. This is Option
 * B from docs/architecture/product-model-realignment-h0.md §4: a read model over the existing
 * typed tables, not a physical unified table. Read-only, same as the typed pages it aggregates —
 * no edit/delete here, all mutation stays on Resource Candidates (E4). The typed pages this
 * replaces as the primary entry point remain reachable and fully functional.
 *
 * Phase H3 — "Generate Learn" is now a real row action (deterministic draft composer, see
 * AdminLearnItemService); Generate Activity / Generate Module stay disabled "Coming soon" —
 * placeholders for Phase H4/H5, which do not exist yet.
 */
@Component({
  selector: 'app-admin-resource-bank-unified',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
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
  templateUrl: './admin-resource-bank-unified.component.html',
})
export class AdminResourceBankUnifiedComponent implements OnInit {
  items = signal<UnifiedResourceBankItemDto[]>([]);
  loading = signal(true);
  error = signal('');

  searchQuery = signal('');
  typeFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  skillFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly typeOptions = [{ value: 'all', label: 'All types' }, ...UNIFIED_RESOURCE_BANK_TYPES];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];
  readonly skillOptions = [
    { value: 'all', label: 'All skills' },
    { value: 'Vocabulary', label: 'Vocabulary' },
    { value: 'Grammar', label: 'Grammar' },
    { value: 'Reading', label: 'Reading' },
  ];

  // ── Detail drawer (uses the already-loaded row — no extra fetch needed) ────
  drawerOpen = signal(false);
  detail = signal<UnifiedResourceBankItemDto | null>(null);

  // ── Phase H3 — Generate Learn ────────────────────────────────────────────
  generatingLearnId = signal<string | null>(null);
  generateSuccess = signal('');
  generateError = signal('');

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private learnItemSvc: AdminLearnItemService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const type = this.typeFilter() === 'all' ? undefined : (this.typeFilter() as UnifiedResourceBankItemType);
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    const skill = this.skillFilter() === 'all' ? undefined : this.skillFilter();
    this.bankSvc
      .list(this.page(), this.pageSize, type, cefrLevel, skill, undefined, undefined, undefined, undefined, this.searchQuery() || undefined)
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load the resource bank.'); },
      });
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onTypeFilterChange(value: string): void {
    this.typeFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onSkillFilterChange(value: string): void {
    this.skillFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openDrawer(item: UnifiedResourceBankItemDto): void {
    this.detail.set(item);
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  typeLabel(type: UnifiedResourceBankItemType): string {
    return UNIFIED_RESOURCE_BANK_TYPES.find(t => t.value === type)?.label ?? type;
  }

  rowActions(item: UnifiedResourceBankItemDto): SpAdminRowAction[] {
    return [
      { id: 'view', label: 'View', icon: 'view', tone: 'default' },
      {
        id: 'generate-learn', label: 'Generate Learn', icon: 'sparkles', tone: 'default', dividerBefore: true,
        disabled: this.generatingLearnId() === item.id,
      },
      { id: 'generate-activity', label: 'Generate Activity (coming soon)', disabled: true },
      { id: 'generate-module', label: 'Generate Module (coming soon)', disabled: true },
    ];
  }

  onRowAction(actionId: string, item: UnifiedResourceBankItemDto): void {
    if (actionId === 'view') this.openDrawer(item);
    if (actionId === 'generate-learn') this.generateLearn(item);
    // generate-activity/generate-module are disabled — no handler needed (H4/H5).
  }

  /** Phase H3 — deterministic draft composer, one resource per call (multi-select is a future
   *  enhancement, not needed for this foundation phase). Always stages a pending-review Learn
   *  Item — never publishes, never assigns anything to a student. */
  generateLearn(item: UnifiedResourceBankItemDto): void {
    this.generatingLearnId.set(item.id);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.learnItemSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LEARN_ITEM_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: result => {
        this.generatingLearnId.set(null);
        this.generateSuccess.set(`Learn Item draft created from "${item.title}" — pending review.`);
        this.loadAll();
      },
      error: err => {
        this.generatingLearnId.set(null);
        this.generateError.set(err.error?.error ?? 'Could not generate a Learn Item.');
      },
    });
  }

  goToLearnItems(): void {
    this.router.navigateByUrl('/admin/learn-items');
  }
}

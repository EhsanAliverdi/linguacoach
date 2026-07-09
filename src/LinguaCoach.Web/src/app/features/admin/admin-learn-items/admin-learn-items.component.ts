import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminLearnItemService } from '../../../core/services/admin-learn-item.service';
import { AdminActivityDefinitionService } from '../../../core/services/admin-activity-definition.service';
import { AdminModuleDefinitionService } from '../../../core/services/admin-module-definition.service';
import {
  LearnItemDto,
  LEARN_ITEM_REVIEW_STATUSES,
} from '../../../core/models/admin-learn-item.models';
import { RESOURCE_BANK_CEFR_LEVELS } from '../../../core/models/admin-resource-import.models';
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
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter(v => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

/**
 * Phase H3 — Learn Item foundation admin page. Reviewable teaching/explanation blocks generated
 * from (or manually authored about) published Resource Bank rows — the "Learn" half of a future
 * Module. Nothing here creates an Activity/Module row or assigns anything to a student.
 */
@Component({
  selector: 'app-admin-learn-items',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-learn-items.component.html',
})
export class AdminLearnItemsComponent implements OnInit {
  items = signal<LearnItemDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  searchQuery = signal('');
  statusFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly statusOptions = [{ value: 'all', label: 'All statuses' }, ...LEARN_ITEM_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...RESOURCE_BANK_CEFR_LEVELS.map(l => ({ value: l, label: l }))];

  // ── Detail drawer ────────────────────────────────────────────────────────
  drawerOpen = signal(false);
  detail = signal<LearnItemDto | null>(null);
  rejectReasonDraft = '';
  approving = signal(false);
  rejecting = signal(false);

  // ── Phase H4 — Generate Activity from this Learn Item ──────────────────
  generatingActivity = signal(false);
  lastActionKind = signal<'activity' | 'module' | null>(null);

  // ── Phase H5 — Generate Module from this Learn Item ─────────────────────
  generatingModule = signal(false);

  constructor(
    private learnItemSvc: AdminLearnItemService,
    private activitySvc: AdminActivityDefinitionService,
    private moduleSvc: AdminModuleDefinitionService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();

    const deepLinkId = this.route.snapshot.queryParamMap.get('id');
    if (deepLinkId) this.openDrawerById(deepLinkId);
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const status = this.statusFilter() === 'all' ? undefined : this.statusFilter();
    const cefrLevel = this.cefrLevelFilter() === 'all' ? undefined : this.cefrLevelFilter();
    this.learnItemSvc
      .list(this.page(), this.pageSize, status, cefrLevel, undefined, undefined, undefined, undefined, undefined, this.searchQuery() || undefined)
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load Learn Items.'); },
      });
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openDrawer(item: LearnItemDto): void {
    this.detail.set(item);
    this.rejectReasonDraft = '';
    this.actionError.set('');
    this.drawerOpen.set(true);
  }

  openDrawerById(id: string): void {
    this.learnItemSvc.get(id).subscribe({
      next: item => this.openDrawer(item),
      error: () => { /* deep-linked item no longer exists — ignore, list still loads */ },
    });
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  examplesFor(item: LearnItemDto): string[] {
    return parseJsonArray(item.examplesJson);
  }

  commonMistakesFor(item: LearnItemDto): string[] {
    return parseJsonArray(item.commonMistakesJson);
  }

  contextTagsFor(item: LearnItemDto): string[] {
    return parseJsonArray(item.contextTagsJson);
  }

  focusTagsFor(item: LearnItemDto): string[] {
    return parseJsonArray(item.focusTagsJson);
  }

  statusTone(status: string): 'success' | 'neutral' | 'danger' | 'warning' {
    if (status === 'Approved') return 'success';
    if (status === 'Rejected') return 'danger';
    if (status === 'PendingReview') return 'warning';
    return 'neutral';
  }

  approveSelected(): void {
    const item = this.detail();
    if (!item) return;
    this.approving.set(true);
    this.actionError.set('');
    this.learnItemSvc.approve(item.id).subscribe({
      next: updated => {
        this.approving.set(false);
        this.lastActionKind.set(null);
        this.detail.set(updated);
        this.actionSuccess.set('Learn Item approved.');
        this.loadAll();
      },
      error: err => { this.approving.set(false); this.actionError.set(err.error?.error ?? 'Could not approve.'); },
    });
  }

  rejectSelected(): void {
    const item = this.detail();
    if (!item) return;
    if (!this.rejectReasonDraft.trim()) {
      this.actionError.set('A rejection reason is required.');
      return;
    }
    this.rejecting.set(true);
    this.actionError.set('');
    this.learnItemSvc.reject(item.id, this.rejectReasonDraft.trim()).subscribe({
      next: updated => {
        this.rejecting.set(false);
        this.lastActionKind.set(null);
        this.detail.set(updated);
        this.actionSuccess.set('Learn Item rejected.');
        this.loadAll();
      },
      error: err => { this.rejecting.set(false); this.actionError.set(err.error?.error ?? 'Could not reject.'); },
    });
  }

  /** Phase H4 — generates an Activity from this Learn Item's own linked resources, using its
   *  own CEFR/skill/subskill/tags as defaults. Always stages a pending-review Activity; the Learn
   *  Item itself is never modified. */
  generateActivity(): void {
    const item = this.detail();
    if (!item) return;
    this.generatingActivity.set(true);
    this.actionError.set('');
    this.activitySvc.generateFromLearnItem({ learnItemId: item.id }).subscribe({
      next: () => {
        this.generatingActivity.set(false);
        this.lastActionKind.set('activity');
        this.actionSuccess.set('Activity draft generated from this Learn Item — pending review.');
      },
      error: err => {
        this.generatingActivity.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate an Activity.');
      },
    });
  }

  goToActivities(): void {
    this.router.navigateByUrl('/admin/activities');
  }

  /** Phase H5 — generates a Module from this (approved) Learn Item's compatible approved
   *  Activity Definition(s). Rejected with a clear message if the Learn Item itself isn't
   *  approved yet, or no compatible approved Activity exists. */
  generateModule(): void {
    const item = this.detail();
    if (!item) return;
    this.generatingModule.set(true);
    this.actionError.set('');
    this.moduleSvc.generateFromLearnItem({ learnItemId: item.id }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastActionKind.set('module');
        this.actionSuccess.set('Module draft generated from this Learn Item — pending review.');
      },
      error: err => {
        this.generatingModule.set(false);
        this.actionError.set(err.error?.error ?? 'Could not generate a Module.');
      },
    });
  }

  goToModules(): void {
    this.router.navigateByUrl('/admin/modules');
  }
}

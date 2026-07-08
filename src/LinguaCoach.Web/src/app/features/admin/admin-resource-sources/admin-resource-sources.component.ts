import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminResourceSourceService } from '../../../core/services/admin-resource-import.service';
import { AdminResourceSourceDto, ResourceSourceRequest } from '../../../core/models/admin-resource-import.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCheckboxComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-admin-resource-sources',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCheckboxComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-resource-sources.component.html',
})
export class AdminResourceSourcesComponent implements OnInit {
  items = signal<AdminResourceSourceDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  approvedFilter = signal<string>('all');
  searchQuery = signal('');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  overallTotalCount = signal(0);
  approvedCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly approvedOptions = [
    { value: 'all', label: 'All sources' },
    { value: 'true', label: 'Approved' },
    { value: 'false', label: 'Not approved' },
  ];

  // ── Create/edit form ─────────────────────────────────────────────────────
  formOpen = signal(false);
  editingId = signal<string | null>(null);
  form: ResourceSourceRequest = this.blankForm();

  // ── Revoke reason prompt ─────────────────────────────────────────────────
  revokeOpen = signal(false);
  revokeTargetId = signal<string | null>(null);
  revokeReason = '';

  constructor(private svc: AdminResourceSourceService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  private blankForm(): ResourceSourceRequest {
    return {
      name: '', licenseType: '', sourceUrl: null, usageRestrictionNotes: null, languageCode: 'en',
      allowsStudentDisplay: false, allowsCommercialUse: false, attributionText: null, sourceVersion: null,
      downloadUrl: null,
    };
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    const approved = this.approvedFilter() === 'all' ? null : this.approvedFilter() === 'true';
    this.svc.list(this.page(), this.pageSize, approved, undefined, this.searchQuery()).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.overallTotalCount.set(result.overallTotalCount);
        this.approvedCount.set(result.approvedCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load resource sources.'); },
    });
  }

  onApprovedFilterChange(value: string): void {
    this.approvedFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  private searchDebounce?: ReturnType<typeof setTimeout>;

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadAll(), 300);
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.form = this.blankForm();
    this.actionError.set('');
    this.formOpen.set(true);
  }

  openEditForm(item: AdminResourceSourceDto): void {
    this.editingId.set(item.sourceId);
    this.form = {
      name: item.name, licenseType: item.licenseType, sourceUrl: item.sourceUrl,
      usageRestrictionNotes: item.usageRestrictionNotes, languageCode: item.languageCode,
      allowsStudentDisplay: item.allowsStudentDisplay, allowsCommercialUse: item.allowsCommercialUse,
      attributionText: item.attributionText, sourceVersion: item.sourceVersion, downloadUrl: item.downloadUrl,
    };
    this.actionError.set('');
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  saveForm(): void {
    if (!this.form.name.trim() || !this.form.licenseType.trim()) return;
    const editingId = this.editingId();
    const request$ = editingId ? this.svc.update(editingId, this.form) : this.svc.add(this.form);
    request$.subscribe({
      next: () => {
        this.formOpen.set(false);
        this.actionSuccess.set(editingId ? 'Source updated.' : 'Source created.');
        this.loadAll();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save source.'),
    });
  }

  approve(item: AdminResourceSourceDto): void {
    this.actionError.set('');
    this.svc.approve(item.sourceId).subscribe({
      next: () => { this.actionSuccess.set(`"${item.name}" approved for import.`); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not approve source.'),
    });
  }

  openRevoke(item: AdminResourceSourceDto): void {
    this.revokeTargetId.set(item.sourceId);
    this.revokeReason = '';
    this.actionError.set('');
    this.revokeOpen.set(true);
  }

  closeRevoke(): void {
    this.revokeOpen.set(false);
  }

  confirmRevoke(): void {
    const id = this.revokeTargetId();
    if (!id || !this.revokeReason.trim()) return;
    this.svc.revoke(id, this.revokeReason.trim()).subscribe({
      next: () => { this.revokeOpen.set(false); this.actionSuccess.set('Import approval revoked.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not revoke approval.'),
    });
  }

  approvedTone(item: AdminResourceSourceDto): 'success' | 'neutral' {
    return item.isImportApproved ? 'success' : 'neutral';
  }

  rowActions(item: AdminResourceSourceDto): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [{ id: 'edit', label: 'Edit', icon: 'edit', tone: 'default' }];
    actions.push(item.isImportApproved
      ? { id: 'revoke', label: 'Revoke approval', icon: 'delete', tone: 'danger' }
      : { id: 'approve', label: 'Approve for import', icon: 'check', tone: 'default' });
    return actions;
  }

  onRowAction(actionId: string, item: AdminResourceSourceDto): void {
    if (actionId === 'edit') this.openEditForm(item);
    if (actionId === 'approve') this.approve(item);
    if (actionId === 'revoke') this.openRevoke(item);
  }
}

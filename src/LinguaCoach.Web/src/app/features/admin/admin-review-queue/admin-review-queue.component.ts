import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminReviewQueueService } from '../../../core/services/admin-review-queue.service';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import { AdminReviewQueueItemDto } from '../../../core/models/admin-review-queue.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableComponent,
  SpAdminTableFooterComponent,
} from '../../../design-system/admin';

const PAGE_SIZE = 20;

/**
 * Phase 9 of the AI bank-first teaching architecture — a single admin surface listing bank
 * content awaiting review, with quick approve/reject actions so an admin doesn't have to visit
 * each entity's own list to triage.
 *
 * Phase I2A (legacy fallback deletion): the legacy ActivityTemplate entity was removed, so this
 * now covers PlacementItemDefinition only. See
 * docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
 */
@Component({
  selector: 'app-admin-review-queue',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
  ],
  templateUrl: './admin-review-queue.component.html',
})
export class AdminReviewQueueComponent implements OnInit {
  items = signal<AdminReviewQueueItemDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  entityTypeFilter = signal<string>('all');
  reviewStatusFilter = signal<string>('PendingReview');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  pendingCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly entityTypeOptions = [
    { value: 'all', label: 'All types' },
    { value: 'PlacementItem', label: 'Placement items' },
  ];

  readonly reviewStatusOptions = [
    { value: 'PendingReview', label: 'Pending review' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' },
    { value: 'NotRequired', label: 'Not required' },
  ];

  constructor(
    private svc: AdminReviewQueueService,
    private placementSvc: AdminPlacementItemService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list(this.page(), this.pageSize, this.entityTypeFilter(), this.reviewStatusFilter()).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.pendingCount.set(result.pendingCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load review queue.'); },
    });
  }

  onEntityTypeFilterChange(value: string): void {
    this.entityTypeFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onReviewStatusFilterChange(value: string): void {
    this.reviewStatusFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.loadAll();
  }

  statusTone(item: AdminReviewQueueItemDto): 'success' | 'neutral' | 'danger' {
    if (item.reviewStatus === 'Approved') return 'success';
    if (item.reviewStatus === 'Rejected') return 'danger';
    return 'neutral';
  }

  viewRoute(item: AdminReviewQueueItemDto): string[] {
    return ['/admin/placement-items', item.entityId];
  }

  approve(item: AdminReviewQueueItemDto): void {
    this.actionError.set('');
    const onSuccess = () => { this.actionSuccess.set(`Approved "${item.displayKey}".`); this.loadAll(); };
    const onError = (err: { error?: { error?: string } }) => this.actionError.set(err.error?.error ?? 'Could not approve item.');

    this.placementSvc.setReviewStatus(item.entityId, { action: 'approve' }).subscribe({ next: onSuccess, error: onError });
  }

  reject(item: AdminReviewQueueItemDto): void {
    const reason = window.prompt(`Reason for rejecting "${item.displayKey}":`);
    if (!reason) return;

    this.actionError.set('');
    const onSuccess = () => { this.actionSuccess.set(`Rejected "${item.displayKey}".`); this.loadAll(); };
    const onError = (err: { error?: { error?: string } }) => this.actionError.set(err.error?.error ?? 'Could not reject item.');

    this.placementSvc.setReviewStatus(item.entityId, { action: 'reject', reason }).subscribe({ next: onSuccess, error: onError });
  }
}

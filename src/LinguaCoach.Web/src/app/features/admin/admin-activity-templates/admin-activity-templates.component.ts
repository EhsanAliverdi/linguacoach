import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminActivityTemplateService } from '../../../core/services/admin-activity-template.service';
import {
  AdminActivityTemplateDto,
  ACTIVITY_TEMPLATE_SKILLS,
  ACTIVITY_TEMPLATE_CEFR_LEVELS,
  ACTIVITY_TEMPLATE_REVIEW_STATUSES,
} from '../../../core/models/admin-activity-template.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
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

@Component({
  selector: 'app-admin-activity-templates',
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
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTableFooterComponent,
  ],
  templateUrl: './admin-activity-templates.component.html',
})
export class AdminActivityTemplatesComponent implements OnInit {
  items = signal<AdminActivityTemplateDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  skillFilter = signal<string>('all');
  cefrLevelFilter = signal<string>('all');
  reviewStatusFilter = signal<string>('all');
  searchQuery = signal('');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  overallTotalCount = signal(0);
  publishedCount = signal(0);
  skillCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly skillOptions = [{ value: 'all', label: 'All skills' }, ...ACTIVITY_TEMPLATE_SKILLS.map(s => ({ value: s, label: s }))];
  readonly cefrLevelOptions = [{ value: 'all', label: 'All levels' }, ...ACTIVITY_TEMPLATE_CEFR_LEVELS.map(l => ({ value: l, label: l }))];
  readonly reviewStatusOptions = [{ value: 'all', label: 'All review statuses' }, ...ACTIVITY_TEMPLATE_REVIEW_STATUSES.map(s => ({ value: s, label: s }))];

  constructor(private svc: AdminActivityTemplateService, private router: Router) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list(
      this.page(), this.pageSize, this.skillFilter(), this.cefrLevelFilter(), this.reviewStatusFilter(), this.searchQuery(),
    ).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.overallTotalCount.set(result.overallTotalCount);
        this.publishedCount.set(result.publishedCount);
        this.skillCount.set(result.skillCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load activity templates.'); },
    });
  }

  onSkillFilterChange(value: string): void {
    this.skillFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onCefrLevelFilterChange(value: string): void {
    this.cefrLevelFilter.set(value);
    this.page.set(1);
    this.loadAll();
  }

  onReviewStatusFilterChange(value: string): void {
    this.reviewStatusFilter.set(value);
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

  removeItem(item: AdminActivityTemplateDto): void {
    this.actionError.set('');
    this.svc.remove(item.templateId).subscribe({
      next: () => { this.actionSuccess.set('Template removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove template.'),
    });
  }

  publishedTone(item: AdminActivityTemplateDto): 'success' | 'neutral' {
    return item.isPublished ? 'success' : 'neutral';
  }

  reviewStatusTone(item: AdminActivityTemplateDto): 'success' | 'neutral' | 'danger' {
    if (item.reviewStatus === 'Approved') return 'success';
    if (item.reviewStatus === 'Rejected') return 'danger';
    return 'neutral';
  }

  rowActions(_item: AdminActivityTemplateDto): SpAdminRowAction[] {
    return [
      { id: 'edit', label: 'Edit', icon: 'edit', tone: 'default' },
      { id: 'remove', label: 'Remove', icon: 'delete', tone: 'danger' },
    ];
  }

  onRowAction(actionId: string, item: AdminActivityTemplateDto): void {
    if (actionId === 'edit') this.router.navigate(['/admin/activity-templates', item.templateId]);
    if (actionId === 'remove') this.removeItem(item);
  }
}

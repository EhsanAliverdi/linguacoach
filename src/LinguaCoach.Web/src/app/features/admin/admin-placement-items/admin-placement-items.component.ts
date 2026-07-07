import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import { AdminPlacementItemDto, PLACEMENT_SKILLS } from '../../../core/models/admin-placement-item.models';
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
  selector: 'app-admin-placement-items',
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
  templateUrl: './admin-placement-items.component.html',
})
export class AdminPlacementItemsComponent implements OnInit {
  items = signal<AdminPlacementItemDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  skillFilter = signal<string>('all');
  searchQuery = signal('');
  page = signal(1);

  readonly pageSize = PAGE_SIZE;
  totalCount = signal(0);
  overallTotalCount = signal(0);
  enabledCount = signal(0);
  skillCount = signal(0);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  readonly skillOptions = [{ value: 'all', label: 'All skills' }, ...PLACEMENT_SKILLS.map(s => ({ value: s, label: s }))];

  readonly itemColumns = [
    { key: 'skill', label: 'Skill' },
    { key: 'cefrLevel', label: 'Level' },
    { key: 'questionPreview', label: 'Question' },
    { key: 'isEnabled', label: 'Enabled' },
    { key: '_actions', label: '' },
  ];

  constructor(private svc: AdminPlacementItemService, private router: Router) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list(this.page(), this.pageSize, this.skillFilter(), this.searchQuery()).subscribe({
      next: result => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.overallTotalCount.set(result.overallTotalCount);
        this.enabledCount.set(result.enabledCount);
        this.skillCount.set(result.skillCount);
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load placement items.'); },
    });
  }

  onSkillFilterChange(value: string): void {
    this.skillFilter.set(value);
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

  removeItem(item: AdminPlacementItemDto): void {
    this.actionError.set('');
    this.svc.remove(item.itemId).subscribe({
      next: () => { this.actionSuccess.set('Item removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove item.'),
    });
  }

  itemTone(item: AdminPlacementItemDto): 'success' | 'neutral' {
    return item.isEnabled ? 'success' : 'neutral';
  }

  rowActions(_item: AdminPlacementItemDto): SpAdminRowAction[] {
    return [
      { id: 'edit', label: 'Edit', icon: 'edit', tone: 'default' },
      { id: 'remove', label: 'Remove', icon: 'delete', tone: 'danger' },
    ];
  }

  onRowAction(actionId: string, item: AdminPlacementItemDto): void {
    if (actionId === 'edit') this.router.navigate(['/admin/placement-items', item.itemId]);
    if (actionId === 'remove') this.removeItem(item);
  }
}

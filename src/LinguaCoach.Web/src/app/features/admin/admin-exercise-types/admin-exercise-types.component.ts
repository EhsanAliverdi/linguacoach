import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, computed, signal } from '@angular/core';
import { AdminService } from '../../../core/services/admin.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCodePillComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-admin-exercise-types',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCodePillComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
  ],
  template: `
    <sp-admin-page-header
      title="Exercise Types"
      subtitle="Control which exercise types are available for SpeakPath lessons and Practice Gym generation." />

    <sp-admin-page-body>
      @if (error()) {
        <sp-admin-error-state title="Exercise types unavailable" [message]="error()!" />
      }

      @if (loading()) {
        <sp-admin-loading-state message="Loading exercise types" />
      } @else if (exerciseTypes().length === 0) {
        <sp-admin-empty-state message="No exercise types found." />
      } @else {

        <sp-admin-filter-bar density="compact">
          <sp-admin-input
            search
            size="sm"
            placeholder="Search by name or key..."
            [value]="searchQuery()"
            (input)="onSearch($event)"
            aria-label="Search exercise types" />
          <sp-admin-select
            filters
            size="sm"
            placeholder="All skills"
            [options]="skillOptions()"
            [(ngModel)]="skillFilterValue"
            (ngModelChange)="onSkillFilterChange($event)"
            aria-label="Filter by skill" />
          <sp-admin-select
            filters
            size="sm"
            placeholder="All statuses"
            [options]="statusOptions"
            [(ngModel)]="statusFilterValue"
            (ngModelChange)="onStatusFilterChange($event)"
            aria-label="Filter by status" />
        </sp-admin-filter-bar>

        @if (filteredExerciseTypes().length === 0) {
          <sp-admin-empty-state message="No exercise types match your filters." />
        } @else {
          <sp-admin-table variant="data" density="compact" minWidth="980px">
            <table>
              <thead>
                <tr>
                  <th>Exercise</th>
                  <th>Skill</th>
                  <th>Status</th>
                  <th>Generation</th>
                  <th>Surfaces</th>
                  <th>Items (min/def/max)</th>
                  <th>Options (min/def/max)</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (type of pagedExerciseTypes(); track type.key) {
                  <tr>
                    <td class="sp-et-name-cell">
                      <div class="sp-et-name">{{ type.displayName }}</div>
                      <sp-admin-code-pill [value]="type.key" tone="neutral" />
                      @if (type.description) {
                        <sp-admin-truncated-text [value]="type.description" [maxLength]="72" class="sp-et-desc" />
                      }
                    </td>
                    <td>
                      <span class="sp-admin-cap">{{ type.primarySkill }}</span>
                      @if (type.secondarySkills.length) {
                        <div class="sp-et-secondary">+{{ type.secondarySkills.join(', ') }}</div>
                      }
                    </td>
                    <td>
                      <div class="sp-et-badges">
                        <sp-admin-badge [tone]="type.implementationStatus === 'ready' ? 'success' : 'warning'">
                          {{ type.implementationStatus === 'ready' ? 'Ready' : 'Not impl.' }}
                        </sp-admin-badge>
                        <sp-admin-badge [tone]="type.isEnabled ? 'success' : 'neutral'">
                          {{ type.isEnabled ? 'Enabled' : 'Disabled' }}
                        </sp-admin-badge>
                      </div>
                    </td>
                    <td>
                      <sp-admin-badge [tone]="type.isAvailableForGeneration ? 'success' : 'danger'">
                        {{ type.isAvailableForGeneration ? 'Available' : 'Blocked' }}
                      </sp-admin-badge>
                    </td>
                    <td class="sp-et-surfaces">
                      <span [class.sp-et-yes]="type.supportsPracticeGym" [class.sp-et-no]="!type.supportsPracticeGym">Gym</span>
                      <span [class.sp-et-yes]="type.supportsTodayLesson" [class.sp-et-no]="!type.supportsTodayLesson">Lesson</span>
                      @if (type.requiresAudio) { <span class="sp-et-tag">Audio</span> }
                      @if (type.requiresImage) { <span class="sp-et-tag">Image</span> }
                    </td>
                    <td class="sp-et-counts">
                      <input type="number" min="0" [(ngModel)]="type.minItemsPerPractice" aria-label="min items" />
                      <input type="number" min="0" [(ngModel)]="type.defaultItemsPerPractice" aria-label="default items" />
                      <input type="number" min="0" [(ngModel)]="type.maxItemsPerPractice" aria-label="max items" />
                    </td>
                    <td class="sp-et-counts">
                      <input type="number" min="0" [(ngModel)]="type.minOptionsPerItem" aria-label="min options" />
                      <input type="number" min="0" [(ngModel)]="type.defaultOptionsPerItem" aria-label="default options" />
                      <input type="number" min="0" [(ngModel)]="type.maxOptionsPerItem" aria-label="max options" />
                      @if (countError(type)) {
                        <div class="sp-et-count-error">{{ countError(type) }}</div>
                      }
                    </td>
                    <td class="sp-et-action-cell">
                      <sp-admin-table-actions
                        [actions]="rowActions(type)"
                        (actionClick)="onRowAction($event, type)" />
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
      }
    </sp-admin-page-body>
  `,
  styles: [`
    .sp-et-name-cell { min-width: 200px; max-width: 280px; }
    .sp-et-name { font-weight: 600; font-size: 13px; margin-bottom: 2px; }
    .sp-et-desc { display: block; margin-top: 2px; font-size: 11px; color: var(--sp-admin-muted, #6b7280); }
    .sp-et-secondary { font-size: 11px; color: var(--sp-admin-muted, #6b7280); margin-top: 2px; }
    .sp-et-badges { display: flex; flex-direction: column; gap: 3px; }
    .sp-et-surfaces { white-space: nowrap; font-size: 11px; display: flex; flex-wrap: wrap; gap: 4px; align-items: center; }
    .sp-et-yes { color: #16a34a; font-weight: 600; }
    .sp-et-no  { color: #9ca3af; }
    .sp-et-tag { background: #f3f4f6; color: #374151; border-radius: 4px; padding: 1px 5px; font-size: 10px; font-weight: 500; }
    .sp-et-counts { white-space: nowrap; min-width: 140px; }
    .sp-et-counts input {
      width: 44px;
      margin-right: 3px;
      border: 1px solid var(--sp-admin-border, #e5e7eb);
      border-radius: 4px;
      padding: 4px 6px;
      font: inherit;
      font-size: 12px;
      text-align: center;
    }
    .sp-et-count-error { color: var(--sp-admin-danger, #dc2626); font-size: 11px; margin-top: 4px; }
    .sp-et-action-cell { width: 40px; text-align: center; }
    .sp-admin-cap { text-transform: capitalize; }
  `],
})
export class AdminExerciseTypesComponent implements OnInit {
  exerciseTypes = signal<ExerciseTypeDefinition[]>([]);
  savingKey = signal<string | null>(null);
  error = signal<string | null>(null);
  loading = signal(true);
  page = signal(1);
  searchQuery = signal('');
  skillFilter = signal('');
  statusFilter = signal('');
  skillFilterValue = '';
  statusFilterValue = '';
  readonly pageSize = 20;

  readonly statusOptions = [
    { value: 'enabled', label: 'Enabled' },
    { value: 'disabled', label: 'Disabled' },
    { value: 'ready', label: 'Ready' },
    { value: 'not_implemented', label: 'Not implemented' },
  ];

  skillOptions = computed(() => {
    const skills = new Set<string>();
    for (const t of this.exerciseTypes()) {
      if (t.primarySkill) skills.add(t.primarySkill);
    }
    return Array.from(skills).sort().map(s => ({ value: s, label: s.charAt(0).toUpperCase() + s.slice(1) }));
  });

  filteredExerciseTypes = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const skill = this.skillFilter();
    const status = this.statusFilter();
    return this.exerciseTypes().filter(t => {
      if (q && !t.displayName.toLowerCase().includes(q) && !t.key.toLowerCase().includes(q)) return false;
      if (skill && t.primarySkill !== skill) return false;
      if (status === 'enabled' && !t.isEnabled) return false;
      if (status === 'disabled' && t.isEnabled) return false;
      if (status === 'ready' && t.implementationStatus !== 'ready') return false;
      if (status === 'not_implemented' && t.implementationStatus === 'ready') return false;
      return true;
    });
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filteredExerciseTypes().length / this.pageSize)));
  pagedExerciseTypes = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredExerciseTypes().slice(start, start + this.pageSize);
  });

  constructor(private admin: AdminService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.admin.listExerciseTypes().subscribe({
      next: items => { this.exerciseTypes.set(items); this.page.set(1); this.loading.set(false); },
      error: () => { this.error.set('Could not load exercise types.'); this.loading.set(false); },
    });
  }

  onSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  onSkillFilterChange(value: string): void {
    this.skillFilter.set(value);
    this.page.set(1);
  }

  onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.page.set(1);
  }

  rowActions(type: ExerciseTypeDefinition) {
    const saving = this.savingKey() === type.key;
    return [
      { label: type.isEnabled ? 'Disable' : 'Enable', disabled: saving },
      { label: 'Save counts', disabled: saving || !!this.countError(type) },
    ];
  }

  onRowAction(action: { label: string }, type: ExerciseTypeDefinition): void {
    if (action.label === 'Save counts') {
      this.saveCounts(type);
    } else {
      this.toggle(type);
    }
  }

  countError(type: ExerciseTypeDefinition): string | null {
    const vals = [
      type.minItemsPerPractice, type.defaultItemsPerPractice, type.maxItemsPerPractice,
      type.minOptionsPerItem, type.defaultOptionsPerItem, type.maxOptionsPerItem,
    ];
    if (vals.some(v => v == null || v < 0)) return 'No negative values.';
    if (!(type.minItemsPerPractice <= type.defaultItemsPerPractice && type.defaultItemsPerPractice <= type.maxItemsPerPractice)) {
      return 'Items: min ≤ default ≤ max.';
    }
    if (!(type.minOptionsPerItem <= type.defaultOptionsPerItem && type.defaultOptionsPerItem <= type.maxOptionsPerItem)) {
      return 'Options: min ≤ default ≤ max.';
    }
    return null;
  }

  saveCounts(type: ExerciseTypeDefinition): void {
    if (this.countError(type)) return;
    this.savingKey.set(type.key);
    this.admin.updateExerciseType(type.key, {
      minItemsPerPractice: type.minItemsPerPractice,
      defaultItemsPerPractice: type.defaultItemsPerPractice,
      maxItemsPerPractice: type.maxItemsPerPractice,
      minOptionsPerItem: type.minOptionsPerItem,
      defaultOptionsPerItem: type.defaultOptionsPerItem,
      maxOptionsPerItem: type.maxOptionsPerItem,
    }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(item => item.key === updated.key ? updated : item));
        this.savingKey.set(null);
      },
      error: () => { this.error.set('Could not update exercise type counts.'); this.savingKey.set(null); },
    });
  }

  toggle(type: ExerciseTypeDefinition): void {
    this.savingKey.set(type.key);
    this.admin.updateExerciseType(type.key, { isEnabled: !type.isEnabled }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(item => item.key === updated.key ? updated : item));
        this.savingKey.set(null);
      },
      error: () => { this.error.set('Could not update exercise type.'); this.savingKey.set(null); },
    });
  }
}

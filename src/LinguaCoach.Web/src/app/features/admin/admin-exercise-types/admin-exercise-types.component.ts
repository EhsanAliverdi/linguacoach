import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, computed, signal } from '@angular/core';
import { AdminService } from '../../../core/services/admin.service';
import { ExerciseTypeDefinition } from '../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-exercise-types',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminTableComponent,
  ],
  template: `
    <sp-admin-page-header
      title="Exercise Types"
      subtitle="Control which exercise types can be used for future SpeakPath lessons and Practice Gym generation." />

      @if (error()) {
        <sp-admin-error-state title="Exercise types unavailable" [message]="error()!" />
      }

      @if (loading()) {
        <sp-admin-loading-state message="Loading exercise types" />
      } @else if (exerciseTypes().length === 0) {
        <sp-admin-empty-state message="No exercise types found." />
      } @else {
      <sp-admin-table variant="data" density="compact" minWidth="1240px">
        <table>
          <thead>
            <tr>
              <th>Exercise</th>
              <th>Skill</th>
              <th>Category</th>
              <th>Status</th>
              <th>Surfaces</th>
              <th>Needs</th>
              <th>Generation</th>
              <th>Item counts</th>
              <th>Option counts</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            @for (type of pagedExerciseTypes(); track type.key) {
              <tr>
                <td class="sp-admin-wide-cell sp-admin-table-wrap">
                  <strong>{{ type.displayName }}</strong>
                  <div class="sp-admin-mono">{{ type.key }}</div>
                  <div class="sp-admin-muted">{{ type.description }}</div>
                </td>
                <td class="sp-admin-table-truncate">
                  <span class="sp-admin-cap">{{ type.primarySkill }}</span>
                  @if (type.secondarySkills.length) {
                    <div class="sp-admin-muted">+ {{ type.secondarySkills.join(', ') }}</div>
                  }
                </td>
                <td class="sp-admin-table-truncate">{{ type.category }}</td>
                <td>
                  <sp-admin-badge [tone]="type.implementationStatus === 'ready' ? 'success' : 'warning'">
                    {{ type.implementationStatus === 'ready' ? 'Ready' : 'Not implemented' }}
                  </sp-admin-badge>
                  <div class="sp-admin-muted">{{ type.isEnabled ? 'Enabled' : 'Disabled' }}</div>
                </td>
                <td class="sp-admin-muted">
                  <div>Practice: {{ type.supportsPracticeGym ? 'yes' : 'no' }}</div>
                  <div>Today: {{ type.supportsTodayLesson ? 'yes' : 'no' }}</div>
                </td>
                <td class="sp-admin-muted">
                  <div>Audio: {{ type.requiresAudio ? 'yes' : 'no' }}</div>
                  <div>Image: {{ type.requiresImage ? 'yes' : 'no' }}</div>
                </td>
                <td>
                  <sp-admin-badge [tone]="type.isAvailableForGeneration ? 'success' : 'danger'">
                    {{ type.isAvailableForGeneration ? 'Available' : 'Blocked' }}
                  </sp-admin-badge>
                </td>
                <td class="sp-admin-counts">
                  <input type="number" min="0" [(ngModel)]="type.minItemsPerPractice" aria-label="min items" />
                  <input type="number" min="0" [(ngModel)]="type.defaultItemsPerPractice" aria-label="default items" />
                  <input type="number" min="0" [(ngModel)]="type.maxItemsPerPractice" aria-label="max items" />
                </td>
                <td class="sp-admin-counts">
                  <input type="number" min="0" [(ngModel)]="type.minOptionsPerItem" aria-label="min options" />
                  <input type="number" min="0" [(ngModel)]="type.defaultOptionsPerItem" aria-label="default options" />
                  <input type="number" min="0" [(ngModel)]="type.maxOptionsPerItem" aria-label="max options" />
                  @if (countError(type)) {
                    <div class="sp-admin-count-error">{{ countError(type) }}</div>
                  }
                </td>
                <td class="sp-admin-actions">
                  <sp-admin-button variant="ghost" size="sm" type="button" (click)="toggle(type)" [disabled]="savingKey() === type.key">
                    {{ type.isEnabled ? 'Disable' : 'Enable' }}
                  </sp-admin-button>
                  <sp-admin-button variant="ghost" size="sm" type="button" (click)="saveCounts(type)" [disabled]="savingKey() === type.key || !!countError(type)">
                    Save counts
                  </sp-admin-button>
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
  `,
  styles: [`
    .sp-admin-wide-cell { min-width: 240px; }
    .sp-admin-counts {
      white-space: nowrap;
      min-width: 156px;
    }
    .sp-admin-counts input {
      width: 48px;
      margin-right: 4px;
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-sm);
      padding: 6px;
      font: inherit;
      font-size: 12px;
    }
    .sp-admin-count-error {
      color: var(--sp-admin-danger);
      font-size: 11px;
      margin-top: 4px;
    }
  `],
})
export class AdminExerciseTypesComponent implements OnInit {
  exerciseTypes = signal<ExerciseTypeDefinition[]>([]);
  savingKey = signal<string | null>(null);
  error = signal<string | null>(null);
  loading = signal(true);
  page = signal(1);
  readonly pageSize = 20;

  totalPages = computed(() => Math.max(1, Math.ceil(this.exerciseTypes().length / this.pageSize)));
  pagedExerciseTypes = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.exerciseTypes().slice(start, start + this.pageSize);
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

  countError(type: ExerciseTypeDefinition): string | null {
    const vals = [
      type.minItemsPerPractice, type.defaultItemsPerPractice, type.maxItemsPerPractice,
      type.minOptionsPerItem, type.defaultOptionsPerItem, type.maxOptionsPerItem,
    ];
    if (vals.some(v => v == null || v < 0)) {
      return 'No negative values.';
    }
    if (!(type.minItemsPerPractice <= type.defaultItemsPerPractice && type.defaultItemsPerPractice <= type.maxItemsPerPractice)) {
      return 'Items: min <= default <= max.';
    }
    if (!(type.minOptionsPerItem <= type.defaultOptionsPerItem && type.defaultOptionsPerItem <= type.maxOptionsPerItem)) {
      return 'Options: min <= default <= max.';
    }
    return null;
  }

  saveCounts(type: ExerciseTypeDefinition): void {
    if (this.countError(type)) {
      return;
    }
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
      error: () => {
        this.error.set('Could not update exercise type counts.');
        this.savingKey.set(null);
      },
    });
  }

  toggle(type: ExerciseTypeDefinition): void {
    this.savingKey.set(type.key);
    this.admin.updateExerciseType(type.key, { isEnabled: !type.isEnabled }).subscribe({
      next: updated => {
        this.exerciseTypes.update(items => items.map(item => item.key === updated.key ? updated : item));
        this.savingKey.set(null);
      },
      error: () => {
        this.error.set('Could not update exercise type.');
        this.savingKey.set(null);
      },
    });
  }
}

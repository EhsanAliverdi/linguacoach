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
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';

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
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
    SpAdminRingMetricComponent,
    SpAdminBreakdownBarsComponent,
  ],
  template: `
    <sp-admin-page-header
      title="Exercise Types"
      subtitle="Control which exercise types are available for SpeakPath lessons and Practice Gym generation." />

    <!-- ── Exercise type KPI strip ── -->
    @if (typeSummary().total > 0) {
      <div class="sp-et-kpi-strip" aria-label="Exercise types summary">
        <sp-admin-kpi-card label="Total types" variant="indigo">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>
          {{ typeSummary().total }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Enabled" [variant]="typeSummary().enabled > 0 ? 'green' : 'slate'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
          {{ typeSummary().enabled }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Ready" [variant]="typeSummary().ready > 0 ? 'teal' : 'amber'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
          {{ typeSummary().ready }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Skills covered" variant="violet">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="20" x2="12" y2="10"/><line x1="18" y1="20" x2="18" y2="4"/><line x1="6" y1="20" x2="6" y2="16"/></svg>
          {{ typeSummary().skills }}
        </sp-admin-kpi-card>
      </div>

      <!-- Ready ring + skill breakdown strip -->
      @if (typeSummary().total > 0) {
        <div style="display:flex;align-items:flex-start;gap:24px;padding:12px 24px 4px;flex-wrap:wrap;">
          <sp-admin-ring-metric
            [pct]="readyRingPct()"
            label="Ready"
            [sub]="typeSummary().ready + ' of ' + typeSummary().total"
            tone="teal"
            [size]="72"
            ariaLabel="Ready exercise types ring" />
          <div style="flex:1;min-width:240px;">
            <sp-admin-breakdown-bars [items]="skillBreakdownItems()" [showPct]="true" title="By skill" />
          </div>
        </div>
      }
    }

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
                      <div class="sp-et-name-row">
                        <div class="sp-et-icon-tile" [style.background]="typeIconBg(type.primarySkill)">
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2" [innerHTML]="typeIconPath(type.primarySkill)"></svg>
                        </div>
                        <div class="sp-et-name-body">
                          <div class="sp-et-name">{{ type.displayName }}</div>
                          <sp-admin-code-pill [value]="type.key" tone="neutral" />
                          @if (type.description) {
                            <sp-admin-truncated-text [value]="type.description" [maxLength]="72" class="sp-et-desc" />
                          }
                          @if (type.implementationStatus !== 'ready') {
                            <div class="sp-et-not-runnable">Not runnable yet — foundation only</div>
                          }
                        </div>
                      </div>
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
    .sp-et-kpi-strip {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      padding: 16px 24px 0;
    }
    @media (max-width: 800px) {
      .sp-et-kpi-strip { grid-template-columns: repeat(2, 1fr); }
    }
    .sp-et-name-cell { min-width: 220px; max-width: 300px; }
    .sp-et-name-row { display: flex; gap: 10px; align-items: flex-start; }
    .sp-et-icon-tile {
      flex-shrink: 0;
      width: 34px; height: 34px;
      border-radius: 8px;
      display: flex; align-items: center; justify-content: center;
    }
    .sp-et-name-body { min-width: 0; }
    .sp-et-name { font-weight: 600; font-size: 13px; margin-bottom: 2px; }
    .sp-et-desc { display: block; margin-top: 2px; font-size: 11px; color: var(--sp-admin-muted, #6b7280); }
    .sp-et-not-runnable { font-size: 10px; color: #b45309; background: #fffbeb; border-radius: 4px; padding: 1px 6px; margin-top: 3px; display: inline-block; }
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

  readonly typeSummary = computed(() => {
    const all = this.exerciseTypes();
    const skills = new Set(all.map(t => t.primarySkill).filter(Boolean)).size;
    return {
      total: all.length,
      enabled: all.filter(t => t.isEnabled).length,
      ready: all.filter(t => t.implementationStatus === 'ready').length,
      skills,
    };
  });

  readonly readyRingPct = computed(() => {
    const { total, ready } = this.typeSummary();
    return total > 0 ? Math.round((ready / total) * 100) : 0;
  });

  readonly skillBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const all = this.exerciseTypes();
    const counts: Record<string, number> = {};
    for (const t of all) if (t.primarySkill) counts[t.primarySkill] = (counts[t.primarySkill] ?? 0) + 1;
    const total = all.length || 1;
    const tones: BreakdownBarItem['tone'][] = ['amber', 'indigo', 'indigo', 'violet', 'amber', 'green'];
    return Object.entries(counts).map(([label, value], i) => ({
      label, value, pct: Math.round((value / total) * 100), tone: tones[i % tones.length],
    }));
  });

  private static readonly SKILL_COLORS: Record<string, string> = {
    speaking:   '#f97316',
    writing:    '#5B4BE8',
    reading:    '#2563EB',
    listening:  '#7C3AED',
    vocabulary: '#d97706',
    grammar:    '#16a34a',
  };

  private static readonly SKILL_ICONS: Record<string, string> = {
    speaking:   '<circle cx="12" cy="12" r="10"/><path d="M8.56 2.75c4.37 6.03 6.02 9.42 8.03 17.72m2.54-15.38c-3.72 4.35-8.94 5.66-16.88 5.85m19.5 1.9c-3.62 4.41-7.87 6.91-16.5 6.5"/>',
    writing:    '<path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/>',
    reading:    '<path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/>',
    listening:  '<path d="M3 18v-6a9 9 0 0 1 18 0v6"/><path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z"/>',
    vocabulary: '<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>',
    grammar:    '<polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/>',
  };

  typeIconBg(skill: string): string {
    return AdminExerciseTypesComponent.SKILL_COLORS[skill] ?? '#64748b';
  }

  typeIconPath(skill: string): string {
    return AdminExerciseTypesComponent.SKILL_ICONS[skill]
      ?? '<circle cx="12" cy="12" r="10"/>';
  }

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

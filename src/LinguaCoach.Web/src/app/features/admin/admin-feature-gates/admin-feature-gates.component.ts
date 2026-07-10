import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminBadgeTone,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCodePillComponent,
  SpAdminDrawerComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSelectOption,
  SpAdminToggleComponent,
} from '../../../design-system/admin';
import { SpAdminTextareaComponent } from '../../../design-system/admin/components/textarea/sp-admin-textarea.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  FeatureGateGroup,
  FeatureGateSettingValue,
  FeatureGateCategory,
  FeatureGateRiskLevel,
} from '../../../core/models/admin.models';

type StatusFilter = 'all' | 'editable' | 'locked' | 'overridden';
type EditValue = boolean | number | string | string[];

// Phase I2C: 'reviewScaffoldPracticeGymPilot' removed — the readiness-pool feature gate groups
// on that category were deleted. See docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
const CATEGORY_LABELS: Record<FeatureGateCategory, string> = {
  readinessPoolLessonGeneration: 'Readiness Pool / Lesson Generation',
  aiSignalSafety: 'AI Signal Safety',
};

const RISK_TONE: Record<FeatureGateRiskLevel, SpAdminBadgeTone> = {
  low: 'neutral',
  medium: 'info',
  high: 'warning',
  critical: 'danger',
};

@Component({
  selector: 'app-admin-feature-gates',
  standalone: true,
  templateUrl: './admin-feature-gates.component.html',
  styles: [`
    .fg-filters { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr; gap: 12px; }
    .fg-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .fg-card-desc { font-size: 12.5px; color: var(--sp-admin-text-muted, #8B85A0); margin: 0 0 10px; }
    .fg-card-badges { display: flex; flex-wrap: wrap; gap: 6px; }
    .fg-drawer-desc { font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0); margin: 0 0 10px; }
    .fg-drawer-badges { margin-bottom: 14px; }
    .fg-deps { font-size: 12.5px; margin: 14px 0; }
    .fg-deps ul { margin: 6px 0 0; padding-left: 18px; }
    .fg-settings { display: flex; flex-direction: column; gap: 18px; margin: 14px 0; }
    .fg-setting-row { border-top: 1px solid var(--sp-admin-border, #ECE9F5); padding-top: 14px; }
    .fg-setting-header { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 4px; }
    .fg-setting-name { font-weight: 700; font-size: 13px; }
    .fg-setting-desc { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); margin: 0 0 6px; }
    .fg-setting-meta { font-size: 11.5px; color: var(--sp-admin-text-muted, #8B85A0); margin: 0 0 8px; }
    .fg-source-checks { display: flex; flex-wrap: wrap; gap: 12px; }
    .fg-audit-line { font-size: 11.5px; color: var(--sp-admin-text-muted, #8B85A0); }
    .fg-drawer-actions { display: flex; gap: 10px; margin-top: 16px; }
  `],
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCodePillComponent,
    SpAdminDrawerComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
    SpAdminToggleComponent,
  ],
})
export class AdminFeatureGatesComponent implements OnInit {
  constructor(
    private adminApi: AdminApiService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  loading = signal(true);
  error = signal('');
  groups = signal<FeatureGateGroup[]>([]);

  searchText = signal('');
  categoryFilter = signal<string>('all');
  riskFilter = signal<string>('all');
  statusFilter = signal<StatusFilter>('all');

  categoryOptions: SpAdminSelectOption[] = [
    { value: 'all', label: 'All categories' },
    { value: 'readinessPoolLessonGeneration', label: CATEGORY_LABELS.readinessPoolLessonGeneration },
    { value: 'aiSignalSafety', label: CATEGORY_LABELS.aiSignalSafety },
  ];

  riskOptions: SpAdminSelectOption[] = [
    { value: 'all', label: 'All risk levels' },
    { value: 'low', label: 'Low' },
    { value: 'medium', label: 'Medium' },
    { value: 'high', label: 'High' },
    { value: 'critical', label: 'Critical' },
  ];

  statusOptions: SpAdminSelectOption[] = [
    { value: 'all', label: 'All statuses' },
    { value: 'editable', label: 'Editable' },
    { value: 'locked', label: 'Locked' },
    { value: 'overridden', label: 'Overridden' },
  ];

  filteredGroups = computed(() => {
    const search = this.searchText().trim().toLowerCase();
    const category = this.categoryFilter();
    const risk = this.riskFilter();
    const status = this.statusFilter();

    return this.groups().filter((g) => {
      if (category !== 'all' && g.category !== category) return false;
      if (risk !== 'all' && this.groupRisk(g) !== risk) return false;
      if (status === 'locked' && !g.isReadOnly) return false;
      if (status === 'editable' && g.isReadOnly) return false;
      if (status === 'overridden' && !g.hasActiveOverride) return false;
      if (search) {
        const haystack = [g.displayName, g.groupKey, ...g.settings.map((s) => s.key)].join(' ').toLowerCase();
        if (!haystack.includes(search)) return false;
      }
      return true;
    });
  });

  // ── drawer state ──────────────────────────────────────────────────────────

  drawerOpen = signal(false);
  selectedGroup = signal<FeatureGateGroup | null>(null);
  editValues = signal<Record<string, EditValue>>({});
  reason = signal('');
  confirmationText = signal('');
  saving = signal(false);
  saveError = signal('');
  resetting = signal(false);

  needsConfirmation = computed(() => {
    const group = this.selectedGroup();
    if (!group) return false;
    return group.settings.some((s) => s.requiresConfirmation && s.isEditableAtRuntime);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.getFeatureGates().subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.loading.set(false);

        const gateParam = this.route.snapshot.queryParamMap.get('gate');
        if (gateParam) {
          const match = groups.find((g) => g.groupKey === gateParam);
          if (match) this.openDrawer(match);
        }
      },
      error: () => {
        this.error.set('Could not load feature gates. Please try again.');
        this.loading.set(false);
      },
    });
  }

  categoryLabel(category: FeatureGateCategory): string {
    return CATEGORY_LABELS[category] ?? category;
  }

  groupRisk(group: FeatureGateGroup): FeatureGateRiskLevel {
    const order: FeatureGateRiskLevel[] = ['low', 'medium', 'high', 'critical'];
    return group.settings.reduce<FeatureGateRiskLevel>((max, s) => {
      return order.indexOf(s.riskLevel) > order.indexOf(max) ? s.riskLevel : max;
    }, 'low');
  }

  riskTone(risk: FeatureGateRiskLevel): SpAdminBadgeTone {
    return RISK_TONE[risk];
  }

  allowedValueOptions(setting: FeatureGateSettingValue): SpAdminSelectOption[] {
    return (setting.allowedValues ?? []).map((v) => ({ value: v, label: v }));
  }

  statusLabel(group: FeatureGateGroup): string {
    if (group.isReadOnly) return 'Locked';
    if (group.hasActiveOverride) return 'Overridden';
    return 'Default';
  }

  statusTone(group: FeatureGateGroup): SpAdminBadgeTone {
    if (group.isReadOnly) return 'neutral';
    if (group.hasActiveOverride) return 'success';
    return 'info';
  }

  parseValue(setting: FeatureGateSettingValue): EditValue {
    try {
      return JSON.parse(setting.effectiveValueJson);
    } catch {
      return setting.effectiveValueJson as EditValue;
    }
  }

  openDrawer(group: FeatureGateGroup): void {
    this.selectedGroup.set(group);
    const values: Record<string, EditValue> = {};
    for (const s of group.settings) values[s.key] = this.parseValue(s);
    this.editValues.set(values);
    this.reason.set('');
    this.confirmationText.set('');
    this.saveError.set('');
    this.drawerOpen.set(true);

    if (this.route.snapshot.queryParamMap.get('gate') !== group.groupKey) {
      this.router.navigate([], { relativeTo: this.route, queryParams: { gate: group.groupKey }, queryParamsHandling: 'merge' });
    }
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
    this.selectedGroup.set(null);
    this.router.navigate([], { relativeTo: this.route, queryParams: { gate: null }, queryParamsHandling: 'merge' });
  }

  setValue(key: string, value: EditValue): void {
    this.editValues.update((v) => ({ ...v, [key]: value }));
  }

  isSourceSelected(setting: FeatureGateSettingValue, option: string): boolean {
    const value = this.editValues()[setting.key];
    return Array.isArray(value) && value.includes(option);
  }

  toggleSourceOption(setting: FeatureGateSettingValue, option: string, checked: boolean): void {
    const current = this.editValues()[setting.key];
    const arr = Array.isArray(current) ? [...current] : [];
    const next = checked ? Array.from(new Set([...arr, option])) : arr.filter((v) => v !== option);
    this.setValue(setting.key, next);
  }

  save(): void {
    const group = this.selectedGroup();
    if (!group) return;

    if (!this.reason().trim()) {
      this.saveError.set('A reason is required to change a setting.');
      return;
    }

    this.saving.set(true);
    this.saveError.set('');

    const values: Record<string, unknown> = {};
    for (const s of group.settings) {
      if (s.isEditableAtRuntime) values[s.key] = this.editValues()[s.key];
    }

    this.adminApi
      .updateFeatureGate(group.groupKey, {
        values,
        reason: this.reason().trim(),
        confirmationText: this.needsConfirmation() ? this.confirmationText().trim() : null,
      })
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.replaceGroup(updated);
          this.selectedGroup.set(updated);
          this.reason.set('');
          this.confirmationText.set('');
        },
        error: (err) => {
          this.saving.set(false);
          this.saveError.set(err?.error?.error ?? 'Could not save this setting. Please check the values and try again.');
        },
      });
  }

  resetToDefault(): void {
    const group = this.selectedGroup();
    if (!group) return;

    if (!this.reason().trim()) {
      this.saveError.set('A reason is required to reset a setting.');
      return;
    }

    this.resetting.set(true);
    this.saveError.set('');

    this.adminApi.resetFeatureGateOverride(group.groupKey, { reason: this.reason().trim() }).subscribe({
      next: (updated) => {
        this.resetting.set(false);
        this.replaceGroup(updated);
        this.selectedGroup.set(updated);
        const values: Record<string, EditValue> = {};
        for (const s of updated.settings) values[s.key] = this.parseValue(s);
        this.editValues.set(values);
        this.reason.set('');
      },
      error: (err) => {
        this.resetting.set(false);
        this.saveError.set(err?.error?.error ?? 'Could not reset this setting.');
      },
    });
  }

  private replaceGroup(updated: FeatureGateGroup): void {
    this.groups.update((groups) => groups.map((g) => (g.groupKey === updated.groupKey ? updated : g)));
  }
}

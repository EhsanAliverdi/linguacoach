import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, signal, computed } from '@angular/core';
import {
  CurriculumService,
  AdminCurriculumObjectiveDto,
  CurriculumTaxonomyDto,
  AdminCurriculumObjectiveUpsertRequest,
  AdminRoutingPreviewRequest,
  AdminRoutingPreviewResult,
} from '../../../core/services/curriculum.service';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminTableComponent,
} from '../../../design-system/admin';
import type { SpAdminSelectOption } from '../../../design-system/admin';
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';

type View = 'list' | 'edit' | 'create' | 'preview';

function parseJsonArray(json: string | null | undefined): string[] {
  if (!json || json === '[]') return [];
  try { return JSON.parse(json); } catch { return []; }
}

@Component({
  selector: 'app-admin-curriculum',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
    SpAdminRingMetricComponent,
    SpAdminBreakdownBarsComponent,
  ],
  styles: [`
    .sp-curr-kpi-strip {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      padding: 16px 24px 0;
    }
    @media (max-width: 800px) {
      .sp-curr-kpi-strip { grid-template-columns: repeat(2, 1fr); }
    }
  `],
  template: `
    <sp-admin-page-header
      title="Curriculum Objectives"
      subtitle="Manage the curriculum syllabus used for CEFR-aware activity routing.">
      <sp-admin-button type="button" (click)="startCreate()">New objective</sp-admin-button>
      <sp-admin-button variant="secondary" type="button" (click)="view.set('preview')">Routing preview</sp-admin-button>
    </sp-admin-page-header>

    <!-- ── Coverage summary strip ── -->
    @if (coverageSummary().total > 0) {
      <div class="sp-curr-kpi-strip" aria-label="Curriculum coverage summary">
        <sp-admin-kpi-card label="Total objectives" variant="indigo">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>
          {{ coverageSummary().total }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Active" [variant]="coverageSummary().active > 0 ? 'green' : 'slate'">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
          {{ coverageSummary().active }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="CEFR bands" variant="violet">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="20" x2="12" y2="10"/><line x1="18" y1="20" x2="18" y2="4"/><line x1="6" y1="20" x2="6" y2="16"/></svg>
          {{ coverageSummary().cefrBands }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Skills covered" variant="amber">
          <svg slot="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/></svg>
          {{ coverageSummary().skills }}
        </sp-admin-kpi-card>
      </div>

      <!-- Active/total ring + CEFR breakdown strip -->
      @if (coverageSummary().total > 0) {
        <div style="display:flex;align-items:flex-start;gap:24px;padding:12px 24px 4px;flex-wrap:wrap;">
          <sp-admin-ring-metric
            [pct]="activeRingPct()"
            label="Active"
            [sub]="coverageSummary().active + ' of ' + coverageSummary().total"
            tone="green"
            [size]="72"
            ariaLabel="Active objectives ring" />
          <div style="flex:1;min-width:240px;">
            <sp-admin-breakdown-bars [items]="cefrBreakdownItems()" [showPct]="true" title="CEFR distribution" />
          </div>
        </div>
      }
    }

    <sp-admin-page-body>

      @if (globalError()) {
        <sp-admin-error-state title="Curriculum unavailable" [message]="globalError()!" />
      }

      <!-- ── List ── -->
      @if (view() === 'list') {
        <sp-admin-filter-bar>
          <sp-admin-select
            [options]="cefrOptions()"
            placeholder="All levels"
            size="sm"
            [fullWidth]="false"
            [(ngModel)]="filterCefr"
            (ngModelChange)="load()" />
          <sp-admin-select
            [options]="skillOptions()"
            placeholder="All skills"
            size="sm"
            [fullWidth]="false"
            [(ngModel)]="filterSkill"
            (ngModelChange)="load()" />
          <sp-admin-select
            [options]="activeOptions"
            placeholder="Active + inactive"
            size="sm"
            [fullWidth]="false"
            [(ngModel)]="filterActive"
            (ngModelChange)="load()" />
        </sp-admin-filter-bar>

        <sp-admin-table>
          @if (loading()) {
            <sp-admin-loading-state message="Loading objectives" />
          } @else if (objectives().length === 0) {
            <p style="padding:16px;color:#64748b">No objectives found.</p>
          } @else {
            <table>
              <thead>
                <tr style="text-align:left;color:#64748b;border-bottom:1px solid #e2e8f0">
                  <th style="padding:12px">Objective</th>
                  <th style="padding:12px">CEFR</th>
                  <th style="padding:12px">Skill</th>
                  <th style="padding:12px">Band</th>
                  <th style="padding:12px">Status</th>
                  <th style="padding:12px">Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (obj of objectives(); track obj.key) {
                  <tr style="border-bottom:1px solid #f1f5f9">
                    <td style="padding:12px;min-width:260px">
                      <strong>{{ obj.title }}</strong>
                      <div style="color:#64748b;font-size:12px">{{ obj.key }}</div>
                      <div style="color:#64748b;font-size:12px;max-width:320px">{{ obj.description }}</div>
                    </td>
                    <td style="padding:12px">
                      <span style="font-weight:600">{{ obj.cefrLevel }}</span>
                    </td>
                    <td style="padding:12px;text-transform:capitalize">
                      {{ obj.primarySkill }}
                      @if (parseJsonArray(obj.secondarySkillsJson).length) {
                        <div style="color:#64748b;font-size:12px">+ {{ parseJsonArray(obj.secondarySkillsJson).join(', ') }}</div>
                      }
                    </td>
                    <td style="padding:12px">{{ obj.difficultyBand }}/5</td>
                    <td style="padding:12px">
                      <sp-admin-badge [tone]="obj.isActive ? 'success' : 'neutral'">
                        {{ obj.isActive ? 'Active' : 'Inactive' }}
                      </sp-admin-badge>
                      @if (obj.isReviewable) { <div style="font-size:11px;color:#64748b">Reviewable</div> }
                      @if (obj.isExamInspired) { <div style="font-size:11px;color:#64748b">Exam-inspired</div> }
                    </td>
                    <td style="padding:12px;white-space:nowrap">
                      <sp-admin-button variant="ghost" size="sm" type="button" (click)="startEdit(obj)">Edit</sp-admin-button>
                      @if (obj.isActive) {
                        <sp-admin-button variant="ghost" size="sm" type="button" [disabled]="actionKey() === obj.key" (click)="deactivate(obj.key)">Deactivate</sp-admin-button>
                      } @else {
                        <sp-admin-button variant="ghost" size="sm" type="button" [disabled]="actionKey() === obj.key" (click)="activate(obj.key)">Activate</sp-admin-button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </sp-admin-table>
      }

      <!-- ── Create / Edit form ── -->
      @if (view() === 'create' || view() === 'edit') {
        <sp-admin-card [title]="view() === 'create' ? 'New objective' : 'Edit: ' + form.key">
          @if (formError()) {
            <div style="color:#991b1b;margin-bottom:12px;padding:10px;background:#fef2f2;border-radius:6px">{{ formError() }}</div>
          }
          <div style="display:grid;gap:14px;max-width:640px">
            <sp-admin-form-field label="Key *" hint="Stable slug — lowercase letters, digits, dots, underscores, hyphens.">
              <input class="sp-input" style="width:100%" [(ngModel)]="form.key" [disabled]="view() === 'edit'" placeholder="e.g. b1.writing.clear_emails" />
            </sp-admin-form-field>
            <sp-admin-form-field label="Title *">
              <input class="sp-input" style="width:100%" [(ngModel)]="form.title" placeholder="e.g. Writing Clear Short Emails" />
            </sp-admin-form-field>
            <sp-admin-form-field label="Description *">
              <textarea class="sp-input" style="width:100%;min-height:70px" [(ngModel)]="form.description"></textarea>
            </sp-admin-form-field>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
              <sp-admin-form-field label="CEFR level *">
                <select class="sp-input" style="width:100%" [(ngModel)]="form.cefrLevel">
                  @for (level of taxonomy()?.cefrLevels ?? []; track level) {
                    <option [value]="level">{{ level }}</option>
                  }
                </select>
              </sp-admin-form-field>
              <sp-admin-form-field label="Primary skill *">
                <select class="sp-input" style="width:100%" [(ngModel)]="form.primarySkill">
                  @for (skill of taxonomy()?.skills ?? []; track skill) {
                    <option [value]="skill">{{ skill }}</option>
                  }
                </select>
              </sp-admin-form-field>
            </div>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
              <sp-admin-form-field label="Difficulty band (1–5) *">
                <input class="sp-input" style="width:100%" type="number" min="1" max="5" [(ngModel)]="form.difficultyBand" />
              </sp-admin-form-field>
              <sp-admin-form-field label="Recommended order">
                <input class="sp-input" style="width:100%" type="number" min="0" [(ngModel)]="form.recommendedOrder" />
              </sp-admin-form-field>
            </div>
            <div>
              <label style="display:block;font-size:13px;font-weight:600;margin-bottom:4px">Context tags</label>
              <div style="display:flex;flex-wrap:wrap;gap:6px">
                @for (tag of taxonomy()?.contextTags ?? []; track tag) {
                  <label style="display:flex;align-items:center;gap:4px;font-size:13px;cursor:pointer">
                    <input type="checkbox" [checked]="form.contextTags.includes(tag)" (change)="toggleTag('contextTags', tag, $event)" />
                    {{ tag }}
                  </label>
                }
              </div>
            </div>
            <sp-admin-form-field label="Focus tags" hint="Comma-separated">
              <input class="sp-input" style="width:100%" [(ngModel)]="focusTagsRaw" placeholder="e.g. email_writing, workplace_communication" />
            </sp-admin-form-field>
            <div>
              <label style="display:block;font-size:13px;font-weight:600;margin-bottom:4px">Secondary skills</label>
              <div style="display:flex;flex-wrap:wrap;gap:6px">
                @for (skill of taxonomy()?.skills ?? []; track skill) {
                  <label style="display:flex;align-items:center;gap:4px;font-size:13px;cursor:pointer">
                    <input type="checkbox" [checked]="form.secondarySkills.includes(skill)" (change)="toggleTag('secondarySkills', skill, $event)" />
                    {{ skill }}
                  </label>
                }
              </div>
            </div>
            <sp-admin-form-field label="Prerequisite keys" hint="Comma-separated">
              <input class="sp-input" style="width:100%" [(ngModel)]="prerequisiteKeysRaw" placeholder="e.g. a2.writing.short_messages" />
            </sp-admin-form-field>
            <div style="display:flex;gap:16px;flex-wrap:wrap">
              <label style="display:flex;align-items:center;gap:6px;font-size:13px;cursor:pointer">
                <input type="checkbox" [(ngModel)]="form.isActive" /> Active
              </label>
              <label style="display:flex;align-items:center;gap:6px;font-size:13px;cursor:pointer">
                <input type="checkbox" [(ngModel)]="form.isReviewable" /> Reviewable
              </label>
              <label style="display:flex;align-items:center;gap:6px;font-size:13px;cursor:pointer">
                <input type="checkbox" [(ngModel)]="form.isExamInspired" /> Exam-inspired
              </label>
            </div>
            <sp-admin-form-field label="Teaching notes" hint="Not shown to students">
              <textarea class="sp-input" style="width:100%;min-height:60px" [(ngModel)]="form.teachingNotes"></textarea>
            </sp-admin-form-field>
            <sp-admin-form-field label="Example prompts" hint="Not shown to students">
              <textarea class="sp-input" style="width:100%;min-height:60px" [(ngModel)]="form.examplePrompts"></textarea>
            </sp-admin-form-field>
            <div style="display:flex;gap:8px;padding-top:8px">
              <sp-admin-button type="button" [loading]="saving()" [disabled]="saving()" (click)="save()">
                {{ view() === 'create' ? 'Create' : 'Save changes' }}
              </sp-admin-button>
              <sp-admin-button variant="secondary" type="button" (click)="cancelEdit()">Cancel</sp-admin-button>
            </div>
          </div>
        </sp-admin-card>
      }

      <!-- ── Routing preview ── -->
      @if (view() === 'preview') {
        <sp-admin-card title="Routing preview" style="display:block;max-width:640px">
          <p style="color:#64748b;font-size:14px;margin-bottom:16px">
            Test routing without generating AI content or mutating any student state.
          </p>
          <div style="display:grid;gap:14px">
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
              <sp-admin-form-field label="CEFR level override">
                <select class="sp-input" style="width:100%" [(ngModel)]="preview.cefrLevelOverride">
                  <option value="">Auto (from student)</option>
                  @for (level of taxonomy()?.cefrLevels ?? []; track level) {
                    <option [value]="level">{{ level }}</option>
                  }
                </select>
              </sp-admin-form-field>
              <sp-admin-form-field label="Primary skill">
                <select class="sp-input" style="width:100%" [(ngModel)]="preview.primarySkill">
                  <option value="">Any</option>
                  @for (skill of taxonomy()?.skills ?? []; track skill) {
                    <option [value]="skill">{{ skill }}</option>
                  }
                </select>
              </sp-admin-form-field>
            </div>
            <sp-admin-form-field label="Source label">
              <select class="sp-input" style="width:100%" [(ngModel)]="preview.source">
                <option value="admin_preview">admin_preview</option>
                <option value="today_lesson">today_lesson</option>
                <option value="practice_gym">practice_gym</option>
                <option value="on_demand">on_demand</option>
              </select>
            </sp-admin-form-field>
            <sp-admin-form-field label="Difficulty preference">
              <select class="sp-input" style="width:100%" [(ngModel)]="preview.difficultyPreference">
                <option value="">Balanced (default)</option>
                <option value="gentle">Gentle</option>
                <option value="challenging">Challenging</option>
              </select>
            </sp-admin-form-field>
            <label style="display:flex;align-items:center;gap:6px;font-size:13px;cursor:pointer">
              <input type="checkbox" [(ngModel)]="preview.allowReviewOrScaffold" />
              Allow review / scaffold (may select lower-level content)
            </label>
            <div style="display:flex;gap:8px">
              <sp-admin-button type="button" [loading]="previewing()" [disabled]="previewing()" (click)="runPreview()">
                Run preview
              </sp-admin-button>
              <sp-admin-button variant="secondary" type="button" (click)="view.set('list')">Back to list</sp-admin-button>
            </div>
            @if (previewResult()) {
              <div style="border:1px solid #e2e8f0;border-radius:8px;padding:16px;background:#f8fafc">
                <div style="display:grid;gap:8px;font-size:14px">
                  <div><strong>Target CEFR:</strong> {{ previewResult()!.targetCefrLevel }}</div>
                  <div><strong>Objective:</strong>
                    @if (previewResult()!.curriculumObjectiveKey) {
                      {{ previewResult()!.curriculumObjectiveTitle }} <span style="color:#64748b">({{ previewResult()!.curriculumObjectiveKey }})</span>
                    } @else {
                      <span style="color:#64748b">None matched</span>
                    }
                  </div>
                  <div><strong>Routing reason:</strong> {{ previewResult()!.routingReason }}</div>
                  <div><strong>Context tags:</strong> {{ previewResult()!.contextTags.join(', ') || '—' }}</div>
                  <div><strong>Focus tags:</strong> {{ previewResult()!.focusTags.join(', ') || '—' }}</div>
                  <div><strong>Difficulty band:</strong> {{ previewResult()!.difficultyBand }}</div>
                  @if (previewResult()!.isLowerLevelContent) {
                    <div style="color:#92400e;background:#fffbeb;padding:8px;border-radius:4px">
                      Lower-level content selected.
                    </div>
                  }
                  @if (previewResult()!.fallbackUsed) {
                    <div style="color:#92400e;background:#fffbeb;padding:8px;border-radius:4px">
                      Fallback used — no matching objective found.
                    </div>
                  }
                  @if (previewResult()!.explanation) {
                    <div style="color:#475569"><strong>Explanation:</strong> {{ previewResult()!.explanation }}</div>
                  }
                  @for (warning of previewResult()!.warnings; track warning) {
                    <div style="color:#b45309;background:#fffbeb;padding:6px 10px;border-radius:4px;font-size:13px">{{ warning }}</div>
                  }
                </div>
              </div>
            }
          </div>
        </sp-admin-card>
      }

    </sp-admin-page-body>
  `,
})
export class AdminCurriculumComponent implements OnInit {
  view = signal<View>('list');
  objectives = signal<AdminCurriculumObjectiveDto[]>([]);
  allObjectives = signal<AdminCurriculumObjectiveDto[]>([]);
  taxonomy = signal<CurriculumTaxonomyDto | null>(null);
  loading = signal(false);
  saving = signal(false);
  previewing = signal(false);
  actionKey = signal<string | null>(null);
  globalError = signal<string | null>(null);
  formError = signal<string | null>(null);
  previewResult = signal<AdminRoutingPreviewResult | null>(null);

  filterCefr = '';
  filterSkill = '';
  filterActive = 'true';
  focusTagsRaw = '';
  prerequisiteKeysRaw = '';

  readonly cefrOptions = computed<SpAdminSelectOption[]>(() =>
    (this.taxonomy()?.cefrLevels ?? []).map(l => ({ value: l, label: l }))
  );
  readonly skillOptions = computed<SpAdminSelectOption[]>(() =>
    (this.taxonomy()?.skills ?? []).map(s => ({ value: s, label: s }))
  );
  readonly activeOptions: SpAdminSelectOption[] = [
    { value: 'true', label: 'Active only' },
    { value: 'false', label: 'Inactive only' },
  ];

  readonly coverageSummary = computed(() => {
    const all = this.allObjectives();
    const cefrBands = new Set(all.map(o => o.cefrLevel)).size;
    const skills = new Set(all.map(o => o.primarySkill)).size;
    return {
      total: all.length,
      active: all.filter(o => o.isActive).length,
      cefrBands,
      skills,
    };
  });

  readonly activeRingPct = computed(() => {
    const { total, active } = this.coverageSummary();
    return total > 0 ? Math.round((active / total) * 100) : 0;
  });

  readonly cefrBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const all = this.allObjectives();
    const counts: Record<string, number> = {};
    for (const o of all) if (o.cefrLevel) counts[o.cefrLevel] = (counts[o.cefrLevel] ?? 0) + 1;
    const order = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];
    const total = all.length || 1;
    const tones: BreakdownBarItem['tone'][] = ['green', 'teal', 'indigo', 'violet', 'amber', 'slate'];
    return order.filter(l => counts[l]).map((l, i) => ({
      label: l, value: counts[l], pct: Math.round((counts[l] / total) * 100), tone: tones[i % tones.length],
    }));
  });

  form: AdminCurriculumObjectiveUpsertRequest = this.emptyForm();
  preview: AdminRoutingPreviewRequest = { allowReviewOrScaffold: false, source: 'admin_preview' };

  readonly parseJsonArray = parseJsonArray;

  constructor(private curriculum: CurriculumService) {}

  ngOnInit(): void {
    this.loadTaxonomy();
    this.load();
    this.loadAll();
  }

  load(): void {
    this.loading.set(true);
    this.globalError.set(null);
    const active = this.filterActive === '' ? undefined : this.filterActive === 'true';
    this.curriculum.listObjectives(
      this.filterCefr || undefined,
      this.filterSkill || undefined,
      active,
    ).subscribe({
      next: items => { this.objectives.set(items); this.loading.set(false); },
      error: () => { this.globalError.set('Could not load objectives.'); this.loading.set(false); },
    });
  }

  loadAll(): void {
    this.curriculum.listObjectives(undefined, undefined, undefined).subscribe({
      next: items => this.allObjectives.set(items),
    });
  }

  loadTaxonomy(): void {
    this.curriculum.getTaxonomy().subscribe({
      next: tax => this.taxonomy.set(tax),
    });
  }

  startCreate(): void {
    this.form = this.emptyForm();
    this.focusTagsRaw = '';
    this.prerequisiteKeysRaw = '';
    this.formError.set(null);
    this.view.set('create');
  }

  startEdit(obj: AdminCurriculumObjectiveDto): void {
    this.form = {
      key: obj.key,
      title: obj.title,
      description: obj.description,
      cefrLevel: obj.cefrLevel,
      primarySkill: obj.primarySkill,
      secondarySkills: parseJsonArray(obj.secondarySkillsJson),
      contextTags: parseJsonArray(obj.contextTagsJson),
      focusTags: parseJsonArray(obj.focusTagsJson),
      prerequisiteObjectiveKeys: parseJsonArray(obj.prerequisiteKeysJson),
      recommendedOrder: obj.recommendedOrder,
      difficultyBand: obj.difficultyBand,
      isActive: obj.isActive,
      isReviewable: obj.isReviewable,
      isExamInspired: obj.isExamInspired,
      teachingNotes: obj.teachingNotes,
      examplePrompts: obj.examplePrompts,
    };
    this.focusTagsRaw = this.form.focusTags.join(', ');
    this.prerequisiteKeysRaw = this.form.prerequisiteObjectiveKeys.join(', ');
    this.formError.set(null);
    this.view.set('edit');
  }

  cancelEdit(): void {
    this.view.set('list');
    this.formError.set(null);
  }

  save(): void {
    this.form.focusTags = this.focusTagsRaw.split(',').map(s => s.trim()).filter(Boolean);
    this.form.prerequisiteObjectiveKeys = this.prerequisiteKeysRaw.split(',').map(s => s.trim()).filter(Boolean);

    this.saving.set(true);
    this.formError.set(null);

    const obs = this.view() === 'create'
      ? this.curriculum.createObjective(this.form)
      : this.curriculum.updateObjective(this.form.key, this.form);

    obs.subscribe({
      next: () => { this.saving.set(false); this.view.set('list'); this.load(); this.loadAll(); },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.error ?? 'Could not save objective.');
      },
    });
  }

  activate(key: string): void {
    this.actionKey.set(key);
    this.curriculum.activateObjective(key).subscribe({
      next: updated => {
        this.objectives.update(items => items.map(o => o.key === key ? updated : o));
        this.actionKey.set(null);
      },
      error: () => { this.globalError.set('Could not activate objective.'); this.actionKey.set(null); },
    });
  }

  deactivate(key: string): void {
    this.actionKey.set(key);
    this.curriculum.deactivateObjective(key).subscribe({
      next: updated => {
        this.objectives.update(items => items.map(o => o.key === key ? updated : o));
        this.actionKey.set(null);
      },
      error: () => { this.globalError.set('Could not deactivate objective.'); this.actionKey.set(null); },
    });
  }

  runPreview(): void {
    this.previewing.set(true);
    this.previewResult.set(null);
    this.curriculum.previewRouting(this.preview).subscribe({
      next: result => { this.previewResult.set(result); this.previewing.set(false); },
      error: () => { this.previewing.set(false); },
    });
  }

  toggleTag(field: 'contextTags' | 'secondarySkills', value: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.form[field].includes(value)) this.form[field] = [...this.form[field], value];
    } else {
      this.form[field] = this.form[field].filter(t => t !== value);
    }
  }

  private emptyForm(): AdminCurriculumObjectiveUpsertRequest {
    return {
      key: '', title: '', description: '',
      cefrLevel: 'A1', primarySkill: 'speaking',
      secondarySkills: [], contextTags: ['general_english'], focusTags: [],
      prerequisiteObjectiveKeys: [],
      recommendedOrder: 0, difficultyBand: 1,
      isActive: true, isReviewable: false, isExamInspired: false,
      teachingNotes: null, examplePrompts: null,
    };
  }
}

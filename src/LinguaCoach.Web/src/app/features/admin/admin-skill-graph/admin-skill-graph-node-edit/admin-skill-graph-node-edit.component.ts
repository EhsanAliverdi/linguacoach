import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphNodeDetail, SkillGraphNodeListItem, SkillGraphTaxonomy } from '../../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../../../../design-system/admin';

/**
 * Editability audit (2026-07-23) — Skill Graph node edit as its own routed page
 * (/admin/skill-graph/nodes/:id/edit), mirroring admin-module-edit.component.ts's exact pattern:
 * this was the codebase's first fully-established "dedicated route, not modal" precedent for
 * editing an AdminReviewStatus-gated entity's core fields. Blocked server-side while Approved
 * (SkillGraphNode.UpdateCore) — reject the node first to reopen editing, same as Module/Lesson/
 * Exercise.
 *
 * User follow-up (2026-07-23): both Edit and View must show/manage prerequisites and unlocks —
 * this page previously only edited core fields with no edge visibility at all. `item` is now a
 * signal so the Prerequisites/Unlocks lists re-render after an add/remove without a full
 * `load()` round-trip forcing the whole form to reset.
 */
@Component({
  selector: 'app-admin-skill-graph-node-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-skill-graph-node-edit.component.html',
})
export class AdminSkillGraphNodeEditComponent implements OnInit {
  nodeId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  item = signal<SkillGraphNodeDetail | null>(null);

  taxonomy = signal<SkillGraphTaxonomy | null>(null);
  cefrLevelOptions = computed(() => (this.taxonomy()?.cefrLevels ?? []).map(l => ({ value: l, label: l })));
  skillOptions = computed(() => (this.taxonomy()?.skills ?? []).map(s => ({ value: s, label: s })));
  subskillOptions = computed(() =>
    (this.taxonomy()?.subskillsBySkill?.[this.skill] ?? []).map(s => ({ value: s, label: s })));

  title = '';
  description = '';
  cefrLevel = '';
  skill = '';
  subskill = '';
  difficultyBand: number | null = null;
  descriptionForAi = '';

  constructor(
    private api: AdminApiService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.nodeId = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.getSkillGraphTaxonomy().subscribe({ next: t => this.taxonomy.set(t), error: () => {} });
    if (!this.nodeId) return;
    this.load();
    this.loadNodesForPicker();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getSkillGraphNode(this.nodeId).subscribe({
      next: item => {
        this.loading.set(false);
        this.item.set(item);
        this.title = item.title;
        this.description = item.description;
        this.cefrLevel = item.cefrLevel;
        this.skill = item.skill;
        this.subskill = item.subskill ?? '';
        this.difficultyBand = item.difficultyBand;
        this.descriptionForAi = item.descriptionForAi ?? '';
      },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this node for editing.'); },
    });
  }

  cancel(): void {
    this.router.navigateByUrl('/admin/skill-graph');
  }

  save(): void {
    const current = this.item();
    if (!current) return;
    if (!this.title.trim() || !this.description.trim() || !this.cefrLevel || !this.skill) {
      this.error.set('Title, description, CEFR level, and skill are all required.');
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.api.updateSkillGraphNode(current.id, {
      title: this.title.trim(),
      description: this.description.trim(),
      cefrLevel: this.cefrLevel,
      skill: this.skill,
      subskill: this.subskill.trim() || null,
      difficultyBand: this.difficultyBand ?? 1,
      descriptionForAi: this.descriptionForAi.trim() || null,
    }).subscribe({
      next: () => { this.saving.set(false); this.router.navigateByUrl('/admin/skill-graph'); },
      error: err => { this.saving.set(false); this.error.set(err.error?.error ?? 'Could not save changes — approved nodes must be rejected first to reopen editing.'); },
    });
  }

  // ── User follow-up (2026-07-23) — Prerequisites/Unlocks management, same shape as the node
  // detail slide-over on the main Skill Graph page (a graph node's place in the graph matters as
  // much here as its content fields do). ──────────────────────────────────────────────────────
  private allNodesForPicker: SkillGraphNodeListItem[] = [];

  private loadNodesForPicker(): void {
    this.api.getSkillGraphNodes({ pageSize: 500 }).subscribe({
      next: r => this.allNodesForPicker = r.items,
      error: () => this.allNodesForPicker = [],
    });
  }

  addPrereqError = signal('');
  addPrereqQuery = '';
  addUnlockError = signal('');
  addUnlockQuery = '';

  addPrereqResults = computed(() => {
    const q = this.addPrereqQuery.trim().toLowerCase();
    if (!q) return [];
    const current = this.item();
    const existingPrereqIds = new Set((current?.prerequisites ?? []).map(p => p.id));
    return this.allNodesForPicker
      .filter(n => n.id !== current?.id && !existingPrereqIds.has(n.id)
        && (n.title.toLowerCase().includes(q) || n.key.toLowerCase().includes(q)))
      .slice(0, 15);
  });

  addUnlockResults = computed(() => {
    const q = this.addUnlockQuery.trim().toLowerCase();
    if (!q) return [];
    const current = this.item();
    const existingDependentIds = new Set((current?.dependents ?? []).map(d => d.id));
    return this.allNodesForPicker
      .filter(n => n.id !== current?.id && !existingDependentIds.has(n.id)
        && (n.title.toLowerCase().includes(q) || n.key.toLowerCase().includes(q)))
      .slice(0, 15);
  });

  addPrerequisite(prereq: SkillGraphNodeListItem): void {
    const current = this.item();
    if (!current) return;
    this.addPrereqError.set('');
    this.api.addSkillGraphPrerequisite(current.id, prereq.id).subscribe({
      next: () => { this.addPrereqQuery = ''; this.load(); },
      error: err => this.addPrereqError.set(err.error?.error ?? 'Could not add this prerequisite.'),
    });
  }

  removePrerequisite(prereqId: string): void {
    const current = this.item();
    if (!current) return;
    this.api.removeSkillGraphPrerequisite(current.id, prereqId).subscribe({
      next: () => this.load(),
      error: err => this.addPrereqError.set(err.error?.error ?? 'Could not remove this prerequisite.'),
    });
  }

  // "X unlocks this node" is the same edge as "X depends on this node" — added/removed via the
  // same cycle-validated endpoint with the arguments swapped.
  addUnlock(dependent: SkillGraphNodeListItem): void {
    const current = this.item();
    if (!current) return;
    this.addUnlockError.set('');
    this.api.addSkillGraphPrerequisite(dependent.id, current.id).subscribe({
      next: () => { this.addUnlockQuery = ''; this.load(); },
      error: err => this.addUnlockError.set(err.error?.error ?? 'Could not add this unlock.'),
    });
  }

  removeUnlock(dependentId: string): void {
    const current = this.item();
    if (!current) return;
    this.api.removeSkillGraphPrerequisite(dependentId, current.id).subscribe({
      next: () => this.load(),
      error: err => this.addUnlockError.set(err.error?.error ?? 'Could not remove this unlock.'),
    });
  }
}

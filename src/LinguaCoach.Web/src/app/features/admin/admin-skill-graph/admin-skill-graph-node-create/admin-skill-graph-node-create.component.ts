import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphNodeListItem, SkillGraphTaxonomy } from '../../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminMultiSelectComponent,
  SpAdminMultiSelectOption,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../../../../design-system/admin';
import { NodeGraphPreviewEdge, NodeGraphPreviewNode, SpAdminNodeGraphPreviewComponent } from '../node-graph-preview/sp-admin-node-graph-preview.component';

interface StagedNodeRef {
  id: string;
  title: string;
}

/**
 * User correction (2026-07-23): Create must be its own routed page, matching View/Edit exactly
 * (page-header + page-body + sp-admin-section-card sections, Save/Cancel bottom-right with no
 * background/border) instead of the previous slide-over. Prerequisites/unlocks picked here are
 * staged exactly like Edit's add-prerequisite/add-unlock flow — nothing is created until Save,
 * and the graph preview shows a synthetic center node (`NEW_NODE_PREVIEW_ID`, since the real node
 * has no id yet) with the staged picks around it so the admin can see the placement before
 * committing.
 */
@Component({
  selector: 'app-admin-skill-graph-node-create',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminMultiSelectComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
    SpAdminNodeGraphPreviewComponent,
  ],
  templateUrl: './admin-skill-graph-node-create.component.html',
})
export class AdminSkillGraphNodeCreateComponent implements OnInit {
  private static readonly NEW_NODE_PREVIEW_ID = '__new-node-preview__';

  creating = signal(false);
  error = signal('');
  createdNodeId = signal<string | null>(null);

  // A signal (not a plain field) specifically so graphPreviewNodes() below can be a real
  // computed() — a plain field read inside computed() is invisible to its dependency tracking,
  // which previously caused the graph preview to return a brand-new array every change-detection
  // cycle (called as a template method) and made cytoscape/elk re-render in an infinite loop.
  title = signal('');
  description = '';
  cefrLevel = '';
  skill = '';
  subskill = '';
  difficultyBand: number | null = 1;
  contextTagsDraft = '';
  focusTagsDraft = '';

  taxonomy = signal<SkillGraphTaxonomy | null>(null);
  cefrLevelOptions = computed(() => (this.taxonomy()?.cefrLevels ?? []).map(l => ({ value: l, label: l })));
  skillOptions = computed(() => (this.taxonomy()?.skills ?? []).map(s => ({ value: s, label: s })));
  subskillOptions = computed(() =>
    (this.taxonomy()?.subskillsBySkill?.[this.skill] ?? []).map(s => ({ value: s, label: s })));

  // ── Staged prerequisites/unlocks (2026-07-23) — same shape/interaction as Edit's staged edge
  // changes: nothing is linked until Save, which creates the node then links each staged pick. ──
  stagedPrereqs = signal<StagedNodeRef[]>([]);
  stagedDependents = signal<StagedNodeRef[]>([]);

  private allNodesForPicker = signal<SkillGraphNodeListItem[]>([]);
  pickerOptions = computed<SpAdminMultiSelectOption[]>(() =>
    this.allNodesForPicker().map(n => ({ value: n.id, label: n.title, sublabel: `${n.cefrLevel} · ${n.skill}` })));

  private loadNodesForPicker(): void {
    this.api.getSkillGraphNodes({ pageSize: 500 }).subscribe({
      next: r => this.allNodesForPicker.set(r.items),
      error: () => this.allNodesForPicker.set([]),
    });
  }

  prereqExcludeIds(): string[] {
    return [...this.stagedPrereqs().map(p => p.id), ...this.stagedDependents().map(d => d.id)];
  }

  unlockExcludeIds(): string[] {
    return [...this.stagedDependents().map(d => d.id), ...this.stagedPrereqs().map(p => p.id)];
  }

  addPrereq(option: SpAdminMultiSelectOption): void {
    this.stagedPrereqs.update(list => [...list, { id: option.value, title: option.label }]);
  }

  removePrereq(id: string): void {
    this.stagedPrereqs.update(list => list.filter(p => p.id !== id));
  }

  addUnlock(option: SpAdminMultiSelectOption): void {
    this.stagedDependents.update(list => [...list, { id: option.value, title: option.label }]);
  }

  removeUnlock(id: string): void {
    this.stagedDependents.update(list => list.filter(d => d.id !== id));
  }

  // Graph preview (2026-07-23) — no real id exists yet, so a synthetic placeholder id stands in
  // for "this node"; every staged pick renders dashed-amber ("pending: add"), matching Edit's
  // convention for an unsaved change. Real computed()s (not plain methods) so they only produce a
  // new array when a tracked signal actually changes — a plain method bound directly as a
  // template input recomputes (and returns a new array reference) on every change-detection
  // cycle, which previously sent the child graph preview's OnChanges into a render loop.
  readonly graphCenterId = AdminSkillGraphNodeCreateComponent.NEW_NODE_PREVIEW_ID;

  graphPreviewNodes = computed<NodeGraphPreviewNode[]>(() => [
    { id: this.graphCenterId, title: this.title().trim() || 'New node' },
    ...this.stagedPrereqs().map(p => ({ id: p.id, title: p.title, pending: 'add' as const })),
    ...this.stagedDependents().map(d => ({ id: d.id, title: d.title, pending: 'add' as const })),
  ]);

  graphPreviewEdges = computed<NodeGraphPreviewEdge[]>(() => [
    ...this.stagedPrereqs().map(p => ({ source: p.id, target: this.graphCenterId, pending: 'add' as const })),
    ...this.stagedDependents().map(d => ({ source: this.graphCenterId, target: d.id, pending: 'add' as const })),
  ]);

  constructor(
    private api: AdminApiService,
    private router: Router,
    private location: Location,
  ) {}

  ngOnInit(): void {
    this.api.getSkillGraphTaxonomy().subscribe({ next: t => this.taxonomy.set(t), error: () => {} });
    this.loadNodesForPicker();
  }

  private parseTagsDraft(raw: string): string[] {
    return raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  // User correction (2026-07-24) — used to hardcode a return to the main list page; real
  // browser-history back instead, consistent with View/Edit's Back/Cancel fix.
  cancel(): void {
    this.location.back();
  }

  viewCreatedNode(): void {
    const id = this.createdNodeId();
    if (id) this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}`);
  }

  save(): void {
    if (!this.title().trim() || !this.description.trim() || !this.cefrLevel || !this.skill) {
      this.error.set('Title, description, CEFR level, and skill are all required.');
      return;
    }
    this.creating.set(true);
    this.error.set('');
    this.api.createSkillGraphNode({
      title: this.title().trim(),
      description: this.description.trim(),
      cefrLevel: this.cefrLevel,
      skill: this.skill,
      subskill: this.subskill.trim() || null,
      difficultyBand: this.difficultyBand ?? 1,
      descriptionForAi: null,
      contextTags: this.parseTagsDraft(this.contextTagsDraft),
      focusTags: this.parseTagsDraft(this.focusTagsDraft),
      prerequisiteNodeIds: this.stagedPrereqs().map(p => p.id),
      dependentNodeIds: this.stagedDependents().map(d => d.id),
    }).subscribe({
      next: r => {
        this.creating.set(false);
        const droppedCount = r.droppedPrerequisites.length + r.droppedDependents.length;
        if (droppedCount > 0) {
          this.createdNodeId.set(r.id);
          this.error.set(`Node created, but ${droppedCount} link(s) could not be made (e.g. would create a cycle). Open the node to review.`);
          return;
        }
        this.router.navigateByUrl(`/admin/skill-graph/nodes/${r.id}`);
      },
      error: err => { this.creating.set(false); this.error.set(err.error?.error ?? 'Could not create node.'); },
    });
  }
}

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of, Observable } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphEdge, SkillGraphNode, SkillGraphNodeDetail, SkillGraphNodeListItem, SkillGraphPlacementSuggestion, SkillGraphTaxonomy, ReparentReviewResult, GraphChangeSuggestion, AddSkillGraphPrerequisiteResponse } from '../../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminMultiSelectComponent,
  SpAdminMultiSelectOption,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../../../../design-system/admin';
import { computeGraphNeighborhood, NodeGraphPreviewEdge, NodeGraphPreviewNode, SpAdminNodeGraphPreviewComponent } from '../node-graph-preview/sp-admin-node-graph-preview.component';

interface StagedNodeRef {
  id: string;
  title: string;
}

/**
 * Editability audit (2026-07-23) — Skill Graph node edit as its own routed page
 * (/admin/skill-graph/nodes/:id/edit), mirroring admin-module-edit.component.ts's exact pattern:
 * this was the codebase's first fully-established "dedicated route, not modal" precedent for
 * editing an AdminReviewStatus-gated entity's core fields. Blocked server-side while Approved
 * (SkillGraphNode.UpdateCore) — reject the node first to reopen editing, same as Module/Lesson/
 * Exercise.
 *
 * User correction (2026-07-23): prerequisite/unlock changes used to call the API immediately on
 * every click — the user pointed out this bypasses the page's own Save/Cancel and can't be
 * undone by hitting Cancel. Adding/removing an edge is now purely local state
 * (`pendingAddPrereqs`/`pendingRemovePrereqIds`/`pendingAddUnlocks`/`pendingRemoveUnlockIds`) —
 * nothing is written to the graph until `save()` commits the core fields AND every staged edge
 * change together. `Cancel` just navigates away, discarding all of it. Staged additions/removals
 * render with a distinct "pending" style (dashed, different color) in both the list and the graph
 * preview, so it's visually obvious what will actually change on Save.
 */
@Component({
  selector: 'app-admin-skill-graph-node-edit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminMultiSelectComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminSelectComponent,
    SpAdminTextareaComponent,
    SpAdminNodeGraphPreviewComponent,
  ],
  templateUrl: './admin-skill-graph-node-edit.component.html',
})
export class AdminSkillGraphNodeEditComponent implements OnInit {
  nodeId = '';
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  item = signal<SkillGraphNodeDetail | null>(null);

  // ── Staged edge changes (2026-07-23) — nothing here calls the API; all of it is applied
  // together in save(). ─────────────────────────────────────────────────────────────────────────
  pendingAddPrereqs = signal<StagedNodeRef[]>([]);
  pendingRemovePrereqIds = signal<Set<string>>(new Set());
  pendingAddUnlocks = signal<StagedNodeRef[]>([]);
  pendingRemoveUnlockIds = signal<Set<string>>(new Set());

  hasPendingEdgeChanges = computed(() =>
    this.pendingAddPrereqs().length > 0 || this.pendingRemovePrereqIds().size > 0 ||
    this.pendingAddUnlocks().length > 0 || this.pendingRemoveUnlockIds().size > 0);

  // What actually renders in the Prerequisites/Unlocks lists and in the graph preview: real
  // edges (minus any staged for removal) plus staged additions, each tagged with its pending state.
  displayPrereqs = computed(() => {
    const current = this.item();
    if (!current) return [];
    const removing = this.pendingRemovePrereqIds();
    const kept = current.prerequisites.map(p => ({ ...p, pending: removing.has(p.id) ? ('remove' as const) : undefined }));
    const added = this.pendingAddPrereqs().map(p => ({ ...p, pending: 'add' as const }));
    return [...kept, ...added];
  });

  displayUnlocks = computed(() => {
    const current = this.item();
    if (!current) return [];
    const removing = this.pendingRemoveUnlockIds();
    const kept = current.dependents.map(d => ({ ...d, pending: removing.has(d.id) ? ('remove' as const) : undefined }));
    const added = this.pendingAddUnlocks().map(d => ({ ...d, pending: 'add' as const }));
    return [...kept, ...added];
  });

  // ── Multi-layer graph expansion (2026-07-23) — "+ layer"/"- layer" widens/narrows how many
  // hops out from this node the preview shows, via BFS over the whole graph's real edges (loaded
  // once here); staged (unsaved) additions/removals are then overlaid on top, but only ever at
  // the direct (1-hop) layer, since staging never touches anything but this node's own edges. ──
  readonly maxGraphLevel = 6;
  graphLevel = signal(1);

  private fullGraphEdges = signal<SkillGraphEdge[]>([]);
  private fullGraphNodesById = signal<Map<string, SkillGraphNode>>(new Map());

  private graphNeighborhood = computed(() => {
    const n = this.item();
    if (!n) return null;
    return computeGraphNeighborhood(n.id, this.fullGraphEdges(), this.graphLevel());
  });

  graphCenterId = computed(() => this.item()?.id ?? null);

  graphPreviewNodes = computed<NodeGraphPreviewNode[]>(() => {
    const n = this.item();
    const neigh = this.graphNeighborhood();
    if (!n || !neigh) return [];
    const byId = this.fullGraphNodesById();
    const removingPrereq = this.pendingRemovePrereqIds();
    const removingUnlock = this.pendingRemoveUnlockIds();

    const nodes: NodeGraphPreviewNode[] = Array.from(neigh.nodeIds).map(id => ({
      id,
      title: id === n.id ? n.title : (byId.get(id)?.title ?? '(unknown)'),
      pending: (removingPrereq.has(id) || removingUnlock.has(id)) ? 'remove' as const : undefined,
    }));

    // Staged additions aren't in the real graph yet — inject them so they're visible pre-Save.
    for (const p of this.pendingAddPrereqs()) if (!neigh.nodeIds.has(p.id)) nodes.push({ id: p.id, title: p.title, pending: 'add' });
    for (const d of this.pendingAddUnlocks()) if (!neigh.nodeIds.has(d.id)) nodes.push({ id: d.id, title: d.title, pending: 'add' });

    return nodes;
  });

  graphPreviewEdges = computed<NodeGraphPreviewEdge[]>(() => {
    const n = this.item();
    const neigh = this.graphNeighborhood();
    if (!n || !neigh) return [];
    const removingPrereq = this.pendingRemovePrereqIds();
    const removingUnlock = this.pendingRemoveUnlockIds();

    const edges: NodeGraphPreviewEdge[] = neigh.edges.map(e => ({
      ...e,
      pending: (e.target === n.id && removingPrereq.has(e.source)) || (e.source === n.id && removingUnlock.has(e.target))
        ? 'remove' as const
        : undefined,
    }));

    for (const p of this.pendingAddPrereqs()) edges.push({ source: p.id, target: n.id, pending: 'add' });
    for (const d of this.pendingAddUnlocks()) edges.push({ source: n.id, target: d.id, pending: 'add' });

    return edges;
  });

  increaseGraphLevel(): void { this.graphLevel.update(l => Math.min(l + 1, this.maxGraphLevel)); }
  decreaseGraphLevel(): void { this.graphLevel.update(l => Math.max(l - 1, 1)); }

  private loadFullGraph(): void {
    this.api.getSkillGraph().subscribe({
      next: r => {
        this.fullGraphEdges.set(r.edges);
        this.fullGraphNodesById.set(new Map(r.nodes.map(n => [n.id, n])));
      },
      error: () => { /* the graph preview just stays at direct-neighbor-only if this fails */ },
    });
  }

  goToNode(id: string): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}`);
  }

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
    private location: Location,
  ) {}

  ngOnInit(): void {
    this.nodeId = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.getSkillGraphTaxonomy().subscribe({ next: t => this.taxonomy.set(t), error: () => {} });
    if (!this.nodeId) return;
    this.load();
    this.loadNodesForPicker();
    this.loadFullGraph();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.graphLevel.set(1);
    this.clearPendingEdgeState();
    this.placementSuggestionError.set('');
    this.placementPrereqSuggestions.set([]);
    this.placementDependentSuggestions.set([]);
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

  private clearPendingEdgeState(): void {
    this.pendingAddPrereqs.set([]);
    this.pendingRemovePrereqIds.set(new Set());
    this.pendingAddUnlocks.set([]);
    this.pendingRemoveUnlockIds.set(new Set());
  }

  // User correction (2026-07-24) — these three used to hardcode a return to the main list page,
  // so an admin who navigated here from another node's graph preview (goToNode, above) would skip
  // past the node they actually came from. Real browser-history back instead.
  cancel(): void {
    this.location.back();
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
    }).pipe(
      switchMap(updateResult => this.commitEdgeChanges(current.id).pipe(map(edgeResult => ({ ...edgeResult, updateResult })))),
    ).subscribe({
      next: ({ failedCount, suggestions, updateResult }) => {
        this.saving.set(false);
        if (failedCount > 0) {
          this.error.set(`Core fields saved, but ${failedCount} graph change(s) could not be applied (e.g. would create a cycle). Reopen this node to review.`);
          this.load();
          this.loadFullGraph();
          return;
        }
        // Skill Graph rebuild Phase 6.3d — if the edit moved this node to a different CEFR
        // level/Skill and it has existing edges, stay on the page and surface them for review
        // instead of navigating away immediately; nothing here is ever removed automatically.
        const hasReparentReview = !!updateResult.reparentReview && updateResult.reparentReview.edgesToReview.length > 0;
        if (hasReparentReview) this.reparentReview.set(updateResult.reparentReview);
        // Phase 6.3e — a staged prerequisite/unlock add can itself trigger 6.3a's inline
        // redundant-edge check; surface it the same way instead of silently discarding it.
        if (suggestions.length > 0) this.redundantEdgeSuggestionsFromSave.set(suggestions);
        if (hasReparentReview || suggestions.length > 0) {
          // Everything staged was actually committed by this point — reload (which also clears
          // the local "pending" state) so the page doesn't confusingly keep showing already-saved
          // changes as "not saved yet" while the admin reviews the suggestion(s) above.
          this.load();
          this.loadFullGraph();
          return;
        }
        this.location.back();
      },
      error: err => { this.saving.set(false); this.error.set(err.error?.error ?? 'Could not save changes — approved nodes must be rejected first to reopen editing.'); },
    });
  }

  // ── Skill Graph rebuild Phase 6.3d — reparenting-on-edit review. Advisory only: "Remove edge"
  // is a real removeSkillGraphPrerequisite call; "Dismiss" just hides it from this list. ────────
  reparentReview = signal<ReparentReviewResult | null>(null);
  reparentReviewError = signal('');

  dismissReparentReviewEdge(otherNodeId: string): void {
    this.reparentReview.update(r => r && { ...r, edgesToReview: r.edgesToReview.filter(e => e.otherNodeId !== otherNodeId) });
  }

  removeReparentReviewEdge(edge: { otherNodeId: string; isPrerequisite: boolean }): void {
    const nodeId = this.nodeId;
    const call = edge.isPrerequisite
      ? this.api.removeSkillGraphPrerequisite(nodeId, edge.otherNodeId)
      : this.api.removeSkillGraphPrerequisite(edge.otherNodeId, nodeId);
    call.subscribe({
      next: () => this.dismissReparentReviewEdge(edge.otherNodeId),
      error: err => this.reparentReviewError.set(err?.error?.error ?? 'Could not remove this edge.'),
    });
  }

  // ── Skill Graph rebuild Phase 6.3e — redundant-edge suggestions surfaced from THIS save's own
  // staged prerequisite/unlock additions (as opposed to the main list page's separate "Run graph
  // audit" on-demand check). Same shape/actions as that card, just a different trigger. ─────────
  redundantEdgeSuggestionsFromSave = signal<GraphChangeSuggestion[]>([]);
  redundantEdgeSaveError = signal('');

  dismissRedundantEdgeSuggestionFromSave(index: number): void {
    this.redundantEdgeSuggestionsFromSave.update(list => list.filter((_, i) => i !== index));
  }

  removeRedundantEdgeFromSave(suggestion: GraphChangeSuggestion, index: number): void {
    const edge = suggestion.proposedEdgesToRemove[0];
    if (!edge) return;
    this.api.removeSkillGraphPrerequisite(edge.nodeId, edge.prerequisiteNodeId).subscribe({
      next: () => {
        this.dismissRedundantEdgeSuggestionFromSave(index);
        this.load();
        this.loadFullGraph();
      },
      error: err => this.redundantEdgeSaveError.set(err?.error?.error ?? 'Could not remove this edge.'),
    });
  }

  // Shared exit point for both the reparent-review and redundant-edge-from-save cards.
  finishReparentReview(): void {
    this.location.back();
  }

  // Applies every staged edge change together; a failed individual call (e.g. would create a
  // cycle) doesn't block the others — failures are counted and reported back to save(). Add-calls
  // also carry through any inline redundant-edge suggestions (6.3e) rather than discarding them.
  private commitEdgeChanges(nodeId: string): Observable<{ failedCount: number; suggestions: GraphChangeSuggestion[] }> {
    const settleBool = (obs$: Observable<unknown>): Observable<boolean> =>
      obs$.pipe(map(() => true), catchError(() => of(false)));
    const settleAdd = (obs$: Observable<AddSkillGraphPrerequisiteResponse>): Observable<{ ok: boolean; suggestions: GraphChangeSuggestion[] }> =>
      obs$.pipe(
        map(r => ({ ok: true, suggestions: r.suggestions })),
        catchError(() => of({ ok: false, suggestions: [] as GraphChangeSuggestion[] })),
      );

    const addCalls: Observable<{ ok: boolean; suggestions: GraphChangeSuggestion[] }>[] = [
      ...this.pendingAddPrereqs().map(p => settleAdd(this.api.addSkillGraphPrerequisite(nodeId, p.id))),
      ...this.pendingAddUnlocks().map(d => settleAdd(this.api.addSkillGraphPrerequisite(d.id, nodeId))),
    ];
    const removeCalls: Observable<boolean>[] = [
      ...Array.from(this.pendingRemovePrereqIds()).map(id => settleBool(this.api.removeSkillGraphPrerequisite(nodeId, id))),
      ...Array.from(this.pendingRemoveUnlockIds()).map(id => settleBool(this.api.removeSkillGraphPrerequisite(id, nodeId))),
    ];

    if (addCalls.length === 0 && removeCalls.length === 0) return of({ failedCount: 0, suggestions: [] });

    const addResults$ = addCalls.length > 0 ? forkJoin(addCalls) : of([] as { ok: boolean; suggestions: GraphChangeSuggestion[] }[]);
    const removeResults$ = removeCalls.length > 0 ? forkJoin(removeCalls) : of([] as boolean[]);

    return forkJoin([addResults$, removeResults$]).pipe(
      map(([addResults, removeResults]) => ({
        failedCount: addResults.filter(r => !r.ok).length + removeResults.filter(ok => !ok).length,
        suggestions: addResults.flatMap(r => r.suggestions),
      })),
    );
  }

  // ── User follow-up (2026-07-23) — Prerequisites/Unlocks management, same shape as the node
  // detail slide-over on the main Skill Graph page (a graph node's place in the graph matters as
  // much here as its content fields do). ──────────────────────────────────────────────────────
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
    const current = this.item();
    if (!current) return [];
    return [
      current.id,
      ...current.prerequisites.map(p => p.id),
      ...this.pendingAddPrereqs().map(p => p.id),
    ];
  }

  unlockExcludeIds(): string[] {
    const current = this.item();
    if (!current) return [];
    return [
      current.id,
      ...current.dependents.map(d => d.id),
      ...this.pendingAddUnlocks().map(d => d.id),
    ];
  }

  addPrerequisite(option: SpAdminMultiSelectOption): void {
    this.pendingAddPrereqs.update(list => [...list, { id: option.value, title: option.label }]);
  }

  cancelAddPrerequisite(id: string): void {
    this.pendingAddPrereqs.update(list => list.filter(p => p.id !== id));
  }

  removePrerequisite(id: string): void {
    this.pendingRemovePrereqIds.update(set => new Set(set).add(id));
  }

  undoRemovePrerequisite(id: string): void {
    this.pendingRemovePrereqIds.update(set => { const next = new Set(set); next.delete(id); return next; });
  }

  addUnlock(option: SpAdminMultiSelectOption): void {
    this.pendingAddUnlocks.update(list => [...list, { id: option.value, title: option.label }]);
  }

  cancelAddUnlock(id: string): void {
    this.pendingAddUnlocks.update(list => list.filter(d => d.id !== id));
  }

  removeUnlock(id: string): void {
    this.pendingRemoveUnlockIds.update(set => new Set(set).add(id));
  }

  undoRemoveUnlock(id: string): void {
    this.pendingRemoveUnlockIds.update(set => { const next = new Set(set); next.delete(id); return next; });
  }

  // ── Skill Graph rebuild Phase 6.2 (2026-07-23) — AI-proposed placement suggestions. Advisory
  // only: accepting one just stages it through the exact same addPrerequisite/addUnlock path as
  // a manually-picked node — nothing here ever calls the graph-mutating API directly, so a
  // suggestion is not written to the graph until the admin hits Save, same as any other edit. ──
  suggestingPlacement = signal(false);
  placementSuggestionError = signal('');
  placementPrereqSuggestions = signal<SkillGraphPlacementSuggestion[]>([]);
  placementDependentSuggestions = signal<SkillGraphPlacementSuggestion[]>([]);

  suggestPlacement(): void {
    const current = this.item();
    if (!current) return;
    this.suggestingPlacement.set(true);
    this.placementSuggestionError.set('');
    this.placementPrereqSuggestions.set([]);
    this.placementDependentSuggestions.set([]);
    this.api.suggestSkillGraphNodePlacement(current.id).subscribe({
      next: r => {
        this.suggestingPlacement.set(false);
        if (!r.success) {
          this.placementSuggestionError.set(r.error ?? 'Could not generate placement suggestions.');
          return;
        }
        this.placementPrereqSuggestions.set(r.prerequisites);
        this.placementDependentSuggestions.set(r.dependents);
        if (r.prerequisites.length === 0 && r.dependents.length === 0) {
          this.placementSuggestionError.set('No suggestions found — this node may already be well-connected, or too few related nodes exist yet.');
        }
      },
      error: err => {
        this.suggestingPlacement.set(false);
        this.placementSuggestionError.set(err.error?.error ?? 'Could not generate placement suggestions.');
      },
    });
  }

  acceptPlacementPrereqSuggestion(s: SkillGraphPlacementSuggestion): void {
    this.addPrerequisite({ value: s.id, label: s.title, sublabel: `confidence ${Math.round(s.confidence * 100)}%` });
    this.placementPrereqSuggestions.update(list => list.filter(x => x.id !== s.id));
  }

  dismissPlacementPrereqSuggestion(s: SkillGraphPlacementSuggestion): void {
    this.placementPrereqSuggestions.update(list => list.filter(x => x.id !== s.id));
  }

  acceptPlacementDependentSuggestion(s: SkillGraphPlacementSuggestion): void {
    this.addUnlock({ value: s.id, label: s.title, sublabel: `confidence ${Math.round(s.confidence * 100)}%` });
    this.placementDependentSuggestions.update(list => list.filter(x => x.id !== s.id));
  }

  dismissPlacementDependentSuggestion(s: SkillGraphPlacementSuggestion): void {
    this.placementDependentSuggestions.update(list => list.filter(x => x.id !== s.id));
  }
}

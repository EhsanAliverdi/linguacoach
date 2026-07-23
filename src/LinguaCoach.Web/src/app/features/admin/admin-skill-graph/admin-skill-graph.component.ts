import { Component, OnInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCoverageHeatmapComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminHeatmapCell,
  SpAdminHeatmapColumn,
  SpAdminHeatmapRow,
  SpAdminHelpIconComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionHeaderComponent,
  SpAdminSelectComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
} from '../../../design-system/admin';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListItem,
  SkillGraphCoverageEntry,
  SkillGraphNode,
  SkillGraphEdge,
  SkillGraphIsolatedNode,
  GraphChangeSuggestion,
  RejectReconnectGroup,
  NearDuplicateNodeSuggestion,
  ConfirmNearDuplicateResponse,
} from '../../../core/models/admin.models';
import { SpAdminGraphCardComponent } from '../../../design-system/admin/components/graph-card/sp-admin-graph-card.component';
import { SpAdminSkillGraphVizComponent } from './skill-graph-viz/sp-admin-skill-graph-viz.component';
import { IssuesSummary } from '../../../core/models/admin-repair.models';
import { AdminBulkRepairService } from '../../../core/services/admin-bulk-repair.service';

@Component({
  selector: 'app-admin-skill-graph',
  standalone: true,
  templateUrl: './admin-skill-graph.component.html',
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCoverageHeatmapComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminHelpIconComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionHeaderComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
    SpAdminGraphCardComponent,
    SpAdminSkillGraphVizComponent,
  ],
})
export class AdminSkillGraphComponent implements OnInit {
  constructor(private api: AdminApiService, public bulkRepair: AdminBulkRepairService, private router: Router) {}

  @ViewChild('nodesTableRef') nodesTableRef?: SpAdminTableComponent;

  // ── Sprint 14.1 — node tag issue count + bulk "Fix All with AI" ──────────────────────────
  nodeIssuesSummary = signal<IssuesSummary | null>(null);

  loadNodeIssuesSummary(): void {
    this.api.getSkillGraphNodeIssuesSummary().subscribe({
      next: summary => this.nodeIssuesSummary.set(summary),
      error: () => this.nodeIssuesSummary.set(null),
    });
  }

  fixAllNodesWithAi(): void {
    this.bulkRepair.run({
      entityLabel: 'Skill Graph Node',
      listWithIssues: () => this.api.listSkillGraphNodesWithIssues(),
      repairOne: id => this.api.repairSkillGraphNode(id),
      onDone: () => {
        this.loadNodeIssuesSummary();
        this.loadNodes();
        this.graphLoaded = false;
        if (this.viewMode() === 'graph') this.loadGraph();
      },
    });
  }

  // ── Sprint 13 — Table/Graph view toggle + bulk nodes+edges for the visual view ────────────
  viewMode = signal<'table' | 'graph'>('table');
  graphLoading = signal(false);
  graphError = signal('');
  graphNodes = signal<SkillGraphNode[]>([]);
  graphEdges = signal<SkillGraphEdge[]>([]);
  graphLoaded = false;
  selectedGraphNode = signal<SkillGraphNode | null>(null);

  setViewMode(mode: 'table' | 'graph'): void {
    this.viewMode.set(mode);
    if (mode === 'graph' && !this.graphLoaded) {
      this.loadGraph();
    }
  }

  loadGraph(): void {
    this.graphLoading.set(true);
    this.graphError.set('');
    this.api.getSkillGraph().subscribe({
      next: r => {
        this.graphNodes.set(r.nodes);
        this.graphEdges.set(r.edges);
        this.graphLoading.set(false);
        this.graphLoaded = true;
      },
      error: err => {
        this.graphError.set(err?.error?.error ?? 'Could not load the skill graph.');
        this.graphLoading.set(false);
      },
    });
  }

  // ── Taxonomy (for dropdowns) ─────────────────────────────────────────────
  taxonomy = signal<SkillGraphTaxonomy | null>(null);
  cefrLevelOptions = computed(() =>
    (this.taxonomy()?.cefrLevels ?? []).map(l => ({ value: l, label: l })));
  skillOptions = computed(() =>
    (this.taxonomy()?.skills ?? []).map(s => ({ value: s, label: s })));
  // Phase 6.1 — Nodes table gained free-text search + ContextTag/FocusTag filters.
  contextTagOptions = computed(() =>
    (this.taxonomy()?.contextTags ?? []).map(t => ({ value: t, label: t })));
  focusTagOptions = computed(() =>
    (this.taxonomy()?.focusTags ?? []).map(t => ({ value: t, label: t })));

  // ── Coverage matrix ──────────────────────────────────────────────────────
  coverageLoading = signal(true);
  coverageError = signal('');
  coverage = signal<SkillGraphCoverageEntry[]>([]);
  coverageGaps = computed(() => this.coverage().filter(c => c.hasGap));

  // Sprint 14.4 — coverage heatmap (replaces the flat table). Row color per CEFR level, matching
  // the standalone design reference; skills come from the taxonomy so column order is stable.
  private readonly cefrColors: Record<string, string> = {
    A1: '#13B07C', A2: '#10B5A4', B1: '#5B4BE8', B2: '#B45CF0', C1: '#FF7A59', C2: '#F0982C',
  };
  heatmapRows = computed<SpAdminHeatmapRow[]>(() =>
    this.cefrLevelOptions().map(o => ({ key: o.value, label: o.value, color: this.cefrColors[o.value] })));
  heatmapColumns = computed<SpAdminHeatmapColumn[]>(() =>
    this.skillOptions().map(o => ({ key: o.value, label: o.value })));
  heatmapCells = computed<SpAdminHeatmapCell[]>(() =>
    this.coverage().map(e => ({
      rowKey: e.cefrLevel,
      columnKey: e.skill,
      value: e.approvedCount,
      secondaryValue: e.pendingCount || undefined,
      clickable: e.hasGap,
    })));
  totalApprovedCoverage = computed(() => this.coverage().reduce((s, e) => s + e.approvedCount, 0));
  totalPendingCoverage = computed(() => this.coverage().reduce((s, e) => s + e.pendingCount, 0));

  onHeatmapCellClick(cell: SpAdminHeatmapCell): void {
    const entry = this.coverage().find(e => e.cefrLevel === cell.rowKey && e.skill === cell.columnKey);
    if (entry) this.draftForGap(entry);
  }

  // ── Draft trigger ────────────────────────────────────────────────────────
  draftCefrLevel = '';
  draftSkill = '';
  draftPending = signal(false);
  draftStatus = signal('');
  draftError = signal('');

  // ── Content coverage (Sprint 2, expanded Sprint 14.2, merged into Nodes 2026-07-23) ───────
  // The separate "Content coverage" table/slide-over was deleted — it showed almost the same
  // node list as the Nodes table below, just with a Linked Modules column. That column now
  // lives directly on the Nodes table (nodesColumns 'linkedModules') and the full linked-Module
  // list is shown in the node detail slide-over. Only the aggregate stat and the sweep action
  // survive here, folded into the Nodes card's own header.
  contentCoverageLoading = signal(true);
  contentCoverageError = signal('');
  totalApprovedNodes = signal(0);
  nodesWithContent = signal(0);

  retagPending = signal(false);
  retagStatus = signal('');
  retagError = signal('');

  // ── Nodes table ──────────────────────────────────────────────────────────
  nodesLoading = signal(true);
  nodesError = signal('');
  nodes = signal<SkillGraphNodeListItem[]>([]);
  nodesPage = signal(1);
  readonly nodesPageSize = 25;
  nodesTotalPages = signal(1);
  nodesTotalCount = signal(0);

  filterCefrLevel = signal('');
  filterSkill = signal('');
  filterReviewStatus = signal('');
  // Phase 6.1 — free-text search + ContextTag/FocusTag filters.
  filterSearch = signal('');
  filterContextTag = signal('');
  filterFocusTag = signal('');
  readonly reviewStatusOptions = [
    { value: 'PendingReview', label: 'Pending review' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' },
  ];

  selectedIds = signal<Set<string>>(new Set());
  hasSelection = computed(() => this.selectedIds().size > 0);

  // Sprint 14.5 — Bulk edit toggle (sp-admin-table's new bulkEditable pattern): checkbox lives
  // merged into the Title cell instead of an always-visible leading column.
  nodesBulkEditMode = signal(false);
  onNodesBulkEditModeChange(enabled: boolean): void {
    this.nodesBulkEditMode.set(enabled);
    if (!enabled) this.clearSelection();
  }

  // Sprint 14.8 — Nodes table fully data-driven (columns/rows/cellTemplate): the table owns
  // thead/tbody, bold title, and the titleColumn checkbox. (selectionChange) emits row INDICES
  // into the currently-bound `nodes()` page, mapped here to real node ids for the batch API.
  readonly nodesColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'skill', label: 'Skill' },
    { key: 'subskill', label: 'Subskill' },
    { key: 'difficultyBand', label: 'Difficulty' },
    { key: 'tags', label: 'Tags' },
    { key: 'linkedModuleCount', label: 'Linked Modules' },
    { key: 'reviewStatus', label: 'Status' },
  ];

  onNodesSelectionChange(indices: number[]): void {
    const rows = this.nodes();
    const ids = indices.map(i => rows[i]?.id).filter((id): id is string => !!id);
    this.selectedIds.set(new Set(ids));
  }

  batchPending = signal(false);
  batchStatus = signal('');
  batchError = signal('');
  rejectReason = '';

  ngOnInit(): void {
    this.loadTaxonomy();
    this.loadCoverage();
    this.loadNodes();
    this.loadContentCoverage();
    this.loadNodeIssuesSummary();
    this.loadIsolatedNodes();
  }

  // ── Editability audit (2026-07-23) — isolated-node connectivity metric ───────────────────
  isolatedNodes = signal<SkillGraphIsolatedNode[]>([]);

  loadIsolatedNodes(): void {
    this.api.getIsolatedSkillGraphNodes().subscribe({
      next: r => this.isolatedNodes.set(r.isolated),
      error: () => this.isolatedNodes.set([]),
    });
  }

  // ── Skill Graph rebuild Phase 6.3a/6.3c, merged into one "Graph audit" section (2026-07-24 user
  // correction: "Graph audit and Near-duplicate nodes are literally the same thing... Graph audit
  // should return both"). One button runs both deterministic (no-AI) checks together: redundant-
  // edge detection and near-duplicate node detection. Advisory only throughout. ──────────────────
  auditingGraph = signal(false);
  auditError = signal('');
  auditRun = signal(false);
  redundantEdgeSuggestions = signal<GraphChangeSuggestion[]>([]);
  nearDuplicateSuggestions = signal<NearDuplicateNodeSuggestion[]>([]);

  runGraphAudit(): void {
    this.auditingGraph.set(true);
    this.auditError.set('');
    forkJoin({
      redundantEdges: this.api.getRedundantEdgeSuggestions(),
      nearDuplicates: this.api.getNearDuplicateSuggestions(),
    }).subscribe({
      next: r => {
        this.auditingGraph.set(false);
        this.auditRun.set(true);
        this.redundantEdgeSuggestions.set(r.redundantEdges.suggestions);
        this.nearDuplicateSuggestions.set(r.nearDuplicates.suggestions);
      },
      error: err => {
        this.auditingGraph.set(false);
        this.auditError.set(err?.error?.error ?? 'Could not run the graph audit.');
      },
    });
  }

  dismissRedundantEdgeSuggestion(index: number): void {
    this.redundantEdgeSuggestions.update(list => list.filter((_, i) => i !== index));
  }

  removeRedundantEdge(suggestion: GraphChangeSuggestion, index: number): void {
    const edge = suggestion.proposedEdgesToRemove[0];
    if (!edge) return;
    this.api.removeSkillGraphPrerequisite(edge.nodeId, edge.prerequisiteNodeId).subscribe({
      next: () => {
        this.dismissRedundantEdgeSuggestion(index);
        this.loadNodes();
        this.loadIsolatedNodes();
      },
      error: err => this.auditError.set(err?.error?.error ?? 'Could not remove this edge.'),
    });
  }

  // ── Near-duplicate suggestions are keyed by node-id pair (not array index) — the AI-confirm
  // result and the pending-merge-confirmation state both need to survive the list re-rendering
  // around them without going stale. ─────────────────────────────────────────────────────────────
  private static duplicateKey(s: NearDuplicateNodeSuggestion): string { return `${s.nodeAId}_${s.nodeBId}`; }

  dismissNearDuplicateSuggestion(suggestion: NearDuplicateNodeSuggestion): void {
    const key = AdminSkillGraphComponent.duplicateKey(suggestion);
    this.nearDuplicateSuggestions.update(list => list.filter(s => AdminSkillGraphComponent.duplicateKey(s) !== key));
    this.aiConfirmResults.update(map => { const next = new Map(map); next.delete(key); return next; });
    if (this.pendingMerge()?.key === key) this.pendingMerge.set(null);
  }

  // User correction (2026-07-24) — merging used to happen on a single click with no way to see
  // what you were about to do or undo it. "Keep A/B" now only stages a confirmation; nothing is
  // called until the admin explicitly confirms.
  pendingMerge = signal<{ key: string; suggestion: NearDuplicateNodeSuggestion; keep: 'A' | 'B' } | null>(null);
  mergeError = signal('');

  stageMerge(suggestion: NearDuplicateNodeSuggestion, keep: 'A' | 'B'): void {
    this.mergeError.set('');
    this.pendingMerge.set({ key: AdminSkillGraphComponent.duplicateKey(suggestion), suggestion, keep });
  }

  cancelMerge(): void {
    this.pendingMerge.set(null);
  }

  confirmMerge(): void {
    const pending = this.pendingMerge();
    if (!pending) return;
    const { suggestion, keep } = pending;
    const keepNodeId = keep === 'A' ? suggestion.nodeAId : suggestion.nodeBId;
    const mergeAwayNodeId = keep === 'A' ? suggestion.nodeBId : suggestion.nodeAId;
    this.api.mergeSkillGraphNodes(keepNodeId, mergeAwayNodeId).subscribe({
      next: () => {
        this.pendingMerge.set(null);
        this.dismissNearDuplicateSuggestion(suggestion);
        this.loadNodes();
        this.loadIsolatedNodes();
      },
      error: err => this.mergeError.set(err?.error?.error ?? 'Could not merge these nodes.'),
    });
  }

  // ── Phase 6.3f — on-demand per-pair AI second opinion. Never called automatically; only when
  // the admin explicitly clicks "Confirm with AI" for one specific pair. Advisory only — the
  // admin still decides whether to merge, same as every other suggestion in this codebase. ───────
  confirmingDuplicateKeys = signal<Set<string>>(new Set());
  aiConfirmResults = signal<Map<string, ConfirmNearDuplicateResponse>>(new Map());

  confirmDuplicateWithAi(suggestion: NearDuplicateNodeSuggestion): void {
    const key = AdminSkillGraphComponent.duplicateKey(suggestion);
    this.confirmingDuplicateKeys.update(set => new Set(set).add(key));
    this.api.confirmNearDuplicate(suggestion.nodeAId, suggestion.nodeBId).subscribe({
      next: r => {
        this.confirmingDuplicateKeys.update(set => { const next = new Set(set); next.delete(key); return next; });
        this.aiConfirmResults.update(map => new Map(map).set(key, r));
      },
      error: err => {
        this.confirmingDuplicateKeys.update(set => { const next = new Set(set); next.delete(key); return next; });
        this.aiConfirmResults.update(map => new Map(map).set(key, {
          success: false, isLikelyDuplicate: null, reasoning: null,
          error: err?.error?.error ?? 'Could not get an AI confirmation.',
        }));
      },
    });
  }

  isDuplicateConfirming(suggestion: NearDuplicateNodeSuggestion): boolean {
    return this.confirmingDuplicateKeys().has(AdminSkillGraphComponent.duplicateKey(suggestion));
  }

  duplicateAiResult(suggestion: NearDuplicateNodeSuggestion): ConfirmNearDuplicateResponse | undefined {
    return this.aiConfirmResults().get(AdminSkillGraphComponent.duplicateKey(suggestion));
  }

  isPendingMergeFor(suggestion: NearDuplicateNodeSuggestion): boolean {
    return this.pendingMerge()?.key === AdminSkillGraphComponent.duplicateKey(suggestion);
  }

  // User correction (2026-07-24) — "there is no way to see the details of those nodes"; both
  // titles in a near-duplicate suggestion now link to their real View page. Safe to navigate away
  // from the audit list now that Back correctly returns to whatever page the admin came from.
  goToNodeView(id: string): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}`);
  }

  // User correction (2026-07-23): Create moved from a slide-over to its own routed page,
  // matching View/Edit's structure exactly (page-header + page-body + section-cards,
  // Save/Cancel bottom-right) — see admin-skill-graph-node-create.component.ts.
  createNode(): void {
    this.router.navigateByUrl('/admin/skill-graph/nodes/create');
  }

  // ── User correction (2026-07-23) — View moved from a slide-over to its own routed page
  // (read-only: no add/edit affordances there at all — those live exclusively on the Edit
  // route). Clicking a Nodes table row now navigates instead of opening a panel in place. ──────
  viewNode(row: SkillGraphNodeListItem): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${row.id}`);
  }

  // User correction (2026-07-24) — the isolated-nodes banner used to tell the admin to "click a
  // node below" with no actual clickable affordance anywhere; each isolated node is now a real
  // link into its View page.
  viewIsolatedNode(node: SkillGraphIsolatedNode): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${node.id}`);
  }

  // Content-coverage merge (2026-07-23) — only the 3 aggregate numbers are used now (for the
  // Nodes card header badge); the per-node list this endpoint also returns is no longer consumed
  // client-side since the Nodes table itself now carries linkedModuleCount per row.
  loadContentCoverage(): void {
    this.contentCoverageLoading.set(true);
    this.contentCoverageError.set('');
    this.api.getSkillGraphContentCoverage().subscribe({
      next: r => {
        this.totalApprovedNodes.set(r.totalApprovedNodes);
        this.nodesWithContent.set(r.nodesWithContent);
        this.contentCoverageLoading.set(false);
      },
      error: err => {
        this.contentCoverageError.set(err?.error?.error ?? 'Could not load content coverage.');
        this.contentCoverageLoading.set(false);
      },
    });
  }

  retagModules(): void {
    this.retagPending.set(true);
    this.retagStatus.set('');
    this.retagError.set('');
    this.api.retagSkillGraphModules().subscribe({
      next: r => {
        this.retagPending.set(false);
        const totalMatched = r.results.reduce((sum, m) => sum + m.matchedCount, 0);
        const remaining = `${r.remainingUntaggedModuleCount} untagged Module(s) remain.`;
        this.retagStatus.set(
          r.sweptCount === 0
            ? `No untagged approved Modules found. ${remaining}`
            : `Swept ${r.sweptCount} Module(s), applied ${totalMatched} node link(s). ${remaining}`);
        this.loadContentCoverage();
        this.loadNodes();
      },
      error: err => {
        this.retagPending.set(false);
        this.retagError.set(err?.error?.error ?? 'Re-tagging failed.');
      },
    });
  }

  private loadTaxonomy(): void {
    this.api.getSkillGraphTaxonomy().subscribe({
      next: t => this.taxonomy.set(t),
      error: () => { /* dropdowns just stay empty; not fatal to the rest of the page */ },
    });
  }

  loadCoverage(): void {
    this.coverageLoading.set(true);
    this.coverageError.set('');
    this.api.getSkillGraphCoverage().subscribe({
      next: r => { this.coverage.set(r.matrix); this.coverageLoading.set(false); },
      error: err => {
        this.coverageError.set(err?.error?.error ?? 'Could not load coverage.');
        this.coverageLoading.set(false);
      },
    });
  }

  loadNodes(): void {
    this.nodesLoading.set(true);
    this.nodesError.set('');
    this.api.getSkillGraphNodes({
      cefrLevel: this.filterCefrLevel() || undefined,
      skill: this.filterSkill() || undefined,
      reviewStatus: this.filterReviewStatus() || undefined,
      search: this.filterSearch() || undefined,
      contextTag: this.filterContextTag() || undefined,
      focusTag: this.filterFocusTag() || undefined,
      page: this.nodesPage(),
      pageSize: this.nodesPageSize,
    }).subscribe({
      next: r => {
        this.nodes.set(r.items);
        this.nodesTotalPages.set(r.totalPages);
        this.nodesTotalCount.set(r.totalCount);
        this.nodesLoading.set(false);
      },
      error: err => {
        this.nodesError.set(err?.error?.error ?? 'Could not load nodes.');
        this.nodesLoading.set(false);
      },
    });
  }

  onFilterChange(): void {
    this.nodesPage.set(1);
    this.selectedIds.set(new Set());
    this.loadNodes();
  }

  // Sprint 14.6 — filters are now a table feature (sp-admin-table's [filters]/(filterChange)),
  // rendered in the same toolbar row as the Bulk edit toggle, instead of a hand-authored filter bar.
  nodesFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'cefrLevel', label: 'CEFR level', options: this.cefrLevelOptions(), value: this.filterCefrLevel(), placeholder: 'All' },
    { key: 'skill', label: 'Skill', options: this.skillOptions(), value: this.filterSkill(), placeholder: 'All' },
    { key: 'reviewStatus', label: 'Review status', options: this.reviewStatusOptions, value: this.filterReviewStatus(), placeholder: 'All' },
    { key: 'contextTag', label: 'Context tag', options: this.contextTagOptions(), value: this.filterContextTag(), placeholder: 'All' },
    { key: 'focusTag', label: 'Focus tag', options: this.focusTagOptions(), value: this.filterFocusTag(), placeholder: 'All' },
  ]);

  onNodesFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'cefrLevel') this.filterCefrLevel.set(event.value);
    else if (event.key === 'skill') this.filterSkill.set(event.value);
    else if (event.key === 'reviewStatus') this.filterReviewStatus.set(event.value);
    else if (event.key === 'contextTag') this.filterContextTag.set(event.value);
    else if (event.key === 'focusTag') this.filterFocusTag.set(event.value);
    this.onFilterChange();
  }

  onNodesSearchChange(value: string): void {
    this.filterSearch.set(value);
    this.onFilterChange();
  }

  onNodesPageChange(page: number): void {
    this.nodesPage.set(page);
    this.loadNodes();
  }

  runDraft(): void {
    if (!this.draftCefrLevel || !this.draftSkill) {
      this.draftError.set('Choose a CEFR level and skill.');
      return;
    }
    this.draftPending.set(true);
    this.draftStatus.set('');
    this.draftError.set('');
    this.api.draftSkillGraph(this.draftCefrLevel, this.draftSkill).subscribe({
      next: r => {
        this.draftPending.set(false);
        if (!r.queued) {
          this.draftError.set(r.error ?? 'Drafting failed.');
          return;
        }
        this.draftStatus.set(
          `Drafted ${r.createdCount} node(s)` +
          (r.droppedEdgeCount ? `, dropped ${r.droppedEdgeCount} edge(s) that would cycle` : '') + '.');
        this.loadNodes();
        this.loadCoverage();
      },
      error: err => {
        this.draftPending.set(false);
        this.draftError.set(err?.error?.error ?? 'Drafting failed.');
      },
    });
  }

  draftForGap(entry: SkillGraphCoverageEntry): void {
    this.draftCefrLevel = entry.cefrLevel;
    this.draftSkill = entry.skill;
    this.runDraft();
  }

  clearSelection(): void {
    this.selectedIds.set(new Set());
    this.nodesTableRef?.clearSelection();
  }

  batchApprove(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    this.batchPending.set(true);
    this.batchStatus.set('');
    this.batchError.set('');
    this.api.batchApproveSkillGraphNodes(ids).subscribe({
      next: r => {
        this.batchPending.set(false);
        this.batchStatus.set(`Approved ${r.succeeded} of ${r.requestedCount}.`);
        this.clearSelection();
        this.loadNodes();
        this.loadCoverage();
      },
      error: err => {
        this.batchPending.set(false);
        this.batchError.set(err?.error?.error ?? 'Approve failed.');
      },
    });
  }

  batchReject(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) return;
    if (!this.rejectReason.trim()) {
      this.batchError.set('A rejection reason is required.');
      return;
    }
    this.batchPending.set(true);
    this.batchStatus.set('');
    this.batchError.set('');
    this.api.batchRejectSkillGraphNodes(ids, this.rejectReason.trim()).subscribe({
      next: r => {
        this.batchPending.set(false);
        this.batchStatus.set(`Rejected ${r.succeeded} of ${r.requestedCount}.`);
        this.rejectReason = '';
        this.clearSelection();
        this.loadNodes();
        // Skill Graph rebuild Phase 6.3b — batch-presented, advisory only: append this call's
        // reconnect groups to whatever's already showing rather than replacing it, since an admin
        // might reject in more than one batch before reviewing suggestions.
        if (r.reconnectSuggestions.length > 0) {
          this.reconnectSuggestionGroups.update(list => [...list, ...r.reconnectSuggestions]);
        }
      },
      error: err => {
        this.batchPending.set(false);
        this.batchError.set(err?.error?.error ?? 'Reject failed.');
      },
    });
  }

  // ── Skill Graph rebuild Phase 6.3b (2026-07-23) — reject-triggered reconnect suggestions.
  // Advisory only: "Reconnect" is a real addSkillGraphPrerequisite call the admin explicitly
  // triggers per suggestion; "Dismiss" just drops it from this list. ────────────────────────────
  reconnectSuggestionGroups = signal<RejectReconnectGroup[]>([]);
  reconnectError = signal('');

  dismissReconnectGroup(groupIndex: number): void {
    this.reconnectSuggestionGroups.update(list => list.filter((_, i) => i !== groupIndex));
  }

  dismissReconnectSuggestion(groupIndex: number, edgeIndex: number): void {
    this.reconnectSuggestionGroups.update(list => list.map((g, i) => {
      if (i !== groupIndex) return g;
      const remaining = g.suggestedReconnects.filter((_, ei) => ei !== edgeIndex);
      return { ...g, suggestedReconnects: remaining };
    }).filter(g => g.suggestedReconnects.length > 0));
  }

  acceptReconnectSuggestion(groupIndex: number, edgeIndex: number): void {
    const group = this.reconnectSuggestionGroups()[groupIndex];
    const edge = group?.suggestedReconnects[edgeIndex];
    if (!edge) return;
    this.reconnectError.set('');
    this.api.addSkillGraphPrerequisite(edge.nodeId, edge.prerequisiteNodeId).subscribe({
      next: r => {
        this.dismissReconnectSuggestion(groupIndex, edgeIndex);
        // Phase 6.3e — this add-prerequisite call can itself trigger 6.3a's inline redundant-edge
        // check (reconnecting A->C after B is rejected can make some OTHER edge redundant); surface
        // it in the same "Graph audit" list rather than silently discarding it.
        if (r.suggestions.length > 0) this.redundantEdgeSuggestions.update(list => [...list, ...r.suggestions]);
        this.loadNodes();
        this.loadIsolatedNodes();
      },
      error: err => this.reconnectError.set(err?.error?.error ?? 'Could not add this reconnect.'),
    });
  }

  reviewStatusTone(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    switch (status) {
      case 'Approved': return 'success';
      case 'PendingReview': return 'warning';
      case 'Rejected': return 'danger';
      default: return 'neutral';
    }
  }
}

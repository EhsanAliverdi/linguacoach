import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TreeNode } from 'primeng/api';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCoverageHeatmapComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminHeatmapCell,
  SpAdminHeatmapColumn,
  SpAdminHeatmapRow,
  SpAdminHelpIconComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionHeaderComponent,
  SpAdminSelectComponent,
  SpAdminTableColumn,
  SpAdminTableFilter,
  SpAdminTreeTableComponent,
} from '../../../design-system/admin';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListItem,
  SkillGraphCoverageEntry,
  SkillGraphNode,
  SkillGraphEdge,
  GraphChangeSuggestion,
  RejectReconnectGroup,
  SkillGraphBatchRejectConfirmationRequired,
} from '../../../core/models/admin.models';
import { SpAdminGraphCardComponent } from '../../../design-system/admin/components/graph-card/sp-admin-graph-card.component';
import { SpAdminSkillGraphVizComponent } from './skill-graph-viz/sp-admin-skill-graph-viz.component';
import { SpAdminSkillGraphNodeDetailsComponent } from './node-details-slide-over/sp-admin-skill-graph-node-details.component';

@Component({
  selector: 'app-admin-skill-graph',
  standalone: true,
  templateUrl: './admin-skill-graph.component.html',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCoverageHeatmapComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminHelpIconComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionHeaderComponent,
    SpAdminSelectComponent,
    SpAdminGraphCardComponent,
    SpAdminSkillGraphVizComponent,
    SpAdminSkillGraphNodeDetailsComponent,
    SpAdminTreeTableComponent,
  ],
})
export class AdminSkillGraphComponent implements OnInit {
  constructor(private api: AdminApiService, private router: Router) {}

  // User correction (2026-07-24) — the tag-issues banner, the isolated-nodes banner, and the
  // merged "Graph audit" (redundant edges + near-duplicate nodes) card used to live directly on
  // this page; all moved to a dedicated /admin/skill-graph/audit page so this list page stays
  // focused on browsing/approving/rejecting nodes. This is just the entry point.
  goToAuditPage(): void {
    this.router.navigateByUrl('/admin/skill-graph/audit');
  }

  // ── Sprint 13 — Nodes/Graph view toggle. Skill Graph rebuild Phase 4 (2026-07-27) — the
  // flat paginated Table and the hand-rolled Tree (added earlier this same phase) were confusing
  // as two separate views of the same data (user feedback); replaced with a single PrimeNG
  // TreeTable ("Nodes") that IS the hierarchy — a container row expands in place to show its leaf
  // children, everything else (search/filter/pagination/bulk actions) unchanged. ─────────────────
  viewMode = signal<'nodes' | 'graph'>('nodes');
  graphLoading = signal(false);
  graphError = signal('');
  graphNodes = signal<SkillGraphNode[]>([]);
  graphEdges = signal<SkillGraphEdge[]>([]);
  graphLoaded = false;
  selectedGraphNode = signal<SkillGraphNode | null>(null);

  // Filter gate (2026-07-30) — with 14,070+ nodes, drawing everything at once is neither useful
  // nor scalable, so the Graph tab now requires picking a CEFR level AND a skill first (e.g. "A1"
  // + "Grammar") before it fetches or renders anything. Selecting either dropdown re-fetches.
  graphFilterCefrLevel = signal('');
  graphFilterSkill = signal('');
  graphFilterReady = computed(() => !!this.graphFilterCefrLevel() && !!this.graphFilterSkill());

  // Topical-hierarchy drill-down (2026-07-30) — empty means the root CEFR+skill-scoped view;
  // drilling into a container pushes it here and re-fetches by parentNodeId (which crosses CEFR
  // boundaries — see AdminSkillGraphController.GetGraph — so it's a separate fetch, not a
  // client-side filter of the root batch).
  graphBreadcrumb = signal<SkillGraphNode[]>([]);

  // Per-node context menu → Details slide-over (2026-07-31) — View/Edit navigate to the existing
  // routed pages (/admin/skill-graph/nodes/:id[/edit]), matching this app's 2026-07-23 decision to
  // keep those as full pages, not slide-overs. Details is the one genuinely new slide-over, scoped
  // to "peek without leaving the graph."
  detailsNodeId = signal<string | null>(null);
  detailsOpen = signal(false);

  onGraphViewNode(node: SkillGraphNode): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${node.id}`);
  }

  onGraphEditNode(node: SkillGraphNode): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${node.id}/edit`);
  }

  onGraphDetailsNode(node: SkillGraphNode): void {
    this.detailsNodeId.set(node.id);
    this.detailsOpen.set(true);
  }

  closeDetails(): void {
    this.detailsOpen.set(false);
  }

  viewNodeById(id: string): void {
    this.detailsOpen.set(false);
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}`);
  }

  editNodeById(id: string): void {
    this.detailsOpen.set(false);
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}/edit`);
  }

  setViewMode(mode: 'nodes' | 'graph'): void {
    this.viewMode.set(mode);
  }

  onGraphFilterChange(): void {
    this.selectedGraphNode.set(null);
    this.graphBreadcrumb.set([]);
    if (this.graphFilterReady()) this.loadGraph();
  }

  loadGraph(): void {
    if (!this.graphFilterReady()) return;
    this.graphLoading.set(true);
    this.graphError.set('');
    this.api.getSkillGraph(this.graphFilterCefrLevel(), this.graphFilterSkill(), undefined, true).subscribe({
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

  onDrillInto(node: SkillGraphNode): void {
    this.selectedGraphNode.set(null);
    this.graphBreadcrumb.set([...this.graphBreadcrumb(), node]);
    this.loadGraphChildren(node.id);
  }

  // Jump to a breadcrumb crumb (index -1 = back to the CEFR+skill root view).
  jumpToBreadcrumb(index: number): void {
    this.selectedGraphNode.set(null);
    if (index < 0) {
      this.graphBreadcrumb.set([]);
      this.loadGraph();
      return;
    }
    const trail = this.graphBreadcrumb().slice(0, index + 1);
    this.graphBreadcrumb.set(trail);
    this.loadGraphChildren(trail[trail.length - 1].id);
  }

  private loadGraphChildren(parentNodeId: string): void {
    this.graphLoading.set(true);
    this.graphError.set('');
    // User correction (2026-07-31) — the CEFR filter must stay active through drill-down too, not
    // just at the root. A container can (and often does — e.g. every CEFR Companion Volume scale
    // container) have children at other levels; those are now correctly hidden while a level filter
    // is selected, exactly like the Nodes table already behaved.
    this.api.getSkillGraph(this.graphFilterCefrLevel(), this.graphFilterSkill(), parentNodeId).subscribe({
      next: r => {
        this.graphNodes.set(r.nodes);
        this.graphEdges.set(r.edges);
        this.graphLoading.set(false);
      },
      error: err => {
        this.graphError.set(err?.error?.error ?? 'Could not load this node’s children.');
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

  // ── Nodes tree table (Skill Graph rebuild Phase 4, 2026-07-27) ────────────────────────────
  // A single PrimeNG TreeTable IS the hierarchy: root rows are server-paginated (lazy-loaded on
  // page/filter change), a container row's children are fetched only when it's expanded — no
  // separate flat/tree views, no full-hierarchy fetch. While a search term is active, results are
  // shown flat (every row `leaf:true`, no expand arrows) rather than grouped — a matched leaf
  // buried inside an unexpanded container would otherwise be invisible; this is the same "search
  // flattens hierarchy" convention many tree UIs use, simpler than reconstructing partial trees.
  nodesLoading = signal(true);
  nodesError = signal('');
  ttNodes = signal<TreeNode<SkillGraphNodeListItem>[]>([]);
  ttFirst = signal(0);
  readonly ttRows = 25;
  ttTotalRecords = signal(0);
  ttSelection = signal<TreeNode<SkillGraphNodeListItem>[]>([]);

  filterCefrLevel = signal('');
  filterSkill = signal('');
  filterReviewStatus = signal('');
  // Phase 6.1 — free-text search + ContextTag/FocusTag filters.
  filterSearch = signal('');
  filterContextTag = signal('');
  filterFocusTag = signal('');
  // Skill Graph rebuild Phase 4 (2026-07-27, user follow-up) — "Has children" filter, '' = All.
  filterHasChildren = signal('');
  readonly reviewStatusOptions = [
    { value: 'PendingReview', label: 'Pending review' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' },
  ];
  readonly hasChildrenOptions = [
    { value: 'true', label: 'Containers only' },
    { value: 'false', label: 'Leaves/standalone only' },
  ];

  // Same shape/convention as the Table view used before Phase 4 — sp-admin-tree-table renders
  // these in the exact same toolbar row sp-admin-table does, for visual parity across the admin.
  nodesFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'cefrLevel', label: 'CEFR level', options: this.cefrLevelOptions(), value: this.filterCefrLevel(), placeholder: 'All' },
    { key: 'skill', label: 'Skill', options: this.skillOptions(), value: this.filterSkill(), placeholder: 'All' },
    { key: 'reviewStatus', label: 'Review status', options: this.reviewStatusOptions, value: this.filterReviewStatus(), placeholder: 'All' },
    { key: 'contextTag', label: 'Context tag', options: this.contextTagOptions(), value: this.filterContextTag(), placeholder: 'All' },
    { key: 'focusTag', label: 'Focus tag', options: this.focusTagOptions(), value: this.filterFocusTag(), placeholder: 'All' },
    { key: 'hasChildren', label: 'Has children', options: this.hasChildrenOptions, value: this.filterHasChildren(), placeholder: 'All' },
  ]);

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

  selectedIds = signal<Set<string>>(new Set());
  hasSelection = computed(() => this.selectedIds().size > 0);

  private toTreeNode(item: SkillGraphNodeListItem, flat: boolean): TreeNode<SkillGraphNodeListItem> {
    return { key: item.id, data: item, leaf: flat || item.childCount === 0 };
  }

  onTtSelectionChange(selection: TreeNode<SkillGraphNodeListItem> | TreeNode<SkillGraphNodeListItem>[] | null): void {
    const nodes = Array.isArray(selection) ? selection : selection ? [selection] : [];
    this.ttSelection.set(nodes);
    this.selectedIds.set(new Set(nodes.map(n => n.data!.id)));
  }

  // Skill Graph pipeline audit (2026-07-24, Bug #1) — fast, client-side-only heads-up computed
  // from the already-loaded rows; purely informational. The real confirmation gate is server-side
  // (see batchReject()/pendingRejectConfirmation below) since another admin tab or a direct API
  // call could change a node's status between page-load and this click.
  selectedApprovedCount = computed(() =>
    this.ttSelection().filter(n => n.data?.reviewStatus === 'Approved').length);

  // Container/leaf hierarchy (2026-07-27) — "Select subtree" on an expanded container row seeds
  // the same selectedIds/ttSelection the toolbar's Approve/Reject-selected buttons already read,
  // so those existing (already confirmation-gated) actions apply to the whole subtree at once.
  selectSubtree(container: TreeNode<SkillGraphNodeListItem>): void {
    const nodes = [container, ...(container.children ?? [])];
    this.onTtSelectionChange(nodes);
  }

  batchPending = signal(false);
  batchStatus = signal('');
  batchError = signal('');
  rejectReason = '';

  // Skill Graph pipeline audit (2026-07-24, Bug #1) — set when the backend reports
  // requiresConfirmation (the batch includes a currently-Approved node); opens the confirm modal.
  // Holds the ids/reason the confirm click will resubmit with confirm:true.
  pendingRejectConfirmation = signal<SkillGraphBatchRejectConfirmationRequired | null>(null);
  private pendingRejectIds: string[] = [];
  private pendingRejectReason = '';

  ngOnInit(): void {
    this.loadTaxonomy();
    this.loadCoverage();
    // Skill Graph rebuild Phase 4 (2026-07-27) — driven explicitly here rather than relying on
    // the TreeTable's own lazyLoadOnInit, so the initial fetch is deterministic/testable the same
    // way every other section on this page already is; `[lazyLoadOnInit]="false"` in the template.
    this.loadNodes(1);
    this.loadContentCoverage();
  }

  // Phase 6.3e — inline redundant-edge suggestions surfaced from accepting a reconnect below
  // (reconnecting A->C after B is rejected can itself make some OTHER edge redundant). The full
  // "Graph audit" (on-demand run + near-duplicate detection) moved to /admin/skill-graph/audit;
  // this stays here since it's tied directly to the Reconnect action on this page.
  redundantEdgeSuggestions = signal<GraphChangeSuggestion[]>([]);
  redundantEdgeSuggestionError = signal('');

  dismissRedundantEdgeSuggestion(index: number): void {
    this.redundantEdgeSuggestions.update(list => list.filter((_, i) => i !== index));
  }

  removeRedundantEdge(suggestion: GraphChangeSuggestion, index: number): void {
    const edge = suggestion.proposedEdgesToRemove[0];
    if (!edge) return;
    this.api.removeSkillGraphPrerequisite(edge.nodeId, edge.prerequisiteNodeId).subscribe({
      next: () => {
        this.dismissRedundantEdgeSuggestion(index);
        this.loadNodes(this.currentPage());
      },
      error: err => this.redundantEdgeSuggestionError.set(err?.error?.error ?? 'Could not remove this edge.'),
    });
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
        this.loadNodes(this.currentPage());
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

  // Root-level rows: server-paginated, lazy-loaded by the TreeTable itself (page/init/filter
  // changes all route through here). `page` is 1-based to match every other admin endpoint's
  // convention; PrimeNG's own `first`/`rows` (0-based offset) is converted at the call site.
  loadNodes(page: number): void {
    this.nodesLoading.set(true);
    this.nodesError.set('');
    const searching = !!this.filterSearch().trim();
    this.api.getSkillGraphNodes({
      cefrLevel: this.filterCefrLevel() || undefined,
      skill: this.filterSkill() || undefined,
      reviewStatus: this.filterReviewStatus() || undefined,
      search: this.filterSearch() || undefined,
      contextTag: this.filterContextTag() || undefined,
      focusTag: this.filterFocusTag() || undefined,
      hasChildren: this.filterHasChildren() === '' ? undefined : this.filterHasChildren() === 'true',
      topLevelOnly: !searching,
      page,
      pageSize: this.ttRows,
    }).subscribe({
      next: r => {
        this.ttNodes.set(r.items.map(item => this.toTreeNode(item, searching)));
        this.ttTotalRecords.set(r.totalCount);
        this.nodesLoading.set(false);
      },
      error: err => {
        this.nodesError.set(err?.error?.error ?? 'Could not load nodes.');
        this.nodesLoading.set(false);
      },
    });
  }

  // Pagination is driven by sp-admin-tree-table's own footer (sp-admin-pagination), matching
  // every sp-admin-table-based page's convention, not PrimeNG's built-in paginator — so this reads
  // a page number directly rather than the offset-based onLazyLoad event p-treeTable would emit.
  ttTotalPages = computed(() => Math.max(1, Math.ceil(this.ttTotalRecords() / this.ttRows)));

  onNodesPageChange(page: number): void {
    this.ttFirst.set((page - 1) * this.ttRows);
    this.loadNodes(page);
  }

  // Container/leaf hierarchy (2026-07-27) — fetches one container's leaf children only when it's
  // expanded, never as part of the root-level fetch. Cached on the node itself (PrimeNG TreeTable
  // convention — re-expanding doesn't re-fetch) until the next full reload (a real filter change
  // always calls loadNodes(), which rebuilds ttNodes with brand-new TreeNode wrappers that have no
  // cached children, so a re-expand after a filter change always re-fetches under the new filters).
  //
  // User correction (2026-07-27): the children fetch originally only sent `parentNodeId` — a
  // container's children ignored every other active filter (CEFR/skill/status/tags/search), so
  // e.g. filtering to Approved-only still showed PendingReview children on expand. Now sends the
  // same filter set the root-level fetch uses.
  onTtNodeExpand(node: TreeNode<SkillGraphNodeListItem>): void {
    if (node.children || !node.data) return;
    node.loading = true;
    this.ttNodes.set([...this.ttNodes()]);
    this.api.getSkillGraphNodes({
      cefrLevel: this.filterCefrLevel() || undefined,
      skill: this.filterSkill() || undefined,
      reviewStatus: this.filterReviewStatus() || undefined,
      contextTag: this.filterContextTag() || undefined,
      focusTag: this.filterFocusTag() || undefined,
      parentNodeId: node.data.id,
      pageSize: 200,
    }).subscribe({
      next: r => {
        node.children = r.items.map(item => this.toTreeNode(item, false));
        node.loading = false;
        this.ttNodes.set([...this.ttNodes()]);
      },
      error: err => {
        node.loading = false;
        this.nodesError.set(err?.error?.error ?? 'Could not load this container\'s children.');
        this.ttNodes.set([...this.ttNodes()]);
      },
    });
  }

  onFilterChange(): void {
    this.ttFirst.set(0);
    this.selectedIds.set(new Set());
    this.ttSelection.set([]);
    this.loadNodes(1);
  }

  onNodesFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'cefrLevel') this.filterCefrLevel.set(event.value);
    else if (event.key === 'skill') this.filterSkill.set(event.value);
    else if (event.key === 'reviewStatus') this.filterReviewStatus.set(event.value);
    else if (event.key === 'contextTag') this.filterContextTag.set(event.value);
    else if (event.key === 'focusTag') this.filterFocusTag.set(event.value);
    else if (event.key === 'hasChildren') this.filterHasChildren.set(event.value);
    this.onFilterChange();
  }

  onNodesSearchChange(value: string): void {
    this.filterSearch.set(value);
    this.onFilterChange();
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
        this.loadNodes(this.currentPage());
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
    this.ttSelection.set([]);
  }

  currentPageForFooter(): number {
    return Math.floor(this.ttFirst() / this.ttRows) + 1;
  }

  private currentPage(): number {
    return this.currentPageForFooter();
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
        this.loadNodes(this.currentPage());
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
    this.runBatchReject(ids, this.rejectReason.trim(), false);
  }

  // Skill Graph pipeline audit (2026-07-24, Bug #1) — re-submits the same ids/reason held from the
  // gated call, this time with confirm:true, so the reject actually applies.
  confirmBatchReject(): void {
    this.runBatchReject(this.pendingRejectIds, this.pendingRejectReason, true);
  }

  cancelBatchReject(): void {
    // Also fires from the modal's backdrop-click/Escape/X-close, not just the footer Cancel
    // button — ignore all of those while a confirm request is in flight so the modal can't be
    // dismissed out from under a pending mutation.
    if (this.batchPending()) return;
    this.pendingRejectConfirmation.set(null);
    this.pendingRejectIds = [];
    this.pendingRejectReason = '';
  }

  private runBatchReject(ids: string[], reason: string, confirm: boolean): void {
    this.batchPending.set(true);
    this.batchStatus.set('');
    this.batchError.set('');
    this.api.batchRejectSkillGraphNodes(ids, reason, confirm).subscribe({
      next: r => {
        this.batchPending.set(false);
        if (r.requiresConfirmation) {
          // Nothing was mutated server-side — hold the ids/reason and let the admin decide.
          this.pendingRejectIds = ids;
          this.pendingRejectReason = reason;
          this.pendingRejectConfirmation.set(r);
          return;
        }
        this.pendingRejectConfirmation.set(null);
        this.pendingRejectIds = [];
        this.pendingRejectReason = '';
        this.batchStatus.set(`Rejected ${r.succeeded} of ${r.requestedCount}.`);
        this.rejectReason = '';
        this.clearSelection();
        this.loadNodes(this.currentPage());
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
        // it here rather than silently discarding it.
        if (r.suggestions.length > 0) this.redundantEdgeSuggestions.update(list => [...list, ...r.suggestions]);
        this.loadNodes(this.currentPage());
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

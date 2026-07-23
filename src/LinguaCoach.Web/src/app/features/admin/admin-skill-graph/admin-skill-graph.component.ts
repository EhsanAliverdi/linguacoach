import { Component, OnInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminMultiSelectComponent,
  SpAdminMultiSelectOption,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListItem,
  SkillGraphCoverageEntry,
  SkillGraphNode,
  SkillGraphEdge,
  SkillGraphIsolatedNode,
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
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminMultiSelectComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
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

  // ── Editability audit (2026-07-23) — a shared "search all nodes" backs both the Create
  // panel's and the node detail panel's prerequisite pickers. Loaded once per panel open — cheap
  // at the current graph size (a few hundred nodes); Phase 6 replaces this with a real
  // server-side search endpoint. ──────────────────────────────────────────────────────────────
  private allNodesForPicker = signal<SkillGraphNodeListItem[]>([]);

  // Reusable design-system multi-select (2026-07-23) — feeds the same picker options to the
  // Create panel's prerequisite/unlock pickers and the node detail/Edit page's add-prerequisite/
  // add-unlock pickers, replacing 3 duplicated "search input + button list" implementations.
  pickerOptions = computed<SpAdminMultiSelectOption[]>(() =>
    this.allNodesForPicker().map(n => ({ value: n.id, label: n.title, sublabel: `${n.cefrLevel} · ${n.skill}` })));

  private loadNodesForPicker(): void {
    this.api.getSkillGraphNodes({ pageSize: 500 }).subscribe({
      next: r => this.allNodesForPicker.set(r.items),
      error: () => this.allNodesForPicker.set([]),
    });
  }

  // ── Create node UX audit (2026-07-23) — Create is a slide-over (matches the node detail
  // panel), not a modal, and lets an admin place the new node in the graph — pick its
  // prerequisites — in the same step as authoring it, instead of create-then-separately-link. ──
  createPanelOpen = signal(false);
  creating = signal(false);
  createError = signal('');
  createTitle = '';
  createDescription = '';
  createCefrLevel = '';
  createSkill = '';
  createSubskill = '';
  createDifficultyBand: number | null = 1;
  createContextTagsDraft = '';
  createFocusTagsDraft = '';
  createPrereqIds: string[] = [];
  // Editability follow-up (2026-07-23) — symmetric direction: existing nodes that this NEW node
  // should become a prerequisite FOR ("what does this unlock?"). A node can have several
  // prerequisites and be the prerequisite for several others — genuine many-to-many both ways.
  createDependentIds: string[] = [];

  createSubskillOptions = computed(() =>
    (this.taxonomy()?.subskillsBySkill?.[this.createSkill] ?? []).map(s => ({ value: s, label: s })));

  openCreateModal(): void {
    this.createTitle = '';
    this.createDescription = '';
    this.createCefrLevel = '';
    this.createSkill = '';
    this.createSubskill = '';
    this.createDifficultyBand = 1;
    this.createContextTagsDraft = '';
    this.createFocusTagsDraft = '';
    this.createPrereqIds = [];
    this.createDependentIds = [];
    this.createError.set('');
    this.createPanelOpen.set(true);
    this.loadNodesForPicker();
  }

  closeCreateModal(): void {
    this.createPanelOpen.set(false);
  }

  private parseTagsDraft(raw: string): string[] {
    return raw.split(',').map(t => t.trim()).filter(t => t.length > 0);
  }

  submitCreateNode(): void {
    if (!this.createTitle.trim() || !this.createDescription.trim() || !this.createCefrLevel || !this.createSkill) {
      this.createError.set('Title, description, CEFR level, and skill are all required.');
      return;
    }
    this.creating.set(true);
    this.createError.set('');
    this.api.createSkillGraphNode({
      title: this.createTitle.trim(),
      description: this.createDescription.trim(),
      cefrLevel: this.createCefrLevel,
      skill: this.createSkill,
      subskill: this.createSubskill.trim() || null,
      difficultyBand: this.createDifficultyBand ?? 1,
      descriptionForAi: null,
      contextTags: this.parseTagsDraft(this.createContextTagsDraft),
      focusTags: this.parseTagsDraft(this.createFocusTagsDraft),
      prerequisiteNodeIds: this.createPrereqIds,
      dependentNodeIds: this.createDependentIds,
    }).subscribe({
      next: r => {
        this.creating.set(false);
        this.createPanelOpen.set(false);
        const droppedCount = r.droppedPrerequisites.length + r.droppedDependents.length;
        if (droppedCount > 0) {
          this.batchStatus.set(`Node created, but ${droppedCount} link(s) could not be made (e.g. would create a cycle).`);
        }
        this.loadNodes();
        this.loadCoverage();
        this.loadIsolatedNodes();
      },
      error: err => { this.creating.set(false); this.createError.set(err.error?.error ?? 'Could not create node.'); },
    });
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
  ]);

  onNodesFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'cefrLevel') this.filterCefrLevel.set(event.value);
    else if (event.key === 'skill') this.filterSkill.set(event.value);
    else if (event.key === 'reviewStatus') this.filterReviewStatus.set(event.value);
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
      },
      error: err => {
        this.batchPending.set(false);
        this.batchError.set(err?.error?.error ?? 'Reject failed.');
      },
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

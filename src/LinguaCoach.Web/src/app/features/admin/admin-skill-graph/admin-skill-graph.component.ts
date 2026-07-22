import { Component, OnInit, signal, computed, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminCoverageHeatmapComponent,
  SpAdminEmptyStateComponent,
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
  SpAdminSlideOverComponent,
  SpAdminTableColumn,
  SpAdminTableComponent,
  SpAdminTableFilter,
} from '../../../design-system/admin';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  SkillGraphTaxonomy,
  SkillGraphNodeListItem,
  SkillGraphCoverageEntry,
  SkillGraphCoverageNode,
  SkillGraphNode,
  SkillGraphEdge,
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
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminCoverageHeatmapComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminHelpIconComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminGraphCardComponent,
    SpAdminSkillGraphVizComponent,
  ],
})
export class AdminSkillGraphComponent implements OnInit {
  constructor(private api: AdminApiService, public bulkRepair: AdminBulkRepairService) {}

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

  // ── Content coverage (Sprint 2, expanded Sprint 14.2) ─────────────────────
  contentCoverageLoading = signal(true);
  contentCoverageError = signal('');
  totalApprovedNodes = signal(0);
  nodesWithContent = signal(0);
  coverageNodes = signal<SkillGraphCoverageNode[]>([]);

  coveragePage = signal(1);
  readonly coveragePageSize = 20;

  // Sprint 14.7 — filtering as a table feature (sp-admin-table's [filters]/(filterChange)).
  // All nodes are already loaded client-side, so filtering narrows coverageNodes() in memory
  // rather than round-tripping to the API.
  coverageFilterCefrLevel = signal('');
  coverageFilterSkill = signal('');
  contentCoverageFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'cefrLevel', label: 'CEFR level', options: this.cefrLevelOptions(), value: this.coverageFilterCefrLevel(), placeholder: 'All' },
    { key: 'skill', label: 'Skill', options: this.skillOptions(), value: this.coverageFilterSkill(), placeholder: 'All' },
  ]);
  onContentCoverageFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'cefrLevel') this.coverageFilterCefrLevel.set(event.value);
    else if (event.key === 'skill') this.coverageFilterSkill.set(event.value);
    this.coveragePage.set(1);
  }
  filteredCoverageNodes = computed(() => {
    const cefr = this.coverageFilterCefrLevel();
    const skill = this.coverageFilterSkill();
    return this.coverageNodes().filter(n =>
      (!cefr || n.cefrLevel === cefr) && (!skill || n.skill === skill));
  });

  coverageTotalPages = computed(() => Math.max(1, Math.ceil(this.filteredCoverageNodes().length / this.coveragePageSize)));
  pagedCoverageNodes = computed(() => {
    const start = (this.coveragePage() - 1) * this.coveragePageSize;
    return this.filteredCoverageNodes().slice(start, start + this.coveragePageSize);
  });
  onCoveragePageChange(page: number): void {
    this.coveragePage.set(page);
  }

  selectedCoverageNode = signal<SkillGraphCoverageNode | null>(null);
  openCoverageNode(node: SkillGraphCoverageNode): void {
    this.selectedCoverageNode.set(node);
  }
  closeCoverageNode(): void {
    this.selectedCoverageNode.set(null);
  }

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
  }

  loadContentCoverage(): void {
    this.contentCoverageLoading.set(true);
    this.contentCoverageError.set('');
    this.api.getSkillGraphContentCoverage().subscribe({
      next: r => {
        this.totalApprovedNodes.set(r.totalApprovedNodes);
        this.nodesWithContent.set(r.nodesWithContent);
        this.coverageNodes.set(r.nodes);
        this.coveragePage.set(1);
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

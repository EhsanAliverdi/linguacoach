import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  SpAdminAlertComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionHeaderComponent,
  SpAdminTableComponent,
  SpAdminTableColumn,
} from '../../../../design-system/admin';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import {
  SkillGraphIsolatedNode,
  GraphChangeSuggestion,
  NearDuplicateNodeSuggestion,
  ConfirmNearDuplicateResponse,
  MergeNodesResponse,
} from '../../../../core/models/admin.models';
import { IssuesSummary, RepairableItemSummary } from '../../../../core/models/admin-repair.models';
import { AdminBulkRepairService } from '../../../../core/services/admin-bulk-repair.service';

const PAGE_SIZE = 20;

/** One row in the "Redundant edges" table — keyed by the edge's own endpoints (not array index),
 *  same discipline as near-duplicate rows, so Dismiss/stage-for-removal/Save survive client-side
 *  search filtering and pagination without going stale. */
interface RedundantEdgeRow {
  key: string;
  suggestion: GraphChangeSuggestion;
  edge: { nodeId: string; nodeTitle: string; prerequisiteNodeId: string; prerequisiteNodeTitle: string };
}

function paginate<T>(items: T[], page: number): T[] {
  const start = (page - 1) * PAGE_SIZE;
  return items.slice(start, start + PAGE_SIZE);
}

function totalPagesFor(count: number): number {
  return Math.max(1, Math.ceil(count / PAGE_SIZE));
}

/**
 * User correction (2026-07-24): the tag-issues banner, the isolated-nodes banner, and the merged
 * "Graph audit" (redundant edges + near-duplicate nodes) card used to live directly on the main
 * Skill Graph list page — moved to a dedicated page instead, reached via a link/summary on the
 * main page, so the main list isn't cluttered with audit findings and actions.
 *
 * Further user corrections: every finding renders as a table row (same convention as the main
 * Nodes table) with a search filter and pagination; redundant-edge removal is staged and requires
 * an explicit "Save changes" the same way Edit's edge changes are staged until Save — a single
 * "Remove edge" click no longer mutates the graph immediately.
 */
@Component({
  selector: 'app-admin-skill-graph-audit',
  standalone: true,
  imports: [
    CommonModule,
    SpAdminAlertComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionHeaderComponent,
    SpAdminTableComponent,
  ],
  templateUrl: './admin-skill-graph-audit.component.html',
})
export class AdminSkillGraphAuditComponent implements OnInit {
  constructor(
    private api: AdminApiService,
    public bulkRepair: AdminBulkRepairService,
    private router: Router,
    private location: Location,
  ) {}

  // User correction (2026-07-24): the process was wrong — the main list page's "Audit" entry
  // point should just navigate here, with every audit running automatically as soon as this page
  // loads (rather than requiring a separate "Run graph audit" click once landed). This page's own
  // top-level control is "Refresh" (re-runs everything again), and each card additionally gets its
  // own per-card refresh for re-running just that one check.
  ngOnInit(): void {
    this.refreshAll();
  }

  // Fires all five independent requests together; each card shows its own loading/error state
  // (there's no single combined completion event to drive one shared spinner).
  refreshAll(): void {
    this.loadNodeIssuesSummary();
    this.loadNodesWithIssues();
    this.loadIsolatedNodes();
    this.loadRedundantEdges();
    this.loadNearDuplicates();
  }

  back(): void {
    this.location.back();
  }

  // ── Missing tags — Sprint 14.1 issue count + bulk/per-row "Fix with AI" ──────────────────────
  nodeIssuesSummary = signal<IssuesSummary | null>(null);
  nodesWithIssues = signal<RepairableItemSummary[]>([]);
  missingTagsSearch = signal('');
  missingTagsPage = signal(1);
  missingTagsColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'actions', label: 'Actions', width: '140px', align: 'right' },
  ];

  filteredNodesWithIssues = computed(() => {
    const q = this.missingTagsSearch().trim().toLowerCase();
    const rows = this.nodesWithIssues();
    return q ? rows.filter(r => r.title.toLowerCase().includes(q)) : rows;
  });

  missingTagsTotalPages = computed(() => totalPagesFor(this.filteredNodesWithIssues().length));
  pagedNodesWithIssues = computed(() => paginate(this.filteredNodesWithIssues(), this.missingTagsPage()));

  setMissingTagsSearch(value: string): void {
    this.missingTagsSearch.set(value);
    this.missingTagsPage.set(1);
  }

  loadingMissingTags = signal(false);

  // Refreshing this card re-runs both its summary count and its full with-issues list.
  refreshMissingTags(): void {
    this.loadNodeIssuesSummary();
    this.loadNodesWithIssues();
  }

  loadNodeIssuesSummary(): void {
    this.api.getSkillGraphNodeIssuesSummary().subscribe({
      next: summary => this.nodeIssuesSummary.set(summary),
      error: () => this.nodeIssuesSummary.set(null),
    });
  }

  loadNodesWithIssues(): void {
    this.loadingMissingTags.set(true);
    this.api.listSkillGraphNodesWithIssues().subscribe({
      next: items => { this.nodesWithIssues.set(items); this.loadingMissingTags.set(false); },
      error: () => { this.nodesWithIssues.set([]); this.loadingMissingTags.set(false); },
    });
  }

  fixAllNodesWithAi(): void {
    this.bulkRepair.run({
      entityLabel: 'Skill Graph Node',
      listWithIssues: () => this.api.listSkillGraphNodesWithIssues(),
      repairOne: id => this.api.repairSkillGraphNode(id),
      onDone: () => {
        this.loadNodeIssuesSummary();
        this.loadNodesWithIssues();
      },
    });
  }

  fixingNodeIds = signal<Set<string>>(new Set());

  isFixingNode(item: RepairableItemSummary): boolean {
    return this.fixingNodeIds().has(item.id);
  }

  fixOneNodeWithAi(item: RepairableItemSummary): void {
    this.fixingNodeIds.update(set => new Set(set).add(item.id));
    this.api.repairSkillGraphNode(item.id).subscribe({
      next: () => {
        this.fixingNodeIds.update(set => { const next = new Set(set); next.delete(item.id); return next; });
        this.loadNodeIssuesSummary();
        this.loadNodesWithIssues();
      },
      error: () => {
        this.fixingNodeIds.update(set => { const next = new Set(set); next.delete(item.id); return next; });
      },
    });
  }

  // ── Isolated nodes — Editability audit (2026-07-23) connectivity metric ──────────────────────
  isolatedNodes = signal<SkillGraphIsolatedNode[]>([]);
  isolatedSearch = signal('');
  isolatedPage = signal(1);
  isolatedColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'key', label: 'Key', muted: true },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'skill', label: 'Skill' },
    { key: 'reviewStatus', label: 'Status' },
    { key: 'actions', label: 'Actions', width: '160px', align: 'right' },
  ];

  filteredIsolatedNodes = computed(() => {
    const q = this.isolatedSearch().trim().toLowerCase();
    const rows = this.isolatedNodes();
    return q ? rows.filter(r => r.title.toLowerCase().includes(q) || r.key.toLowerCase().includes(q)) : rows;
  });

  isolatedTotalPages = computed(() => totalPagesFor(this.filteredIsolatedNodes().length));
  pagedIsolatedNodes = computed(() => paginate(this.filteredIsolatedNodes(), this.isolatedPage()));

  setIsolatedSearch(value: string): void {
    this.isolatedSearch.set(value);
    this.isolatedPage.set(1);
  }

  loadingIsolated = signal(false);

  loadIsolatedNodes(): void {
    this.loadingIsolated.set(true);
    this.api.getIsolatedSkillGraphNodes().subscribe({
      next: r => { this.isolatedNodes.set(r.isolated); this.loadingIsolated.set(false); },
      error: () => { this.isolatedNodes.set([]); this.loadingIsolated.set(false); },
    });
  }

  // Goes straight to the Edit page — "click one below, then use Add prerequisite" is literally
  // this action, so the row's own button skips the intermediate View page.
  editIsolatedNode(node: SkillGraphIsolatedNode): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${node.id}/edit`);
  }

  // ── Skill Graph rebuild Phase 6.3a/6.3c — deterministic (no AI) redundant-edge detection and
  // near-duplicate node detection. Advisory only throughout. User correction (2026-07-24): these
  // used to share one combined "Run graph audit" button; each now runs (and refreshes) on its own,
  // matching every other card's per-card refresh, and both fire automatically on page load. ───────
  redundantEdgeSuggestions = signal<GraphChangeSuggestion[]>([]);
  nearDuplicateSuggestions = signal<NearDuplicateNodeSuggestion[]>([]);
  loadingRedundantEdges = signal(false);
  loadingNearDuplicates = signal(false);
  redundantEdgesLoadError = signal('');
  nearDuplicatesLoadError = signal('');

  loadRedundantEdges(): void {
    this.loadingRedundantEdges.set(true);
    this.redundantEdgesLoadError.set('');
    this.api.getRedundantEdgeSuggestions().subscribe({
      next: r => { this.redundantEdgeSuggestions.set(r.suggestions); this.loadingRedundantEdges.set(false); },
      error: err => {
        this.loadingRedundantEdges.set(false);
        this.redundantEdgesLoadError.set(err?.error?.error ?? 'Could not check for redundant edges.');
      },
    });
  }

  loadNearDuplicates(): void {
    this.loadingNearDuplicates.set(true);
    this.nearDuplicatesLoadError.set('');
    this.api.getNearDuplicateSuggestions().subscribe({
      next: r => { this.nearDuplicateSuggestions.set(r.suggestions); this.loadingNearDuplicates.set(false); },
      error: err => {
        this.loadingNearDuplicates.set(false);
        this.nearDuplicatesLoadError.set(err?.error?.error ?? 'Could not check for near-duplicate nodes.');
      },
    });
  }

  // ── Redundant edges table — User correction (2026-07-24): "Remove edge" used to call the API
  // immediately per click; it now only stages the row for removal (same discipline as Edit's
  // staged edge changes), committed together via an explicit "Save changes" action. ─────────────
  redundantEdgesColumns: SpAdminTableColumn[] = [
    { key: 'from', label: 'From', titleColumn: true },
    { key: 'to', label: 'To' },
    { key: 'reason', label: 'Reason', muted: true },
    { key: 'actions', label: 'Actions', width: '220px', align: 'right' },
  ];
  redundantEdgesSearch = signal('');
  redundantEdgesPage = signal(1);

  private static redundantEdgeKey(edge: { nodeId: string; prerequisiteNodeId: string }): string {
    return `${edge.prerequisiteNodeId}_${edge.nodeId}`;
  }

  private redundantEdgeRows = computed<RedundantEdgeRow[]>(() =>
    this.redundantEdgeSuggestions()
      .map(suggestion => {
        const edge = suggestion.proposedEdgesToRemove[0];
        return edge ? { key: AdminSkillGraphAuditComponent.redundantEdgeKey(edge), suggestion, edge } : null;
      })
      .filter((r): r is RedundantEdgeRow => !!r));

  filteredRedundantEdgeRows = computed(() => {
    const q = this.redundantEdgesSearch().trim().toLowerCase();
    const rows = this.redundantEdgeRows();
    if (!q) return rows;
    return rows.filter(r =>
      r.edge.nodeTitle.toLowerCase().includes(q) || r.edge.prerequisiteNodeTitle.toLowerCase().includes(q));
  });

  redundantEdgesTotalPages = computed(() => totalPagesFor(this.filteredRedundantEdgeRows().length));
  pagedRedundantEdgeRows = computed(() => paginate(this.filteredRedundantEdgeRows(), this.redundantEdgesPage()));

  setRedundantEdgesSearch(value: string): void {
    this.redundantEdgesSearch.set(value);
    this.redundantEdgesPage.set(1);
  }

  dismissRedundantEdgeSuggestion(row: RedundantEdgeRow): void {
    this.redundantEdgeSuggestions.update(list =>
      list.filter(s => AdminSkillGraphAuditComponent.redundantEdgeKey(s.proposedEdgesToRemove[0] ?? row.edge) !== row.key));
    this.stagedRemovalKeys.update(set => { const next = new Set(set); next.delete(row.key); return next; });
  }

  stagedRemovalKeys = signal<Set<string>>(new Set());
  savingRedundantEdges = signal(false);

  isStagedForRemoval(row: RedundantEdgeRow): boolean {
    return this.stagedRemovalKeys().has(row.key);
  }

  toggleStageRemoval(row: RedundantEdgeRow): void {
    this.stagedRemovalKeys.update(set => {
      const next = new Set(set);
      if (next.has(row.key)) next.delete(row.key); else next.add(row.key);
      return next;
    });
  }

  saveRedundantEdgeRemovals(): void {
    const staged = this.redundantEdgeRows().filter(r => this.stagedRemovalKeys().has(r.key));
    if (staged.length === 0) return;
    this.savingRedundantEdges.set(true);
    this.redundantEdgesLoadError.set('');
    forkJoin(staged.map(r => this.api.removeSkillGraphPrerequisite(r.edge.nodeId, r.edge.prerequisiteNodeId))).subscribe({
      next: () => {
        this.savingRedundantEdges.set(false);
        const removedKeys = new Set(staged.map(r => r.key));
        this.redundantEdgeSuggestions.update(list =>
          list.filter(s => {
            const edge = s.proposedEdgesToRemove[0];
            return !edge || !removedKeys.has(AdminSkillGraphAuditComponent.redundantEdgeKey(edge));
          }));
        this.stagedRemovalKeys.set(new Set());
        this.loadIsolatedNodes();
      },
      error: err => {
        this.savingRedundantEdges.set(false);
        this.redundantEdgesLoadError.set(err?.error?.error ?? 'Could not save the staged edge removals.');
      },
    });
  }

  // ── Near-duplicate nodes table ────────────────────────────────────────────────────────────
  nearDuplicatesColumns: SpAdminTableColumn[] = [
    { key: 'nodeA', label: 'Node A', titleColumn: true },
    { key: 'nodeB', label: 'Node B' },
    { key: 'context', label: 'CEFR / Skill' },
    { key: 'similarity', label: 'Similarity' },
    { key: 'actions', label: 'Actions', width: '100px', align: 'right' },
  ];
  nearDuplicatesSearch = signal('');
  nearDuplicatesPage = signal(1);

  filteredNearDuplicates = computed(() => {
    const q = this.nearDuplicatesSearch().trim().toLowerCase();
    const rows = this.nearDuplicateSuggestions();
    return q ? rows.filter(s => s.nodeATitle.toLowerCase().includes(q) || s.nodeBTitle.toLowerCase().includes(q)) : rows;
  });

  nearDuplicatesTotalPages = computed(() => totalPagesFor(this.filteredNearDuplicates().length));
  pagedNearDuplicates = computed(() => paginate(this.filteredNearDuplicates(), this.nearDuplicatesPage()));

  setNearDuplicatesSearch(value: string): void {
    this.nearDuplicatesSearch.set(value);
    this.nearDuplicatesPage.set(1);
  }

  // Suggestions are keyed by node-id pair (not array index) — the AI-confirm result, the pending-
  // merge-confirmation state, and the expanded-row state all need to survive list re-rendering
  // without going stale.
  private static duplicateKey(s: NearDuplicateNodeSuggestion): string { return `${s.nodeAId}_${s.nodeBId}`; }

  expandedDuplicateKeys = signal<Set<string>>(new Set());

  toggleDuplicateExpanded(suggestion: NearDuplicateNodeSuggestion): void {
    const key = AdminSkillGraphAuditComponent.duplicateKey(suggestion);
    this.expandedDuplicateKeys.update(set => {
      const next = new Set(set);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }

  // Bound with an arrow-function field (not a prototype method) so `this` stays correct when
  // sp-admin-table calls it directly as `isRowExpanded(row, index)`.
  isDuplicateRowExpanded = (row: unknown): boolean => {
    const suggestion = row as NearDuplicateNodeSuggestion;
    const key = AdminSkillGraphAuditComponent.duplicateKey(suggestion);
    return this.expandedDuplicateKeys().has(key)
      || this.aiConfirmResults().has(key)
      || this.pendingMerge()?.key === key;
  };

  dismissNearDuplicateSuggestion(suggestion: NearDuplicateNodeSuggestion): void {
    const key = AdminSkillGraphAuditComponent.duplicateKey(suggestion);
    this.nearDuplicateSuggestions.update(list => list.filter(s => AdminSkillGraphAuditComponent.duplicateKey(s) !== key));
    this.aiConfirmResults.update(map => { const next = new Map(map); next.delete(key); return next; });
    this.expandedDuplicateKeys.update(set => { const next = new Set(set); next.delete(key); return next; });
    if (this.pendingMerge()?.key === key) this.pendingMerge.set(null);
  }

  // "Keep A/B" only stages a confirmation; nothing is called until the admin explicitly clicks
  // "Confirm merge" — that click IS the save action for this table (merges are one-at-a-time
  // decisions, unlike redundant edges' batchable removals).
  pendingMerge = signal<{ key: string; suggestion: NearDuplicateNodeSuggestion; keep: 'A' | 'B' } | null>(null);
  mergeError = signal('');
  // Skill Graph pipeline audit (2026-07-24, Bug #2) — surfaces the content-link repoint counts the
  // backend now reports, so a merge that moved/dropped Module tagging isn't invisible to the admin.
  mergeStatus = signal('');

  stageMerge(suggestion: NearDuplicateNodeSuggestion, keep: 'A' | 'B'): void {
    this.mergeError.set('');
    this.mergeStatus.set('');
    this.pendingMerge.set({ key: AdminSkillGraphAuditComponent.duplicateKey(suggestion), suggestion, keep });
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
      next: r => {
        this.pendingMerge.set(null);
        this.mergeStatus.set(this.describeMergeResult(r));
        this.dismissNearDuplicateSuggestion(suggestion);
        this.loadIsolatedNodes();
      },
      error: err => this.mergeError.set(err?.error?.error ?? 'Could not merge these nodes.'),
    });
  }

  private describeMergeResult(r: MergeNodesResponse): string {
    const parts: string[] = [];
    if (r.relinkedModuleCount > 0) parts.push(`${r.relinkedModuleCount} module link(s) moved`);
    if (r.droppedDuplicateModuleLinkCount > 0) parts.push(`${r.droppedDuplicateModuleLinkCount} duplicate link(s) dropped`);
    return parts.length > 0 ? `Merged. ${parts.join(', ')}.` : 'Merged.';
  }

  // ── Phase 6.3f — on-demand per-pair AI second opinion. Never called automatically; only when
  // the admin explicitly clicks "Confirm with AI" for one specific pair. Advisory only — the
  // admin still decides whether to merge, same as every other suggestion in this codebase. ───────
  confirmingDuplicateKeys = signal<Set<string>>(new Set());
  aiConfirmResults = signal<Map<string, ConfirmNearDuplicateResponse>>(new Map());

  confirmDuplicateWithAi(suggestion: NearDuplicateNodeSuggestion): void {
    const key = AdminSkillGraphAuditComponent.duplicateKey(suggestion);
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
    return this.confirmingDuplicateKeys().has(AdminSkillGraphAuditComponent.duplicateKey(suggestion));
  }

  duplicateAiResult(suggestion: NearDuplicateNodeSuggestion): ConfirmNearDuplicateResponse | undefined {
    return this.aiConfirmResults().get(AdminSkillGraphAuditComponent.duplicateKey(suggestion));
  }

  isPendingMergeFor(suggestion: NearDuplicateNodeSuggestion): boolean {
    return this.pendingMerge()?.key === AdminSkillGraphAuditComponent.duplicateKey(suggestion);
  }

  // Both titles in a near-duplicate suggestion link to their real View page. Safe to navigate away
  // from the audit list now that Back correctly returns here afterward (Location.back()).
  goToNodeView(id: string): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${id}`);
  }
}

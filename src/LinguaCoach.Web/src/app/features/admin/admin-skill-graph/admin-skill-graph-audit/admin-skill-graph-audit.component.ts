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
  SpAdminTableComponent,
  SpAdminTableColumn,
} from '../../../../design-system/admin';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import {
  SkillGraphIsolatedNode,
  GraphChangeSuggestion,
  NearDuplicateNodeSuggestion,
  ConfirmNearDuplicateResponse,
} from '../../../../core/models/admin.models';
import { IssuesSummary, RepairableItemSummary } from '../../../../core/models/admin-repair.models';
import { AdminBulkRepairService } from '../../../../core/services/admin-bulk-repair.service';

/** One row in the "Redundant edges" table — carries the original array index alongside the
 *  suggestion/edge so Dismiss/Remove still target the right entry in `redundantEdgeSuggestions`
 *  after client-side search filtering. */
interface RedundantEdgeRow {
  index: number;
  suggestion: GraphChangeSuggestion;
  edge: { nodeId: string; nodeTitle: string; prerequisiteNodeId: string; prerequisiteNodeTitle: string };
}

/**
 * User correction (2026-07-24): the tag-issues banner, the isolated-nodes banner, and the merged
 * "Graph audit" (redundant edges + near-duplicate nodes) card used to live directly on the main
 * Skill Graph list page — the user asked for all of it to move to its own dedicated page instead,
 * reached via a link/summary on the main page, so the main list isn't cluttered with audit
 * findings and actions.
 *
 * Further user correction: every finding should render as a table row (same convention as the
 * main Nodes table) with real action buttons and a search filter, not a plain bulleted list — so
 * an admin can find and fix a specific issue the same way they'd work the Nodes table.
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

  ngOnInit(): void {
    this.loadNodeIssuesSummary();
    this.loadNodesWithIssues();
    this.loadIsolatedNodes();
  }

  back(): void {
    this.location.back();
  }

  // ── Missing tags — Sprint 14.1 issue count + bulk/per-row "Fix with AI" ──────────────────────
  nodeIssuesSummary = signal<IssuesSummary | null>(null);
  nodesWithIssues = signal<RepairableItemSummary[]>([]);
  missingTagsSearch = signal('');
  missingTagsColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'actions', label: 'Actions', width: '160px' },
  ];

  filteredNodesWithIssues = computed(() => {
    const q = this.missingTagsSearch().trim().toLowerCase();
    const rows = this.nodesWithIssues();
    return q ? rows.filter(r => r.title.toLowerCase().includes(q)) : rows;
  });

  loadNodeIssuesSummary(): void {
    this.api.getSkillGraphNodeIssuesSummary().subscribe({
      next: summary => this.nodeIssuesSummary.set(summary),
      error: () => this.nodeIssuesSummary.set(null),
    });
  }

  loadNodesWithIssues(): void {
    this.api.listSkillGraphNodesWithIssues().subscribe({
      next: items => this.nodesWithIssues.set(items),
      error: () => this.nodesWithIssues.set([]),
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
  isolatedColumns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'key', label: 'Key', muted: true },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'skill', label: 'Skill' },
    { key: 'reviewStatus', label: 'Status' },
    { key: 'actions', label: 'Actions', width: '160px' },
  ];

  filteredIsolatedNodes = computed(() => {
    const q = this.isolatedSearch().trim().toLowerCase();
    const rows = this.isolatedNodes();
    return q ? rows.filter(r => r.title.toLowerCase().includes(q) || r.key.toLowerCase().includes(q)) : rows;
  });

  loadIsolatedNodes(): void {
    this.api.getIsolatedSkillGraphNodes().subscribe({
      next: r => this.isolatedNodes.set(r.isolated),
      error: () => this.isolatedNodes.set([]),
    });
  }

  // Goes straight to the Edit page — "click one below, then use Add prerequisite" is literally
  // this action, so the row's own button skips the intermediate View page.
  editIsolatedNode(node: SkillGraphIsolatedNode): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${node.id}/edit`);
  }

  // ── Skill Graph rebuild Phase 6.3a/6.3c, merged into one "Graph audit" section — one button
  // runs both deterministic (no-AI) checks together: redundant-edge detection and near-duplicate
  // node detection. Advisory only throughout. ─────────────────────────────────────────────────
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

  // ── Redundant edges table ─────────────────────────────────────────────────────────────────
  redundantEdgesColumns: SpAdminTableColumn[] = [
    { key: 'from', label: 'From', titleColumn: true },
    { key: 'to', label: 'To' },
    { key: 'reason', label: 'Reason', muted: true },
    { key: 'actions', label: 'Actions', width: '200px' },
  ];
  redundantEdgesSearch = signal('');

  private redundantEdgeRows = computed<RedundantEdgeRow[]>(() =>
    this.redundantEdgeSuggestions()
      .map((suggestion, index) => ({ index, suggestion, edge: suggestion.proposedEdgesToRemove[0] }))
      .filter((r): r is RedundantEdgeRow => !!r.edge));

  filteredRedundantEdgeRows = computed(() => {
    const q = this.redundantEdgesSearch().trim().toLowerCase();
    const rows = this.redundantEdgeRows();
    if (!q) return rows;
    return rows.filter(r =>
      r.edge.nodeTitle.toLowerCase().includes(q) || r.edge.prerequisiteNodeTitle.toLowerCase().includes(q));
  });

  dismissRedundantEdgeSuggestion(index: number): void {
    this.redundantEdgeSuggestions.update(list => list.filter((_, i) => i !== index));
  }

  removeRedundantEdge(suggestion: GraphChangeSuggestion, index: number): void {
    const edge = suggestion.proposedEdgesToRemove[0];
    if (!edge) return;
    this.api.removeSkillGraphPrerequisite(edge.nodeId, edge.prerequisiteNodeId).subscribe({
      next: () => {
        this.dismissRedundantEdgeSuggestion(index);
        this.loadIsolatedNodes();
      },
      error: err => this.auditError.set(err?.error?.error ?? 'Could not remove this edge.'),
    });
  }

  // ── Near-duplicate nodes table ────────────────────────────────────────────────────────────
  nearDuplicatesColumns: SpAdminTableColumn[] = [
    { key: 'nodeA', label: 'Node A', titleColumn: true },
    { key: 'nodeB', label: 'Node B' },
    { key: 'context', label: 'CEFR / Skill' },
    { key: 'similarity', label: 'Similarity' },
    { key: 'actions', label: 'Actions', width: '110px' },
  ];
  nearDuplicatesSearch = signal('');

  filteredNearDuplicates = computed(() => {
    const q = this.nearDuplicatesSearch().trim().toLowerCase();
    const rows = this.nearDuplicateSuggestions();
    return q ? rows.filter(s => s.nodeATitle.toLowerCase().includes(q) || s.nodeBTitle.toLowerCase().includes(q)) : rows;
  });

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

  // "Keep A/B" only stages a confirmation; nothing is called until the admin explicitly confirms.
  pendingMerge = signal<{ key: string; suggestion: NearDuplicateNodeSuggestion; keep: 'A' | 'B' } | null>(null);
  mergeError = signal('');

  stageMerge(suggestion: NearDuplicateNodeSuggestion, keep: 'A' | 'B'): void {
    this.mergeError.set('');
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
      next: () => {
        this.pendingMerge.set(null);
        this.dismissNearDuplicateSuggestion(suggestion);
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

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphEdge, SkillGraphNode, SkillGraphNodeDetail } from '../../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminErrorStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
} from '../../../../design-system/admin';
import { computeGraphNeighborhood, NodeGraphPreviewEdge, NodeGraphPreviewNode, SpAdminNodeGraphPreviewComponent } from '../node-graph-preview/sp-admin-node-graph-preview.component';

/**
 * User correction (2026-07-23): View must be a dedicated page, not a slide-over, and must be
 * strictly read-only — no add/remove/edit affordances at all (those moved exclusively to the
 * Edit route). This page also carries the local graph preview (required-first / this node /
 * unlocks), which Edit also shows but updates live after each mutation.
 *
 * Multi-layer expansion (2026-07-23) — "+ layer"/"- layer" widens/narrows how many hops out from
 * the current node the graph shows, computed via BFS (`computeGraphNeighborhood`) over the whole
 * graph's real edges (loaded once via `getSkillGraph()`), not just the node's own direct
 * prerequisites/dependents.
 */
@Component({
  selector: 'app-admin-skill-graph-node-view',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminErrorStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
    SpAdminNodeGraphPreviewComponent,
  ],
  templateUrl: './admin-skill-graph-node-view.component.html',
})
export class AdminSkillGraphNodeViewComponent implements OnInit {
  nodeId = '';
  loading = signal(true);
  error = signal('');
  item = signal<SkillGraphNodeDetail | null>(null);

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
    return Array.from(neigh.nodeIds).map(id => ({
      id,
      title: id === n.id ? n.title : (byId.get(id)?.title ?? '(unknown)'),
    }));
  });

  graphPreviewEdges = computed<NodeGraphPreviewEdge[]>(() => this.graphNeighborhood()?.edges ?? []);

  increaseGraphLevel(): void { this.graphLevel.update(l => Math.min(l + 1, this.maxGraphLevel)); }
  decreaseGraphLevel(): void { this.graphLevel.update(l => Math.max(l - 1, 1)); }

  constructor(
    private api: AdminApiService,
    private route: ActivatedRoute,
    private router: Router,
    private location: Location,
  ) {}

  ngOnInit(): void {
    this.loadFullGraph();
    // Route reactivity (not just snapshot) — the graph preview lets an admin click a neighbor
    // node and navigate to ITS view page, which re-uses this same routed component instance
    // with a new :id param rather than re-instantiating it.
    this.route.paramMap.subscribe(params => {
      this.nodeId = params.get('id') ?? '';
      if (this.nodeId) this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.graphLevel.set(1);
    this.api.getSkillGraphNode(this.nodeId).subscribe({
      next: item => { this.loading.set(false); this.item.set(item); },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this node.'); },
    });
  }

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

  // User correction (2026-07-24) — this used to hardcode a return to the main list page, so
  // clicking a neighbor node in the graph preview (goToNode, above) and then hitting "Back" would
  // skip past whichever node the admin actually came from. Real browser-history back instead.
  back(): void {
    this.location.back();
  }

  editNode(): void {
    this.router.navigateByUrl(`/admin/skill-graph/nodes/${this.nodeId}/edit`);
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

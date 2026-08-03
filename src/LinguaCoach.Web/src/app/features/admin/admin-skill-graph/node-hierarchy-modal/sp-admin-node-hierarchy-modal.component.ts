import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphNodeDetail } from '../../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
} from '../../../../design-system/admin';

/**
 * Container/leaf hierarchy (2026-07-27) — a lightweight "peek" modal for a node's own container/
 * leaf hierarchy, opened by clicking a node inside the local graph preview on View/Edit (previously
 * a graph-preview click navigated straight away, losing the page the admin was on; this lets them
 * check a neighbor's place in the hierarchy without leaving). Fetches `GET nodes/{id}` fresh on
 * every open (no caching) — hierarchy state is cheap to re-fetch and can change between opens.
 */
@Component({
  selector: 'sp-admin-node-hierarchy-modal',
  standalone: true,
  imports: [CommonModule, SpAdminAlertComponent, SpAdminBadgeComponent, SpAdminButtonComponent, SpAdminLoadingStateComponent, SpAdminModalComponent],
  template: `
    <sp-admin-modal [open]="!!nodeId" [title]="item()?.title ?? 'Node'" size="md" (closed)="closed.emit()">
      @if (loading()) {
        <sp-admin-loading-state message="Loading node" />
      } @else if (error()) {
        <sp-admin-alert variant="error">{{ error() }}</sp-admin-alert>
      } @else {
        @if (item(); as n) {
          <div class="sp-admin-form-actions" style="margin-bottom:10px;">
            <sp-admin-badge [tone]="reviewStatusTone(n.reviewStatus)" appearance="soft" size="sm">{{ n.reviewStatus }}</sp-admin-badge>
            <span class="sp-admin-text-muted">{{ n.cefrLevel }} · {{ n.skill || 'container (no single skill)' }}{{ n.subskill ? ' / ' + n.subskill : '' }}</span>
          </div>

          @if (n.parent; as parent) {
            <p class="sp-admin-section-sub">Leaf under <strong>{{ parent.title }}</strong>.</p>
          }

          @if (n.children.length > 0) {
            <p class="sp-admin-section-sub">Container with {{ n.children.length }} leaf child node(s):</p>
            <ul class="sp-admin-simple-list">
              @for (c of n.children; track c.id) {
                <li>
                  {{ c.title }}
                  <sp-admin-badge [tone]="reviewStatusTone(c.reviewStatus)" appearance="soft" size="sm">{{ c.reviewStatus }}</sp-admin-badge>
                </li>
              }
            </ul>
          } @else if (!n.parent) {
            <p class="sp-admin-section-sub">Standalone node — no parent, no children.</p>
          }
        }
      }

      <div slot="footer">
        <sp-admin-button variant="neutral" appearance="outline" (clicked)="closed.emit()">Close</sp-admin-button>
        @if (nodeId) {
          <sp-admin-button variant="primary" (clicked)="viewFullPage.emit(nodeId)">View full page</sp-admin-button>
        }
      </div>
    </sp-admin-modal>
  `,
})
export class SpAdminNodeHierarchyModalComponent implements OnChanges {
  @Input() nodeId: string | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() viewFullPage = new EventEmitter<string>();

  loading = signal(false);
  error = signal('');
  item = signal<SkillGraphNodeDetail | null>(null);

  constructor(private api: AdminApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['nodeId'] || !this.nodeId) return;
    this.load(this.nodeId);
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.item.set(null);
    this.api.getSkillGraphNode(id).subscribe({
      next: n => { this.item.set(n); this.loading.set(false); },
      error: err => { this.error.set(err?.error?.error ?? 'Could not load this node.'); this.loading.set(false); },
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

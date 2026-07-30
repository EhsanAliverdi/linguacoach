import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../../core/services/admin.api.service';
import { SkillGraphNodeDetail } from '../../../../core/models/admin.models';
import {
  SpAdminBadgeComponent,
  SpAdminButtonGroupComponent,
  SpAdminButtonGroupAction,
  SpAdminSlideOverComponent,
} from '../../../../design-system/admin';

/**
 * Graph context menu "Details" (2026-07-31) — a quick-glance, read-only panel for a node without
 * leaving the Cytoscape graph (unlike View/Edit, which are deliberate full-page navigations per the
 * 2026-07-23 "slide-overs moved to routed pages" correction — this one is intentionally scoped to
 * "peek without losing your place in the graph," not a replacement for the View page). Surfaces the
 * Phase GSG-1 (2026-07-31) provenance fields (CefrConfidence/CefrSource/NodeType/RoutingEligible),
 * which had no admin-visible surface at all before this.
 */
@Component({
  selector: 'sp-admin-skill-graph-node-details',
  standalone: true,
  imports: [CommonModule, RouterLink, SpAdminBadgeComponent, SpAdminButtonGroupComponent, SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over
      [open]="open"
      title="Node details"
      [subtitle]="item?.title ?? ''"
      [loading]="loading"
      [error]="error"
      (closed)="closed.emit()"
    >
      @if (item; as n) {
        <div class="sp-sgnd-row">
          <sp-admin-badge [tone]="reviewStatusTone(n.reviewStatus)" appearance="soft" size="sm">{{ n.reviewStatus }}</sp-admin-badge>
          <span class="sp-sgnd-muted">{{ n.cefrLevel }} · {{ n.skill }}{{ n.subskill ? ' / ' + n.subskill : '' }}</span>
        </div>
        <p class="sp-sgnd-key">{{ n.key }}</p>
        <p class="sp-sgnd-desc">{{ n.description }}</p>

        <div class="sp-sgnd-section">
          <div class="sp-sgnd-section-title">CEFR provenance</div>
          <div class="sp-sgnd-row">
            <sp-admin-badge [tone]="confidenceTone(n.cefrConfidence)" appearance="soft" size="sm">{{ n.cefrConfidence }}</sp-admin-badge>
            @if (n.cefrSource) { <span class="sp-sgnd-muted">source: {{ n.cefrSource }}</span> }
          </div>
          <div class="sp-sgnd-row sp-sgnd-mt">
            <span class="sp-sgnd-muted">Node type:</span>
            <sp-admin-badge tone="neutral" appearance="outline" size="sm">{{ n.nodeType ?? 'Unclassified' }}</sp-admin-badge>
            <span class="sp-sgnd-muted">Routing eligible:</span>
            <sp-admin-badge [tone]="n.routingEligible ? 'success' : 'neutral'" appearance="outline" size="sm">{{ n.routingEligible ? 'Yes' : 'No' }}</sp-admin-badge>
          </div>
        </div>

        @if (n.parent || n.children.length > 0) {
          <div class="sp-sgnd-section">
            <div class="sp-sgnd-section-title">Hierarchy</div>
            @if (n.parent; as parent) {
              <p class="sp-sgnd-muted">Leaf under <a [routerLink]="['/admin/skill-graph/nodes', parent.id]">{{ parent.title }}</a></p>
            }
            @if (n.children.length > 0) {
              <p class="sp-sgnd-muted">{{ n.children.length }} child node(s)</p>
              <ul class="sp-sgnd-list">
                @for (c of n.children; track c.id) {
                  <li><a [routerLink]="['/admin/skill-graph/nodes', c.id]">{{ c.title }}</a></li>
                }
              </ul>
            }
          </div>
        }

        <div class="sp-sgnd-section">
          <div class="sp-sgnd-section-title">Prerequisites ({{ n.prerequisites.length }})</div>
          @if (n.prerequisites.length === 0) {
            <p class="sp-sgnd-muted">None.</p>
          } @else {
            <ul class="sp-sgnd-list">
              @for (p of n.prerequisites; track p.id) { <li><a [routerLink]="['/admin/skill-graph/nodes', p.id]">{{ p.title }}</a></li> }
            </ul>
          }
        </div>

        <div class="sp-sgnd-section">
          <div class="sp-sgnd-section-title">Unlocks ({{ n.dependents.length }})</div>
          @if (n.dependents.length === 0) {
            <p class="sp-sgnd-muted">None.</p>
          } @else {
            <ul class="sp-sgnd-list">
              @for (d of n.dependents; track d.id) { <li><a [routerLink]="['/admin/skill-graph/nodes', d.id]">{{ d.title }}</a></li> }
            </ul>
          }
        </div>

        @if (n.linkedModules.length > 0) {
          <div class="sp-sgnd-section">
            <div class="sp-sgnd-section-title">Linked modules ({{ n.linkedModules.length }})</div>
            <ul class="sp-sgnd-list">
              @for (m of n.linkedModules; track m.id) { <li>{{ m.title }}</li> }
            </ul>
          </div>
        }
      }

      <sp-admin-button-group slot="footer" [actions]="footerActions" align="right" (actionSelected)="onFooterAction($event)" />
    </sp-admin-slide-over>
  `,
  styles: [`
    .sp-sgnd-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .sp-sgnd-mt { margin-top: 6px; }
    .sp-sgnd-muted { font-size: 12px; color: var(--sp-admin-text-muted, #64748B); }
    .sp-sgnd-key { font-size: 11px; font-family: monospace; color: var(--sp-admin-text-muted, #64748B); margin: 4px 0 12px; }
    .sp-sgnd-desc { font-size: 13px; color: var(--sp-admin-text, #0F172A); margin: 0 0 16px; line-height: 1.5; }
    .sp-sgnd-section { margin-top: 18px; padding-top: 14px; border-top: 1px solid var(--sp-admin-border-subtle, #F4F2FC); }
    .sp-sgnd-section-title { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: .02em; color: var(--sp-admin-text-muted, #64748B); margin-bottom: 6px; }
    .sp-sgnd-list { list-style: none; margin: 0; padding: 0; font-size: 13px; display: flex; flex-direction: column; gap: 4px; }
  `],
})
export class SpAdminSkillGraphNodeDetailsComponent implements OnChanges {
  @Input() open = false;
  @Input() nodeId: string | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() view = new EventEmitter<string>();
  @Output() edit = new EventEmitter<string>();

  item: SkillGraphNodeDetail | null = null;
  loading = false;
  error = '';

  constructor(private readonly api: AdminApiService) {}

  // sp-admin-button-group renders from an `actions` array + `actionSelected` output — it does NOT
  // project child <sp-admin-button> content, unlike sp-admin-slide-over's other slots.
  get footerActions(): SpAdminButtonGroupAction[] {
    return [
      { id: 'close', label: 'Close', variant: 'neutral', appearance: 'outline' },
      { id: 'edit', label: 'Edit', variant: 'primary', appearance: 'outline', hidden: !this.item },
      { id: 'view', label: 'Open full page', variant: 'primary', hidden: !this.item },
    ];
  }

  onFooterAction(actionId: string): void {
    if (!this.item) { if (actionId === 'close') this.closed.emit(); return; }
    if (actionId === 'close') this.closed.emit();
    else if (actionId === 'edit') this.edit.emit(this.item.id);
    else if (actionId === 'view') this.view.emit(this.item.id);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['open'] || changes['nodeId']) && this.open && this.nodeId) {
      this.load(this.nodeId);
    }
    if (changes['open'] && !this.open) {
      this.item = null;
      this.error = '';
    }
  }

  private load(id: string): void {
    this.loading = true;
    this.error = '';
    this.item = null;
    this.api.getSkillGraphNode(id).subscribe({
      next: n => { this.item = n; this.loading = false; },
      error: err => { this.error = err?.error?.error ?? 'Could not load this node.'; this.loading = false; },
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

  confidenceTone(confidence: string): 'success' | 'warning' | 'danger' | 'neutral' {
    switch (confidence) {
      case 'Attested':
      case 'Curated':
        return 'success';
      case 'Fallback':
        return 'warning';
      case 'Inherited':
        return 'neutral';
      default:
        return 'danger';
    }
  }
}

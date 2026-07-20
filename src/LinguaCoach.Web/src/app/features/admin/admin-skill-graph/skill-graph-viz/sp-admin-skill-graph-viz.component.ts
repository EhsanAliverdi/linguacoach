import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { SkillGraphEdge, SkillGraphNode } from '../../../../core/models/admin.models';

cytoscape.use(dagre);

// Sprint 13 — CEFR-level color coding, lightest (A1) to darkest (C2), matching the design
// system's existing indigo/purple palette used elsewhere in admin (see badge tones).
const CEFR_COLORS: Record<string, string> = {
  A1: '#C0BAF9',
  A2: '#A08EF0',
  B1: '#8B74EA',
  B2: '#5B4BE8',
  C1: '#3A2EA8',
  C2: '#211B36',
};

/**
 * Sprint 13 — visual skill-graph view (Cytoscape.js + Dagre hierarchical layout), alongside the
 * existing table view on the admin Skill Graph page. Nodes color-coded by CEFR level; prerequisite
 * edges point from prerequisite -> dependent (Dagre reads this as the hierarchy direction).
 * Deliberately a plain wrapper around a raw DOM container (Cytoscape needs a real element to mount
 * into, unlike this design system's other chart primitives which are hand-rolled inline SVG) — see
 * sp-admin-graph-card, which is meant to wrap this for title/status/footer chrome.
 */
@Component({
  selector: 'sp-admin-skill-graph-viz',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-sgv-legend">
      @for (level of cefrLevels; track level) {
        <span class="sp-sgv-legend-item">
          <span class="sp-sgv-legend-dot" [style.background]="cefrColor(level)"></span>{{ level }}
        </span>
      }
    </div>
    <div #cyContainer class="sp-sgv-canvas"></div>
  `,
  styles: [`
    .sp-sgv-legend { display: flex; gap: 14px; flex-wrap: wrap; margin-bottom: 8px; }
    .sp-sgv-legend-item { display: inline-flex; align-items: center; gap: 5px; font-size: 11px; color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-sgv-legend-dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
    .sp-sgv-canvas { width: 100%; height: 520px; border: 1px solid var(--sp-admin-border, #ECE9F5); border-radius: 10px; background: var(--sp-admin-surface, #fff); }
  `],
})
export class SpAdminSkillGraphVizComponent implements OnChanges, OnDestroy {
  @Input() nodes: SkillGraphNode[] = [];
  @Input() edges: SkillGraphEdge[] = [];
  @Output() nodeSelected = new EventEmitter<SkillGraphNode>();

  @ViewChild('cyContainer', { static: true }) container!: ElementRef<HTMLDivElement>;

  readonly cefrLevels = Object.keys(CEFR_COLORS);
  private cy: Core | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['nodes'] || changes['edges']) {
      this.render();
    }
  }

  ngOnDestroy(): void {
    this.cy?.destroy();
  }

  cefrColor(level: string): string {
    return CEFR_COLORS[level] ?? '#8B85A0';
  }

  private render(): void {
    if (!this.container || this.nodes.length === 0) return;

    this.cy?.destroy();

    const nodeIds = new Set(this.nodes.map(n => n.id));
    const elements: ElementDefinition[] = [
      ...this.nodes.map(n => ({
        data: { id: n.id, label: n.title, cefrLevel: n.cefrLevel },
      })),
      // Dagre reads edge direction as the hierarchy (source above target), so prerequisite ->
      // dependent gives "must-master-first" nodes above the nodes that require them.
      ...this.edges
        .filter(e => nodeIds.has(e.nodeId) && nodeIds.has(e.prerequisiteNodeId))
        .map(e => ({
          data: { id: `${e.prerequisiteNodeId}->${e.nodeId}`, source: e.prerequisiteNodeId, target: e.nodeId },
        })),
    ];

    this.cy = cytoscape({
      container: this.container.nativeElement,
      elements,
      style: [
        {
          selector: 'node',
          style: {
            'background-color': (ele: cytoscape.NodeSingular) => this.cefrColor(ele.data('cefrLevel')),
            label: 'data(label)',
            'font-size': '9px',
            color: '#211B36',
            'text-valign': 'bottom',
            'text-margin-y': 4,
            width: 22,
            height: 22,
            'text-wrap': 'ellipsis',
            'text-max-width': '80px',
          },
        },
        {
          selector: 'edge',
          style: {
            width: 1.5,
            'line-color': '#C0BAF9',
            'target-arrow-color': '#C0BAF9',
            'target-arrow-shape': 'triangle',
            'curve-style': 'bezier',
          },
        },
      ],
      layout: { name: 'dagre', rankDir: 'TB', nodeSep: 24, rankSep: 60 } as cytoscape.LayoutOptions,
      wheelSensitivity: 0.2,
    });

    this.cy.on('tap', 'node', evt => {
      const id = evt.target.id();
      const node = this.nodes.find(n => n.id === id);
      if (node) this.nodeSelected.emit(node);
    });
  }
}

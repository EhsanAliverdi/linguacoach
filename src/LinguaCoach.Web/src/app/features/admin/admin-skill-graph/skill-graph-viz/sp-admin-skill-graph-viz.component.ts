import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';
import coseBilkent from 'cytoscape-cose-bilkent';
import elk from 'cytoscape-elk';
import { SkillGraphEdge, SkillGraphNode } from '../../../../core/models/admin.models';

cytoscape.use(coseBilkent);
cytoscape.use(elk);

// User request (2026-07-23) — expose the same layout algorithm choices as the
// cytoscape.js-elk demo site (https://cytoscape.org/cytoscape.js-elk), so an admin isn't stuck
// with only one drawing style. 'cose-bilkent' (the original default) keeps the compound "boxes
// around nodes with the same Skill" grouping; every ELK algorithm renders the same filtered node
// set flat (no compound boxes — ELK's compound support doesn't map cleanly onto this component's
// bespoke skill-box grouping, so ELK algorithms trade that visual for their own layout shape).
type LayoutAlgorithm = 'cose-bilkent' | 'layered' | 'force' | 'disco' | 'stress' | 'random' | 'box' | 'mrtree';
const LAYOUT_OPTIONS: { value: LayoutAlgorithm; label: string }[] = [
  { value: 'cose-bilkent', label: 'Compound (skill groups)' },
  { value: 'layered', label: 'Layered' },
  { value: 'force', label: 'Force' },
  { value: 'stress', label: 'Stress' },
  { value: 'disco', label: 'Disco' },
  { value: 'mrtree', label: 'Tree' },
  { value: 'box', label: 'Box' },
  { value: 'random', label: 'Random' },
];

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
// Sprint 14.1 — light per-skill box tints for the compound "group by skill" parent nodes.
const SKILL_BOX_COLORS: Record<string, string> = {
  grammar: '#F4F2FE',
  vocabulary: '#EFFAF5',
  reading: '#FFF7EB',
  writing: '#FDF1F1',
  listening: '#EEF6FF',
  speaking: '#FBF0FA',
};

/**
 * Sprint 13/14.1 — visual skill-graph view (Cytoscape.js + cose-bilkent compound layout),
 * alongside the existing table view on the admin Skill Graph page.
 *
 * Real data reality: only ~15 prerequisite edges exist across 219 nodes (confirmed live), so a
 * layout driven purely by those edges renders as one flat row of disconnected dots — not a useful
 * graph. Every node DOES carry a real Skill, so nodes are grouped into compound "box" parent nodes
 * by Skill (cose-bilkent renders these as bounded regions, matching the requested "boxes around
 * nodes with similar feature" look) — real prerequisite edges still render as connecting lines,
 * including across skill boxes when a prerequisite crosses skills. A CEFR-level filter (toggled via
 * the legend chips) keeps any one view to a manageable node count, since showing all 219 at once is
 * illegible regardless of layout.
 */
@Component({
  selector: 'sp-admin-skill-graph-viz',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="sp-sgv-legend">
      <input
        type="text"
        class="sp-sgv-search"
        placeholder="Find a node by title…"
        [(ngModel)]="searchTerm"
        (input)="onSearchInput()"
      />
      @if (searchMatches.length > 0) {
        <span class="sp-sgv-search-nav">
          <button type="button" class="sp-sgv-icon-btn" (click)="jumpToMatch(-1)" title="Previous match">‹</button>
          {{ searchMatchIndex + 1 }}/{{ searchMatches.length }}
          <button type="button" class="sp-sgv-icon-btn" (click)="jumpToMatch(1)" title="Next match">›</button>
        </span>
      } @else if (searchTerm) {
        <span class="sp-sgv-search-nav sp-sgv-search-nav--empty">No match</span>
      }
      <select class="sp-sgv-layout-select" [(ngModel)]="layoutAlgorithm" (ngModelChange)="onLayoutChange()" title="Graph drawing algorithm">
        @for (opt of layoutOptions; track opt.value) {
          <option [value]="opt.value">{{ opt.label }}</option>
        }
      </select>
      <span class="sp-sgv-legend-count">{{ nodes.length }} node(s) shown</span>
    </div>
    <div class="sp-sgv-canvas-wrap">
      <div #cyContainer class="sp-sgv-canvas" [class.sp-sgv-canvas--area-zoom]="areaZoomActive" (mousedown)="onAreaZoomMouseDown($event)" (contextmenu)="$event.preventDefault()"></div>
      <div class="sp-sgv-zoom-controls">
        <button type="button" class="sp-sgv-icon-btn" (click)="zoomBy(1.3)" title="Zoom in">+</button>
        <button type="button" class="sp-sgv-icon-btn" (click)="zoomBy(1 / 1.3)" title="Zoom out">−</button>
        <button type="button" class="sp-sgv-icon-btn" (click)="fitToView()" title="Fit to view">⤢</button>
        <button type="button" class="sp-sgv-icon-btn" [class.sp-sgv-icon-btn--active]="areaZoomActive" (click)="toggleAreaZoom()" title="Area zoom in — drag to select a region"><i class="fa-solid fa-magnifying-glass-plus"></i></button>
        <button type="button" class="sp-sgv-icon-btn" (click)="areaZoomOut()" title="Area zoom out — back to the whole graph"><i class="fa-solid fa-magnifying-glass-minus"></i></button>
      </div>
      @if (tooltip) {
        <div class="sp-sgv-tooltip" [style.left.px]="tooltip.x" [style.top.px]="tooltip.y">
          <div class="sp-sgv-tooltip-title">{{ tooltip.title }}</div>
          @if (tooltip.description) {
            <div class="sp-sgv-tooltip-desc">{{ tooltip.description }}</div>
          }
          @if (tooltip.hasChildren) {
            <div class="sp-sgv-tooltip-hint">Click to open · Right-click for actions</div>
          } @else {
            <div class="sp-sgv-tooltip-hint">Right-click for actions</div>
          }
        </div>
      }
      @if (contextMenu; as menu) {
        <div class="sp-sgv-ctx-backdrop" (click)="closeContextMenu()" (contextmenu)="onContextMenuBackdropRightClick($event)"></div>
        <div class="sp-sgv-ctx-menu" [style.left.px]="menu.x" [style.top.px]="menu.y">
          <div class="sp-sgv-ctx-menu-title">{{ menu.title }}</div>
          <button type="button" class="sp-sgv-ctx-menu-item" (click)="onMenuAction('view', menu.node)">View</button>
          <button type="button" class="sp-sgv-ctx-menu-item" (click)="onMenuAction('edit', menu.node)">Edit</button>
          <button type="button" class="sp-sgv-ctx-menu-item" (click)="onMenuAction('details', menu.node)">Details</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .sp-sgv-legend { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin-bottom: 8px; }
    .sp-sgv-search {
      font-size: 11px; padding: 4px 8px; border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 6px; width: 160px; color: var(--sp-admin-text, #211B36);
    }
    .sp-sgv-layout-select {
      font-size: 11px; padding: 4px 8px; border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 6px; color: var(--sp-admin-text, #211B36); background: #fff;
    }
    .sp-sgv-search-nav { display: inline-flex; align-items: center; gap: 4px; font-size: 11px; color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-sgv-search-nav--empty { color: var(--sp-admin-danger, #DC2626); }
    .sp-sgv-legend-count { font-size: 11px; color: var(--sp-admin-text-dim, #BDB8CC); margin-left: auto; }
    .sp-sgv-canvas-wrap { position: relative; }
    .sp-sgv-canvas { position: relative; width: 100%; height: 620px; border: 1px solid var(--sp-admin-border, #ECE9F5); border-radius: 10px; background: var(--sp-admin-surface, #fff); }
    .sp-sgv-canvas--area-zoom { cursor: crosshair; }
    .sp-sgv-zoom-controls {
      position: absolute; bottom: 12px; right: 12px; display: flex; flex-direction: column; gap: 4px;
      background: var(--sp-admin-surface, #fff); border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 8px; padding: 4px; box-shadow: 0 2px 8px rgba(0,0,0,.08);
    }
    .sp-sgv-icon-btn {
      width: 26px; height: 26px; border: none; background: none; cursor: pointer;
      font-size: 15px; font-weight: 700; color: var(--sp-admin-text, #211B36); border-radius: 5px;
      display: flex; align-items: center; justify-content: center; line-height: 1;
    }
    .sp-sgv-icon-btn:hover { background: var(--sp-admin-border, #ECE9F5); }
    .sp-sgv-icon-btn--active { background: #5B4BE8; color: #fff; }
    .sp-sgv-icon-btn--active:hover { background: #4A3BC7; }
    .sp-sgv-tooltip {
      position: absolute; transform: translate(-50%, -100%); margin-top: -10px;
      max-width: 220px; padding: 6px 10px; border-radius: 6px; pointer-events: none; z-index: 30;
      background: #211B36; color: #fff; box-shadow: 0 4px 12px rgba(0,0,0,.2);
    }
    .sp-sgv-tooltip-title { font-size: 11px; font-weight: 700; }
    .sp-sgv-tooltip-desc { font-size: 10px; color: #C0BAF9; margin-top: 2px; }
    .sp-sgv-tooltip-hint { font-size: 9px; color: #8B85A0; margin-top: 4px; font-style: italic; }
    .sp-sgv-ctx-backdrop { position: absolute; inset: 0; z-index: 39; }
    .sp-sgv-ctx-menu {
      position: absolute; transform: translate(-50%, 4px); min-width: 140px; z-index: 40;
      background: #fff; border: 1px solid var(--sp-admin-border, #ECE9F5); border-radius: 8px;
      box-shadow: 0 8px 24px rgba(0,0,0,.15); padding: 4px; display: flex; flex-direction: column;
    }
    .sp-sgv-ctx-menu-title {
      font-size: 10px; font-weight: 700; color: var(--sp-admin-text-muted, #8B85A0);
      padding: 6px 10px 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 220px;
    }
    .sp-sgv-ctx-menu-item {
      text-align: left; border: none; background: none; padding: 7px 10px; font-size: 12px;
      color: var(--sp-admin-text, #211B36); border-radius: 5px; cursor: pointer;
    }
    .sp-sgv-ctx-menu-item:hover { background: var(--sp-admin-border, #ECE9F5); }
  `],
})
export class SpAdminSkillGraphVizComponent implements OnChanges, OnDestroy {
  @Input() nodes: SkillGraphNode[] = [];
  @Input() edges: SkillGraphEdge[] = [];
  // Topical-hierarchy drill-down (2026-07-30, user follow-up) — the compound layout's grouping box
  // used to always show the Skill name ("Grammar"), even after drilling into a container, since
  // every visible node always shares one skill (the Graph tab requires a skill filter). That read
  // as "the container's title never changes." When set, overrides the box label with the current
  // container's actual title; the parent component passes the last breadcrumb crumb's title.
  @Input() containerLabel: string | null = null;
  @Output() nodeSelected = new EventEmitter<SkillGraphNode>();
  // Topical-hierarchy drill-down (2026-07-30) — fired instead of nodeSelected when the tapped node
  // has children (i.e. some other node in the currently-visible set has parentNodeId === this
  // node's id). Leaf taps keep firing nodeSelected as before.
  @Output() drillInto = new EventEmitter<SkillGraphNode>();
  // Per-node context menu (2026-07-31, user follow-up) — right-click any node (leaf or container)
  // for View/Edit/Details, without disturbing the existing left-click select/drill-in behavior.
  @Output() viewNode = new EventEmitter<SkillGraphNode>();
  @Output() editNode = new EventEmitter<SkillGraphNode>();
  @Output() detailsNode = new EventEmitter<SkillGraphNode>();

  tooltip: { x: number; y: number; title: string; description: string; hasChildren: boolean } | null = null;
  contextMenu: { x: number; y: number; title: string; node: SkillGraphNode } | null = null;

  @ViewChild('cyContainer', { static: true }) container!: ElementRef<HTMLDivElement>;

  // Layout algorithm picker (2026-07-23) — applies to whatever's currently visible, i.e. the
  // CEFR-level-filtered set (see `visibleNodes` in render()), not the whole 600-node graph.
  readonly layoutOptions = LAYOUT_OPTIONS;
  // User preference (2026-07-30) — Layered reads better than the compound skill-box grouping now
  // that the root graph view is topLevelOnly-filtered (no more flat leaf/container flood to
  // organize into boxes) and containers are already visually distinct via their own border/icon.
  layoutAlgorithm: LayoutAlgorithm = 'layered';

  onLayoutChange(): void {
    this.render();
  }

  // Sprint 14.4 — Google-Maps-style navigation: explicit zoom in/out/fit controls (mouse wheel
  // alone was the only way to zoom before, and with 219 nodes finding a specific one by panning
  // around was impractical) plus a find-by-title search that centers and highlights matches.
  searchTerm = '';
  searchMatches: string[] = [];
  searchMatchIndex = -1;

  // Area (marquee) zoom (2026-07-23) — user follow-up: "I don't see area select zoom icon, area
  // select zoom out icon". Same drag-a-rectangle-to-zoom pattern as sp-admin-node-graph-preview.
  areaZoomActive = false;
  private areaZoomStart: { x: number; y: number } | null = null;
  private areaZoomBox: HTMLDivElement | null = null;
  private areaZoomMoveHandler = (e: MouseEvent) => this.onAreaZoomMouseMove(e);
  private areaZoomUpHandler = (e: MouseEvent) => this.onAreaZoomMouseUp(e);

  private cy: Core | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['nodes'] || changes['edges']) {
      this.render();
    }
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.areaZoomMoveHandler);
    document.removeEventListener('mouseup', this.areaZoomUpHandler);
    this.areaZoomBox?.remove();
    this.cy?.destroy();
  }

  cefrColor(level: string): string {
    return CEFR_COLORS[level] ?? '#8B85A0';
  }

  // Labels now render inside the node (centered), not below it — C1/C2's dark background colors
  // need light text for contrast; the lighter A1-B2 backgrounds keep the existing dark text.
  labelColorFor(level: string): string {
    return level === 'C1' || level === 'C2' ? '#fff' : '#211B36';
  }

  private render(): void {
    if (!this.container || this.nodes.length === 0) return;

    this.cy?.destroy();
    this.searchMatches = [];
    this.searchMatchIndex = -1;
    this.areaZoomActive = false;
    this.areaZoomBox?.remove();
    this.areaZoomBox = null;
    this.areaZoomStart = null;

    // CEFR-level filtering now happens server-side (the admin page requires a level+skill
    // selection before fetching at all — see AdminSkillGraphController.GetGraph), so `nodes` here
    // is already the visible set; no client-side level filter needed anymore.
    const visibleNodes = this.nodes;
    if (visibleNodes.length === 0) return;

    const nodeIds = new Set(visibleNodes.map(n => n.id));
    const filteredEdges = this.edges.filter(e => nodeIds.has(e.nodeId) && nodeIds.has(e.prerequisiteNodeId));
    const isCompound = this.layoutAlgorithm === 'cose-bilkent';

    // Compound path (cose-bilkent, the original default) groups nodes into per-Skill parent
    // boxes; every ELK algorithm instead renders the same filtered nodes/edges flat — ELK's own
    // compound-layout support doesn't map cleanly onto this bespoke skill-box grouping, so
    // switching to an ELK algorithm trades the boxes for that algorithm's own layout shape.
    const skillsPresent = new Set(visibleNodes.map(n => n.skill || 'other'));
    const elements: ElementDefinition[] = [
      ...(isCompound
        ? Array.from(skillsPresent).map(skill => ({
            data: { id: `skill:${skill}`, label: this.containerLabel ?? this.skillLabel(skill), isParent: true },
          }))
        : []),
      ...visibleNodes.map(n => ({
        data: {
          id: n.id,
          label: n.hasChildren ? `\u{1F4C1} ${n.title}` : n.title,
          description: n.description,
          cefrLevel: n.cefrLevel,
          hasChildren: n.hasChildren,
          ...(isCompound ? { parent: `skill:${n.skill || 'other'}` } : {}),
        },
      })),
      ...filteredEdges.map(e => ({
        data: { id: `${e.prerequisiteNodeId}->${e.nodeId}`, source: e.prerequisiteNodeId, target: e.nodeId },
      })),
    ];

    this.cy = cytoscape({
      container: this.container.nativeElement,
      elements,
      style: [
        {
          selector: 'node[?isParent]',
          style: {
            'background-color': (ele: cytoscape.NodeSingular) => this.skillBoxColor(ele.data('label')),
            'background-opacity': 1,
            'border-width': 1,
            'border-color': 'var(--sp-admin-border, #ECE9F5)',
            label: 'data(label)',
            'text-valign': 'top',
            'text-halign': 'center',
            'text-margin-y': -6,
            'font-size': '11px',
            'font-weight': 700,
            color: '#211B36',
            shape: 'round-rectangle',
            padding: '18px',
          },
        },
        {
          // User feedback (2026-07-30): circular nodes left label text overlapping/illegible —
          // squares/rectangles (matching sp-admin-node-graph-preview's convention) give the label
          // room to actually sit inside the shape instead of spilling below it.
          selector: 'node[!isParent]',
          style: {
            shape: 'round-rectangle',
            'background-color': (ele: cytoscape.NodeSingular) => this.cefrColor(ele.data('cefrLevel')),
            'border-width': 1,
            'border-color': 'rgba(33,27,54,0.15)',
            label: 'data(label)',
            'font-size': '9px',
            color: (ele: cytoscape.NodeSingular) => this.labelColorFor(ele.data('cefrLevel')),
            'text-valign': 'center',
            'text-halign': 'center',
            'text-wrap': 'wrap',
            'text-max-width': '90px',
            width: 'label',
            height: 'label',
            padding: '8px',
          },
        },
        {
          // User feedback (2026-07-30) — "different color for containers, clearly identifiable":
          // a real node with children (topical containers like "Adverbs", subtopics like "Adverbs
          // of frequency") looked identical to a leaf, distinguishable only by hovering to check the
          // tooltip's "Click to open" hint. A thick, fixed-color double border (independent of CEFR
          // tint, so it reads the same at every level) plus a small folder glyph in the label makes
          // "this opens into more nodes" visible at a glance without hovering.
          selector: 'node[!isParent][?hasChildren]',
          style: {
            'border-width': 3,
            'border-color': '#5B4BE8',
            'border-style': 'double',
            'font-weight': 700,
          },
        },
        {
          selector: 'edge',
          style: {
            width: 1.5,
            'line-color': '#5B4BE8',
            'target-arrow-color': '#5B4BE8',
            'target-arrow-shape': 'triangle',
            'curve-style': 'bezier',
            opacity: 0.85,
          },
        },
        {
          selector: '.sp-sgv-highlight',
          style: {
            'border-width': 3,
            'border-color': '#F0982C',
            'z-index': 999,
          },
        },
      ],
      layout: isCompound
        ? ({
            name: 'cose-bilkent',
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            ...({ nodeDimensionsIncludeLabels: true, animate: false, padding: 30, idealEdgeLength: 80 } as any),
          } as cytoscape.LayoutOptions)
        : ({
            name: 'elk',
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            elk: { algorithm: this.layoutAlgorithm },
            fit: true,
            padding: 30,
          } as unknown as cytoscape.LayoutOptions),
      wheelSensitivity: 0.2,
      minZoom: 0.1,
      maxZoom: 4,
    });

    this.cy.on('tap', 'node', evt => {
      if (evt.target.data('isParent')) return;
      const id = evt.target.id();
      const node = this.nodes.find(n => n.id === id);
      if (!node) return;
      if (node.hasChildren) this.drillInto.emit(node);
      else this.nodeSelected.emit(node);
    });

    // Hover tooltip (2026-07-30) — no tooltip library in this repo yet, so a plain positioned div
    // driven off renderedPosition(), matching this component's existing hand-rolled controls.
    this.cy.on('mouseover', 'node[!isParent]', evt => {
      const pos = evt.target.renderedPosition();
      this.tooltip = {
        x: pos.x,
        y: pos.y - evt.target.renderedOuterHeight() / 2,
        title: evt.target.data('label'),
        description: evt.target.data('description') || '',
        hasChildren: !!evt.target.data('hasChildren'),
      };
    });
    this.cy.on('mouseout', 'node[!isParent]', () => { this.tooltip = null; });
    this.cy.on('pan zoom', () => { this.tooltip = null; });

    // Context menu (2026-07-31) — right-click (Cytoscape's 'cxttap') opens View/Edit/Details,
    // independent of left-click's select/drill-in behavior. 'cxttap' also fires on a
    // touch-and-hold, so this works without a mouse too.
    this.cy.on('cxttap', 'node[!isParent]', evt => {
      const id = evt.target.id();
      const node = this.nodes.find(n => n.id === id);
      if (!node) return;
      const pos = evt.target.renderedPosition();
      this.tooltip = null;
      this.contextMenu = { x: pos.x, y: pos.y + evt.target.renderedOuterHeight() / 2, title: node.title, node };
    });
    this.cy.on('pan zoom', () => { this.contextMenu = null; });
  }

  closeContextMenu(): void {
    this.contextMenu = null;
  }

  // Right-clicking the backdrop (i.e. empty canvas, outside any node) should just close the menu,
  // not also pop the browser's native context menu on top of it.
  onContextMenuBackdropRightClick(event: MouseEvent): void {
    event.preventDefault();
    this.contextMenu = null;
  }

  onMenuAction(action: 'view' | 'edit' | 'details', node: SkillGraphNode): void {
    this.contextMenu = null;
    if (action === 'view') this.viewNode.emit(node);
    else if (action === 'edit') this.editNode.emit(node);
    else this.detailsNode.emit(node);
  }

  private skillLabel(skill: string): string {
    return skill.charAt(0).toUpperCase() + skill.slice(1);
  }

  private skillBoxColor(label: string): string {
    return SKILL_BOX_COLORS[label.toLowerCase()] ?? '#F6F4FB';
  }

  // ── Sprint 14.4 — zoom/pan controls ──────────────────────────────────────

  zoomBy(factor: number): void {
    if (!this.cy) return;
    const level = Math.max(this.cy.minZoom(), Math.min(this.cy.maxZoom(), this.cy.zoom() * factor));
    this.cy.animate({ zoom: level, duration: 150 });
  }

  fitToView(): void {
    this.cy?.animate({ fit: { eles: this.cy.elements(), padding: 30 }, duration: 200 });
  }

  // ── Area (marquee) zoom (2026-07-23) ──────────────────────────────────────
  toggleAreaZoom(): void {
    this.areaZoomActive = !this.areaZoomActive;
    this.cy?.userPanningEnabled(!this.areaZoomActive);
  }

  areaZoomOut(): void {
    this.areaZoomActive = false;
    this.cy?.userPanningEnabled(true);
    this.fitToView();
  }

  onAreaZoomMouseDown(evt: MouseEvent): void {
    if (!this.areaZoomActive || !this.container) return;
    evt.preventDefault();
    const rect = this.container.nativeElement.getBoundingClientRect();
    this.areaZoomStart = { x: evt.clientX - rect.left, y: evt.clientY - rect.top };

    const box = document.createElement('div');
    Object.assign(box.style, {
      position: 'absolute', border: '1.5px dashed #5B4BE8', background: 'rgba(91,75,232,0.08)',
      pointerEvents: 'none', zIndex: '20',
      left: `${this.areaZoomStart.x}px`, top: `${this.areaZoomStart.y}px`, width: '0px', height: '0px',
    });
    this.container.nativeElement.appendChild(box);
    this.areaZoomBox = box;

    document.addEventListener('mousemove', this.areaZoomMoveHandler);
    document.addEventListener('mouseup', this.areaZoomUpHandler);
  }

  private onAreaZoomMouseMove(evt: MouseEvent): void {
    if (!this.areaZoomStart || !this.areaZoomBox || !this.container) return;
    const rect = this.container.nativeElement.getBoundingClientRect();
    const cur = { x: evt.clientX - rect.left, y: evt.clientY - rect.top };
    const x = Math.min(this.areaZoomStart.x, cur.x);
    const y = Math.min(this.areaZoomStart.y, cur.y);
    const w = Math.abs(cur.x - this.areaZoomStart.x);
    const h = Math.abs(cur.y - this.areaZoomStart.y);
    Object.assign(this.areaZoomBox.style, { left: `${x}px`, top: `${y}px`, width: `${w}px`, height: `${h}px` });
  }

  private onAreaZoomMouseUp(evt: MouseEvent): void {
    document.removeEventListener('mousemove', this.areaZoomMoveHandler);
    document.removeEventListener('mouseup', this.areaZoomUpHandler);
    this.areaZoomBox?.remove();
    this.areaZoomBox = null;

    // User-reported bug (2026-07-23): area zoom mode previously stayed on until the toggle button
    // was clicked again, so panning stayed disabled and the admin got stuck. Single-shot instead —
    // every drag (or even a stray click) exits the mode and restores normal panning immediately.
    this.areaZoomActive = false;
    this.cy?.userPanningEnabled(true);

    const start = this.areaZoomStart;
    this.areaZoomStart = null;
    if (!start || !this.cy || !this.container) return;

    const rect = this.container.nativeElement.getBoundingClientRect();
    const end = { x: evt.clientX - rect.left, y: evt.clientY - rect.top };
    const w = Math.abs(end.x - start.x);
    const h = Math.abs(end.y - start.y);
    if (w < 8 || h < 8) return; // treat as a click, not a real drag — ignore

    const pan = this.cy.pan();
    const zoom = this.cy.zoom();
    const x1 = (Math.min(start.x, end.x) - pan.x) / zoom;
    const y1 = (Math.min(start.y, end.y) - pan.y) / zoom;
    const x2 = (Math.max(start.x, end.x) - pan.x) / zoom;
    const y2 = (Math.max(start.y, end.y) - pan.y) / zoom;
    // `boundingBox` is a real runtime option for `animate({ fit })` (cytoscape reads
    // `fit.eles || fit.boundingBox`) — just missing from @types/cytoscape's AnimationFitOptions.
    this.cy.animate({ fit: { boundingBox: { x1, y1, x2, y2 }, padding: 10 } as unknown as cytoscape.AnimationFitOptions, duration: 200 });
  }

  onSearchInput(): void {
    if (!this.cy || !this.searchTerm.trim()) {
      this.searchMatches = [];
      this.searchMatchIndex = -1;
      return;
    }
    const term = this.searchTerm.trim().toLowerCase();
    this.searchMatches = this.cy
      .nodes('[!isParent]')
      .filter(n => (n.data('label') as string).toLowerCase().includes(term))
      .map(n => n.id());
    this.searchMatchIndex = this.searchMatches.length > 0 ? 0 : -1;
    if (this.searchMatchIndex >= 0) this.centerOnMatch();
  }

  jumpToMatch(delta: number): void {
    if (this.searchMatches.length === 0) return;
    this.searchMatchIndex = (this.searchMatchIndex + delta + this.searchMatches.length) % this.searchMatches.length;
    this.centerOnMatch();
  }

  private centerOnMatch(): void {
    if (!this.cy || this.searchMatchIndex < 0) return;
    const id = this.searchMatches[this.searchMatchIndex];
    const ele = this.cy.getElementById(id);
    if (ele.empty()) return;
    this.cy.animate({ center: { eles: ele }, zoom: Math.max(this.cy.zoom(), 1.2), duration: 250 });
    this.cy.elements().removeClass('sp-sgv-highlight');
    ele.addClass('sp-sgv-highlight');
  }
}

import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';
import elk from 'cytoscape-elk';

cytoscape.use(elk);

export interface NodeGraphPreviewNode {
  id: string;
  title: string;
  /** Unsaved local state (2026-07-23) — 'add' = staged to be linked, 'remove' = staged to be
   *  unlinked, on Save. Undefined means the edge is already real/persisted. Only ever set on
   *  nodes directly adjacent to the center, since staging only ever touches direct edges. */
  pending?: 'add' | 'remove';
}

export interface NodeGraphPreviewEdge {
  /** Prerequisite side — the edge points from the thing you must master first to the thing it unlocks. */
  source: string;
  target: string;
  pending?: 'add' | 'remove';
}

/** Real `{ nodeId, prerequisiteNodeId }` edge shape from `GET /skill-graph/graph`. */
export interface RawSkillGraphEdge {
  nodeId: string;
  prerequisiteNodeId: string;
}

/**
 * Breadth-first expansion (2026-07-23) — walks `level` hops of real prerequisite/unlock edges
 * out from `centerId` in both directions, so the graph preview can show more than just the
 * immediate neighbors (user: "+ more layer / - layer should show the next layer"). Pure/stateless
 * so both the View and Edit pages can share it against the full graph's edge list.
 */
export function computeGraphNeighborhood(
  centerId: string,
  allEdges: RawSkillGraphEdge[],
  level: number,
): { nodeIds: Set<string>; edges: { source: string; target: string }[] } {
  const nodeIds = new Set<string>([centerId]);
  const edges: { source: string; target: string }[] = [];
  const edgeKeys = new Set<string>();

  const addEdge = (source: string, target: string) => {
    const key = `${source}->${target}`;
    if (!edgeKeys.has(key)) { edgeKeys.add(key); edges.push({ source, target }); }
  };

  // Ancestors — walk backward via edges that unlock the current frontier.
  let frontier = new Set([centerId]);
  for (let hop = 0; hop < level && frontier.size > 0; hop++) {
    const next = new Set<string>();
    for (const e of allEdges) {
      if (frontier.has(e.nodeId)) {
        addEdge(e.prerequisiteNodeId, e.nodeId);
        if (!nodeIds.has(e.prerequisiteNodeId)) { nodeIds.add(e.prerequisiteNodeId); next.add(e.prerequisiteNodeId); }
      }
    }
    frontier = next;
  }

  // Descendants — walk forward via edges the current frontier unlocks.
  frontier = new Set([centerId]);
  for (let hop = 0; hop < level && frontier.size > 0; hop++) {
    const next = new Set<string>();
    for (const e of allEdges) {
      if (frontier.has(e.prerequisiteNodeId)) {
        addEdge(e.prerequisiteNodeId, e.nodeId);
        if (!nodeIds.has(e.nodeId)) { nodeIds.add(e.nodeId); next.add(e.nodeId); }
      }
    }
    frontier = next;
  }

  return { nodeIds, edges };
}

/**
 * A local "neighborhood" graph for a single Skill Graph node — prerequisites flowing into it on
 * one side, the node itself highlighted in the middle, and what it unlocks flowing out the other
 * side. Used by the node View and Edit pages (2026-07-23).
 *
 * User correction (2026-07-23): the first version of this was a plain 3-column CSS layout —
 * replaced with a real cytoscape rendering (elk's `layered` algorithm, left-to-right direction,
 * boxed/labeled-node look and taxi/orthogonal edge routing matching cytoscape.js-elk's own
 * "layered" demo: https://cytoscape.org/cytoscape.js-elk/?demo=layered) instead of a disconnected
 * set of cards.
 *
 * Staged edits (2026-07-23) — a neighbor tagged `pending: 'add'` renders dashed + amber (not yet
 * linked), `pending: 'remove'` renders dashed + greyed out (will be unlinked on Save). View never
 * sets `pending`, since it only ever shows already-real edges.
 *
 * Multi-layer expansion (2026-07-23) — callers now pass the whole visible subgraph (`nodes` +
 * `edges`, computed via `computeGraphNeighborhood` above) instead of just direct
 * prerequisites/dependents, so a "+ layer"/"- layer" control on the host page can widen or narrow
 * how many hops out from `centerId` are shown.
 *
 * Zoom/magnifier (2026-07-23) — same zoom in/out/fit-to-view controls and find-by-title search
 * (with prev/next match navigation + highlight) as the whole-graph viz on the main Skill Graph
 * page (`sp-admin-skill-graph-viz`'s `.sp-sgv-*` pattern), reused here as `.sp-ngp-*` since a
 * multi-layer preview can grow to dozens of nodes just like the full graph can.
 *
 * Area (marquee) zoom (2026-07-23) — user follow-up: "I don't see area select zoom icon, area
 * select zoom out icon". The `⊞` toggle enables drag-to-select: while active, dragging on the
 * canvas draws a rectangle (a plain absolutely-positioned overlay div, not a cytoscape feature)
 * and releasing zooms/pans to fit exactly that rectangle — converted from screen pixels to model
 * coordinates via the current pan/zoom, then `cy.fit({ boundingBox })`, which (unlike fitting to
 * elements) works for any arbitrary region including empty space. `⊟` exits that mode and fits
 * back to the whole visible subgraph in one step (the "zoom out" counterpart).
 */
@Component({
  selector: 'sp-admin-node-graph-preview',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (!centerId) {
      <div class="sp-ngp-empty">No node selected.</div>
    } @else {
      <div class="sp-ngp-toolbar">
        <input
          type="text"
          class="sp-ngp-search"
          placeholder="Find a node by title…"
          [(ngModel)]="searchTerm"
          (input)="onSearchInput()"
        />
        @if (searchMatches.length > 0) {
          <span class="sp-ngp-search-nav">
            <button type="button" class="sp-ngp-icon-btn" (click)="jumpToMatch(-1)" title="Previous match">‹</button>
            {{ searchMatchIndex + 1 }}/{{ searchMatches.length }}
            <button type="button" class="sp-ngp-icon-btn" (click)="jumpToMatch(1)" title="Next match">›</button>
          </span>
        } @else if (searchTerm) {
          <span class="sp-ngp-search-nav sp-ngp-search-nav--empty">No match</span>
        }
      </div>
      <div class="sp-ngp-canvas-wrap">
        <div #cyContainer class="sp-ngp-canvas" [class.sp-ngp-canvas--area-zoom]="areaZoomActive" (mousedown)="onAreaZoomMouseDown($event)"></div>
        <div class="sp-ngp-zoom-controls">
          <button type="button" class="sp-ngp-icon-btn" (click)="zoomBy(1.3)" title="Zoom in">+</button>
          <button type="button" class="sp-ngp-icon-btn" (click)="zoomBy(1 / 1.3)" title="Zoom out">−</button>
          <button type="button" class="sp-ngp-icon-btn" (click)="fitToView()" title="Fit to view">⤢</button>
          <button type="button" class="sp-ngp-icon-btn" [class.sp-ngp-icon-btn--active]="areaZoomActive" (click)="toggleAreaZoom()" title="Area zoom in — drag to select a region"><i class="fa-solid fa-magnifying-glass-plus"></i></button>
          <button type="button" class="sp-ngp-icon-btn" (click)="areaZoomOut()" title="Area zoom out — back to the whole graph"><i class="fa-solid fa-magnifying-glass-minus"></i></button>
        </div>
      </div>
    }
  `,
  styles: [`
    .sp-ngp-toolbar { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
    .sp-ngp-search {
      font-size: 11px; padding: 4px 8px; border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 6px; width: 200px; color: var(--sp-admin-text, #211B36);
    }
    .sp-ngp-search-nav { display: inline-flex; align-items: center; gap: 4px; font-size: 11px; color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-ngp-search-nav--empty { color: var(--sp-admin-danger, #DC2626); }
    .sp-ngp-canvas-wrap { position: relative; }
    .sp-ngp-canvas {
      position: relative;
      width: 100%; height: 300px;
      border: 1px solid var(--sp-admin-border, #ECE9F5); border-radius: 10px;
      background: var(--sp-admin-surface, #fff);
    }
    .sp-ngp-canvas--area-zoom { cursor: crosshair; }
    .sp-ngp-zoom-controls {
      position: absolute; bottom: 12px; right: 12px; display: flex; flex-direction: column; gap: 4px;
      background: var(--sp-admin-surface, #fff); border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 8px; padding: 4px; box-shadow: 0 2px 8px rgba(0,0,0,.08);
    }
    .sp-ngp-icon-btn {
      width: 26px; height: 26px; border: none; background: none; cursor: pointer;
      font-size: 15px; font-weight: 700; color: var(--sp-admin-text, #211B36); border-radius: 5px;
      display: flex; align-items: center; justify-content: center; line-height: 1;
    }
    .sp-ngp-icon-btn:hover { background: var(--sp-admin-border, #ECE9F5); }
    .sp-ngp-icon-btn--active { background: #5B4BE8; color: #fff; }
    .sp-ngp-icon-btn--active:hover { background: #4A3BC7; }
    .sp-ngp-empty { font-size: 12.5px; color: var(--sp-admin-text-muted, #8B85A0); padding: 8px 0; }
  `],
})
export class SpAdminNodeGraphPreviewComponent implements OnChanges, OnDestroy {
  @Input() centerId: string | null = null;
  @Input() nodes: NodeGraphPreviewNode[] = [];
  @Input() edges: NodeGraphPreviewEdge[] = [];
  @Output() nodeClick = new EventEmitter<string>();

  @ViewChild('cyContainer') container?: ElementRef<HTMLDivElement>;

  // Zoom/magnifier (2026-07-23) — mirrors sp-admin-skill-graph-viz's find-by-title search.
  searchTerm = '';
  searchMatches: string[] = [];
  searchMatchIndex = -1;

  // Area (marquee) zoom (2026-07-23).
  areaZoomActive = false;
  private areaZoomStart: { x: number; y: number } | null = null;
  private areaZoomBox: HTMLDivElement | null = null;
  private areaZoomMoveHandler = (e: MouseEvent) => this.onAreaZoomMouseMove(e);
  private areaZoomUpHandler = (e: MouseEvent) => this.onAreaZoomMouseUp(e);

  private cy: Core | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['centerId'] || changes['nodes'] || changes['edges']) {
      // Wait a tick for *ngIf/@if to attach #cyContainer before rendering into it.
      setTimeout(() => this.render(), 0);
    }
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.areaZoomMoveHandler);
    document.removeEventListener('mouseup', this.areaZoomUpHandler);
    this.areaZoomBox?.remove();
    this.cy?.destroy();
  }

  zoomBy(factor: number): void {
    if (!this.cy) return;
    const level = Math.max(this.cy.minZoom(), Math.min(this.cy.maxZoom(), this.cy.zoom() * factor));
    this.cy.animate({ zoom: level, duration: 150 });
  }

  fitToView(): void {
    this.cy?.animate({ fit: { eles: this.cy.elements(), padding: 30 }, duration: 200 });
  }

  onSearchInput(): void {
    if (!this.cy || !this.searchTerm.trim()) {
      this.searchMatches = [];
      this.searchMatchIndex = -1;
      return;
    }
    const term = this.searchTerm.trim().toLowerCase();
    this.searchMatches = this.cy
      .nodes()
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
    this.cy.elements().removeClass('sp-ngp-search-highlight');
    ele.addClass('sp-ngp-search-highlight');
  }

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

  private render(): void {
    this.cy?.destroy();
    this.cy = null;
    this.searchTerm = '';
    this.searchMatches = [];
    this.searchMatchIndex = -1;
    this.areaZoomActive = false;
    this.areaZoomBox?.remove();
    this.areaZoomBox = null;
    this.areaZoomStart = null;
    if (!this.container || !this.centerId) return;

    const centerId = this.centerId;
    const elements: ElementDefinition[] = [
      ...this.nodes.map(n => ({
        data: { id: n.id, label: n.title, kind: n.id === centerId ? 'center' : 'neighbor', pending: n.pending ?? 'none' },
      })),
      ...this.edges.map(e => ({
        data: { id: `${e.source}->${e.target}`, source: e.source, target: e.target, pending: e.pending ?? 'none' },
      })),
    ];

    this.cy = cytoscape({
      container: this.container.nativeElement,
      elements,
      style: [
        {
          selector: 'node',
          style: {
            shape: 'round-rectangle',
            'background-color': '#EDEBFF',
            'border-width': 1.5,
            'border-color': '#C0BAF9',
            label: 'data(label)',
            'font-size': '11px',
            color: '#3A2EA8',
            'text-valign': 'center',
            'text-halign': 'center',
            'text-wrap': 'wrap',
            'text-max-width': '110px',
            width: 'label',
            height: 'label',
            padding: '10px',
          },
        },
        {
          selector: 'node[kind = "center"]',
          style: {
            'background-color': '#5B4BE8',
            'border-width': 3,
            'border-color': '#F0982C',
            color: '#fff',
            'font-weight': 700,
          },
        },
        {
          selector: 'node[pending = "add"]',
          style: {
            'background-color': '#FFF4E0',
            'border-width': 2,
            'border-color': '#F0982C',
            'border-style': 'dashed',
            color: '#8A5A00',
          },
        },
        {
          selector: 'node[pending = "remove"]',
          style: {
            'background-color': '#F5F4FA',
            'border-width': 2,
            'border-color': '#BDB8CC',
            'border-style': 'dashed',
            color: '#8B85A0',
            'text-opacity': 0.7,
          },
        },
        {
          selector: 'edge',
          style: {
            width: 1.5,
            'line-color': '#5B4BE8',
            'target-arrow-color': '#5B4BE8',
            'target-arrow-shape': 'triangle',
            'curve-style': 'taxi',
            'taxi-direction': 'rightward',
            'taxi-turn': '50%',
            opacity: 0.85,
          },
        },
        {
          selector: 'edge[pending = "add"]',
          style: { 'line-color': '#F0982C', 'target-arrow-color': '#F0982C', 'line-style': 'dashed' },
        },
        {
          selector: 'edge[pending = "remove"]',
          style: { 'line-color': '#BDB8CC', 'target-arrow-color': '#BDB8CC', 'line-style': 'dashed', opacity: 0.6 },
        },
        {
          selector: '.sp-ngp-search-highlight',
          style: { 'border-width': 3, 'border-color': '#F0982C', 'z-index': 999 },
        },
      ],
      layout: {
        name: 'elk',
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        elk: { algorithm: 'layered', 'elk.direction': 'RIGHT', 'elk.spacing.nodeNode': 32, 'elk.layered.spacing.nodeNodeBetweenLayers': 80 },
        fit: true,
        padding: 30,
      } as unknown as cytoscape.LayoutOptions,
      wheelSensitivity: 0.2,
      minZoom: 0.2,
      maxZoom: 3,
      autoungrabify: true,
    });

    this.cy.on('tap', 'node', evt => {
      const id = evt.target.id();
      if (id !== centerId) this.nodeClick.emit(id);
    });
  }
}

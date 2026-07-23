import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import cytoscape, { Core, ElementDefinition } from 'cytoscape';
import coseBilkent from 'cytoscape-cose-bilkent';
import { SkillGraphEdge, SkillGraphNode } from '../../../../core/models/admin.models';

cytoscape.use(coseBilkent);

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
const CEFR_LEVELS = Object.keys(CEFR_COLORS);

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
      @for (level of cefrLevels; track level) {
        <button
          type="button"
          class="sp-sgv-legend-item"
          [class.sp-sgv-legend-item--off]="!activeLevels.has(level)"
          (click)="toggleLevel(level)"
        >
          <span class="sp-sgv-legend-dot" [style.background]="cefrColor(level)"></span>{{ level }}
        </button>
      }
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
      <span class="sp-sgv-legend-count">{{ visibleCount }} of {{ nodes.length }} nodes shown</span>
    </div>
    <div class="sp-sgv-canvas-wrap">
      <div #cyContainer class="sp-sgv-canvas" [class.sp-sgv-canvas--area-zoom]="areaZoomActive" (mousedown)="onAreaZoomMouseDown($event)"></div>
      <div class="sp-sgv-zoom-controls">
        <button type="button" class="sp-sgv-icon-btn" (click)="zoomBy(1.3)" title="Zoom in">+</button>
        <button type="button" class="sp-sgv-icon-btn" (click)="zoomBy(1 / 1.3)" title="Zoom out">−</button>
        <button type="button" class="sp-sgv-icon-btn" (click)="fitToView()" title="Fit to view">⤢</button>
        <button type="button" class="sp-sgv-icon-btn" [class.sp-sgv-icon-btn--active]="areaZoomActive" (click)="toggleAreaZoom()" title="Area zoom in — drag to select a region"><i class="fa-solid fa-magnifying-glass-plus"></i></button>
        <button type="button" class="sp-sgv-icon-btn" (click)="areaZoomOut()" title="Area zoom out — back to the whole graph"><i class="fa-solid fa-magnifying-glass-minus"></i></button>
      </div>
    </div>
  `,
  styles: [`
    .sp-sgv-legend { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; margin-bottom: 8px; }
    .sp-sgv-legend-item {
      display: inline-flex; align-items: center; gap: 5px; font-size: 11px;
      color: var(--sp-admin-text-muted, #8B85A0); background: none; border: none; cursor: pointer;
      padding: 2px 6px; border-radius: 6px;
    }
    .sp-sgv-legend-item:hover { background: var(--sp-admin-border, #ECE9F5); }
    .sp-sgv-legend-item--off { opacity: 0.35; }
    .sp-sgv-legend-dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
    .sp-sgv-search {
      font-size: 11px; padding: 4px 8px; border: 1px solid var(--sp-admin-border, #ECE9F5);
      border-radius: 6px; width: 160px; color: var(--sp-admin-text, #211B36);
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
  `],
})
export class SpAdminSkillGraphVizComponent implements OnChanges, OnDestroy {
  @Input() nodes: SkillGraphNode[] = [];
  @Input() edges: SkillGraphEdge[] = [];
  @Output() nodeSelected = new EventEmitter<SkillGraphNode>();

  @ViewChild('cyContainer', { static: true }) container!: ElementRef<HTMLDivElement>;

  readonly cefrLevels = CEFR_LEVELS;
  activeLevels = new Set<string>(CEFR_LEVELS);
  visibleCount = 0;

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

  toggleLevel(level: string): void {
    if (this.activeLevels.has(level)) {
      if (this.activeLevels.size === 1) return; // never allow zero levels selected
      this.activeLevels.delete(level);
    } else {
      this.activeLevels.add(level);
    }
    this.render();
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

    const visibleNodes = this.nodes.filter(n => this.activeLevels.has(n.cefrLevel));
    this.visibleCount = visibleNodes.length;
    if (visibleNodes.length === 0) return;

    const nodeIds = new Set(visibleNodes.map(n => n.id));
    const skillsPresent = new Set(visibleNodes.map(n => n.skill || 'other'));

    const elements: ElementDefinition[] = [
      // Compound parent boxes, one per Skill actually present in the current filtered view.
      ...Array.from(skillsPresent).map(skill => ({
        data: { id: `skill:${skill}`, label: this.skillLabel(skill), isParent: true },
      })),
      ...visibleNodes.map(n => ({
        data: {
          id: n.id,
          label: n.title,
          cefrLevel: n.cefrLevel,
          parent: `skill:${n.skill || 'other'}`,
        },
      })),
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
          selector: 'node[!isParent]',
          style: {
            'background-color': (ele: cytoscape.NodeSingular) => this.cefrColor(ele.data('cefrLevel')),
            label: 'data(label)',
            'font-size': '9px',
            color: '#211B36',
            'text-valign': 'bottom',
            'text-halign': 'center',
            'text-margin-y': 4,
            width: 22,
            height: 22,
            'text-wrap': 'wrap',
            'text-max-width': '90px',
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
      layout: {
        name: 'cose-bilkent',
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ...({ nodeDimensionsIncludeLabels: true, animate: false, padding: 30, idealEdgeLength: 80 } as any),
      } as cytoscape.LayoutOptions,
      wheelSensitivity: 0.2,
      minZoom: 0.1,
      maxZoom: 4,
    });

    this.cy.on('tap', 'node', evt => {
      if (evt.target.data('isParent')) return;
      const id = evt.target.id();
      const node = this.nodes.find(n => n.id === id);
      if (node) this.nodeSelected.emit(node);
    });
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

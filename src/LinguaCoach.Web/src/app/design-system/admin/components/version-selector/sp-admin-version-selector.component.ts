import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SpAdminVersion {
  id: string;
  version: number;
  isActive: boolean;
}

@Component({
  selector: 'sp-admin-version-selector',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-vs-btns">
      @for (v of versions; track v.id) {
        <button
          type="button"
          class="sp-vs-btn"
          [class.sp-vs-btn--active]="v.id === selectedId"
          (click)="versionChange.emit(v)">
          v{{ v.version }}
          @if (v.isActive) {
            <span class="sp-vs-dot" [class.sp-vs-dot--cur]="v.id === selectedId"></span>
          }
        </button>
      }
    </div>
    @if (selectedVersion) {
      @if (selectedVersion.isActive) {
        <div class="sp-vs-status sp-vs-status--active">&#9679; v{{ selectedVersion.version }} is the active version</div>
      } @else {
        <div class="sp-vs-status sp-vs-status--inactive">&#9679; v{{ selectedVersion.version }} is inactive</div>
      }
    }
  `,
  styles: [`
    .sp-vs-btns { display:flex; gap:6px; flex-wrap:wrap; margin-bottom:8px; }
    .sp-vs-btn {
      display:inline-flex; align-items:center; gap:4px;
      min-width:52px; height:28px; padding:0 12px;
      border-radius:7px; font-size:13px; font-weight:600;
      font-family:inherit; cursor:pointer; transition:background .08s;
      border:1.5px solid var(--sp-admin-border,#ECE9F5);
      background:#fff; color:var(--sp-admin-text,#0F172A);
    }
    .sp-vs-btn:hover { background:var(--sp-admin-surface-subtle,#FBFAFE); }
    .sp-vs-btn--active { background:#5B4BE8; color:#fff; border-color:#5B4BE8; }
    .sp-vs-btn--active:hover { background:#3A2EA8; }
    .sp-vs-dot {
      width:6px; height:6px; border-radius:50%;
      background:#13B07C; flex-shrink:0; display:inline-block;
    }
    .sp-vs-dot--cur { background:#fff; }
    .sp-vs-status { font-size:12px; font-weight:600; }
    .sp-vs-status--active { color:#13B07C; }
    .sp-vs-status--inactive { color:var(--sp-admin-text-muted,#8B85A0); }
  `],
})
export class SpAdminVersionSelectorComponent {
  @Input() versions: SpAdminVersion[] = [];
  @Input() selectedId: string | null = null;
  @Output() versionChange = new EventEmitter<SpAdminVersion>();

  get selectedVersion(): SpAdminVersion | undefined {
    return this.versions.find(v => v.id === this.selectedId);
  }
}

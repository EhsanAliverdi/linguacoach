import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type PlaceholderState =
  | 'not-available'
  | 'not-implemented'
  | 'foundation-only'
  | 'coming-later'
  | 'deferred';

export type PlaceholderSkeleton =
  | 'none'
  | 'chart'
  | 'ring'
  | 'timeline'
  | 'grid';

@Component({
  selector: 'sp-admin-visual-placeholder',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-vp-root sp-vp-{{ state }}" [attr.aria-label]="title || stateLabel">

      @if (skeleton !== 'none') {
        <div class="sp-vp-skeleton sp-vp-sk-{{ skeleton }}" aria-hidden="true">

          @switch (skeleton) {
            @case ('chart') {
              <svg viewBox="0 0 120 52" xmlns="http://www.w3.org/2000/svg" class="sp-vp-sk-svg">
                <rect x="4"  y="28" width="10" height="24" rx="2" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <rect x="18" y="18" width="10" height="34" rx="2" class="sp-vp-sk-bar sp-vp-sk-b2"/>
                <rect x="32" y="32" width="10" height="20" rx="2" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <rect x="46" y="10" width="10" height="42" rx="2" class="sp-vp-sk-bar sp-vp-sk-b3"/>
                <rect x="60" y="22" width="10" height="30" rx="2" class="sp-vp-sk-bar sp-vp-sk-b2"/>
                <rect x="74" y="14" width="10" height="38" rx="2" class="sp-vp-sk-bar sp-vp-sk-b3"/>
                <rect x="88" y="26" width="10" height="26" rx="2" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <rect x="102" y="8" width="10" height="44" rx="2" class="sp-vp-sk-bar sp-vp-sk-b2"/>
              </svg>
            }
            @case ('ring') {
              <svg viewBox="0 0 52 52" xmlns="http://www.w3.org/2000/svg" class="sp-vp-sk-svg sp-vp-sk-svg--ring">
                <circle cx="26" cy="26" r="20" fill="none" stroke-width="7" class="sp-vp-sk-ring-track"/>
                <circle cx="26" cy="26" r="20" fill="none" stroke-width="7" stroke-linecap="round"
                  stroke-dasharray="75.4 125.7" stroke-dashoffset="31.4" class="sp-vp-sk-ring-arc sp-vp-sk-b3"/>
              </svg>
            }
            @case ('timeline') {
              <svg viewBox="0 0 120 52" xmlns="http://www.w3.org/2000/svg" class="sp-vp-sk-svg">
                <line x1="10" y1="10" x2="10" y2="44" stroke-width="1.5" class="sp-vp-sk-tl-line"/>
                <circle cx="10" cy="10" r="3" class="sp-vp-sk-bar sp-vp-sk-b2"/>
                <rect x="20" y="6"  width="46" height="7" rx="3" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <circle cx="10" cy="22" r="3" class="sp-vp-sk-bar sp-vp-sk-b3"/>
                <rect x="20" y="18" width="62" height="7" rx="3" class="sp-vp-sk-bar sp-vp-sk-b2"/>
                <circle cx="10" cy="34" r="3" class="sp-vp-sk-bar sp-vp-sk-b2"/>
                <rect x="20" y="30" width="38" height="7" rx="3" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <circle cx="10" cy="44" r="3" class="sp-vp-sk-bar sp-vp-sk-b1"/>
                <rect x="20" y="40" width="54" height="7" rx="3" class="sp-vp-sk-bar sp-vp-sk-b3"/>
              </svg>
            }
            @case ('grid') {
              <svg viewBox="0 0 120 52" xmlns="http://www.w3.org/2000/svg" class="sp-vp-sk-svg">
                @for (cell of gridCells; track cell.key) {
                  <rect [attr.x]="cell.x" [attr.y]="cell.y" width="16" height="12" rx="2" [class]="'sp-vp-sk-bar ' + cell.tone" />
                }
              </svg>
            }
          }

        </div>
      }

      <div class="sp-vp-body">
        <div class="sp-vp-icon" aria-hidden="true">
          @switch (state) {
            @case ('not-available') {
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" width="18" height="18">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 3v11.25A2.25 2.25 0 0 0 6 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0 1 18 16.5h-2.25m-7.5 0h7.5m-7.5 0-1 3m8.5-3 1 3m0 0 .5 1.5m-.5-1.5h-9.5m0 0-.5 1.5M9 11.25v1.5M12 9v3.75m3-6v6" />
              </svg>
            }
            @case ('foundation-only') {
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" width="18" height="18">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z" />
                <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
              </svg>
            }
            @default {
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" width="18" height="18">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
              </svg>
            }
          }
        </div>
        <div class="sp-vp-content">
          <div class="sp-vp-label">{{ title || stateLabel }}</div>
          @if (message) {
            <div class="sp-vp-msg">{{ message }}</div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sp-vp-root {
      border-radius: 10px;
      border: 1px dashed var(--sp-admin-border, #ECE9F5);
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      overflow: hidden;
    }
    .sp-vp-skeleton {
      width: 100%;
      padding: 12px 12px 8px;
      border-bottom: 1px solid var(--sp-admin-border, #ECE9F5);
    }
    .sp-vp-sk-svg { width: 100%; display: block; }
    .sp-vp-sk-svg--ring { width: 52px; height: 52px; margin: 0 auto; display: block; }

    /* skeleton tones — no numbers, purely decorative opacity bands */
    .sp-vp-sk-bar   { fill: var(--sp-admin-border, #ECE9F5); }
    .sp-vp-sk-b1    { opacity: 0.55; }
    .sp-vp-sk-b2    { opacity: 0.80; }
    .sp-vp-sk-b3    { opacity: 1.00; }
    .sp-vp-sk-ring-track { stroke: var(--sp-admin-border, #ECE9F5); }
    .sp-vp-sk-ring-arc   { stroke: var(--sp-admin-primary, #5B4BE8); opacity: 0.25; }
    .sp-vp-sk-tl-line { stroke: var(--sp-admin-border, #ECE9F5); }

    .sp-vp-body {
      display: flex; align-items: center; gap: 10px;
      padding: 12px 14px;
    }
    .sp-vp-icon {
      flex-shrink: 0; width: 28px; height: 28px; border-radius: 7px;
      display: flex; align-items: center; justify-content: center;
    }
    .sp-vp-not-available .sp-vp-icon    { background: var(--sp-admin-border, #ECE9F5);      color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-vp-foundation-only .sp-vp-icon  { background: var(--sp-admin-amber-bg, #FFFBEB);     color: var(--sp-admin-amber, #D97706); }
    .sp-vp-not-implemented .sp-vp-icon  { background: var(--sp-admin-border, #ECE9F5);      color: var(--sp-admin-text-muted, #8B85A0); }
    .sp-vp-coming-later .sp-vp-icon     { background: var(--sp-admin-primary-bg, #EDEBFF);  color: var(--sp-admin-primary, #5B4BE8); }
    .sp-vp-deferred .sp-vp-icon         { background: var(--sp-admin-border, #ECE9F5);      color: var(--sp-admin-text-dim, #BDB8CC); }
    .sp-vp-content { min-width: 0; }
    .sp-vp-label { font-size: 12px; font-weight: 700; color: var(--sp-admin-text-secondary, #4B4462); }
    .sp-vp-msg   { font-size: 11px; color: var(--sp-admin-text-muted, #8B85A0); margin-top: 2px; line-height: 1.4; }
  `],
})
export class SpAdminVisualPlaceholderComponent {
  readonly gridCells: { key: string; x: number; y: number; tone: string }[] = (() => {
    const cells = [];
    for (let row = 0; row < 3; row++) {
      for (let col = 0; col < 6; col++) {
        const v = (row * 7 + col * 3) % 3;
        cells.push({
          key: `${row}-${col}`,
          x: 4 + col * 20,
          y: 4 + row * 16,
          tone: v === 2 ? 'sp-vp-sk-b3' : v === 1 ? 'sp-vp-sk-b2' : 'sp-vp-sk-b1',
        });
      }
    }
    return cells;
  })();

  @Input() state: PlaceholderState = 'not-available';
  @Input() title = '';
  @Input() message = '';
  @Input() skeleton: PlaceholderSkeleton = 'none';

  get stateLabel(): string {
    const map: Record<PlaceholderState, string> = {
      'not-available':   'Backend not available yet',
      'not-implemented': 'Not implemented',
      'foundation-only': 'Foundation only',
      'coming-later':    'Coming later',
      'deferred':        'Deferred',
    };
    return map[this.state];
  }

}

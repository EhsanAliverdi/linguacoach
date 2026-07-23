import { Component, ElementRef, EventEmitter, HostListener, Input, Output, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SpAdminMultiSelectOption {
  value: string;
  label: string;
  /** Shown in smaller muted text next to the label, e.g. "A1 · grammar". */
  sublabel?: string;
}

export type SpAdminMultiSelectSize = 'sm' | 'md';

/**
 * Reusable Choice.js-style multi-select: a search box that opens a filtered dropdown of options,
 * with selected options shown as removable chips inside the control. Built for the Skill Graph's
 * prerequisite/unlock pickers (2026-07-23) — previously each picker was a one-off "search input +
 * list of plain buttons" pattern hand-rolled per screen (Create panel, node detail slide-over, the
 * dedicated Edit page), duplicated 3 times with no shared component.
 *
 * Two modes, both driven by the same dropdown/search UI:
 * - `accumulate` (default): a real controlled multi-select via ControlValueAccessor — picking an
 *   option adds it to `value` (string[]) and renders it as a removable chip. Use with
 *   `[(ngModel)]="selectedIds"` when the caller wants to stage several picks before submitting
 *   (e.g. Create node's prerequisites).
 * - `accumulate=false`: picking an option only emits `(optionPicked)` and clears the search box —
 *   nothing is rendered as a chip inside this component. Use when each pick should immediately
 *   trigger an external mutation (e.g. View/Edit's "Add prerequisite", which POSTs on every pick
 *   and shows the real result in a separate, already-existing list elsewhere on the page).
 *
 * `excludeValues` additionally hides options from the dropdown regardless of mode (e.g. the node's
 * own id, or ids already linked as real prerequisites) — kept separate from `value` so
 * `accumulate=false` callers still get correct filtering without needing to fake a `value`.
 */
@Component({
  selector: 'sp-admin-multi-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminMultiSelectComponent),
      multi: true,
    },
  ],
  template: `
    <div class="sp-adm-ms" [class.sp-adm-ms-open]="open" [class.sp-adm-ms-disabled]="disabled">
      <div class="sp-adm-ms-control" [class]="'sp-adm-ms-control-' + size" (click)="focusSearch()">
        @for (chip of selectedOptions(); track chip.value) {
          <span class="sp-adm-ms-chip">
            <span>{{ chip.label }}</span>
            @if (chip.sublabel) { <span class="sp-adm-ms-chip-sub">{{ chip.sublabel }}</span> }
            <button type="button" class="sp-adm-ms-chip-remove" (click)="removeChip(chip.value, $event)" aria-label="Remove">×</button>
          </span>
        }
        <input
          #searchInput
          class="sp-adm-ms-input"
          type="text"
          [placeholder]="selectedOptions().length === 0 ? placeholder : ''"
          [disabled]="disabled"
          [(ngModel)]="searchTerm"
          (ngModelChange)="onSearchChange()"
          (focus)="openPanel()"
          (keydown)="onKeydown($event)"
        />
      </div>

      @if (open && filteredOptions().length > 0) {
        <ul class="sp-adm-ms-panel" role="listbox">
          @for (opt of filteredOptions(); track opt.value; let i = $index) {
            <li
              class="sp-adm-ms-option"
              [class.sp-adm-ms-option-active]="i === highlightedIndex"
              (mouseenter)="highlightedIndex = i"
              (click)="pick(opt)"
              role="option"
            >
              <span>{{ opt.label }}</span>
              @if (opt.sublabel) { <span class="sp-adm-ms-option-sub">{{ opt.sublabel }}</span> }
            </li>
          }
        </ul>
      } @else if (open && searchTerm.trim().length > 0) {
        <ul class="sp-adm-ms-panel">
          <li class="sp-adm-ms-empty">No matches.</li>
        </ul>
      }
    </div>
  `,
  styles: [`
    :host { display:block; min-width:0; }
    .sp-adm-ms { position:relative; }

    .sp-adm-ms-control {
      box-sizing:border-box; width:100%; min-height:36px;
      display:flex; flex-wrap:wrap; align-items:center; gap:6px;
      padding:4px 8px;
      border-radius:8px; border:1.5px solid #E2DEF0;
      background:#fff; cursor:text;
      transition:border-color .15s;
    }
    .sp-adm-ms-open .sp-adm-ms-control { border-color:#5B4BE8; }
    .sp-adm-ms-control-sm { min-height:30px; padding:2px 6px; }
    .sp-adm-ms-disabled .sp-adm-ms-control { opacity:.55; cursor:not-allowed; background:#FBFAFE; }

    .sp-adm-ms-chip {
      display:inline-flex; align-items:center; gap:6px;
      background:#EDEBFF; color:#3A2EA8;
      border-radius:999px; padding:4px 6px 4px 12px;
      font-size:12.5px; font-weight:600; line-height:1.3;
      max-width:100%;
    }
    .sp-adm-ms-chip-sub { color:#7A6FC2; font-weight:500; }
    .sp-adm-ms-chip-remove {
      display:inline-flex; align-items:center; justify-content:center;
      width:18px; height:18px; flex-shrink:0;
      border:none; border-radius:999px; background:rgba(91,75,232,.14); color:#5B4BE8; cursor:pointer;
      font-size:12px; line-height:1; padding:0; font-weight:700;
      transition:background .15s, color .15s;
    }
    .sp-adm-ms-chip-remove:hover { background:#EF4444; color:#fff; }

    .sp-adm-ms-input {
      flex:1 1 80px; min-width:80px; border:none; outline:none;
      font-size:13.5px; font-family:inherit; color:#211B36; background:transparent;
      padding:4px 2px;
    }
    .sp-adm-ms-input::placeholder { color:#BDB8CC; }
    .sp-adm-ms-disabled .sp-adm-ms-input { cursor:not-allowed; }

    .sp-adm-ms-panel {
      position:absolute; z-index:30; top:calc(100% + 4px); left:0; right:0;
      max-height:220px; overflow-y:auto;
      background:#fff; border:1px solid var(--sp-admin-border,#ECE9F5); border-radius:8px;
      box-shadow:0 8px 24px rgba(33,27,54,.12);
      margin:0; padding:4px; list-style:none;
    }
    .sp-adm-ms-option {
      display:flex; align-items:center; gap:8px; justify-content:space-between;
      padding:7px 10px; border-radius:6px; cursor:pointer; font-size:13px; color:#211B36;
    }
    .sp-adm-ms-option-active, .sp-adm-ms-option:hover { background:#F6F4FB; }
    .sp-adm-ms-option-sub { color:var(--sp-admin-text-muted,#8B85A0); font-size:12px; flex-shrink:0; }
    .sp-adm-ms-empty { padding:10px; font-size:12.5px; color:var(--sp-admin-text-muted,#8B85A0); }
  `],
})
export class SpAdminMultiSelectComponent implements ControlValueAccessor {
  @Input() options: SpAdminMultiSelectOption[] = [];
  @Input() excludeValues: string[] = [];
  @Input() placeholder = 'Search…';
  @Input() size: SpAdminMultiSelectSize = 'md';
  @Input() accumulate = true;
  @Input() maxResults = 15;

  private _disabled = false;
  @Input()
  get disabled(): boolean { return this._disabled; }
  set disabled(v: boolean) { this._disabled = v; }

  /** Fires on every option pick, in both modes — the one signal `accumulate=false` callers need. */
  @Output() optionPicked = new EventEmitter<SpAdminMultiSelectOption>();

  searchTerm = '';
  open = false;
  highlightedIndex = 0;

  private value: string[] = [];
  private onChange: (value: string[]) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private host: ElementRef<HTMLElement>) {}

  selectedOptions(): SpAdminMultiSelectOption[] {
    if (!this.accumulate) return [];
    return this.options.filter(o => this.value.includes(o.value));
  }

  filteredOptions(): SpAdminMultiSelectOption[] {
    const q = this.searchTerm.trim().toLowerCase();
    const hidden = new Set([...this.value, ...this.excludeValues]);
    let candidates = this.options.filter(o => !hidden.has(o.value));
    if (q.length > 0) {
      candidates = candidates.filter(o =>
        o.label.toLowerCase().includes(q) || (o.sublabel ?? '').toLowerCase().includes(q));
    }
    return candidates.slice(0, this.maxResults);
  }

  openPanel(): void {
    if (this.disabled) return;
    this.open = true;
    this.highlightedIndex = 0;
  }

  closePanel(): void {
    this.open = false;
  }

  focusSearch(): void {
    if (this.disabled) return;
    this.openPanel();
  }

  onSearchChange(): void {
    this.highlightedIndex = 0;
    if (!this.open) this.openPanel();
  }

  pick(option: SpAdminMultiSelectOption): void {
    if (this.accumulate && !this.value.includes(option.value)) {
      this.value = [...this.value, option.value];
      this.onChange(this.value);
    }
    this.optionPicked.emit(option);
    this.searchTerm = '';
    this.highlightedIndex = 0;
    // Stays open (Choice.js multi-select behavior) so several options can be picked in a row.
  }

  removeChip(value: string, event: Event): void {
    event.stopPropagation();
    this.value = this.value.filter(v => v !== value);
    this.onChange(this.value);
  }

  onKeydown(event: KeyboardEvent): void {
    const results = this.filteredOptions();
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.openPanel();
      this.highlightedIndex = Math.min(this.highlightedIndex + 1, Math.max(results.length - 1, 0));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.highlightedIndex = Math.max(this.highlightedIndex - 1, 0);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const chosen = results[this.highlightedIndex];
      if (chosen) this.pick(chosen);
    } else if (event.key === 'Escape') {
      this.closePanel();
    } else if (event.key === 'Backspace' && this.searchTerm.length === 0 && this.accumulate && this.value.length > 0) {
      this.value = this.value.slice(0, -1);
      this.onChange(this.value);
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open && !this.host.nativeElement.contains(event.target as Node)) {
      this.closePanel();
      this.onTouched();
    }
  }

  writeValue(value: string[] | null): void { this.value = value ?? []; }
  registerOnChange(fn: (value: string[]) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this._disabled = isDisabled; }
}

import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SpAdminIconComponent, SpAdminIconName } from '../icon/sp-admin-icon.component';

export interface SpAdminTabItem {
  value: string;
  label: string;
  icon?: SpAdminIconName;
  /** Small count badge shown after the label (e.g. unread count). */
  count?: number;
  disabled?: boolean;
}

export type SpAdminTabsVariant = 'underline' | 'default' | 'pills' | 'full-width' | 'vertical';

let nextId = 0;

/**
 * Tab strip for navigating between sections of an admin page (Import Content's
 * New/History, Student Detail's Overview/Activity/Settings, etc). Renders only the
 * tab strip — the host page owns which panel is shown, exactly like the hand-rolled
 * `.sp-admin-tab-bar` markup this replaces. `variant` swaps between Flowbite's tab
 * layouts; icons are set per-item via `SpAdminTabItem.icon`, not a separate variant.
 *
 * Selection is one-way in + an output, matching this codebase's existing tab pages
 * (host keeps its own `activeTab` signal/property and re-renders panels from it).
 */
@Component({
  selector: 'sp-admin-tabs',
  standalone: true,
  imports: [CommonModule, FormsModule, SpAdminIconComponent],
  template: `
    @switch (variant) {
      @case ('underline') {
        <div class="border-b border-default">
          <ul class="flex flex-wrap -mb-px text-sm font-medium text-center text-body" role="tablist" [attr.aria-label]="ariaLabel || null">
            @for (tab of tabs; track tab.value) {
              <li class="me-2" role="presentation">
                <button type="button" role="tab" [disabled]="tab.disabled"
                  [attr.aria-selected]="tab.value === active"
                  [class]="underlineTabClass(tab)"
                  (click)="select(tab)">
                  @if (tab.icon) {
                    <sp-admin-icon [name]="tab.icon" size="sm" [tone]="tab.value === active ? 'primary' : 'muted'" class="me-2" />
                  }
                  {{ tab.label }}
                  @if (tab.count !== undefined) {
                    <span class="sp-admin-tab-count">{{ tab.count }}</span>
                  }
                </button>
              </li>
            }
          </ul>
        </div>
      }
      @case ('default') {
        <ul class="flex flex-wrap text-sm font-medium text-center text-body border-b border-default" role="tablist" [attr.aria-label]="ariaLabel || null">
          @for (tab of tabs; track tab.value) {
            <li class="me-2" role="presentation">
              <button type="button" role="tab" [disabled]="tab.disabled"
                [attr.aria-selected]="tab.value === active"
                [class]="defaultTabClass(tab)"
                (click)="select(tab)">
                @if (tab.icon) {
                  <sp-admin-icon [name]="tab.icon" size="sm" [tone]="tab.value === active ? 'primary' : 'muted'" class="me-2" />
                }
                {{ tab.label }}
                @if (tab.count !== undefined) {
                  <span class="sp-admin-tab-count">{{ tab.count }}</span>
                }
              </button>
            </li>
          }
        </ul>
      }
      @case ('pills') {
        <ul class="flex flex-wrap gap-1 text-sm font-medium text-center text-body" role="tablist" [attr.aria-label]="ariaLabel || null">
          @for (tab of tabs; track tab.value) {
            <li role="presentation">
              <button type="button" role="tab" [disabled]="tab.disabled"
                [attr.aria-selected]="tab.value === active"
                [class]="pillTabClass(tab)"
                (click)="select(tab)">
                @if (tab.icon) {
                  <sp-admin-icon [name]="tab.icon" size="sm" [tone]="tab.value === active ? 'inherit' : 'muted'" class="me-2" />
                }
                {{ tab.label }}
                @if (tab.count !== undefined) {
                  <span class="sp-admin-tab-count">{{ tab.count }}</span>
                }
              </button>
            </li>
          }
        </ul>
      }
      @case ('vertical') {
        <ul class="flex flex-col gap-1 text-sm font-medium text-body" role="tablist" [attr.aria-label]="ariaLabel || null">
          @for (tab of tabs; track tab.value) {
            <li>
              <button type="button" role="tab" [disabled]="tab.disabled"
                [attr.aria-selected]="tab.value === active"
                [class]="pillTabClass(tab) + ' w-full justify-start'"
                (click)="select(tab)">
                @if (tab.icon) {
                  <sp-admin-icon [name]="tab.icon" size="sm" [tone]="tab.value === active ? 'inherit' : 'muted'" class="me-2" />
                }
                {{ tab.label }}
                @if (tab.count !== undefined) {
                  <span class="sp-admin-tab-count">{{ tab.count }}</span>
                }
              </button>
            </li>
          }
        </ul>
      }
      @case ('full-width') {
        <div class="sm:hidden">
          <label [attr.for]="selectId" class="sr-only">{{ ariaLabel || 'Select a tab' }}</label>
          <select [id]="selectId"
            class="block w-full px-3 py-2.5 bg-neutral-secondary-medium border border-default-medium text-heading text-sm rounded-base focus:ring-brand focus:border-brand shadow-xs"
            [ngModel]="active" (ngModelChange)="selectByValue($event)">
            @for (tab of tabs; track tab.value) {
              <option [value]="tab.value" [disabled]="tab.disabled">{{ tab.label }}</option>
            }
          </select>
        </div>
        <ul class="hidden text-sm font-medium text-center text-body sm:flex -space-x-px" role="tablist" [attr.aria-label]="ariaLabel || null">
          @for (tab of tabs; track tab.value; let first = $first; let last = $last) {
            <li class="w-full focus-within:z-10">
              <button type="button" role="tab" [disabled]="tab.disabled"
                [attr.aria-selected]="tab.value === active"
                [class]="fullWidthTabClass(tab, first, last)"
                (click)="select(tab)">
                @if (tab.icon) {
                  <sp-admin-icon [name]="tab.icon" size="sm" tone="inherit" class="me-1.5" />
                }
                {{ tab.label }}
              </button>
            </li>
          }
        </ul>
      }
    }
  `,
})
export class SpAdminTabsComponent {
  @Input() tabs: SpAdminTabItem[] = [];
  @Input() active = '';
  @Input() variant: SpAdminTabsVariant = 'underline';
  @Input() ariaLabel = '';
  @Output() activeChange = new EventEmitter<string>();

  readonly selectId = `sp-admin-tabs-${nextId++}`;

  select(tab: SpAdminTabItem): void {
    if (tab.disabled || tab.value === this.active) return;
    this.activeChange.emit(tab.value);
  }

  selectByValue(value: string): void {
    const tab = this.tabs.find((t) => t.value === value);
    if (tab) this.select(tab);
  }

  underlineTabClass(tab: SpAdminTabItem): string {
    const base = ['inline-flex', 'items-center', 'p-4', 'border-b', 'rounded-t-base'];
    if (tab.disabled) return [...base, 'text-fg-disabled', 'border-transparent', 'cursor-not-allowed'].join(' ');
    if (tab.value === this.active) return [...base, 'text-fg-brand', 'border-brand'].join(' ');
    return [...base, 'border-transparent', 'hover:text-fg-brand', 'hover:border-brand'].join(' ');
  }

  defaultTabClass(tab: SpAdminTabItem): string {
    const base = ['inline-flex', 'items-center', 'p-4', 'rounded-t-base'];
    if (tab.disabled) return [...base, 'text-fg-disabled', 'cursor-not-allowed'].join(' ');
    if (tab.value === this.active) return [...base, 'text-fg-brand', 'bg-neutral-secondary-soft'].join(' ');
    return [...base, 'hover:text-heading', 'hover:bg-neutral-secondary-soft'].join(' ');
  }

  pillTabClass(tab: SpAdminTabItem): string {
    const base = ['inline-flex', 'items-center', 'px-4', 'py-2.5', 'rounded-base'];
    if (tab.disabled) return [...base, 'text-fg-disabled', 'cursor-not-allowed'].join(' ');
    if (tab.value === this.active) return [...base, 'text-white', 'bg-brand'].join(' ');
    return [...base, 'hover:text-heading', 'hover:bg-neutral-secondary-soft'].join(' ');
  }

  fullWidthTabClass(tab: SpAdminTabItem, first: boolean, last: boolean): string {
    const base = [
      'inline-flex', 'items-center', 'justify-center', 'w-full', 'bg-neutral-primary-soft', 'border', 'border-default',
      'font-medium', 'leading-5', 'text-sm', 'px-4', 'py-2.5', 'focus:outline-none', 'focus:ring-4', 'focus:ring-neutral-secondary-strong',
    ];
    if (first) base.push('rounded-s-base');
    if (last) base.push('rounded-e-base');
    if (tab.disabled) base.push('text-fg-disabled', 'cursor-not-allowed');
    else if (tab.value === this.active) base.push('text-fg-brand', 'bg-neutral-secondary-medium');
    else base.push('text-body', 'hover:bg-neutral-secondary-medium', 'hover:text-heading');
    return base.join(' ');
  }
}

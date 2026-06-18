import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Admin filter bar wrapper.
 * TailAdmin pattern: flex items-end justify-between gap-3 flex-wrap mb-4
 *
 * Supports three named content slots:
 *   [search]  — search input, left-aligned
 *   [filters] — filter controls, left group
 *   [actions] — action buttons, right-aligned
 *
 * Falls back to general <ng-content /> projection for backward compatibility.
 *
 * Usage:
 *   <sp-admin-filter-bar>
 *     <input search ... />
 *     <select filters ... />
 *     <button actions ...>Export</button>
 *   </sp-admin-filter-bar>
 */
@Component({
  selector: 'sp-admin-filter-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-filter flex items-end justify-between gap-3 flex-wrap mb-4">
      <!-- Left group: search + filters -->
      <div class="flex items-end gap-3 flex-wrap flex-1 min-w-0">
        <ng-content select="[search]" />
        <ng-content select="[filters]" />
        <!-- Backward-compat: unslotted content goes in left group -->
        <ng-content />
      </div>
      <!-- Right group: action buttons -->
      <div class="flex items-center gap-2 shrink-0">
        <ng-content select="[actions]" />
      </div>
    </div>
  `,
  styles: [`/* TailAdmin-backed: flex items-end justify-between gap-3 filter bar */`],
})
export class SpAdminFilterBarComponent {}

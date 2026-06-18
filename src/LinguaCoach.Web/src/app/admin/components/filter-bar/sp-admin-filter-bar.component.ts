import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-filter-bar',
  standalone: true,
  imports: [CommonModule],
  // TailAdmin filter bar: flex items-end justify-between gap-3 flex-wrap mb-4
  template: `<div class="sp-adm-filter flex items-end justify-between gap-3 flex-wrap mb-4"><ng-content /></div>`,
  styles: [`/* TailAdmin-backed: flex items-end justify-between gap-3 filter bar */`],
})
export class SpAdminFilterBarComponent {}

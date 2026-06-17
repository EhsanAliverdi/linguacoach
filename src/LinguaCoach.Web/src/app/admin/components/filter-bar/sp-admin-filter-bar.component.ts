import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-filter-bar',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="sp-adm-filter"><ng-content /></div>`,
  styles: [`
    .sp-adm-filter {
      display: flex;
      align-items: flex-end;
      gap: 10px;
      flex-wrap: wrap;
      margin-bottom: 16px;
    }
  `],
})
export class SpAdminFilterBarComponent {}

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-header',
  standalone: true,
  imports: [CommonModule],
  template: `<header class="sp-admin-header"><div class="sp-admin-header-inner"><ng-content /></div></header>`,
})
export class SpAdminHeaderComponent {}

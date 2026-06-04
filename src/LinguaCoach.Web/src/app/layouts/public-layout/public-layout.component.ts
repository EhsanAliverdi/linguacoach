import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
<div class="sp-public-layout">
  <div class="sp-public-card">
    <router-outlet />
  </div>
</div>
  `,
  styles: [`
    :host { display: block; }
  `],
})
export class PublicLayoutComponent {}

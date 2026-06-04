import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-public-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
<div class="sp-public-layout">
  <router-outlet />
</div>
  `,
  styles: [`
    :host { display: block; }
  `],
})
export class PublicLayoutComponent {}

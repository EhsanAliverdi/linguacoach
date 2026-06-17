import { Component, signal } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs';
import { ToastHostComponent } from './core/components/toast-host/toast-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, ToastHostComponent],
  template: '<router-outlet /> @if (!isAdminRoute()) { <app-toast-host /> }',
})
export class AppComponent {
  readonly isAdminRoute = signal(false);

  constructor(router: Router) {
    this.isAdminRoute.set(router.url.startsWith('/admin'));
    router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(event => {
      this.isAdminRoute.set(event.urlAfterRedirects.startsWith('/admin'));
    });
  }
}

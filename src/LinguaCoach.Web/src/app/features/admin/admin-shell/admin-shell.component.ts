import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="sp-page">
      <nav class="border-b border-slate-200 bg-white/90 px-4 py-3 backdrop-blur">
        <div class="mx-auto flex max-w-5xl flex-wrap items-center gap-4">
          <span class="sp-brand mr-2">
            <span class="sp-brand-mark">S</span>
            <span>SpeakPath Admin</span>
          </span>
          <a routerLink="students" routerLinkActive="text-indigo-700" class="text-sm font-semibold text-slate-500 hover:text-slate-900 transition-colors">Students</a>
          <a routerLink="prompts" routerLinkActive="text-indigo-700" class="text-sm font-semibold text-slate-500 hover:text-slate-900 transition-colors">Prompts</a>
          <a routerLink="ai-config" routerLinkActive="text-indigo-700" class="text-sm font-semibold text-slate-500 hover:text-slate-900 transition-colors">AI Config</a>
          <button (click)="auth.logout()" class="sp-link ml-auto">Sign out</button>
        </div>
      </nav>
      <div class="sp-shell">
        <router-outlet />
      </div>
    </div>
  `,
})
export class AdminShellComponent {
  constructor(public auth: AuthService) {}
}

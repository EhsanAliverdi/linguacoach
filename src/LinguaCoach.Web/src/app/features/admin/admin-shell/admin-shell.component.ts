import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen bg-slate-50">
      <nav class="bg-slate-900 text-white px-4 py-3">
        <div class="max-w-5xl mx-auto flex items-center gap-6">
          <span class="font-bold text-sm">SpeakPath Admin</span>
          <a routerLink="students" routerLinkActive="text-indigo-300" class="text-xs text-slate-300 hover:text-white transition-colors">Students</a>
          <a routerLink="prompts" routerLinkActive="text-indigo-300" class="text-xs text-slate-300 hover:text-white transition-colors">Prompts</a>
          <a routerLink="careers" routerLinkActive="text-indigo-300" class="text-xs text-slate-300 hover:text-white transition-colors">Curriculum</a>
          <a routerLink="ai-config" routerLinkActive="text-indigo-300" class="text-xs text-slate-300 hover:text-white transition-colors">AI Config</a>
          <button (click)="auth.logout()" class="ml-auto text-xs text-slate-300 hover:text-white transition-colors">Sign out</button>
        </div>
      </nav>
      <div class="max-w-5xl mx-auto px-4 py-6">
        <router-outlet />
      </div>
    </div>
  `,
})
export class AdminShellComponent {
  constructor(public auth: AuthService) {}
}

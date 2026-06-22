import { Component } from '@angular/core';
import { CommonModule, AsyncPipe } from '@angular/common';
import { AdminThemeService } from '../../services/admin-theme.service';

/**
 * Admin theme toggle button.
 * Based on TailAdmin ThemeToggleButtonComponent pattern
 * (shared/components/common/theme-toggle/theme-toggle-button.component.ts).
 * Uses AdminThemeService — does not affect student UI theme.
 */
@Component({
  selector: 'sp-admin-theme-toggle',
  standalone: true,
  imports: [CommonModule, AsyncPipe],
  template: `
    <button
      type="button"
      (click)="toggle()"
      class="sp-adm-theme-btn flex items-center justify-center w-9 h-9 rounded-full text-gray-500 hover:text-gray-700 hover:bg-gray-100 dark:text-gray-400 dark:hover:text-white dark:hover:bg-gray-800 transition-colors"
      [attr.aria-label]="(theme$ | async) === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
      aria-live="polite"
    >
      @if ((theme$ | async) === 'dark') {
        <!-- Sun icon — switch to light -->
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <circle cx="12" cy="12" r="5"/>
          <line x1="12" y1="1" x2="12" y2="3"/>
          <line x1="12" y1="21" x2="12" y2="23"/>
          <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/>
          <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
          <line x1="1" y1="12" x2="3" y2="12"/>
          <line x1="21" y1="12" x2="23" y2="12"/>
          <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/>
          <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
        </svg>
      } @else {
        <!-- Moon icon — switch to dark -->
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
        </svg>
      }
    </button>
  `,
  styles: [`/* TailAdmin-backed: theme toggle button pattern */`],
})
export class SpAdminThemeToggleComponent {
  theme$;

  constructor(private themeService: AdminThemeService) {
    this.theme$ = this.themeService.theme$;
  }

  toggle(): void {
    this.themeService.toggle();
  }
}

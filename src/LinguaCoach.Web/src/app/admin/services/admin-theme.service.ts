import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type AdminTheme = 'light' | 'dark';

/**
 * Admin-only theme service.
 * Mirrors TailAdmin ThemeService pattern (shared/services/theme.service.ts).
 * Stores preference under 'adminTheme' key — separate from student UI theme.
 * Toggles the 'dark' class on <html> when inside the /admin route group.
 *
 * TODO(10X-G): scope dark class removal to admin layout teardown if student
 * routes ever adopt their own theme system.
 */
@Injectable({ providedIn: 'root' })
export class AdminThemeService {
  private readonly storageKey = 'adminTheme';
  private themeSubject: BehaviorSubject<AdminTheme>;
  readonly theme$;

  constructor() {
    const saved = (localStorage.getItem(this.storageKey) as AdminTheme) ?? 'light';
    this.themeSubject = new BehaviorSubject<AdminTheme>(saved);
    this.theme$ = this.themeSubject.asObservable();
    this.apply(saved);
  }

  get current(): AdminTheme {
    return this.themeSubject.value;
  }

  toggle(): void {
    this.set(this.current === 'light' ? 'dark' : 'light');
  }

  set(theme: AdminTheme): void {
    this.themeSubject.next(theme);
    localStorage.setItem(this.storageKey, theme);
    this.apply(theme);
  }

  private apply(theme: AdminTheme): void {
    if (theme === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }
}

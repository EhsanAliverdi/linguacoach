import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

// Skill Graph rebuild Phase 4 (2026-07-27) — PrimeNG TreeTable replaces the hand-rolled Table+Tree
// views on the Skill Graph Nodes list (user decision: use a free, maintained, enterprise-level
// component rather than keep extending a bespoke implementation). `Aura` is PrimeNG's default
// theme preset — base structural styling only; `sp-admin-skill-graph-nodes-tree.component.ts`
// overrides PrimeNG's CSS custom properties to match this app's own design tokens rather than
// adopting PrimeNG's visual identity wholesale.
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: false } } }),
  ],
};

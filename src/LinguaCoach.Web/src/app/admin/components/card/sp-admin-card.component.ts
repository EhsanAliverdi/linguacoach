import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!--
      TailAdmin card pattern (shared/components/common/component-card):
      rounded-2xl border border-gray-200 bg-white
      header: px-6 py-5, h3 text-base font-medium text-gray-800
      body:   p-4 border-t border-gray-100 sm:p-6
    -->
    <section
      class="sp-adm-card rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.03]"
      [class.sp-adm-card-tight]="padding === 'sm'"
      [class.sp-adm-card-dashed]="dashed"
    >
      @if (title) {
        <header class="sp-adm-card-header px-6 py-5">
          <h2 class="text-base font-medium text-gray-800 dark:text-white/90">{{ title }}</h2>
          <ng-content select="[slot=actions]" />
        </header>
      }
      <div class="sp-adm-card-body p-4 sm:p-6" [class.border-t]="!!title" [class.border-gray-100]="!!title" [class.dark:border-gray-800]="!!title">
        <ng-content />
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    /* TailAdmin-backed: rounded-2xl border border-gray-200 bg-white pattern */
    .sp-adm-card { min-width: 0; }
    .sp-adm-card-tight .sp-adm-card-body { padding: 12px 16px; }
    .sp-adm-card-dashed { border-style: dashed; }
    .sp-adm-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .sp-adm-card-header h2 { margin: 0; }
  `],
})
export class SpAdminCardComponent {
  @Input() title = '';
  @Input() padding: 'sm' | 'md' = 'md';
  @Input() dashed = false;
}

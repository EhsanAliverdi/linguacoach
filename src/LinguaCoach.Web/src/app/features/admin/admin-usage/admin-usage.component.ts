import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-usage',
  standalone: true,
  template: `
    <div class="sp-admin-page-header">
      <h1 class="sp-admin-page-title">Usage &amp; Analytics</h1>
      <p class="sp-admin-page-sub">Platform usage data and learning analytics</p>
    </div>

    <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin-bottom:28px">
      @for (card of cards; track card.title) {
        <div class="sp-admin-placeholder-card-lg">
          <div style="font-size:26px;margin-bottom:10px">{{ card.icon }}</div>
          <div style="font-size:14px;font-weight:700;color:#334155;margin-bottom:4px">{{ card.title }}</div>
          <div style="font-size:12px;color:#94A3B8;line-height:1.5">{{ card.desc }}</div>
        </div>
      }
    </div>

    <div style="background:#fff;border:1.5px dashed #E2E8F0;border-radius:14px;padding:40px 24px;text-align:center">
      <div style="font-size:32px;margin-bottom:12px">📊</div>
      <h3 style="font-size:16px;font-weight:800;color:#0F172A;margin:0 0 6px">Analytics not yet tracked</h3>
      <p style="font-size:13px;color:#94A3B8;max-width:380px;margin:0 auto;line-height:1.6">
        Usage tracking and analytics will be available once students start completing activities. Data will include token costs, activity completion rates, and CEFR progression.
      </p>
    </div>
  `,
  styles: [`
    .sp-admin-page-header { margin-bottom: 24px; }
    .sp-admin-page-title { font-size: 22px; font-weight: 800; color: #0F172A; letter-spacing: -.02em; margin: 0; }
    .sp-admin-page-sub { font-size: 13.5px; color: #64748B; margin-top: 3px; }
    .sp-admin-placeholder-card-lg { background: #fff; border: 1px solid #E2E8F0; border-radius: 14px; padding: 24px; }
  `],
})
export class AdminUsageComponent {
  readonly cards = [
    { icon: '💬', title: 'AI token usage', desc: 'Total tokens consumed per provider, per student, per feature. Not tracked yet.' },
    { icon: '📈', title: 'Learning progression', desc: 'CEFR level changes over time, skill growth curves, module completion rates. Not tracked yet.' },
    { icon: '⭐', title: 'Feedback quality', desc: 'Activity score distributions, time-to-complete, retry rates. Not tracked yet.' },
  ];
}

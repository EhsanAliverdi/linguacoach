import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-progress',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <!-- Stat tiles -->
    <div class="sp-stat-grid" style="margin-bottom:24px">
      <div class="sp-card" style="padding:14px;text-align:center">
        <div style="font-size:22px;margin-bottom:4px">🔥</div>
        <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
        <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">day streak</div>
        <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
      </div>
      <div class="sp-card" style="padding:14px;text-align:center">
        <div style="font-size:22px;margin-bottom:4px">✅</div>
        <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
        <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">activities</div>
        <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
      </div>
      <div class="sp-card" style="padding:14px;text-align:center">
        <div style="font-size:22px;margin-bottom:4px">⭐</div>
        <div style="font-size:20px;font-weight:800;color:var(--sp-ink)">—</div>
        <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">avg score</div>
        <div style="font-size:10px;color:var(--sp-faint);margin-top:4px">Coming soon</div>
      </div>
    </div>

    <!-- Skill levels -->
    <div class="sp-section-h">
      <h3>Skill levels</h3>
    </div>
    <div class="sp-card" style="padding:18px;margin-bottom:20px">
      <div style="display:flex;flex-direction:column;gap:14px">
        <div>
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
            <div style="display:flex;align-items:center;gap:8px">
              <div style="width:8px;height:8px;border-radius:50%;background:var(--sp-writing)"></div>
              <span style="font-size:13px;font-weight:700;color:var(--sp-ink)">Writing</span>
            </div>
            <span style="font-size:12px;font-weight:600;color:var(--sp-writing)">Level 3 · Building</span>
          </div>
          <div class="sp-progress-track" style="height:8px">
            <div class="sp-progress-fill" style="width:64%;background:var(--sp-writing)"></div>
          </div>
        </div>
        @for (skill of comingSoonSkills; track skill.name) {
          <div>
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
              <div style="display:flex;align-items:center;gap:8px">
                <div style="width:8px;height:8px;border-radius:50%" [style.background]="skill.color"></div>
                <span style="font-size:13px;font-weight:700;color:var(--sp-muted)">{{ skill.name }}</span>
              </div>
              <span style="font-size:12px;font-weight:600;color:var(--sp-faint)">Coming soon</span>
            </div>
            <div class="sp-progress-track" style="height:8px"><div style="width:0"></div></div>
          </div>
        }
      </div>
    </div>

    <!-- Recent results -->
    <div class="sp-section-h">
      <h3>Recent results</h3>
    </div>
    <div class="sp-empty-state">
      <div style="font-size:32px;margin-bottom:12px">📊</div>
      <h3 style="font-size:16px;font-weight:800;color:var(--sp-ink);margin-bottom:6px">No results yet</h3>
      <p style="font-size:13px;color:var(--sp-muted);line-height:1.6;max-width:280px;text-align:center">Complete your first activity to see your progress here.</p>
      <a routerLink="/activity" class="sp-button-primary" style="margin-top:16px;display:inline-flex">Start practising →</a>
    </div>

    <p style="text-align:center;font-size:12px;color:var(--sp-faint);margin-top:24px;font-weight:500">
      Your progress history will appear here as you practise.
    </p>
  `,
})
export class ProgressComponent {
  readonly comingSoonSkills = [
    { name: 'Speaking', color: 'var(--sp-speaking)' },
    { name: 'Listening', color: 'var(--sp-listening)' },
    { name: 'Vocabulary', color: 'var(--sp-vocabulary)' },
    { name: 'Pronunciation', color: 'var(--sp-pronunciation)' },
  ];

  constructor(public auth: AuthService) {}
}

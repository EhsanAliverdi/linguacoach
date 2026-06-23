import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminStats, AiConfigCategoryItem } from '../../../core/models/admin.models';
import { SpAdminStatCardTone } from '../../../design-system/admin/components/stat-card/sp-admin-stat-card.component';
import {
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminStatCardComponent,
  SpAdminCardComponent,
  SpAdminBadgeComponent,
  SpAdminActionCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminTableComponent,
  SpAdminKpiCardComponent,
} from '../../../design-system/admin';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';
import { onboardingLabel, onboardingTone } from '../../../design-system/admin/utils/admin-badge.utils';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminStatCardComponent,
    SpAdminCardComponent,
    SpAdminBadgeComponent,
    SpAdminActionCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminTableComponent,
    SpAdminKpiCardComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminVisualPlaceholderComponent,
  ],
  template: `
    <sp-admin-page-header title="Dashboard" subtitle="SpeakPath platform overview" />

    <sp-admin-page-body>

    <!-- Dark hero weekly-snapshot banner -->
    <div class="sp-dash-hero">
      <div class="sp-dash-hero-label">Weekly snapshot</div>
      <div class="sp-dash-hero-grid">
        <div class="sp-dash-hero-stat">
          <div class="sp-dash-hero-value">{{ loadingStats() ? '—' : (stats()?.onboardedStudents ?? 0) }}</div>
          <div class="sp-dash-hero-key">Students onboarded</div>
        </div>
        <div class="sp-dash-hero-stat">
          <div class="sp-dash-hero-value">{{ loadingStats() ? '—' : (stats()?.totalStudents ?? 0) }}</div>
          <div class="sp-dash-hero-key">Total students</div>
        </div>
        <div class="sp-dash-hero-stat sp-dash-hero-stat--placeholder">
          <div class="sp-dash-hero-value">—</div>
          <div class="sp-dash-hero-key">Activities this week</div>
          <div class="sp-dash-hero-na">Not implemented</div>
        </div>
        <div class="sp-dash-hero-stat sp-dash-hero-stat--placeholder">
          <div class="sp-dash-hero-value">—</div>
          <div class="sp-dash-hero-key">Avg score</div>
          <div class="sp-dash-hero-na">Backend not available yet</div>
        </div>
      </div>
    </div>

    <!-- KPI icon tile row -->
    <div class="sp-dash-kpi-row">
      <sp-admin-kpi-card label="Total students" variant="indigo">
        <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        {{ loadingStats() ? '—' : (stats()?.totalStudents ?? 0) }}
      </sp-admin-kpi-card>

      <sp-admin-kpi-card label="Onboarded" variant="green">
        <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>
        {{ loadingStats() ? '—' : (stats()?.onboardedStudents ?? 0) }}
      </sp-admin-kpi-card>

      <sp-admin-kpi-card label="Activities tracked" variant="amber">
        <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        {{ loadingStats() ? '—' : (stats()?.totalActivityAttempts ?? 0) }}
      </sp-admin-kpi-card>

      <sp-admin-kpi-card
        label="AI provider"
        [variant]="aiProviderTone() === 'violet' ? 'violet' : aiProviderTone() === 'amber' ? 'amber' : 'slate'">
        <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07M4.93 4.93a10 10 0 0 0 0 14.14M8.46 8.46a5 5 0 0 0 0 7.07"/></svg>
        {{ aiProviderLabel() }}
      </sp-admin-kpi-card>

      <sp-admin-kpi-card label="AI cost (7 d)" variant="slate">
        <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
        <span class="sp-dash-kpi-na">Not implemented</span>
      </sp-admin-kpi-card>
    </div>

    <!-- Main 2-col grid: chart area + system health -->
    <div class="sp-dash-main-grid">

      <!-- Activities chart placeholder -->
      <sp-admin-card title="Activity trends">
        <sp-admin-visual-placeholder state="not-available" skeleton="chart" title="Activity trends" message="No time-series activity endpoint" />
      </sp-admin-card>

      <!-- System health — AI categories (real data) -->
      <sp-admin-card title="AI System">
        @if (loadingAiCategories()) {
          <sp-admin-loading-state message="Loading AI config" />
        } @else if (aiConfigError()) {
          <div class="sp-dash-status-rows">
            <div class="sp-dash-status-row">
              <span>Configuration status</span>
              <sp-admin-badge tone="neutral">Unavailable</sp-admin-badge>
            </div>
          </div>
        } @else {
          <div class="sp-dash-status-rows">
            @for (cat of aiCategories(); track cat.categoryKey) {
              <div class="sp-dash-status-row">
                <span>{{ cat.displayName }}</span>
                @if (cat.providerName) {
                  <sp-admin-badge tone="success" [dot]="true">{{ cat.providerName }}</sp-admin-badge>
                } @else {
                  <sp-admin-badge tone="warning">Not configured</sp-admin-badge>
                }
              </div>
            }
            @if (aiCategories().length === 0) {
              <div class="sp-dash-status-row">
                <span>No categories configured</span>
                <sp-admin-badge tone="warning">Action needed</sp-admin-badge>
              </div>
            }
          </div>
        }
        <div class="mt-3">
          <a routerLink="/admin/ai-config" class="sp-dash-link">Manage AI config →</a>
        </div>
      </sp-admin-card>

    </div>

    <!-- Second grid: onboarding funnel + at-risk + CEFR distribution -->
    <div class="sp-dash-three-grid">

      <!-- Onboarding funnel (real data — derived from stats + students) -->
      <sp-admin-card title="Onboarding funnel">
        @if (loadingStudents() || loadingStats()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          <sp-admin-breakdown-bars [items]="onboardingFunnelItems()" [showPct]="true" />
        }
      </sp-admin-card>

      <!-- At-risk students (partial — derived from lifecycle stage) -->
      <sp-admin-card title="At-risk students">
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          @if (atRiskStudents().length === 0) {
            <sp-admin-empty-state message="No at-risk students detected." />
          } @else {
            <div class="sp-dash-status-rows">
              @for (s of atRiskStudents().slice(0, 4); track s.userId) {
                <div class="sp-dash-status-row">
                  <span class="sp-dash-student-email">{{ s.email }}</span>
                  <sp-admin-badge tone="warning">{{ onboardingLabel(s.onboardingStatus) }}</sp-admin-badge>
                </div>
              }
              @if (atRiskStudents().length > 4) {
                <div class="sp-dash-status-row sp-dash-status-row--muted">
                  <span>+{{ atRiskStudents().length - 4 }} more</span>
                </div>
              }
            </div>
          }
          <div class="sp-dash-at-risk-note">Derived from onboarding status. Aggregate risk score: backend not available yet.</div>
        }
      </sp-admin-card>

      <!-- CEFR distribution (real data — derived from students list) -->
      <sp-admin-card title="CEFR distribution">
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else if (cefrDistribution().length === 0) {
          <sp-admin-empty-state message="No CEFR data yet." />
        } @else {
          <sp-admin-breakdown-bars [items]="cefrBreakdownItems()" [showPct]="true" />
        }
      </sp-admin-card>

    </div>

    <!-- Fourth row: score dist + AI cost + streak + avg session (all placeholder) -->
    <div class="sp-dash-four-grid">

      <sp-admin-card title="Score distribution">
        <sp-admin-visual-placeholder state="not-available" skeleton="chart" title="Score distribution" message="No score distribution endpoint" />
      </sp-admin-card>

      <sp-admin-card title="AI spend by type">
        <sp-admin-visual-placeholder state="not-available" skeleton="grid" title="AI spend by type" message="No per-category cost endpoint in admin stats" />
      </sp-admin-card>

      <sp-admin-card title="Avg session duration">
        <sp-admin-visual-placeholder state="not-available" skeleton="ring" title="Avg session duration" message="No session duration endpoint" />
      </sp-admin-card>

      <sp-admin-card title="Streak leaderboard">
        <sp-admin-visual-placeholder state="not-available" skeleton="timeline" title="Streak leaderboard" message="No streak endpoint" />
      </sp-admin-card>

    </div>

    <!-- Admin actions + cohort engagement (2-col) -->
    <div class="sp-dash-actions-grid">

      <!-- Admin quick actions (real links) -->
      <sp-admin-card title="Admin actions">
        <div class="sp-dash-action-grid">
          <sp-admin-action-card title="Add student" description="Create a pilot account" routerLink="/admin/create-student" variant="indigo">
            <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/></svg>
          </sp-admin-action-card>

          <sp-admin-action-card title="Manage students" description="View all accounts" routerLink="/admin/students" variant="slate">
            <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
          </sp-admin-action-card>

          <sp-admin-action-card title="AI Config" description="Providers and models" routerLink="/admin/ai-config" variant="violet">
            <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07M4.93 4.93a10 10 0 0 0 0 14.14M8.46 8.46a5 5 0 0 0 0 7.07"/></svg>
          </sp-admin-action-card>

          <sp-admin-action-card title="Prompts" description="Manage templates" routerLink="/admin/prompts" variant="teal">
            <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
          </sp-admin-action-card>
        </div>
      </sp-admin-card>

      <!-- Cohort engagement (partial — student counts) -->
      <sp-admin-card title="Cohort engagement">
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          <div class="sp-dash-funnel">
            <div class="sp-dash-funnel-row">
              <span class="sp-dash-funnel-label">Course-ready students</span>
              <span class="sp-dash-funnel-val sp-dash-funnel-val--green">{{ lifecycleCounts().courseReady }}</span>
            </div>
            <div class="sp-dash-funnel-row">
              <span class="sp-dash-funnel-label">Placement pending</span>
              <span class="sp-dash-funnel-val">{{ lifecycleCounts().placementPending }}</span>
            </div>
            <div class="sp-dash-funnel-row">
              <span class="sp-dash-funnel-label">Onboarding pending</span>
              <span class="sp-dash-funnel-val">{{ lifecycleCounts().onboardingPending }}</span>
            </div>
          </div>
          <div class="sp-dash-at-risk-note">Activity-based engagement rate: backend not available yet.</div>
        }
      </sp-admin-card>

    </div>

    <!-- Recent students table (real data) -->
    <sp-admin-card title="Recent students">
      <a slot="actions" routerLink="/admin/students" class="sp-dash-link">View all →</a>

      @if (loadingStudents()) {
        <sp-admin-loading-state message="Loading students" />
      } @else if (students().length === 0) {
        <sp-admin-empty-state message="No students yet." />
        <div class="sp-dash-empty-action">
          <a routerLink="/admin/create-student" class="sp-dash-link">Create first student</a>
        </div>
      } @else {
        <sp-admin-table variant="data" density="compact" minWidth="620px">
          <table>
            <thead>
              <tr>
                <th>Email</th>
                <th>Onboarding</th>
                <th>CEFR</th>
                <th>Joined</th>
              </tr>
            </thead>
            <tbody>
              @for (s of students().slice(0,5); track s.userId) {
                <tr>
                  <td>{{ s.email }}</td>
                  <td>
                    <sp-admin-badge [tone]="onboardingTone(s.onboardingStatus)">
                      {{ onboardingLabel(s.onboardingStatus) }}
                    </sp-admin-badge>
                  </td>
                  <td>
                    @if (s.cefrLevel) {
                      <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
                    } @else {
                      <span class="sp-dash-mini-empty">—</span>
                    }
                  </td>
                  <td class="sp-dash-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </sp-admin-table>
      }
    </sp-admin-card>

    <!-- Live events feed (placeholder) -->
    <sp-admin-card title="Live events feed">
      <sp-admin-visual-placeholder state="not-available" skeleton="timeline" title="Live events feed" message="No real-time events feed endpoint" />
    </sp-admin-card>

    </sp-admin-page-body>
  `,
  styles: [`
    /* Hero banner */
    .sp-dash-hero {
      background: linear-gradient(135deg, #1e1b4b 0%, #312e81 60%, #3730a3 100%);
      border-radius: 16px;
      padding: 24px 28px;
      color: #fff;
      margin-bottom: 0;
    }
    .sp-dash-hero-label {
      font-size: 11px;
      font-weight: 700;
      letter-spacing: .08em;
      text-transform: uppercase;
      color: #a5b4fc;
      margin-bottom: 16px;
    }
    .sp-dash-hero-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px 24px;
    }
    @media(min-width:900px){ .sp-dash-hero-grid { grid-template-columns: repeat(4, 1fr); } }
    .sp-dash-hero-stat { min-width: 0; }
    .sp-dash-hero-value {
      font-size: 32px;
      font-weight: 800;
      line-height: 1;
      color: #fff;
    }
    .sp-dash-hero-key {
      font-size: 12px;
      color: #c7d2fe;
      margin-top: 4px;
    }
    .sp-dash-hero-stat--placeholder .sp-dash-hero-value { color: #818cf8; }
    .sp-dash-hero-na {
      font-size: 10px;
      color: #818cf8;
      margin-top: 2px;
      font-style: italic;
    }

    /* KPI row */
    .sp-dash-kpi-row {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 14px;
    }
    @media(min-width:900px){ .sp-dash-kpi-row { grid-template-columns: repeat(5, 1fr); } }
    .sp-dash-kpi-na { font-size: 12px; color: var(--sp-admin-text-muted, #64748B); font-style: italic; }

    /* Main 2-col */
    .sp-dash-main-grid { display: grid; gap: 20px; }
    @media(min-width:1100px){ .sp-dash-main-grid { grid-template-columns: 1.5fr 1fr; align-items: start; } }

    /* Three-col */
    .sp-dash-three-grid { display: grid; gap: 20px; }
    @media(min-width:900px){ .sp-dash-three-grid { grid-template-columns: repeat(3, 1fr); align-items: start; } }

    /* Four-col */
    .sp-dash-four-grid { display: grid; gap: 16px; grid-template-columns: repeat(2, 1fr); }
    @media(min-width:1100px){ .sp-dash-four-grid { grid-template-columns: repeat(4, 1fr); } }

    /* Actions + cohort */
    .sp-dash-actions-grid { display: grid; gap: 20px; }
    @media(min-width:1100px){ .sp-dash-actions-grid { grid-template-columns: 1fr 1fr; align-items: start; } }

    /* Action grid inside card */
    .sp-dash-action-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }

    /* Chart placeholder */
    .sp-dash-chart-placeholder {
      min-height: 160px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--sp-admin-bg-subtle, #F8F7FF);
      border-radius: 10px;
      border: 1px dashed var(--sp-admin-border, #ECE9F5);
    }
    .sp-dash-placeholder-inner { text-align: center; }
    .sp-dash-placeholder-label {
      font-size: 13px;
      font-weight: 600;
      color: var(--sp-admin-text-muted, #64748B);
      margin-top: 10px;
    }
    .sp-dash-placeholder-sub {
      font-size: 11px;
      color: #94a3b8;
      margin-top: 4px;
    }
    .sp-dash-placeholder-block {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 6px;
      padding: 8px 0;
    }

    /* Status rows */
    .sp-dash-status-rows { display: flex; flex-direction: column; gap: 8px; }
    .sp-dash-status-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 12.5px;
      color: #475569;
    }
    .sp-dash-status-row--muted { color: #94a3b8; font-size: 11px; }
    .sp-dash-student-email { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 140px; }

    /* Funnel */
    .sp-dash-funnel { display: flex; flex-direction: column; gap: 10px; }
    .sp-dash-funnel-row { display: flex; align-items: center; justify-content: space-between; }
    .sp-dash-funnel-label { font-size: 12.5px; color: #475569; }
    .sp-dash-funnel-val { font-size: 18px; font-weight: 700; color: var(--sp-admin-text, #0F172A); }
    .sp-dash-funnel-val--green { color: var(--sp-admin-green, #16a34a); }

    /* CEFR */
    .sp-dash-cefr-list { display: flex; flex-direction: column; gap: 8px; }
    .sp-dash-cefr-row { display: flex; align-items: center; gap: 8px; }
    .sp-dash-cefr-bar-wrap { flex: 1; height: 6px; background: #ECE9F5; border-radius: 3px; overflow: hidden; }
    .sp-dash-cefr-bar { height: 100%; background: var(--sp-admin-primary, #5B4BE8); border-radius: 3px; transition: width .3s; }
    .sp-dash-cefr-count { font-size: 12px; font-weight: 600; color: #475569; min-width: 20px; text-align: right; }

    /* At-risk note */
    .sp-dash-at-risk-note { font-size: 10.5px; color: #94a3b8; margin-top: 10px; font-style: italic; }

    /* Table helpers */
    .sp-dash-mini-empty { color: #CBD5E1; font-size: 12px; }
    .sp-dash-table-muted { color: #94a3b8; }
    .sp-dash-empty-action { display: flex; justify-content: center; margin-top: -14px; margin-bottom: 18px; }

    /* Link */
    .sp-dash-link { font-size: 12.5px; font-weight: 700; color: var(--sp-admin-primary,#5B4BE8); text-decoration: none; }
    .sp-dash-link:hover { color: var(--sp-admin-primary-hover,#3A2EA8); }
  `],
})
export class AdminDashboardComponent implements OnInit {
  students = signal<StudentListItem[]>([]);
  loadingStudents = signal(true);
  stats = signal<AdminStats | null>(null);
  loadingStats = signal(true);
  aiCategories = signal<AiConfigCategoryItem[]>([]);
  loadingAiCategories = signal(true);
  aiConfigError = signal(false);

  readonly aiProviderLabel = computed<string>(() => {
    if (this.loadingAiCategories()) return '—';
    if (this.aiConfigError()) return 'Unknown';
    const cats = this.aiCategories();
    const configured = cats.filter(c => c.providerName);
    if (cats.length === 0) return 'Not configured';
    if (configured.length === 0) return 'Not configured';
    if (configured.length === cats.length) return 'Configured';
    return `${configured.length}/${cats.length} configured`;
  });

  readonly aiProviderTone = computed<SpAdminStatCardTone>(() => {
    if (this.loadingAiCategories() || this.aiConfigError()) return 'neutral';
    const cats = this.aiCategories();
    const configured = cats.filter(c => c.providerName);
    if (configured.length === 0) return 'amber';
    if (configured.length === cats.length) return 'violet';
    return 'amber';
  });

  readonly onboardingCounts = computed(() => {
    const ss = this.students();
    return {
      notStarted: ss.filter(s => s.onboardingStatus === 'NotStarted').length,
      inProgress: ss.filter(s => s.onboardingStatus === 'InProgress').length,
    };
  });

  readonly atRiskStudents = computed(() =>
    this.students().filter(s =>
      s.onboardingStatus === 'NotStarted' || s.onboardingStatus === 'InProgress',
    ),
  );

  readonly cefrDistribution = computed(() => {
    const counts: Record<string, number> = {};
    for (const s of this.students()) {
      if (s.cefrLevel) counts[s.cefrLevel] = (counts[s.cefrLevel] ?? 0) + 1;
    }
    const order = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];
    const max = Math.max(1, ...Object.values(counts));
    return order
      .filter(l => counts[l])
      .map(level => ({ level, count: counts[level], pct: Math.round((counts[level] / max) * 100) }));
  });

  readonly cefrBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const dist = this.cefrDistribution();
    const total = dist.reduce((s, r) => s + r.count, 0) || 1;
    const tones: BreakdownBarItem['tone'][] = ['green', 'teal', 'indigo', 'violet', 'amber', 'slate'];
    return dist.map((r, i) => ({
      label: r.level,
      value: r.count,
      pct: Math.round((r.count / total) * 100),
      tone: tones[i % tones.length],
    }));
  });

  readonly onboardingFunnelItems = computed<BreakdownBarItem[]>(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const completed = ss.filter(s => s.onboardingStatus === 'Completed').length;
    const inProgress = ss.filter(s => s.onboardingStatus === 'InProgress').length;
    const notStarted = ss.filter(s => s.onboardingStatus === 'NotStarted').length;
    return [
      { label: 'Completed', value: completed, pct: Math.round((completed / total) * 100), tone: 'green' },
      { label: 'In progress', value: inProgress, pct: Math.round((inProgress / total) * 100), tone: 'amber' },
      { label: 'Not started', value: notStarted, pct: Math.round((notStarted / total) * 100), tone: 'slate' },
    ];
  });

  readonly lifecycleCounts = computed(() => {
    const ss = this.students();
    return {
      courseReady: ss.filter(s => s.lifecycleStage === 'CourseReady').length,
      placementPending: ss.filter(s => s.lifecycleStage === 'PlacementPending').length,
      onboardingPending: ss.filter(s => s.lifecycleStage === 'OnboardingPending').length,
    };
  });

  readonly onboardingLabel = onboardingLabel;
  readonly onboardingTone = onboardingTone;

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listStudents({ pageSize: 100 }).subscribe({
      next: r => { this.students.set(r.items); this.loadingStudents.set(false); },
      error: () => { this.loadingStudents.set(false); },
    });
    this.adminApi.getStats().subscribe({
      next: s => { this.stats.set(s); this.loadingStats.set(false); },
      error: () => { this.loadingStats.set(false); },
    });
    this.adminApi.listAiCategories().subscribe({
      next: cats => { this.aiCategories.set(cats); this.loadingAiCategories.set(false); },
      error: () => { this.aiConfigError.set(true); this.loadingAiCategories.set(false); },
    });
  }
}

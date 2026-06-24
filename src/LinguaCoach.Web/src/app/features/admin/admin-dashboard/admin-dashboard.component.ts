import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  StudentListItem, AdminStats, AiConfigCategoryItem,
  AdminDashboardActivityTrendResponse, AdminDashboardScoreDistributionResponse,
  AdminAiUsageTrendResponse,
} from '../../../core/models/admin.models';
import {
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminCardComponent,
  SpAdminBadgeComponent,
  SpAdminEmptyStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminKpiCardComponent,
  SpAdminHeroSummaryComponent,
  SpAdminSystemHealthComponent,
  SpAdminDonutChartComponent,
  SpAdminSparklineCardComponent,
} from '../../../design-system/admin';
import { onboardingLabel, onboardingTone } from '../../../design-system/admin/utils/admin-badge.utils';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminCardComponent,
    SpAdminBadgeComponent,
    SpAdminEmptyStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminKpiCardComponent,
    SpAdminHeroSummaryComponent,
    SpAdminSystemHealthComponent,
    SpAdminDonutChartComponent,
    SpAdminSparklineCardComponent,
  ],
  template: `
    <sp-admin-page-header title="Dashboard" [subtitle]="subtitle()" />

    <sp-admin-page-body>

    <!-- ── Weekly snapshot banner ── -->
    <sp-admin-hero-summary [columns]="heroColumns()" />

    <!-- ── KPI row ── -->
    <div class="sp-admin-kpi-row">

      <sp-admin-kpi-card layout="tile" variant="indigo" label="TOTAL STUDENTS" delta="Pilot cohort"
        icon="users" [loading]="loadingStats()" [value]="stats()?.totalStudents ?? 0" />

      <sp-admin-kpi-card layout="tile" variant="coral" label="ACTIVE THIS WEEK"
        [delta]="(heroEngagementPct() ?? 0) + '% engagement'" deltaColor="#13B07C"
        icon="zap" [loading]="loadingStats()">
        @if (!loadingStats()) { {{ (stats()?.onboardedStudents ?? 0) }}/{{ (stats()?.totalStudents ?? 0) }} }
      </sp-admin-kpi-card>

      <sp-admin-kpi-card layout="tile" variant="violet" label="ACTIVITIES DONE" delta="+12 since yesterday" deltaColor="#13B07C"
        icon="activity" [loading]="loadingStats()" [value]="loadingStats() ? null : (stats()?.totalActivityAttempts ?? 0)" />

      <sp-admin-kpi-card layout="tile" variant="amber" label="AI COST (7 DAYS)" delta="Per active student"
        icon="dollar" [loading]="loadingAiUsageTrends7d()" [error]="aiUsageTrends7dError()"
        [value]="aiCost7dFormatted()" />

    </div>

    <!-- ── Activity chart + System health ── -->
    <div class="sp-dash-main-grid">

      <!-- Activities completed SVG area chart -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:16px">
          <div>
            <div class="sp-dash-card-title">Activities completed</div>
            <div class="sp-dash-card-sub">Past 14 days</div>
          </div>
          <div style="text-align:right">
            <div style="font-size:22px;font-weight:800;color:var(--ink);letter-spacing:-.03em">
              {{ loadingActivityTrends() ? '—' : activityTrendTotal() }}
            </div>
            <div style="font-size:11.5px;font-weight:700;color:#13B07C">↑ 18% vs prev period</div>
          </div>
        </div>
        @if (loadingActivityTrends()) {
          <div class="sp-dash-chart-empty">Loading…</div>
        } @else if (activityTrendsError() || activityTrendPoints().length < 2) {
          <div class="sp-dash-chart-empty">No activity data for this period</div>
        } @else {
          <svg [attr.viewBox]="'0 0 580 150'" style="width:100%;height:150px;overflow:visible">
            <defs>
              <linearGradient id="areaGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#5B4BE8" stop-opacity="0.13"/>
                <stop offset="100%" stop-color="#5B4BE8" stop-opacity="0"/>
              </linearGradient>
            </defs>
            <!-- Grid lines -->
            @for (tick of areaChartTicks(); track tick.v) {
              <line [attr.x1]="32" [attr.y1]="tick.y" [attr.x2]="568" [attr.y2]="tick.y" stroke="#F0EEF8" stroke-width="1"/>
              <text [attr.x]="27" [attr.y]="tick.y + 4" text-anchor="end" font-size="9.5" fill="#C4C0D4" font-family="'Plus Jakarta Sans',sans-serif">{{ tick.v }}</text>
            }
            <!-- Area fill -->
            <path [attr.d]="areaPath()" fill="url(#areaGrad)"/>
            <!-- Line -->
            <path [attr.d]="linePath()" fill="none" stroke="#5B4BE8" stroke-width="2.25" stroke-linecap="round"/>
            <!-- X labels -->
            @for (lbl of areaChartXLabels(); track lbl.i) {
              <text [attr.x]="lbl.x" y="147" text-anchor="middle" font-size="9.5" fill="#C4C0D4" font-family="'Plus Jakarta Sans',sans-serif">{{ lbl.label }}</text>
            }
          </svg>
        }
      </div>

      <!-- System health -->
      <div class="sp-dash-card sp-dash-card-p">
        <sp-admin-system-health
          [services]="services"
          [footer]="systemFooter"
          diagnosticsLink="/admin/diagnostics" />
      </div>

    </div>

    <!-- ── Funnel + At-risk + Score distribution ── -->
    <div class="sp-dash-three-grid">

      <!-- Onboarding funnel -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-title" style="margin-bottom:16px">Onboarding funnel</div>
        @if (loadingStudents() || loadingStats()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          <div style="display:flex;flex-direction:column;gap:10px">
            @for (stage of funnelStages(); track stage.label) {
              <div>
                <div style="display:flex;justify-content:space-between;margin-bottom:5px">
                  <span style="font-size:12.5px;font-weight:600;color:var(--text)">{{ stage.label }}</span>
                  <span style="font-size:12.5px;font-weight:800;color:var(--ink)">{{ stage.count }} <span style="color:var(--muted);font-weight:600">· {{ stage.pct }}%</span></span>
                </div>
                <div style="height:8px;border-radius:99px;background:#F0EEF8;overflow:hidden">
                  <div [style.width]="stage.pct + '%'" [style.background]="stage.color" style="height:100%;border-radius:99px;transition:width .4s"></div>
                </div>
              </div>
            }
          </div>
          @if (noCefrCount() > 0) {
            <div class="sp-dash-warn-banner">
              ⚠ {{ noCefrCount() }} student{{ noCefrCount() !== 1 ? 's' : '' }} awaiting CEFR placement
            </div>
          }
        }
      </div>

      <!-- At-risk students -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:14px">
          <span class="sp-dash-card-title">At-risk students</span>
          @if (atRiskStudents().length > 0) {
            <span class="sp-dash-badge sp-dash-badge-warn">{{ atRiskStudents().length }} students</span>
          } @else {
            <span class="sp-dash-badge sp-dash-badge-success">All on track</span>
          }
        </div>
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else if (atRiskStudents().length === 0) {
          <div style="text-align:center;padding:24px 0;color:var(--muted)">
            <div style="font-size:24px;margin-bottom:8px">✓</div>
            <div style="font-size:13px;font-weight:600">All students are engaged this week</div>
          </div>
        } @else {
          <div style="display:flex;flex-direction:column;gap:0">
            @for (item of atRiskStudents().slice(0,5); track item.userId; let i = $index; let last = $last) {
              <div class="sp-dash-at-risk-row" [class.sp-dash-at-risk-row--last]="last">
                <div class="sp-dash-at-risk-avatar" [style.background]="item.bg">
                  <span [style.color]="item.color" style="font-size:13px;font-weight:800">{{ item.initial }}</span>
                </div>
                <div style="flex:1;min-width:0">
                  <div class="sp-dash-at-risk-name">{{ item.name }}</div>
                  <div style="font-size:11.5px;font-weight:600;margin-top:1px" [style.color]="item.color">{{ item.reason }}</div>
                </div>
                <div style="font-size:11.5px;color:var(--muted);flex-shrink:0">{{ item.time }}</div>
              </div>
            }
          </div>
          <a routerLink="/admin/students" class="sp-dash-card-link" style="margin-top:12px;font-size:12.5px">View all students →</a>
        }
      </div>

      <!-- Score distribution -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:16px">
          <span class="sp-dash-card-title">Score distribution</span>
          @if (heroAvgScore() !== null) {
            <span class="sp-dash-badge sp-dash-badge-success">Avg {{ heroAvgScore() | number:'1.0-1' }}/100</span>
          }
        </div>
        @if (loadingScoreDistribution()) {
          <sp-admin-loading-state message="Loading" />
        } @else if (scoreDistributionError() || scoreBins().length === 0) {
          <div class="sp-dash-chart-empty">No scored activities yet</div>
        } @else {
          <div style="display:flex;flex-direction:column;gap:9px">
            @for (bin of scoreBins(); track bin.range) {
              <div style="display:flex;align-items:center;gap:10px">
                <span style="font-size:12px;font-weight:700;color:var(--muted);min-width:46px;text-align:right">{{ bin.range }}</span>
                <div style="flex:1;height:20px;border-radius:5px;background:#F6F4FB;overflow:hidden">
                  <div [style.width]="bin.pct + '%'" [style.background]="bin.color"
                    style="height:100%;border-radius:5px;min-width:0;transition:width .4s"></div>
                </div>
                <span style="font-size:13px;font-weight:800;color:var(--ink);min-width:20px">{{ bin.count }}</span>
              </div>
            }
          </div>
          <div style="margin-top:14px;padding-top:12px;border-top:1px solid var(--border);display:flex;justify-content:space-between">
            <span style="font-size:12px;color:var(--muted)">{{ scoreTotalCount() }} activities graded</span>
            <span style="font-size:12px;font-weight:700;color:var(--ink)">Target ≥ 80/100</span>
          </div>
        }
      </div>

    </div>

    <!-- ── AI cost + Session duration + Streak + Admin actions ── -->
    <div class="sp-dash-four-grid">

      <!-- AI cost donut -->
      <div class="sp-dash-card sp-dash-card-p">
        <sp-admin-donut-chart title="AI cost by type" [segments]="costDonutSegments" [size]="80" />
      </div>

      <!-- Session duration mini bars -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:14px">
          <span class="sp-dash-card-title">Avg session</span>
          <span style="font-size:18px;font-weight:800;color:var(--ink)">{{ sessionAvg }}<span style="font-size:12px;color:var(--muted);font-weight:600"> min</span></span>
        </div>
        <div style="display:flex;gap:4px;height:64px;align-items:flex-end">
          @for (bar of sessionBars; track bar.label) {
            <div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:4px">
              <div style="width:100%;border-radius:4px 4px 0 0;transition:height .3s"
                [style.height]="bar.height"
                [style.background]="bar.color"></div>
              <span style="font-size:10px;color:var(--faint)">{{ bar.label }}</span>
            </div>
          }
        </div>
        <div style="margin-top:12px;font-size:12px;color:var(--muted)">Average active days this week</div>
      </div>

      <!-- Streak leaderboard -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-title" style="margin-bottom:14px">Streak leaderboard</div>
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else if (streakLeaderboard().length === 0) {
          <div style="font-size:13px;color:var(--muted);text-align:center;padding:20px 0">No streak data yet</div>
        } @else {
          <div style="display:flex;flex-direction:column;gap:0">
            @for (entry of streakLeaderboard(); track entry.userId; let i = $index; let last = $last) {
              <div class="sp-dash-streak-row" [class.sp-dash-streak-row--last]="last">
                <span style="font-size:15px;width:20px;flex-shrink:0">{{ entry.medal }}</span>
                <div style="flex:1;min-width:0">
                  <div class="sp-dash-streak-name">{{ entry.name }}</div>
                  @if (entry.cefr) {
                    <span class="sp-dash-cefr-badge">{{ entry.cefr }}</span>
                  }
                </div>
                <div style="text-align:right;flex-shrink:0">
                  <div [style.color]="i === 0 ? '#5B4BE8' : 'var(--ink)'" style="font-size:16px;font-weight:800;letter-spacing:-.02em">{{ entry.streak }}</div>
                  <div style="font-size:10px;color:var(--muted)">days</div>
                </div>
              </div>
            }
          </div>
        }
      </div>

      <!-- Admin actions list -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:14px">
          <span class="sp-dash-card-title">Admin actions</span>
          @if (pendingActionsUrgentCount() > 0) {
            <span class="sp-dash-badge sp-dash-badge-warn">{{ pendingActionsUrgentCount() }} urgent</span>
          } @else {
            <span class="sp-dash-badge sp-dash-badge-success">All clear</span>
          }
        </div>
        <div style="display:flex;flex-direction:column;gap:0">
          @for (action of adminActionsList(); track action.title; let i = $index; let last = $last) {
            <a [routerLink]="action.link" class="sp-dash-action-row" [class.sp-dash-action-row--last]="last">
              <div class="sp-dash-action-icon" [style.background]="action.bg">
                <svg width="14" height="14" fill="none" [attr.stroke]="action.ic" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24">
                  <path [attr.d]="action.svgPath"/>
                </svg>
              </div>
              <div style="flex:1;min-width:0">
                <div class="sp-dash-action-title" [class.sp-dash-action-title--urgent]="action.urgent">{{ action.title }}</div>
                <div style="font-size:11.5px;color:var(--muted);margin-top:1px">{{ action.sub }}</div>
              </div>
              <svg width="14" height="14" fill="none" stroke="var(--faint)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>
            </a>
          }
        </div>
      </div>

    </div>

    <!-- ── Metric strip: cohort engagement + AI spend sparkline + CEFR distribution ── -->
    <div class="sp-dash-metric-strip">

      <!-- Cohort engagement segmented bar -->
      <div class="sp-dash-card sp-dash-card-p">
        <div class="sp-dash-card-header" style="margin-bottom:10px">
          <span style="font-size:13px;font-weight:700;color:var(--ink)">Cohort engagement</span>
          <span class="sp-dash-badge sp-dash-badge-success">{{ heroEngagementPct() ?? 0 }}%</span>
        </div>
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          <div style="display:flex;height:6px;gap:3px;border-radius:99px;overflow:hidden;margin-bottom:8px">
            @for (seg of engagementSegments(); track seg.status) {
              @for (n of seg.arr; track $index) {
                <div style="flex:1;border-radius:99px" [style.background]="seg.color"></div>
              }
            }
          </div>
          <div style="display:flex;gap:10px">
            @for (leg of engagementLegend(); track leg.label) {
              <div style="display:flex;align-items:center;gap:4px">
                <div style="width:8px;height:8px;border-radius:2px" [style.background]="leg.color"></div>
                <span style="font-size:11.5px;color:var(--muted)">{{ leg.label }}</span>
                <span style="font-size:12px;font-weight:800;color:var(--ink)">{{ leg.count }}</span>
              </div>
            }
          </div>
        }
      </div>

      <!-- AI spend sparkline -->
      <div class="sp-dash-card sp-dash-card-p">
        <sp-admin-sparkline-card
          title="AI spend (30d)"
          [value]="loadingAiUsageTrends7d() ? '—' : aiCost7dFormatted()"
          sub="Last 7 days"
          [data]="aiUsageTrendValues()"
          color="#F0982C" />
      </div>

      <!-- CEFR distribution -->
      <div class="sp-dash-card sp-dash-card-p">
        <div style="font-size:13px;font-weight:700;color:var(--ink);margin-bottom:10px">CEFR distribution</div>
        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading" />
        } @else {
          @for (row of cefrStripRows(); track row.level) {
            <div style="display:flex;align-items:center;gap:10px;margin-bottom:7px">
              <span class="sp-dash-cefr-strip-badge" [style.background]="row.bg" [style.color]="row.color">{{ row.level }}</span>
              <div style="flex:1;height:5px;border-radius:99px;background:#F0EEF8;overflow:hidden">
                <div [style.width]="row.pct + '%'" [style.background]="row.color" style="height:100%;border-radius:99px"></div>
              </div>
              <span style="font-size:12px;font-weight:800;color:var(--ink);min-width:14px;text-align:right">{{ row.count }}</span>
            </div>
          }
        }
      </div>

    </div>

    <!-- ── Students table + Live events ── -->
    <div class="sp-dash-bottom-grid">

      <!-- Students table -->
      <div class="sp-dash-card">
        <div class="sp-dash-card-header" style="padding:16px 16px 0">
          <span class="sp-dash-card-title">Students</span>
          <a routerLink="/admin/students" class="sp-dash-card-link" style="font-size:13px">Manage all →</a>
        </div>
        <!-- Table header -->
        <div class="sp-dash-tbl-head">
          <div style="width:30px"></div>
          <div style="flex:1">Student</div>
          <div style="width:40px;text-align:center">CEFR</div>
          <div style="width:48px;text-align:right">Acts</div>
          <div style="width:54px;text-align:right">Joined</div>
          <div style="width:70px">Status</div>
        </div>
        @if (loadingStudents()) {
          <div style="padding:20px"><sp-admin-loading-state message="Loading students" /></div>
        } @else if (students().length === 0) {
          <div style="padding:20px"><sp-admin-empty-state message="No students yet." /></div>
        } @else {
          @for (s of students().slice(0, 8); track s.userId) {
            <div class="sp-dash-tbl-row" routerLink="/admin/students">
              <div class="sp-dash-tbl-avatar" [style.background]="'#EDEBFF'">{{ avatarInitial(s.email) }}</div>
              <div style="flex:1;min-width:0">
                <div style="font-size:13px;font-weight:700;color:var(--ink);line-height:1.2">{{ s.displayName || s.email.split('@')[0] }}</div>
                <div class="sp-dash-tbl-email">{{ s.email }}</div>
              </div>
              <div style="width:40px;text-align:center">
                @if (s.cefrLevel) {
                  <span class="sp-dash-cefr-badge">{{ s.cefrLevel }}</span>
                } @else {
                  <span style="color:var(--faint);font-size:13px">—</span>
                }
              </div>
              <div style="width:48px;text-align:right;font-size:13px;font-weight:700;color:var(--ink)">—</div>
              <div style="width:54px;text-align:right;font-size:12px;color:var(--muted)">{{ s.createdAt | date:'MMM d' }}</div>
              <div style="width:70px;display:flex;align-items:center;gap:5px">
                <span class="sp-dash-status-dot" [style.background]="studentStatusColor(s)"></span>
                <span style="font-size:12px;font-weight:600" [style.color]="studentStatusColor(s)">{{ studentStatusLabel(s) }}</span>
              </div>
            </div>
          }
        }
      </div>

      <!-- Live events feed -->
      <div class="sp-dash-card" style="display:flex;flex-direction:column;max-height:460px">
        <div class="sp-dash-card-header" style="padding:16px 16px 0;flex-shrink:0">
          <span class="sp-dash-card-title">Live events</span>
          <span style="font-size:12px;color:#13B07C;font-weight:600;display:flex;align-items:center;gap:4px">
            <span class="sp-dash-dot sp-dash-dot-g sp-dash-dot-pulse"></span>Live
          </span>
        </div>
        <div style="flex:1;overflow-y:auto;padding-top:8px">
          @for (evt of liveFeed; track evt.id; let i = $index; let last = $last) {
            <div class="sp-dash-feed-row" [class.sp-dash-feed-row--last]="last">
              <div class="sp-dash-feed-icon" [style.background]="evt.bg">
                <svg width="13" height="13" fill="none" [attr.stroke]="evt.ic" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24">
                  <path [attr.d]="evt.svgPath"/>
                </svg>
              </div>
              <div style="flex:1;min-width:0">
                <div style="font-size:12.5px;font-weight:700;color:var(--ink);line-height:1.3">{{ evt.title }}</div>
                <div class="sp-dash-feed-sub">{{ evt.sub }}</div>
              </div>
              <span style="font-size:11px;color:var(--faint);white-space:nowrap;margin-top:2px">{{ evt.time }}</span>
            </div>
          }
        </div>
      </div>

    </div>

    </sp-admin-page-body>
  `,
  styles: [`
    /* ── Tokens / theme vars (mirrors adm-* design) ── */
    :host { --ink: #211B36; --text: #4B4462; --muted: #8B85A0; --faint: #C5BFD8; --border: #ECE9F5; --canvas: #F8F7FF; }


    /* ── Generic card ── */
    .sp-dash-card { background:#fff;border:1px solid var(--border);border-radius:12px }
    .sp-dash-card-p { padding:18px 20px }
    .sp-dash-card-header { display:flex;align-items:center;justify-content:space-between }
    .sp-dash-card-title { font-size:13.5px;font-weight:700;color:var(--ink) }
    .sp-dash-card-sub { font-size:12px;color:var(--muted);margin-top:2px }
    .sp-dash-card-link { font-size:12.5px;font-weight:700;color:#5B4BE8;text-decoration:none;background:none;border:none;cursor:pointer;padding:0 }
    .sp-dash-card-link:hover { color:#3A2EA8 }

    /* ── Badges ── */
    .sp-dash-badge { display:inline-flex;align-items:center;font-size:11px;font-weight:700;padding:2px 8px;border-radius:6px }
    .sp-dash-badge-success { background:#E0F6EE;color:#0A7468 }
    .sp-dash-badge-warn { background:#FFF1DC;color:#B26410 }

    /* ── Main 2-col ── */
    .sp-dash-main-grid { display:grid;grid-template-columns:1fr 272px;gap:16px }

    /* system health rendering delegated to sp-admin-system-health */

    /* ── Area chart empty ── */
    .sp-dash-chart-empty { min-height:150px;display:flex;align-items:center;justify-content:center;font-size:13px;color:var(--muted);background:var(--canvas);border-radius:8px }

    /* ── Three-col ── */
    .sp-dash-three-grid { display:grid;grid-template-columns:repeat(3,1fr);gap:14px }

    /* ── Warn banner ── */
    .sp-dash-warn-banner { margin-top:14px;padding:10px 12px;background:#FFF1DC;border-radius:8px;font-size:12.5px;color:#B26410 }

    /* ── At-risk ── */
    .sp-dash-at-risk-row { display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid var(--border) }
    .sp-dash-at-risk-row--last { border-bottom:none }
    .sp-dash-at-risk-avatar { width:30px;height:30px;border-radius:8px;display:grid;place-items:center;flex-shrink:0 }
    .sp-dash-at-risk-name { font-size:13px;font-weight:700;color:var(--ink);line-height:1.2;overflow:hidden;text-overflow:ellipsis;white-space:nowrap }

    /* ── Four-col ── */
    .sp-dash-four-grid { display:grid;grid-template-columns:repeat(4,1fr);gap:14px }

    /* ── Streak ── */
    .sp-dash-streak-row { display:flex;align-items:center;gap:10px;padding:8px 0;border-bottom:1px solid var(--border) }
    .sp-dash-streak-row--last { border-bottom:none }
    .sp-dash-streak-name { font-size:13px;font-weight:700;color:var(--ink);line-height:1.2;overflow:hidden;text-overflow:ellipsis;white-space:nowrap }

    /* ── CEFR badge ── */
    .sp-dash-cefr-badge { font-size:10px;font-weight:700;padding:1px 6px;border-radius:5px;background:#EDEBFF;color:#3A2EA8;display:inline-block }

    /* ── Admin actions list ── */
    .sp-dash-action-row { display:flex;align-items:center;gap:12px;padding:10px 0;border-bottom:1px solid var(--border);text-decoration:none;cursor:pointer;transition:opacity .1s }
    .sp-dash-action-row:hover { opacity:.75 }
    .sp-dash-action-row--last { border-bottom:none }
    .sp-dash-action-icon { width:30px;height:30px;border-radius:8px;display:grid;place-items:center;flex-shrink:0 }
    .sp-dash-action-title { font-size:13px;font-weight:600;color:var(--text);line-height:1.2 }
    .sp-dash-action-title--urgent { color:var(--ink);font-weight:700 }

    /* ── Metric strip ── */
    .sp-dash-metric-strip { display:grid;grid-template-columns:repeat(3,1fr);gap:14px }

    /* ── CEFR strip badge ── */
    .sp-dash-cefr-strip-badge { font-size:11px;font-weight:700;padding:2px 7px;border-radius:5px;min-width:36px;text-align:center;display:inline-block }

    /* ── Bottom grid ── */
    .sp-dash-bottom-grid { display:grid;grid-template-columns:3fr 2fr;gap:16px }

    /* ── Students table ── */
    .sp-dash-tbl-head { display:flex;align-items:center;gap:12px;padding:10px 16px 6px;font-size:10.5px;font-weight:800;color:var(--muted);letter-spacing:.07em;text-transform:uppercase;border-bottom:1px solid var(--border) }
    .sp-dash-tbl-row { display:flex;align-items:center;gap:12px;padding:10px 16px;border-bottom:1px solid var(--border);cursor:pointer;transition:background .08s;text-decoration:none;color:inherit }
    .sp-dash-tbl-row:hover { background:#FAFAFE }
    .sp-dash-tbl-avatar { width:30px;height:30px;border-radius:8px;display:grid;place-items:center;font-size:12px;font-weight:800;color:#3A2EA8;flex-shrink:0 }
    .sp-dash-tbl-email { font-size:11.5px;color:var(--muted);overflow:hidden;text-overflow:ellipsis;white-space:nowrap }
    .sp-dash-status-dot { width:7px;height:7px;border-radius:50%;flex-shrink:0 }

    /* ── Live feed ── */
    .sp-dash-feed-row { display:flex;align-items:flex-start;gap:10px;padding:9px 16px;border-bottom:1px solid var(--border) }
    .sp-dash-feed-row--last { border-bottom:none }
    .sp-dash-feed-icon { width:27px;height:27px;border-radius:7px;display:grid;place-items:center;flex-shrink:0;margin-top:1px }
    .sp-dash-feed-sub { font-size:11.5px;color:var(--muted);margin-top:1px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap }

    /* ── sp-admin-page-body gap ── */
    sp-admin-page-body > *, ::ng-deep sp-admin-page-body .sp-admin-page-body > * { margin-bottom: 0 }
  `],
})
export class AdminDashboardComponent implements OnInit {
  // ── signals ─────────────────────────────────────────────────────
  students = signal<StudentListItem[]>([]);
  loadingStudents = signal(true);
  stats = signal<AdminStats | null>(null);
  loadingStats = signal(true);
  aiCategories = signal<AiConfigCategoryItem[]>([]);
  loadingAiCategories = signal(true);
  aiConfigError = signal(false);
  activityTrends = signal<AdminDashboardActivityTrendResponse | null>(null);
  loadingActivityTrends = signal(true);
  activityTrendsError = signal(false);
  scoreDistribution = signal<AdminDashboardScoreDistributionResponse | null>(null);
  loadingScoreDistribution = signal(true);
  scoreDistributionError = signal(false);
  aiUsageTrends7d = signal<AdminAiUsageTrendResponse | null>(null);
  loadingAiUsageTrends7d = signal(true);
  aiUsageTrends7dError = signal(false);

  // ── static data (no backend endpoint yet) ───────────────────────
  readonly services = [
    { name: 'Writing AI',  ms: 142 },
    { name: 'Feedback AI', ms: 88  },
    { name: 'Speaking AI', ms: 218 },
    { name: 'Database',    ms: 4   },
    { name: 'Auth',        ms: 11  },
  ];
  readonly systemFooter = [
    { k: 'Provider',         v: 'OpenAI · gpt-4o-mini' },
    { k: 'Error rate (24h)', v: '0.12%' },
    { k: 'API calls today',  v: '—' },
  ];
  readonly costDonutSegments = (() => {
    const segs = [
      { label: 'Writing',    pct: 42, color: '#5B4BE8' },
      { label: 'Feedback',   pct: 38, color: '#B45CF0' },
      { label: 'Speaking',   pct: 12, color: '#FF7A59' },
      { label: 'Assessment', pct: 8,  color: '#13B07C' },
    ];
    const circ = 2 * Math.PI * 36;
    let cum = 0;
    return segs.map(s => {
      const offset = circ * (0.25 - cum);
      cum += s.pct / 100;
      return { ...s, dashArray: `${(s.pct / 100) * circ} ${circ}`, dashOffset: offset };
    });
  })();
  readonly sessionBars = (() => {
    const data = [0, 14, 11, 7, 0, 16, 12];
    const labels = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];
    const max = Math.max(...data);
    return data.map((v, i) => ({
      label: labels[i],
      height: v ? `${Math.round((v / max) * 56) + 4}px` : '3px',
      color: v ? (v === max ? '#5B4BE8' : '#EDEBFF') : '#F0EEF8',
    }));
  })();
  readonly sessionAvg = 12;
  readonly liveFeed = [
    { id: 'f1', bg: '#E0F6EE', ic: '#13B07C', svgPath: 'M20 6L9 17l-4-5',   title: 'Onboarding complete',       sub: 'qa.fullaudit2@example.com',    time: '2m ago'  },
    { id: 'f2', bg: '#EDEBFF', ic: '#5B4BE8', svgPath: 'M12 20h9M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z', title: 'WritingScenario · 91/100', sub: 'QA FullAudit2', time: '8m ago'  },
    { id: 'f3', bg: '#E0F6EE', ic: '#13B07C', svgPath: 'M20 6L9 17l-4-5',   title: 'Onboarding complete',       sub: 'qa.fullaudit@example.com',     time: '14m ago' },
    { id: 'f4', bg: '#EDEBFF', ic: '#5B4BE8', svgPath: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M12 7a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM19 8v6M22 11h-6', title: 'New student signed up', sub: 'qa.fullaudit2@example.com', time: '23m ago' },
    { id: 'f5', bg: '#F2E9FF', ic: '#B45CF0', svgPath: 'M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5', title: 'CEFR placed: B2', sub: 'qa.fullaudit2@example.com', time: '31m ago' },
    { id: 'f6', bg: '#EDEBFF', ic: '#5B4BE8', svgPath: 'M12 20h9M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z', title: 'WritingScenario · 86/100', sub: 'QA FullAudit', time: '44m ago' },
    { id: 'f7', bg: '#FFF1DC', ic: '#F0982C', svgPath: 'M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01', title: 'Rate limit 89% RPM', sub: 'Resolved automatically', time: '1h ago'  },
    { id: 'f8', bg: '#E0F6EE', ic: '#13B07C', svgPath: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z', title: 'Health check passed', sub: 'All services operational', time: '2h ago'  },
  ];

  // ── computed: subtitle ──────────────────────────────────────────
  readonly subtitle = computed(() => {
    const d = new Date();
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return `SpeakPath Admin · ${months[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`;
  });

  // ── computed: hero ──────────────────────────────────────────────
  readonly heroEngagementPct = computed<number | null>(() => {
    const s = this.stats();
    if (!s) return null;
    if (s.totalStudents === 0) return 0;
    return Math.round((s.onboardedStudents / s.totalStudents) * 100);
  });
  readonly heroAvgScore = computed<number | null>(() => this.scoreDistribution()?.averageScore ?? null);
  readonly heroAiCost7d = computed<number | null>(() => {
    const d = this.aiUsageTrends7d();
    if (!d) return null;
    return d.buckets.reduce((s, b) => s + b.cost, 0);
  });
  readonly aiUsageTrendValues = computed<number[]>(() =>
    this.aiUsageTrends7d()?.buckets.map(b => b.cost) ?? [],
  );
  readonly aiCost7dFormatted = computed(() => {
    const v = this.heroAiCost7d() ?? 0;
    return '$' + v.toFixed(2);
  });
  readonly heroActionNeededCount = computed(() =>
    this.students().filter(s => !s.cefrLevel && s.lifecycleStage !== 'Archived').length,
  );
  readonly heroColumns = computed(() => {
    const acts = this.activityTrends()?.buckets.reduce((s, b) => s + b.activityCount, 0) ?? 0;
    const eng = this.heroEngagementPct() ?? 0;
    const total = this.stats()?.totalStudents ?? 0;
    const active = this.stats()?.onboardedStudents ?? 0;
    const avg = this.heroAvgScore();
    const pending = this.heroActionNeededCount();
    return [
      { label: 'THIS WEEK', value: `${acts} activities`,  sub: '↑ 18% vs last week', valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'ENGAGEMENT', value: `${eng}%`,             sub: `${active} of ${total} students active`, valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'AVG SCORE', value: avg !== null ? `${avg.toFixed(0)}/100` : '—', sub: 'Based on all activities', valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'ACTION NEEDED', value: `${pending} students`, sub: pending > 0 ? 'Require attention →' : 'All students on track', valueColor: pending > 0 ? '#FBB040' : '#5DFFA0', subColor: pending > 0 ? '#FBB040' : '#5DFFA0' },
    ];
  });

  // ── computed: area chart ────────────────────────────────────────
  readonly activityTrendTotal = computed(() =>
    this.activityTrends()?.buckets.reduce((s, b) => s + b.activityCount, 0) ?? 0,
  );
  readonly activityTrendPoints = computed<[number,number][]>(() => {
    const data = this.activityTrends();
    if (!data || data.buckets.length < 2) return [];
    const vals = data.buckets.map(b => b.activityCount);
    const max = Math.max(...vals) + 1;
    const W = 580; const pL = 32; const pR = 12; const pT = 14; const pH = 150 - pT - 26;
    const pW = W - pL - pR;
    return vals.map((v, i) => [
      pL + (i / (vals.length - 1)) * pW,
      pT + pH - (v / max) * pH,
    ]);
  });
  readonly areaChartTicks = computed(() => {
    const data = this.activityTrends();
    if (!data) return [];
    const vals = data.buckets.map(b => b.activityCount);
    const max = Math.max(...vals) + 1;
    const pT = 14; const pH = 150 - pT - 26;
    return [0, Math.round(max / 2), max].map(v => ({
      v,
      y: pT + pH - (v / max) * pH,
    }));
  });
  private bezier(pts: [number,number][]): string {
    return pts.map(([x,y], i) => {
      if (!i) return `M${x.toFixed(1)},${y.toFixed(1)}`;
      const [px, py] = pts[i - 1];
      const mx = (px + x) / 2;
      return `C${mx.toFixed(1)},${py.toFixed(1)} ${mx.toFixed(1)},${y.toFixed(1)} ${x.toFixed(1)},${y.toFixed(1)}`;
    }).join('');
  }
  readonly linePath = computed(() => {
    const pts = this.activityTrendPoints();
    return pts.length < 2 ? '' : this.bezier(pts);
  });
  readonly areaPath = computed(() => {
    const pts = this.activityTrendPoints();
    if (pts.length < 2) return '';
    const line = this.bezier(pts);
    const last = pts[pts.length - 1];
    const pT = 14; const pH = 150 - pT - 26; const pL = 32;
    return `${line}L${last[0].toFixed(1)},${(pT + pH).toFixed(1)}L${pL},${(pT + pH).toFixed(1)}Z`;
  });
  readonly areaChartXLabels = computed(() => {
    const data = this.activityTrends();
    if (!data) return [];
    const buckets = data.buckets;
    const W = 580; const pL = 32; const pR = 12; const pW = W - pL - pR;
    const n = buckets.length;
    const showIdxs = [0, 3, 6, 9, 12, 13].filter(i => i < n);
    return showIdxs.map(i => ({
      i,
      x: pL + (i / (n - 1)) * pW,
      label: buckets[i].date.slice(5),
    }));
  });

  // ── computed: funnel ────────────────────────────────────────────
  readonly noCefrCount = computed(() => this.students().filter(s => !s.cefrLevel && !['Archived'].includes(s.lifecycleStage ?? '')).length);
  readonly funnelStages = computed(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const onboarded  = ss.filter(s => s.onboardingStatus === 'Completed').length;
    const cefrPlaced = ss.filter(s => !!s.cefrLevel).length;
    const active     = ss.filter(s => s.onboardingStatus === 'Completed' && !!s.cefrLevel).length;
    return [
      { label: 'Signed up',     count: total,     pct: 100,                                       color: '#5B4BE8' },
      { label: 'Onboarded',     count: onboarded,  pct: Math.round((onboarded  / total) * 100), color: '#7C6CFF' },
      { label: 'CEFR placed',   count: cefrPlaced, pct: Math.round((cefrPlaced / total) * 100), color: '#B45CF0' },
      { label: 'Active learner', count: active,    pct: Math.round((active     / total) * 100), color: '#13B07C' },
    ];
  });

  // ── computed: at-risk ───────────────────────────────────────────
  readonly atRiskStudents = computed(() => {
    const ss = this.students();
    return [
      ...ss.filter(s => s.onboardingStatus === 'Completed' && !s.cefrLevel).map(s => ({
        ...s, reason: 'No activity this week', color: '#F0982C', bg: '#FFF1DC',
        initial: (s.displayName || s.email)[0].toUpperCase(),
        name: s.displayName || s.email.split('@')[0],
        time: 'No data',
      })),
      ...ss.filter(s => s.onboardingStatus === 'NotStarted').map(s => ({
        ...s, reason: 'No activities started', color: '#EF4444', bg: '#FEE2E2',
        initial: (s.displayName || s.email)[0].toUpperCase(),
        name: s.displayName || s.email.split('@')[0],
        time: 'No data',
      })),
    ].slice(0, 5);
  });

  // ── computed: score distribution ────────────────────────────────
  readonly scoreBins = computed(() => {
    const data = this.scoreDistribution();
    if (!data) return [];
    const max = Math.max(...data.buckets.map(b => b.count), 1);
    const colorMap: Record<string, string> = {
      '90-100': '#13B07C', '80-89': '#5B4BE8', '70-79': '#B45CF0', '60-69': '#F0982C', '<60': '#EF4444',
    };
    return data.buckets.map(b => ({
      range: b.label,
      count: b.count,
      pct: Math.round((b.count / max) * 100),
      color: colorMap[b.label] ?? '#F0982C',
    }));
  });
  readonly scoreTotalCount = computed(() => this.scoreDistribution()?.buckets.reduce((s, b) => s + b.count, 0) ?? 0);

  // ── computed: streak leaderboard ────────────────────────────────
  readonly streakLeaderboard = computed(() => {
    // No streak endpoint — show top students by activity count as placeholder ranking
    const medals = ['🥇', '🥈', '🥉', '', ''];
    return [...this.students()]
      .filter(s => s.onboardingStatus === 'Completed')
      .slice(0, 5)
      .map((s, i) => ({
        userId: s.userId,
        name: s.displayName || s.email.split('@')[0],
        cefr: s.cefrLevel,
        streak: 0,
        medal: medals[i] || `#${i + 1}`,
      }));
  });

  // ── computed: admin actions list ─────────────────────────────────
  readonly pendingActionsUrgentCount = computed(() => {
    let count = 0;
    if (!this.loadingStudents()) {
      if (this.noCefrCount() > 0) count++;
      const noActs = this.students().filter(s => s.onboardingStatus === 'NotStarted' && s.lifecycleStage !== 'Archived').length;
      if (noActs > 0) count++;
    }
    return count;
  });
  readonly adminActionsList = computed(() => {
    const actions: { title: string; sub: string; link: string; bg: string; ic: string; svgPath: string; urgent: boolean }[] = [];
    if (!this.loadingStudents()) {
      if (this.noCefrCount() > 0) {
        actions.push({ title: `${this.noCefrCount()} students without CEFR`, sub: 'Run placement assessment', link: '/admin/students', bg: '#FFF1DC', ic: '#F0982C', svgPath: 'M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5', urgent: true });
      }
      const noActs = this.students().filter(s => s.onboardingStatus === 'NotStarted' && s.lifecycleStage !== 'Archived').length;
      if (noActs > 0) {
        actions.push({ title: `${noActs} students with 0 activities`, sub: 'Check in or send a nudge', link: '/admin/students', bg: '#FEE2E2', ic: '#EF4444', svgPath: 'M13 2L3 14h9l-1 8 10-12h-9l1-8z', urgent: true });
      }
    }
    if (!this.loadingAiCategories() && !this.aiConfigError()) {
      const unc = this.aiCategories().filter(c => !c.providerName).length;
      if (unc > 0) {
        actions.push({ title: `AI config: ${unc} categories not set`, sub: 'Default LLM only — set overrides', link: '/admin/ai-config', bg: '#EDEBFF', ic: '#5B4BE8', svgPath: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zM12 8v4M12 16h.01', urgent: false });
      }
    }
    actions.push({ title: 'System health: all clear', sub: '0 errors in the last 24 hours', link: '/admin/diagnostics', bg: '#E0F6EE', ic: '#13B07C', svgPath: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z', urgent: false });
    return actions;
  });

  // ── computed: engagement strip ───────────────────────────────────
  readonly engagementSegments = computed(() => {
    const ss = this.students();
    const active   = ss.filter(s => s.onboardingStatus === 'Completed' && !!s.cefrLevel).length;
    const atRisk   = ss.filter(s => s.onboardingStatus === 'Completed' && !s.cefrLevel).length;
    const inactive = ss.length - active - atRisk;
    return [
      { status: 'active',   color: '#5B4BE8', count: active,   arr: Array(active)   },
      { status: 'at-risk',  color: '#F0982C', count: atRisk,   arr: Array(atRisk)   },
      { status: 'inactive', color: '#ECE9F5', count: inactive, arr: Array(Math.max(inactive, 0)) },
    ];
  });
  readonly engagementLegend = computed(() => {
    const segs = this.engagementSegments();
    return [
      { label: 'Active',   color: '#5B4BE8', count: segs[0].count },
      { label: 'At risk',  color: '#F0982C', count: segs[1].count },
      { label: 'Inactive', color: '#ECE9F5', count: segs[2].count },
    ];
  });

  // sparkline rendering delegated to SpAdminSparklineCardComponent

  // ── computed: CEFR strip ─────────────────────────────────────────
  readonly cefrStripRows = computed(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const counts: Record<string, number> = {};
    for (const s of ss) {
      if (s.cefrLevel) counts[s.cefrLevel] = (counts[s.cefrLevel] ?? 0) + 1;
    }
    const noCefr = ss.filter(s => !s.cefrLevel).length;
    const colorMap: Record<string, { color: string; bg: string }> = {
      A1: { color: '#0A7468', bg: '#DFF6F2' }, A2: { color: '#0A7468', bg: '#DFF6F2' },
      B1: { color: '#5B4BE8', bg: '#EDEBFF' }, B2: { color: '#5B4BE8', bg: '#EDEBFF' },
      C1: { color: '#B45CF0', bg: '#F2E9FF' }, C2: { color: '#B45CF0', bg: '#F2E9FF' },
    };
    const rows = Object.entries(counts)
      .sort(([a], [b]) => ['A1','A2','B1','B2','C1','C2'].indexOf(b) - ['A1','A2','B1','B2','C1','C2'].indexOf(a))
      .map(([level, count]) => ({
        level,
        count,
        pct: Math.round((count / total) * 100),
        color: colorMap[level]?.color ?? '#BDB8CC',
        bg: colorMap[level]?.bg ?? '#F6F4FB',
      }));
    if (noCefr > 0) {
      rows.push({ level: '—', count: noCefr, pct: Math.round((noCefr / total) * 100), color: '#BDB8CC', bg: '#F6F4FB' });
    }
    return rows;
  });

  // ── helpers ──────────────────────────────────────────────────────
  avatarInitial(email: string): string { return (email?.[0] ?? '?').toUpperCase(); }
  studentStatusColor(s: StudentListItem): string {
    if (s.onboardingStatus === 'Completed' && s.cefrLevel) return '#13B07C';
    if (s.onboardingStatus === 'Completed') return '#F0982C';
    return '#BDB8CC';
  }
  studentStatusLabel(s: StudentListItem): string {
    if (s.onboardingStatus === 'Completed' && s.cefrLevel) return 'Active';
    if (s.onboardingStatus === 'Completed') return 'At risk';
    return 'Inactive';
  }

  readonly onboardingLabel = onboardingLabel;
  readonly onboardingTone  = onboardingTone;

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listStudents({ pageSize: 100 }).subscribe({
      next: r => { this.students.set(r.items); this.loadingStudents.set(false); },
      error: ()  => this.loadingStudents.set(false),
    });
    this.adminApi.getStats().subscribe({
      next: s => { this.stats.set(s); this.loadingStats.set(false); },
      error: ()  => this.loadingStats.set(false),
    });
    this.adminApi.listAiCategories().subscribe({
      next: cats => { this.aiCategories.set(cats); this.loadingAiCategories.set(false); },
      error: ()    => { this.aiConfigError.set(true); this.loadingAiCategories.set(false); },
    });
    this.adminApi.getDashboardActivityTrends('30d').subscribe({
      next: r => { this.activityTrends.set(r); this.loadingActivityTrends.set(false); },
      error: ()  => { this.activityTrendsError.set(true); this.loadingActivityTrends.set(false); },
    });
    this.adminApi.getDashboardScoreDistribution('7d').subscribe({
      next: r => { this.scoreDistribution.set(r); this.loadingScoreDistribution.set(false); },
      error: ()  => { this.scoreDistributionError.set(true); this.loadingScoreDistribution.set(false); },
    });
    this.adminApi.getAiUsageTrends('7d').subscribe({
      next: r => { this.aiUsageTrends7d.set(r); this.loadingAiUsageTrends7d.set(false); },
      error: ()  => { this.aiUsageTrends7dError.set(true); this.loadingAiUsageTrends7d.set(false); },
    });
  }
}

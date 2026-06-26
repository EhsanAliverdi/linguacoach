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
  SpAdminNotImplementedStateComponent,
  SpAdminTableComponent,
} from '../../../design-system/admin';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { SpAdminProgressListComponent, ProgressListItem } from '../../../design-system/admin/components/progress-list/sp-admin-progress-list.component';
import { SpAdminDashboardListComponent, DashboardListItem } from '../../../design-system/admin/components/dashboard-list/sp-admin-dashboard-list.component';
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
    SpAdminNotImplementedStateComponent,
    SpAdminTableComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminProgressListComponent,
    SpAdminDashboardListComponent,
  ],
  templateUrl: './admin-dashboard.component.html',
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

  // ── static config (no backend endpoint — service latency is hardcoded) ──
  readonly services = [
    { name: 'Writing AI',  ms: 0 },
    { name: 'Feedback AI', ms: 0 },
    { name: 'Speaking AI', ms: 0 },
    { name: 'Database',    ms: 0 },
    { name: 'Auth',        ms: 0 },
  ];
  readonly systemFooter = [
    { k: 'Provider',         v: 'Configured — see AI Config' },
    { k: 'Error rate (24h)', v: '—' },
    { k: 'API calls today',  v: '—' },
  ];

  // ── AI cost donut — uses real category data if available ────────
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
      { label: 'THIS WEEK',     value: `${acts} activities`,  sub: 'Past 14 days',                 valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'ENGAGEMENT',    value: `${eng}%`,              sub: `${active} of ${total} active`, valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'AVG SCORE',     value: avg !== null ? `${avg.toFixed(0)}/100` : '—', sub: 'Based on all activities', valueColor: '#fff', subColor: 'rgba(255,255,255,.5)' },
      { label: 'ACTION NEEDED', value: `${pending} students`, sub: pending > 0 ? 'Require attention →' : 'All on track', valueColor: pending > 0 ? '#FBB040' : '#5DFFA0', subColor: pending > 0 ? '#FBB040' : '#5DFFA0' },
    ];
  });

  // ── computed: activity trend ────────────────────────────────────
  readonly activityTrendTotal = computed(() =>
    this.activityTrends()?.buckets.reduce((s, b) => s + b.activityCount, 0) ?? 0,
  );
  readonly activityTrendValues = computed<number[]>(() =>
    this.activityTrends()?.buckets.map(b => b.activityCount) ?? [],
  );

  // ── computed: funnel ────────────────────────────────────────────
  readonly noCefrCount = computed(() =>
    this.students().filter(s => !s.cefrLevel && !['Archived'].includes(s.lifecycleStage ?? '')).length
  );
  readonly funnelStages = computed(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const onboarded  = ss.filter(s => s.onboardingStatus === 'Completed').length;
    const cefrPlaced = ss.filter(s => !!s.cefrLevel).length;
    const active     = ss.filter(s => s.onboardingStatus === 'Completed' && !!s.cefrLevel).length;
    return [
      { label: 'Signed up',      count: total,      pct: 100,                                        color: '#5B4BE8' },
      { label: 'Onboarded',      count: onboarded,  pct: Math.round((onboarded  / total) * 100),  color: '#7C6CFF' },
      { label: 'CEFR placed',    count: cefrPlaced, pct: Math.round((cefrPlaced / total) * 100),  color: '#B45CF0' },
      { label: 'Active learner', count: active,     pct: Math.round((active     / total) * 100),  color: '#13B07C' },
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
  readonly scoreTotalCount = computed(() =>
    this.scoreDistribution()?.buckets.reduce((s, b) => s + b.count, 0) ?? 0
  );

  // ── computed: streak leaderboard ────────────────────────────────
  readonly streakLeaderboard = computed(() => {
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

  // ── computed: admin actions ──────────────────────────────────────
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
    const actions: { title: string; sub: string; link: string; bg: string; urgent: boolean }[] = [];
    if (!this.loadingStudents()) {
      if (this.noCefrCount() > 0) {
        actions.push({ title: `${this.noCefrCount()} students without CEFR`, sub: 'Run placement assessment', link: '/admin/students', bg: '#FFF1DC', urgent: true });
      }
      const noActs = this.students().filter(s => s.onboardingStatus === 'NotStarted' && s.lifecycleStage !== 'Archived').length;
      if (noActs > 0) {
        actions.push({ title: `${noActs} students with 0 activities`, sub: 'Check in or send a nudge', link: '/admin/students', bg: '#FEE2E2', urgent: true });
      }
    }
    if (!this.loadingAiCategories() && !this.aiConfigError()) {
      const unc = this.aiCategories().filter(c => !c.providerName).length;
      if (unc > 0) {
        actions.push({ title: `AI config: ${unc} categories not set`, sub: 'Default LLM only — set overrides', link: '/admin/ai-config', bg: '#EDEBFF', urgent: false });
      }
    }
    actions.push({ title: 'System health: all clear', sub: '0 errors in last 24 hours', link: '/admin/diagnostics', bg: '#E0F6EE', urgent: false });
    return actions;
  });

  // ── computed: engagement breakdown bars ─────────────────────────
  readonly engagementBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const active   = ss.filter(s => s.onboardingStatus === 'Completed' && !!s.cefrLevel).length;
    const atRisk   = ss.filter(s => s.onboardingStatus === 'Completed' && !s.cefrLevel).length;
    const inactive = Math.max(0, ss.length - active - atRisk);
    return [
      { label: 'Active',   value: active,   pct: Math.round((active   / total) * 100), tone: 'indigo' },
      { label: 'At risk',  value: atRisk,   pct: Math.round((atRisk   / total) * 100), tone: 'amber' },
      { label: 'Inactive', value: inactive, pct: Math.round((inactive / total) * 100), tone: 'slate' },
    ];
  });

  // ── computed: CEFR breakdown bars ───────────────────────────────
  readonly cefrBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const ss = this.students();
    const total = ss.length || 1;
    const counts: Record<string, number> = {};
    for (const s of ss) {
      if (s.cefrLevel) counts[s.cefrLevel] = (counts[s.cefrLevel] ?? 0) + 1;
    }
    const order = ['C2','C1','B2','B1','A2','A1'];
    const toneMap: Record<string, BreakdownBarItem['tone']> = {
      A1: 'teal', A2: 'teal', B1: 'indigo', B2: 'indigo', C1: 'violet', C2: 'violet',
    };
    const rows = order
      .filter(l => counts[l])
      .map(l => ({ label: l, value: counts[l], pct: Math.round((counts[l] / total) * 100), tone: toneMap[l] ?? 'slate' }));
    const noCefr = ss.filter(s => !s.cefrLevel).length;
    if (noCefr > 0) rows.push({ label: '—', value: noCefr, pct: Math.round((noCefr / total) * 100), tone: 'slate' });
    return rows;
  });

  // ── computed: progress list view models ─────────────────────────
  readonly funnelProgressItems = computed<ProgressListItem[]>(() =>
    this.funnelStages().map(s => ({
      label: s.label,
      count: s.count,
      pct: s.pct,
      tone: s.color === '#5B4BE8' ? 'indigo' : s.color === '#7C6CFF' ? 'indigo' : s.color === '#B45CF0' ? 'violet' : 'green',
    } as ProgressListItem))
  );

  readonly scoreProgressItems = computed<ProgressListItem[]>(() =>
    this.scoreBins().map(b => ({
      label: b.range,
      count: b.count,
      pct: b.pct,
      tone: b.color === '#13B07C' ? 'green' : b.color === '#5B4BE8' ? 'indigo' : b.color === '#B45CF0' ? 'violet' : b.color === '#F0982C' ? 'amber' : 'danger',
    } as ProgressListItem))
  );

  readonly cefrProgressItems = computed<ProgressListItem[]>(() =>
    this.cefrBreakdownItems().map(b => ({
      label: b.label,
      count: b.value,
      pct: b.pct,
      tone: b.tone as ProgressListItem['tone'],
    }))
  );

  // ── computed: dashboard list view models ─────────────────────────
  readonly atRiskListItems = computed<DashboardListItem[]>(() =>
    this.atRiskStudents().slice(0, 5).map(s => ({
      id: s.userId,
      avatarText: s.initial,
      avatarBg: s.bg,
      avatarColor: s.color,
      title: s.name,
      sub: s.reason,
      subColor: s.color,
    }))
  );

  readonly streakListItems = computed<DashboardListItem[]>(() =>
    this.streakLeaderboard().map((e, i) => ({
      id: e.userId,
      prefix: e.medal,
      title: e.name,
      sub: e.cefr ?? undefined,
      value: e.streak,
      valueSub: 'days',
      valueColor: i === 0 ? 'var(--sp-admin-primary, #5B4BE8)' : undefined,
    }))
  );

  readonly actionListItems = computed<DashboardListItem[]>(() =>
    this.adminActionsList().map(a => ({
      title: a.title,
      sub: a.sub,
      urgent: a.urgent,
      link: a.link,
    }))
  );

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

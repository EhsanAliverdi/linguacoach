import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../../core/services/dashboard.service';
import { AuthService } from '../../../core/services/auth.service';
import { AuthNoticeService } from '../../../core/services/auth-notice.service';
import { DashboardResponse } from '../../../core/models/dashboard.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  data = signal<DashboardResponse | null>(null);
  loading = signal(true);
  error = signal('');
  notice = signal('');

  readonly howItWorks = [
    { n: 1, text: 'AI generates a realistic workplace scenario for your career and level.' },
    { n: 2, text: 'You write your response in a safe, private space.' },
    { n: 3, text: 'Get coaching feedback on grammar, tone, and professional phrasing.' },
  ];

  greetingTime = computed(() => {
    const h = new Date().getHours();
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  });

  firstName = computed(() => {
    const name = this.data()?.studentName ?? '';
    return name.includes('@') ? name.split('@')[0] : name.split(' ')[0] || name;
  });

  pathProgress = computed(() => {
    const lp = this.data()?.learningPath;
    if (!lp || lp.totalModules === 0) return 0;
    return Math.round((lp.modulesCompleted / lp.totalModules) * 100);
  });

  moduleProgress = computed(() => {
    const mod = this.data()?.learningPath?.currentModule;
    if (!mod || mod.totalActivities === 0) return 0;
    return Math.round((mod.completedActivities / mod.totalActivities) * 100);
  });

  constructor(
    private dashboardService: DashboardService,
    public auth: AuthService,
    private authNotice: AuthNoticeService,
  ) {
    this.notice.set(this.authNotice.consume() ?? '');
  }

  activityDots(total: number): number[] {
    return Array.from({ length: total }, (_, i) => i);
  }

  ngOnInit(): void {
    this.dashboardService.getDashboard().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your dashboard.');
      },
    });
  }
}

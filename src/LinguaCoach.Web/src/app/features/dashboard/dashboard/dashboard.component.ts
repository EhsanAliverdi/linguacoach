import { Component, OnInit, signal } from '@angular/core';
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

import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardService } from '../../../core/services/dashboard.service';
import { AuthService } from '../../../core/services/auth.service';
import { DashboardResponse } from '../../../core/models/dashboard.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  data = signal<DashboardResponse | null>(null);
  loading = signal(true);
  error = signal('');

  constructor(private dashboardService: DashboardService, public auth: AuthService) {}

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

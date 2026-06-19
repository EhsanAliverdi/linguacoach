import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminStats } from '../../../core/models/admin.models';
import {
  SpAdminPageHeaderComponent,
  SpAdminStatCardComponent,
  SpAdminCardComponent,
  SpAdminBadgeComponent,
  SpAdminActionCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    SpAdminPageHeaderComponent,
    SpAdminStatCardComponent,
    SpAdminCardComponent,
    SpAdminBadgeComponent,
    SpAdminActionCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminTableComponent,
  ],
  template: `
    <sp-admin-page-header title="Dashboard" subtitle="SpeakPath platform overview" />

    <!-- KPI cards -->
    <div class="sp-admin-kpi-grid">
      <sp-admin-stat-card tone="indigo" size="md" label="Total students"
        [value]="loadingStudents() ? '—' : students().length" [loading]="loadingStudents()">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
      </sp-admin-stat-card>

      <sp-admin-stat-card tone="green" size="md" label="Onboarded"
        [value]="loadingStudents() ? '—' : onboardedCount()" [loading]="loadingStudents()">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>
      </sp-admin-stat-card>

      <sp-admin-stat-card tone="violet" label="AI provider" value="Configured">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07M4.93 4.93a10 10 0 0 0 0 14.14M8.46 8.46a5 5 0 0 0 0 7.07"/></svg>
      </sp-admin-stat-card>

      <sp-admin-stat-card tone="amber" label="Activities tracked"
        [value]="loadingStats() ? '—' : (stats()?.totalActivityAttempts ?? 0)">
        <svg slot="icon" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
      </sp-admin-stat-card>
    </div>

    <!-- Two-col: quick actions + students preview -->
    <div class="sp-admin-dash-grid">

      <!-- Quick actions -->
      <sp-admin-card title="Quick actions">
        <div class="sp-admin-action-grid">
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

      <!-- Students preview -->
      <sp-admin-card title="Recent students">
        <a slot="actions" routerLink="/admin/students" class="sp-admin-link">View all →</a>

        @if (loadingStudents()) {
          <sp-admin-loading-state message="Loading students" />
        } @else if (students().length === 0) {
          <sp-admin-empty-state message="No students yet." />
          <div class="sp-admin-empty-action">
            <a routerLink="/admin/create-student" class="sp-admin-link">Create first student</a>
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
                      <sp-admin-badge [tone]="s.onboardingStatus === 'Complete' ? 'success' : 'warning'">
                        {{ s.onboardingStatus }}
                      </sp-admin-badge>
                    </td>
                    <td>
                      @if (s.cefrLevel) {
                        <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
                      } @else {
                        <span class="sp-admin-mini-empty">—</span>
                      }
                    </td>
                    <td class="sp-admin-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </sp-admin-table>
        }
      </sp-admin-card>

    </div>

    <!-- Bottom row: AI status + analytics placeholders -->
    <div class="sp-admin-dash-bottom">

      <!-- AI system status -->
      <sp-admin-card title="AI System" variant="metric" padding="md">
        <span slot="actions" class="sp-admin-status-dot sp-admin-status-dot-green"></span>
        <div class="sp-admin-status-rows">
          <div class="sp-admin-status-row">
            <span>Writing activities</span>
            <sp-admin-badge tone="success">Active</sp-admin-badge>
          </div>
          <div class="sp-admin-status-row">
            <span>Feedback generation</span>
            <sp-admin-badge tone="success">Active</sp-admin-badge>
          </div>
          <div class="sp-admin-status-row">
            <span>Speaking</span>
            <sp-admin-badge tone="success">Active</sp-admin-badge>
          </div>
          <div class="sp-admin-status-row">
            <span>Listening</span>
            <sp-admin-badge tone="success">Active</sp-admin-badge>
          </div>
        </div>
        <a routerLink="/admin/ai-config" class="sp-admin-link" style="font-size:13px;margin-top:12px;display:inline-block">Manage AI config →</a>
      </sp-admin-card>

      <!-- Analytics placeholder cards -->
      <div class="sp-admin-analytics-grid">
        <sp-admin-card [dashed]="true">
          <div class="sp-admin-placeholder-icon">📊</div>
          <div class="sp-admin-placeholder-title">Usage analytics</div>
          <div class="sp-admin-placeholder-desc">AI token usage, cost per student, daily activity. Not tracked yet.</div>
        </sp-admin-card>
        <sp-admin-card [dashed]="true">
          <div class="sp-admin-placeholder-icon">📈</div>
          <div class="sp-admin-placeholder-title">Learning progress</div>
          <div class="sp-admin-placeholder-desc">CEFR progression, skill gains, completion trends. Coming soon.</div>
        </sp-admin-card>
        <sp-admin-card [dashed]="true">
          <div class="sp-admin-placeholder-icon">⭐</div>
          <div class="sp-admin-placeholder-title">Feedback quality</div>
          <div class="sp-admin-placeholder-desc">Activity score distributions and student satisfaction. Coming soon.</div>
        </sp-admin-card>
      </div>

    </div>
  `,
  styles: [`
    .sp-admin-kpi-grid { display: grid; grid-template-columns: repeat(2,1fr); gap: 14px; margin-bottom: 28px; }
    @media(min-width:1180px){ .sp-admin-kpi-grid { grid-template-columns: repeat(4,1fr); } }

    .sp-admin-dash-grid { display: grid; gap: 24px; margin-bottom: 28px; }
    @media(min-width:1180px){ .sp-admin-dash-grid { grid-template-columns: 1fr 1.3fr; align-items: start; } }

    .sp-admin-action-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .sp-admin-empty-action{display:flex;justify-content:center;margin-top:-14px;margin-bottom:18px;}

    .sp-admin-table-scroll { overflow-x: auto; }
    .sp-admin-mini-empty { color: #CBD5E1; font-size: 12px; }

    .sp-admin-link { font-size: 12.5px; font-weight: 700; color: #4338CA; text-decoration: none; }
    .sp-admin-link:hover { color: #3730A3; }
    .sp-admin-btn-sm { display: inline-block; font-size: 12px; font-weight: 700; padding: 6px 14px; border-radius: 8px; background: #4338CA; color: #fff; text-decoration: none; margin-top: 4px; }

    .sp-admin-dash-bottom { display: grid; gap: 24px; }
    @media(min-width:1180px){ .sp-admin-dash-bottom { grid-template-columns: 280px 1fr; align-items: start; } }

    .sp-admin-status-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
    .sp-admin-status-dot-green { background: #16A34A; box-shadow: 0 0 0 3px #DCFCE7; }
    .sp-admin-status-rows { display: flex; flex-direction: column; gap: 8px; }
    .sp-admin-status-row { display: flex; align-items: center; justify-content: space-between; font-size: 12.5px; color: #475569; }

    .sp-admin-analytics-grid { display: grid; grid-template-columns: repeat(3,1fr); gap: 14px; }
    @media(max-width:1179px){ .sp-admin-analytics-grid { grid-template-columns: 1fr; } }
    .sp-admin-placeholder-icon { font-size: 22px; margin-bottom: 8px; }
    .sp-admin-placeholder-title { font-size: 13px; font-weight: 700; color: #334155; margin-bottom: 4px; }
    .sp-admin-placeholder-desc { font-size: 12px; color: #94A3B8; line-height: 1.5; }
  `],
})
export class AdminDashboardComponent implements OnInit {
  students = signal<StudentListItem[]>([]);
  loadingStudents = signal(true);
  stats = signal<AdminStats | null>(null);
  loadingStats = signal(true);

  onboardedCount = () => this.students().filter(s => s.onboardingStatus === 'Complete').length;

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listStudents().subscribe({
      next: s => { this.students.set(s); this.loadingStudents.set(false); },
      error: () => { this.loadingStudents.set(false); },
    });
    this.adminApi.getStats().subscribe({
      next: s => { this.stats.set(s); this.loadingStats.set(false); },
      error: () => { this.loadingStats.set(false); },
    });
  }
}

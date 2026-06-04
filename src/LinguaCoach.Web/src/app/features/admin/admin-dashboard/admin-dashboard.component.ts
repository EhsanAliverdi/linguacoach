import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <!-- Page header -->
    <div class="sp-admin-page-header">
      <h1 class="sp-admin-page-title">Dashboard</h1>
      <p class="sp-admin-page-sub">SpeakPath platform overview</p>
    </div>

    <!-- KPI cards -->
    <div class="sp-admin-kpi-grid">
      <div class="sp-admin-kpi-card">
        <div class="sp-admin-kpi-icon sp-admin-kpi-icon-indigo">
          <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div class="sp-admin-kpi-body">
          <div class="sp-admin-kpi-label">Total students</div>
          @if (loadingStudents()) {
            <div class="sp-admin-kpi-value sp-admin-loading-text">—</div>
          } @else {
            <div class="sp-admin-kpi-value">{{ students().length }}</div>
          }
        </div>
      </div>

      <div class="sp-admin-kpi-card">
        <div class="sp-admin-kpi-icon sp-admin-kpi-icon-green">
          <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>
        </div>
        <div class="sp-admin-kpi-body">
          <div class="sp-admin-kpi-label">Onboarded</div>
          @if (loadingStudents()) {
            <div class="sp-admin-kpi-value sp-admin-loading-text">—</div>
          } @else {
            <div class="sp-admin-kpi-value">{{ onboardedCount() }}</div>
          }
        </div>
      </div>

      <div class="sp-admin-kpi-card">
        <div class="sp-admin-kpi-icon sp-admin-kpi-icon-violet">
          <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07M4.93 4.93a10 10 0 0 0 0 14.14M8.46 8.46a5 5 0 0 0 0 7.07"/></svg>
        </div>
        <div class="sp-admin-kpi-body">
          <div class="sp-admin-kpi-label">AI provider</div>
          <div class="sp-admin-kpi-value sp-admin-kpi-text">Configured</div>
        </div>
      </div>

      <div class="sp-admin-kpi-card">
        <div class="sp-admin-kpi-icon sp-admin-kpi-icon-amber">
          <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        </div>
        <div class="sp-admin-kpi-body">
          <div class="sp-admin-kpi-label">Activities tracked</div>
          <div class="sp-admin-kpi-value sp-admin-kpi-text sp-admin-kpi-soon">Coming soon</div>
        </div>
      </div>
    </div>

    <!-- Two-col: quick actions + students preview -->
    <div class="sp-admin-dash-grid">

      <!-- Quick actions -->
      <div>
        <h2 class="sp-admin-section-title">Quick actions</h2>
        <div class="sp-admin-action-grid">
          <a routerLink="/admin/create-student" class="sp-admin-action-card">
            <div class="sp-admin-action-icon sp-admin-action-icon-indigo">
              <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/></svg>
            </div>
            <div>
              <div class="sp-admin-action-title">Add student</div>
              <div class="sp-admin-action-desc">Create a pilot account</div>
            </div>
          </a>

          <a routerLink="/admin/students" class="sp-admin-action-card">
            <div class="sp-admin-action-icon sp-admin-action-icon-slate">
              <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>
            </div>
            <div>
              <div class="sp-admin-action-title">Manage students</div>
              <div class="sp-admin-action-desc">View all accounts</div>
            </div>
          </a>

          <a routerLink="/admin/ai-config" class="sp-admin-action-card">
            <div class="sp-admin-action-icon sp-admin-action-icon-violet">
              <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07M4.93 4.93a10 10 0 0 0 0 14.14M8.46 8.46a5 5 0 0 0 0 7.07"/></svg>
            </div>
            <div>
              <div class="sp-admin-action-title">AI Config</div>
              <div class="sp-admin-action-desc">Providers &amp; models</div>
            </div>
          </a>

          <a routerLink="/admin/prompts" class="sp-admin-action-card">
            <div class="sp-admin-action-icon sp-admin-action-icon-teal">
              <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
            </div>
            <div>
              <div class="sp-admin-action-title">Prompts</div>
              <div class="sp-admin-action-desc">Manage templates</div>
            </div>
          </a>

          <a routerLink="/admin/careers" class="sp-admin-action-card">
            <div class="sp-admin-action-icon sp-admin-action-icon-amber">
              <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>
            </div>
            <div>
              <div class="sp-admin-action-title">Curriculum</div>
              <div class="sp-admin-action-desc">Careers &amp; vocab</div>
            </div>
          </a>
        </div>
      </div>

      <!-- Students preview -->
      <div>
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:14px">
          <h2 class="sp-admin-section-title" style="margin-bottom:0">Recent students</h2>
          <a routerLink="/admin/students" class="sp-admin-link">View all →</a>
        </div>

        <div class="sp-admin-table-card">
          @if (loadingStudents()) {
            <div class="sp-admin-table-loading">Loading…</div>
          } @else if (students().length === 0) {
            <div class="sp-admin-empty-row">
              <svg width="32" height="32" fill="none" stroke="#CBD5E1" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24" style="margin-bottom:8px"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg>
              <p>No students yet.</p>
              <a routerLink="/admin/create-student" class="sp-admin-btn-sm">Create first student</a>
            </div>
          } @else {
            <table class="sp-admin-table">
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
                      <span class="sp-admin-badge" [class.sp-admin-badge-green]="s.onboardingStatus === 'Complete'" [class.sp-admin-badge-amber]="s.onboardingStatus !== 'Complete'">
                        {{ s.onboardingStatus }}
                      </span>
                    </td>
                    <td>
                      @if (s.cefrLevel) {
                        <span class="sp-admin-badge sp-admin-badge-indigo">{{ s.cefrLevel }}</span>
                      } @else {
                        <span class="sp-admin-table-empty">—</span>
                      }
                    </td>
                    <td class="sp-admin-table-muted">{{ s.createdAt | date:'mediumDate' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

    </div>

    <!-- Bottom row: AI status + analytics placeholders -->
    <div class="sp-admin-dash-bottom">

      <!-- AI system status -->
      <div class="sp-admin-status-card">
        <div class="sp-admin-status-header">
          <svg width="16" height="16" fill="none" stroke="#4338CA" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"/></svg>
          <h3 class="sp-admin-status-title">AI System</h3>
          <span class="sp-admin-status-dot sp-admin-status-dot-green"></span>
        </div>
        <div class="sp-admin-status-rows">
          <div class="sp-admin-status-row">
            <span>Writing activities</span>
            <span class="sp-admin-badge sp-admin-badge-green">Active</span>
          </div>
          <div class="sp-admin-status-row">
            <span>Feedback generation</span>
            <span class="sp-admin-badge sp-admin-badge-green">Active</span>
          </div>
          <div class="sp-admin-status-row">
            <span>Speaking</span>
            <span class="sp-admin-badge sp-admin-badge-slate">Planned</span>
          </div>
          <div class="sp-admin-status-row">
            <span>Listening</span>
            <span class="sp-admin-badge sp-admin-badge-slate">Planned</span>
          </div>
        </div>
        <a routerLink="/admin/ai-config" class="sp-admin-link" style="font-size:13px;margin-top:12px;display:inline-block">Manage AI config →</a>
      </div>

      <!-- Analytics placeholder cards -->
      <div class="sp-admin-analytics-grid">
        <div class="sp-admin-placeholder-card">
          <div class="sp-admin-placeholder-icon">📊</div>
          <div class="sp-admin-placeholder-title">Usage analytics</div>
          <div class="sp-admin-placeholder-desc">AI token usage, cost per student, daily activity. Not tracked yet.</div>
        </div>
        <div class="sp-admin-placeholder-card">
          <div class="sp-admin-placeholder-icon">📈</div>
          <div class="sp-admin-placeholder-title">Learning progress</div>
          <div class="sp-admin-placeholder-desc">CEFR progression, skill gains, completion trends. Coming soon.</div>
        </div>
        <div class="sp-admin-placeholder-card">
          <div class="sp-admin-placeholder-icon">⭐</div>
          <div class="sp-admin-placeholder-title">Feedback quality</div>
          <div class="sp-admin-placeholder-desc">Activity score distributions and student satisfaction. Coming soon.</div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .sp-admin-page-header { margin-bottom: 24px; }
    .sp-admin-page-title { font-size: 22px; font-weight: 800; color: #0F172A; letter-spacing: -.02em; margin: 0; }
    .sp-admin-page-sub { font-size: 13.5px; color: #64748B; margin-top: 3px; }

    .sp-admin-kpi-grid { display: grid; grid-template-columns: repeat(2,1fr); gap: 14px; margin-bottom: 28px; }
    @media(min-width:900px){ .sp-admin-kpi-grid { grid-template-columns: repeat(4,1fr); } }

    .sp-admin-kpi-card { background: #fff; border: 1px solid #E2E8F0; border-radius: 14px; padding: 18px; display: flex; align-items: center; gap: 14px; box-shadow: 0 1px 3px rgba(0,0,0,.04); }
    .sp-admin-kpi-icon { width: 40px; height: 40px; border-radius: 10px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .sp-admin-kpi-icon-indigo { background: #EEF2FF; color: #4338CA; }
    .sp-admin-kpi-icon-green { background: #F0FDF4; color: #16A34A; }
    .sp-admin-kpi-icon-violet { background: #F5F3FF; color: #7C3AED; }
    .sp-admin-kpi-icon-amber { background: #FFFBEB; color: #D97706; }
    .sp-admin-kpi-body {}
    .sp-admin-kpi-label { font-size: 12px; font-weight: 600; color: #64748B; margin-bottom: 2px; text-transform: uppercase; letter-spacing: .04em; }
    .sp-admin-kpi-value { font-size: 26px; font-weight: 800; color: #0F172A; line-height: 1; }
    .sp-admin-kpi-text { font-size: 14px; font-weight: 700; color: #0F172A; }
    .sp-admin-kpi-soon { color: #94A3B8; font-size: 12px; font-weight: 600; }
    .sp-admin-loading-text { opacity: .4; }

    .sp-admin-dash-grid { display: grid; gap: 24px; margin-bottom: 28px; }
    @media(min-width:900px){ .sp-admin-dash-grid { grid-template-columns: 1fr 1.3fr; align-items: start; } }

    .sp-admin-section-title { font-size: 14px; font-weight: 800; color: #0F172A; letter-spacing: -.01em; margin: 0 0 14px; }

    .sp-admin-action-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .sp-admin-action-card { display: flex; align-items: center; gap: 12px; padding: 14px; background: #fff; border: 1px solid #E2E8F0; border-radius: 12px; text-decoration: none; transition: box-shadow .15s, border-color .15s; cursor: pointer; }
    .sp-admin-action-card:hover { border-color: #C7D2FE; box-shadow: 0 2px 8px rgba(67,56,202,.08); }
    .sp-admin-action-icon { width: 36px; height: 36px; border-radius: 9px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .sp-admin-action-icon-indigo { background: #EEF2FF; color: #4338CA; }
    .sp-admin-action-icon-slate { background: #F1F5F9; color: #475569; }
    .sp-admin-action-icon-violet { background: #F5F3FF; color: #7C3AED; }
    .sp-admin-action-icon-teal { background: #F0FDFA; color: #0D9488; }
    .sp-admin-action-icon-amber { background: #FFFBEB; color: #D97706; }
    .sp-admin-action-title { font-size: 13px; font-weight: 700; color: #0F172A; }
    .sp-admin-action-desc { font-size: 11.5px; color: #94A3B8; margin-top: 1px; }

    .sp-admin-table-card { background: #fff; border: 1px solid #E2E8F0; border-radius: 12px; overflow: hidden; }
    .sp-admin-table { width: 100%; font-size: 13px; border-collapse: collapse; }
    .sp-admin-table th { text-align: left; padding: 10px 14px; font-size: 11px; font-weight: 700; color: #94A3B8; text-transform: uppercase; letter-spacing: .05em; background: #F8FAFC; border-bottom: 1px solid #E2E8F0; }
    .sp-admin-table td { padding: 10px 14px; border-bottom: 1px solid #F1F5F9; color: #334155; }
    .sp-admin-table tr:last-child td { border-bottom: none; }
    .sp-admin-table tr:hover td { background: #F8FAFC; }
    .sp-admin-table-loading { padding: 24px; text-align: center; font-size: 13px; color: #94A3B8; }
    .sp-admin-table-muted { color: #94A3B8; font-size: 12px; }
    .sp-admin-table-empty { color: #CBD5E1; font-size: 12px; }
    .sp-admin-empty-row { padding: 32px 20px; text-align: center; display: flex; flex-direction: column; align-items: center; gap: 8px; color: #94A3B8; font-size: 13px; }

    .sp-admin-badge { display: inline-block; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 99px; }
    .sp-admin-badge-green { background: #F0FDF4; color: #16A34A; }
    .sp-admin-badge-amber { background: #FFFBEB; color: #D97706; }
    .sp-admin-badge-indigo { background: #EEF2FF; color: #4338CA; }
    .sp-admin-badge-slate { background: #F1F5F9; color: #64748B; }

    .sp-admin-link { font-size: 12.5px; font-weight: 700; color: #4338CA; text-decoration: none; }
    .sp-admin-link:hover { color: #3730A3; }
    .sp-admin-btn-sm { display: inline-block; font-size: 12px; font-weight: 700; padding: 6px 14px; border-radius: 8px; background: #4338CA; color: #fff; text-decoration: none; margin-top: 4px; }

    .sp-admin-dash-bottom { display: grid; gap: 24px; }
    @media(min-width:900px){ .sp-admin-dash-bottom { grid-template-columns: 280px 1fr; align-items: start; } }

    .sp-admin-status-card { background: #fff; border: 1px solid #E2E8F0; border-radius: 14px; padding: 18px; }
    .sp-admin-status-header { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; }
    .sp-admin-status-title { font-size: 14px; font-weight: 800; color: #0F172A; flex: 1; margin: 0; }
    .sp-admin-status-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
    .sp-admin-status-dot-green { background: #16A34A; box-shadow: 0 0 0 3px #DCFCE7; }
    .sp-admin-status-rows { display: flex; flex-direction: column; gap: 8px; }
    .sp-admin-status-row { display: flex; align-items: center; justify-content: space-between; font-size: 12.5px; color: #475569; }

    .sp-admin-analytics-grid { display: grid; grid-template-columns: repeat(3,1fr); gap: 14px; }
    @media(max-width:640px){ .sp-admin-analytics-grid { grid-template-columns: 1fr; } }
    .sp-admin-placeholder-card { background: #fff; border: 1.5px dashed #E2E8F0; border-radius: 14px; padding: 20px; }
    .sp-admin-placeholder-icon { font-size: 22px; margin-bottom: 8px; }
    .sp-admin-placeholder-title { font-size: 13px; font-weight: 700; color: #334155; margin-bottom: 4px; }
    .sp-admin-placeholder-desc { font-size: 12px; color: #94A3B8; line-height: 1.5; }
  `],
})
export class AdminDashboardComponent implements OnInit {
  students = signal<StudentListItem[]>([]);
  loadingStudents = signal(true);

  onboardedCount = () => this.students().filter(s => s.onboardingStatus === 'Complete').length;

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listStudents().subscribe({
      next: s => { this.students.set(s); this.loadingStudents.set(false); },
      error: () => { this.loadingStudents.set(false); },
    });
  }
}

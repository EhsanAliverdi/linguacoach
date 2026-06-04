import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-students',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="sp-admin-page-header">
      <div style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px">
        <div>
          <h1 class="sp-admin-page-title">Students</h1>
          <p class="sp-admin-page-sub">Manage pilot student accounts</p>
        </div>
        <a routerLink="../create-student" class="sp-admin-btn-primary">
          + Create student
        </a>
      </div>
    </div>
    @if (loading()) {
      <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
    } @else if (error()) {
      <div class="sp-admin-alert-error">{{ error() }}</div>
    } @else {
      <div class="sp-admin-table-card">
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
            @for (s of students(); track s.userId) {
              <tr>
                <td>{{ s.email }}</td>
                <td>
                  <span class="sp-admin-badge"
                    [class.sp-admin-badge-green]="s.onboardingStatus === 'Complete'"
                    [class.sp-admin-badge-amber]="s.onboardingStatus !== 'Complete'">
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
        @if (students().length === 0) {
          <div class="sp-admin-empty-row">No students yet.</div>
        }
      </div>
    }
  `,
})
export class AdminStudentsComponent implements OnInit {
  students = signal<StudentListItem[]>([]);
  loading = signal(true);
  error = signal('');

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.adminApi.listStudents().subscribe({
      next: s => { this.students.set(s); this.loading.set(false); },
      error: () => { this.error.set('Could not load students.'); this.loading.set(false); },
    });
  }
}

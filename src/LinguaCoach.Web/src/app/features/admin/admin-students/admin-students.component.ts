import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-students',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2 class="text-lg font-bold text-slate-900 mb-4">Students</h2>
    @if (loading()) {
      <div class="flex justify-center py-8"><div class="w-6 h-6 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin"></div></div>
    } @else if (error()) {
      <div class="text-sm text-red-600">{{ error() }}</div>
    } @else {
      <div class="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        <table class="w-full text-sm">
          <thead class="bg-slate-50 border-b border-slate-200">
            <tr>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Email</th>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Onboarding</th>
              <th class="text-left px-4 py-3 font-medium text-slate-600">CEFR</th>
              <th class="text-left px-4 py-3 font-medium text-slate-600">Joined</th>
            </tr>
          </thead>
          <tbody>
            @for (s of students(); track s.userId) {
              <tr class="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                <td class="px-4 py-3 text-slate-800">{{ s.email }}</td>
                <td class="px-4 py-3">
                  <span class="inline-block rounded-full px-2 py-0.5 text-xs font-medium
                    {{ s.onboardingStatus === 'Complete' ? 'bg-green-100 text-green-700' : 'bg-amber-100 text-amber-700' }}">
                    {{ s.onboardingStatus }}
                  </span>
                </td>
                <td class="px-4 py-3">
                  @if (s.cefrLevel) {
                    <span class="inline-block rounded-full bg-indigo-100 px-2 py-0.5 text-xs font-bold text-indigo-700">{{ s.cefrLevel }}</span>
                  } @else {
                    <span class="text-slate-400 text-xs">—</span>
                  }
                </td>
                <td class="px-4 py-3 text-slate-500 text-xs">{{ s.createdAt | date:'mediumDate' }}</td>
              </tr>
            }
          </tbody>
        </table>
        @if (students().length === 0) {
          <p class="px-4 py-6 text-sm text-slate-400 text-center">No students yet.</p>
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

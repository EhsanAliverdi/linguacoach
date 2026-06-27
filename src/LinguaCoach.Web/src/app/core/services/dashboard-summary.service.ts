import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StudentDashboardSummary } from '../models/dashboard-summary.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class DashboardSummaryService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getSummary(): Observable<StudentDashboardSummary> {
    return this.http.get<StudentDashboardSummary>(
      `${this.api}/student/dashboard/summary`
    );
  }
}

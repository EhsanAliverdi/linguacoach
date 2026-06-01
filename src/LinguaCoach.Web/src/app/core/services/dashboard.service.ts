import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardResponse } from '../models/dashboard.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<DashboardResponse> {
    return this.http.get<DashboardResponse>(`${this.api}/dashboard`);
  }
}

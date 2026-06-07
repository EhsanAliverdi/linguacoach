import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ProgressSummary } from '../models/progress.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ProgressService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getProgress(): Observable<ProgressSummary> {
    return this.http.get<ProgressSummary>(`${this.api}/progress`);
  }
}

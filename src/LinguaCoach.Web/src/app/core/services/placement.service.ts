import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PlacementStatus, PlacementCurrentSection, SavePlacementAnswers, PlacementResult,
} from '../models/placement.models';

@Injectable({ providedIn: 'root' })
export class PlacementService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<PlacementStatus> {
    return this.http.get<PlacementStatus>(`${this.api}/placement/status`);
  }

  start(): Observable<PlacementStatus> {
    return this.http.post<PlacementStatus>(`${this.api}/placement/start`, {});
  }

  getCurrent(): Observable<PlacementCurrentSection> {
    return this.http.get<PlacementCurrentSection>(`${this.api}/placement/current`);
  }

  saveAnswers(payload: SavePlacementAnswers): Observable<PlacementStatus> {
    return this.http.post<PlacementStatus>(`${this.api}/placement/answers`, payload);
  }

  complete(): Observable<PlacementResult> {
    return this.http.post<PlacementResult>(`${this.api}/placement/complete`, {});
  }

  getResult(): Observable<PlacementResult> {
    return this.http.get<PlacementResult>(`${this.api}/placement/result`);
  }
}

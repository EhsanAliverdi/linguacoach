import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LanguagePairDto, CareerProfileDto } from '../models/reference.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ReferenceService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getLanguagePairs(): Observable<LanguagePairDto[]> {
    return this.http.get<LanguagePairDto[]>(`${this.api}/reference/language-pairs`);
  }

  getCareerProfiles(languagePairId: string): Observable<CareerProfileDto[]> {
    return this.http.get<CareerProfileDto[]>(`${this.api}/reference/career-profiles`, {
      params: { languagePairId }
    });
  }
}

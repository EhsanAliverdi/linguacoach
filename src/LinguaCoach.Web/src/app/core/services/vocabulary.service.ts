import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StudentVocabularyItem, VocabularyItemStatus } from '../models/vocabulary.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class VocabularyService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getVocabulary(status?: string, category?: string): Observable<StudentVocabularyItem[]> {
    const params: Record<string, string> = {};
    if (status) params['status'] = status;
    if (category) params['category'] = category;
    return this.http.get<StudentVocabularyItem[]>(`${this.api}/vocabulary`, { params });
  }

  updateStatus(id: string, status: VocabularyItemStatus): Observable<void> {
    return this.http.patch<void>(`${this.api}/vocabulary/${id}/status`, { status });
  }
}

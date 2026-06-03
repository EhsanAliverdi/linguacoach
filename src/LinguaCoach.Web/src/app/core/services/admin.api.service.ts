import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  StudentListItem, PromptTemplateItem, PromptTemplateDetail,
  CareerProfileItem, CurriculumWordItem, AiProviderConfigItem, AiProviderCatalogItem
} from '../models/admin.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  // Students
  listStudents(): Observable<StudentListItem[]> {
    return this.http.get<StudentListItem[]>(`${this.api}/students`);
  }

  // Prompts
  listPrompts(): Observable<PromptTemplateItem[]> {
    return this.http.get<PromptTemplateItem[]>(`${this.api}/prompts`);
  }
  getPrompt(id: string): Observable<PromptTemplateDetail> {
    return this.http.get<PromptTemplateDetail>(`${this.api}/prompts/${id}`);
  }
  createPromptVersion(data: { key: string; content: string; maxInputTokens: number; maxOutputTokens: number }): Observable<PromptTemplateDetail> {
    return this.http.post<PromptTemplateDetail>(`${this.api}/prompts`, data);
  }
  activatePrompt(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/prompts/${id}/activate`, null);
  }
  deactivatePrompt(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/prompts/${id}/deactivate`, null);
  }

  // Careers + words
  listCareers(): Observable<CareerProfileItem[]> {
    return this.http.get<CareerProfileItem[]>(`${this.api}/careers`);
  }
  listWords(careerId: string, languagePairId: string): Observable<CurriculumWordItem[]> {
    return this.http.get<CurriculumWordItem[]>(`${this.api}/careers/${careerId}/words?languagePairId=${languagePairId}`);
  }
  addWord(careerId: string, data: object): Observable<CurriculumWordItem> {
    return this.http.post<CurriculumWordItem>(`${this.api}/careers/${careerId}/words`, data);
  }
  updateWord(wordId: string, data: object): Observable<CurriculumWordItem> {
    return this.http.put<CurriculumWordItem>(`${this.api}/careers/words/${wordId}`, data);
  }

  // AI feature routing
  listAiConfigs(): Observable<AiProviderConfigItem[]> {
    return this.http.get<AiProviderConfigItem[]>(`${this.api}/ai-config`);
  }
  updateAiConfig(id: string, providerName: string, modelName: string): Observable<AiProviderConfigItem> {
    return this.http.put<AiProviderConfigItem>(`${this.api}/ai-config/${id}`, { providerName, modelName });
  }

  // AI provider credentials
  listAiProviders(): Observable<AiProviderCatalogItem[]> {
    return this.http.get<AiProviderCatalogItem[]>(`${this.api}/ai-providers`);
  }
  setProviderApiKey(provider: string, apiKey: string | null): Observable<AiProviderCatalogItem> {
    return this.http.put<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/api-key`, { apiKey });
  }
  testProvider(provider: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/test`, null);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  StudentListItem, PromptTemplateItem, PromptTemplateDetail,
  CareerProfileItem, CurriculumWordItem,
  AiProviderCatalogItem, AdminStudentLearningMemory, UpdateStudentProfileRequest,
  AiConfigCategoryItem, UpdateAiCategoryRequest, CategoryTestResult
} from '../models/admin.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  // Students
  listStudents(includeArchived = false): Observable<StudentListItem[]> {
    const suffix = includeArchived ? '?includeArchived=true' : '';
    return this.http.get<StudentListItem[]>(`${this.api}/students${suffix}`);
  }
  updateStudent(studentProfileId: string, data: UpdateStudentProfileRequest): Observable<StudentListItem> {
    return this.http.put<StudentListItem>(`${this.api}/students/${studentProfileId}`, data);
  }
  archiveStudent(studentProfileId: string): Observable<StudentListItem> {
    return this.http.post<StudentListItem>(`${this.api}/students/${studentProfileId}/archive`, null);
  }
  getStudentLearningMemory(studentProfileId: string): Observable<AdminStudentLearningMemory> {
    return this.http.get<AdminStudentLearningMemory>(`${this.api}/students/${studentProfileId}/learning-memory`);
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

  // AI provider credentials
  listAiProviders(): Observable<AiProviderCatalogItem[]> {
    return this.http.get<AiProviderCatalogItem[]>(`${this.api}/ai-providers`);
  }
  setProviderApiKey(provider: string, apiKey: string | null): Observable<AiProviderCatalogItem> {
    return this.http.put<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/api-key`, { apiKey });
  }
  setProviderEndpoint(provider: string, apiEndpoint: string | null): Observable<AiProviderCatalogItem> {
    return this.http.put<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/endpoint`, { apiEndpoint });
  }
  testProvider(provider: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/test`, null);
  }
  addProviderModel(provider: string, modelName: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/models`, { modelName });
  }
  testProviderModel(provider: string, modelName: string): Observable<AiProviderCatalogItem> {
    return this.http.post<AiProviderCatalogItem>(`${this.api}/ai-providers/${provider}/models/test`, { modelName });
  }

  // AI config categories
  listAiCategories(): Observable<AiConfigCategoryItem[]> {
    return this.http.get<AiConfigCategoryItem[]>(`${this.api}/ai/categories`);
  }
  updateAiCategory(categoryKey: string, data: UpdateAiCategoryRequest): Observable<AiConfigCategoryItem> {
    return this.http.patch<AiConfigCategoryItem>(`${this.api}/ai/categories/${categoryKey}`, data);
  }
  testAiCategory(categoryKey: string): Observable<CategoryTestResult> {
    return this.http.post<CategoryTestResult>(`${this.api}/ai/categories/${categoryKey}/test`, null);
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ExerciseTypeDefinition, UpdateExerciseTypeRequest } from '../models/admin.models';

export interface CreateStudentRequest {
  email: string;
  temporaryPassword: string;
  mustChangePassword?: boolean;
  firstName?: string;
  lastName?: string;
  displayName?: string;
  careerContext?: string;
  learningGoal?: string;
  preferredSessionDurationMinutes?: number;
  professionalExperienceLevel?: number;
  roleFamiliarity?: number;
}

export interface CreateStudentResponse {
  studentProfileId: string;
  userId: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createStudent(request: CreateStudentRequest): Observable<CreateStudentResponse> {
    return this.http.post<CreateStudentResponse>(`${this.api}/admin/students`, request);
  }

  listExerciseTypes(): Observable<ExerciseTypeDefinition[]> {
    return this.http.get<ExerciseTypeDefinition[]>(`${this.api}/admin/exercise-types`);
  }

  updateExerciseType(key: string, request: UpdateExerciseTypeRequest): Observable<ExerciseTypeDefinition> {
    return this.http.patch<ExerciseTypeDefinition>(`${this.api}/admin/exercise-types/${key}`, request);
  }
}

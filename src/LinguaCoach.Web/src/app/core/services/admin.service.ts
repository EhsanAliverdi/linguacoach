import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

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
}

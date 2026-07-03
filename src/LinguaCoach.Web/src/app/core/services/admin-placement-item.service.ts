import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminPlacementItemDto, PlacementItemRequest } from '../models/admin-placement-item.models';

@Injectable({ providedIn: 'root' })
export class AdminPlacementItemService {
  private readonly base = `${environment.apiUrl}/admin/placement-items`;

  constructor(private http: HttpClient) {}

  list(): Observable<AdminPlacementItemDto[]> {
    return this.http.get<AdminPlacementItemDto[]>(this.base);
  }

  add(request: PlacementItemRequest): Observable<AdminPlacementItemDto> {
    return this.http.post<AdminPlacementItemDto>(this.base, request);
  }

  update(itemId: string, request: PlacementItemRequest): Observable<AdminPlacementItemDto> {
    return this.http.put<AdminPlacementItemDto>(`${this.base}/${itemId}`, request);
  }

  remove(itemId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${itemId}`);
  }
}

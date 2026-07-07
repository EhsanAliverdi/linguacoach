import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AdminPlacementItemDto,
  AdminPlacementItemListResult,
  PlacementItemRequest,
} from '../models/admin-placement-item.models';

@Injectable({ providedIn: 'root' })
export class AdminPlacementItemService {
  private readonly base = `${environment.apiUrl}/admin/placement-items`;

  constructor(private http: HttpClient) {}

  list(page: number, pageSize: number, skill?: string, search?: string): Observable<AdminPlacementItemListResult> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (skill && skill !== 'all') params = params.set('skill', skill);
    if (search) params = params.set('search', search);
    return this.http.get<AdminPlacementItemListResult>(this.base, { params });
  }

  get(itemId: string): Observable<AdminPlacementItemDto> {
    return this.http.get<AdminPlacementItemDto>(`${this.base}/${itemId}`);
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

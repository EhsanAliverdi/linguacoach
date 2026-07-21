import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Static, dev-authored admin help copy (HTML strings keyed by a dotted key, e.g.
 * "admin.skillGraph.sweepUntaggedModules"). Fetched once per session and cached — this content
 * is never edited at runtime, only added via backend seeding, so there is no reason to refetch.
 */
@Injectable({ providedIn: 'root' })
export class AdminHelpContentService {
  private readonly api = `${environment.apiUrl}/admin/help-content`;
  private content$: Observable<Record<string, string>> | null = null;

  constructor(private http: HttpClient) {}

  private load(): Observable<Record<string, string>> {
    if (!this.content$) {
      this.content$ = this.http
        .get<Record<string, string>>(this.api)
        .pipe(shareReplay(1));
    }
    return this.content$;
  }

  get(key: string): Observable<string | undefined> {
    return new Observable(subscriber => {
      this.load().subscribe({
        next: map => {
          subscriber.next(map[key]);
          subscriber.complete();
        },
        error: err => subscriber.error(err),
      });
    });
  }
}

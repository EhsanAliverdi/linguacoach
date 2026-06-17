import { Injectable, signal } from '@angular/core';

export interface AdminDrawerState {
  title: string;
  context?: unknown;
}

@Injectable({ providedIn: 'root' })
export class AdminDrawerService {
  readonly state = signal<AdminDrawerState | null>(null);

  open(state: AdminDrawerState): void {
    this.state.set(state);
  }

  close(): void {
    this.state.set(null);
  }
}

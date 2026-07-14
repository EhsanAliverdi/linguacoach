import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'warning' | 'info' | 'progress';

export interface ToastMessage {
  id: number;
  kind: ToastKind;
  message: string;
  /** Only set for kind 'progress' — current/total item counts for a progress bar. */
  progressCurrent?: number;
  progressTotal?: number;
}

/** Phase K11 — this app is mounted once at the admin shell (SpAdminToastOutletComponent lives in
 *  AdminAppLayoutComponent, not any individual page), so a toast created here survives route
 *  navigation — used for long-running background actions (e.g. bulk "Fix with AI") the admin may
 *  navigate away from mid-run. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly messages = signal<ToastMessage[]>([]);

  success(message: string): void { this.show('success', message); }
  error(message: string): void { this.show('error', message); }
  warning(message: string): void { this.show('warning', message); }
  info(message: string): void { this.show('info', message); }

  /** Creates a persistent (no auto-dismiss) progress toast and returns its id — pass that id to
   *  updateProgress()/dismiss() as the operation advances/finishes. */
  showProgress(message: string, current: number, total: number): number {
    const id = this.nextId++;
    this.messages.update(items => [
      ...items,
      { id, kind: 'progress', message, progressCurrent: current, progressTotal: total },
    ]);
    return id;
  }

  updateProgress(id: number, message: string, current: number, total: number): void {
    this.messages.update(items => items.map(item =>
      item.id === id ? { ...item, message, progressCurrent: current, progressTotal: total } : item));
  }

  dismiss(id: number): void {
    this.messages.update(items => items.filter(item => item.id !== id));
  }

  private show(kind: ToastKind, message: string): void {
    const id = this.nextId++;
    this.messages.update(items => [...items, { id, kind, message }]);
    window.setTimeout(() => this.dismiss(id), 4500);
  }
}

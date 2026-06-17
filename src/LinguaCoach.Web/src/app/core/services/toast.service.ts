import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'warning' | 'info';

export interface ToastMessage {
  id: number;
  kind: ToastKind;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly messages = signal<ToastMessage[]>([]);

  success(message: string): void { this.show('success', message); }
  error(message: string): void { this.show('error', message); }
  warning(message: string): void { this.show('warning', message); }
  info(message: string): void { this.show('info', message); }

  dismiss(id: number): void {
    this.messages.update(items => items.filter(item => item.id !== id));
  }

  private show(kind: ToastKind, message: string): void {
    const id = this.nextId++;
    this.messages.update(items => [...items, { id, kind, message }]);
    window.setTimeout(() => this.dismiss(id), 4500);
  }
}

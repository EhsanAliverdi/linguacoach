import { Injectable, signal } from '@angular/core';

export interface AdminConfirmRequest {
  title: string;
  body: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

@Injectable({ providedIn: 'root' })
export class AdminModalService {
  readonly confirmRequest = signal<AdminConfirmRequest | null>(null);

  confirm(request: AdminConfirmRequest): void {
    this.confirmRequest.set(request);
  }

  close(): void {
    this.confirmRequest.set(null);
  }
}

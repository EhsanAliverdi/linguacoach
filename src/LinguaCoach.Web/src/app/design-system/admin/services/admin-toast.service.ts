import { Injectable } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Injectable({ providedIn: 'root' })
export class AdminToastService {
  constructor(private toast: ToastService) {}

  success(message: string): void { this.toast.success(message); }
  error(message: string): void { this.toast.error(message); }
  warning(message: string): void { this.toast.warning(message); }
  info(message: string): void { this.toast.info(message); }
  dismiss(id: number): void { this.toast.dismiss(id); }
}


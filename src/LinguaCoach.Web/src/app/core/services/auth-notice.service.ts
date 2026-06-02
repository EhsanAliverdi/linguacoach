import { Injectable, signal } from '@angular/core';

const NoticeKey = 'speakpath.auth.notice';

@Injectable({ providedIn: 'root' })
export class AuthNoticeService {
  readonly message = signal<string | null>(this.restore());

  set(message: string): void {
    this.message.set(message);
    sessionStorage.setItem(NoticeKey, message);
  }

  clear(): void {
    this.message.set(null);
    sessionStorage.removeItem(NoticeKey);
  }

  consume(): string | null {
    const message = this.message();
    this.clear();
    return message;
  }

  private restore(): string | null {
    return sessionStorage.getItem(NoticeKey);
  }
}

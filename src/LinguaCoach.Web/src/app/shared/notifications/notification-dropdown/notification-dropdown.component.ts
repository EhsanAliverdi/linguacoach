import {
  Component, OnInit, OnDestroy, signal, computed,
  ElementRef, HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Router } from '@angular/router';
import { NotificationService, NotificationItem } from '../../../core/services/notification.service';

/**
 * App-owned live notification bell + dropdown.
 * Lives in committed source (not gitignored vendor/templates).
 * Replaces the static bell placeholder in StudentAppLayout.
 */
@Component({
  selector: 'sp-notification-dropdown',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './notification-dropdown.component.html',
})
export class NotificationDropdownComponent implements OnInit, OnDestroy {
  isOpen = false;

  readonly notifications = signal<NotificationItem[]>([]);
  readonly unreadCount = signal<number>(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly hasUnread = computed(() => this.unreadCount() > 0);

  constructor(
    private notificationSvc: NotificationService,
    private router: Router,
    private elRef: ElementRef,
  ) {}

  ngOnInit(): void {
    this.loadUnreadCount();
  }

  ngOnDestroy(): void {}

  @HostListener('document:mousedown', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen && !this.elRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  toggleDropdown(): void {
    this.isOpen = !this.isOpen;
    if (this.isOpen) {
      this.loadNotifications();
    }
  }

  closeDropdown(): void {
    this.isOpen = false;
  }

  loadNotifications(): void {
    this.loading.set(true);
    this.error.set(null);
    this.notificationSvc.list(1, 20).subscribe({
      next: (res) => {
        this.notifications.set(res.items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load notifications.');
        this.loading.set(false);
      },
    });
  }

  loadUnreadCount(): void {
    this.notificationSvc.getUnreadCount().subscribe({
      next: (res) => this.unreadCount.set(res.unreadCount),
      error: () => { /* silent — bell still renders */ },
    });
  }

  onNotificationClick(item: NotificationItem): void {
    if (!item.readAtUtc) {
      this.notificationSvc.markRead(item.id).subscribe({
        next: () => {
          this.notifications.update(ns =>
            ns.map(n => n.id === item.id ? { ...n, readAtUtc: new Date().toISOString() } : n)
          );
          this.unreadCount.update(c => Math.max(0, c - 1));
        },
      });
    }
    if (item.deepLinkUrl) {
      this.closeDropdown();
      this.router.navigateByUrl(item.deepLinkUrl);
    }
  }

  onMarkAllRead(): void {
    this.notificationSvc.markAllRead().subscribe({
      next: () => {
        const now = new Date().toISOString();
        this.notifications.update(ns => ns.map(n => ({ ...n, readAtUtc: n.readAtUtc ?? now })));
        this.unreadCount.set(0);
      },
    });
  }

  onArchive(event: Event, id: string): void {
    event.stopPropagation();
    this.notificationSvc.archive(id).subscribe({
      next: () => {
        const removed = this.notifications().find(n => n.id === id);
        this.notifications.update(ns => ns.filter(n => n.id !== id));
        if (removed && !removed.readAtUtc) {
          this.unreadCount.update(c => Math.max(0, c - 1));
        }
      },
    });
  }

  retry(): void {
    this.loadNotifications();
  }

  severityIcon(severity: string): string {
    switch (severity.toLowerCase()) {
      case 'error':   return '🔴';
      case 'warning': return '🟡';
      case 'success': return '🟢';
      default:        return '🔵';
    }
  }

  timeAgo(iso: string): string {
    const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
    if (diff < 60)    return `${diff}s ago`;
    if (diff < 3600)  return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
  }
}

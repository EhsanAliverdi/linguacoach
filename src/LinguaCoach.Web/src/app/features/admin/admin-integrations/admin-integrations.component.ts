import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
} from '../../../core/services/admin-integrations.service';
import {
  SpAdminBadgeComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
} from '../../../design-system/admin';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';

@Component({
  selector: 'app-admin-integrations',
  standalone: true,
  imports: [CommonModule, RouterLink, SpAdminBadgeComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminButtonComponent, SpAdminVisualPlaceholderComponent],
  templateUrl: './admin-integrations.component.html',
  styles: [`
    .sp-int-card{background:var(--sp-admin-surface,#fff);border:1px solid var(--sp-admin-border,#ECE9F5);border-radius:16px;padding:20px;display:flex;flex-direction:column;gap:12px;}
    .sp-int-card-header{display:flex;align-items:center;gap:12px;}
    .sp-int-card-icon{width:40px;height:40px;border-radius:10px;display:grid;place-items:center;flex-shrink:0;}
    .sp-int-card-name{font-size:14px;font-weight:700;color:var(--sp-admin-text,#211B36);}
    .sp-int-card-desc{font-size:13px;color:var(--sp-admin-muted,#8B85A0);flex:1;}
    .sp-int-card-actions{display:flex;gap:8px;margin-top:4px;}
    .sp-int-card-not-impl{font-size:12px;color:var(--sp-admin-muted,#8B85A0);}
  `],
})
export class AdminIntegrationsComponent implements OnInit {
  // Storage
  storage = signal<StorageSettings | null>(null);
  storageTest = signal<StorageTestResult | null>(null);
  storageError = signal('');
  testing = signal(false);

  loading = signal(true);

  constructor(private svc: AdminIntegrationsService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.svc.getStorage().subscribe({
      next: s => this.storage.set(s),
      error: err => this.storageError.set(err.error?.error ?? 'Could not load storage settings.'),
    });
    this.loading.set(false);
  }

  testConnection(): void {
    this.testing.set(true);
    this.storageTest.set(null);
    this.svc.testStorage().subscribe({
      next: r => { this.storageTest.set(r); this.testing.set(false); },
      error: err => {
        this.testing.set(false);
        this.storageTest.set({ ok: false, lastCheckedUtc: new Date().toISOString(), error: err.error?.error ?? 'Test failed.' });
      },
    });
  }
}

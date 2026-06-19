import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
  GenerationSettings,
  BatchesResponse,
} from '../../../core/services/admin-integrations.service';
import {
  SpAdminCardComponent,
  SpAdminBadgeComponent,
  SpAdminErrorStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminStatCardComponent,
  SpAdminTableComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-integrations',
  standalone: true,
  imports: [CommonModule, FormsModule, SpAdminBadgeComponent, SpAdminCardComponent, SpAdminErrorStateComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminButtonComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminStatCardComponent, SpAdminTableComponent],
  templateUrl: './admin-integrations.component.html',
  styles: [`
    .sp-admin-integration-metrics{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;margin-bottom:16px;}
    .sp-adm-num-input{width:100%;height:44px;border:1px solid #E5E7EB;border-radius:8px;padding:0 16px;font-size:13px;background:#fff;color:#1A2130;box-sizing:border-box;}
    .sp-adm-num-input:focus{outline:none;border-color:#93C5FD;box-shadow:0 0 0 2px rgba(59,130,246,.1);}
  `],
})
export class AdminIntegrationsComponent implements OnInit {
  // Storage
  storage = signal<StorageSettings | null>(null);
  storageTest = signal<StorageTestResult | null>(null);
  storageError = signal('');
  testing = signal(false);

  // Generation settings
  settings = signal<GenerationSettings | null>(null);
  settingsError = signal('');
  settingsSaved = signal(false);

  // Background jobs
  batches = signal<BatchesResponse | null>(null);
  batchesError = signal('');
  generateStatus = signal('');
  generateStudentId = '';

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
    this.svc.getGenerationSettings().subscribe({
      next: s => this.settings.set(s),
      error: err => this.settingsError.set(err.error?.error ?? 'Could not load generation settings.'),
    });
    this.loadBatches();
    this.loading.set(false);
  }

  loadBatches(): void {
    this.svc.getBatches().subscribe({
      next: b => this.batches.set(b),
      error: err => this.batchesError.set(err.error?.error ?? 'Could not load background jobs.'),
    });
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

  saveSettings(): void {
    const s = this.settings();
    if (!s) return;
    this.settingsError.set('');
    this.settingsSaved.set(false);
    this.svc.updateGenerationSettings(s).subscribe({
      next: updated => { this.settings.set(updated); this.settingsSaved.set(true); },
      error: err => this.settingsError.set(err.error?.error ?? 'Could not save settings.'),
    });
  }

  retry(id: string): void {
    this.svc.retryBatch(id).subscribe({ next: () => this.loadBatches() });
  }

  cancel(id: string): void {
    this.batchesError.set('');
    this.svc.cancelBatch(id).subscribe({
      next: () => this.loadBatches(),
      error: err => this.batchesError.set(err.error?.error ?? 'Could not cancel this batch.'),
    });
  }

  batchStatusTone(status: string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' {
    switch (status) {
      case 'Completed': return 'success';
      case 'Failed': return 'danger';
      case 'Partial': return 'warning';
      case 'Running': return 'info';
      case 'Queued': return 'neutral';
      default: return 'neutral';
    }
  }

  generateLessons(): void {
    if (!this.generateStudentId) return;
    this.generateStatus.set('');
    this.batchesError.set('');
    const studentId = this.generateStudentId;
    this.svc.generateLessons(studentId).subscribe({
      next: res => {
        this.generateStudentId = '';
        this.generateStatus.set(`Queued generation for ${studentId} (${res?.requestedCount ?? '?'} sessions).`);
        this.loadBatches();
      },
      error: err => {
        this.batchesError.set(err.error?.error ?? 'Could not queue lesson generation. Check the student profile ID.');
      },
    });
  }
}

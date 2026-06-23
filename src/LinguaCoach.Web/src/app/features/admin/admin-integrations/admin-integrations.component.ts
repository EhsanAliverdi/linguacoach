import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
  GenerationSettings,
  BatchesResponse,
} from '../../../core/services/admin-integrations.service';
import {
  SpAdminAlertComponent,
  SpAdminCardComponent,
  SpAdminBadgeComponent,
  SpAdminCheckboxComponent,
  SpAdminCopyableTextComponent,
  SpAdminErrorStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminButtonComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminFormGridComponent,
  SpAdminKpiCardComponent,
  SpAdminSectionHeaderComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-admin-integrations',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SpAdminAlertComponent, SpAdminBadgeComponent, SpAdminCardComponent, SpAdminCheckboxComponent, SpAdminCopyableTextComponent, SpAdminErrorStateComponent, SpAdminFormGridComponent, SpAdminKpiCardComponent, SpAdminLoadingStateComponent, SpAdminNumberInputComponent, SpAdminPageBodyComponent, SpAdminPageHeaderComponent, SpAdminButtonComponent, SpAdminFormFieldComponent, SpAdminInputComponent, SpAdminSectionHeaderComponent, SpAdminTableComponent, SpAdminTruncatedTextComponent],
  templateUrl: './admin-integrations.component.html',
  styles: [`
    .sp-int-kpi-strip{display:grid;grid-template-columns:repeat(2,1fr);gap:14px;margin-bottom:16px;}
    @media(min-width:900px){.sp-int-kpi-strip{grid-template-columns:repeat(4,1fr);}}
    .sp-int-card-grid{display:grid;gap:16px;margin-bottom:24px;}
    @media(min-width:700px){.sp-int-card-grid{grid-template-columns:1fr 1fr;}}
    @media(min-width:1100px){.sp-int-card-grid{grid-template-columns:repeat(3,1fr);}}
    .sp-int-card{background:var(--sp-admin-surface,#fff);border:1px solid var(--sp-admin-border,#ECE9F5);border-radius:16px;padding:20px;display:flex;flex-direction:column;gap:12px;}
    .sp-int-card-header{display:flex;align-items:center;gap:12px;}
    .sp-int-card-icon{width:40px;height:40px;border-radius:10px;display:grid;place-items:center;flex-shrink:0;}
    .sp-int-card-name{font-size:14px;font-weight:700;color:var(--sp-admin-text,#0F172A);}
    .sp-int-card-desc{font-size:13px;color:var(--sp-admin-muted,#6b7280);flex:1;}
    .sp-int-card-actions{display:flex;gap:8px;margin-top:4px;}
    .sp-int-card-not-impl{font-size:12px;color:var(--sp-admin-muted,#9ca3af);}
    .sp-int-cb-stack{display:flex;flex-direction:column;gap:10px;margin-top:12px;}
    .sp-int-test-row{display:flex;align-items:center;gap:16px;margin-top:16px;}
    .sp-int-generate-row{display:flex;align-items:flex-end;gap:12px;margin-bottom:16px;flex-wrap:wrap;}
    .sp-int-api-url{font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,monospace;font-size:12px;background:var(--sp-admin-surface-alt,#f6f4fb);border-radius:6px;padding:8px 12px;color:var(--sp-admin-muted,#6b7280);word-break:break-all;}
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

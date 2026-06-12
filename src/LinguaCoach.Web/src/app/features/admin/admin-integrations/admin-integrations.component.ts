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

@Component({
  selector: 'app-admin-integrations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-integrations.component.html',
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

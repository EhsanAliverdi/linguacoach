import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminCardComponent,
  SpAdminButtonComponent,
  SpAdminNumberInputComponent,
  SpAdminFormFieldComponent,
  SpAdminToggleComponent,
} from '../../../design-system/admin';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';

@Component({
  selector: 'app-admin-lessons',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminCardComponent,
    SpAdminButtonComponent,
    SpAdminNumberInputComponent,
    SpAdminFormFieldComponent,
    SpAdminToggleComponent,
    SpAdminVisualPlaceholderComponent,
  ],
  styles: [`
    .sp-les-pool-grid { display: grid; gap: 16px; margin-bottom: 24px; }
    @media(min-width: 700px) { .sp-les-pool-grid { grid-template-columns: 1fr 1fr; } }
    .sp-les-settings-grid { display: grid; gap: 16px; }
    @media(min-width: 700px) { .sp-les-settings-grid { grid-template-columns: 1fr 1fr; } }
    .sp-les-toggle-row { display: flex; align-items: center; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid var(--sp-admin-border, #ECE9F5); }
    .sp-les-toggle-row:last-child { border-bottom: none; }
    .sp-les-toggle-label { font-size: 13px; font-weight: 500; color: var(--sp-admin-text, #211B36); }
    .sp-les-generate-row { display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }
    .sp-les-mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 12px; color: var(--sp-admin-muted, #8B85A0); }
  `],
  template: `
    <sp-admin-page-header
      title="Lessons"
      subtitle="Lesson generation, ready buffer per student, and readiness pool health." />

    <sp-admin-page-body>

      <!-- Lesson generation card -->
      <sp-admin-card title="Lesson generation">
        <p style="font-size:13px;color:var(--sp-admin-muted,#8B85A0);margin-bottom:16px;">
          Generate lessons for student
        </p>
        <div class="sp-les-generate-row">
          <sp-admin-button variant="primary" size="sm" (clicked)="generateLessons()">
            Generate lessons now
          </sp-admin-button>
        </div>
        @if (generateStatus()) {
          <p style="font-size:13px;color:var(--sp-admin-muted,#8B85A0);">{{ generateStatus() }}</p>
        }
      </sp-admin-card>

      <!-- Ready lesson buffer per student -->
      <sp-admin-card title="Ready lesson buffer per student">
        <sp-admin-visual-placeholder
          state="not-available"
          title="Ready lesson buffer"
          message="Per-student ready lesson buffer is available on the student detail page. Aggregate view: backend not available yet." />
      </sp-admin-card>

      <!-- Lesson buffer settings -->
      <sp-admin-card title="Lesson Buffer Settings">
        <p style="font-size:13px;color:var(--sp-admin-muted,#8B85A0);margin-bottom:16px;">
          Control background generation of ready lessons and TTS audio.
        </p>
        <div class="sp-les-settings-grid">
          <sp-admin-form-field label="Ready lesson buffer size">
            <sp-admin-number-input placeholder="e.g. 5" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Refill threshold">
            <sp-admin-number-input placeholder="e.g. 2" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Refill batch size">
            <sp-admin-number-input placeholder="e.g. 3" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max generation attempts">
            <sp-admin-number-input placeholder="e.g. 3" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Generation timeout (s)">
            <sp-admin-number-input placeholder="e.g. 120" />
          </sp-admin-form-field>
          <sp-admin-form-field label="TTS timeout (s)">
            <sp-admin-number-input placeholder="e.g. 60" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max concurrent generation jobs">
            <sp-admin-number-input placeholder="e.g. 2" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Max concurrent TTS jobs">
            <sp-admin-number-input placeholder="e.g. 4" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Practice ready per type">
            <sp-admin-number-input placeholder="e.g. 3" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Practice refill threshold">
            <sp-admin-number-input placeholder="e.g. 1" />
          </sp-admin-form-field>
          <sp-admin-form-field label="Practice refill count">
            <sp-admin-number-input placeholder="e.g. 2" />
          </sp-admin-form-field>
        </div>
        <div style="margin-top:16px;">
          <div class="sp-les-toggle-row">
            <span class="sp-les-toggle-label">Background generation</span>
            <sp-admin-toggle [checked]="bgGenEnabled()" (changed)="bgGenEnabled.set($event)" />
          </div>
          <div class="sp-les-toggle-row">
            <span class="sp-les-toggle-label">TTS generation</span>
            <sp-admin-toggle [checked]="ttsEnabled()" (changed)="ttsEnabled.set($event)" />
          </div>
        </div>
        <div style="margin-top:16px;">
          <sp-admin-button variant="primary" size="sm" (clicked)="saveSettings()">Save</sp-admin-button>
        </div>
        <p style="font-size:12px;color:var(--sp-admin-muted,#8B85A0);margin-top:8px;">
          Backend not available yet — settings shown for reference only.
        </p>
      </sp-admin-card>

      <!-- Readiness pool aggregate health -->
      <sp-admin-card title="Readiness pool — aggregate health">
        <sp-admin-visual-placeholder
          state="not-available"
          title="Aggregate pool health"
          message="A system-wide readiness pool aggregate endpoint is not yet implemented. Per-student pool health is available on the student detail page." />
      </sp-admin-card>

    </sp-admin-page-body>
  `,
})
export class AdminLessonsComponent {
  generateStatus = signal('');
  bgGenEnabled = signal(true);
  ttsEnabled = signal(true);

  generateLessons(): void {
    this.generateStatus.set('Backend not available yet — lesson generation not implemented.');
  }

  saveSettings(): void {
    this.generateStatus.set('Backend not available yet — settings not saved.');
  }
}

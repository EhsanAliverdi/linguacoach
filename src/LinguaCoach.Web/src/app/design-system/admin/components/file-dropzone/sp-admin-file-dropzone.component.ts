import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminIconComponent } from '../icon/sp-admin-icon.component';

/**
 * A single-file picker styled as a dashed drop zone (label wrapping a visually-hidden native
 * `<input type="file">` — no real drag-and-drop wiring, click-to-browse only, matching every
 * other admin form's file inputs). Shows the selected file's name once chosen.
 */
@Component({
  selector: 'sp-admin-file-dropzone',
  standalone: true,
  imports: [CommonModule, SpAdminIconComponent],
  template: `
    <label class="sp-adm-dropzone" [class.sp-adm-dropzone--disabled]="disabled">
      <sp-admin-icon name="upload" size="lg" tone="muted" />
      <span class="sp-adm-dropzone-name">{{ fileName || placeholder }}</span>
      <span class="sp-adm-dropzone-hint">{{ hint }}</span>
      <input type="file" class="sp-adm-dropzone-input" [accept]="accept" [disabled]="disabled" (change)="onFileChange($event)" />
    </label>
  `,
  styles: [`
    :host { display: block; }

    .sp-adm-dropzone {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 8px;
      border: 1.5px dashed var(--sp-admin-border-2, #E2DEF0);
      border-radius: 10px;
      padding: 28px 16px;
      cursor: pointer;
      background: var(--sp-admin-bg, #F6F4FB);
      text-align: center;
      transition: border-color var(--sp-admin-transition-fast, .12s ease);
    }
    .sp-adm-dropzone:hover { border-color: var(--sp-admin-primary, #5B4BE8); }
    .sp-adm-dropzone--disabled { cursor: not-allowed; opacity: .6; }
    .sp-adm-dropzone--disabled:hover { border-color: var(--sp-admin-border-2, #E2DEF0); }

    .sp-adm-dropzone-name {
      font-size: 13px;
      font-weight: 700;
      color: var(--sp-admin-text, #211B36);
    }
    .sp-adm-dropzone-hint {
      font-size: 12px;
      color: var(--sp-admin-text-muted, #8B85A0);
    }
    .sp-adm-dropzone-input {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
  `],
})
export class SpAdminFileDropzoneComponent {
  @Input() fileName: string | null = null;
  @Input() placeholder = 'Choose a file to upload';
  @Input() hint = '';
  @Input() accept = '';
  @Input() disabled = false;
  @Output() fileSelected = new EventEmitter<File | null>();

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.fileSelected.emit(input.files && input.files.length > 0 ? input.files[0] : null);
  }
}

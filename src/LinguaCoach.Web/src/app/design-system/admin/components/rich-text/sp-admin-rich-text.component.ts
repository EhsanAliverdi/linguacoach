import {
  AfterViewInit, Component, ElementRef, forwardRef, Input, OnDestroy, ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import { Observable } from 'rxjs';
import { AtomImage, AudioEmbed, VideoEmbed } from './rich-text-media-nodes';

export interface SpAdminRichTextUploadResult {
  url: string;
  mimeType: string;
}

/**
 * TipTap-backed rich text editor with inline image/audio/video embeds, replacing the plain
 * textarea + "one value per line" convention previously used for Lesson Body/Examples/Common
 * Mistakes/Usage Notes. `ControlValueAccessor` value is the editor's sanitized-on-save HTML
 * string — same contract `sp-admin-textarea` already has, so it drops into existing
 * `[(ngModel)]` bindings unchanged.
 *
 * Media upload is intentionally NOT baked in: the caller supplies `[uploadMedia]`, a function
 * that uploads a `File` and resolves to `{ url, mimeType }` (see `AdminLessonService.uploadMedia`)
 * — keeps this design-system component free of any feature-specific HTTP dependency, matching
 * `sp-admin-file-dropzone`'s emit-only convention as closely as an inline-insert editor allows.
 */
@Component({
  selector: 'sp-admin-rich-text',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SpAdminRichTextComponent),
      multi: true,
    },
  ],
  template: `
    <div class="sp-adm-richtext" [class.sp-adm-richtext--compact]="compact" [class.sp-adm-richtext--disabled]="isDisabled">
      <div class="sp-adm-richtext-toolbar" role="toolbar" aria-label="Formatting">
        <button type="button" class="sp-adm-rt-btn" [class.sp-adm-rt-btn--active]="isActive('bold')"
                [disabled]="isDisabled" (click)="toggleBold()" title="Bold"><strong>B</strong></button>
        <button type="button" class="sp-adm-rt-btn" [class.sp-adm-rt-btn--active]="isActive('italic')"
                [disabled]="isDisabled" (click)="toggleItalic()" title="Italic"><em>I</em></button>
        <span class="sp-adm-rt-sep"></span>
        <button type="button" class="sp-adm-rt-btn" [class.sp-adm-rt-btn--active]="isActive('bulletList')"
                [disabled]="isDisabled" (click)="toggleBulletList()" title="Bullet list">&bull; List</button>
        <button type="button" class="sp-adm-rt-btn" [class.sp-adm-rt-btn--active]="isActive('orderedList')"
                [disabled]="isDisabled" (click)="toggleOrderedList()" title="Numbered list">1. List</button>
        <span class="sp-adm-rt-sep"></span>
        <button type="button" class="sp-adm-rt-btn" [class.sp-adm-rt-btn--active]="isActive('link')"
                [disabled]="isDisabled" (click)="toggleLink()" title="Link">Link</button>
        @if (uploadMedia) {
          <span class="sp-adm-rt-sep"></span>
          <button type="button" class="sp-adm-rt-btn" [disabled]="isDisabled || uploading" (click)="imageInput.click()" title="Insert image">Image</button>
          <button type="button" class="sp-adm-rt-btn" [disabled]="isDisabled || uploading" (click)="audioInput.click()" title="Insert audio">Audio</button>
          <button type="button" class="sp-adm-rt-btn" [disabled]="isDisabled || uploading" (click)="videoInput.click()" title="Insert video">Video</button>
          @if (uploading) { <span class="sp-adm-rt-uploading">Uploading…</span> }
        }
      </div>
      <div #editorHost class="sp-adm-richtext-content"></div>
      @if (uploadError) { <p class="sp-adm-rt-error">{{ uploadError }}</p> }
    </div>
    <input #imageInput type="file" accept="image/png,image/jpeg,image/webp,image/gif" class="sp-adm-rt-hidden-input"
           (change)="onFileSelected($event, 'image')" />
    <input #audioInput type="file" accept="audio/webm,audio/wav,audio/mpeg,audio/mp4,audio/ogg" class="sp-adm-rt-hidden-input"
           (change)="onFileSelected($event, 'audio')" />
    <input #videoInput type="file" accept="video/mp4,video/webm" class="sp-adm-rt-hidden-input"
           (change)="onFileSelected($event, 'video')" />
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .sp-adm-richtext { border: 1px solid var(--sp-admin-border, #ECE9F5); border-radius: 8px; background: transparent; overflow: hidden; }
    .sp-adm-richtext--disabled { opacity: .55; }

    .sp-adm-richtext-toolbar { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; padding: 6px 8px; border-bottom: 1px solid var(--sp-admin-border, #ECE9F5); background: var(--sp-admin-surface-subtle, #FBFAFE); }
    .sp-adm-richtext--compact .sp-adm-richtext-toolbar { padding: 4px 6px; gap: 2px; }

    .sp-adm-rt-btn { border: 1px solid transparent; background: transparent; border-radius: 6px; padding: 4px 8px; font-size: 12.5px; color: var(--sp-admin-text, #211B36); cursor: pointer; }
    .sp-adm-rt-btn:hover:not(:disabled) { background: var(--sp-admin-border, #ECE9F5); }
    .sp-adm-rt-btn--active { background: var(--sp-admin-primary-bg, rgba(91,75,232,.12)); color: var(--sp-admin-primary, #5B4BE8); }
    .sp-adm-rt-btn:disabled { cursor: not-allowed; opacity: .5; }
    .sp-adm-rt-sep { width: 1px; align-self: stretch; background: var(--sp-admin-border, #ECE9F5); margin: 2px 2px; }
    .sp-adm-rt-uploading { font-size: 12px; color: var(--sp-admin-text-muted, #8B85A0); padding-left: 4px; }
    .sp-adm-rt-error { margin: 4px 8px; font-size: 12px; color: #ef4444; }
    .sp-adm-rt-hidden-input { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }

    /* TipTap/ProseMirror injects editor content via direct DOM manipulation, bypassing Angular's
       template compiler — Angular's emulated view encapsulation never stamps those elements with
       its scoping attribute, so scoped selectors can't reach them. ::ng-deep pierces that for the
       editor content subtree specifically (not used anywhere else in this component). */
    .sp-adm-richtext-content { padding: 10px 12px; font-size: 13px; color: var(--sp-admin-text, #0F172A); min-height: 120px; }
    .sp-adm-richtext--compact .sp-adm-richtext-content { min-height: 56px; padding: 8px 10px; font-size: 12.5px; }
    .sp-adm-richtext-content ::ng-deep .ProseMirror { outline: none; min-height: inherit; }
    .sp-adm-richtext-content ::ng-deep p { margin: 0 0 8px; }
    .sp-adm-richtext-content ::ng-deep p:last-child { margin-bottom: 0; }
    .sp-adm-richtext-content ::ng-deep ul, .sp-adm-richtext-content ::ng-deep ol { margin: 0 0 8px; padding-left: 20px; }
    .sp-adm-richtext-content ::ng-deep img { max-width: 100%; border-radius: 6px; margin: 6px 0; }
    .sp-adm-richtext-content ::ng-deep audio, .sp-adm-richtext-content ::ng-deep video { max-width: 100%; margin: 6px 0; }
    .sp-adm-richtext-content ::ng-deep a { color: var(--sp-admin-primary, #5B4BE8); }
  `],
})
export class SpAdminRichTextComponent implements ControlValueAccessor, AfterViewInit, OnDestroy {
  @Input() compact = false;
  @Input() placeholder = '';
  @Input() uploadMedia?: (file: File) => Observable<SpAdminRichTextUploadResult>;

  @ViewChild('editorHost', { static: true }) private editorHost!: ElementRef<HTMLDivElement>;

  private editor: Editor | null = null;
  private pendingValue = '';
  uploading = false;
  uploadError = '';

  private _disabled = false;
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  get isDisabled(): boolean { return this._disabled; }

  ngAfterViewInit(): void {
    this.editor = new Editor({
      element: this.editorHost.nativeElement,
      extensions: [
        StarterKit.configure({ link: { openOnClick: false } }),
        AtomImage, AudioEmbed, VideoEmbed,
      ],
      content: this.pendingValue,
      editable: !this._disabled,
      onUpdate: ({ editor }) => {
        const html = editor.getHTML();
        this.onChange(html);
      },
      onBlur: () => this.onTouched(),
    });
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
  }

  writeValue(value: string): void {
    this.pendingValue = value ?? '';
    if (this.editor && this.editor.getHTML() !== this.pendingValue) {
      this.editor.commands.setContent(this.pendingValue, { emitUpdate: false });
    }
  }

  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }

  setDisabledState(isDisabled: boolean): void {
    this._disabled = isDisabled;
    this.editor?.setEditable(!isDisabled);
  }

  isActive(name: string): boolean { return this.editor?.isActive(name) ?? false; }
  toggleBold(): void { this.editor?.chain().focus().toggleBold().run(); }
  toggleItalic(): void { this.editor?.chain().focus().toggleItalic().run(); }
  toggleBulletList(): void { this.editor?.chain().focus().toggleBulletList().run(); }
  toggleOrderedList(): void { this.editor?.chain().focus().toggleOrderedList().run(); }

  toggleLink(): void {
    if (!this.editor) return;
    if (this.editor.isActive('link')) {
      this.editor.chain().focus().unsetLink().run();
      return;
    }
    // eslint-disable-next-line no-alert
    const url = window.prompt('Link URL');
    if (url) this.editor.chain().focus().setLink({ href: url }).run();
  }

  onFileSelected(event: Event, kind: 'image' | 'audio' | 'video'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (!file || !this.uploadMedia || !this.editor) return;

    this.uploading = true;
    this.uploadError = '';
    this.uploadMedia(file).subscribe({
      next: result => {
        this.uploading = false;
        if (!this.editor) return;
        if (kind === 'image') this.editor.chain().focus().setImage({ src: result.url }).run();
        else if (kind === 'audio') this.editor.chain().focus().setAudio({ src: result.url }).run();
        else this.editor.chain().focus().setVideo({ src: result.url }).run();
      },
      error: err => {
        this.uploading = false;
        this.uploadError = err?.error?.error ?? 'Upload failed.';
      },
    });
  }
}

import { Component, Input, booleanAttribute } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { SpAdminIconComponent } from '../icon/sp-admin-icon.component';
import { SpAdminSlideOverComponent } from '../slide-over/sp-admin-slide-over.component';
import {
  SpAdminButtonComponent,
  SpAdminButtonVariant,
  SpAdminButtonAppearance,
  SpAdminButtonSize,
} from '../button/sp-admin-button.component';
import { AdminHelpContentService } from '../../../../core/services/admin-help-content.service';

/**
 * Drop-in "i" icon: click opens a slide-over with the HTML help copy registered under `key` in
 * the backend's static help-content map (see LinguaCoach.Api/HelpContent/help-content.json).
 * Self-contained — each usage owns its own slide-over, no shared open-state to wire up.
 *
 * Standalone usage (floating circular icon, any context):
 *   <sp-admin-help-icon key="admin.skillGraph.sweepUntaggedModules" title="Sweep untagged Modules" />
 *
 * Attached usage (Flowbite-style joined segment at the end of a button): set `attached` and
 * match `variant`/`appearance`/`size` to the adjacent <sp-admin-button> so the seam lines up.
 * Defaults to variant="primary" appearance="solid" — the common case (a solid primary action
 * button) — override both when the adjacent button uses a different variant/appearance.
 *   <div class="sp-admin-attached-group">
 *     <sp-admin-button variant="primary" size="sm" groupPosition="start" ...>Sweep</sp-admin-button>
 *     <sp-admin-help-icon attached variant="primary" size="sm" key="..." title="..." />
 *   </div>
 */
@Component({
  selector: 'sp-admin-help-icon',
  standalone: true,
  imports: [CommonModule, SpAdminIconComponent, SpAdminSlideOverComponent, SpAdminButtonComponent],
  template: `
    @if (attached) {
      <sp-admin-button
        [variant]="variant"
        [appearance]="appearance"
        [size]="size"
        [iconOnly]="true"
        groupPosition="end"
        [attr.aria-label]="'Help: ' + (title || key)"
        (clicked)="open()"
      >
        <sp-admin-icon name="info" [size]="size === 'lg' ? 'sm' : 'xs'" tone="inherit" />
      </sp-admin-button>
    } @else {
      <button
        type="button"
        class="sp-admin-help-icon-btn"
        (click)="open()"
        [attr.aria-label]="'Help: ' + (title || key)"
      >
        <sp-admin-icon name="info" size="sm" tone="muted" />
      </button>
    }

    <sp-admin-slide-over
      [open]="isOpen"
      [title]="title || 'Help'"
      [loading]="loading"
      [error]="error"
      size="sm"
      (closed)="close()"
    >
      <div class="sp-admin-help-icon-body" [innerHTML]="html"></div>
    </sp-admin-slide-over>
  `,
  styles: [`
    .sp-admin-help-icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 20px;
      height: 20px;
      padding: 0;
      border: none;
      background: transparent;
      color: var(--sp-admin-text-muted,#8B85A0);
      cursor: pointer;
      border-radius: 50%;
      vertical-align: middle;
    }
    .sp-admin-help-icon-btn:hover {
      color: var(--sp-admin-primary,#5B4BE8);
      background: var(--sp-admin-surface-subtle,#FBFAFE);
    }
    .sp-admin-help-icon-btn:focus-visible {
      outline: 2px solid var(--sp-admin-primary,#5B4BE8);
      outline-offset: 2px;
    }
    .sp-admin-help-icon-body {
      font-size: 13px;
      line-height: 1.6;
      color: var(--sp-admin-text,#0F172A);
    }
    .sp-admin-help-icon-body :first-child {
      margin-top: 0;
    }
    .sp-admin-help-icon-body :last-child {
      margin-bottom: 0;
    }
  `],
})
export class SpAdminHelpIconComponent {
  @Input({ required: true }) key!: string;
  @Input() title = '';

  /** Renders as a joined sp-admin-button segment (icon-only, groupPosition="end") instead of a floating circular icon. */
  @Input({ transform: booleanAttribute }) attached = false;
  /** Only used when `attached` — match the adjacent button's variant so the seam is a single continuous color. */
  @Input() variant: SpAdminButtonVariant = 'primary';
  /** Only used when `attached` — match the adjacent button's appearance. */
  @Input() appearance: SpAdminButtonAppearance = 'solid';
  /** Only used when `attached` — match the adjacent button's size so heights line up. */
  @Input() size: SpAdminButtonSize = 'sm';

  isOpen = false;
  loading = false;
  error = '';
  html: SafeHtml = '';

  constructor(
    private helpContent: AdminHelpContentService,
    private sanitizer: DomSanitizer,
  ) {}

  open(): void {
    this.isOpen = true;
    this.loading = true;
    this.error = '';
    this.helpContent.get(this.key).subscribe({
      next: content => {
        this.loading = false;
        if (!content) {
          this.error = `No help content registered for "${this.key}".`;
          return;
        }
        this.html = this.sanitizer.bypassSecurityTrustHtml(content);
      },
      error: () => {
        this.loading = false;
        this.error = 'Could not load help content.';
      },
    });
  }

  close(): void {
    this.isOpen = false;
  }
}

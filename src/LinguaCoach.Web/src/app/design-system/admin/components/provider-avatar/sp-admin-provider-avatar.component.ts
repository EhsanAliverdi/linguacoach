import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminIconComponent, SpAdminIconName, SpAdminIconSize, SpAdminIconTone } from '../icon/sp-admin-icon.component';

export type SpAdminProviderAvatarSize = 'sm' | 'md';

const PROVIDER_ICON: Record<string, SpAdminIconName> = {
  openai:    'radio',
  anthropic: 'alert-triangle',
  gemini:    'cpu-chip',
  qwen:      'zap',
  fake:      'zap',
};

const PROVIDER_TONE: Record<string, SpAdminIconTone> = {
  openai:    'primary',
  anthropic: 'primary',
  gemini:    'primary',
  qwen:      'primary',
  fake:      'muted',
};

@Component({
  selector: 'sp-admin-provider-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, SpAdminIconComponent],
  template: `
    <div class="sp-adm-pav" [class.sp-adm-pav-sm]="size === 'sm'" [attr.aria-label]="ariaLabel || provider">
      @if (iconName) {
        <sp-admin-icon [name]="iconName" [size]="iconSize" [tone]="iconTone" />
      } @else {
        <span aria-hidden="true">{{ initial }}</span>
      }
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .sp-adm-pav {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border-radius: 10px;
      background: var(--sp-admin-primary-bg, #EDEBFF);
      flex-shrink: 0;
      font-size: 13px;
      font-weight: 800;
      color: var(--sp-admin-primary, #5B4BE8);
    }
    .sp-adm-pav-sm {
      width: 28px;
      height: 28px;
      border-radius: 7px;
    }
  `],
})
export class SpAdminProviderAvatarComponent {
  @Input({ required: true }) provider!: string;
  @Input() size: SpAdminProviderAvatarSize = 'md';
  @Input() ariaLabel = '';

  get key(): string { return this.provider.toLowerCase().trim(); }
  get iconName(): SpAdminIconName | null { return PROVIDER_ICON[this.key] ?? null; }
  get iconSize(): SpAdminIconSize { return this.size === 'sm' ? 'sm' : 'md'; }
  get iconTone(): SpAdminIconTone { return PROVIDER_TONE[this.key] ?? 'primary'; }
  get initial(): string { return this.provider.charAt(0).toUpperCase(); }
}

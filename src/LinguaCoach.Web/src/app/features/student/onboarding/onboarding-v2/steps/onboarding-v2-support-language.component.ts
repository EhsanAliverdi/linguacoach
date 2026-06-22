import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OnboardingV2Step, OnboardingV2Option } from '../../../../../core/models/onboarding-v2.models';

@Component({
  selector: 'app-onboarding-v2-support-language',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="sp-card" data-testid="step-support-language">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>
      <div class="space-y-2 mb-6">
        <button
          *ngFor="let opt of step.options"
          class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
          [class.border-blue-500]="selectedKey === opt.key"
          [class.bg-blue-50]="selectedKey === opt.key"
          [class.border-slate-200]="selectedKey !== opt.key"
          (click)="select(opt)"
          [attr.data-testid]="'lang-option-' + opt.key"
        >
          {{ opt.label }}
        </button>
      </div>
      <button class="sp-btn-primary w-full" [disabled]="!selectedKey" (click)="submit()">Continue</button>
    </div>
  `,
})
export class OnboardingV2SupportLanguageComponent {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  selectedKey: string | null = null;

  select(opt: OnboardingV2Option): void {
    this.selectedKey = opt.key;
  }

  submit(): void {
    if (!this.selectedKey) return;
    const langCode = this.selectedKey === 'none' ? null : this.selectedKey;
    const langName = this.step.options?.find(o => o.key === this.selectedKey)?.label ?? null;
    this.submitted.emit(JSON.stringify({
      languageCode: langCode,
      languageName: langCode ? langName : null,
      translationHelp: langCode ? 'WhenAsked' : 'Never',
    }));
  }
}




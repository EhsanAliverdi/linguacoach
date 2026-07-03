import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step, OnboardingV2Option } from '../../../../../core/models/onboarding-v2.models';
import { QuestionRendererComponent } from '../../../../../shared/question/question-renderer.component';
import { QuestionAnswerItem, SingleChoiceQuestion } from '../../../../../shared/question/question-content.models';

/**
 * Renders via the shared QuestionRendererComponent when the step's Content (Unified Question-
 * Schema Phase 5/6) is present, falling back to the legacy button list otherwise. Either way,
 * the submitted wire format is unchanged: {"key": "..."} — OnboardingV2StepHandler needs no changes.
 */
@Component({
  selector: 'app-onboarding-v2-single-choice',
  standalone: true,
  imports: [CommonModule, QuestionRendererComponent],
  template: `
    <div class="sp-card" data-testid="step-single-choice">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>

      <div class="mb-6" *ngIf="step.content; else legacy">
        <app-question-renderer [content]="$any(step.content)" [(answers)]="answers" />
      </div>
      <ng-template #legacy>
        <div class="space-y-2 mb-6">
          <button
            *ngFor="let opt of step.options"
            class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
            [class.border-blue-500]="selectedKey === opt.key"
            [class.bg-blue-50]="selectedKey === opt.key"
            [class.border-slate-200]="selectedKey !== opt.key"
            (click)="select(opt)"
            [attr.data-testid]="'choice-option-' + opt.key"
          >
            {{ opt.label }}
          </button>
        </div>
      </ng-template>

      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <button class="sp-btn-primary w-full" [disabled]="!canSubmit()" (click)="submit()">Continue</button>
    </div>
  `,
})
export class OnboardingV2SingleChoiceComponent implements OnChanges {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  selectedKey: string | null = null;
  error: string | null = null;
  answers = signal<QuestionAnswerItem[]>([]);

  // If a future flow uses two SingleChoice steps back to back, this component instance is
  // reused — reset state on every step change or the second step inherits the first's answer.
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['step'] && !changes['step'].firstChange) {
      this.selectedKey = null;
      this.error = null;
      this.answers.set([]);
    }
  }

  select(opt: OnboardingV2Option): void {
    this.selectedKey = opt.key;
    this.error = null;
  }

  canSubmit(): boolean {
    return this.step.content ? this.selectedContentKey() !== null : !!this.selectedKey;
  }

  private selectedContentKey(): string | null {
    const content = this.step.content as SingleChoiceQuestion | undefined;
    if (!content) return null;
    return this.answers().find(a => a.questionId === content.id)?.values[0] ?? null;
  }

  submit(): void {
    const key = this.step.content ? this.selectedContentKey() : this.selectedKey;
    if (!key) {
      this.error = 'Please select an option.';
      return;
    }
    this.submitted.emit(JSON.stringify({ key }));
  }
}

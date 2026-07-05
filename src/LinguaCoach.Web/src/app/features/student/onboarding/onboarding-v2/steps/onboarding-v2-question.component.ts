import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';
import { QuestionRendererComponent } from '../../../../../shared/question/question-renderer.component';
import { QuestionAnswerItem, flattenLeafQuestions } from '../../../../../shared/question/question-content.models';

/**
 * Unified Question-Schema Phase 6b — the single generic step component for every onboarding
 * question (SingleChoice/MultipleChoice/FreeText), replacing 11 of the former 13 one-off step
 * components. Renders via the shared QuestionRendererComponent and submits the shared
 * QuestionAnswer wire format ({"answers":[{"questionId","values"}]}). Welcome/Summary remain
 * dedicated components — they're not questions at all, just intro/outro screens.
 */
@Component({
  selector: 'app-onboarding-v2-question',
  standalone: true,
  imports: [CommonModule, QuestionRendererComponent],
  template: `
    <div class="sp-card" data-testid="step-question">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-6" *ngIf="step.description">{{ step.description }}</p>

      <div class="mb-6" *ngIf="step.content">
        <app-question-renderer [content]="$any(step.content)" [(answers)]="answers" />
      </div>

      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <div class="flex gap-3 mt-4">
        <button class="sp-btn-primary flex-1" [disabled]="!canSubmit()" (click)="submit()" data-testid="question-continue">
          Continue
        </button>
        <button *ngIf="allowSkip()" class="sp-btn-ghost flex-1" (click)="skip()" data-testid="question-skip">
          Skip for now
        </button>
      </div>
    </div>
  `,
})
export class OnboardingV2QuestionStepComponent implements OnChanges {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  answers = signal<QuestionAnswerItem[]>([]);
  error: string | null = null;

  // Plain method, not computed(): allowSkip derives from a regular @Input (this.step), which
  // Angular signals don't track — a computed() here would cache its first-ever result and
  // never update as steps change, since it has no signal dependency to invalidate it.
  allowSkip(): boolean {
    return this.step?.requirementType === 'AdminConfigured';
  }

  // Two consecutive steps of the same shape reuse this same component instance — reset state on
  // every step change or the next step would inherit the previous one's answer.
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['step'] && !changes['step'].firstChange) {
      this.answers.set([]);
      this.error = null;
    }
  }

  canSubmit(): boolean {
    if (!this.step.content) return false;
    const leaves = flattenLeafQuestions(this.step.content);
    return leaves.every(leaf => {
      const values = this.answers().find(a => a.questionId === leaf.id)?.values ?? [];
      return values.length > 0 && values.every(v => v.trim().length > 0);
    });
  }

  submit(): void {
    if (!this.canSubmit()) {
      this.error = 'Please answer before continuing.';
      return;
    }
    this.error = null;
    this.submitted.emit(JSON.stringify({ answers: this.answers() }));
  }

  skip(): void {
    this.error = null;
    this.submitted.emit('{}');
  }
}

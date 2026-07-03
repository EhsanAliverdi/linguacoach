import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';
import { QuestionRendererComponent } from '../../../../../shared/question/question-renderer.component';
import { MultipleChoiceQuestion, QuestionAnswerItem } from '../../../../../shared/question/question-content.models';

/** Renders via the shared QuestionRendererComponent when Content is present, falling back to
 * the legacy button list otherwise. Submitted wire format unchanged: {"keys": [...]}. */
@Component({
  selector: 'app-onboarding-v2-multiple-choice',
  standalone: true,
  imports: [CommonModule, QuestionRendererComponent],
  template: `
    <div class="sp-card" data-testid="step-multiple-choice">
      <h2 class="sp-heading-2 mb-2">{{ step.title }}</h2>
      <p class="text-slate-600 mb-4" *ngIf="step.description">{{ step.description }}</p>
      <p class="text-xs text-slate-500 mb-4" *ngIf="maxSelections < 100">Select up to {{ maxSelections }}</p>

      <div class="mb-6" *ngIf="step.content; else legacy">
        <app-question-renderer [content]="$any(step.content)" [(answers)]="answers" />
      </div>
      <ng-template #legacy>
        <div class="space-y-2 mb-6">
          <button
            *ngFor="let opt of step.options"
            class="w-full text-left px-4 py-3 rounded-lg border transition-colors"
            [class.border-blue-500]="selectedKeys.has(opt.key)"
            [class.bg-blue-50]="selectedKeys.has(opt.key)"
            [class.border-slate-200]="!selectedKeys.has(opt.key)"
            (click)="toggle(opt.key)"
            [attr.data-testid]="'multi-option-' + opt.key"
          >
            {{ opt.label }}
          </button>
        </div>
      </ng-template>

      <p *ngIf="error" class="text-red-600 text-sm mb-2">{{ error }}</p>
      <button class="sp-btn-primary w-full" (click)="submit()">Continue</button>
    </div>
  `,
})
export class OnboardingV2MultipleChoiceComponent implements OnChanges {
  @Input() step!: OnboardingV2Step;
  @Output() submitted = new EventEmitter<string>();

  selectedKeys = new Set<string>();
  error: string | null = null;
  answers = signal<QuestionAnswerItem[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['step'] && !changes['step'].firstChange) {
      this.selectedKeys = new Set<string>();
      this.error = null;
      this.answers.set([]);
    }
  }

  get maxSelections(): number {
    return this.step.validationMetadata?.maxSelections ?? 100;
  }

  toggle(key: string): void {
    if (this.selectedKeys.has(key)) {
      this.selectedKeys.delete(key);
    } else if (this.selectedKeys.size < this.maxSelections) {
      this.selectedKeys.add(key);
    }
    this.error = null;
  }

  private selectedContentKeys(): string[] {
    const content = this.step.content as MultipleChoiceQuestion | undefined;
    if (!content) return [];
    return this.answers().find(a => a.questionId === content.id)?.values ?? [];
  }

  submit(): void {
    const keys = this.step.content ? this.selectedContentKeys() : Array.from(this.selectedKeys);
    if (keys.length === 0) {
      this.error = 'Please select at least one option.';
      return;
    }
    this.error = null;
    this.submitted.emit(JSON.stringify({ keys }));
  }
}

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PlacementService } from '../../core/services/placement.service';
import {
  PlacementSection, PlacementResult, PlacementAnswerInput,
} from '../../core/models/placement.models';

type PageState = 'loading' | 'intro' | 'section' | 'evaluating' | 'result' | 'error';

@Component({
  selector: 'app-placement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './placement.component.html',
})
export class PlacementComponent implements OnInit {
  state = signal<PageState>('loading');
  error = signal('');
  submitting = signal(false);

  section = signal<PlacementSection | null>(null);
  currentOrder = signal(1);
  totalSections = signal(6);
  result = signal<PlacementResult | null>(null);

  // Working answers for the current section, keyed by questionKey.
  answers = signal<Record<string, string>>({});

  readonly ratingScale = [1, 2, 3, 4, 5];

  progressPercent = computed(() => {
    const total = this.totalSections();
    if (total === 0) return 0;
    return Math.round(((this.currentOrder() - 1) / total) * 100);
  });

  isLastSection = computed(() => this.currentOrder() >= this.totalSections());

  constructor(private placement: PlacementService, private router: Router) {}

  ngOnInit(): void {
    this.placement.getStatus().subscribe({
      next: status => {
        this.totalSections.set(status.totalSections);
        this.currentOrder.set(status.currentSectionOrder);
        if (status.isCompleted) {
          this.loadResult();
        } else if (status.status === 'NotStarted') {
          this.state.set('intro');
        } else {
          this.loadCurrentSection();
        }
      },
      error: () => { this.error.set('Could not load your placement.'); this.state.set('error'); },
    });
  }

  begin(): void {
    this.submitting.set(true);
    this.placement.start().subscribe({
      next: status => {
        this.currentOrder.set(status.currentSectionOrder);
        this.totalSections.set(status.totalSections);
        this.submitting.set(false);
        this.loadCurrentSection();
      },
      error: () => { this.submitting.set(false); this.error.set('Could not start placement.'); this.state.set('error'); },
    });
  }

  private loadCurrentSection(): void {
    this.state.set('loading');
    this.placement.getCurrent().subscribe({
      next: cur => {
        if (cur.isCompleted) { this.loadResult(); return; }
        this.section.set(cur.section);
        this.currentOrder.set(cur.currentSectionOrder);
        this.totalSections.set(cur.totalSections);
        this.answers.set({});
        this.state.set('section');
      },
      error: () => { this.error.set('Could not load the current section.'); this.state.set('error'); },
    });
  }

  setAnswer(questionKey: string, value: string): void {
    this.answers.update(a => ({ ...a, [questionKey]: value }));
  }

  answerValue(questionKey: string): string {
    return this.answers()[questionKey] ?? '';
  }

  canContinue(): boolean {
    const sec = this.section();
    if (!sec) return false;
    // Require an answer for every non-optional question. self_check text fields are optional.
    for (const q of sec.questions) {
      const optional = sec.sectionType === 'self_check' && (q.type === 'text' || q.key === 'self_level');
      if (optional) continue;
      if (!this.answerValue(q.key)) return false;
    }
    return true;
  }

  saveAndContinue(): void {
    const sec = this.section();
    if (!sec || !this.canContinue()) return;

    const payloadAnswers: PlacementAnswerInput[] = sec.questions.map(q => {
      const value = this.answerValue(q.key);
      if (q.type === 'choice' || q.type === 'rating') {
        return { questionKey: q.key, selectedOption: value || null, responseText: null };
      }
      return { questionKey: q.key, responseText: value || null, selectedOption: null };
    });

    this.submitting.set(true);
    this.placement.saveAnswers({ sectionKey: sec.key, answers: payloadAnswers }).subscribe({
      next: status => {
        this.submitting.set(false);
        this.currentOrder.set(status.currentSectionOrder);
        if (sec.order >= status.totalSections) {
          this.evaluate();
        } else {
          this.loadCurrentSection();
        }
      },
      error: err => {
        this.submitting.set(false);
        this.error.set(err.error?.error ?? 'Could not save your answers.');
      },
    });
  }

  private evaluate(): void {
    this.state.set('evaluating');
    this.placement.complete().subscribe({
      next: res => { this.result.set(res); this.state.set('result'); },
      error: () => { this.error.set('Could not evaluate your placement.'); this.state.set('error'); },
    });
  }

  private loadResult(): void {
    this.placement.getResult().subscribe({
      next: res => { this.result.set(res); this.state.set('result'); },
      error: () => { this.error.set('Could not load your result.'); this.state.set('error'); },
    });
  }

  continueToCourse(): void {
    this.router.navigate(['/dashboard']);
  }

  retry(): void {
    this.error.set('');
    this.ngOnInit();
  }
}

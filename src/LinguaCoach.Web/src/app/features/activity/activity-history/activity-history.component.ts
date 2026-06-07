import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { HistoryService } from '../../../core/services/history.service';
import { ActivityAttemptHistory, AttemptDetail, AttemptChange } from '../../../core/models/history.models';

@Component({
  selector: 'app-activity-history',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './activity-history.component.html',
})
export class ActivityHistoryComponent implements OnInit {
  history = signal<ActivityAttemptHistory | null>(null);
  loading = signal(true);
  error = signal('');
  selectedAttemptIndex = signal(0);
  showNativeExplanation = signal(false);

  constructor(
    private route: ActivatedRoute,
    private historySvc: HistoryService,
  ) {}

  ngOnInit(): void {
    const activityId = this.route.snapshot.paramMap.get('activityId');
    if (!activityId) { this.error.set('Activity not found.'); this.loading.set(false); return; }

    this.historySvc.getActivityAttempts(activityId).subscribe({
      next: h => { this.history.set(h); this.selectedAttemptIndex.set(h.attempts.length - 1); this.loading.set(false); },
      error: err => { this.error.set(err.error?.error ?? 'Could not load history.'); this.loading.set(false); },
    });
  }

  get selectedAttempt(): AttemptDetail | null {
    const h = this.history();
    if (!h || h.attempts.length === 0) return null;
    const idx = this.selectedAttemptIndex();
    return h.attempts[Math.min(idx, h.attempts.length - 1)];
  }

  selectAttempt(index: number): void {
    this.selectedAttemptIndex.set(index);
    this.showNativeExplanation.set(false);
  }

  isVocabPractice(): boolean {
    return this.history()?.activityType === 'vocabularyPractice';
  }

  scoreColour(score: number | null): string {
    if (score === null) return 'var(--sp-faint)';
    if (score >= 85) return 'var(--sp-success)';
    if (score >= 70) return 'var(--sp-vocabulary)';
    return 'var(--sp-speaking)';
  }

  categoryColour(cat: string | null): string {
    switch (cat) {
      case 'grammar': return 'var(--sp-writing)';
      case 'vocabulary': return 'var(--sp-vocabulary)';
      case 'tone': return 'var(--sp-assessment)';
      case 'clarity': return 'var(--sp-speaking)';
      default: return 'var(--sp-muted)';
    }
  }

  categoryLabel(cat: string | null): string {
    switch (cat) {
      case 'grammar': return 'Grammar';
      case 'vocabulary': return 'Vocabulary';
      case 'tone': return 'Tone';
      case 'clarity': return 'Clarity';
      case 'structure': return 'Structure';
      case 'punctuation': return 'Punctuation';
      default: return cat ?? 'Feedback';
    }
  }

  scoreImprovement(attempt: AttemptDetail): string {
    const h = this.history();
    if (!h || attempt.attemptNumber <= 1) return '';
    const prev = h.attempts[attempt.attemptNumber - 2];
    if (!prev.score || !attempt.score) return '';
    const diff = Math.round(attempt.score - prev.score);
    if (diff > 0) return `+${diff} from attempt ${prev.attemptNumber}`;
    if (diff < 0) return `${diff} from attempt ${prev.attemptNumber}`;
    return `same score as attempt ${prev.attemptNumber}`;
  }

  improveAgainUrl(activityId: string): string {
    return `/activity?resume=${activityId}`;
  }
}

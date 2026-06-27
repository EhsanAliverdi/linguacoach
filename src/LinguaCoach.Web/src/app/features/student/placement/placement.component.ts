import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PlacementService } from '../../../core/services/placement.service';
import {
  AdaptivePlacementSummary,
  AdaptivePlacementNextItem,
  PlacementConfig,
} from '../../../core/models/placement.models';

export type PlacementPageState =
  | 'loading'
  | 'welcome'
  | 'question'
  | 'submitting'
  | 'completing'
  | 'done'
  | 'error';

interface ParsedChoice {
  letter: string;
  text: string;
}

@Component({
  selector: 'app-placement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './placement.component.html',
})
export class PlacementComponent implements OnInit {
  state = signal<PlacementPageState>('loading');
  error = signal('');

  assessment = signal<AdaptivePlacementSummary | null>(null);
  currentItem = signal<AdaptivePlacementNextItem | null>(null);
  config = signal<PlacementConfig | null>(null);

  selectedAnswer = signal('');
  gapFillAnswer = signal('');
  private itemStartTime = 0;

  questionText = computed(() => this.parseQuestionText(this.currentItem()?.prompt ?? ''));
  choices = computed(() => this.parseChoices(this.currentItem()?.prompt ?? ''));

  progressPercent = computed(() => {
    const item = this.currentItem();
    if (!item) return 0;
    const answered = item.answeredCount;
    const remaining = item.estimatedRemainingItems;
    const total = answered + remaining + 1;
    if (total <= 0) return 0;
    return Math.round((answered / total) * 100);
  });

  questionNumber = computed(() => (this.currentItem()?.answeredCount ?? 0) + 1);

  canSubmit = computed(() => {
    const item = this.currentItem();
    if (!item) return false;
    if (item.itemType === 'multiple_choice') return !!this.selectedAnswer();
    return !!this.gapFillAnswer().trim();
  });

  constructor(private placement: PlacementService, private router: Router) {}

  ngOnInit(): void {
    // Load config and current assessment in parallel
    this.placement.getPlacementConfig().subscribe({
      next: cfg => this.config.set(cfg),
      error: () => { /* non-fatal — config stays null, defaults apply */ },
    });

    this.placement.getAdaptiveCurrent().subscribe({
      next: result => {
        if (!result || !result.hasPlacement) {
          // No assessment yet — show welcome
          this.state.set('welcome');
          return;
        }
        if (result.status === 'Completed') {
          this.assessment.set(result);
          this.state.set('done');
        } else if (result.status === 'InProgress') {
          this.assessment.set(result);
          this.loadNextItem(result.assessmentId);
        } else {
          // Abandoned / Expired / other — allow starting fresh
          this.state.set('welcome');
        }
      },
      error: () => this.state.set('welcome'),
    });
  }

  begin(): void {
    this.state.set('loading');
    const cfg = this.config();
    const start$ = cfg?.autoStartPlacement
      ? this.placement.resumeAdaptive()
      : this.placement.startAdaptive();

    start$.subscribe({
      next: result => {
        this.assessment.set(result);
        if (result.status === 'Completed') {
          this.state.set('done');
        } else {
          this.loadNextItem(result.assessmentId);
        }
      },
      error: err => {
        this.error.set(err?.error?.error ?? 'Could not start your placement. Please try again.');
        this.state.set('error');
      },
    });
  }

  private loadNextItem(assessmentId: string): void {
    this.placement.getAdaptiveNextItem(assessmentId).subscribe({
      next: item => {
        if (!item) {
          // No more items — trigger completion
          this.triggerCompletion(assessmentId);
        } else {
          this.currentItem.set(item);
          this.selectedAnswer.set('');
          this.gapFillAnswer.set('');
          this.itemStartTime = Date.now();
          this.state.set('question');
        }
      },
      error: () => {
        this.error.set('Could not load the next question.');
        this.state.set('error');
      },
    });
  }

  submitAnswer(): void {
    const item = this.currentItem();
    const assessment = this.assessment();
    if (!item || !assessment || !this.canSubmit() || this.state() === 'submitting') return;

    const response = item.itemType === 'multiple_choice'
      ? this.selectedAnswer()
      : this.gapFillAnswer().trim();
    const durationSeconds = Math.max(1, Math.round((Date.now() - this.itemStartTime) / 1000));

    this.state.set('submitting');
    this.placement.respondToItem({
      assessmentId: assessment.assessmentId,
      itemId: item.itemId,
      response,
      durationSeconds,
    }).subscribe({
      next: result => {
        if (result.assessmentComplete) {
          if (result.summary) {
            this.assessment.set(result.summary);
            this.state.set('done');
          } else {
            this.triggerCompletion(assessment.assessmentId);
          }
        } else if (result.nextItem) {
          this.currentItem.set(result.nextItem);
          this.selectedAnswer.set('');
          this.gapFillAnswer.set('');
          this.itemStartTime = Date.now();
          this.state.set('question');
        } else {
          this.triggerCompletion(assessment.assessmentId);
        }
      },
      error: err => {
        this.error.set(err?.error?.error ?? 'Could not submit your answer. Please try again.');
        // Stay on question state so student can retry
        this.state.set('question');
      },
    });
  }

  private triggerCompletion(assessmentId: string): void {
    this.state.set('completing');
    this.placement.completeAdaptive(assessmentId).subscribe({
      next: result => {
        this.assessment.set(result);
        this.state.set('done');
      },
      error: () => {
        this.error.set('Could not finalise your placement. Please try again.');
        this.state.set('error');
      },
    });
  }

  selectChoice(letter: string): void {
    this.selectedAnswer.set(letter);
  }

  continueToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  retry(): void {
    this.error.set('');
    this.ngOnInit();
  }

  // ── Prompt parsing ──────────────────────────────────────────────────────────

  parseQuestionText(prompt: string): string {
    if (!prompt) return '';
    // Remove the choices block — everything before the first (A)
    const choiceIdx = prompt.search(/\(A\)/i);
    if (choiceIdx > 0) return prompt.slice(0, choiceIdx).trim();
    return prompt.trim();
  }

  parseChoices(prompt: string): ParsedChoice[] {
    if (!prompt) return [];
    const matches = [...prompt.matchAll(/\(([A-Z])\)\s*([^(]+?)(?=\s*\([A-Z]\)|$)/gi)];
    return matches.map(m => ({ letter: m[1].toUpperCase(), text: m[2].trim() }));
  }

  // Skill label for display
  skillLabel(skill: string | null | undefined): string {
    if (!skill) return '';
    return skill.charAt(0).toUpperCase() + skill.slice(1);
  }
}

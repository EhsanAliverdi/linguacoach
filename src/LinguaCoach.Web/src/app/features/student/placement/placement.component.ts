import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PlacementService } from '../../../core/services/placement.service';
import { AdaptivePlacementNextItem } from '../../../core/models/placement.models';
import { QuestionRendererComponent } from '../../../shared/question/question-renderer.component';
import { QuestionAnswerItem, flattenLeafQuestions } from '../../../shared/question/question-content.models';

export type PlacementPageState = 'loading' | 'question' | 'submitting' | 'error';

interface ParsedChoice {
  letter: string;
  text: string;
}

/**
 * Runs the adaptive placement engine scoped to a single skill (the placement-cards flow —
 * see PlacementCardsComponent, the actual landing page). Once this skill's card is finished
 * (no more items for it) or the whole assessment completes, navigates back to /placement,
 * which owns all "what's left" / "you're done" UI.
 */
@Component({
  selector: 'app-placement',
  standalone: true,
  imports: [CommonModule, FormsModule, QuestionRendererComponent],
  templateUrl: './placement.component.html',
})
export class PlacementComponent implements OnInit {
  state = signal<PlacementPageState>('loading');
  error = signal('');

  currentItem = signal<AdaptivePlacementNextItem | null>(null);

  selectedAnswer = signal('');
  gapFillAnswer = signal('');
  audioUrl = signal<string | null>(null);
  audioLoading = signal(false);
  answers = signal<QuestionAnswerItem[]>([]);
  private itemStartTime = 0;
  private assessmentId = '';
  private skill = '';

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

    if (item.content) {
      const leaves = flattenLeafQuestions(item.content);
      return leaves.every(leaf => {
        const values = this.answers().find(a => a.questionId === leaf.id)?.values ?? [];
        return values.length > 0 && values.every(v => v.trim().length > 0);
      });
    }

    if (item.itemType === 'multiple_choice') return !!this.selectedAnswer();
    return !!this.gapFillAnswer().trim();
  });

  constructor(
    private placement: PlacementService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.skill = this.route.snapshot.paramMap.get('skill') ?? '';
    if (!this.skill) {
      this.router.navigate(['/placement']);
      return;
    }
    this.load();
  }

  private load(): void {
    this.state.set('loading');
    this.placement.getAdaptiveCurrent().subscribe({
      next: result => {
        if (!result || !result.hasPlacement) {
          this.startThenLoadItem();
          return;
        }
        if (result.status === 'Completed') {
          this.router.navigate(['/placement']);
          return;
        }
        this.assessmentId = result.assessmentId;
        this.loadNextItem();
      },
      error: () => this.startThenLoadItem(),
    });
  }

  private startThenLoadItem(): void {
    this.placement.startAdaptive().subscribe({
      next: result => {
        if (result.status === 'Completed') {
          this.router.navigate(['/placement']);
          return;
        }
        this.assessmentId = result.assessmentId;
        this.loadNextItem();
      },
      error: err => {
        this.error.set(err?.error?.error ?? 'Could not start your placement. Please try again.');
        this.state.set('error');
      },
    });
  }

  private loadNextItem(): void {
    this.placement.getAdaptiveNextItem(this.assessmentId, this.skill).subscribe({
      next: item => {
        if (!item) {
          // No more items for this skill — the card is done, hand back to the cards page.
          this.router.navigate(['/placement']);
        } else {
          this.setCurrentItem(item);
        }
      },
      error: () => {
        this.error.set('Could not load the next question.');
        this.state.set('error');
      },
    });
  }

  private setCurrentItem(item: AdaptivePlacementNextItem): void {
    this.revokeAudioUrl();
    this.currentItem.set(item);
    this.selectedAnswer.set('');
    this.gapFillAnswer.set('');
    this.answers.set([]);
    this.itemStartTime = Date.now();
    this.state.set('question');

    if (item.hasAudio) {
      this.audioLoading.set(true);
      this.placement.getAdaptiveItemAudioBlobUrl(this.assessmentId, item.itemId).subscribe({
        next: url => { this.audioUrl.set(url); this.audioLoading.set(false); },
        error: () => { this.audioUrl.set(null); this.audioLoading.set(false); },
      });
    }
  }

  private revokeAudioUrl(): void {
    const url = this.audioUrl();
    if (url) URL.revokeObjectURL(url);
    this.audioUrl.set(null);
  }

  /** The backend's respond endpoint still scores a single legacy response string per item —
   * true multi-sub-question submission needs a backend change not yet made. Every item today
   * has exactly one leaf sub-question (no admin editor for multi-question groups exists yet —
   * that's Phase 4), so taking the first leaf's first value is lossless for all content that
   * can currently be authored. */
  private buildLegacyResponse(item: AdaptivePlacementNextItem): string {
    if (item.content) {
      const leaves = flattenLeafQuestions(item.content);
      const firstLeaf = leaves[0];
      const values = this.answers().find(a => a.questionId === firstLeaf?.id)?.values ?? [];
      return (values[0] ?? '').trim();
    }

    return item.itemType === 'multiple_choice'
      ? this.selectedAnswer()
      : this.gapFillAnswer().trim();
  }

  submitAnswer(): void {
    const item = this.currentItem();
    if (!item || !this.assessmentId || !this.canSubmit() || this.state() === 'submitting') return;

    const response = this.buildLegacyResponse(item);
    const durationSeconds = Math.max(1, Math.round((Date.now() - this.itemStartTime) / 1000));

    this.state.set('submitting');
    this.placement.respondToItem({
      assessmentId: this.assessmentId,
      itemId: item.itemId,
      response,
      durationSeconds,
      skill: this.skill,
    }).subscribe({
      next: result => {
        if (result.assessmentComplete || !result.nextItem) {
          // Either the whole assessment just finished, or this skill's card is done —
          // either way, the cards page owns what happens next.
          this.router.navigate(['/placement']);
        } else {
          this.setCurrentItem(result.nextItem);
        }
      },
      error: err => {
        this.error.set(err?.error?.error ?? 'Could not submit your answer. Please try again.');
        this.state.set('question');
      },
    });
  }

  selectChoice(letter: string): void {
    this.selectedAnswer.set(letter);
  }

  retry(): void {
    this.error.set('');
    this.load();
  }

  // ── Prompt parsing ──────────────────────────────────────────────────────────

  parseQuestionText(prompt: string): string {
    if (!prompt) return '';
    const choiceIdx = prompt.search(/\(A\)/i);
    if (choiceIdx > 0) return prompt.slice(0, choiceIdx).trim();
    return prompt.trim();
  }

  parseChoices(prompt: string): ParsedChoice[] {
    if (!prompt) return [];
    const matches = [...prompt.matchAll(/\(([A-Z])\)\s*([^(]+?)(?=\s*\([A-Z]\)|$)/gi)];
    return matches.map(m => ({ letter: m[1].toUpperCase(), text: m[2].trim() }));
  }

  skillLabel(skill: string | null | undefined): string {
    if (!skill) return '';
    return skill.charAt(0).toUpperCase() + skill.slice(1);
  }
}

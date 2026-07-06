import { Component, OnInit, ViewChild, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PlacementService } from '../../../core/services/placement.service';
import { AdaptivePlacementNextItem } from '../../../core/models/placement.models';
import { FormioRendererComponent } from '../../../shared/formio/formio-renderer.component';

export type PlacementPageState = 'loading' | 'question' | 'submitting' | 'error';

/**
 * Runs the adaptive placement engine scoped to a single skill (the placement-cards flow —
 * see PlacementCardsComponent, the actual landing page). Once this skill's card is finished
 * (no more items for it) or the whole assessment completes, navigates back to /placement,
 * which owns all "what's left" / "you're done" UI.
 *
 * Form.io migration: each served item now always carries a `formIoSchemaJson` (backend-derived
 * or item-bank-authored) rendered via the shared FormioRendererComponent, replacing the old
 * QuestionRendererComponent/regex-parsed-prompt UI entirely.
 */
@Component({
  selector: 'app-placement',
  standalone: true,
  imports: [CommonModule, FormsModule, FormioRendererComponent],
  templateUrl: './placement.component.html',
})
export class PlacementComponent implements OnInit {
  state = signal<PlacementPageState>('loading');
  error = signal('');

  currentItem = signal<AdaptivePlacementNextItem | null>(null);
  parsedSchema = computed(() => {
    const json = this.currentItem()?.formIoSchemaJson;
    if (!json) return null;
    try {
      return JSON.parse(json);
    } catch {
      return null;
    }
  });

  audioUrl = signal<string | null>(null);
  audioLoading = signal(false);
  private latestSubmissionData: Record<string, any> = {};
  private itemStartTime = 0;
  private assessmentId = '';
  private skill = '';

  @ViewChild(FormioRendererComponent) rendererRef?: FormioRendererComponent;

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

  canSubmit = computed(() => !!this.currentItem() && !!this.parsedSchema() && this.state() !== 'submitting');

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
    this.latestSubmissionData = {};
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

  onFormChange(data: any): void {
    this.latestSubmissionData = data ?? {};
  }

  /** Single value if there's exactly one component key; otherwise (multi-component
   * listening/reading group item) take the first sub-answer — a known pre-existing limitation
   * of the single-string `respond` endpoint, out of scope to fix here. */
  private extractResponse(data: Record<string, any>): string {
    const keys = Object.keys(data ?? {});
    if (keys.length === 0) return '';
    const value = data[keys[0]];
    if (value == null) return '';
    return typeof value === 'string' ? value.trim() : JSON.stringify(value);
  }

  /** Triggers Form.io's own validation/submit pipeline; the actual answer submission happens
   * in onFormSubmit() once Form.io confirms the submission is valid. */
  submitAnswer(): void {
    if (!this.canSubmit()) return;
    this.rendererRef?.submitForm();
  }

  onFormSubmit(data: any): void {
    const item = this.currentItem();
    if (!item || !this.assessmentId || this.state() === 'submitting') return;

    const response = this.extractResponse(data ?? this.latestSubmissionData);
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

  retry(): void {
    this.error.set('');
    this.load();
  }

  skillLabel(skill: string | null | undefined): string {
    if (!skill) return '';
    return skill.charAt(0).toUpperCase() + skill.slice(1);
  }
}

import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { WritingService } from '../../../core/services/writing.service';
import { WritingExerciseDto, WritingFeedbackDto } from '../../../core/models/writing.models';

type PageState = 'loading' | 'learning' | 'exercise' | 'submitting' | 'feedback' | 'error';

@Component({
  selector: 'app-writing-exercise',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './writing-exercise.component.html',
})
export class WritingExerciseComponent implements OnInit {
  state = signal<PageState>('loading');
  exercise = signal<WritingExerciseDto | null>(null);
  feedback = signal<WritingFeedbackDto | null>(null);
  draftText = '';
  errorMessage = signal('');

  private scenarioId = '';

  constructor(
    private writingService: WritingService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.scenarioId = this.route.snapshot.paramMap.get('scenarioId') ?? '';
    if (!this.scenarioId) {
      this.errorMessage.set('No scenario selected.');
      this.state.set('error');
      return;
    }
    this.writingService.getExercise(this.scenarioId).subscribe({
      next: ex => { this.exercise.set(ex); this.state.set('learning'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not load exercise.');
        this.state.set('error');
      },
    });
  }

  startWriting(): void {
    this.state.set('exercise');
  }

  onSubmit(): void {
    if (!this.draftText.trim()) return;
    this.state.set('submitting');
    this.writingService.submitDraft(this.draftText, this.scenarioId).subscribe({
      next: fb => { this.feedback.set(fb); this.state.set('feedback'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Failed to get feedback. Please try again.');
        this.state.set('exercise');
      },
    });
  }

  tryAgain(): void {
    this.feedback.set(null);
    this.draftText = '';
    this.state.set('exercise');
  }

  tryAnotherScenario(): void {
    this.router.navigate(['/writing']);
  }

  backToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  scoreColour(score: number | null): string {
    if (score === null) return 'text-slate-500';
    if (score >= 75) return 'text-green-600';
    if (score >= 50) return 'text-amber-600';
    return 'text-red-600';
  }
}

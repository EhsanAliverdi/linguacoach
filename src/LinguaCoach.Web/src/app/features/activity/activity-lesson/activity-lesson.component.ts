import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ActivityService } from '../../../core/services/activity.service';
import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';

type PageState = 'loading' | 'learning' | 'writing' | 'submitting' | 'feedback' | 'error';

@Component({
  selector: 'app-activity-lesson',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './activity-lesson.component.html',
})
export class ActivityLessonComponent implements OnInit {
  state = signal<PageState>('loading');
  activity = signal<ActivityDto | null>(null);
  feedback = signal<ActivityFeedbackDto | null>(null);
  draftText = '';
  errorMessage = signal('');

  constructor(
    private activityService: ActivityService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.activityService.getNext().subscribe({
      next: a => { this.activity.set(a); this.state.set('learning'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not load activity. Please try again.');
        this.state.set('error');
      },
    });
  }

  startWriting(): void {
    this.state.set('writing');
  }

  onSubmit(): void {
    const a = this.activity();
    if (!this.draftText.trim() || !a) return;
    this.state.set('submitting');
    this.activityService.submitAttempt(a.activityId, this.draftText).subscribe({
      next: fb => { this.feedback.set(fb); this.state.set('feedback'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Failed to get feedback. Please try again.');
        this.state.set('writing');
      },
    });
  }

  tryAgain(): void {
    this.feedback.set(null);
    this.draftText = '';
    this.state.set('writing');
  }

  nextActivity(): void {
    this.state.set('loading');
    this.feedback.set(null);
    this.activity.set(null);
    this.draftText = '';
    this.activityService.getNext().subscribe({
      next: a => { this.activity.set(a); this.state.set('learning'); },
      error: err => {
        this.errorMessage.set(err.error?.error ?? 'Could not load next activity.');
        this.state.set('error');
      },
    });
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

  isAiGenerated(): boolean {
    return this.activity()?.source === 'aiGenerated';
  }
}

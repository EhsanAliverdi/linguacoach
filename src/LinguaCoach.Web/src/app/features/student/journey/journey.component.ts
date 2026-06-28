import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { JourneyService } from '../../../core/services/journey.service';
import { StudentJourney, JourneyObjective } from '../../../core/models/journey.models';

@Component({
  selector: 'app-journey',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './journey.component.html',
})
export class JourneyComponent implements OnInit {
  loading = signal(true);
  error = signal('');
  journey = signal<StudentJourney | null>(null);

  readonly hasPlan = computed(() => {
    const j = this.journey();
    return !!j && j.planStatus !== 'None' && j.totalObjectives > 0;
  });

  readonly hasCurrentObjective = computed(
    () => !!this.journey()?.currentObjective,
  );

  readonly hasCompletedObjectives = computed(
    () => (this.journey()?.completedObjectives?.length ?? 0) > 0,
  );

  readonly hasReviewObjectives = computed(
    () => (this.journey()?.reviewObjectives?.length ?? 0) > 0,
  );

  readonly progressBarWidth = computed(() => {
    const pct = this.journey()?.completionPercentage ?? 0;
    return `${Math.max(0, Math.min(100, pct))}%`;
  });

  constructor(private journeyService: JourneyService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.journeyService.getJourney().subscribe({
      next: j => { this.journey.set(j); this.loading.set(false); },
      error: () => {
        this.error.set('Could not load your learning journey. Please try again.');
        this.loading.set(false);
      },
    });
  }

  statusLabel(status: string): string {
    return ({
      Current:   'In Progress',
      Ready:     'Up Next',
      Upcoming:  'Upcoming',
      Locked:    'Locked',
      Completed: 'Completed',
      Review:    'Review',
      Blocked:   'Blocked',
    } as Record<string, string>)[status] ?? status;
  }

  skillLabel(skill: string): string {
    return ({
      speaking:    'Speaking',
      listening:   'Listening',
      reading:     'Reading',
      writing:     'Writing',
      vocabulary:  'Vocabulary',
      grammar:     'Grammar',
      pronunciation: 'Pronunciation',
    } as Record<string, string>)[skill?.toLowerCase()] ?? skill;
  }

  objectiveDisplayTitle(obj: JourneyObjective): string {
    if (obj.title) return obj.title;
    const skill = this.skillLabel(obj.skill);
    return `${skill} — ${obj.cefrLevel}`;
  }

  completedDateLabel(lastEvaluatedAt: string | null): string {
    if (!lastEvaluatedAt) return '';
    return new Date(lastEvaluatedAt).toLocaleDateString(undefined, {
      day: 'numeric', month: 'short', year: 'numeric',
    });
  }
}

import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SessionService } from '../../core/services/session.service';
import {
  SessionDetailResponse,
  SessionExercise,
  ExerciseKind,
  SessionStatus,
} from '../../core/models/session.models';

type PageState = 'loading' | 'ready' | 'completing' | 'completed' | 'error';

@Component({
  selector: 'app-lesson',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './lesson.component.html',
})
export class LessonComponent implements OnInit {
  session = signal<SessionDetailResponse | null>(null);
  pageState = signal<PageState>('loading');
  errorMessage = signal('');
  activeExerciseIndex = signal(0);
  completingExerciseId = signal<string | null>(null);
  startingSession = signal(false);

  /** Tracks which exercise is currently being prepared (calling /prepare endpoint). */
  preparingExerciseId = signal<string | null>(null);

  /**
   * Local activityId overrides per exercise — populated when /prepare returns in this
   * browser session before the page is refreshed. Keyed by exerciseId.
   */
  localActivityIds = signal<Record<string, string>>({});

  sessionId = '';

  progress = computed(() => {
    const s = this.session();
    if (!s || s.exercises.length === 0) return 0;
    const done = s.exercises.filter(
      e => e.status === 'completed' || e.status === 'skipped'
    ).length;
    return Math.round((done / s.exercises.length) * 100);
  });

  allExercisesDone = computed(() => {
    const s = this.session();
    if (!s || s.exercises.length === 0) return false;
    return s.exercises.every(e => e.status === 'completed' || e.status === 'skipped');
  });

  doneCount = computed(() => {
    const s = this.session();
    if (!s) return 0;
    return s.exercises.filter(e => e.status === 'completed' || e.status === 'skipped').length;
  });

  activeExercise = computed((): SessionExercise | null => {
    const s = this.session();
    if (!s) return null;
    return s.exercises[this.activeExerciseIndex()] ?? null;
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private sessionService: SessionService,
  ) {}

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    if (!this.sessionId) {
      this.router.navigate(['/dashboard']);
      return;
    }
    this.loadSession();
  }

  private loadSession(): void {
    this.pageState.set('loading');
    this.sessionService.getById(this.sessionId).subscribe({
      next: s => {
        this.session.set(s);
        this.pageState.set(s.status === 'completed' ? 'completed' : 'ready');
        // Advance to first incomplete exercise
        const firstIncomplete = s.exercises.findIndex(
          e => e.status !== 'completed' && e.status !== 'skipped'
        );
        this.activeExerciseIndex.set(firstIncomplete >= 0 ? firstIncomplete : 0);
        // Trigger prepare for the active exercise immediately on load
        const active = s.exercises[firstIncomplete >= 0 ? firstIncomplete : 0];
        if (active && active.status !== 'completed' && active.status !== 'skipped') {
          this.prepareIfNeeded(active);
        }
      },
      error: err => {
        this.pageState.set('error');
        this.errorMessage.set(err.error?.error ?? 'Could not load the lesson.');
      },
    });
  }

  /** Returns the resolved activityId for an exercise: server value or local override. */
  resolvedActivityId(exercise: SessionExercise): string | null {
    return exercise.learningActivityId ?? this.localActivityIds()[exercise.exerciseId] ?? null;
  }

  /** Calls /prepare for an exercise if it doesn't yet have an activity and isn't a Review step. */
  prepareIfNeeded(exercise: SessionExercise): void {
    if (exercise.kind === 'review') return;
    if (this.resolvedActivityId(exercise)) return;
    if (this.preparingExerciseId() === exercise.exerciseId) return;

    this.preparingExerciseId.set(exercise.exerciseId);
    this.sessionService.prepareExercise(this.sessionId, exercise.exerciseId).subscribe({
      next: result => {
        this.preparingExerciseId.set(null);
        this.localActivityIds.update(ids => ({
          ...ids,
          [exercise.exerciseId]: result.activityId,
        }));
      },
      error: () => {
        this.preparingExerciseId.set(null);
      },
    });
  }

  selectExercise(index: number): void {
    this.activeExerciseIndex.set(index);
    const s = this.session();
    if (!s) return;
    const exercise = s.exercises[index];
    if (exercise && exercise.status !== 'completed' && exercise.status !== 'skipped') {
      this.prepareIfNeeded(exercise);
    }
  }

  /** URL for the activity page for this exercise. */
  activityUrl(exercise: SessionExercise): string {
    const actId = this.resolvedActivityId(exercise);
    if (!actId) return '/activity';
    return `/activity?activityId=${actId}&returnTo=/lesson/${this.sessionId}`;
  }

  startLesson(): void {
    if (this.startingSession()) return;
    this.startingSession.set(true);
    this.sessionService.start(this.sessionId).subscribe({
      next: result => {
        this.startingSession.set(false);
        const s = this.session();
        if (s) this.session.set({ ...s, status: result.status, startedAtUtc: result.startedAtUtc });
      },
      error: () => this.startingSession.set(false),
    });
  }

  completeExercise(exercise: SessionExercise): void {
    if (this.completingExerciseId()) return;
    this.completingExerciseId.set(exercise.exerciseId);

    const doComplete = () => {
      this.sessionService.completeExercise(this.sessionId, exercise.exerciseId).subscribe({
        next: result => {
          this.completingExerciseId.set(null);
          const s = this.session();
          if (!s) return;
          const exercises = s.exercises.map(e =>
            e.exerciseId === result.exerciseId
              ? { ...e, status: result.status, completedAtUtc: result.completedAtUtc }
              : e
          );
          this.session.set({ ...s, exercises });
          if (result.sessionComplete) {
            this.completeSession();
          } else {
            const next = exercises.findIndex(
              e => e.status !== 'completed' && e.status !== 'skipped'
            );
            if (next >= 0) {
              this.activeExerciseIndex.set(next);
              this.prepareIfNeeded(exercises[next]);
            }
          }
        },
        error: () => this.completingExerciseId.set(null),
      });
    };

    const s = this.session();
    if (s?.status === 'notStarted') {
      this.sessionService.start(this.sessionId).subscribe({
        next: result => {
          if (s) this.session.set({ ...s, status: result.status, startedAtUtc: result.startedAtUtc });
          doComplete();
        },
        error: () => {
          this.completingExerciseId.set(null);
        },
      });
    } else {
      doComplete();
    }
  }

  completeSession(): void {
    this.pageState.set('completing');
    this.sessionService.complete(this.sessionId).subscribe({
      next: result => {
        const s = this.session();
        if (s) this.session.set({ ...s, status: result.status, completedAtUtc: result.completedAtUtc });
        this.pageState.set('completed');
      },
      error: () => {
        this.pageState.set('completed');
      },
    });
  }

  kindLabel(kind: ExerciseKind): string {
    switch (kind) {
      case 'vocabularyWarmup': return 'Vocabulary warm-up';
      case 'contextInput': return 'Context';
      case 'listeningInput': return 'Listening';
      case 'readingInput': return 'Reading';
      case 'writingTask': return 'Writing task';
      case 'speakingTask': return 'Speaking task';
      case 'review': return 'Lesson review';
      default: return kind;
    }
  }

  kindColor(kind: ExerciseKind): string {
    switch (kind) {
      case 'vocabularyWarmup': return 'var(--sp-vocabulary)';
      case 'listeningInput': return 'var(--sp-listening)';
      case 'speakingTask': return 'var(--sp-speaking)';
      case 'writingTask': return 'var(--sp-writing)';
      case 'review': return 'var(--sp-success)';
      default: return 'var(--sp-muted)';
    }
  }

  kindColorSoft(kind: ExerciseKind): string {
    switch (kind) {
      case 'vocabularyWarmup': return 'var(--sp-vocabulary-soft)';
      case 'listeningInput': return 'var(--sp-listening-soft)';
      case 'speakingTask': return 'var(--sp-speaking-soft)';
      case 'writingTask': return 'var(--sp-writing-soft)';
      case 'review': return 'var(--sp-success-soft)';
      default: return 'var(--sp-canvas2)';
    }
  }

  statusLabel(status: SessionStatus): string {
    switch (status) {
      case 'notStarted': return 'Not started';
      case 'inProgress': return 'In progress';
      case 'completed': return 'Completed';
      default: return status;
    }
  }
}

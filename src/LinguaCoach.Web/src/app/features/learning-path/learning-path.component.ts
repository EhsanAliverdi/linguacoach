import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LearningPathService } from '../../core/services/learning-path.service';
import { SessionService } from '../../core/services/session.service';
import { StudentLearningMemory } from '../../core/models/learning-path.models';
import { SessionHistoryItem } from '../../core/models/session.models';

@Component({
  selector: 'app-learning-path',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './learning-path.component.html',
})
export class LearningPathComponent implements OnInit {
  loading = signal(true);
  error = signal('');
  memory = signal<StudentLearningMemory | null>(null);
  memoryLoading = signal(true);

  sessions = signal<SessionHistoryItem[]>([]);
  sessionsLoading = signal(true);
  sessionsError = signal('');
  totalSessions = signal(0);
  currentPage = signal(1);
  readonly pageSize = 10;

  expandedSessionId = signal<string | null>(null);

  hasMemory = computed(() => {
    const m = this.memory();
    return !!m && !!(
      m.journeySummary ||
      m.strongSkills?.length ||
      m.weakSkills?.length ||
      m.recurringMistakes?.length ||
      m.nextRecommendedFocus?.length ||
      m.coveredScenarioCount ||
      m.skillProfile?.length
    );
  });

  weakSkills = computed(() => (this.memory()?.skillProfile ?? []).filter(s => s.isWeak).slice(0, 5));
  strongSkillProfiles = computed(() => (this.memory()?.skillProfile ?? []).filter(s => !s.isWeak).slice(0, 3));

  hasMorePages = computed(() => {
    const loaded = (this.currentPage() - 1) * this.pageSize + this.sessions().length;
    return loaded < this.totalSessions();
  });

  constructor(
    private pathService: LearningPathService,
    private sessionSvc: SessionService,
  ) {}

  ngOnInit(): void {
    this.loadMemory();
    this.loadHistory(1);
  }

  private loadMemory(): void {
    this.memoryLoading.set(true);
    this.pathService.getLearningMemory().subscribe({
      next: m => { this.memory.set(m); this.memoryLoading.set(false); this.loading.set(false); },
      error: () => { this.memory.set(null); this.memoryLoading.set(false); this.loading.set(false); },
    });
  }

  private loadHistory(page: number): void {
    this.sessionsLoading.set(true);
    this.sessionsError.set('');
    this.sessionSvc.getHistory(page, this.pageSize).subscribe({
      next: result => {
        if (page === 1) {
          this.sessions.set(result.sessions);
        } else {
          this.sessions.update(s => [...s, ...result.sessions]);
        }
        this.totalSessions.set(result.totalCount);
        this.currentPage.set(page);
        this.sessionsLoading.set(false);
      },
      error: err => {
        this.sessionsError.set(err.error?.error ?? 'Could not load session history.');
        this.sessionsLoading.set(false);
      },
    });
  }

  loadMore(): void {
    this.loadHistory(this.currentPage() + 1);
  }

  toggleSession(sessionId: string): void {
    this.expandedSessionId.update(id => id === sessionId ? null : sessionId);
  }

  sessionDate(session: SessionHistoryItem): string {
    const d = session.startedAtUtc ?? session.completedAtUtc;
    if (!d) return 'Not started';
    return new Date(d).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  }

  sessionStatusLabel(session: SessionHistoryItem): string {
    if (session.status === 'completed') return 'Completed';
    if (session.status === 'inProgress') return 'In progress';
    return 'Not started';
  }

  sessionAvgScore(session: SessionHistoryItem): number | null {
    const scored = session.exercises.filter(e => e.score !== null);
    if (!scored.length) return null;
    return Math.round(scored.reduce((sum, e) => sum + e.score!, 0) / scored.length);
  }

  exerciseLabel(patternKey: string): string {
    return ({
      phrase_match: 'Phrase Match',
      listen_and_answer: 'Listen & Answer',
      listen_and_gap_fill: 'Listen & Gap Fill',
      writing_response: 'Writing',
      speaking_role_play: 'Speaking Role-play',
      lesson_reflection: 'Lesson Reflection',
      gap_fill_workplace_phrase: 'Gap Fill',
      email_reply: 'Email Reply',
      teams_chat_simulation: 'Chat Simulation',
      spoken_response_from_prompt: 'Spoken Response',
    } as Record<string, string>)[patternKey] ?? patternKey.replace(/_/g, ' ');
  }

  scoreColour(score: number | null): string {
    if (score === null) return 'var(--sp-faint)';
    if (score >= 85) return 'var(--sp-success)';
    if (score >= 70) return 'var(--sp-vocabulary)';
    return 'var(--sp-speaking)';
  }
}

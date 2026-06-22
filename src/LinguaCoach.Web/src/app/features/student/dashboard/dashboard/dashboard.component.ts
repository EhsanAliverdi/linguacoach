import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../../../core/services/dashboard.service';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';
import { DashboardResponse } from '../../../../core/models/dashboard.models';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { StudentLearningMemory } from '../../../../core/models/learning-path.models';
import { PlacementService } from '../../../../core/services/placement.service';
import { PlacementResult } from '../../../../core/models/placement.models';
import { SessionService } from '../../../../core/services/session.service';
import { TodaysSessionResponse } from '../../../../core/models/session.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  data = signal<DashboardResponse | null>(null);
  loading = signal(true);
  error = signal('');
  notice = signal('');
  memory = signal<StudentLearningMemory | null>(null);
  placementResult = signal<PlacementResult | null>(null);
  todaysSession = signal<TodaysSessionResponse | null>(null);
  sessionLoading = signal(false);

  readonly howItWorks = [
    { n: 1, text: 'AI generates a realistic scenario for your goals and level.' },
    { n: 2, text: 'You write or speak your response in a safe, private space.' },
    { n: 3, text: 'Get coaching feedback on grammar, tone, and natural phrasing.' },
  ];

  pathProgress = computed(() => {
    const lp = this.data()?.learningPath;
    if (!lp || lp.totalModules === 0) return 0;
    return Math.round((lp.modulesCompleted / lp.totalModules) * 100);
  });

  moduleProgress = computed(() => {
    const mod = this.data()?.learningPath?.currentModule;
    if (!mod || mod.totalActivities === 0) return 0;
    return Math.round((mod.completedActivities / mod.totalActivities) * 100);
  });

  constructor(
    private dashboardService: DashboardService,
    private learningPathService: LearningPathService,
    private placementService: PlacementService,
    private sessionService: SessionService,
    private authNotice: AuthNoticeService,
  ) {
    this.notice.set(this.authNotice.consume() ?? '');
  }

  activityDots(total: number): number[] {
    return Array.from({ length: total }, (_, i) => i);
  }

  ngOnInit(): void {
    this.dashboardService.getDashboard().subscribe({
      next: d => {
        this.data.set(d);
        this.loading.set(false);
        if (this.hasPlacementResultState(d.lifecycleStage)) {
          this.placementService.getResult().subscribe({
            next: result => this.placementResult.set(result),
            error: () => this.placementResult.set(null),
          });
        }
        if (this.hasLessonAccess(d.lifecycleStage)) {
          this.loadTodaysSession();
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your dashboard.');
      },
    });
    this.learningPathService.getLearningMemory().subscribe({
      next: memory => this.memory.set(memory),
      error: () => this.memory.set(null),
    });
  }

  private loadTodaysSession(): void {
    this.sessionLoading.set(true);
    this.sessionService.getToday().subscribe({
      next: session => {
        this.todaysSession.set(session);
        this.sessionLoading.set(false);
      },
      error: err => {
        this.sessionLoading.set(false);
        this.error.set(err.error?.error ?? 'Could not load today\'s lesson.');
      },
    });
  }

  lessonButtonLabel(): string {
    const s = this.todaysSession();
    if (!s) return 'Start today\'s lesson';
    if (s.status === 'completed') return 'Review today\'s lesson';
    if (s.status === 'inProgress') return 'Resume lesson';
    return 'Start today\'s lesson';
  }

  private hasLessonAccess(stage: string): boolean {
    return stage === 'CourseReady' || stage === 'InLesson' || stage === 'ActiveLearning';
  }

  primaryMemoryFocus(): string | null {
    const memory = this.memory();
    return memory?.nextRecommendedFocus?.[0]
      ?? memory?.weakSkills?.[0]
      ?? memory?.skillProfile?.find(s => s.isWeak)?.skillLabel
      ?? null;
  }

  isPlacementRequired(): boolean {
    return this.data()?.lifecycleStage === 'PlacementRequired';
  }

  isPlacementInProgress(): boolean {
    return this.data()?.lifecycleStage === 'PlacementInProgress';
  }

  isCourseReady(): boolean {
    const stage = this.data()?.lifecycleStage;
    return stage === 'CourseReady' || stage === 'PlacementCompleted';
  }

  showTodaysLesson(): boolean {
    return this.hasLessonAccess(this.data()?.lifecycleStage ?? '');
  }

  private hasPlacementResultState(stage: string): boolean {
    return stage === 'CourseReady' || stage === 'PlacementCompleted';
  }
}


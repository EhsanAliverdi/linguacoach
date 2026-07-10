import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthNoticeService } from '../../../../core/services/auth-notice.service';
import { DashboardSummaryService } from '../../../../core/services/dashboard-summary.service';
import { PlacementService } from '../../../../core/services/placement.service';
import { PlacementResult, AdaptivePlacementSummary } from '../../../../core/models/placement.models';
import { StudentDashboardSummary } from '../../../../core/models/dashboard-summary.models';
import { DashboardResponse } from '../../../../core/models/dashboard.models';
import { StudentLearningMemory } from '../../../../core/models/learning-path.models';
import { DailyLessonModuleSection } from '../../../../core/models/session.models';
import { PracticeGymSuggestionsResponse } from '../../../../core/services/practice-gym-suggestions.service';
import { SessionService } from '../../../../core/services/session.service';

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
  practiceSuggestions = signal<PracticeGymSuggestionsResponse | null>(null);
  practiceLoading = signal(false);
  /**
   * Phase I2B — Today is module-only now. `today.moduleSection` from GET /api/sessions/today is
   * the single source of truth for the Today card: null while loading, populated once the
   * request resolves. `todaySectionLoaded`/`todaySectionAvailable` distinguish "still loading"
   * from "loaded but nothing available" so the template never shows a stale/legacy shape.
   */
  dailyLessonModuleSection = signal<DailyLessonModuleSection | null>(null);
  todaySectionLoading = signal(false);
  todaySectionLoaded = signal(false);
  todaySectionAvailable = signal(false);

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

  reviewCount = computed(() => {
    const s = this.practiceSuggestions();
    return s ? s.reviewItems.length : null;
  });

  constructor(
    private summaryService: DashboardSummaryService,
    private placementService: PlacementService,
    private authNotice: AuthNoticeService,
    private sessionService: SessionService,
  ) {
    this.notice.set(this.authNotice.consume() ?? '');
  }

  activityDots(total: number): number[] {
    return Array.from({ length: total }, (_, i) => i);
  }

  ngOnInit(): void {
    this.summaryService.getSummary().subscribe({
      next: summary => {
        this.loading.set(false);
        this.applyFromSummary(summary);
        if (this.hasPlacementResultState(summary.courseReadiness.lifecycleStatus)) {
          this.placementService.getAdaptiveCurrent().subscribe({
            next: adaptive => this.placementResult.set(this.toPlacementResult(adaptive)),
            error: () => this.placementResult.set(null),
          });
        }
        // Phase I2B — Today is module-only: a failed/empty module section is an honest
        // "nothing available yet" state, not swallowed to null like the old additive H6 behavior.
        if (this.hasLessonAccess(summary.courseReadiness.lifecycleStatus)) {
          this.todaySectionLoading.set(true);
          this.sessionService.getToday().subscribe({
            next: today => {
              this.dailyLessonModuleSection.set(today.moduleSection ?? null);
              this.todaySectionAvailable.set(today.available);
              this.todaySectionLoading.set(false);
              this.todaySectionLoaded.set(true);
            },
            error: () => {
              this.dailyLessonModuleSection.set(null);
              this.todaySectionAvailable.set(false);
              this.todaySectionLoading.set(false);
              this.todaySectionLoaded.set(true);
            },
          });
        }
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your dashboard.');
      },
    });
  }

  private applyFromSummary(s: StudentDashboardSummary): void {
    // Synthesize DashboardResponse from summary for template compatibility.
    this.data.set({
      studentName: s.profile.displayName,
      careerProfile: '',
      cefrLevel: s.profile.cefrLevel,
      message: '',
      lifecycleStage: s.courseReadiness.lifecycleStatus,
      learningPath: s.learningPlan.totalObjectives > 0 ? {
        pathId: '',
        title: s.learningPlan.pathTitle ?? '',
        modulesCompleted: s.learningPlan.modulesCompleted,
        totalModules: s.learningPlan.totalObjectives,
        currentModule: s.learningPlan.currentObjective ? {
          moduleId: '',
          title: s.learningPlan.currentObjective,
          description: s.learningPlan.currentObjectiveDescription ?? '',
          order: s.learningPlan.objectiveIndex,
          completedActivities: s.learningPlan.completedActivities,
          totalActivities: s.learningPlan.totalActivities,
          isCurrent: true,
          isCompleted: false,
          isReadyToComplete: false,
          averageScore: null,
          latestScore: null,
        } : null,
      } : null,
      activityStats: {
        activitiesCompleted: s.quickStats.activitiesCompleted,
        latestScore: null,
        averageScore: null,
      },
      currentFocus: null,
      nextRecommendedPractice: null,
      latestImprovement: null,
      streakDays: s.quickStats.streakDays,
    });

    // Synthesize StudentLearningMemory from progress section.
    if (s.progress.skillProfile.length > 0 || s.progress.journeySummary) {
      this.memory.set({
        journeySummary: s.progress.journeySummary,
        strongSkills: s.progress.strongSkills,
        weakSkills: s.progress.weakSkills,
        recurringMistakes: [],
        nextRecommendedFocus: s.progress.nextRecommendedFocus,
        coveredScenarioCount: 0,
        skillProfile: s.progress.skillProfile.map(sk => ({
          skillKey: sk.skillKey,
          skillLabel: sk.skillLabel,
          isWeak: sk.isWeak,
          scorePercent: sk.scorePercent,
        })),
      });
    }

    // Synthesize PracticeGymSuggestionsResponse from practice section.
    // NotAvailable leaves the signal null so the template shows the "preparing" state.
    const p = s.practice;
    if (p.status !== 'NotAvailable') {
      const suggestedItems = p.suggestedItem ? [{
        readinessItemId: p.suggestedItem.readinessItemId,
        title: p.suggestedItem.title,
        description: p.suggestedItem.description,
        primarySkill: p.suggestedItem.primarySkill,
        secondarySkills: [],
        patternKey: null,
        activityType: null,
        targetCefrLevel: '',
        studentCefrLevelSnapshot: null,
        curriculumObjectiveKey: null,
        curriculumObjectiveTitle: null,
        contextTags: [],
        focusTags: [],
        routingReason: '',
        isLowerLevelContent: false,
        difficultyBand: 0,
        estimatedDurationMinutes: null,
        supportLanguageName: null,
        status: 'ready',
        callToAction: p.suggestedItem.callToAction,
        explanation: '',
        linkedLearningActivityId: null,
        linkedLearningSessionId: null,
        linkedSessionExerciseId: null,
      }] : [];

      this.practiceSuggestions.set({
        suggestedItems,
        continueItems: [],
        reviewItems: Array.from({ length: p.reviewQueueCount }) as any,
        readyCount: suggestedItems.length,
        reviewOnlyCount: p.reviewQueueCount,
        reservedCount: 0,
        isReplenishmentRecommended: false,
        generatedAtUtc: new Date().toISOString(),
        // Phase H7 — this synthesized dashboard summary never carries real module suggestions;
        // the dedicated Practice Gym page calls the real suggestions endpoint for that.
        moduleSuggestions: null,
      });
    }
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

  hasLessonAccess(stage: string): boolean {
    return stage === 'CourseReady' || stage === 'InLesson' || stage === 'ActiveLearning';
  }

  private hasPlacementResultState(stage: string): boolean {
    return stage === 'CourseReady' || stage === 'PlacementCompleted';
  }

  /** Maps the adaptive placement summary onto the dashboard's PlacementResult display shape. */
  private toPlacementResult(adaptive: AdaptivePlacementSummary | null): PlacementResult | null {
    if (!adaptive) return null;
    return {
      estimatedOverallLevel: adaptive.overallCefrLevel ?? '',
      skillLevels: adaptive.skillResults.map(r => ({ skill: r.skill, level: r.estimatedCefrLevel })),
      strengths: adaptive.skillResults.map(r => r.strengths).filter((s): s is string => !!s),
      weaknesses: adaptive.skillResults.map(r => r.weaknesses).filter((w): w is string => !!w),
      recommendedStartingCourse: null,
      recommendedSessionDuration: null,
      placementNotes: adaptive.resultSummary,
      isCompleted: adaptive.status === 'Completed',
    };
  }
}

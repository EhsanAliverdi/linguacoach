import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LearningPathService } from '../../core/services/learning-path.service';
import { HistoryService } from '../../core/services/history.service';
import { LearningPathDetail, LearningModuleSummary, StudentLearningMemory } from '../../core/models/learning-path.models';
import { ModuleActivityHistory } from '../../core/models/history.models';

@Component({
  selector: 'app-learning-path',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './learning-path.component.html',
})
export class LearningPathComponent implements OnInit {
  path = signal<LearningPathDetail | null>(null);
  loading = signal(true);
  error = signal('');
  memory = signal<StudentLearningMemory | null>(null);
  memoryLoading = signal(true);
  continueLoading = signal(false);
  continueSuccess = signal('');
  continueError = signal('');
  completingModuleId = signal<string | null>(null);
  completeSuccess = signal(false);

  // Module drill-down
  expandedModuleId = signal<string | null>(null);
  moduleHistory = signal<ModuleActivityHistory | null>(null);
  loadingHistory = signal(false);
  historyError = signal('');

  overallProgress = computed(() => {
    const p = this.path();
    if (!p || p.totalModules === 0) return 0;
    return Math.round((p.modulesCompleted / p.totalModules) * 100);
  });

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

  canGenerateNext = computed(() => {
    const p = this.path();
    if (!p || this.continueLoading()) return false;
    const hasCompleted = p.modulesCompleted > 0 || p.modules.some(m => m.isCompleted || m.completedActivities >= m.totalActivities);
    const hasUpcoming = p.modules.some(m => !m.isCompleted && m.completedActivities < m.totalActivities);
    return hasCompleted && !hasUpcoming;
  });

  constructor(
    private pathService: LearningPathService,
    private historySvc: HistoryService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.memoryLoading.set(true);
    this.pathService.getActivePath().subscribe({
      next: p => { this.path.set(p); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        if (err.status === 404) {
          this.error.set('Your learning path is not ready yet. Complete onboarding and start an activity to generate your path.');
        } else {
          this.error.set(err.error?.error ?? 'Could not load your learning path.');
        }
      },
    });
    this.pathService.getLearningMemory().subscribe({
      next: m => { this.memory.set(m); this.memoryLoading.set(false); },
      error: () => { this.memory.set(null); this.memoryLoading.set(false); },
    });
  }

  moduleStatus(mod: LearningModuleSummary): 'current' | 'complete' | 'locked' {
    if (mod.isCompleted || mod.completedActivities >= mod.totalActivities) return 'complete';
    if (mod.isCurrent) return 'current';
    return 'locked';
  }

  progressPercent(mod: LearningModuleSummary): number {
    if (mod.totalActivities === 0) return 0;
    return Math.round((mod.completedActivities / mod.totalActivities) * 100);
  }

  completeModule(moduleId: string): void {
    if (this.completingModuleId()) return;
    this.completingModuleId.set(moduleId);
    this.pathService.completeModule(moduleId).subscribe({
      next: () => {
        this.completingModuleId.set(null);
        this.completeSuccess.set(true);
        this.load();
      },
      error: err => {
        this.completingModuleId.set(null);
        this.error.set(err.error?.error ?? 'Could not complete module. Please try again.');
      },
    });
  }

  generateNextModules(): void {
    const p = this.path();
    if (!p || this.continueLoading()) return;
    this.continueLoading.set(true);
    this.continueSuccess.set('');
    this.continueError.set('');
    this.pathService.generateNextModules(p.pathId).subscribe({
      next: updated => {
        this.path.set(updated);
        this.continueLoading.set(false);
        this.continueSuccess.set('New recommended modules have been added to your path.');
        this.pathService.getLearningMemory().subscribe({ next: m => this.memory.set(m), error: () => {} });
      },
      error: err => {
        this.continueLoading.set(false);
        const cid = err.error?.correlationId ?? err.headers?.get?.('x-correlation-id');
        if (err.status === 503) {
          this.continueError.set(`The AI coach is temporarily unavailable. Please try again shortly.${cid ? ' Reference: ' + cid : ''}`);
        } else if (err.status === 409) {
          this.continueError.set('Your path was updated. Refreshing now.');
          this.load();
        } else {
          this.continueError.set(err.error?.error ?? 'Could not add recommended modules. Please try again.');
        }
      },
    });
  }

  toggleModuleHistory(moduleId: string): void {
    if (this.expandedModuleId() === moduleId) {
      this.expandedModuleId.set(null);
      this.moduleHistory.set(null);
      return;
    }
    this.expandedModuleId.set(moduleId);
    this.moduleHistory.set(null);
    this.loadingHistory.set(true);
    this.historyError.set('');
    this.historySvc.getModuleActivities(moduleId).subscribe({
      next: h => { this.moduleHistory.set(h); this.loadingHistory.set(false); },
      error: err => {
        this.loadingHistory.set(false);
        this.historyError.set(err.error?.error ?? 'Could not load module activities.');
      },
    });
  }

  activityDots(total: number): number[] {
    return Array.from({ length: total }, (_, i) => i);
  }

  scoreColour(score: number | null): string {
    if (score === null) return 'var(--sp-faint)';
    if (score >= 85) return 'var(--sp-success)';
    if (score >= 70) return 'var(--sp-vocabulary)';
    return 'var(--sp-speaking)';
  }

  skillLabel(value: string | null | undefined): string {
    if (!value) return '';
    return value
      .replace(/_/g, ' ')
      .replace(/\b\w/g, c => c.toUpperCase());
  }
}

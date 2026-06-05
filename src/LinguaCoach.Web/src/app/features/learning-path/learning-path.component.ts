import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LearningPathService } from '../../core/services/learning-path.service';
import { HistoryService } from '../../core/services/history.service';
import { LearningPathDetail, LearningModuleSummary } from '../../core/models/learning-path.models';
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

  constructor(
    private pathService: LearningPathService,
    private historySvc: HistoryService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
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
}

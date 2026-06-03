import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LearningPathService } from '../../core/services/learning-path.service';
import { LearningPathDetail, LearningModuleSummary } from '../../core/models/learning-path.models';

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

  overallProgress = computed(() => {
    const p = this.path();
    if (!p || p.totalModules === 0) return 0;
    return Math.round((p.modulesCompleted / p.totalModules) * 100);
  });

  constructor(private pathService: LearningPathService) {}

  ngOnInit(): void {
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
    if (mod.isCurrent) return 'current';
    if (mod.completedActivities >= mod.totalActivities) return 'complete';
    return 'locked';
  }

  progressPercent(mod: LearningModuleSummary): number {
    if (mod.totalActivities === 0) return 0;
    return Math.round((mod.completedActivities / mod.totalActivities) * 100);
  }

  activityDots(total: number): number[] {
    return Array.from({ length: total }, (_, i) => i);
  }
}

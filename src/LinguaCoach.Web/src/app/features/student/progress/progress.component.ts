import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProgressService } from '../../../core/services/progress.service';
import { VocabularyService } from '../../../core/services/vocabulary.service';
import { ProgressSummary, ProgressModule, ScoreTrendPoint } from '../../../core/models/progress.models';
import { StudentVocabularyItem } from '../../../core/models/vocabulary.models';

@Component({
  selector: 'app-progress',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <!-- Loading skeleton -->
    @if (loading()) {
      <div class="sp-section-h" style="margin-bottom:16px">
        <h3>Your progress</h3>
        <p style="font-size:13px;color:var(--sp-muted)">Loading your progress dataâ€¦</p>
      </div>
      <div class="sp-stat-grid" style="margin-bottom:24px">
        @for (n of [1,2,3,4,5,6]; track n) {
          <div class="sp-card sp-skeleton" style="height:80px"></div>
        }
      </div>
    }

    <!-- Error state -->
    @if (!loading() && error()) {
      <div class="sp-card sp-card-warning" style="padding:20px;margin-bottom:20px;text-align:center">
        <div style="font-size:28px;margin-bottom:8px">âš ï¸</div>
        <p style="font-size:14px;font-weight:600;color:var(--sp-ink);margin-bottom:4px">Could not load progress</p>
        <p style="font-size:13px;color:var(--sp-muted);margin-bottom:12px">{{ error() }}</p>
        <button class="sp-button-secondary" (click)="load()">Try again</button>
      </div>
    }

    <!-- Empty state: no attempts yet -->
    @if (!loading() && !error() && isEmpty()) {
      <div class="sp-section-h" style="margin-bottom:8px">
        <h3>Your progress</h3>
        <p style="font-size:13px;color:var(--sp-muted)">Track your writing practice, skill growth, and next focus.</p>
      </div>
      <div class="sp-empty-state">
        <div style="font-size:36px;margin-bottom:12px">ðŸ“Š</div>
        <h3 style="font-size:16px;font-weight:800;color:var(--sp-ink);margin-bottom:8px">No progress yet</h3>
        <p style="font-size:13px;color:var(--sp-muted);line-height:1.6;max-width:290px;text-align:center">
          Your progress will appear here after you complete your first activity.
        </p>
        <a routerLink="/activity" class="sp-button-primary" style="margin-top:16px;display:inline-flex">
          Start practising â†’
        </a>
      </div>
    }

    <!-- Real data -->
    @if (!loading() && !error() && !isEmpty()) {
      <!-- Header -->
      <div class="sp-section-h" style="margin-bottom:20px">
        <div>
          <h3 style="margin-bottom:4px">Your progress</h3>
          <p style="font-size:13px;color:var(--sp-muted)">Track your writing practice, skill growth, and next focus.</p>
        </div>
      </div>

      <!-- Summary cards -->
      <div class="sp-stat-grid" style="margin-bottom:24px">
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">âœ…</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ data()!.summary.activitiesCompleted }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">activities completed</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">â­</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">
            {{ data()!.summary.averageScore != null ? data()!.summary.averageScore : 'â€”' }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">average score</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">ðŸŽ¯</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">
            {{ data()!.summary.latestScore != null ? data()!.summary.latestScore : 'â€”' }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">latest score</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">ðŸ“…</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ data()!.summary.activitiesThisWeek }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">this week</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">ðŸ“š</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ data()!.summary.modulesCompleted }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">modules completed</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:20px;margin-bottom:4px">ðŸ”„</div>
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ data()!.summary.retryAttempts }}</div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">retry attempts</div>
        </div>
      </div>

      <!-- Score trend -->
      @if (data()!.scoreTrend.length > 0) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Recent scores</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px">
          <div style="display:flex;flex-direction:column;gap:10px">
            @for (point of data()!.scoreTrend; track point.attemptDate) {
              <div style="display:flex;align-items:center;gap:12px">
                <div [style.background]="scoreColour(point.score)"
                     style="min-width:42px;height:28px;border-radius:6px;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:800;color:#fff">
                  {{ point.score | number:'1.0-0' }}
                </div>
                <div style="flex:1;min-width:0">
                  <div style="font-size:13px;font-weight:600;color:var(--sp-ink);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">
                    {{ point.activityTitle }}
                  </div>
                  <div style="font-size:11px;color:var(--sp-muted)">
                    Attempt {{ point.attemptNumber }}
                    @if (point.moduleTitle) { Â· {{ point.moduleTitle }} }
                    Â· {{ point.attemptDate | date:'d MMM' }}
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- Skill progress -->
      @if (hasSkills()) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Skill progress</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px">
          @if (data()!.skillProgress.topStrengths.length > 0) {
            <div style="margin-bottom:14px">
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px">
                Strengths
              </div>
              <div style="display:flex;flex-wrap:wrap;gap:6px">
                @for (s of data()!.skillProgress.topStrengths; track s) {
                  <span style="font-size:12px;font-weight:600;padding:4px 10px;border-radius:20px;background:var(--sp-success-soft);color:var(--sp-success)">
                    {{ s }}
                  </span>
                }
              </div>
            </div>
          }
          @if (data()!.skillProgress.weakestSkills.length > 0) {
            <div>
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px">
                Areas to improve
              </div>
              <div style="display:flex;flex-wrap:wrap;gap:6px">
                @for (s of data()!.skillProgress.weakestSkills; track s) {
                  <span style="font-size:12px;font-weight:600;padding:4px 10px;border-radius:20px;background:var(--sp-warn-soft);color:var(--sp-warn)">
                    {{ s }}
                  </span>
                }
              </div>
            </div>
          }
          @if (data()!.skillProgress.topStrengths.length === 0 && data()!.skillProgress.weakestSkills.length === 0) {
            <div style="display:flex;flex-direction:column;gap:10px">
              @for (skill of data()!.skillProgress.skills; track skill.skillKey) {
                <div style="display:flex;justify-content:space-between;align-items:center">
                  <div style="display:flex;align-items:center;gap:8px">
                    <div style="width:8px;height:8px;border-radius:50%"
                         [style.background]="skill.isWeak ? 'var(--sp-warn)' : 'var(--sp-success)'">
                    </div>
                    <span style="font-size:13px;font-weight:600;color:var(--sp-ink)">{{ skill.skillLabel }}</span>
                  </div>
                  <span style="font-size:11px;font-weight:600" [style.color]="skill.isWeak ? 'var(--sp-warn)' : 'var(--sp-muted)'">
                    {{ skill.scorePercent }}%
                  </span>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Module progress -->
      @if (data()!.moduleProgress.length > 0) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Module progress</h3>
          <a routerLink="/my-path" style="font-size:13px;color:var(--sp-link,var(--sp-writing));font-weight:600;text-decoration:none">
            View path â†’
          </a>
        </div>
        <div style="display:flex;flex-direction:column;gap:10px;margin-bottom:20px">
          @for (mod of data()!.moduleProgress; track mod.moduleId) {
            <div class="sp-card" style="padding:14px">
              <div style="display:flex;align-items:flex-start;justify-content:space-between;gap:8px;margin-bottom:8px">
                <div style="font-size:13px;font-weight:700;color:var(--sp-ink);flex:1;min-width:0">{{ mod.title }}</div>
                <span [style.background]="statusBg(mod.status)"
                      [style.color]="statusFg(mod.status)"
                      style="font-size:10px;font-weight:700;padding:2px 8px;border-radius:12px;white-space:nowrap;flex-shrink:0">
                  {{ statusLabel(mod.status) }}
                </span>
              </div>
              <div style="display:flex;gap:16px;flex-wrap:wrap">
                <span style="font-size:12px;color:var(--sp-muted)">
                  {{ mod.completedActivities }}/{{ mod.totalRequired }} activities
                </span>
                @if (mod.averageScore != null) {
                  <span style="font-size:12px;color:var(--sp-muted)">Avg {{ mod.averageScore | number:'1.0-0' }}</span>
                }
                @if (mod.latestScore != null) {
                  <span style="font-size:12px;color:var(--sp-muted)">Latest {{ mod.latestScore | number:'1.0-0' }}</span>
                }
              </div>
              @if (mod.isReadyToComplete && mod.status === 'current') {
                <div style="margin-top:8px;font-size:12px;font-weight:600;color:var(--sp-writing,#2e7d32)">
                  Ready to complete â€” open My Path to advance.
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Vocabulary snapshot -->
      @if (vocabPreview().length > 0) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Vocabulary snapshot</h3>
          <a routerLink="/vocabulary" style="font-size:13px;color:var(--sp-link,var(--sp-writing));font-weight:600;text-decoration:none">
            View all â†’
          </a>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px">
          <div style="display:flex;flex-direction:column;gap:10px">
            @for (v of vocabPreview(); track v.id) {
              <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px">
                <div style="flex:1;min-width:0">
                  <div style="font-size:13px;font-weight:700;color:var(--sp-ink)">{{ v.term }}</div>
                  @if (v.suggestedPhrase) {
                    <div style="font-size:12px;color:var(--sp-muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ v.suggestedPhrase }}</div>
                  }
                </div>
                <span style="font-size:10px;font-weight:700;padding:2px 8px;border-radius:12px;white-space:nowrap;flex-shrink:0;background:var(--sp-writing-soft);color:var(--sp-writing-ink)">
                  {{ v.status }}
                </span>
              </div>
            }
          </div>
        </div>
      }

      <!-- Learning focus -->
      @if (data()!.learningFocus) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Your learning focus</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px">
          @if (data()!.learningFocus!.journeySummary) {
            <p style="font-size:13px;color:var(--sp-ink);line-height:1.6;margin-bottom:12px">
              {{ data()!.learningFocus!.journeySummary }}
            </p>
          }
          @if (data()!.learningFocus!.nextRecommendedFocus.length > 0) {
            <div style="margin-bottom:10px">
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px">
                Next focus
              </div>
              <ul style="list-style:disc;padding-left:18px;margin:0;display:flex;flex-direction:column;gap:4px">
                @for (f of data()!.learningFocus!.nextRecommendedFocus; track f) {
                  <li style="font-size:13px;color:var(--sp-ink)">{{ f }}</li>
                }
              </ul>
            </div>
          }
          @if (data()!.learningFocus!.recurringMistakes.length > 0) {
            <div>
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px">
                Recurring mistakes to watch
              </div>
              <ul style="list-style:disc;padding-left:18px;margin:0;display:flex;flex-direction:column;gap:4px">
                @for (m of data()!.learningFocus!.recurringMistakes; track m) {
                  <li style="font-size:13px;color:var(--sp-ink)">{{ m }}</li>
                }
              </ul>
            </div>
          }
        </div>
      }
    }
  `,
})
export class ProgressComponent implements OnInit {
  data = signal<ProgressSummary | null>(null);
  loading = signal(true);
  error = signal('');
  vocabPreview = signal<StudentVocabularyItem[]>([]);

  isEmpty = computed(() => {
    const d = this.data();
    return d !== null && d.summary.totalAttempts === 0;
  });

  hasSkills = computed(() => {
    const d = this.data();
    return d !== null && d.skillProgress.skills.length > 0;
  });

  constructor(
    private progressService: ProgressService,
    private vocabularyService: VocabularyService,
  ) {}

  ngOnInit(): void {
    this.load();
    this.vocabularyService.getVocabulary('Practising').subscribe({
      next: items => this.vocabPreview.set(items.slice(0, 3)),
      error: () => this.vocabPreview.set([]),
    });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.progressService.getProgress().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your progress. Please try again.');
      },
    });
  }

  scoreColour(score: number): string {
    if (score >= 80) return 'var(--sp-success)';
    if (score >= 65) return 'var(--sp-warn)';
    return 'var(--sp-speaking)';
  }

  statusLabel(status: string): string {
    if (status === 'completed') return 'Completed';
    if (status === 'current') return 'In progress';
    return 'Upcoming';
  }

  statusBg(status: string): string {
    if (status === 'completed') return 'var(--sp-success-soft)';
    if (status === 'current') return 'var(--sp-writing-soft)';
    return 'var(--sp-canvas2)';
  }

  statusFg(status: string): string {
    if (status === 'completed') return 'var(--sp-success)';
    if (status === 'current') return 'var(--sp-writing-ink)';
    return 'var(--sp-muted)';
  }
}



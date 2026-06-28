import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProgressService } from '../../../core/services/progress.service';
import { StudentProgressSummary, ProgressSummarySkill, ProgressActivityEvent } from '../../../core/models/student-progress-summary.models';

@Component({
  selector: 'app-progress',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <!-- Loading skeleton -->
    @if (loading()) {
      <div data-testid="progress-loading">
        <div class="sp-section-h" style="margin-bottom:16px">
          <h3>Your progress</h3>
        </div>
        <div class="sp-stat-grid" style="margin-bottom:24px">
          @for (n of [1,2,3,4]; track n) {
            <div class="sp-card sp-skeleton" style="height:80px"></div>
          }
        </div>
        <div class="sp-card sp-skeleton" style="height:140px;margin-bottom:16px"></div>
        <div class="sp-card sp-skeleton" style="height:180px;margin-bottom:16px"></div>
      </div>
    }

    <!-- Error state -->
    @if (!loading() && error()) {
      <div class="sp-card sp-card-warning" style="padding:20px;margin-bottom:20px;text-align:center"
           data-testid="progress-error">
        <p style="font-size:14px;font-weight:600;color:var(--sp-ink);margin-bottom:4px">Could not load progress</p>
        <p style="font-size:13px;color:var(--sp-muted);margin-bottom:12px">{{ error() }}</p>
        <button class="sp-button-secondary" (click)="load()" data-testid="progress-retry">Try again</button>
      </div>
    }

    <!-- Loaded state -->
    @if (!loading() && !error() && data()) {
      <!-- Page header -->
      <div class="sp-section-h" style="margin-bottom:20px">
        <div>
          <h3 style="margin-bottom:4px">Your progress</h3>
          <p style="font-size:13px;color:var(--sp-muted)">How you're improving, where to focus, what to do next.</p>
        </div>
        <div style="display:flex;gap:8px">
          <a routerLink="/journey" style="font-size:13px;color:var(--sp-link,var(--sp-writing));font-weight:600;text-decoration:none">
            Journey &rarr;
          </a>
          <span style="color:var(--sp-muted)">|</span>
          <a routerLink="/practice" style="font-size:13px;color:var(--sp-link,var(--sp-writing));font-weight:600;text-decoration:none">
            Practice &rarr;
          </a>
        </div>
      </div>

      <!-- Part C: Learning Summary -->
      <div class="sp-section-h" style="margin-bottom:12px">
        <h3 data-testid="learning-summary-heading">Learning summary</h3>
      </div>
      <div class="sp-stat-grid" style="margin-bottom:24px" data-testid="learning-summary">
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)" data-testid="current-cefr">
            {{ data()!.learning.currentCefrLevel ?? '—' }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">current level</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">
            {{ data()!.learning.currentLearningPhase }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">learning phase</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">
            {{ data()!.learning.objectivesCompleted + data()!.learning.objectivesMastered }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">objectives done</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">
            {{ data()!.learning.objectivesRemaining }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">remaining</div>
        </div>
      </div>

      <!-- Learning plan progress bar -->
      @if (data()!.learning.totalObjectives > 0) {
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="learning-plan-progress">
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px">
            <span style="font-size:13px;font-weight:600;color:var(--sp-ink)">Plan progress</span>
            <span style="font-size:13px;font-weight:700;color:var(--sp-writing-ink)">
              {{ data()!.learning.completionPercentage | number:'1.0-0' }}%
            </span>
          </div>
          <div style="height:8px;border-radius:4px;background:var(--sp-canvas2);overflow:hidden">
            <div [style.width.%]="data()!.learning.completionPercentage"
                 style="height:100%;border-radius:4px;background:var(--sp-writing);transition:width .4s ease">
            </div>
          </div>
          <div style="display:flex;gap:16px;flex-wrap:wrap;margin-top:10px">
            <span style="font-size:12px;color:var(--sp-muted)">
              {{ data()!.learning.objectivesMastered }} mastered
            </span>
            <span style="font-size:12px;color:var(--sp-muted)">
              {{ data()!.learning.objectivesInProgress }} in progress
            </span>
            @if (data()!.learning.objectivesCompletedToday > 0) {
              <span style="font-size:12px;font-weight:600;color:var(--sp-success)">
                +{{ data()!.learning.objectivesCompletedToday }} today
              </span>
            }
          </div>
          @if (data()!.learning.currentObjectiveSkill) {
            <div style="margin-top:10px;font-size:12px;color:var(--sp-ink)">
              Current focus: <strong>{{ data()!.learning.currentObjectiveSkill }}</strong>
            </div>
          }
        </div>
      }

      <!-- Part E: CEFR Progress -->
      <div class="sp-section-h" style="margin-bottom:12px">
        <h3>CEFR progress</h3>
      </div>
      <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="cefr-progress">
        @if (data()!.cefr.startingCefrLevel) {
          <div style="display:flex;align-items:center;gap:12px;flex-wrap:wrap">
            <div style="text-align:center;padding:10px 16px;border-radius:10px;background:var(--sp-canvas2)">
              <div style="font-size:20px;font-weight:800;color:var(--sp-muted)">
                {{ data()!.cefr.startingCefrLevel }}
              </div>
              <div style="font-size:10px;font-weight:600;color:var(--sp-muted);margin-top:2px">started</div>
            </div>
            <div style="font-size:20px;color:var(--sp-muted)">&rarr;</div>
            <div style="text-align:center;padding:10px 16px;border-radius:10px"
                 [style.background]="data()!.cefr.cefrImproved ? 'var(--sp-success-soft)' : 'var(--sp-canvas2)'">
              <div style="font-size:20px;font-weight:800"
                   [style.color]="data()!.cefr.cefrImproved ? 'var(--sp-success)' : 'var(--sp-ink)'">
                {{ data()!.cefr.currentCefrLevel ?? '—' }}
              </div>
              <div style="font-size:10px;font-weight:600;color:var(--sp-muted);margin-top:2px">
                {{ data()!.cefr.cefrImproved ? 'improved' : 'current' }}
              </div>
            </div>
          </div>
          @if (data()!.cefr.placementDate) {
            <div style="margin-top:10px;font-size:12px;color:var(--sp-muted)">
              Placement completed {{ data()!.cefr.placementDate | date:'d MMM yyyy' }}
            </div>
          }
        } @else {
          <p style="font-size:13px;color:var(--sp-muted)">
            {{ data()!.cefr.note ?? 'Complete a placement assessment to see your CEFR level.' }}
          </p>
        }
      </div>

      <!-- Part D: Skill Progress -->
      @if (data()!.skills.length > 0) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Skill progress</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="skill-progress">
          <div style="display:flex;flex-direction:column;gap:12px">
            @for (skill of data()!.skills; track skill.skillKey) {
              <div>
                <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:4px">
                  <div style="display:flex;align-items:center;gap:8px">
                    <div style="width:8px;height:8px;border-radius:50%"
                         [style.background]="skill.isWeak ? 'var(--sp-warn)' : 'var(--sp-success)'">
                    </div>
                    <span style="font-size:13px;font-weight:600;color:var(--sp-ink)">{{ skill.skillLabel }}</span>
                    @if (skill.isWeak) {
                      <span style="font-size:10px;font-weight:700;padding:1px 6px;border-radius:10px;background:var(--sp-warn-soft);color:var(--sp-warn)">
                        needs work
                      </span>
                    }
                  </div>
                  <span style="font-size:12px;font-weight:600"
                        [style.color]="skill.isWeak ? 'var(--sp-warn)' : 'var(--sp-muted)'">
                    {{ skill.scorePercent }}%
                  </span>
                </div>
                <div style="height:4px;border-radius:2px;background:var(--sp-canvas2);overflow:hidden">
                  <div [style.width.%]="skill.scorePercent"
                       [style.background]="skill.isWeak ? 'var(--sp-warn)' : 'var(--sp-success)'"
                       style="height:100%;border-radius:2px;transition:width .4s ease">
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
      } @else {
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="skill-progress-empty">
          <p style="font-size:13px;color:var(--sp-muted)">
            Skill data will appear after you complete your first activities.
          </p>
        </div>
      }

      <!-- Part F: Mastery & Review -->
      <div class="sp-section-h" style="margin-bottom:12px">
        <h3>Mastery &amp; review</h3>
      </div>
      <div class="sp-stat-grid" style="margin-bottom:20px" data-testid="mastery-summary">
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-success)">
            {{ data()!.mastery.masteredObjectivesCount }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">mastered</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800;color:var(--sp-writing-ink)">
            {{ data()!.mastery.inProgressObjectivesCount }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">in progress</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800"
               [style.color]="data()!.mastery.reviewQueueCount > 0 ? 'var(--sp-warn)' : 'var(--sp-muted)'">
            {{ data()!.mastery.reviewQueueCount }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">need review</div>
        </div>
        <div class="sp-card" style="padding:14px;text-align:center">
          <div style="font-size:22px;font-weight:800"
               [style.color]="data()!.mastery.weakSkillsCount > 0 ? 'var(--sp-warn)' : 'var(--sp-muted)'">
            {{ data()!.mastery.weakSkillsCount }}
          </div>
          <div style="font-size:11px;font-weight:600;color:var(--sp-muted);margin-top:2px">weak skills</div>
        </div>
      </div>
      @if (data()!.mastery.weakSkillLabels.length > 0) {
        <div class="sp-card" style="padding:12px 16px;margin-bottom:20px" data-testid="weak-skill-labels">
          <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px">
            Skills needing attention
          </div>
          <div style="display:flex;flex-wrap:wrap;gap:6px">
            @for (label of data()!.mastery.weakSkillLabels; track label) {
              <span style="font-size:12px;font-weight:600;padding:3px 10px;border-radius:20px;background:var(--sp-warn-soft);color:var(--sp-warn)">
                {{ label }}
              </span>
            }
          </div>
        </div>
      }

      <!-- Part H: Focus Recommendations -->
      @if (hasFocus()) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>What to focus on</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="focus-recommendations">
          @if (data()!.focus.journeySummary) {
            <p style="font-size:13px;color:var(--sp-ink);line-height:1.6;margin-bottom:12px">
              {{ data()!.focus.journeySummary }}
            </p>
          }
          @if (data()!.focus.recommendations.length > 0) {
            <div style="margin-bottom:10px">
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px">
                Recommended next steps
              </div>
              <ul style="list-style:disc;padding-left:18px;margin:0;display:flex;flex-direction:column;gap:4px">
                @for (rec of data()!.focus.recommendations; track rec) {
                  <li style="font-size:13px;color:var(--sp-ink)">{{ rec }}</li>
                }
              </ul>
            </div>
          }
          @if (data()!.focus.recurringMistakes.length > 0) {
            <div>
              <div style="font-size:11px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.5px;margin-bottom:6px">
                Watch out for
              </div>
              <ul style="list-style:disc;padding-left:18px;margin:0;display:flex;flex-direction:column;gap:4px">
                @for (m of data()!.focus.recurringMistakes; track m) {
                  <li style="font-size:13px;color:var(--sp-ink)">{{ m }}</li>
                }
              </ul>
            </div>
          }
        </div>
      }

      <!-- Part G: Recent Activity -->
      @if (data()!.recentActivity.length > 0) {
        <div class="sp-section-h" style="margin-bottom:12px">
          <h3>Recent activity</h3>
        </div>
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="recent-activity">
          <div style="display:flex;flex-direction:column;gap:10px">
            @for (event of data()!.recentActivity; track event.occurredAt) {
              <div style="display:flex;align-items:flex-start;gap:10px">
                <div style="width:8px;height:8px;border-radius:50%;margin-top:5px;flex-shrink:0"
                     [style.background]="eventColour(event.eventType)">
                </div>
                <div style="flex:1;min-width:0">
                  <div style="font-size:13px;font-weight:600;color:var(--sp-ink)">
                    {{ event.description }}
                  </div>
                  @if (event.detail) {
                    <div style="font-size:12px;color:var(--sp-muted)">{{ event.detail }}</div>
                  }
                  <div style="font-size:11px;color:var(--sp-muted);margin-top:2px">
                    {{ event.occurredAt | date:'d MMM, h:mm a' }}
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
      } @else {
        <div class="sp-card" style="padding:16px;margin-bottom:20px" data-testid="recent-activity-empty">
          <p style="font-size:13px;color:var(--sp-muted)">No recent activity yet. Complete your first lesson to get started.</p>
        </div>
      }

      <!-- Navigation links -->
      <div style="display:flex;gap:12px;flex-wrap:wrap;margin-bottom:20px">
        <a routerLink="/dashboard" class="sp-button-secondary" style="font-size:13px">
          Dashboard
        </a>
        <a routerLink="/today" class="sp-button-secondary" style="font-size:13px">
          Today's lesson
        </a>
        <a routerLink="/journey" class="sp-button-secondary" style="font-size:13px">
          Journey
        </a>
        <a routerLink="/practice" class="sp-button-secondary" style="font-size:13px">
          Practice
        </a>
      </div>
    }
  `,
})
export class ProgressComponent implements OnInit {
  data = signal<StudentProgressSummary | null>(null);
  loading = signal(true);
  error = signal('');

  hasFocus = computed(() => {
    const d = this.data();
    if (!d) return false;
    return d.focus.recommendations.length > 0
        || d.focus.recurringMistakes.length > 0
        || !!d.focus.journeySummary;
  });

  constructor(private progressService: ProgressService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.progressService.getProgressSummary().subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load your progress. Please try again.');
      },
    });
  }

  eventColour(eventType: string): string {
    switch (eventType) {
      case 'PlacementCompleted': return 'var(--sp-writing)';
      case 'LessonCompleted':    return 'var(--sp-success)';
      case 'PracticeCompleted':  return 'var(--sp-listening)';
      case 'ObjectiveMastered':  return 'var(--sp-success)';
      default:                   return 'var(--sp-muted)';
    }
  }
}

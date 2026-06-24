import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  StudentListItem, UpdateStudentProfileRequest, ResetStudentRequest, StudentLifecycleStageName,
  AdminStudentLearningMemory, ResetStudentResponse, AdminActivityHistoryItem,
  AdminStudentDetail, StudentAuditHistoryItem, StudentReadinessPoolHealth,
} from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { UsageGovernanceService, StudentEffectivePolicy, UsagePolicy } from '../../../core/services/usage-governance.service';
import { SpAdminSlideOverComponent } from '../../../design-system/admin/components/slide-over/sp-admin-slide-over.component';
import { SpAdminPageHeaderComponent } from '../../../design-system/admin/components/page-header/sp-admin-page-header.component';
import { SpAdminPageBodyComponent } from '../../../design-system/admin/components/page-body/sp-admin-page-body.component';
import { SpAdminCardComponent } from '../../../design-system/admin/components/card/sp-admin-card.component';
import { SpAdminBadgeComponent } from '../../../design-system/admin/components/badge/sp-admin-badge.component';
import { SpAdminStatCardComponent } from '../../../design-system/admin/components/stat-card/sp-admin-stat-card.component';
import { SpAdminButtonComponent } from '../../../design-system/admin/components/button/sp-admin-button.component';
import { SpAdminAlertComponent } from '../../../design-system/admin/components/alert/sp-admin-alert.component';
import { SpAdminTableComponent } from '../../../design-system/admin/components/table/sp-admin-table.component';
import { SpAdminKpiCardComponent } from '../../../design-system/admin/components/kpi-card/sp-admin-kpi-card.component';
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { lifecycleLabel, lifecycleTone, onboardingLabel, onboardingTone, eventLevelLabel } from '../../../design-system/admin/utils/admin-badge.utils';

interface StudentEditForm {
  firstName: string;
  lastName: string;
  displayName: string;
  careerContext: string;
  learningGoal: string;
  learningGoalDescription: string;
  difficultSituationsText: string;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
}

@Component({
  selector: 'app-admin-student-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    SpAdminSlideOverComponent, SpAdminPageHeaderComponent, SpAdminPageBodyComponent,
    SpAdminCardComponent, SpAdminBadgeComponent, SpAdminStatCardComponent,
    SpAdminButtonComponent, SpAdminAlertComponent, SpAdminTableComponent,
    SpAdminKpiCardComponent, SpAdminRingMetricComponent, SpAdminBreakdownBarsComponent,
  ],
  template: `
    <sp-admin-page-header title="Student detail">
      <sp-admin-button appearance="ghost" size="sm" routerLink="/admin/students">← Students</sp-admin-button>
    </sp-admin-page-header>

    <sp-admin-page-body>

    @if (loading()) {
      <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
    } @else if (error()) {
      <sp-admin-alert variant="error">{{ error() }}</sp-admin-alert>
    } @else if (student()) {
      @let s = student()!;

      <!-- Hero section -->
      <div class="sp-sd-hero">
        <div class="sp-sd-ava" [style.background]="avatarColor(s)">{{ initials(s) }}</div>
        <div class="sp-sd-hero-body">
          <div class="sp-sd-hero-name">{{ displayName(s) }}</div>
          <div class="sp-sd-hero-email">{{ s.email }}</div>
          <div class="sp-sd-hero-badges">
            <sp-admin-badge [tone]="lifecycleTone(s.lifecycleStage)">{{ lifecycleLabel(s.lifecycleStage) }}</sp-admin-badge>
            <sp-admin-badge [tone]="onboardingTone(s.onboardingStatus)">{{ onboardingLabel(s.onboardingStatus) }}</sp-admin-badge>
            @if (s.cefrLevel) {
              <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
            }
            @if (s.supportLanguageName || s.supportLanguageCode) {
              <span class="sp-sd-hero-chip">{{ s.supportLanguageName || s.supportLanguageCode }}</span>
            }
          </div>
        </div>
        <div class="sp-sd-hero-actions">
          <sp-admin-button appearance="ghost" size="sm" (click)="startEdit(s)">Edit</sp-admin-button>
          @if (s.lifecycleStage !== 'Archived') {
            <sp-admin-button appearance="ghost" size="sm" (click)="startResetPassword(s)">Reset password</sp-admin-button>
            <sp-admin-button appearance="ghost" size="sm" (click)="sendResetLink(s)" [disabled]="sendingResetLink()">Send reset link</sp-admin-button>
          }
          @if (s.lifecycleStage === 'Archived') {
            <sp-admin-button appearance="ghost" size="sm" (click)="startLifecycleAction('reactivate', s)">Reactivate</sp-admin-button>
          }
          @if (s.lifecycleStage === 'Paused') {
            <sp-admin-button appearance="ghost" size="sm" (click)="startLifecycleAction('unpause', s)">Unpause</sp-admin-button>
          }
          @if (s.lifecycleStage !== 'Archived' && s.lifecycleStage !== 'Paused') {
            <sp-admin-button variant="danger" appearance="ghost" size="sm" (click)="startLifecycleAction('pause', s)">Pause</sp-admin-button>
          }
        </div>
      </div>

      <!-- KPI strip -->
      <div class="sp-admin-kpi-strip">
        <sp-admin-kpi-card label="Lifecycle" variant="indigo">
          <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
          {{ lifecycleLabel(s.lifecycleStage) }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Onboarding" [variant]="onboardingTone(s.onboardingStatus) === 'success' ? 'green' : 'amber'">
          <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11"/></svg>
          {{ onboardingLabel(s.onboardingStatus) }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="CEFR level" variant="violet">
          <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>
          {{ s.cefrLevel ?? 'Not set' }}
        </sp-admin-kpi-card>
        <sp-admin-kpi-card label="Pool health" [variant]="poolHealthTone() === 'success' ? 'green' : poolHealthTone() === 'warning' ? 'amber' : 'slate'">
          <svg slot="icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>
          {{ poolHealthLoading() ? '…' : poolHealthLabel() }}
        </sp-admin-kpi-card>
      </div>

      <div class="sp-admin-detail-grid">
        <sp-admin-card>
          <h2 class="sp-admin-card-title">Profile</h2>
          <dl class="sp-admin-detail-list">
            <div>
              <dt>Lifecycle stage</dt>
              <dd>
                <sp-admin-badge [tone]="lifecycleTone(s.lifecycleStage)">{{ lifecycleLabel(s.lifecycleStage) }}</sp-admin-badge>
              </dd>
            </div>
            <div>
              <dt>Onboarding</dt>
              <dd>
                <sp-admin-badge [tone]="onboardingTone(s.onboardingStatus)">{{ onboardingLabel(s.onboardingStatus) }}</sp-admin-badge>
              </dd>
            </div>
            <div>
              <dt>CEFR level</dt>
              <dd>
                <span class="sp-admin-cefr-row">
                  @if (s.cefrLevel) {
                    <sp-admin-badge tone="primary">{{ s.cefrLevel }}</sp-admin-badge>
                  } @else {
                    <span class="sp-admin-table-muted">Not set</span>
                  }
                  <button type="button" class="sp-admin-link-button" (click)="startSetCefr(s)">Set CEFR</button>
                </span>
                <p class="sp-admin-cefr-hint">CEFR is controlled by assessment and admin. Students cannot edit this.</p>
              </dd>
            </div>
            <div>
              <dt>Career context</dt>
              <dd>{{ s.careerContext || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Learning goal</dt>
              <dd>{{ s.learningGoal || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Learning goal description</dt>
              <dd>{{ s.learningGoalDescription || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Difficult situations</dt>
              <dd>{{ s.difficultSituationsText || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Preferred session duration</dt>
              <dd>{{ s.preferredSessionDurationMinutes ? s.preferredSessionDurationMinutes + ' minutes' : 'Not set' }}</dd>
            </div>
            <div>
              <dt>Experience level</dt>
              <dd>{{ experienceLabel(s.professionalExperienceLevel) }}</dd>
            </div>
            <div>
              <dt>Role familiarity</dt>
              <dd>{{ familiarityLabel(s.roleFamiliarity) }}</dd>
            </div>
            <div>
              <dt>Joined</dt>
              <dd>{{ s.createdAt | date:'mediumDate' }}</dd>
            </div>
          </dl>
        </sp-admin-card>

        <sp-admin-card aria-label="Student preferences">
          <div class="sp-admin-section-header-row">
            <h2 class="sp-admin-card-title">Student preferences</h2>
            <button type="button" class="sp-admin-link-button" (click)="openPrefsSlideOver()">View preferences</button>
          </div>
          @if (hasAnyPreference(s)) {
            <dl class="sp-admin-detail-list">
              @if (s.preferredName) {
                <div>
                  <dt>Preferred name</dt>
                  <dd>{{ s.preferredName }}</dd>
                </div>
              }
              @if (s.supportLanguageName || s.supportLanguageCode) {
                <div>
                  <dt>Support language</dt>
                  <dd>{{ s.supportLanguageName || s.supportLanguageCode }}</dd>
                </div>
              }
              @if (s.difficultyPreference) {
                <div>
                  <dt>Difficulty</dt>
                  <dd>{{ s.difficultyPreference }}</dd>
                </div>
              }
              @if (s.focusAreas?.length) {
                <div>
                  <dt>Focus areas</dt>
                  <dd>{{ s.focusAreas.slice(0, 3).join(', ') }}{{ s.focusAreas.length > 3 ? '…' : '' }}</dd>
                </div>
              }
            </dl>
          } @else {
            <p class="sp-admin-table-empty">Student has not set any learning preferences yet.</p>
          }
        </sp-admin-card>

        <sp-admin-card aria-label="Onboarding progress">
          <h2 class="sp-admin-card-title">Onboarding progress</h2>
          @if (s.onboardingProgress; as op) {
            <dl class="sp-admin-detail-list">
              <div>
                <dt>Status</dt>
                <dd>
                  <sp-admin-badge [tone]="op.isComplete ? 'success' : 'info'">{{ op.isComplete ? 'Complete' : 'In progress' }}</sp-admin-badge>
                </dd>
              </div>
              <div>
                <dt>Progress</dt>
                <dd>{{ op.percentageComplete }}%</dd>
              </div>
              <div>
                <dt>Steps completed</dt>
                <dd>{{ op.completedStepKeys.length }}</dd>
              </div>
              @if (op.currentStepKey) {
                <div>
                  <dt>Current step</dt>
                  <dd><code class="sp-admin-code-pill">{{ op.currentStepKey }}</code></dd>
                </div>
              }
              @if (op.preliminaryCefrLevel) {
                <div>
                  <dt>Preliminary CEFR</dt>
                  <dd>{{ op.preliminaryCefrLevel }}</dd>
                </div>
              }
              <div>
                <dt>Started</dt>
                <dd>{{ op.startedAt | date:'mediumDate' }}</dd>
              </div>
              @if (op.completedAt) {
                <div>
                  <dt>Completed</dt>
                  <dd>{{ op.completedAt | date:'mediumDate' }}</dd>
                </div>
              }
            </dl>
          } @else {
            <p class="sp-admin-table-empty">No onboarding progress recorded for this student.</p>
          }
        </sp-admin-card>

        <sp-admin-card>
          <div class="sp-admin-section-header-row">
            <h2 class="sp-admin-card-title">Usage policy</h2>
            <div class="sp-admin-row-actions">
              <button type="button" class="sp-admin-link-button" (click)="startAssignPolicy()">Assign policy</button>
              @if (effectivePolicy()?.isOverride) {
                <button type="button" class="sp-admin-danger-link" (click)="confirmRemovePolicy()">Reset to default</button>
              }
            </div>
          </div>
          @if (policyLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (policyError()) {
            <div class="sp-admin-alert-error">{{ policyError() }}</div>
          } @else if (effectivePolicy()) {
            @let ep = effectivePolicy()!;
            <dl class="sp-admin-detail-list">
              <div>
                <dt>Policy name</dt>
                <dd>{{ ep.policy.name }}</dd>
              </div>
              <div>
                <dt>Scope</dt>
                <dd>{{ ep.policy.scopeType }}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>
                  <sp-admin-badge [tone]="ep.isOverride ? 'primary' : 'neutral'">
                    {{ ep.isOverride ? 'Student override' : 'Global default' }}
                  </sp-admin-badge>
                </dd>
              </div>
              @if (ep.isOverride) {
                <div>
                  <dt>Assigned</dt>
                  <dd>{{ ep.assignedAt | date:'mediumDate' }}</dd>
                </div>
                @if (ep.reason) {
                  <div>
                    <dt>Reason</dt>
                    <dd class="sp-safe-text">{{ ep.reason }}</dd>
                  </div>
                }
              }
              <div>
                <dt>Active rules</dt>
                <dd>{{ ep.policy.rules.length }}</dd>
              </div>
            </dl>
          } @else {
            <p class="sp-admin-table-empty">No policy found. Set a global default policy to enforce limits.</p>
          }
        </sp-admin-card>

        <sp-admin-card>
          <h2 class="sp-admin-card-title">Learning memory</h2>
          @if (memoryLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (memoryError()) {
            <div class="sp-admin-alert-error">{{ memoryError() }}</div>
          } @else if (memory()) {
            @let mem = memory()!;
            <div class="sp-admin-memory">
              <div>
                <h3>Journey summary</h3>
                <p>{{ mem.journeySummary || 'No summary yet.' }}</p>
              </div>
              <div>
                <h3>Strong skills</h3>
                @if (mem.strongSkills.length) {
                  <ul>@for (skill of mem.strongSkills; track skill) { <li>{{ skill }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Weak skills</h3>
                @if (mem.weakSkills.length) {
                  <ul>@for (skill of mem.weakSkills; track skill) { <li>{{ skill }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Recurring mistakes</h3>
                @if (mem.recurringMistakes.length) {
                  <ul>@for (m of mem.recurringMistakes; track m) { <li>{{ m }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Next recommended focus</h3>
                @if (mem.nextRecommendedFocus.length) {
                  <ul>@for (f of mem.nextRecommendedFocus; track f) { <li>{{ f }}</li> }</ul>
                } @else { <p class="sp-admin-table-empty">None recorded.</p> }
              </div>
              <div>
                <h3>Covered scenarios</h3>
                <p>{{ mem.coveredScenarioCount }}</p>
              </div>
              <div class="sp-admin-wide">
                <h3>Skill profile</h3>
                @if (mem.skillProfile.length) {
                  <div class="sp-admin-skill-tags">
                    @for (skill of mem.skillProfile; track skill.skillKey) {
                      <sp-admin-badge [tone]="skill.isWeak ? 'warning' : 'success'">{{ skill.skillLabel }}</sp-admin-badge>
                    }
                  </div>
                } @else { <p class="sp-admin-table-empty">No skill profile yet.</p> }
              </div>
            </div>
          }
        </sp-admin-card>

        <!-- Readiness pool health (TODO-UI-02) -->
        <sp-admin-card class="sp-admin-wide" aria-label="Readiness pool health">
          <h2 class="sp-admin-card-title">Readiness pool health</h2>
          @if (poolHealthLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (poolHealthError()) {
            <sp-admin-alert variant="error">{{ poolHealthError() }}</sp-admin-alert>
          } @else if (poolHealth()) {
            @let ph = poolHealth()!;
            <div class="sp-admin-pool-grid">
              <div class="sp-admin-pool-source">
                <h3 class="sp-admin-pool-source-title">Today's lesson</h3>
                <div style="display:flex;align-items:flex-start;gap:16px;flex-wrap:wrap;margin-top:8px;">
                  <sp-admin-ring-metric
                    [pct]="lessonRingPct()"
                    label="Ready"
                    [sub]="ph.todayLesson.readyCount + ' / ' + ph.todayLesson.targetCount"
                    [tone]="ph.todayLesson.needsReplenishment ? 'amber' : 'green'"
                    [size]="64"
                    ariaLabel="Lesson pool ready ring" />
                  @if (lessonPoolBreakdown().length > 0) {
                    <div style="flex:1;min-width:180px;">
                      <sp-admin-breakdown-bars [items]="lessonPoolBreakdown()" [showPct]="false" />
                    </div>
                  }
                </div>
                <div style="margin-top:8px;">
                  <sp-admin-badge [tone]="ph.todayLesson.needsReplenishment ? 'warning' : 'success'">
                    {{ ph.todayLesson.needsReplenishment ? 'Needs replenishment' : 'Healthy' }}
                  </sp-admin-badge>
                </div>
              </div>
              <div class="sp-admin-pool-source">
                <h3 class="sp-admin-pool-source-title">Practice gym</h3>
                <div style="display:flex;align-items:flex-start;gap:16px;flex-wrap:wrap;margin-top:8px;">
                  <sp-admin-ring-metric
                    [pct]="gymRingPct()"
                    label="Ready"
                    [sub]="ph.practiceGym.readyCount + ' / ' + ph.practiceGym.targetCount"
                    [tone]="ph.practiceGym.needsReplenishment ? 'amber' : 'green'"
                    [size]="64"
                    ariaLabel="Gym pool ready ring" />
                  @if (gymPoolBreakdown().length > 0) {
                    <div style="flex:1;min-width:180px;">
                      <sp-admin-breakdown-bars [items]="gymPoolBreakdown()" [showPct]="false" />
                    </div>
                  }
                </div>
                <div style="margin-top:8px;">
                  <sp-admin-badge [tone]="ph.practiceGym.needsReplenishment ? 'warning' : 'success'">
                    {{ ph.practiceGym.needsReplenishment ? 'Needs replenishment' : 'Healthy' }}
                  </sp-admin-badge>
                </div>
              </div>
            </div>
          } @else {
            <p class="sp-admin-table-empty">Pool health data not available.</p>
          }
        </sp-admin-card>

        <sp-admin-card class="sp-admin-wide">
          <h2 class="sp-admin-card-title">Activity history</h2>
          @if (historyLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (historyError()) {
            <sp-admin-alert variant="error">{{ historyError() }}</sp-admin-alert>
          } @else if (history().length === 0) {
            <p class="sp-admin-table-empty">No activity attempts yet.</p>
          } @else {
            <sp-admin-table>
              <thead>
                <tr>
                  <th>Activity</th>
                  <th>Type</th>
                  <th>Score</th>
                  <th>Result</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                @for (item of history(); track item.attemptId) {
                  <tr>
                    <td class="sp-safe-text">{{ item.activityTitle }}</td>
                    <td>{{ item.activityType }}</td>
                    <td>{{ item.score !== null ? (item.score | number:'1.0-0') + '%' : (item.percentage !== null ? (item.percentage | number:'1.0-0') + '%' : '—') }}</td>
                    <td>
                      @if (item.passed !== null) {
                        <sp-admin-badge [tone]="item.passed ? 'success' : 'warning'">{{ item.passed ? 'Passed' : 'Not passed' }}</sp-admin-badge>
                      } @else if (item.completed !== null) {
                        <sp-admin-badge [tone]="item.completed ? 'success' : 'warning'">{{ item.completed ? 'Completed' : 'Incomplete' }}</sp-admin-badge>
                      } @else {
                        —
                      }
                    </td>
                    <td>{{ item.createdAt | date:'medium' }}</td>
                  </tr>
                }
              </tbody>
            </sp-admin-table>
          }
        </sp-admin-card>

        <sp-admin-card class="sp-admin-wide" aria-label="Audit history">
          <h2 class="sp-admin-card-title">Audit history</h2>
          @if (auditHistoryLoading()) {
            <div class="sp-admin-table-loading"><div class="sp-admin-spinner"></div></div>
          } @else if (auditHistoryError()) {
            <sp-admin-alert variant="error">{{ auditHistoryError() }}</sp-admin-alert>
          } @else if (auditHistory().length === 0) {
            <p class="sp-admin-table-empty">No admin actions recorded for this student.</p>
          } @else {
            <sp-admin-table>
              <thead>
                <tr>
                  <th>Action</th>
                  <th>Source</th>
                  <th>Actor</th>
                  <th>Reason</th>
                  <th>Value change</th>
                  <th>Details</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                @for (item of auditHistory(); track item.id) {
                  <tr>
                    <td>
                      <sp-admin-badge [tone]="item.source === 'AdminAuditLog' ? 'primary' : 'warning'">{{ item.action }}</sp-admin-badge>
                    </td>
                    <td class="sp-admin-table-muted">{{ item.source === 'AdminAuditLog' ? 'Audit' : 'Reset' }}</td>
                    <td class="sp-admin-table-muted">
                      @if (item.actorEmail) {
                        {{ item.actorEmail }}
                      } @else if (item.actorId) {
                        <code class="sp-admin-code-pill">{{ item.actorId | slice:0:8 }}…</code>
                      } @else {
                        —
                      }
                    </td>
                    <td class="sp-safe-text sp-admin-table-muted">{{ item.reason || '—' }}</td>
                    <td>
                      @if (item.oldValue || item.newValue) {
                        <span class="sp-admin-audit-change">
                          <span class="sp-admin-table-muted">{{ item.oldValue || '—' }}</span>
                          <span>&rarr;</span>
                          <span>{{ item.newValue || '—' }}</span>
                        </span>
                      } @else {
                        —
                      }
                    </td>
                    <td>
                      @if (item.details) {
                        @if ((item.details || '').length > 80) {
                          <button type="button" class="sp-admin-link-button" (click)="openAuditDetails(item)">View details</button>
                        } @else {
                          <code class="sp-admin-code-pill">{{ item.details }}</code>
                        }
                      } @else {
                        —
                      }
                    </td>
                    <td class="sp-admin-table-muted">{{ item.timestamp | date:'medium' }}</td>
                  </tr>
                }
              </tbody>
            </sp-admin-table>
          }
        </sp-admin-card>

        <!-- Danger zone -->
        <sp-admin-card class="sp-admin-wide" aria-label="Danger zone">
          <div class="sp-sd-danger-header">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24" style="flex-shrink:0;color:#DC2626"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
            <h2 class="sp-sd-danger-title">Danger zone</h2>
          </div>

          <div class="sp-sd-danger-rows">
            <!-- Reset data -->
            @if (s.lifecycleStage !== 'Archived') {
              <div class="sp-sd-danger-row">
                <div>
                  <div class="sp-sd-danger-row-label">Reset student data</div>
                  <div class="sp-sd-danger-row-sub">Delete activity history, placement results, or learning memory. Use for pilot re-runs or data corrections.</div>
                </div>
                <sp-admin-button variant="danger" appearance="ghost" size="sm" (click)="startResetData(s)">Reset data</sp-admin-button>
              </div>
            }

            <!-- Archive -->
            @if (s.lifecycleStage !== 'Archived') {
              <div class="sp-sd-danger-row">
                <div>
                  <div class="sp-sd-danger-row-label">Archive student</div>
                  <div class="sp-sd-danger-row-sub">Hide this student from the active list and revoke sign-in access. Reversible via Reactivate.</div>
                </div>
                <sp-admin-button variant="danger" appearance="ghost" size="sm" (click)="confirmArchive(s)">Archive</sp-admin-button>
              </div>
            }

            <!-- Reactivate (archived only) -->
            @if (s.lifecycleStage === 'Archived') {
              <div class="sp-sd-danger-row">
                <div>
                  <div class="sp-sd-danger-row-label">Reactivate student</div>
                  <div class="sp-sd-danger-row-sub">Restore access and set lifecycle stage back to Onboarding Required.</div>
                </div>
                <sp-admin-button appearance="ghost" size="sm" (click)="startLifecycleAction('reactivate', s)">Reactivate</sp-admin-button>
              </div>
            }

            <!-- No actions if only archived state but already showing reactivate above -->
            @if (s.lifecycleStage === 'Archived') {
              <p class="sp-sd-danger-note">
                Student is archived. Only Reactivate is available above. Reset data and further archive actions are disabled.
              </p>
            }
          </div>
        </sp-admin-card>

      </div>
    }

    </sp-admin-page-body>

    @if (student(); as s) {
      <sp-admin-slide-over
        [open]="prefsSlideOverOpen()"
        title="Student preferences"
        [subtitle]="s.email"
        size="md"
        (closed)="closePrefsSlideOver()"
      >
        @if (hasAnyPreference(s)) {
          <dl class="sp-admin-detail-list sp-adm-prefs-list">
            <div>
              <dt>Preferred name</dt>
              <dd>{{ s.preferredName || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Support language</dt>
              <dd>{{ s.supportLanguageName ? s.supportLanguageName + (s.supportLanguageCode ? ' (' + s.supportLanguageCode + ')' : '') : (s.supportLanguageCode || 'Not set') }}</dd>
            </div>
            <div>
              <dt>Difficulty preference</dt>
              <dd>{{ s.difficultyPreference || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Translation help</dt>
              <dd>{{ s.translationHelpPreference || 'Not set' }}</dd>
            </div>
            <div>
              <dt>Focus areas</dt>
              <dd>
                @if (s.focusAreas?.length) {
                  <ul class="sp-adm-prefs-list-ul">
                    @for (area of s.focusAreas; track area) { <li>{{ area }}</li> }
                  </ul>
                } @else { Not set }
              </dd>
            </div>
            @if (s.customFocusArea) {
              <div>
                <dt>Custom focus area</dt>
                <dd>{{ s.customFocusArea }}</dd>
              </div>
            }
            <div>
              <dt>Learning goals</dt>
              <dd>
                @if (s.learningGoals?.length) {
                  <ul class="sp-adm-prefs-list-ul">
                    @for (goal of s.learningGoals; track goal) { <li>{{ goal }}</li> }
                  </ul>
                } @else { Not set }
              </dd>
            </div>
            @if (s.customLearningGoal) {
              <div>
                <dt>Custom learning goal</dt>
                <dd>{{ s.customLearningGoal }}</dd>
              </div>
            }
            @if (s.learningPreferencesUpdatedAt) {
              <div>
                <dt>Last updated</dt>
                <dd>{{ s.learningPreferencesUpdatedAt | date:'mediumDate' }}</dd>
              </div>
            }
          </dl>
        } @else {
          <p class="sp-admin-table-empty">Student has not set any learning preferences yet.</p>
        }
      </sp-admin-slide-over>
    }

    @if (auditDetailsItem()) {
      <sp-admin-slide-over
        [open]="auditDetailsSlideOverOpen()"
        title="Audit details"
        size="md"
        (closed)="closeAuditDetails()"
      >
        @let item = auditDetailsItem()!;
        <dl class="sp-admin-detail-list sp-adm-prefs-list">
          <div><dt>Action</dt><dd>{{ item.action }}</dd></div>
          <div><dt>Source</dt><dd>{{ item.source }}</dd></div>
          @if (item.reason) { <div><dt>Reason</dt><dd class="sp-safe-text">{{ item.reason }}</dd></div> }
          @if (item.correlationId) { <div><dt>Correlation ID</dt><dd><code class="sp-admin-code-pill">{{ item.correlationId }}</code></dd></div> }
          @if (item.details) { <div><dt>Details</dt><dd><pre class="sp-admin-audit-pre">{{ item.details }}</pre></dd></div> }
          <div><dt>Timestamp</dt><dd>{{ item.timestamp | date:'medium' }}</dd></div>
        </dl>
      </sp-admin-slide-over>
    }

    <sp-admin-slide-over
      [open]="assigningPolicy()"
      title="Assign usage policy"
      [subtitle]="student()?.email ?? ''"
      size="sm"
      [stackIndex]="1"
      (closed)="cancelAssignPolicy()"
    >
      <form (ngSubmit)="saveAssignPolicy()" class="sp-admin-edit-grid">
        <label class="sp-admin-wide">
          <span>Policy</span>
          <select class="sp-input" [(ngModel)]="assignPolicyForm.policyId" name="policyId">
            <option value="">Select a policy...</option>
            @for (p of availablePolicies(); track p.id) {
              <option [value]="p.id">{{ p.name }}{{ p.isDefault ? ' (default)' : '' }}</option>
            }
          </select>
        </label>
        <label class="sp-admin-wide">
          <span>Reason (optional)</span>
          <textarea class="sp-input" rows="2" [(ngModel)]="assignPolicyForm.reason" name="reason"
            placeholder="Why is this policy being assigned?"></textarea>
        </label>
        @if (assignPolicyError()) {
          <div class="sp-admin-alert-error sp-admin-wide">{{ assignPolicyError() }}</div>
        }
        <div class="sp-admin-modal-actions sp-admin-wide">
          <button type="button" class="sp-button-ghost" (click)="cancelAssignPolicy()">Cancel</button>
          <button type="submit" class="sp-admin-btn-primary"
            [disabled]="savingAssignPolicy() || !assignPolicyForm.policyId">
            {{ savingAssignPolicy() ? 'Saving...' : 'Assign policy' }}
          </button>
        </div>
      </form>
    </sp-admin-slide-over>

    @if (editing(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelEdit()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="editStudentTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="editStudentTitle">Edit student</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelEdit()" aria-label="Close edit student">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        <form (ngSubmit)="saveEdit()" class="sp-admin-edit-grid">
          <label>
            <span>First name</span>
            <input class="sp-input" [(ngModel)]="editForm.firstName" name="firstName" />
          </label>
          <label>
            <span>Last name</span>
            <input class="sp-input" [(ngModel)]="editForm.lastName" name="lastName" />
          </label>
          <label class="sp-admin-wide">
            <span>Display name</span>
            <input class="sp-input" [(ngModel)]="editForm.displayName" name="displayName" />
          </label>
          <label class="sp-admin-wide">
            <span>Career or work context</span>
            <input class="sp-input" [(ngModel)]="editForm.careerContext" name="careerContext" />
          </label>
          <label class="sp-admin-wide">
            <span>Learning goal</span>
            <input class="sp-input" [(ngModel)]="editForm.learningGoal" name="learningGoal" />
          </label>
          <label class="sp-admin-wide">
            <span>Learning goal description</span>
            <textarea class="sp-input" rows="3" [(ngModel)]="editForm.learningGoalDescription" name="learningGoalDescription"></textarea>
          </label>
          <label class="sp-admin-wide">
            <span>Difficult situations</span>
            <textarea class="sp-input" rows="3" [(ngModel)]="editForm.difficultSituationsText" name="difficultSituationsText"></textarea>
          </label>
          <label>
            <span>Preferred duration</span>
            <select class="sp-input" [(ngModel)]="editForm.preferredSessionDurationMinutes" name="preferredSessionDurationMinutes">
              <option [ngValue]="null">Not set</option>
              @for (duration of sessionDurations; track duration) {
                <option [ngValue]="duration">{{ duration }} minutes</option>
              }
            </select>
          </label>
          <label>
            <span>Experience level</span>
            <select class="sp-input" [(ngModel)]="editForm.professionalExperienceLevel" name="professionalExperienceLevel">
              <option [ngValue]="null">Not set</option>
              @for (level of experienceLevels; track level.value) {
                <option [ngValue]="level.value">{{ level.label }}</option>
              }
            </select>
          </label>
          <label>
            <span>Role familiarity</span>
            <select class="sp-input" [(ngModel)]="editForm.roleFamiliarity" name="roleFamiliarity">
              <option [ngValue]="null">Not set</option>
              @for (level of familiarityLevels; track level.value) {
                <option [ngValue]="level.value">{{ level.label }}</option>
              }
            </select>
          </label>
          @if (editError()) {
            <div class="sp-admin-alert-error sp-admin-wide">{{ editError() }}</div>
          }
          <div class="sp-admin-modal-actions sp-admin-wide">
            <button type="button" class="sp-button-ghost" (click)="cancelEdit()">Cancel</button>
            <button type="submit" class="sp-admin-btn-primary" [disabled]="savingEdit()">
              {{ savingEdit() ? 'Saving...' : 'Save changes' }}
            </button>
          </div>
        </form>
      </section>
    }

    @if (resetting(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelResetPassword()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="resetPasswordTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="resetPasswordTitle">Reset password</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelResetPassword()" aria-label="Close reset password">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        @if (resetSuccessPassword()) {
          <div class="sp-admin-edit-grid">
            <div class="sp-admin-wide sp-admin-alert-success">
              Password reset. Share this temporary password with the student
              securely — it will not be shown again.
            </div>
            <label class="sp-admin-wide">
              <span>New temporary password</span>
              <input class="sp-input" [value]="resetSuccessPassword()" readonly />
            </label>
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-admin-btn-primary" (click)="cancelResetPassword()">Done</button>
            </div>
          </div>
        } @else {
          <form (ngSubmit)="saveResetPassword()" class="sp-admin-edit-grid">
            <label class="sp-admin-wide">
              <span>New temporary password</span>
              <input class="sp-input" [(ngModel)]="resetForm.newPassword" name="newPassword"
                placeholder="At least 8 characters, with a digit" autocomplete="off" />
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetForm.mustChangePassword" name="mustChangePassword" style="margin:0;" />
              <span style="margin:0;">Require password change on next login</span>
            </label>
            @if (resetError()) {
              <div class="sp-admin-alert-error sp-admin-wide">{{ resetError() }}</div>
            }
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-button-ghost" (click)="generateResetPassword()">Generate password</button>
              <button type="button" class="sp-button-ghost" (click)="cancelResetPassword()">Cancel</button>
              <button type="submit" class="sp-admin-btn-primary" [disabled]="savingReset() || resetForm.newPassword.length < 8">
                {{ savingReset() ? 'Saving...' : 'Reset password' }}
              </button>
            </div>
          </form>
        }
      </section>
    }

    @if (resettingData(); as student) {
      <div class="sp-admin-modal-backdrop" (click)="cancelResetData()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="resetDataTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="resetDataTitle">Reset student data</h2>
            <p>{{ student.email }}</p>
          </div>
          <button type="button" (click)="cancelResetData()" aria-label="Close reset data">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        @if (resetDataResult(); as result) {
          <div class="sp-admin-edit-grid">
            <div class="sp-admin-wide sp-admin-alert-success">
              Reset complete. New stage: {{ result.newStage }} (was {{ result.previousStage }}).
            </div>
            <div class="sp-admin-wide sp-admin-table-muted">
              Cleared: onboarding={{ result.clearedItems.onboardingAnswers }},
              placement={{ result.clearedItems.placementResults }},
              courses/sessions={{ result.clearedItems.coursesAndSessions }},
              attempts={{ result.clearedItems.activityAttempts }},
              vocabulary={{ result.clearedItems.vocabulary }},
              memory={{ result.clearedItems.learningMemory }},
              audio files deleted={{ result.clearedItems.audioFilesDeleted }},
              progress={{ result.clearedItems.progressData }}.
            </div>
            <div class="sp-admin-wide sp-admin-table-muted">Reset log: {{ result.resetLogId }}</div>
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-admin-btn-primary" (click)="cancelResetData()">Done</button>
            </div>
          </div>
        } @else {
          <form (ngSubmit)="saveResetData()" class="sp-admin-edit-grid">
            <label class="sp-admin-wide">
              <span>Preset</span>
              <select class="sp-input" [(ngModel)]="resetDataForm.preset" name="preset" (change)="applyResetPreset()">
                @for (preset of resetPresets; track preset.key) {
                  <option [ngValue]="preset.key">{{ preset.label }}</option>
                }
              </select>
            </label>

            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearOnboardingAnswers" name="clearOnboardingAnswers" style="margin:0;" />
              <span style="margin:0;">Clear onboarding answers</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearPlacementResults" name="clearPlacementResults" style="margin:0;" />
              <span style="margin:0;">Clear placement results</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearCoursesAndSessions" name="clearCoursesAndSessions" style="margin:0;" />
              <span style="margin:0;">Clear courses and sessions</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearActivityAttempts" name="clearActivityAttempts" style="margin:0;" />
              <span style="margin:0;">Clear activity attempts</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearVocabulary" name="clearVocabulary" style="margin:0;" />
              <span style="margin:0;">Clear vocabulary</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearLearningMemory" name="clearLearningMemory" style="margin:0;" />
              <span style="margin:0;">Clear learning memory</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearAudioFiles" name="clearAudioFiles" style="margin:0;" />
              <span style="margin:0;">Delete audio files</span>
            </label>
            <label class="sp-admin-wide" style="display:flex;align-items:center;gap:8px;flex-direction:row;">
              <input type="checkbox" [(ngModel)]="resetDataForm.clearProgressData" name="clearProgressData" style="margin:0;" />
              <span style="margin:0;">Recalculate progress data</span>
            </label>

            <label class="sp-admin-wide">
              <span>Reason (required)</span>
              <textarea class="sp-input" rows="2" [(ngModel)]="resetDataForm.reason" name="reason"
                placeholder="Why is this reset being performed?"></textarea>
            </label>

            <label class="sp-admin-wide">
              <span>Type the student's email to confirm: {{ student.email }}</span>
              <input class="sp-input" [(ngModel)]="resetDataForm.confirmEmail" name="confirmEmail" autocomplete="off" />
            </label>

            @if (resetDataError()) {
              <div class="sp-admin-alert-error sp-admin-wide">{{ resetDataError() }}</div>
            }
            <div class="sp-admin-modal-actions sp-admin-wide">
              <button type="button" class="sp-button-ghost" (click)="cancelResetData()">Cancel</button>
              <button type="submit" class="sp-admin-btn-primary"
                [disabled]="savingResetData() || resetDataForm.confirmEmail !== student.email || !resetDataForm.reason.trim()">
                {{ savingResetData() ? 'Resetting...' : 'Reset data' }}
              </button>
            </div>
          </form>
        }
      </section>
    }
    <sp-admin-slide-over
      [open]="settingCefr()"
      title="Set CEFR level"
      [subtitle]="student()?.email ?? ''"
      size="sm"
      [stackIndex]="1"
      (closed)="cancelSetCefr()"
    >
      <form (ngSubmit)="saveSetCefr()" class="sp-admin-edit-grid">
        <label class="sp-admin-wide">
          <span>CEFR level</span>
          <select class="sp-input" [(ngModel)]="cefrForm.cefrLevel" name="cefrLevel">
            @for (level of cefrLevels; track level.value) {
              <option [value]="level.value">{{ level.label }}</option>
            }
          </select>
        </label>
        <label class="sp-admin-wide">
          <span>Reason (optional)</span>
          <textarea class="sp-input" rows="2" [(ngModel)]="cefrForm.reason" name="reason"
            placeholder="Why is this CEFR level being set?"></textarea>
        </label>
        @if (cefrError()) {
          <div class="sp-admin-alert-error sp-admin-wide">{{ cefrError() }}</div>
        }
        <div class="sp-admin-modal-actions sp-admin-wide">
          <button type="button" class="sp-button-ghost" (click)="cancelSetCefr()">Cancel</button>
          <button type="submit" class="sp-admin-btn-primary" [disabled]="savingCefr()">
            {{ savingCefr() ? 'Saving...' : 'Save' }}
          </button>
        </div>
      </form>
    </sp-admin-slide-over>
    @if (lifecycleAction()) {
      <div class="sp-admin-modal-backdrop" (click)="cancelLifecycleAction()"></div>
      <section class="sp-admin-modal" role="dialog" aria-modal="true" aria-labelledby="lifecycleActionTitle">
        <div class="sp-admin-modal-header">
          <div>
            <h2 id="lifecycleActionTitle">{{ lifecycleActionTitle() }}</h2>
            @if (student(); as s) { <p class="sp-safe-text">{{ s.email }}</p> }
          </div>
          <button type="button" (click)="cancelLifecycleAction()" aria-label="Close">
            <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>
          </button>
        </div>
        <div class="sp-admin-edit-grid">
          <p class="sp-admin-wide">{{ lifecycleActionDescription() }}</p>
          @if (lifecycleActionError()) {
            <div class="sp-admin-alert-error sp-admin-wide">{{ lifecycleActionError() }}</div>
          }
          <div class="sp-admin-modal-actions sp-admin-wide">
            <button type="button" class="sp-button-ghost" (click)="cancelLifecycleAction()">Cancel</button>
            <button type="button" class="sp-admin-btn-primary" [disabled]="savingLifecycleAction()" (click)="confirmLifecycleAction()">
              {{ savingLifecycleAction() ? 'Saving...' : lifecycleActionTitle() }}
            </button>
          </div>
        </div>
      </section>
    }
  `,
  styles: [`
    /* ── Hero ── */
    .sp-sd-hero{display:flex;align-items:flex-start;gap:18px;background:var(--sp-admin-surface,#fff);border:1px solid var(--sp-admin-border,#ECE9F5);border-radius:14px;padding:24px;margin-bottom:24px;flex-wrap:wrap;box-shadow:0 1px 2px rgba(33,27,54,.06);}
    .sp-sd-ava{width:56px;height:56px;border-radius:14px;display:flex;align-items:center;justify-content:center;font-size:18px;font-weight:800;color:#fff;flex-shrink:0;letter-spacing:-.02em;}
    .sp-sd-hero-body{flex:1;min-width:0;}
    .sp-sd-hero-name{font-size:20px;font-weight:800;color:var(--sp-admin-text,#211B36);line-height:1.2;margin-bottom:3px;letter-spacing:-0.025em;}
    .sp-sd-hero-email{font-size:13.5px;color:var(--sp-admin-text-muted,#8B85A0);margin-bottom:10px;}
    .sp-sd-hero-badges{display:flex;flex-wrap:wrap;gap:7px;align-items:center;}
    .sp-sd-hero-chip{font-size:12px;font-weight:600;color:var(--sp-admin-text-muted,#64748B);background:var(--sp-admin-surface-subtle,#FBFAFE);border:1px solid var(--sp-admin-border,#ECE9F5);border-radius:99px;padding:2px 10px;}
    .sp-sd-hero-actions{display:flex;flex-wrap:wrap;gap:8px;align-items:flex-start;margin-left:auto;}
    @media(max-width:800px){.sp-sd-hero{flex-direction:column;}.sp-sd-hero-actions{margin-left:0;}}

    /* ── KPI strip (now uses sp-admin-kpi-card) ── */
    .sp-admin-kpi-strip{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:20px;}
    @media(max-width:900px){.sp-admin-kpi-strip{grid-template-columns:repeat(2,1fr);}}
    @media(max-width:600px){.sp-admin-kpi-strip{grid-template-columns:1fr;}}
    .sp-admin-pool-grid{display:grid;grid-template-columns:1fr 1fr;gap:24px;}
    @media(max-width:700px){.sp-admin-pool-grid{grid-template-columns:1fr;}}
    .sp-admin-pool-source-title{font-size:13px;font-weight:800;color:var(--sp-admin-text-muted,#64748B);margin-bottom:12px;}
    .sp-admin-header-row{display:flex;align-items:start;justify-content:space-between;gap:12px;flex-wrap:wrap;}
    .sp-admin-section-header-row{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:14px;}
    .sp-admin-section-header-row .sp-admin-card-title{margin-bottom:0;}
    .sp-admin-back-link{display:inline-block;margin-bottom:8px;font-size:13px;font-weight:700;color:var(--sp-admin-primary,#5B4BE8);text-decoration:none;}
    .sp-admin-row-actions{display:flex;align-items:center;gap:10px;white-space:nowrap;}
    .sp-admin-link-button,.sp-admin-danger-link{border:none;background:none;padding:0;font:inherit;font-size:12.5px;font-weight:800;cursor:pointer;}
    .sp-admin-link-button{color:var(--sp-admin-primary,#5B4BE8);}
    .sp-admin-danger-link{color:#DC2626;}
    .sp-admin-detail-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;}
    .sp-admin-detail-card{padding:20px;}
    .sp-admin-card-title{font-size:16px;font-weight:800;color:#0F172A;margin-bottom:14px;}
    .sp-admin-detail-list{display:grid;gap:12px;}
    .sp-admin-detail-list dt{font-size:12px;font-weight:800;color:#64748B;margin-bottom:2px;}
    .sp-admin-detail-list dd{font-size:14px;color:#0F172A;}
    .sp-admin-memory{display:grid;gap:16px;}
    .sp-admin-memory h3{font-size:12px;font-weight:800;color:#64748B;margin-bottom:6px;}
    .sp-admin-memory p{font-size:14px;color:#0F172A;}
    .sp-admin-memory ul{margin:0;padding-left:18px;font-size:14px;color:#0F172A;}
    .sp-admin-skill-tags{display:flex;flex-wrap:wrap;gap:8px;}
    .sp-admin-wide{grid-column:1/-1;}
    .sp-admin-modal-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.38);z-index:120;}
    .sp-admin-modal{position:fixed;right:24px;top:76px;bottom:24px;z-index:121;width:min(720px,calc(100vw - 48px));overflow:auto;background:#fff;border:1px solid #E2E8F0;border-radius:14px;box-shadow:0 20px 60px rgba(15,23,42,.22);}
    .sp-admin-modal-header{display:flex;align-items:start;justify-content:space-between;gap:12px;padding:20px 22px;border-bottom:1px solid #E2E8F0;}
    .sp-admin-modal-header h2{font-size:18px;font-weight:800;color:#0F172A;}
    .sp-admin-modal-header p{font-size:13px;color:#64748B;margin-top:2px;}
    .sp-admin-modal-header button{width:34px;height:34px;border-radius:9px;border:1px solid #E2E8F0;background:#fff;color:#64748B;cursor:pointer;display:grid;place-items:center;}
    .sp-admin-edit-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;padding:22px;}
    .sp-admin-edit-grid label span{display:block;margin-bottom:6px;font-size:12px;font-weight:800;color:#475569;}
    .sp-admin-modal-actions{display:flex;justify-content:flex-end;gap:10px;padding-top:8px;}
    .sp-adm-prefs-list{display:grid;gap:14px;}
    .sp-adm-prefs-list dt{font-size:12px;font-weight:800;color:#64748B;margin-bottom:2px;}
    .sp-adm-prefs-list dd{font-size:14px;color:#0F172A;}
    .sp-adm-prefs-list-ul{margin:0;padding-left:18px;font-size:14px;color:#0F172A;}
    .sp-admin-cefr-row{display:flex;align-items:center;gap:8px;}
    .sp-admin-cefr-hint{font-size:11px;color:#94A3B8;margin-top:4px;}
    .sp-admin-audit-change{display:flex;align-items:center;gap:4px;font-size:13px;}
    .sp-admin-audit-pre{font-size:12px;color:#334155;white-space:pre-wrap;word-break:break-all;margin:0;background:#F8FAFC;border-radius:6px;padding:8px;}
    @media(max-width:900px){
      .sp-admin-detail-grid{grid-template-columns:1fr;}
    }
    @media(max-width:720px){
      .sp-admin-modal{left:12px;right:12px;top:68px;bottom:12px;width:auto;}
      .sp-admin-edit-grid{grid-template-columns:1fr;padding:18px;}
    }

    /* ── Danger zone ── */
    .sp-sd-danger-header{display:flex;align-items:center;gap:8px;margin-bottom:16px;}
    .sp-sd-danger-title{font-size:15px;font-weight:800;color:#DC2626;margin:0;}
    .sp-sd-danger-rows{display:flex;flex-direction:column;gap:0;}
    .sp-sd-danger-row{display:flex;align-items:center;justify-content:space-between;gap:16px;padding:16px 0;border-top:1px solid var(--sp-admin-border,#ECE9F5);}
    .sp-sd-danger-row:first-child{border-top:none;}
    .sp-sd-danger-row-label{font-size:14px;font-weight:700;color:var(--sp-admin-text,#0F172A);margin-bottom:3px;}
    .sp-sd-danger-row-sub{font-size:12.5px;color:var(--sp-admin-text-muted,#64748B);}
    .sp-sd-danger-note{font-size:12px;color:var(--sp-admin-text-muted,#64748B);margin-top:12px;font-style:italic;}
  `],
})
export class AdminStudentDetailComponent implements OnInit {
  student = signal<AdminStudentDetail | null>(null);
  loading = signal(true);
  error = signal('');

  memory = signal<AdminStudentLearningMemory | null>(null);
  memoryLoading = signal(true);
  memoryError = signal('');

  history = signal<AdminActivityHistoryItem[]>([]);
  historyLoading = signal(true);
  historyError = signal('');

  auditHistory = signal<StudentAuditHistoryItem[]>([]);
  auditHistoryLoading = signal(true);
  auditHistoryError = signal('');
  auditDetailsSlideOverOpen = signal(false);
  auditDetailsItem = signal<StudentAuditHistoryItem | null>(null);

  editing = signal<AdminStudentDetail | null>(null);
  savingEdit = signal(false);
  editError = signal('');
  editForm: StudentEditForm = this.emptyForm();

  resetting = signal<AdminStudentDetail | null>(null);
  savingReset = signal(false);
  resetError = signal('');
  resetSuccessPassword = signal('');
  resetForm = { newPassword: '', mustChangePassword: true };

  sendingResetLink = signal(false);
  resetLinkSent = signal(false);

  resettingData = signal<AdminStudentDetail | null>(null);
  savingResetData = signal(false);
  resetDataError = signal('');
  resetDataResult = signal<ResetStudentResponse | null>(null);
  resetDataForm = this.emptyResetDataForm();

  readonly resetPresets: { key: string; label: string; flags: Omit<ResetStudentRequest, 'reason'> }[] = [
    {
      key: 'fixPassword',
      label: 'Fix password',
      flags: { targetStage: 'PasswordChangeRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'restartOnboarding',
      label: 'Restart onboarding',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'restartPlacement',
      label: 'Restart placement',
      flags: { targetStage: 'PlacementRequired', clearOnboardingAnswers: false, clearPlacementResults: true, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'resetCourseOnly',
      label: 'Reset course only',
      flags: { targetStage: 'CourseReady', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: true, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
    {
      key: 'fullCleanReset',
      label: 'Full clean reset',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: true, clearPlacementResults: true, clearCoursesAndSessions: true, clearActivityAttempts: true, clearVocabulary: true, clearLearningMemory: true, clearAudioFiles: true, clearProgressData: true },
    },
    {
      key: 'custom',
      label: 'Custom',
      flags: { targetStage: 'OnboardingRequired', clearOnboardingAnswers: false, clearPlacementResults: false, clearCoursesAndSessions: false, clearActivityAttempts: false, clearVocabulary: false, clearLearningMemory: false, clearAudioFiles: false, clearProgressData: false },
    },
  ];

  readonly sessionDurations = [15, 20, 30, 45, 60];
  readonly experienceLevels = [
    { value: 0, label: 'No professional experience' },
    { value: 1, label: 'Entry level or graduate' },
    { value: 2, label: 'Junior, 0-2 years' },
    { value: 3, label: 'Mid-level, 2-5 years' },
    { value: 4, label: 'Senior, 5-10 years' },
    { value: 5, label: 'Lead or manager, 10+ years' },
  ];
  readonly familiarityLevels = [
    { value: 0, label: 'New to role' },
    { value: 1, label: 'Understands basics' },
    { value: 2, label: 'Currently working in role' },
    { value: 3, label: 'Experienced in role' },
    { value: 4, label: 'Manages or trains others' },
  ];

  poolHealth = signal<StudentReadinessPoolHealth | null>(null);
  poolHealthLoading = signal(true);
  poolHealthError = signal('');

  readonly lessonRingPct = computed(() => {
    const ph = this.poolHealth();
    if (!ph) return 0;
    const t = ph.todayLesson.targetCount;
    return t > 0 ? Math.round((ph.todayLesson.readyCount / t) * 100) : 0;
  });

  readonly gymRingPct = computed(() => {
    const ph = this.poolHealth();
    if (!ph) return 0;
    const t = ph.practiceGym.targetCount;
    return t > 0 ? Math.round((ph.practiceGym.readyCount / t) * 100) : 0;
  });

  readonly lessonPoolBreakdown = computed<BreakdownBarItem[]>(() => {
    const ph = this.poolHealth();
    if (!ph) return [];
    const l = ph.todayLesson;
    const tot = l.targetCount || 1;
    const rows: BreakdownBarItem[] = [
      { label: 'Ready', value: l.readyCount, pct: Math.round((l.readyCount / tot) * 100), tone: 'green' },
      { label: 'Queued', value: l.queuedOrGeneratingCount, pct: Math.round((l.queuedOrGeneratingCount / tot) * 100), tone: 'indigo' },
      { label: 'Shortfall', value: l.shortfallCount, pct: Math.round((l.shortfallCount / tot) * 100), tone: 'amber' },
      { label: 'Failed', value: l.failedCount, pct: Math.round((l.failedCount / tot) * 100), tone: 'danger' },
      { label: 'Stale', value: l.staleCount, pct: Math.round((l.staleCount / tot) * 100), tone: 'slate' },
    ];
    return rows.filter(i => i.value > 0);
  });

  readonly gymPoolBreakdown = computed<BreakdownBarItem[]>(() => {
    const ph = this.poolHealth();
    if (!ph) return [];
    const g = ph.practiceGym;
    const tot = g.targetCount || 1;
    const rows: BreakdownBarItem[] = [
      { label: 'Ready', value: g.readyCount, pct: Math.round((g.readyCount / tot) * 100), tone: 'green' },
      { label: 'Queued', value: g.queuedOrGeneratingCount, pct: Math.round((g.queuedOrGeneratingCount / tot) * 100), tone: 'indigo' },
      { label: 'Shortfall', value: g.shortfallCount, pct: Math.round((g.shortfallCount / tot) * 100), tone: 'amber' },
      { label: 'Failed', value: g.failedCount, pct: Math.round((g.failedCount / tot) * 100), tone: 'danger' },
      { label: 'Stale', value: g.staleCount, pct: Math.round((g.staleCount / tot) * 100), tone: 'slate' },
    ];
    return rows.filter(i => i.value > 0);
  });

  readonly lifecycleLabel = lifecycleLabel;
  readonly lifecycleTone = lifecycleTone;
  readonly onboardingLabel = onboardingLabel;
  readonly onboardingTone = onboardingTone;
  readonly eventLevelLabel = eventLevelLabel;

  poolHealthLabel(): string {
    const ph = this.poolHealth();
    if (this.poolHealthLoading()) return '…';
    if (this.poolHealthError() || !ph) return 'Unknown';
    const todayOk = !ph.todayLesson.needsReplenishment;
    const gymOk = !ph.practiceGym.needsReplenishment;
    if (todayOk && gymOk) return 'Healthy';
    if (!todayOk && !gymOk) return 'Both need fill';
    return todayOk ? 'Gym needs fill' : 'Lesson needs fill';
  }

  poolHealthTone(): 'success' | 'warning' | 'neutral' {
    const ph = this.poolHealth();
    if (this.poolHealthLoading() || this.poolHealthError() || !ph) return 'neutral';
    return (ph.todayLesson.needsReplenishment || ph.practiceGym.needsReplenishment) ? 'warning' : 'success';
  }

  effectivePolicy = signal<StudentEffectivePolicy | null>(null);
  policyLoading = signal(true);
  policyError = signal('');

  availablePolicies = signal<UsagePolicy[]>([]);
  assigningPolicy = signal(false);
  savingAssignPolicy = signal(false);
  assignPolicyError = signal('');
  assignPolicyForm: { policyId: string; reason: string } = { policyId: '', reason: '' };

  prefsSlideOverOpen = signal(false);

  settingCefr = signal(false);
  savingCefr = signal(false);
  cefrError = signal('');
  cefrForm: { cefrLevel: string; reason: string } = { cefrLevel: '', reason: '' };

  readonly cefrLevels = [
    { value: '', label: 'Clear / Not set' },
    { value: 'A1', label: 'A1' },
    { value: 'A2', label: 'A2' },
    { value: 'B1', label: 'B1' },
    { value: 'B2', label: 'B2' },
    { value: 'C1', label: 'C1' },
    { value: 'C2', label: 'C2' },
  ];

  startSetCefr(student: AdminStudentDetail): void {
    this.cefrForm = { cefrLevel: student.cefrLevel ?? '', reason: '' };
    this.cefrError.set('');
    this.settingCefr.set(true);
  }

  cancelSetCefr(): void {
    this.settingCefr.set(false);
    this.cefrError.set('');
  }

  saveSetCefr(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId) return;

    this.savingCefr.set(true);
    this.cefrError.set('');

    const cefrLevel = this.cefrForm.cefrLevel.trim() || null;
    const reason = this.cefrForm.reason.trim() || undefined;

    this.adminApi.updateStudentCefr(studentId, cefrLevel, reason).subscribe({
      next: () => {
        this.savingCefr.set(false);
        this.settingCefr.set(false);
        this.loadStudent(studentId);
        this.toast.success(cefrLevel ? `CEFR level set to ${cefrLevel}` : 'CEFR level cleared');
      },
      error: (err: { error?: { error?: string } }) => {
        this.savingCefr.set(false);
        this.cefrError.set(err.error?.error ?? 'Could not update CEFR level.');
      },
    });
  }

  lifecycleAction = signal<'reactivate' | 'pause' | 'unpause' | null>(null);
  savingLifecycleAction = signal(false);
  lifecycleActionError = signal('');

  lifecycleActionTitle(): string {
    switch (this.lifecycleAction()) {
      case 'reactivate': return 'Reactivate student';
      case 'pause': return 'Pause student';
      case 'unpause': return 'Unpause student';
      default: return '';
    }
  }

  lifecycleActionDescription(): string {
    switch (this.lifecycleAction()) {
      case 'reactivate': return 'This will reactivate the student and set their lifecycle stage to Onboarding Required.';
      case 'pause': return 'This will pause the student. They will not be able to progress until unpaused.';
      case 'unpause': return 'This will unpause the student and set their lifecycle stage to Onboarding Required.';
      default: return '';
    }
  }

  startLifecycleAction(action: 'reactivate' | 'pause' | 'unpause', _student: AdminStudentDetail): void {
    this.lifecycleAction.set(action);
    this.lifecycleActionError.set('');
    this.savingLifecycleAction.set(false);
  }

  cancelLifecycleAction(): void {
    this.lifecycleAction.set(null);
    this.lifecycleActionError.set('');
  }

  confirmLifecycleAction(): void {
    const action = this.lifecycleAction();
    const studentId = this.student()?.studentProfileId;
    if (!action || !studentId) return;

    this.savingLifecycleAction.set(true);
    this.lifecycleActionError.set('');

    let call$;
    if (action === 'reactivate') call$ = this.adminApi.reactivateStudent(studentId);
    else if (action === 'pause') call$ = this.adminApi.pauseStudent(studentId);
    else call$ = this.adminApi.unpauseStudent(studentId);

    call$.subscribe({
      next: () => {
        this.savingLifecycleAction.set(false);
        this.lifecycleAction.set(null);
        this.loadStudent(studentId);
        this.toast.success(`Student ${action}d successfully`);
      },
      error: (err: { error?: { error?: string } }) => {
        this.savingLifecycleAction.set(false);
        this.lifecycleActionError.set(err.error?.error ?? `Could not ${action} student.`);
      },
    });
  }

  openPrefsSlideOver(): void { this.prefsSlideOverOpen.set(true); }
  closePrefsSlideOver(): void { this.prefsSlideOverOpen.set(false); }

  hasAnyPreference(s: AdminStudentDetail): boolean {
    return !!(
      s.preferredName ||
      s.supportLanguageCode ||
      s.supportLanguageName ||
      s.difficultyPreference ||
      s.translationHelpPreference ||
      s.focusAreas?.length ||
      s.customFocusArea ||
      s.learningGoals?.length ||
      s.customLearningGoal
    );
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private adminApi: AdminApiService,
    private toast: ToastService,
    private governance: UsageGovernanceService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Missing student id.');
      this.loading.set(false);
      return;
    }
    this.loadStudent(id);
    this.loadMemory(id);
    this.loadHistory(id);
    this.loadAuditHistory(id);
    this.loadPolicy(id);
    this.loadPoolHealth(id);
  }

  private loadStudent(id: string): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.getStudent(id).subscribe({
      next: detail => { this.student.set(detail); this.loading.set(false); },
      error: (err) => {
        const status = err?.status;
        this.error.set(status === 404 ? 'Student not found.' : 'Could not load student.');
        this.loading.set(false);
      },
    });
  }

  private loadPoolHealth(id: string): void {
    this.poolHealthLoading.set(true);
    this.poolHealthError.set('');
    this.adminApi.getStudentReadinessPoolHealth(id).subscribe({
      next: ph => { this.poolHealth.set(ph); this.poolHealthLoading.set(false); },
      error: () => { this.poolHealthError.set('Could not load pool health.'); this.poolHealthLoading.set(false); },
    });
  }

  private loadMemory(id: string): void {
    this.memoryLoading.set(true);
    this.memoryError.set('');
    this.adminApi.getStudentLearningMemory(id).subscribe({
      next: mem => { this.memory.set(mem); this.memoryLoading.set(false); },
      error: () => { this.memoryError.set('Could not load learning memory.'); this.memoryLoading.set(false); },
    });
  }

  private loadHistory(id: string): void {
    this.historyLoading.set(true);
    this.historyError.set('');
    this.adminApi.getActivityHistory(id).subscribe({
      next: items => { this.history.set(items); this.historyLoading.set(false); },
      error: () => { this.historyError.set('Could not load activity history.'); this.historyLoading.set(false); },
    });
  }

  private loadAuditHistory(id: string): void {
    this.auditHistoryLoading.set(true);
    this.auditHistoryError.set('');
    this.adminApi.getStudentAuditHistory(id).subscribe({
      next: items => { this.auditHistory.set(items); this.auditHistoryLoading.set(false); },
      error: () => { this.auditHistoryError.set('Could not load audit history.'); this.auditHistoryLoading.set(false); },
    });
  }

  openAuditDetails(item: StudentAuditHistoryItem): void {
    this.auditDetailsItem.set(item);
    this.auditDetailsSlideOverOpen.set(true);
  }

  closeAuditDetails(): void {
    this.auditDetailsSlideOverOpen.set(false);
    this.auditDetailsItem.set(null);
  }

  displayName(student: AdminStudentDetail): string {
    return student.displayName
      || [student.firstName, student.lastName].filter(Boolean).join(' ')
      || student.email;
  }

  initials(student: AdminStudentDetail): string {
    const name = this.displayName(student);
    const parts = name.trim().split(/\s+/);
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    return name.slice(0, 2).toUpperCase();
  }

  avatarColor(student: AdminStudentDetail): string {
    const COLORS = ['#5B4BE8','#0D9488','#7C3AED','#D97706','#2563EB','#059669','#DB2777','#0891B2'];
    const name = this.displayName(student);
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return COLORS[Math.abs(hash) % COLORS.length];
  }

  experienceLabel(value: number | null): string {
    return this.experienceLevels.find(l => l.value === value)?.label ?? 'Not set';
  }

  familiarityLabel(value: number | null): string {
    return this.familiarityLevels.find(l => l.value === value)?.label ?? 'Not set';
  }

  startEdit(student: AdminStudentDetail): void {
    this.editing.set(student);
    this.editError.set('');
    this.editForm = {
      firstName: student.firstName ?? '',
      lastName: student.lastName ?? '',
      displayName: student.displayName ?? '',
      careerContext: student.careerContext ?? '',
      learningGoal: student.learningGoal ?? '',
      learningGoalDescription: student.learningGoalDescription ?? '',
      difficultSituationsText: student.difficultSituationsText ?? '',
      preferredSessionDurationMinutes: student.preferredSessionDurationMinutes,
      professionalExperienceLevel: student.professionalExperienceLevel,
      roleFamiliarity: student.roleFamiliarity,
    };
  }

  cancelEdit(): void {
    this.editing.set(null);
    this.editError.set('');
  }

  saveEdit(): void {
    const student = this.editing();
    if (!student) return;

    this.savingEdit.set(true);
    this.editError.set('');

    const request: UpdateStudentProfileRequest = {
      firstName: this.nullIfBlank(this.editForm.firstName),
      lastName: this.nullIfBlank(this.editForm.lastName),
      displayName: this.nullIfBlank(this.editForm.displayName),
      careerContext: this.nullIfBlank(this.editForm.careerContext),
      learningGoal: this.nullIfBlank(this.editForm.learningGoal),
      learningGoalDescription: this.nullIfBlank(this.editForm.learningGoalDescription),
      difficultSituationsText: this.nullIfBlank(this.editForm.difficultSituationsText),
      preferredSessionDurationMinutes: this.editForm.preferredSessionDurationMinutes,
      professionalExperienceLevel: this.editForm.professionalExperienceLevel,
      roleFamiliarity: this.editForm.roleFamiliarity,
    };

    this.adminApi.updateStudent(student.studentProfileId, request).subscribe({
      next: () => {
        this.savingEdit.set(false);
        this.editing.set(null);
        this.loadStudent(student.studentProfileId);
        this.toast.success('Student updated successfully');
      },
      error: err => {
        this.savingEdit.set(false);
        this.editError.set(err.error?.error ?? 'Could not update student.');
      },
    });
  }

  confirmArchive(student: AdminStudentDetail): void {
    const confirmed = window.confirm(`Archive ${student.email}? They will be hidden from the active list and cannot sign in.`);
    if (!confirmed) return;

    this.adminApi.archiveStudent(student.studentProfileId).subscribe({
      next: () => {
        this.loadStudent(student.studentProfileId);
        this.toast.success('Student archived');
      },
      error: err => this.toast.error(err.error?.error ?? 'Could not archive student.'),
    });
  }

  startResetPassword(student: AdminStudentDetail): void {
    this.resetting.set(student);
    this.resetError.set('');
    this.resetSuccessPassword.set('');
    this.resetForm = { newPassword: '', mustChangePassword: true };
  }

  cancelResetPassword(): void {
    this.resetting.set(null);
    this.resetError.set('');
    this.resetSuccessPassword.set('');
  }

  generateResetPassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789';
    let result = '';
    for (let i = 0; i < 12; i++) {
      result += chars[Math.floor(Math.random() * chars.length)];
    }
    this.resetForm.newPassword = result;
  }

  saveResetPassword(): void {
    const student = this.resetting();
    if (!student || this.resetForm.newPassword.length < 8) return;

    this.savingReset.set(true);
    this.resetError.set('');

    this.adminApi.resetStudentPassword(student.studentProfileId, this.resetForm.newPassword, this.resetForm.mustChangePassword).subscribe({
      next: () => {
        this.savingReset.set(false);
        this.resetSuccessPassword.set(this.resetForm.newPassword);
        this.toast.success(`Password reset for ${student.email}`);
      },
      error: err => {
        this.savingReset.set(false);
        this.resetError.set(err.error?.error ?? 'Could not reset password.');
      },
    });
  }

  sendResetLink(student: AdminStudentDetail): void {
    this.sendingResetLink.set(true);
    this.resetLinkSent.set(false);

    this.adminApi.sendStudentResetLink(student.studentProfileId).subscribe({
      next: () => {
        this.sendingResetLink.set(false);
        this.resetLinkSent.set(true);
        this.toast.success(`Reset link sent to ${student.email}`);
      },
      error: () => {
        this.sendingResetLink.set(false);
        this.toast.error('Could not send reset link.');
      },
    });
  }

  startResetData(student: AdminStudentDetail): void {
    this.resettingData.set(student);
    this.resetDataError.set('');
    this.resetDataResult.set(null);
    this.resetDataForm = this.emptyResetDataForm();
  }

  cancelResetData(): void {
    this.resettingData.set(null);
    this.resetDataError.set('');
    this.resetDataResult.set(null);
  }

  applyResetPreset(): void {
    const preset = this.resetPresets.find(p => p.key === this.resetDataForm.preset);
    if (!preset) return;
    Object.assign(this.resetDataForm, preset.flags);
  }

  saveResetData(): void {
    const student = this.resettingData();
    if (!student) return;
    if (this.resetDataForm.confirmEmail !== student.email || !this.resetDataForm.reason.trim()) return;

    this.savingResetData.set(true);
    this.resetDataError.set('');

    const request: ResetStudentRequest = {
      targetStage: this.resetDataForm.targetStage,
      clearOnboardingAnswers: this.resetDataForm.clearOnboardingAnswers,
      clearPlacementResults: this.resetDataForm.clearPlacementResults,
      clearCoursesAndSessions: this.resetDataForm.clearCoursesAndSessions,
      clearActivityAttempts: this.resetDataForm.clearActivityAttempts,
      clearVocabulary: this.resetDataForm.clearVocabulary,
      clearLearningMemory: this.resetDataForm.clearLearningMemory,
      clearAudioFiles: this.resetDataForm.clearAudioFiles,
      clearProgressData: this.resetDataForm.clearProgressData,
      reason: this.resetDataForm.reason.trim(),
    };

    this.adminApi.resetStudent(student.studentProfileId, request).subscribe({
      next: result => {
        this.savingResetData.set(false);
        this.resetDataResult.set(result);
        this.toast.success(`Student data reset for ${student.email}`);
        this.loadStudent(student.studentProfileId);
        this.loadMemory(student.studentProfileId);
      },
      error: err => {
        this.savingResetData.set(false);
        this.resetDataError.set(err.error?.error ?? 'Could not reset student data.');
      },
    });
  }

  private loadPolicy(id: string): void {
    this.policyLoading.set(true);
    this.policyError.set('');
    this.governance.getStudentEffectivePolicy(id).subscribe({
      next: ep => { this.effectivePolicy.set(ep); this.policyLoading.set(false); },
      error: () => { this.policyError.set('Could not load usage policy.'); this.policyLoading.set(false); },
    });
  }

  startAssignPolicy(): void {
    this.assignPolicyError.set('');
    this.assignPolicyForm = { policyId: this.effectivePolicy()?.policy.id ?? '', reason: '' };
    this.governance.listUsagePolicies().subscribe({
      next: policies => {
        this.availablePolicies.set(policies.filter(p => p.isActive));
        this.assigningPolicy.set(true);
      },
      error: () => this.toast.error('Could not load policies.'),
    });
  }

  cancelAssignPolicy(): void {
    this.assigningPolicy.set(false);
    this.assignPolicyError.set('');
  }

  saveAssignPolicy(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId || !this.assignPolicyForm.policyId) return;

    this.savingAssignPolicy.set(true);
    this.assignPolicyError.set('');
    this.governance.assignStudentPolicy(studentId, this.assignPolicyForm.policyId, this.assignPolicyForm.reason || null).subscribe({
      next: () => {
        this.savingAssignPolicy.set(false);
        this.assigningPolicy.set(false);
        this.toast.success('Usage policy assigned.');
        this.loadPolicy(studentId);
      },
      error: err => {
        this.savingAssignPolicy.set(false);
        this.assignPolicyError.set(err.error?.message ?? 'Could not assign policy.');
      },
    });
  }

  confirmRemovePolicy(): void {
    const studentId = this.student()?.studentProfileId;
    if (!studentId) return;
    const confirmed = window.confirm('Remove the student policy override? The student will revert to the global default policy.');
    if (!confirmed) return;

    this.governance.removeStudentPolicy(studentId).subscribe({
      next: () => {
        this.toast.success('Policy override removed. Student reverts to global default.');
        this.loadPolicy(studentId);
      },
      error: err => this.toast.error(err.error?.message ?? 'Could not remove policy assignment.'),
    });
  }

  private emptyResetDataForm() {
    return {
      preset: 'restartOnboarding' as string,
      targetStage: 'OnboardingRequired' as StudentLifecycleStageName,
      clearOnboardingAnswers: true,
      clearPlacementResults: false,
      clearCoursesAndSessions: false,
      clearActivityAttempts: false,
      clearVocabulary: false,
      clearLearningMemory: false,
      clearAudioFiles: false,
      clearProgressData: false,
      reason: '',
      confirmEmail: '',
    };
  }

  private nullIfBlank(value: string): string | null {
    const trimmed = value.trim();
    return trimmed ? trimmed : null;
  }

  private emptyForm(): StudentEditForm {
    return {
      firstName: '',
      lastName: '',
      displayName: '',
      careerContext: '',
      learningGoal: '',
      learningGoalDescription: '',
      difficultSituationsText: '',
      preferredSessionDurationMinutes: null,
      professionalExperienceLevel: null,
      roleFamiliarity: null,
    };
  }
}

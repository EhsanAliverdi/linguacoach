import {
  Component,
  computed,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import {
  ProfileService,
  StudentProfileResponse,
  UpdateLearningPreferencesRequest,
} from '../../../core/services/profile.service';
import {
  NotificationPreferencesService,
  NotificationPreferenceItem,
  UpdateNotificationPreferenceRequest,
} from '../../../core/services/notification-preferences.service';
import { PlacementService } from '../../../core/services/placement.service';
import {
  AdaptivePlacementSummary,
  PlacementConfig,
} from '../../../core/models/placement.models';

/** Keys must match the onboarding "learning_goals" MultipleChoiceQuestion choices
 * (OnboardingFlowSeeder.cs) exactly — StudentProfile.LearningGoals stores raw choice keys,
 * and this page must recognise them as selected, not just goals picked here. Extra
 * profile-only options (not offered during onboarding) get their own keys. */
const PREDEFINED_LEARNING_GOALS: ReadonlyArray<{ key: string; label: string }> = [
  { key: 'day_to_day', label: 'Day-to-day English' },
  { key: 'travel', label: 'Travel English' },
  { key: 'work', label: 'Workplace English' },
  { key: 'study', label: 'Academic English' },
  { key: 'migration', label: 'Migration & settlement' },
  { key: 'job_interview', label: 'Job interviews' },
  { key: 'social', label: 'Social conversation' },
  { key: 'pronunciation', label: 'Pronunciation' },
  { key: 'listening_confidence', label: 'Listening confidence' },
  { key: 'writing_confidence', label: 'Writing confidence' },
  { key: 'exam_inspired_practice', label: 'Exam-inspired practice' },
];

/** Keys must match the onboarding "focus_areas" MultipleChoiceQuestion choices
 * (OnboardingFlowSeeder.cs) exactly — see note on PREDEFINED_LEARNING_GOALS above. */
const PREDEFINED_FOCUS_AREAS: ReadonlyArray<{ key: string; label: string }> = [
  { key: 'speaking', label: 'Speaking' },
  { key: 'listening', label: 'Listening' },
  { key: 'writing', label: 'Writing' },
  { key: 'reading', label: 'Reading' },
  { key: 'vocabulary', label: 'Vocabulary' },
  { key: 'grammar', label: 'Grammar' },
  { key: 'pronunciation', label: 'Pronunciation' },
  { key: 'fluency', label: 'Fluency' },
  { key: 'confidence', label: 'Confidence' },
  { key: 'interviews', label: 'Interviews' },
  { key: 'travel_conversations', label: 'Travel conversations' },
  { key: 'social_conversation', label: 'Social conversation' },
];

const CEFR_EXPLANATIONS: Record<string, string> = {
  A1: 'Beginner — can understand and use basic phrases',
  A2: 'Elementary — can communicate in simple routine tasks',
  B1: 'Intermediate — can handle most everyday situations',
  B2: 'Upper-Intermediate — can understand complex texts and interact fluently',
  C1: 'Advanced — can use language flexibly and effectively',
  C2: 'Proficient — can understand virtually everything heard or read',
};

const SUPPORT_LANGUAGES = [
  { code: 'fa', name: 'Persian' },
  { code: 'zh', name: 'Chinese' },
  { code: 'ar', name: 'Arabic' },
  { code: 'vi', name: 'Vietnamese' },
  { code: 'ko', name: 'Korean' },
  { code: 'es', name: 'Spanish' },
  { code: 'hi', name: 'Hindi' },
  { code: 'tr', name: 'Turkish' },
  { code: 'uk', name: 'Ukrainian' },
  { code: 'ru', name: 'Russian' },
];

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <!-- Loading state -->
    @if (loading()) {
      <div style="display:flex;flex-direction:column;gap:12px">
        <div class="sp-skeleton" style="height:90px"></div>
        <div class="sp-skeleton" style="height:64px"></div>
        <div class="sp-skeleton" style="height:64px"></div>
        <div class="sp-skeleton" style="height:120px"></div>
        <div class="sp-skeleton" style="height:80px"></div>
      </div>
    }

    @if (!loading()) {
      <!-- Section 1: Account -->
      <div class="sp-section-h"><h3>Account</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px">
        <div style="display:flex;align-items:center;gap:14px;margin-bottom:14px">
          <div style="width:52px;height:52px;border-radius:50%;background:var(--sp-grad-brand);display:flex;align-items:center;justify-content:center;font-size:20px;font-weight:800;color:#fff;flex-shrink:0">
            {{ avatarLetter() }}
          </div>
          <div>
            <div style="font-size:15px;font-weight:700;color:var(--sp-ink)">{{ displayName() }}</div>
            <div style="font-size:12px;color:var(--sp-muted)">{{ profile()?.email }}</div>
          </div>
        </div>
        <div>
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:4px">Preferred name</label>
          <input
            [(ngModel)]="form.preferredName"
            placeholder="How you like to be called"
            style="width:100%;box-sizing:border-box;padding:10px 12px;border:1px solid var(--sp-border);border-radius:var(--sp-r-md);font-size:13px;color:var(--sp-ink);background:var(--sp-canvas)"
          />
        </div>
      </div>

      <!-- Section 2: Level (read-only) -->
      <div class="sp-section-h"><h3>Level</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px" data-testid="level-section">
        <div style="display:flex;align-items:center;justify-content:space-between">
          <div>
            <div style="font-size:14px;font-weight:700;color:var(--sp-ink)">{{ profile()?.cefrLevel ?? 'Not assessed yet' }}</div>
            @if (profile()?.cefrLevel) {
              <div style="font-size:12px;color:var(--sp-muted);margin-top:3px">{{ cefrExplanation() }}</div>
            }
          </div>
          <div style="background:var(--sp-grad-brand-soft);border-radius:var(--sp-r-sm);padding:4px 10px;font-size:11px;font-weight:700;color:var(--sp-brand)">
            {{ profile()?.cefrLevel ?? '—' }}
          </div>
        </div>
        <div style="font-size:11px;color:var(--sp-faint);margin-top:12px;padding-top:12px;border-top:1px solid var(--sp-border)">
          Your level is updated through placement, learning progress, and teacher/admin review.
        </div>
      </div>

      <!-- Section: Placement summary -->
      <div class="sp-section-h"><h3>Placement</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px" data-testid="placement-summary-section">
        @if (placementLoading()) {
          <div style="font-size:13px;color:var(--sp-muted)">Loading placement...</div>
        }
        @if (!placementLoading()) {
          @if (!placement()) {
            <div style="font-size:13px;color:var(--sp-muted)" data-testid="no-placement-message">
              You have not completed a placement assessment yet.
            </div>
          }
          @if (placement() && placement()!.status === 'InProgress') {
            <div style="font-size:13px;color:var(--sp-ink);margin-bottom:12px" data-testid="placement-in-progress">
              Your placement assessment is in progress.
            </div>
            <button
              type="button"
              (click)="navigateToPlacement()"
              data-testid="continue-placement-button"
              style="padding:9px 18px;border-radius:var(--sp-r-md);background:var(--sp-grad-brand);border:none;color:#fff;font-size:13px;font-weight:700;cursor:pointer"
            >Continue placement</button>
          }
          @if (placement() && (placement()!.status === 'Completed' || !!placement()!.overallCefrLevel)) {
            <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
              <div style="font-size:22px;font-weight:800;color:var(--sp-ink)">{{ placement()!.overallCefrLevel ?? 'Unknown' }}</div>
              @if (placement()!.isProvisional) {
                <span data-testid="provisional-badge" style="background:#FEF3C7;color:#92400E;border-radius:var(--sp-r-sm);padding:3px 8px;font-size:11px;font-weight:700">Provisional</span>
              } @else {
                <span data-testid="confirmed-badge" style="background:#D1FAE5;color:#065F46;border-radius:var(--sp-r-sm);padding:3px 8px;font-size:11px;font-weight:700">Confirmed</span>
              }
            </div>
            @if (placement()!.completedAtUtc) {
              <div style="font-size:12px;color:var(--sp-muted);margin-bottom:10px" data-testid="placement-date">
                Assessed {{ placement()!.completedAtUtc | date:'mediumDate' }}
              </div>
            }
            @if (placement()!.skillResults?.length) {
              <div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-bottom:12px" data-testid="skill-breakdown">
                @for (s of placement()!.skillResults; track s.skill) {
                  <div style="background:var(--sp-canvas-raised,#F6F4FB);border-radius:var(--sp-r-sm);padding:6px 10px;display:flex;justify-content:space-between;align-items:center;font-size:12px">
                    <span style="color:var(--sp-ink);text-transform:capitalize">{{ s.skill }}</span>
                    <span style="font-weight:700;color:var(--sp-brand)">{{ s.estimatedCefrLevel }}</span>
                  </div>
                }
              </div>
            }
            @if (placementConfig()?.allowPlacementRetake) {
              <button
                type="button"
                data-testid="retake-placement-button"
                (click)="navigateToPlacement()"
                style="padding:9px 18px;border-radius:var(--sp-r-md);background:var(--sp-canvas);border:1px solid var(--sp-border);color:var(--sp-ink);font-size:13px;font-weight:600;cursor:pointer"
              >Request retake</button>
            } @else {
              <div style="font-size:11px;color:var(--sp-faint);margin-top:4px" data-testid="retake-not-available">
                Retake placement is not available yet.
              </div>
            }
          }
        }
      </div>

      <!-- Section 3: Learning goals -->
      <div class="sp-section-h"><h3>Learning goals</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px">
        <div style="font-size:12px;color:var(--sp-muted);margin-bottom:10px">Select what you want to achieve</div>
        <div style="display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px" data-testid="learning-goals-chips">
          @for (goal of predefinedGoals; track goal.key) {
            <button
              type="button"
              (click)="toggleGoal(goal.key)"
              class="sp-pref-chip"
              [class.sp-pref-chip--on]="isGoalSelected(goal.key)"
              [attr.aria-pressed]="isGoalSelected(goal.key)"
              [attr.data-testid]="'goal-chip-' + goal.key"
            >{{ goal.label }}</button>
          }
        </div>
        <div>
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:4px">Custom goal</label>
          <input
            [(ngModel)]="form.customLearningGoal"
            placeholder="Something not on the list"
            maxlength="200"
            data-testid="custom-goal-input"
            style="width:100%;box-sizing:border-box;padding:10px 12px;border:1px solid var(--sp-border);border-radius:var(--sp-r-md);font-size:13px;color:var(--sp-ink);background:var(--sp-canvas)"
          />
        </div>
      </div>

      <!-- Section 4: Focus areas -->
      <div class="sp-section-h"><h3>Focus areas</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px">
        <div style="font-size:12px;color:var(--sp-muted);margin-bottom:10px">Select skills to focus on</div>
        <div style="display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px">
          @for (area of predefinedFocusAreas; track area.key) {
            <button
              type="button"
              (click)="toggleFocusArea(area.key)"
              class="sp-pref-chip"
              [class.sp-pref-chip--on]="isFocusAreaSelected(area.key)"
              [attr.aria-pressed]="isFocusAreaSelected(area.key)"
              [attr.data-testid]="'focus-chip-' + area.key"
            >{{ area.label }}</button>
          }
        </div>
        <div>
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:4px">Custom focus area</label>
          <input
            [(ngModel)]="form.customFocusArea"
            placeholder="Something else"
            maxlength="200"
            style="width:100%;box-sizing:border-box;padding:10px 12px;border:1px solid var(--sp-border);border-radius:var(--sp-r-md);font-size:13px;color:var(--sp-ink);background:var(--sp-canvas)"
          />
        </div>
      </div>

      <!-- Section 5: Support language -->
      <div class="sp-section-h"><h3>Support language</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px">
        <div style="margin-bottom:14px">
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:4px">Support language</label>
          <select
            [(ngModel)]="form.supportLanguageCode"
            (ngModelChange)="onSupportLanguageChange($event)"
            data-testid="support-language-select"
            style="width:100%;padding:10px 12px;border:1px solid var(--sp-border);border-radius:var(--sp-r-md);font-size:13px;color:var(--sp-ink);background:var(--sp-canvas)"
          >
            <option [ngValue]="null">None selected</option>
            @for (lang of supportLanguages; track lang.code) {
              <option [ngValue]="lang.code">{{ lang.name }}</option>
            }
          </select>
        </div>
        <div>
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:4px">Translation help</label>
          <select
            [(ngModel)]="form.translationHelpPreference"
            style="width:100%;padding:10px 12px;border:1px solid var(--sp-border);border-radius:var(--sp-r-md);font-size:13px;color:var(--sp-ink);background:var(--sp-canvas)"
          >
            <option [ngValue]="null">Not set</option>
            <option [ngValue]="0">Never</option>
            <option [ngValue]="1">When it gets difficult</option>
            <option [ngValue]="2">Always available</option>
          </select>
        </div>
      </div>

      <!-- Section 6: Practice preferences -->
      <div class="sp-section-h"><h3>Practice preferences</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:20px">
        <div style="margin-bottom:14px">
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:8px">Session length</label>
          <div style="display:flex;flex-wrap:wrap;gap:8px">
            @for (mins of sessionLengths; track mins) {
              <button
                type="button"
                (click)="form.preferredSessionDurationMinutes = mins"
                class="sp-pref-chip"
                [class.sp-pref-chip--on]="form.preferredSessionDurationMinutes === mins"
                [attr.aria-pressed]="form.preferredSessionDurationMinutes === mins"
                [attr.data-testid]="'session-length-' + mins"
              >{{ mins }} min</button>
            }
          </div>
        </div>
        <div>
          <label style="font-size:12px;font-weight:600;color:var(--sp-muted);display:block;margin-bottom:8px">Difficulty</label>
          <div style="display:flex;flex-wrap:wrap;gap:8px">
            <button type="button" (click)="form.difficultyPreference = 0" class="sp-pref-chip" [class.sp-pref-chip--on]="form.difficultyPreference === 0" [attr.aria-pressed]="form.difficultyPreference === 0" data-testid="difficulty-gentle">Gentle</button>
            <button type="button" (click)="form.difficultyPreference = 1" class="sp-pref-chip" [class.sp-pref-chip--on]="form.difficultyPreference === 1" [attr.aria-pressed]="form.difficultyPreference === 1" data-testid="difficulty-balanced">Balanced</button>
            <button type="button" (click)="form.difficultyPreference = 2" class="sp-pref-chip" [class.sp-pref-chip--on]="form.difficultyPreference === 2" [attr.aria-pressed]="form.difficultyPreference === 2" data-testid="difficulty-challenging">Challenging</button>
          </div>
        </div>
      </div>

      <!-- Section 7: Notification preferences -->
      <div class="sp-section-h"><h3>Notification preferences</h3></div>
      <div class="sp-card" style="padding:18px;margin-bottom:16px" data-testid="notification-prefs-section">
        @if (prefsLoading()) {
          <div style="font-size:13px;color:var(--sp-muted)" data-testid="prefs-loading">Loading preferences...</div>
        }
        @if (!prefsLoading()) {
          <div style="overflow-x:auto">
            <table style="width:100%;border-collapse:collapse;font-size:13px" data-testid="prefs-table">
              <thead>
                <tr>
                  <th style="text-align:left;padding:6px 8px;color:var(--sp-muted);font-weight:600;border-bottom:1px solid var(--sp-border)">Category</th>
                  <th style="text-align:center;padding:6px 8px;color:var(--sp-muted);font-weight:600;border-bottom:1px solid var(--sp-border)">In-App</th>
                  <th style="text-align:center;padding:6px 8px;color:var(--sp-muted);font-weight:600;border-bottom:1px solid var(--sp-border)">Email</th>
                  <th style="text-align:center;padding:6px 8px;color:var(--sp-muted);font-weight:600;border-bottom:1px solid var(--sp-border)">SMS</th>
                </tr>
              </thead>
              <tbody>
                @for (row of prefRows(); track row.category) {
                  <tr style="border-bottom:1px solid var(--sp-border)">
                    <td style="padding:8px;font-weight:600;color:var(--sp-ink)">
                      {{ row.category }}
                      @if (row.isRequired) {
                        <span style="font-size:10px;font-weight:700;color:var(--sp-brand);margin-left:4px" data-testid="required-badge">Required</span>
                      }
                    </td>
                    <td style="text-align:center;padding:8px">
                      <input type="checkbox"
                        [checked]="getPref(row.category, 'InApp')"
                        [disabled]="isPrefRequired(row.category, 'InApp')"
                        (change)="setPref(row.category, 'InApp', $any($event.target).checked)"
                        [attr.data-testid]="'pref-inapp-' + row.category"
                      />
                    </td>
                    <td style="text-align:center;padding:8px">
                      <input type="checkbox"
                        [checked]="getPref(row.category, 'Email')"
                        [disabled]="isPrefRequired(row.category, 'Email')"
                        (change)="setPref(row.category, 'Email', $any($event.target).checked)"
                        [attr.data-testid]="'pref-email-' + row.category"
                      />
                    </td>
                    <td style="text-align:center;padding:8px">
                      <span style="font-size:11px;color:var(--sp-faint)" data-testid="sms-coming-soon">Coming soon</span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (prefsSaveError()) {
            <div style="margin-top:10px;font-size:12px;color:#991B1B" data-testid="prefs-error">{{ prefsSaveError() }}</div>
          }
          <button
            (click)="savePrefs()"
            [disabled]="prefsSaving()"
            data-testid="save-prefs-button"
            style="margin-top:12px;padding:10px 20px;border-radius:var(--sp-r-md);background:var(--sp-grad-brand);border:none;color:#fff;font-size:13px;font-weight:700;cursor:pointer"
          >{{ prefsSaving() ? 'Saving...' : 'Save notification preferences' }}</button>
        }
      </div>

      <!-- Save / error / success -->
      @if (errorMessage()) {
        <div style="background:#FEE2E2;border:1px solid #FECACA;border-radius:var(--sp-r-md);padding:12px 16px;font-size:13px;color:#991B1B;margin-bottom:14px" data-testid="error-message">
          {{ errorMessage() }}
        </div>
      }
      @if (successMessage()) {
        <div style="background:#D1FAE5;border:1px solid #A7F3D0;border-radius:var(--sp-r-md);padding:12px 16px;font-size:13px;color:#065F46;margin-bottom:14px" data-testid="success-message">
          {{ successMessage() }}
        </div>
      }

      <button
        (click)="save()"
        [disabled]="saving()"
        data-testid="save-button"
        style="width:100%;display:flex;align-items:center;justify-content:center;padding:14px;border-radius:var(--sp-r-lg);background:var(--sp-grad-brand);border:none;color:#fff;font-size:14px;font-weight:700;cursor:pointer;transition:opacity .15s;margin-bottom:14px"
      >
        {{ saving() ? 'Saving...' : 'Save preferences' }}
      </button>

      <!-- Sign out -->
      <button (click)="auth.logout()"
        style="width:100%;display:flex;align-items:center;justify-content:center;gap:8px;padding:14px;border-radius:var(--sp-r-lg);background:var(--sp-speaking-soft);border:1px solid #F5BDB3;color:var(--sp-speaking-ink);font-size:14px;font-weight:700;cursor:pointer;transition:opacity .15s">
        <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
        Sign out
      </button>
    }
  `,
})
export class ProfileComponent implements OnInit {
  readonly predefinedGoals = PREDEFINED_LEARNING_GOALS;
  readonly predefinedFocusAreas = PREDEFINED_FOCUS_AREAS;
  readonly supportLanguages = SUPPORT_LANGUAGES;
  readonly sessionLengths = [10, 15, 20, 30, 45];

  profile = signal<StudentProfileResponse | null>(null);
  loading = signal(true);
  saving = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  // Placement summary (Part F)
  placement = signal<AdaptivePlacementSummary | null>(null);
  placementConfig = signal<PlacementConfig | null>(null);
  placementLoading = signal(true);

  // Notification preferences state
  allPrefs = signal<NotificationPreferenceItem[]>([]);
  prefsLoading = signal(true);
  prefsSaving = signal(false);
  prefsSaveError = signal<string | null>(null);
  // Local edits: category+channel -> isEnabled
  private prefEdits = new Map<string, boolean>();

  prefRows = computed(() => {
    const seen = new Set<string>();
    return this.allPrefs().filter(p => {
      if (seen.has(p.category)) return false;
      seen.add(p.category);
      return true;
    });
  });

  form: {
    preferredName: string | null;
    supportLanguageCode: string | null;
    supportLanguageName: string | null;
    translationHelpPreference: number | null;
    learningGoals: string[];
    customLearningGoal: string | null;
    focusAreas: string[];
    customFocusArea: string | null;
    difficultyPreference: number | null;
    preferredSessionDurationMinutes: number | null;
  } = {
    preferredName: null,
    supportLanguageCode: null,
    supportLanguageName: null,
    translationHelpPreference: null,
    learningGoals: [],
    customLearningGoal: null,
    focusAreas: [],
    customFocusArea: null,
    difficultyPreference: null,
    preferredSessionDurationMinutes: null,
  };

  avatarLetter = computed(() => {
    const p = this.profile();
    const name = p?.preferredName ?? p?.displayName ?? p?.firstName ?? p?.email ?? '';
    return name.charAt(0).toUpperCase() || 'U';
  });

  displayName = computed(() => {
    const p = this.profile();
    return p?.preferredName ?? p?.displayName ?? p?.firstName ?? p?.email ?? '';
  });

  cefrExplanation = computed(() => {
    const level = this.profile()?.cefrLevel;
    return level ? CEFR_EXPLANATIONS[level] ?? '' : '';
  });

  constructor(
    public auth: AuthService,
    private profileService: ProfileService,
    private notifPrefsService: NotificationPreferencesService,
    private placementService: PlacementService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.profileService.getProfile().subscribe({
      next: p => {
        this.profile.set(p);
        this.form = {
          preferredName: p.preferredName ?? null,
          supportLanguageCode: p.supportLanguageCode ?? null,
          supportLanguageName: p.supportLanguageName ?? null,
          translationHelpPreference: this.translationHelpToInt(p.translationHelpPreference),
          learningGoals: p.learningGoals ?? [],
          customLearningGoal: p.customLearningGoal ?? null,
          focusAreas: p.focusAreas ?? [],
          customFocusArea: p.customFocusArea ?? null,
          difficultyPreference: this.difficultyToInt(p.difficultyPreference),
          preferredSessionDurationMinutes: p.preferredSessionDurationMinutes ?? null,
        };
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });

    this.notifPrefsService.getPreferences().subscribe({
      next: prefs => {
        this.allPrefs.set(prefs);
        this.prefEdits.clear();
        prefs.forEach(p => this.prefEdits.set(`${p.category}:${p.channel}`, p.isEnabled));
        this.prefsLoading.set(false);
      },
      error: () => this.prefsLoading.set(false),
    });

    this.placementService.getAdaptiveCurrent().pipe(
      catchError(() => of(null))
    ).subscribe(p => {
      this.placement.set(p);
      this.placementLoading.set(false);
    });

    this.placementService.getPlacementConfig().pipe(
      catchError(() => of(null))
    ).subscribe(cfg => this.placementConfig.set(cfg));
  }

  navigateToPlacement(): void {
    this.router.navigate(['/placement']);
  }

  getPref(category: string, channel: string): boolean {
    return this.prefEdits.get(`${category}:${channel}`) ?? true;
  }

  isPrefRequired(category: string, channel: string): boolean {
    return this.allPrefs().find(p => p.category === category && p.channel === channel)?.isRequired ?? false;
  }

  setPref(category: string, channel: string, value: boolean): void {
    this.prefEdits.set(`${category}:${channel}`, value);
  }

  savePrefs(): void {
    this.prefsSaving.set(true);
    this.prefsSaveError.set(null);

    const updates: UpdateNotificationPreferenceRequest[] = [];
    this.prefEdits.forEach((isEnabled, key) => {
      const [category, channel] = key.split(':');
      updates.push({ category, channel, isEnabled });
    });

    this.notifPrefsService.updatePreferences(updates).subscribe({
      next: () => this.prefsSaving.set(false),
      error: () => {
        this.prefsSaving.set(false);
        this.prefsSaveError.set('Could not save notification preferences. Please try again.');
      },
    });
  }

  isGoalSelected(goal: string): boolean {
    return this.form.learningGoals.includes(goal);
  }

  toggleGoal(goal: string): void {
    if (this.isGoalSelected(goal)) {
      this.form.learningGoals = this.form.learningGoals.filter(g => g !== goal);
    } else if (this.form.learningGoals.length < 10) {
      this.form.learningGoals = [...this.form.learningGoals, goal];
    }
  }

  isFocusAreaSelected(area: string): boolean {
    return this.form.focusAreas.includes(area);
  }

  toggleFocusArea(area: string): void {
    if (this.isFocusAreaSelected(area)) {
      this.form.focusAreas = this.form.focusAreas.filter(a => a !== area);
    } else if (this.form.focusAreas.length < 10) {
      this.form.focusAreas = [...this.form.focusAreas, area];
    }
  }

  onSupportLanguageChange(code: string | null): void {
    const lang = this.supportLanguages.find(l => l.code === code);
    this.form.supportLanguageName = lang?.name ?? null;
  }

  save(): void {
    this.saving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const payload: UpdateLearningPreferencesRequest = {
      preferredName: this.form.preferredName || null,
      supportLanguageCode: this.form.supportLanguageCode || null,
      supportLanguageName: this.form.supportLanguageName || null,
      translationHelpPreference: this.form.translationHelpPreference,
      learningGoals: this.form.learningGoals,
      customLearningGoal: this.form.customLearningGoal || null,
      focusAreas: this.form.focusAreas,
      customFocusArea: this.form.customFocusArea || null,
      difficultyPreference: this.form.difficultyPreference,
      preferredSessionDurationMinutes: this.form.preferredSessionDurationMinutes,
    };

    this.profileService.updatePreferences(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMessage.set('Preferences saved.');
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: () => {
        this.saving.set(false);
        this.errorMessage.set('Could not save. Please try again.');
      },
    });
  }

  private translationHelpToInt(val: string | null | undefined): number | null {
    if (!val) return null;
    const map: Record<string, number> = { Never: 0, WhenDifficult: 1, AlwaysAvailable: 2 };
    return map[val] ?? null;
  }

  private difficultyToInt(val: string | null | undefined): number | null {
    if (!val) return null;
    const map: Record<string, number> = { Gentle: 0, Balanced: 1, Challenging: 2 };
    return map[val] ?? null;
  }
}



import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LessonDto } from '../../../../core/models/admin-lesson.models';

interface SkillBadge {
  label: string;
  background: string;
  color: string;
}

// Same pastel badge convention already used by the real student Teach page
// (activity-teach-page's presenters) — grammar/pronunciation/fluency/confidence/collocation
// didn't have a badge yet since no student-facing surface showed those skills before Lessons did.
const SKILL_BADGES: Record<string, SkillBadge> = {
  vocabulary: { label: 'Vocabulary', background: '#ede9fe', color: '#6d28d9' },
  listening: { label: 'Listening', background: '#e0f2fe', color: '#0369a1' },
  reading: { label: 'Reading', background: '#dcfce7', color: '#15803d' },
  speaking: { label: 'Speaking', background: '#fef3c7', color: '#92400e' },
  writing: { label: 'Writing', background: 'var(--sp-writing-soft)', color: 'var(--sp-writing-ink)' },
  grammar: { label: 'Grammar', background: '#fee2e2', color: '#b91c1c' },
  pronunciation: { label: 'Pronunciation', background: '#fce7f3', color: '#a21caf' },
  fluency: { label: 'Fluency', background: '#e0e7ff', color: '#3730a3' },
  confidence: { label: 'Confidence', background: '#fff7ed', color: '#c2410c' },
  collocation: { label: 'Collocation', background: '#ecfccb', color: '#4d7c0f' },
};
const DEFAULT_BADGE: SkillBadge = { label: 'Lesson', background: 'var(--sp-canvas2)', color: 'var(--sp-muted)' };

function parseJsonStringArray(json: string | null | undefined): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed.filter(v => typeof v === 'string') : [];
  } catch {
    return [];
  }
}

/**
 * 2026-08-04 — renders a Lesson exactly the way a student sees teaching content elsewhere in the
 * app: same markup/classes/CSS custom properties as the real Teach page's `stagedLearning` case
 * (activity-teach-page.component.html), not an admin-styled preview. Used by the admin Lesson
 * detail page's "View as Student" modal so what an admin approves is visibly the same thing a
 * student would be taught — Lesson content has no other student-facing renderer today (the H10
 * runtime bridge only ever launches an Exercise's practice form, never a Lesson's explanation).
 */
@Component({
  selector: 'app-lesson-student-preview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display:flex;flex-direction:column;gap:16px">
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:4px">
        <span class="sp-skill-badge" [style.background]="badge.background" [style.color]="badge.color">
          {{ badge.label }}
        </span>
        @if (lesson.cefrLevel) {
          <span style="font-size:12px;font-weight:600;color:var(--sp-muted)">{{ lesson.cefrLevel }}</span>
        }
        @if (lesson.difficultyBand) {
          <span style="font-size:12px;font-weight:600;color:var(--sp-muted);margin-left:auto">Difficulty {{ lesson.difficultyBand }}/5</span>
        }
      </div>

      <h2 style="font-size:18px;font-weight:800;color:var(--sp-ink);letter-spacing:-.01em;margin:0">{{ lesson.title }}</h2>

      <div class="sp-card" style="padding:18px">
        <p style="font-size:14.5px;color:var(--sp-text);line-height:1.65;white-space:pre-wrap">{{ lesson.body }}</p>
      </div>

      @if (examples().length > 0) {
        <div style="display:flex;flex-direction:column;gap:8px">
          <p style="font-size:12px;font-weight:700;color:var(--sp-muted);text-transform:uppercase;letter-spacing:.06em;margin-bottom:2px">Examples</p>
          @for (example of examples(); track example) {
            <div class="sp-card" style="padding:14px;border-left:3px solid var(--sp-listening,#0369a1)">
              <div style="font-size:14px;color:var(--sp-ink)">{{ example }}</div>
            </div>
          }
        </div>
      }

      @if (lesson.usageNotes) {
        <div style="display:flex;align-items:flex-start;gap:10px;padding:12px 16px;background:var(--sp-canvas2);border-radius:12px">
          <svg style="flex-shrink:0;margin-top:2px" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><path d="M12 8v4l3 3"/></svg>
          <p style="font-size:13px;color:var(--sp-text);line-height:1.6;margin:0"><strong>Usage:</strong> {{ lesson.usageNotes }}</p>
        </div>
      }

      @if (commonMistakes().length > 0) {
        <div class="sp-alert-info" style="font-size:13px">
          <strong>Watch out for:</strong>
          <ul style="margin:6px 0 0;padding-left:18px">
            @for (mistake of commonMistakes(); track mistake) {
              <li>{{ mistake }}</li>
            }
          </ul>
        </div>
      }

      <button type="button" class="sp-button-primary" style="width:100%" disabled>Start practice</button>
    </div>
  `,
})
export class SpLessonStudentPreviewComponent {
  @Input({ required: true }) lesson!: LessonDto;

  get badge(): SkillBadge {
    const key = (this.lesson.skill ?? '').trim().toLowerCase();
    return SKILL_BADGES[key] ?? DEFAULT_BADGE;
  }

  examples(): string[] {
    return parseJsonStringArray(this.lesson.examplesJson);
  }

  commonMistakes(): string[] {
    return parseJsonStringArray(this.lesson.commonMistakesJson);
  }
}

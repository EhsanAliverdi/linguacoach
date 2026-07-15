import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminIconComponent } from '../icon/sp-admin-icon.component';

/**
 * Horizontal step indicator for multi-stage admin workflows (e.g. Import Content's
 * add → structure → review → publish pipeline). Steps before `currentIndex` render as completed
 * (checkmark), the step at `currentIndex` renders as current (highlighted), everything after
 * renders as upcoming (muted). Purely presentational — the host page owns step derivation.
 */
@Component({
  selector: 'sp-admin-stepper',
  standalone: true,
  imports: [CommonModule, SpAdminIconComponent],
  template: `
    <div class="sp-adm-stepper" role="list">
      @for (step of steps; track step; let i = $index; let last = $last) {
        <div class="sp-adm-step" role="listitem">
          <div class="sp-adm-step-marker">
            <div class="sp-adm-step-circle"
              [class.sp-adm-step-circle--done]="i < currentIndex"
              [class.sp-adm-step-circle--current]="i === currentIndex">
              @if (i < currentIndex) {
                <sp-admin-icon name="check" size="xs" />
              } @else {
                {{ i + 1 }}
              }
            </div>
            @if (!last) {
              <div class="sp-adm-step-line" [class.sp-adm-step-line--done]="i < currentIndex"></div>
            }
          </div>
          <div class="sp-adm-step-label"
            [class.sp-adm-step-label--current]="i === currentIndex"
            [class.sp-adm-step-label--done]="i < currentIndex">
            {{ step }}
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }

    .sp-adm-stepper {
      display: flex;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 0;
      margin-bottom: 20px;
    }

    .sp-adm-step {
      display: flex;
      align-items: flex-start;
      flex: 1;
      min-width: 140px;
    }
    .sp-adm-step:last-child { flex: 0 0 auto; min-width: 0; }

    .sp-adm-step-marker {
      display: flex;
      align-items: center;
      flex-shrink: 0;
    }

    .sp-adm-step-circle {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      font-size: 12px;
      font-weight: 800;
      background: var(--sp-admin-surface-subtle, #FBFAFE);
      color: var(--sp-admin-text-muted, #8B85A0);
      border: 1.5px solid var(--sp-admin-border-2, #E2DEF0);
    }
    .sp-adm-step-circle--current {
      background: var(--sp-admin-primary, #5B4BE8);
      color: #fff;
      border-color: var(--sp-admin-primary, #5B4BE8);
      box-shadow: var(--sp-admin-shadow-indigo, 0 2px 8px rgba(91,75,232,.20));
    }
    .sp-adm-step-circle--done {
      background: var(--sp-admin-primary-bg, #EDEBFF);
      color: var(--sp-admin-primary, #5B4BE8);
      border-color: var(--sp-admin-primary-bg, #EDEBFF);
    }

    .sp-adm-step-line {
      width: 32px;
      height: 1.5px;
      background: var(--sp-admin-border-2, #E2DEF0);
      margin: 0 4px;
      flex-shrink: 1;
      min-width: 12px;
    }
    .sp-adm-step-line--done { background: var(--sp-admin-primary, #5B4BE8); }

    .sp-adm-step-label {
      font-size: 12.5px;
      font-weight: 600;
      color: var(--sp-admin-text-muted, #8B85A0);
      margin-left: 8px;
      margin-top: 4px;
      line-height: 1.3;
    }
    .sp-adm-step-label--current { color: var(--sp-admin-text, #211B36); font-weight: 800; }
    .sp-adm-step-label--done { color: var(--sp-admin-text-secondary, #4B4462); }

    @media (max-width: 700px) {
      .sp-adm-stepper { flex-direction: column; gap: 10px; }
      .sp-adm-step-line { display: none; }
    }
  `],
})
export class SpAdminStepperComponent {
  @Input() steps: string[] = [];
  /** 0-based index of the current/active step. */
  @Input() currentIndex = 0;
}

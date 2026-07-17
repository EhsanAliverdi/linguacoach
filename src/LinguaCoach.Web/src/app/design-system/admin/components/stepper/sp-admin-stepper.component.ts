import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminIconComponent } from '../icon/sp-admin-icon.component';

export type SpAdminStepperVariant =
  | 'default'
  | 'progress'
  | 'detailed'
  | 'vertical'
  | 'breadcrumb'
  | 'timeline';

/**
 * Step indicator for multi-stage admin workflows (e.g. Import Content's
 * add → structure → review → publish pipeline). Steps before `currentIndex` render as
 * completed (checkmark), the step at `currentIndex` renders as current (highlighted),
 * everything after renders as upcoming (muted). Purely presentational — the host page
 * owns step derivation. `variant` swaps the layout between Flowbite's stepper patterns;
 * `subtitles` is only read by the `detailed` and `timeline` variants.
 *
 * Class strings are built in TS rather than via `[class.foo:bar]` bindings because
 * Angular's binding-name parser rejects colons (Tailwind's `after:` / `sm:` variants).
 */
@Component({
  selector: 'sp-admin-stepper',
  standalone: true,
  imports: [CommonModule, SpAdminIconComponent],
  template: `
    @switch (variant) {
      @case ('default') {
        <ol class="flex flex-wrap items-center w-full text-sm font-medium text-center text-body">
          @for (step of steps; track step; let i = $index; let last = $last) {
            <li [class]="defaultItemClass(i, last)">
              <span class="flex items-center">
                @if (i < currentIndex) {
                  <sp-admin-icon name="check" size="xs" tone="primary" />
                } @else {
                  <span class="me-2">{{ i + 1 }}</span>
                }
                <span class="ms-1.5">{{ step }}</span>
              </span>
            </li>
          }
        </ol>
      }
      @case ('progress') {
        <ol class="flex items-center w-full space-x-4">
          @for (step of steps; track step; let i = $index; let last = $last) {
            <li [class]="progressItemClass(i, last)">
              <span [class]="progressCircleClass(i)">
                @if (i < currentIndex) {
                  <sp-admin-icon name="check" size="sm" tone="primary" />
                } @else {
                  <span class="text-sm font-bold" [class]="i === currentIndex ? 'text-fg-brand' : 'text-body'">{{ i + 1 }}</span>
                }
              </span>
            </li>
          }
        </ol>
        @if (steps[currentIndex]) {
          <p class="mt-2 text-sm font-semibold text-heading">{{ steps[currentIndex] }}</p>
        }
      }
      @case ('detailed') {
        <ol class="items-start w-full space-y-4 sm:flex sm:space-x-8 sm:space-y-0">
          @for (step of steps; track step; let i = $index) {
            <li class="flex items-center space-x-3" [class]="i <= currentIndex ? 'text-fg-brand' : 'text-body'">
              <span [class]="progressCircleClass(i)">
                @if (i < currentIndex) {
                  <sp-admin-icon name="check" size="sm" tone="primary" />
                } @else {
                  <span class="text-sm font-bold">{{ i + 1 }}</span>
                }
              </span>
              <span>
                <h3 class="font-medium leading-tight text-heading">{{ step }}</h3>
                @if (subtitles?.[i]) {
                  <p class="text-sm text-body">{{ subtitles![i] }}</p>
                }
              </span>
            </li>
          }
        </ol>
      }
      @case ('vertical') {
        <ol class="space-y-3 w-full max-w-xs">
          @for (step of steps; track step; let i = $index) {
            <li [class]="verticalItemClass(i)" role="alert">
              <div class="flex items-center justify-between">
                <h3 class="font-medium">{{ i + 1 }}. {{ step }}</h3>
                @if (i < currentIndex) {
                  <sp-admin-icon name="check" size="sm" />
                }
              </div>
            </li>
          }
        </ol>
      }
      @case ('breadcrumb') {
        <ol class="flex flex-wrap items-center w-full p-3 space-x-2 text-sm font-medium text-center text-body bg-neutral-primary-soft border border-default rounded-base shadow-xs sm:p-4 sm:space-x-4">
          @for (step of steps; track step; let i = $index; let last = $last) {
            <li class="flex items-center" [class]="i <= currentIndex ? 'text-fg-brand' : ''">
              <span class="flex items-center justify-center w-5 h-5 me-2 text-xs border rounded-full shrink-0"
                [class]="i <= currentIndex ? 'border-brand' : 'border-body'">
                {{ i + 1 }}
              </span>
              {{ step }}
              @if (!last) {
                <sp-admin-icon name="chevron-right" size="xs" />
              }
            </li>
          }
        </ol>
      }
      @case ('timeline') {
        <ol class="relative text-body border-s border-default ms-2">
          @for (step of steps; track step; let i = $index; let last = $last) {
            <li class="ms-7" [class]="last ? '' : 'mb-8'">
              <span [class]="timelineDotClass(i)">
                @if (i < currentIndex) {
                  <sp-admin-icon name="check" size="sm" />
                } @else {
                  <span class="text-xs font-bold">{{ i + 1 }}</span>
                }
              </span>
              <h3 class="font-medium leading-tight text-heading">{{ step }}</h3>
              @if (subtitles?.[i]) {
                <p class="text-sm text-body">{{ subtitles![i] }}</p>
              }
            </li>
          }
        </ol>
      }
    }
  `,
})
export class SpAdminStepperComponent {
  @Input() steps: string[] = [];
  /** 0-based index of the current/active step. */
  @Input() currentIndex = 0;
  /** Layout pattern — see Flowbite's stepper examples. Defaults to the original circle+line design. */
  @Input() variant: SpAdminStepperVariant = 'progress';
  /** Optional per-step subtitle, read by the `detailed` and `timeline` variants only. */
  @Input() subtitles?: string[];

  defaultItemClass(i: number, last: boolean): string {
    const base = ['flex', 'md:w-full', 'items-center'];
    if (i <= this.currentIndex) base.push('text-fg-brand');
    if (!last) {
      base.push(
        "after:content-['']", 'after:w-full', 'after:h-1', 'after:border-b',
        'after:border-default', 'after:hidden', 'sm:after:inline-block', 'after:mx-6', 'xl:after:mx-10',
      );
    }
    return base.join(' ');
  }

  progressItemClass(i: number, last: boolean): string {
    const base = ['flex', 'items-center'];
    if (!last) {
      base.push(
        'w-full', "after:content-['']", 'after:w-full', 'after:h-1', 'after:inline-block', 'after:ms-4', 'after:rounded-full',
        i < this.currentIndex ? 'after:bg-brand-subtle' : 'after:bg-default',
      );
    }
    return base.join(' ');
  }

  progressCircleClass(i: number): string {
    const base = ['flex', 'items-center', 'justify-center', 'w-10', 'h-10', 'rounded-full', 'lg:h-12', 'lg:w-12', 'shrink-0'];
    base.push(i <= this.currentIndex ? 'bg-brand-softer' : 'bg-neutral-tertiary');
    return base.join(' ');
  }

  verticalItemClass(i: number): string {
    const base = ['w-full', 'p-4', 'rounded-base', 'border'];
    if (i < this.currentIndex) base.push('bg-success-soft', 'border-success-subtle', 'text-fg-success-strong');
    else if (i === this.currentIndex) base.push('bg-brand-softer', 'border-brand-subtle', 'text-fg-brand-strong');
    else base.push('bg-neutral-secondary', 'border-default', 'text-body');
    return base.join(' ');
  }

  timelineDotClass(i: number): string {
    const base = ['absolute', 'flex', 'items-center', 'justify-center', 'w-8', 'h-8', 'rounded-full', '-start-4', 'ring-4', 'ring-buffer'];
    if (i < this.currentIndex) base.push('bg-success-soft', 'text-fg-success-strong');
    else if (i === this.currentIndex) base.push('bg-brand-softer', 'text-fg-brand');
    else base.push('bg-neutral-tertiary', 'text-body');
    return base.join(' ');
  }
}

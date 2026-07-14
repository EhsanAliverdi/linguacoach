import { Injectable, signal } from '@angular/core';
import { Observable, from, of } from 'rxjs';
import { catchError, concatMap, map, tap, toArray } from 'rxjs/operators';
import { ToastService } from './toast.service';
import { RepairableItemSummary } from '../models/admin-repair.models';

export interface AdminBulkRepairRunOptions {
  /** e.g. "Resource Bank", "Lesson", "Exercise", "Module" — used in toast text only. */
  entityLabel: string;
  listWithIssues: () => Observable<RepairableItemSummary[]>;
  repairOne: (id: string) => Observable<unknown>;
  /** Called once the run finishes (success or failure) — use it to refresh the calling page's
   *  own list/summary if it's still mounted. Safe to call even if the page has since navigated
   *  away (a no-op HTTP call against a destroyed component, never throws). */
  onDone?: () => void;
}

/**
 * Phase K11 — runs a bulk "Fix with AI" pass as a root-provided (app-lifetime) service instead of
 * component-local state, so the progress toast (rendered once in AdminAppLayoutComponent, outside
 * any individual page) keeps updating even if the admin navigates to a different admin page
 * mid-run. `running` is a single global flag shared across all four entity types — deliberately
 * blocks starting a second bulk run while one is already in flight, rather than allowing several
 * concurrent AI-repair sweeps.
 */
@Injectable({ providedIn: 'root' })
export class AdminBulkRepairService {
  readonly running = signal(false);

  constructor(private toast: ToastService) {}

  run(options: AdminBulkRepairRunOptions): void {
    if (this.running()) return;
    this.running.set(true);

    options.listWithIssues().subscribe({
      next: targets => {
        if (targets.length === 0) {
          this.running.set(false);
          this.toast.info(`No ${options.entityLabel} items with auto-fixable issues found.`);
          options.onDone?.();
          return;
        }

        const total = targets.length;
        let current = 0;
        const toastId = this.toast.showProgress(`Fixing ${options.entityLabel}: 0/${total}…`, 0, total);

        from(targets).pipe(
          concatMap(target => {
            this.toast.updateProgress(toastId, `Fixing ${options.entityLabel}: ${current}/${total} — "${target.title}"…`, current, total);
            return options.repairOne(target.id).pipe(
              map(() => ({ success: true, title: target.title, error: null as string | null })),
              catchError((err: { error?: { error?: string } }) =>
                of({ success: false, title: target.title, error: err.error?.error ?? 'failed' })),
              tap(() => {
                current++;
                this.toast.updateProgress(toastId, `Fixing ${options.entityLabel}: ${current}/${total}…`, current, total);
              }),
            );
          }),
          toArray(),
        ).subscribe(results => {
          this.running.set(false);
          this.toast.dismiss(toastId);
          const succeeded = results.filter(r => r.success).length;
          const failed = results.length - succeeded;
          if (failed > 0) {
            this.toast.warning(
              `Fixed ${succeeded} of ${results.length} ${options.entityLabel} item(s) — ${failed} failed: ` +
              results.filter(r => !r.success).map(r => r.title + ' (' + r.error + ')').join('; '));
          } else {
            this.toast.success(`Fixed ${succeeded} of ${results.length} ${options.entityLabel} item(s).`);
          }
          options.onDone?.();
        });
      },
      error: () => {
        this.running.set(false);
        this.toast.error(`Could not load ${options.entityLabel} items with issues.`);
        options.onDone?.();
      },
    });
  }
}

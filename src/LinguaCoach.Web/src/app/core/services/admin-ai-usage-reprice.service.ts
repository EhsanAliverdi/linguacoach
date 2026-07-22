import { Injectable, signal } from '@angular/core';
import { ToastService } from './toast.service';
import { AiUsageService, AiUsageDateRange, AiUsageRecentCallFilter } from './ai-usage.service';

/**
 * Runs the "Fix $0-cost AI usage records" bulk repricing pass as a root-provided (app-lifetime)
 * service — same pattern as AdminBulkRepairService — so the progress toast keeps updating even if
 * the admin navigates to a different admin page mid-run. Batches through the backend
 * (POST reprice-zero-cost) rather than one HTTP call per row, since this is a DB-only recalculation,
 * not a per-item AI call.
 */
@Injectable({ providedIn: 'root' })
export class AdminAiUsageRepriceService {
  readonly running = signal(false);

  constructor(private ai: AiUsageService, private toast: ToastService) {}

  run(range: AiUsageDateRange | undefined, filters: AiUsageRecentCallFilter | undefined, totalZeroCost: number, onDone?: () => void): void {
    if (this.running()) return;
    this.running.set(true);

    const toastId = this.toast.showProgress(`Fixing $0-cost AI usage records: 0/${totalZeroCost}…`, 0, totalZeroCost);

    let fixedTotal = 0;
    let skippedTotal = 0;
    let costAddedTotal = 0;

    const step = () => {
      this.ai.repriceZeroCostBatch(range, filters, 200).subscribe({
        next: result => {
          fixedTotal += result.fixedInBatch;
          skippedTotal += result.skippedInBatch;
          costAddedTotal += result.costAddedUsd;
          const done = fixedTotal + skippedTotal;
          this.toast.updateProgress(
            toastId,
            `Fixing $0-cost AI usage records: ${Math.min(done, totalZeroCost)}/${totalZeroCost}…`,
            Math.min(done, totalZeroCost),
            totalZeroCost);

          // Stop once nothing changed this batch (no more fixable rows) or everything is done.
          if (result.processedInBatch === 0 || result.fixedInBatch === 0 || result.remainingZeroCost === 0) {
            this.finish(toastId, fixedTotal, skippedTotal, costAddedTotal, onDone);
          } else {
            step();
          }
        },
        error: () => {
          this.toast.dismiss(toastId);
          this.running.set(false);
          this.toast.error('Could not fix $0-cost AI usage records.');
          onDone?.();
        },
      });
    };

    step();
  }

  private finish(toastId: number, fixedTotal: number, skippedTotal: number, costAddedTotal: number, onDone?: () => void): void {
    this.toast.dismiss(toastId);
    this.running.set(false);
    if (fixedTotal === 0 && skippedTotal === 0) {
      this.toast.info('No $0-cost AI usage records found to fix.');
    } else if (skippedTotal > 0) {
      this.toast.warning(
        `Fixed ${fixedTotal} record(s) (+$${costAddedTotal.toFixed(4)}). ${skippedTotal} still have no resolvable pricing — ` +
        `add pricing for those provider/model pairs on AI Config → Model Pricing, then run Fix now again.`);
    } else {
      this.toast.success(`Fixed ${fixedTotal} record(s) — added $${costAddedTotal.toFixed(4)} in recovered cost.`);
    }
    onDone?.();
  }
}

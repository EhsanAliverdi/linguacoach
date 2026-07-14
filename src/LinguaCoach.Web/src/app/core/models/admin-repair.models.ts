// Phase K8 — one shared "diagnose then AI-repair" concept reused across the Resource Bank,
// Lesson, Exercise, and Module admin pages. See AdminRepairFieldGenerator's backend doc comment
// for the shared AI mechanism this is a thin frontend view of.

export interface DiagnosticIssue {
  code: string;
  message: string;
  autoFixable: boolean;
}

// ── Phase K9 — top-level issue count + bulk "Fix All with AI" ────────────────
export interface IssuesSummary {
  totalItems: number;
  itemsWithIssues: number;
}

export interface BulkRepairResult {
  itemsScanned: number;
  itemsWithIssues: number;
  itemsRepaired: number;
  itemsFailed: number;
  errors: string[];
}

// ── Phase K10 — client-side "Fix All with AI" progress loop ──────────────────
export interface RepairableItemSummary {
  id: string;
  title: string;
}

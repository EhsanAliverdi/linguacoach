import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
} from '../../../core/models/admin-resource-import.models';
import { DiagnosticIssue } from '../../../core/models/admin-repair.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSectionCardComponent,
} from '../../../design-system/admin';

/** Phase H3 mirror — only Vocabulary/Grammar/ReadingReference/ReadingPassage feed the Lesson
 *  generator so far (LessonResourceLookup doesn't read Writing/Listening/Speaking yet). */
const TYPES_SUPPORTING_GENERATION: ReadonlySet<UnifiedResourceBankItemType> =
  new Set(['vocabulary', 'grammar', 'readingReference', 'readingPassage']);

const RESOURCE_TYPE_TO_LESSON_TYPE: Record<UnifiedResourceBankItemType, string> = {
  vocabulary: 'Vocabulary',
  grammar: 'Grammar',
  readingReference: 'ReadingReference',
  readingPassage: 'ReadingPassage',
  writing: 'Writing',
  listening: 'Listening',
  speaking: 'Speaking',
};

/**
 * Phase K3 — Resource Bank item detail as its own routed page (/admin/resource-bank/:id),
 * replacing the old in-place slide-in drawer.
 *
 * Phase K5 — product decision: Resource Bank items only ever generate a Lesson now ("Generate
 * Activity"/"Generate Module" removed from here — Exercises come only from a Lesson, and Modules
 * are created automatically once a Lesson has Exercises, see AdminLessonDetailComponent). Also
 * adds admin Edit — AI/import-generated content is not immutable; an admin can correct it here.
 */
@Component({
  selector: 'app-admin-resource-bank-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSectionCardComponent,
  ],
  templateUrl: './admin-resource-bank-detail.component.html',
})
export class AdminResourceBankDetailComponent implements OnInit {
  itemId = '';
  item = signal<UnifiedResourceBankItemDto | null>(null);
  loading = signal(true);
  error = signal('');

  generateSuccess = signal('');
  generateError = signal('');

  generatingLearn = signal(false);
  generatingLearnAi = signal(false);
  archiving = signal(false);

  // ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────
  issues = signal<DiagnosticIssue[]>([]);
  repairing = signal(false);
  repairSuccess = signal('');
  repairError = signal('');

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private lessonSvc: AdminLessonService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.itemId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.itemId) return;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.bankSvc.get(this.itemId).subscribe({
      next: item => { this.item.set(item); this.loading.set(false); },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load this Resource Bank item.'); },
    });
    this.bankSvc.diagnose(this.itemId).subscribe({
      next: issues => this.issues.set(issues),
      error: () => this.issues.set([]),
    });
  }

  get autoFixableIssues(): DiagnosticIssue[] {
    return this.issues().filter(i => i.autoFixable);
  }

  fixWithAi(): void {
    this.repairing.set(true);
    this.repairError.set('');
    this.repairSuccess.set('');
    this.bankSvc.repair(this.itemId).subscribe({
      next: result => {
        this.repairing.set(false);
        this.repairSuccess.set(`Fixed ${result.issuesFixed.length} issue(s)` + (result.providerName ? ` using ${result.providerName}/${result.modelName}.` : '.'));
        this.load();
      },
      error: err => { this.repairing.set(false); this.repairError.set(err.error?.error ?? 'Could not repair this item.'); },
    });
  }

  backToList(): void {
    this.router.navigateByUrl('/admin/resource-bank');
  }

  typeLabel(type: UnifiedResourceBankItemType): string {
    return UNIFIED_RESOURCE_BANK_TYPES.find(t => t.value === type)?.label ?? type;
  }

  supportsGeneration(item: UnifiedResourceBankItemDto): boolean {
    return TYPES_SUPPORTING_GENERATION.has(item.type);
  }

  goToLessons(): void { this.router.navigateByUrl('/admin/lesson-library'); }

  generateLearn(): void {
    const item = this.item();
    if (!item) return;
    this.generatingLearn.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.lessonSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: () => {
        this.generatingLearn.set(false);
        this.generateSuccess.set(`Lesson draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingLearn.set(false); this.generateError.set(err.error?.error ?? 'Could not generate a Lesson.'); },
    });
  }

  generateLearnWithAi(): void {
    const item = this.item();
    if (!item) return;
    this.generatingLearnAi.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.lessonSvc.generateFromResourcesWithAi({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: () => {
        this.generatingLearnAi.set(false);
        this.generateSuccess.set(`AI-generated Lesson draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingLearnAi.set(false); this.generateError.set(err.error?.error ?? 'Could not generate a Lesson with AI.'); },
    });
  }

  archive(): void {
    const item = this.item();
    if (!item) return;
    this.archiving.set(true);
    this.bankSvc.archive([item.id]).subscribe({
      next: () => { this.archiving.set(false); this.backToList(); },
      error: err => { this.archiving.set(false); this.generateError.set(err.error?.error ?? 'Could not archive this item.'); },
    });
  }

  unarchive(): void {
    const item = this.item();
    if (!item) return;
    this.archiving.set(true);
    this.bankSvc.unarchive([item.id]).subscribe({
      next: () => { this.archiving.set(false); this.load(); },
      error: err => { this.archiving.set(false); this.generateError.set(err.error?.error ?? 'Could not unarchive this item.'); },
    });
  }

  goToEdit(): void {
    this.router.navigateByUrl(`/admin/resource-bank/${this.itemId}/edit`);
  }
}

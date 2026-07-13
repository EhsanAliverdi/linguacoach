import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AdminUnifiedResourceBankService } from '../../../core/services/admin-resource-import.service';
import { AdminLessonService } from '../../../core/services/admin-lesson.service';
import { AdminExerciseService } from '../../../core/services/admin-exercise.service';
import { AdminModuleService } from '../../../core/services/admin-module.service';
import {
  UnifiedResourceBankItemDto,
  UnifiedResourceBankItemType,
  UNIFIED_RESOURCE_BANK_TYPES,
} from '../../../core/models/admin-resource-import.models';
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

/** Phase H3/H4/H5 mirror — same "which types support generation" gate as the list page. */
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
 * replacing the old in-place slide-in drawer. Every action that used to live in the list page's
 * row-action dropdown (Generate Learn/Activity/Module, deterministic and AI variants) now lives
 * here instead — the list page's only per-row action is navigating here.
 */
@Component({
  selector: 'app-admin-resource-bank-detail',
  standalone: true,
  imports: [
    CommonModule,
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
  lastGeneratedKind = signal<'learn' | 'activity' | 'module' | null>(null);

  generatingLearn = signal(false);
  generatingLearnAi = signal(false);
  generatingActivity = signal(false);
  generatingActivityAi = signal(false);
  generatingModule = signal(false);
  generatingModuleAi = signal(false);
  archiving = signal(false);

  constructor(
    private bankSvc: AdminUnifiedResourceBankService,
    private lessonSvc: AdminLessonService,
    private exerciseSvc: AdminExerciseService,
    private moduleSvc: AdminModuleService,
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
  goToActivities(): void { this.router.navigateByUrl('/admin/exercises'); }
  goToModules(): void { this.router.navigateByUrl('/admin/modules'); }

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
        this.lastGeneratedKind.set('learn');
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
        this.lastGeneratedKind.set('learn');
        this.generateSuccess.set(`AI-generated Lesson draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingLearnAi.set(false); this.generateError.set(err.error?.error ?? 'Could not generate a Lesson with AI.'); },
    });
  }

  generateActivity(): void {
    const item = this.item();
    if (!item) return;
    this.generatingActivity.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.exerciseSvc.generateFromResources({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: () => {
        this.generatingActivity.set(false);
        this.lastGeneratedKind.set('activity');
        this.generateSuccess.set(`Exercise draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingActivity.set(false); this.generateError.set(err.error?.error ?? 'Could not generate an Exercise.'); },
    });
  }

  generateActivityWithAi(): void {
    const item = this.item();
    if (!item) return;
    this.generatingActivityAi.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.exerciseSvc.generateFromResourcesWithAi({
      resources: [{ resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type], resourceId: item.id, role: 'Primary' }],
    }).subscribe({
      next: () => {
        this.generatingActivityAi.set(false);
        this.lastGeneratedKind.set('activity');
        this.generateSuccess.set(`AI-generated Exercise draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingActivityAi.set(false); this.generateError.set(err.error?.error ?? 'Could not generate an Exercise with AI.'); },
    });
  }

  generateModule(): void {
    const item = this.item();
    if (!item) return;
    this.generatingModule.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.moduleSvc.generateFromResource({
      resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type],
      resourceId: item.id,
    }).subscribe({
      next: () => {
        this.generatingModule.set(false);
        this.lastGeneratedKind.set('module');
        this.generateSuccess.set(`Module draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingModule.set(false); this.generateError.set(err.error?.error ?? 'Could not generate a Module.'); },
    });
  }

  generateModuleWithAi(): void {
    const item = this.item();
    if (!item) return;
    this.generatingModuleAi.set(true);
    this.generateError.set('');
    this.generateSuccess.set('');
    this.moduleSvc.generateFromResourceWithAi({
      resourceType: RESOURCE_TYPE_TO_LESSON_TYPE[item.type],
      resourceId: item.id,
    }).subscribe({
      next: () => {
        this.generatingModuleAi.set(false);
        this.lastGeneratedKind.set('module');
        this.generateSuccess.set(`AI-generated Module draft created from "${item.title}" — pending review.`);
        this.load();
      },
      error: err => { this.generatingModuleAi.set(false); this.generateError.set(err.error?.error ?? 'Could not generate a Module with AI.'); },
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
}

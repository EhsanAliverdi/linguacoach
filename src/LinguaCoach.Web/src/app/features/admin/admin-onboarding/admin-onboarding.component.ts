import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminOnboardingService } from '../../../core/services/admin-onboarding.service';
import { StudentFlowTemplateSummaryDto } from '../../../core/models/admin-onboarding.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';
import type { SpAdminRowAction } from '../../../design-system/admin';

/**
 * Onboarding templates list page. Authoring (the Form.io builder) lives on its own page
 * (AdminOnboardingEditorComponent, /admin/onboarding/:templateId) — kept separate from this
 * table so the builder gets a full page rather than fighting for space under a list.
 */
@Component({
  selector: 'app-admin-onboarding',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminModalComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminTableActionsComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-onboarding.component.html',
})
export class AdminOnboardingComponent implements OnInit {
  templates = signal<StudentFlowTemplateSummaryDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  createFormOpen = signal(false);
  newTemplateName = '';
  newTemplateDescription = '';

  readonly templateColumns = [
    { key: 'name', label: 'Name' },
    { key: 'status', label: 'Status' },
    { key: 'versionCount', label: 'Versions' },
    { key: 'updatedAt', label: 'Updated' },
    { key: '_actions', label: '' },
  ];

  readonly publishedCount = computed(() => this.templates().filter(t => t.status === 'Published').length);

  constructor(private svc: AdminOnboardingService, private router: Router) {}

  ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.listTemplates().subscribe({
      next: templates => {
        this.templates.set(templates);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not load onboarding templates.');
      },
    });
  }

  openCreateForm(): void {
    this.newTemplateName = '';
    this.newTemplateDescription = '';
    this.actionError.set('');
    this.createFormOpen.set(true);
  }

  closeCreateForm(): void {
    this.createFormOpen.set(false);
  }

  createTemplate(): void {
    if (!this.newTemplateName.trim()) return;
    this.actionError.set('');
    this.svc.createTemplate({ name: this.newTemplateName.trim(), description: this.newTemplateDescription.trim() || undefined }).subscribe({
      next: detail => {
        this.createFormOpen.set(false);
        this.router.navigate(['/admin/onboarding', detail.templateId]);
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not create template.'),
    });
  }

  editTemplate(templateId: string): void {
    this.router.navigate(['/admin/onboarding', templateId]);
  }

  rowActions(row: StudentFlowTemplateSummaryDto): SpAdminRowAction[] {
    const actions: SpAdminRowAction[] = [{ id: 'edit', label: 'Edit', icon: 'edit' }];
    if (row.status !== 'Archived') {
      actions.push({ id: 'archive', label: 'Archive', icon: 'archive', tone: 'danger' });
    }
    return actions;
  }

  onRowAction(actionId: string, row: StudentFlowTemplateSummaryDto): void {
    switch (actionId) {
      case 'edit': this.editTemplate(row.templateId); break;
      case 'archive': this.archive(row.templateId); break;
    }
  }

  archive(templateId: string): void {
    this.actionError.set('');
    this.svc.archive(templateId).subscribe({
      next: () => {
        this.actionSuccess.set('Template archived.');
        this.loadTemplates();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not archive template.'),
    });
  }

  statusTone(status: string): 'success' | 'neutral' | 'warning' {
    if (status === 'Published') return 'success';
    if (status === 'Draft') return 'warning';
    return 'neutral';
  }
}

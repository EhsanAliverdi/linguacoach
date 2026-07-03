import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import {
  AdminPlacementItemDto,
  PlacementItemRequest,
  PLACEMENT_SKILLS,
  PLACEMENT_CEFR_LEVELS,
  PLACEMENT_ITEM_TYPES,
} from '../../../core/models/admin-placement-item.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCheckboxComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminSelectComponent,
  SpAdminSlideOverComponent,
  SpAdminTableComponent,
  SpAdminTextareaComponent,
} from '../../../design-system/admin';

@Component({
  selector: 'app-admin-placement-items',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCheckboxComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminSelectComponent,
    SpAdminSlideOverComponent,
    SpAdminTableComponent,
    SpAdminTextareaComponent,
  ],
  templateUrl: './admin-placement-items.component.html',
})
export class AdminPlacementItemsComponent implements OnInit {
  items = signal<AdminPlacementItemDto[]>([]);
  loading = signal(true);
  error = signal('');
  actionError = signal('');
  actionSuccess = signal('');

  slideOverOpen = signal(false);
  editingItem = signal<AdminPlacementItemDto | null>(null);

  itemForm: PlacementItemRequest = this.emptyItemForm();

  skillFilter = signal<string>('all');

  readonly skillOptions = [{ value: 'all', label: 'All skills' }, ...PLACEMENT_SKILLS.map(s => ({ value: s, label: s }))];
  readonly formSkillOptions = PLACEMENT_SKILLS.map(s => ({ value: s, label: s }));
  readonly cefrLevelOptions = PLACEMENT_CEFR_LEVELS.map(l => ({ value: l, label: l }));
  readonly itemTypeOptions = PLACEMENT_ITEM_TYPES.map(t => ({ value: t, label: t }));

  readonly filteredItems = computed(() => {
    const filter = this.skillFilter();
    return filter === 'all' ? this.items() : this.items().filter(i => i.skill === filter);
  });

  readonly totalItems = computed(() => this.items().length);
  readonly enabledItems = computed(() => this.items().filter(i => i.isEnabled).length);
  readonly skillCount = computed(() => new Set(this.items().map(i => i.skill)).size);

  readonly itemColumns = [
    { key: 'skill', label: 'Skill' },
    { key: 'cefrLevel', label: 'Level' },
    { key: 'itemType', label: 'Type' },
    { key: 'prompt', label: 'Prompt' },
    { key: 'isEnabled', label: 'Enabled' },
    { key: '_actions', label: '' },
  ];

  constructor(private svc: AdminPlacementItemService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.error.set('');
    this.svc.list().subscribe({
      next: items => { this.items.set(items); this.loading.set(false); },
      error: err => { this.loading.set(false); this.error.set(err.error?.error ?? 'Could not load placement items.'); },
    });
  }

  openAddItem(): void {
    this.editingItem.set(null);
    this.itemForm = this.emptyItemForm();
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  openEditItem(item: AdminPlacementItemDto): void {
    this.editingItem.set(item);
    this.itemForm = {
      skill: item.skill,
      cefrLevel: item.cefrLevel,
      itemType: item.itemType,
      prompt: item.prompt,
      correctAnswer: item.correctAnswer,
      readingPassage: item.readingPassage,
      listeningAudioScript: item.listeningAudioScript,
      itemOrder: item.itemOrder,
      isEnabled: item.isEnabled,
    };
    this.actionError.set('');
    this.actionSuccess.set('');
    this.slideOverOpen.set(true);
  }

  closeSlideOver(): void {
    this.slideOverOpen.set(false);
    this.editingItem.set(null);
  }

  saveItem(): void {
    this.actionError.set('');
    const editing = this.editingItem();
    const obs = editing
      ? this.svc.update(editing.itemId, this.itemForm)
      : this.svc.add(this.itemForm);

    obs.subscribe({
      next: () => {
        this.actionSuccess.set(editing ? 'Item updated.' : 'Item added.');
        this.slideOverOpen.set(false);
        this.loadAll();
      },
      error: err => this.actionError.set(err.error?.error ?? 'Could not save item.'),
    });
  }

  removeItem(item: AdminPlacementItemDto): void {
    this.actionError.set('');
    this.svc.remove(item.itemId).subscribe({
      next: () => { this.actionSuccess.set('Item removed.'); this.loadAll(); },
      error: err => this.actionError.set(err.error?.error ?? 'Could not remove item.'),
    });
  }

  itemTone(item: AdminPlacementItemDto): 'success' | 'neutral' {
    return item.isEnabled ? 'success' : 'neutral';
  }

  private emptyItemForm(): PlacementItemRequest {
    return {
      skill: 'grammar',
      cefrLevel: 'A1',
      itemType: 'multiple_choice',
      prompt: '',
      correctAnswer: '',
      readingPassage: null,
      listeningAudioScript: null,
      itemOrder: 1,
      isEnabled: true,
    };
  }
}

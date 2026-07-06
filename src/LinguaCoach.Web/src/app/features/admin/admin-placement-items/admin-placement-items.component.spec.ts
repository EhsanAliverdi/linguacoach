import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminPlacementItemsComponent } from './admin-placement-items.component';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import { AdminPlacementItemDto } from '../../../core/models/admin-placement-item.models';

const ITEM_A: AdminPlacementItemDto = {
  itemId: 'item-1',
  skill: 'grammar',
  cefrLevel: 'A1',
  itemType: 'multiple_choice',
  prompt: 'Which is correct?',
  correctAnswer: 'A',
  readingPassage: null,
  listeningAudioScript: null,
  itemOrder: 1,
  isEnabled: true,
  content: {
    type: 'single_choice',
    id: 'q1',
    questionText: 'Which is correct?',
    choices: [{ key: 'A', label: 'am' }, { key: 'B', label: 'is' }],
    correctAnswerKey: 'A',
  },
  formIoSchemaJson: null,
  scoringRulesJson: null,
};

const ITEM_B: AdminPlacementItemDto = {
  itemId: 'item-2',
  skill: 'listening',
  cefrLevel: 'B1',
  itemType: 'gap_fill',
  prompt: 'You hear: complete the sentence.',
  correctAnswer: 'extended',
  readingPassage: null,
  listeningAudioScript: 'The deadline has been extended.',
  itemOrder: 2,
  isEnabled: false,
  content: {
    type: 'listening_group',
    id: 'g1',
    audioScript: 'The deadline has been extended.',
    questions: [{ type: 'gap_fill', id: 'q1', questionText: 'You hear: complete the sentence.', correctAnswer: 'extended' }],
  },
  formIoSchemaJson: null,
  scoringRulesJson: null,
};

function makeService(items: AdminPlacementItemDto[] = [ITEM_A, ITEM_B]) {
  return {
    list: jasmine.createSpy('list').and.returnValue(of(items)),
    add: jasmine.createSpy('add').and.returnValue(of(ITEM_A)),
    update: jasmine.createSpy('update').and.returnValue(of(ITEM_A)),
    remove: jasmine.createSpy('remove').and.returnValue(of(void 0)),
  };
}

describe('AdminPlacementItemsComponent', () => {
  let fixture: ComponentFixture<AdminPlacementItemsComponent>;
  let component: AdminPlacementItemsComponent;
  let svc: ReturnType<typeof makeService>;

  async function setup(items: AdminPlacementItemDto[] = [ITEM_A, ITEM_B]) {
    svc = makeService(items);
    await TestBed.configureTestingModule({
      imports: [AdminPlacementItemsComponent],
      providers: [{ provide: AdminPlacementItemService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPlacementItemsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page heading', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Placement items');
  });

  it('loading signal starts true before data arrives', () => {
    svc = makeService();
    TestBed.configureTestingModule({
      imports: [AdminPlacementItemsComponent],
      providers: [{ provide: AdminPlacementItemService, useValue: svc }],
    });
    fixture = TestBed.createComponent(AdminPlacementItemsComponent);
    component = fixture.componentInstance;
    expect(component.loading()).toBeTrue();
  });

  it('calls list on init', async () => {
    await setup();
    expect(svc.list).toHaveBeenCalledTimes(1);
  });

  it('populates items signal after load', async () => {
    await setup();
    expect(component.items().length).toBe(2);
  });

  it('loading is false after data arrives', async () => {
    await setup();
    expect(component.loading()).toBeFalse();
  });

  it('shows error state when list fails', async () => {
    svc = makeService();
    svc.list.and.returnValue(throwError(() => ({ error: { error: 'Server error' } })));
    await TestBed.configureTestingModule({
      imports: [AdminPlacementItemsComponent],
      providers: [{ provide: AdminPlacementItemService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPlacementItemsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toBeTruthy();
  });

  // ── KPI computed signals ──────────────────────────────────────────────────

  it('totalItems computed reflects item count', async () => {
    await setup();
    expect(component.totalItems()).toBe(2);
  });

  it('enabledItems computed counts only enabled items', async () => {
    await setup();
    expect(component.enabledItems()).toBe(1);
  });

  it('skillCount computed counts distinct skills', async () => {
    await setup();
    expect(component.skillCount()).toBe(2);
  });

  // ── Skill filter ───────────────────────────────────────────────────────────

  it('filteredItems returns all items when filter is "all"', async () => {
    await setup();
    expect(component.filteredItems().length).toBe(2);
  });

  it('filteredItems filters by selected skill', async () => {
    await setup();
    component.skillFilter.set('grammar');
    expect(component.filteredItems().length).toBe(1);
    expect(component.filteredItems()[0].itemId).toBe('item-1');
  });

  // ── Slide-over ──────────────────────────────────────────────────────────────

  it('slideOverOpen is false initially', async () => {
    await setup();
    expect(component.slideOverOpen()).toBeFalse();
  });

  it('openAddItem opens slide-over with null editingItem', async () => {
    await setup();
    component.openAddItem();
    expect(component.slideOverOpen()).toBeTrue();
    expect(component.editingItem()).toBeNull();
  });

  it('openAddItem resets itemForm to defaults', async () => {
    await setup();
    component.openAddItem();
    expect(component.itemForm.content.type).toBe('single_choice');
    expect(component.itemForm.isEnabled).toBeTrue();
    expect(component.itemForm.skill).toBe('grammar');
  });

  it('openEditItem opens slide-over with selected item', async () => {
    await setup();
    component.openEditItem(ITEM_B);
    expect(component.slideOverOpen()).toBeTrue();
    expect(component.editingItem()).toBe(ITEM_B);
  });

  it('openEditItem populates itemForm from selected item', async () => {
    await setup();
    component.openEditItem(ITEM_B);
    expect(component.itemForm.skill).toBe('listening');
    expect(component.itemForm.content).toEqual(ITEM_B.content);
    expect(component.itemForm.isEnabled).toBeFalse();
  });

  it('closeSlideOver closes slide-over and clears editingItem', async () => {
    await setup();
    component.openEditItem(ITEM_A);
    component.closeSlideOver();
    expect(component.slideOverOpen()).toBeFalse();
    expect(component.editingItem()).toBeNull();
  });

  // ── Save item ──────────────────────────────────────────────────────────────

  it('saveItem calls add when editingItem is null', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.updateContent({ ...component.itemForm.content, questionText: 'New prompt' } as any);
    component.saveItem();
    tick();
    expect(svc.add).toHaveBeenCalledWith(jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
      content: component.itemForm.content,
    }));
  }));

  it('saveItem calls update when editingItem is set', fakeAsync(async () => {
    await setup();
    component.openEditItem(ITEM_B);
    component.saveItem();
    tick();
    expect(svc.update).toHaveBeenCalledWith('item-2', jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
      content: component.itemForm.content,
    }));
  }));

  it('saveItem includes formIoSchemaJson and scoringRulesJson when Form.io authoring is enabled', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioEnabled.set(true);
    component.formioSchema.set({ display: 'form', components: [] });
    component.scoringRulesJson.set('{"assessment_q1":{"correctAnswerKey":"b"}}');
    component.saveItem();
    tick();
    expect(svc.add).toHaveBeenCalledWith(jasmine.objectContaining({
      formIoSchemaJson: jasmine.any(String),
      scoringRulesJson: '{"assessment_q1":{"correctAnswerKey":"b"}}',
    }));
  }));

  it('saveItem closes slide-over on success', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.saveItem();
    tick();
    expect(component.slideOverOpen()).toBeFalse();
  }));

  it('saveItem sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.saveItem();
    tick();
    expect(component.actionSuccess()).toBe('Item added.');
  }));

  it('saveItem sets actionError on failure', fakeAsync(async () => {
    await setup();
    svc.add.and.returnValue(throwError(() => ({ error: { error: 'Validation failed' } })));
    component.openAddItem();
    component.saveItem();
    tick();
    expect(component.actionError()).toContain('Validation failed');
  }));

  // ── Remove item ────────────────────────────────────────────────────────────

  it('removeItem calls service with correct itemId', fakeAsync(async () => {
    await setup();
    component.removeItem(ITEM_A);
    tick();
    expect(svc.remove).toHaveBeenCalledWith('item-1');
  }));

  it('removeItem sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.removeItem(ITEM_A);
    tick();
    expect(component.actionSuccess()).toBe('Item removed.');
  }));

  // ── itemTone helper ────────────────────────────────────────────────────────

  it('itemTone returns success for enabled item', async () => {
    await setup();
    expect(component.itemTone(ITEM_A)).toBe('success');
  });

  it('itemTone returns neutral for disabled item', async () => {
    await setup();
    expect(component.itemTone(ITEM_B)).toBe('neutral');
  });
});

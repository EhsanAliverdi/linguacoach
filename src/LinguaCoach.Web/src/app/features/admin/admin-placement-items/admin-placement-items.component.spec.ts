import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminPlacementItemsComponent } from './admin-placement-items.component';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import { AdminPlacementItemDto } from '../../../core/models/admin-placement-item.models';

const SCHEMA_WITH_ANSWER = JSON.stringify({
  display: 'form',
  components: [{ type: 'radio', key: 'answer', label: 'Which is correct?', values: [{ label: 'am', value: 'A' }, { label: 'is', value: 'B' }] }],
});

const ITEM_A: AdminPlacementItemDto = {
  itemId: 'item-1',
  skill: 'grammar',
  cefrLevel: 'A1',
  itemType: 'multiple_choice',
  prompt: 'Which is correct?',
  itemOrder: 1,
  isEnabled: true,
  formIoSchemaJson: SCHEMA_WITH_ANSWER,
  scoringRulesJson: '{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}',
  scoringRulesVersion: 1,
  rendererKind: 'FormIo',
};

const ITEM_B: AdminPlacementItemDto = {
  itemId: 'item-2',
  skill: 'listening',
  cefrLevel: 'B1',
  itemType: 'gap_fill',
  prompt: 'You hear: complete the sentence.',
  itemOrder: 2,
  isEnabled: false,
  formIoSchemaJson: JSON.stringify({ display: 'form', components: [{ type: 'textfield', key: 'answer', label: 'Answer' }] }),
  scoringRulesJson: '{"components":{"answer":{"kind":"text_normalized","correctAnswer":"extended"}}}',
  scoringRulesVersion: 1,
  rendererKind: 'FormIo',
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
    expect(component.itemForm.itemType).toBe('multiple_choice');
    expect(component.itemForm.isEnabled).toBeTrue();
    expect(component.itemForm.skill).toBe('grammar');
  });

  it('openEditItem opens slide-over with selected item', async () => {
    await setup();
    component.openEditItem(ITEM_B);
    expect(component.slideOverOpen()).toBeTrue();
    expect(component.editingItem()).toBe(ITEM_B);
  });

  it('openEditItem populates itemForm and formioSchema from selected item', async () => {
    await setup();
    component.openEditItem(ITEM_B);
    expect(component.itemForm.skill).toBe('listening');
    expect(component.itemForm.prompt).toBe(ITEM_B.prompt);
    expect(component.itemForm.isEnabled).toBeFalse();
    expect(component.formioSchema()).toEqual(JSON.parse(ITEM_B.formIoSchemaJson!));
    expect(component.scoringRulesJson()).toBe(ITEM_B.scoringRulesJson!);
  });

  it('closeSlideOver closes slide-over and clears editingItem', async () => {
    await setup();
    component.openEditItem(ITEM_A);
    component.closeSlideOver();
    expect(component.slideOverOpen()).toBeFalse();
    expect(component.editingItem()).toBeNull();
  });

  // ── Form.io builder is always rendered (no more formioEnabled toggle) ──────

  it('FormioBuilderComponent has no conditional toggle — the component no longer exposes formioEnabled', async () => {
    await setup();
    expect((component as any).formioEnabled).toBeUndefined();
  });

  // ── schemaComponentKeys ──────────────────────────────────────────────────

  it('schemaComponentKeys flattens leaf component keys from the current schema', async () => {
    await setup();
    component.openEditItem(ITEM_A);
    expect(component.schemaComponentKeys()).toEqual(['answer']);
  });

  it('schemaComponentKeys is empty for a schema with no components', async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [] });
    expect(component.schemaComponentKeys()).toEqual([]);
  });

  // ── Save item ──────────────────────────────────────────────────────────────

  it('saveItem calls add with formIoSchemaJson and scoringRulesJson, no content field', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}');
    component.itemForm.prompt = 'New prompt';
    component.saveItem();
    tick();
    expect(svc.add).toHaveBeenCalledWith(jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
      prompt: 'New prompt',
      formIoSchemaJson: jasmine.any(String),
      scoringRulesJson: '{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}',
    }));
    const savedArgs = svc.add.calls.mostRecent().args[0];
    expect(savedArgs.content).toBeUndefined();
  }));

  it('saveItem calls update when editingItem is set', fakeAsync(async () => {
    await setup();
    component.openEditItem(ITEM_B);
    component.saveItem();
    tick();
    expect(svc.update).toHaveBeenCalledWith('item-2', jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
    }));
  }));

  it('saveItem rejects invalid scoring rules JSON and does not call add', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{not valid json');
    component.saveItem();
    tick();
    expect(svc.add).not.toHaveBeenCalled();
    expect(component.scoringRulesError()).toContain('invalid');
  }));

  it('saveItem rejects scoring rules referencing a component key not present in the schema', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{"components":{"orphanKey":{"kind":"single_choice","correctAnswer":"A"}}}');
    component.saveItem();
    tick();
    expect(svc.add).not.toHaveBeenCalled();
    expect(component.scoringRulesError()).toContain('orphanKey');
  }));

  it('saveItem rejects empty scoring rules', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('');
    component.saveItem();
    tick();
    expect(svc.add).not.toHaveBeenCalled();
    expect(component.scoringRulesError()).toBeTruthy();
  }));

  it('saveItem closes slide-over on success', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}');
    component.saveItem();
    tick();
    expect(component.slideOverOpen()).toBeFalse();
  }));

  it('saveItem sets actionSuccess on success', fakeAsync(async () => {
    await setup();
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}');
    component.saveItem();
    tick();
    expect(component.actionSuccess()).toBe('Item added.');
  }));

  it('saveItem sets actionError on failure', fakeAsync(async () => {
    await setup();
    svc.add.and.returnValue(throwError(() => ({ error: { error: 'Validation failed' } })));
    component.openAddItem();
    component.formioSchema.set({ display: 'form', components: [{ type: 'radio', key: 'answer', label: 'Q' }] });
    component.scoringRulesJson.set('{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}');
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

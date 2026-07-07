import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminPlacementItemEditorComponent } from './admin-placement-item-editor.component';
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
  itemOrder: 1,
  isEnabled: true,
  formIoSchemaJson: SCHEMA_WITH_ANSWER,
  scoringRulesJson: '{"components":{"answer":{"kind":"single_choice","correctAnswer":"A"}}}',
  scoringRulesVersion: 1,
  rendererKind: 'FormIo',
  questionPreview: 'Which is correct?',
  authoringSchemaJson: null,
};

const ITEM_B: AdminPlacementItemDto = {
  itemId: 'item-2',
  skill: 'listening',
  cefrLevel: 'B1',
  itemOrder: 2,
  isEnabled: false,
  formIoSchemaJson: JSON.stringify({ display: 'form', components: [{ type: 'textfield', key: 'answer', label: 'Answer' }] }),
  scoringRulesJson: '{"components":{"answer":{"kind":"text_normalized","correctAnswer":"extended"}}}',
  scoringRulesVersion: 1,
  rendererKind: 'FormIo',
  questionPreview: 'Answer',
  authoringSchemaJson: JSON.stringify({
    display: 'form',
    components: [{
      type: 'textfield', key: 'answer', label: 'Answer',
      quiz: { enabled: true, rule: { kind: 'text_normalized', correctAnswer: 'extended', points: 1 } },
    }],
  }),
};

function makeService(items: AdminPlacementItemDto[] = [ITEM_A, ITEM_B]) {
  return {
    get: jasmine.createSpy('get').and.callFake((itemId: string) => {
      const item = items.find(i => i.itemId === itemId);
      return item ? of(item) : throwError(() => ({ status: 404 }));
    }),
    add: jasmine.createSpy('add').and.returnValue(of(ITEM_A)),
    update: jasmine.createSpy('update').and.returnValue(of(ITEM_A)),
  };
}

describe('AdminPlacementItemEditorComponent', () => {
  let fixture: ComponentFixture<AdminPlacementItemEditorComponent>;
  let component: AdminPlacementItemEditorComponent;
  let svc: ReturnType<typeof makeService>;
  let router: Router;
  let navigateSpy: jasmine.Spy;

  async function setup(itemId: string, items: AdminPlacementItemDto[] = [ITEM_A, ITEM_B]) {
    svc = makeService(items);
    await TestBed.configureTestingModule({
      imports: [AdminPlacementItemEditorComponent],
      providers: [
        provideRouter([]),
        { provide: AdminPlacementItemService, useValue: svc },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ itemId }) } },
        },
      ],
    }).compileComponents();
    router = TestBed.inject(Router);
    navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(AdminPlacementItemEditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  // ── Add mode ("new") ─────────────────────────────────────────────────────

  it('treats "new" as add mode without calling get', async () => {
    await setup('new');
    expect(component.isNew).toBeTrue();
    expect(component.loading()).toBeFalse();
    expect(svc.get).not.toHaveBeenCalled();
  });

  it('resets itemForm to defaults in add mode', async () => {
    await setup('new');
    expect(component.itemForm.isEnabled).toBeTrue();
    expect(component.itemForm.skill).toBe('grammar');
  });

  it('saveItem calls add with authoringSchemaJson, no legacy fields', fakeAsync(async () => {
    await setup('new');
    component.formioSchema.set({
      display: 'form',
      components: [{ type: 'radio', key: 'answer', label: 'Q', quiz: { enabled: true, rule: { correctAnswer: 'A' } } }],
    });
    component.saveItem();
    tick();
    expect(svc.add).toHaveBeenCalledWith(jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
      authoringSchemaJson: jasmine.any(String),
    }));
    const savedArgs = svc.add.calls.mostRecent().args[0];
    expect(savedArgs.itemType).toBeUndefined();
    expect(savedArgs.prompt).toBeUndefined();

    // finalizeQuizAnnotations derives the "kind" from the component type before save.
    const authoringSchema = JSON.parse(savedArgs.authoringSchemaJson);
    expect(authoringSchema.components[0].quiz.rule.kind).toBe('single_choice');
  }));

  it('saveItem navigates back to the item list on success', fakeAsync(async () => {
    await setup('new');
    component.saveItem();
    tick();
    expect(navigateSpy).toHaveBeenCalledWith(['/admin/placement-items']);
  }));

  it('saveItem sets actionError on failure', fakeAsync(async () => {
    await setup('new');
    svc.add.and.returnValue(throwError(() => ({ error: { error: 'Validation failed' } })));
    component.saveItem();
    tick();
    expect(component.actionError()).toContain('Validation failed');
  }));

  // ── Edit mode (existing itemId) ──────────────────────────────────────────

  it('loads the matching item via the get endpoint and populates the form', async () => {
    await setup('item-2');
    expect(component.isNew).toBeFalse();
    expect(component.itemForm.skill).toBe('listening');
    expect(component.itemForm.isEnabled).toBeFalse();
    // The live Form.io builder normalizes the schema on render (adds ids/defaults/a submit
    // button), so assert on the authored component rather than deep-equality of the whole tree.
    expect(component.formioSchema().components[0].type).toBe('textfield');
    expect(component.formioSchema().components[0].key).toBe('answer');
  });

  it('seeds from authoringSchemaJson (quiz-annotated) when present, not formIoSchemaJson', async () => {
    await setup('item-2');
    expect(component.formioSchema().components[0].quiz?.enabled).toBeTrue();
    expect(component.needsReauthoring()).toBeFalse();
  });

  it('sets needsReauthoring when scoringRulesJson exists but authoringSchemaJson does not (legacy item)', async () => {
    await setup('item-1');
    expect(component.needsReauthoring()).toBeTrue();
  });

  it('sets an error when the itemId is not found (404)', async () => {
    await setup('does-not-exist');
    expect(component.error()).toBeTruthy();
  });

  it('saveItem calls update with the route itemId', fakeAsync(async () => {
    await setup('item-2');
    component.saveItem();
    tick();
    expect(svc.update).toHaveBeenCalledWith('item-2', jasmine.objectContaining({
      skill: component.itemForm.skill,
      cefrLevel: component.itemForm.cefrLevel,
    }));
  }));

  // ── scoredSummary ──────────────────────────────────────────────────────────

  it('scoredSummary counts scorable components with quiz enabled', async () => {
    await setup('item-2');
    expect(component.scoredSummary()).toEqual({ scored: 1, total: 1 });
  });

  it('scoredSummary is 0 of 0 for a schema with no scorable components', async () => {
    await setup('new');
    component.formioSchema.set({ display: 'form', components: [] });
    expect(component.scoredSummary()).toEqual({ scored: 0, total: 0 });
  });
});

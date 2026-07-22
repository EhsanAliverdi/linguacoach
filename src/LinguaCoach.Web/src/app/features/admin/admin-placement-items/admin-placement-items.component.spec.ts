import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminPlacementItemsComponent } from './admin-placement-items.component';
import { AdminPlacementItemService } from '../../../core/services/admin-placement-item.service';
import { AdminPlacementItemDto, AdminPlacementItemListResult } from '../../../core/models/admin-placement-item.models';

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
  difficultyBand: 1,
  discriminationIndex: null,
  calibrationSampleSize: null,
  evidenceWeight: 1,
  reviewStatus: 'NotRequired',
  itemVersion: 1,
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
  authoringSchemaJson: null,
  difficultyBand: 1,
  discriminationIndex: null,
  calibrationSampleSize: null,
  evidenceWeight: 1,
  reviewStatus: 'NotRequired',
  itemVersion: 1,
};

function makeResult(items: AdminPlacementItemDto[]): AdminPlacementItemListResult {
  return {
    items,
    totalCount: items.length,
    overallTotalCount: items.length,
    enabledCount: items.filter(i => i.isEnabled).length,
    skillCount: new Set(items.map(i => i.skill)).size,
  };
}

function makeService(result: AdminPlacementItemListResult = makeResult([ITEM_A, ITEM_B])) {
  return {
    list: jasmine.createSpy('list').and.returnValue(of(result)),
    remove: jasmine.createSpy('remove').and.returnValue(of(void 0)),
  };
}

describe('AdminPlacementItemsComponent', () => {
  let fixture: ComponentFixture<AdminPlacementItemsComponent>;
  let component: AdminPlacementItemsComponent;
  let svc: ReturnType<typeof makeService>;

  async function setup(result: AdminPlacementItemListResult = makeResult([ITEM_A, ITEM_B])) {
    svc = makeService(result);
    await TestBed.configureTestingModule({
      imports: [AdminPlacementItemsComponent],
      providers: [provideRouter([]), { provide: AdminPlacementItemService, useValue: svc }],
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
      providers: [provideRouter([]), { provide: AdminPlacementItemService, useValue: svc }],
    });
    fixture = TestBed.createComponent(AdminPlacementItemsComponent);
    component = fixture.componentInstance;
    expect(component.loading()).toBeTrue();
  });

  it('calls list with page 1, default page size, and "all" skill on init', async () => {
    await setup();
    expect(svc.list).toHaveBeenCalledWith(1, component.pageSize, 'all', '');
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
      providers: [provideRouter([]), { provide: AdminPlacementItemService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminPlacementItemsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toBeTruthy();
  });

  // ── KPI signals (global, unfiltered) ──────────────────────────────────────

  it('overallTotalCount reflects the server-reported unfiltered total', async () => {
    await setup();
    expect(component.overallTotalCount()).toBe(2);
  });

  it('enabledCount reflects the server-reported unfiltered enabled count', async () => {
    await setup();
    expect(component.enabledCount()).toBe(1);
  });

  it('skillCount reflects the server-reported distinct skill count', async () => {
    await setup();
    expect(component.skillCount()).toBe(2);
  });

  // ── Skill filter (server-side) ────────────────────────────────────────────

  it('onSkillFilterChange resets to page 1 and re-fetches with the new skill', async () => {
    await setup();
    component.page.set(3);
    component.onSkillFilterChange('grammar');
    expect(component.page()).toBe(1);
    expect(svc.list).toHaveBeenCalledWith(1, component.pageSize, 'grammar', '');
  });

  // ── Search (server-side, debounced) ───────────────────────────────────────

  it('onSearchChange resets to page 1 immediately but debounces the re-fetch', fakeAsync(async () => {
    await setup();
    component.page.set(3);
    component.onSearchChange('turn left');
    expect(component.page()).toBe(1);
    expect(svc.list).toHaveBeenCalledTimes(1); // only the initial load so far
    tick(300);
    expect(svc.list).toHaveBeenCalledWith(1, component.pageSize, 'all', 'turn left');
  }));

  // ── Pagination ─────────────────────────────────────────────────────────────

  it('totalPages computed from totalCount and pageSize', async () => {
    await setup(makeResult([ITEM_A, ITEM_B]));
    component.totalCount.set(45);
    expect(component.totalPages()).toBe(Math.ceil(45 / component.pageSize));
  });

  it('onPageChange updates page and re-fetches', async () => {
    await setup();
    component.onPageChange(2);
    expect(component.page()).toBe(2);
    expect(svc.list).toHaveBeenCalledWith(2, component.pageSize, 'all', '');
  });

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

  // ── Row actions ────────────────────────────────────────────────────────────

  it('onRowAction "remove" calls removeItem', fakeAsync(async () => {
    await setup();
    component.onRowAction('remove', ITEM_A);
    tick();
    expect(svc.remove).toHaveBeenCalledWith('item-1');
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

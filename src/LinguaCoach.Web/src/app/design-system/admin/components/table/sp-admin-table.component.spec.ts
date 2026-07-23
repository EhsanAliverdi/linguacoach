import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminTableComponent, SpAdminTableColumn } from './sp-admin-table.component';

// Regression test (2026-07-24 user report): "clicking on one row shows all selected, clicking
// it again unselects all" on every table with bulk edit enabled. Root cause: the per-row bulk
// checkbox rendered inside the nested `@for (column of columns; ...)` loop read `$index`, which
// Angular's new control-flow syntax resolves to the nearest enclosing `@for`'s index — the
// COLUMN's index, not the row's — so every row's checkbox ended up toggling the same fixed index.
describe('SpAdminTableComponent bulk-select row independence', () => {
  let fixture: ComponentFixture<SpAdminTableComponent>;
  let component: SpAdminTableComponent;

  const columns: SpAdminTableColumn[] = [
    { key: 'title', label: 'Title', titleColumn: true },
    { key: 'status', label: 'Status' },
  ];
  const rows = [
    { title: 'Row A', status: 'Pending' },
    { title: 'Row B', status: 'Pending' },
    { title: 'Row C', status: 'Pending' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminTableComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminTableComponent);
    component = fixture.componentInstance;
    component.columns = columns;
    component.rows = rows;
    component.bulkEditable = true;
    component.bulkEditMode = true;
    fixture.detectChanges();
  });

  function bulkCheckboxes(): HTMLInputElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('tbody input.sp-adm-bulk-checkbox'));
  }

  it('checking one row does not check every row', () => {
    const checkboxes = bulkCheckboxes();
    expect(checkboxes.length).toBe(3);

    checkboxes[0].checked = true;
    checkboxes[0].dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const afterFirstToggle = bulkCheckboxes();
    expect(afterFirstToggle[0].checked).toBeTrue();
    expect(afterFirstToggle[1].checked).toBeFalse();
    expect(afterFirstToggle[2].checked).toBeFalse();
  });

  it('emits only the toggled row index, not every row', () => {
    const emitted: number[][] = [];
    component.selectionChange.subscribe(indices => emitted.push(indices));

    bulkCheckboxes()[1].checked = true;
    bulkCheckboxes()[1].dispatchEvent(new Event('change'));

    expect(emitted[emitted.length - 1]).toEqual([1]);
  });

  it('unchecking one previously-checked row leaves the others checked', () => {
    bulkCheckboxes()[0].checked = true;
    bulkCheckboxes()[0].dispatchEvent(new Event('change'));
    fixture.detectChanges();
    bulkCheckboxes()[2].checked = true;
    bulkCheckboxes()[2].dispatchEvent(new Event('change'));
    fixture.detectChanges();

    bulkCheckboxes()[0].checked = false;
    bulkCheckboxes()[0].dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const final = bulkCheckboxes();
    expect(final[0].checked).toBeFalse();
    expect(final[1].checked).toBeFalse();
    expect(final[2].checked).toBeTrue();
  });
});

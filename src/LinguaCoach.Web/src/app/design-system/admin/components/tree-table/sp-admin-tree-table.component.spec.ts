import { Component, ViewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { TreeNode } from 'primeng/api';
import { SpAdminTreeTableComponent } from './sp-admin-tree-table.component';
import { SpAdminTableColumn, SpAdminTableFilter } from '../table/sp-admin-table.component';

interface Row {
  id: string;
  title: string;
  status: string;
}

const COLUMNS: SpAdminTableColumn[] = [
  { key: 'title', label: 'Title', titleColumn: true },
  { key: 'status', label: 'Status' },
];

function node(id: string, title: string, opts: Partial<TreeNode<Row>> = {}): TreeNode<Row> {
  return { key: id, data: { id, title, status: 'Approved' }, leaf: true, ...opts };
}

@Component({
  standalone: true,
  imports: [SpAdminTreeTableComponent],
  template: `
    <sp-admin-tree-table
      [columns]="columns" [rows]="rows" [loading]="loading" [error]="error"
      [selectable]="selectable" [selection]="selection" (selectionChange)="selection = $event"
      [searchable]="true" [filters]="filters" (searchChange)="lastSearch = $event" (filterChange)="lastFilter = $event"
      (nodeExpand)="expandedNodes.push($event)"
    >
      <ng-template #cell let-row let-col="col">{{ col.key }}:{{ row[col.key] }}</ng-template>
    </sp-admin-tree-table>
  `,
})
class HostComponent {
  @ViewChild(SpAdminTreeTableComponent) treeTable!: SpAdminTreeTableComponent<Row>;
  columns = COLUMNS;
  rows: TreeNode<Row>[] = [];
  loading = false;
  error = '';
  selectable = false;
  selection: TreeNode<Row>[] = [];
  filters: SpAdminTableFilter[] = [];
  lastSearch = '';
  lastFilter: { key: string; value: string } | null = null;
  expandedNodes: TreeNode<Row>[] = [];
}

describe('SpAdminTreeTableComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  async function setup(rows: TreeNode<Row>[] = [node('r1', 'Row One')]) {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.rows = rows;
    fixture.detectChanges();
  }

  it('renders a header cell per column', async () => {
    await setup();
    const headers = Array.from(fixture.nativeElement.querySelectorAll('th')) as HTMLElement[];
    const headerText = headers.map(h => h.textContent?.trim());
    expect(headerText).toContain('Title');
    expect(headerText).toContain('Status');
  });

  it('renders one row per top-level node via the projected #cell template', async () => {
    await setup([node('r1', 'Row One'), node('r2', 'Row Two')]);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('title:Row One');
    expect(text).toContain('title:Row Two');
  });

  it('shows the loading state instead of the table when loading', async () => {
    await setup();
    host.loading = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  it('shows the error state instead of the table when error is set', async () => {
    await setup();
    host.error = 'Could not load nodes.';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Could not load nodes.');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  it('shows the empty state when rows is empty', async () => {
    await setup([]);
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  it('emits searchChange when the search input changes', async () => {
    await setup();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-toolbar-search input');
    input.value = 'present simple';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(host.lastSearch).toBe('present simple');
  });

  it('emits filterChange with the filter key when a dropdown filter changes', async () => {
    host = new HostComponent();
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.rows = [node('r1', 'Row One')];
    host.filters = [{ key: 'status', label: 'Status', options: [{ value: 'Approved', label: 'Approved' }], value: '' }];
    fixture.detectChanges();

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = 'Approved';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(host.lastFilter).toEqual({ key: 'status', value: 'Approved' });
  });

  it('applies sp-adm-fluid-layout and marks the title column fluid when layout is first-column-fluid', async () => {
    await setup();
    host.treeTable.layout = 'first-column-fluid';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sp-adm-fluid-layout')).not.toBeNull();
    const titleTh = Array.from(fixture.nativeElement.querySelectorAll('th')).find(
      (el) => (el as HTMLElement).textContent?.trim() === 'Title') as HTMLElement;
    expect(titleTh.classList.contains('sp-admin-fluid-col')).toBeTrue();
  });

  it('does not mark the title column fluid when layout is the default (auto)', async () => {
    await setup();
    const titleTh = Array.from(fixture.nativeElement.querySelectorAll('th')).find(
      (el) => (el as HTMLElement).textContent?.trim() === 'Title') as HTMLElement;
    expect(titleTh.classList.contains('sp-admin-fluid-col')).toBeFalse();
  });

  it('renders an expand toggle only for non-leaf rows', async () => {
    await setup([node('r1', 'Leaf', { leaf: true }), node('r2', 'Container', { leaf: false })]);
    const togglers = fixture.nativeElement.querySelectorAll('.p-treetable-toggler');
    expect(togglers.length).toBe(1);
  });

  it('emits nodeExpand when a non-leaf row\'s toggler is clicked and it has no children yet', async () => {
    await setup([node('r1', 'Container', { leaf: false })]);
    const toggler: HTMLElement = fixture.nativeElement.querySelector('.p-treetable-toggler');
    toggler.click();
    fixture.detectChanges();

    expect(host.expandedNodes.length).toBe(1);
    expect(host.expandedNodes[0].data!.id).toBe('r1');
  });

  it('renders selection checkboxes when selectable is true', async () => {
    host = new HostComponent();
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.selectable = true;
    host.rows = [node('r1', 'Row One')];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.p-checkbox').length).toBeGreaterThan(0);
  });
});

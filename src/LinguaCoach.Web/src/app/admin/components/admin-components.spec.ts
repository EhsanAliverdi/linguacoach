import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SpAdminSidebarNavItemComponent } from './sidebar-nav-item/sp-admin-sidebar-nav-item.component';
import { SpAdminSidebarSectionComponent } from './sidebar-section/sp-admin-sidebar-section.component';
import { SpAdminUserMenuComponent } from './user-menu/sp-admin-user-menu.component';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import {
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminSelectComponent,
  SpAdminTextareaComponent,
} from '../index';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminDrawerComponent,
  SpAdminDropdownComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminHeaderComponent,
  SpAdminLayoutComponent,
  SpAdminLoadingStateComponent,
  SpAdminModalComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSidebarComponent,
  SpAdminStatCardComponent,
  SpAdminTableActionsComponent,
  SpAdminTableComponent,
  SpAdminThemeToggleComponent,
} from '../index';

@Component({
  standalone: true,
  imports: [SpAdminButtonComponent],
  template: `<sp-admin-button variant="primary" [loading]="loading" [disabled]="disabled">Save</sp-admin-button>`,
})
class ButtonHostComponent {
  loading = false;
  disabled = false;
}

@Component({
  standalone: true,
  imports: [SpAdminCardComponent],
  template: `<sp-admin-card title="Card title"><p>Projected body</p></sp-admin-card>`,
})
class CardHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminBadgeComponent],
  template: `<sp-admin-badge tone="success">Active</sp-admin-badge>`,
})
class BadgeHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminPageHeaderComponent],
  template: `<sp-admin-page-header title="Students" subtitle="Manage accounts"><button>New</button></sp-admin-page-header>`,
})
class PageHeaderHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminTableComponent],
  template: `<sp-admin-table [columns]="columns" [rows]="rows" emptyMessage="No students" />`,
})
class TableHostComponent {
  columns = [{ key: 'email', label: 'Email' }];
  rows: Record<string, unknown>[] = [{ email: 'admin@example.com' }];
}

@Component({
  standalone: true,
  imports: [SpAdminEmptyStateComponent],
  template: `<sp-admin-empty-state message="Nothing here" ctaLabel="Create" ctaRoute="/admin/create-student" />`,
})
class EmptyStateHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminLayoutComponent],
  template: `
    <sp-admin-layout [collapsed]="true">
      <div slot="sidebar">Sidebar</div>
      <div slot="header">Header</div>
      <div>Content</div>
    </sp-admin-layout>
  `,
})
class LayoutHostComponent {}

describe('admin wrapper components', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('renders button variants and disabled/loading state', () => {
    const fixture = TestBed.createComponent(ButtonHostComponent);
    fixture.componentInstance.loading = true;
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.textContent).toContain('Save');
    expect(button.disabled).toBeTrue();
    expect(button.getAttribute('aria-busy')).toBe('true');
  });

  it('projects card content', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Card title');
    expect(fixture.nativeElement.textContent).toContain('Projected body');
  });

  it('renders badge tone', () => {
    const fixture = TestBed.createComponent(BadgeHostComponent);
    fixture.detectChanges();

    const badge: HTMLElement = fixture.nativeElement.querySelector('span');
    expect(badge.textContent).toContain('Active');
  });

  it('renders page header title subtitle and actions', () => {
    const fixture = TestBed.createComponent(PageHeaderHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Students');
    expect(fixture.nativeElement.textContent).toContain('Manage accounts');
    expect(fixture.nativeElement.textContent).toContain('New');
  });

  it('renders table columns and rows', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Email');
    expect(fixture.nativeElement.textContent).toContain('admin@example.com');
  });

  it('renders table empty state', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.componentInstance.rows = [];
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No students');
  });

  it('renders loading state', () => {
    const fixture = TestBed.createComponent(SpAdminLoadingStateComponent);
    fixture.componentInstance.message = 'Loading records';
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading records');
  });

  it('renders error state', () => {
    const fixture = TestBed.createComponent(SpAdminErrorStateComponent);
    fixture.componentInstance.title = 'Failed';
    fixture.componentInstance.message = 'Could not load';
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Failed');
    expect(fixture.nativeElement.textContent).toContain('Could not load');
  });

  it('renders empty state', () => {
    const fixture = TestBed.createComponent(EmptyStateHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Nothing here');
    expect(fixture.nativeElement.textContent).toContain('Create');
  });

  it('renders layout sidebar header and content', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sidebar');
    expect(fixture.nativeElement.textContent).toContain('Header');
    expect(fixture.nativeElement.textContent).toContain('Content');
    expect(fixture.nativeElement.querySelector('main')).not.toBeNull();
  });

  it('renders drawer when open and emits close', () => {
    const fixture = TestBed.createComponent(SpAdminDrawerComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Details';
    spyOn(fixture.componentInstance.closed, 'emit');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button').click();
    expect(fixture.nativeElement.textContent).toContain('Details');
    expect(fixture.componentInstance.closed.emit).toHaveBeenCalled();
  });

  it('layout renders a semantic main region when collapsed=true', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('main');
    expect(main).not.toBeNull();
    expect(main.textContent).toContain('Content');
  });

  it('layout renders sidebar and header slots in correct containers', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sidebar');
    expect(fixture.nativeElement.textContent).toContain('Header');
    expect(fixture.nativeElement.querySelector('main')?.textContent).toContain('Content');
  });

  it('card renders header divider when title is set', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('section')?.textContent).toContain('Card title');
  });

  it('card renders as a section element', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('section')).not.toBeNull();
  });

  it('layout renders projected shell content', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sidebar');
    expect(fixture.nativeElement.textContent).toContain('Header');
    expect(fixture.nativeElement.querySelector('main')).not.toBeNull();
  });

  it('layout keeps projected content inside main when collapsed', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('main');
    expect(main.textContent).toContain('Content');
  });

  it('sidebar renders an aside element', () => {
    const fixture = TestBed.createComponent(SpAdminSidebarComponent);
    fixture.componentInstance.collapsed = false;
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
    expect(aside).not.toBeNull();
  });

  it('sidebar remains present when collapsed=true', () => {
    const fixture = TestBed.createComponent(SpAdminSidebarComponent);
    fixture.componentInstance.collapsed = true;
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
    expect(aside).not.toBeNull();
  });

  it('header renders a semantic header element', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('header');
    expect(header).not.toBeNull();
  });

  it('button renders projected label', () => {
    const fixture = TestBed.createComponent(ButtonHostComponent);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button');
    expect(button.textContent).toContain('Save');
  });

  it('badge renders projected label', () => {
    const fixture = TestBed.createComponent(BadgeHostComponent);
    fixture.detectChanges();

    const badge = fixture.nativeElement.querySelector('span');
    expect(badge.textContent).toContain('Active');
  });

  it('card renders title and projected body', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Card title');
    expect(fixture.nativeElement.textContent).toContain('Projected body');
  });

  it('stat-card renders label and value', () => {
    const fixture = TestBed.createComponent(SpAdminStatCardComponent);
    fixture.componentInstance.label = 'Students';
    fixture.componentInstance.value = '42';
    fixture.componentInstance.tone = 'indigo';
    fixture.detectChanges();

    const article = fixture.nativeElement.querySelector('article');
    expect(article).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Students');
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  it('table renders a table element', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('table')).not.toBeNull();
  });

  it('table header renders column label', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    const th = fixture.nativeElement.querySelector('th');
    expect(th.textContent).toContain('Email');
  });

  it('modal renders a dialog with title when open', () => {
    const fixture = TestBed.createComponent(SpAdminModalComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Confirm';
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('aria-label')).toBe('Confirm');
  });

  it('modal close button emits closed event', () => {
    const fixture = TestBed.createComponent(SpAdminModalComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Test';
    spyOn(fixture.componentInstance.closed, 'emit');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button[aria-label="Close dialog"]').click();
    expect(fixture.componentInstance.closed.emit).toHaveBeenCalled();
  });

  it('drawer renders a dialog when open', () => {
    const fixture = TestBed.createComponent(SpAdminDrawerComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Detail Panel';
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('aria-label')).toBe('Detail Panel');
  });

  it('layout shell keeps projected content intact', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sidebar');
    expect(fixture.nativeElement.textContent).toContain('Header');
    expect(fixture.nativeElement.textContent).toContain('Content');
  });
});

// ── Phase 10X-F: dropdown, sortable table, table-actions, theme toggle, filter-bar, header ──

@Component({
  standalone: true,
  imports: [SpAdminDropdownComponent],
  template: `
    <sp-admin-dropdown [isOpen]="open" (isOpenChange)="open = $event">
      <button trigger>Open</button>
      <div menu>Menu content</div>
    </sp-admin-dropdown>
  `,
})
class DropdownHostComponent {
  open = false;
}

@Component({
  standalone: true,
  imports: [SpAdminTableComponent],
  template: `
    <sp-admin-table
      [columns]="columns"
      [rows]="rows"
      [sortColumn]="sortColumn"
      [sortDirection]="sortDirection"
      (sortChange)="onSort($event)"
    />
  `,
})
class SortableTableHostComponent {
  columns = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'email', label: 'Email', sortable: false },
  ];
  rows: Record<string, unknown>[] = [{ name: 'Alice', email: 'alice@example.com' }];
  sortColumn = '';
  sortDirection: 'asc' | 'desc' = 'asc';
  lastSort: { column: string; direction: string } | null = null;
  onSort(e: { column: string; direction: 'asc' | 'desc' }) { this.lastSort = e; }
}

@Component({
  standalone: true,
  imports: [SpAdminTableActionsComponent],
  template: `
    <sp-admin-table-actions [actions]="actions" (actionClick)="last = $event.label" />
  `,
})
class TableActionsHostComponent {
  actions = [
    { label: 'View' },
    { label: 'Edit' },
    { label: 'Delete', danger: true },
  ];
  last = '';
}

@Component({
  standalone: true,
  imports: [SpAdminTableActionsComponent],
  template: `
    <sp-admin-table-actions>
      <button role="menuitem" type="button" class="sp-adm-action-item" (click)="last = 'view'">View</button>
      <button role="menuitem" type="button" class="sp-adm-action-item sp-adm-action-danger" (click)="last = 'delete'">Delete</button>
    </sp-admin-table-actions>
  `,
})
class TableActionsProjectedHostComponent {
  last = '';
}

@Component({
  standalone: true,
  imports: [SpAdminFilterBarComponent],
  template: `
    <sp-admin-filter-bar>
      <input search placeholder="Search" />
      <select filters><option>All</option></select>
      <button actions>Export</button>
    </sp-admin-filter-bar>
  `,
})
class FilterBarHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminPaginationComponent],
  template: `<sp-admin-pagination [page]="2" [totalPages]="5" (pageChange)="last = $event" />`,
})
class PaginationHostComponent {
  last = 0;
}

describe('admin wrapper components — Phase 10X-F', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  // sp-admin-dropdown
  it('dropdown is closed by default', () => {
    const fixture = TestBed.createComponent(DropdownHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[menu]')).toBeNull();
  });

  it('dropdown opens when trigger is clicked', () => {
    const fixture = TestBed.createComponent(DropdownHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Menu content');
  });

  it('dropdown projects trigger content', () => {
    const fixture = TestBed.createComponent(DropdownHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Open');
  });

  it('dropdown closes on Escape key', () => {
    const fixture = TestBed.createComponent(DropdownHostComponent);
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Menu content');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[menu]')).toBeNull();
  });

  // sp-admin-table sortable columns
  it('table renders sortable column header as interactive', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th[role="button"]');
    expect(th).not.toBeNull();
    expect(th.getAttribute('aria-sort')).toBe('none');
  });

  it('table non-sortable column is not interactive', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const ths: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('th');
    expect(ths[1].getAttribute('role')).toBeNull();
    expect(ths[1].getAttribute('aria-sort')).toBeNull();
  });

  it('table emits sortChange when sortable header is clicked', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th[role="button"]');
    th.click();
    expect(fixture.componentInstance.lastSort).toEqual({ column: 'name', direction: 'asc' });
  });

  it('table toggles sort direction on second click of same column', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'asc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th[role="button"]');
    th.click();
    expect(fixture.componentInstance.lastSort?.direction).toBe('desc');
  });

  it('table shows ascending arrow icon when sort active asc', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'asc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th[role="button"]');
    expect(th.getAttribute('aria-sort')).toBe('ascending');
  });

  it('table shows descending arrow icon when sort active desc', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'desc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th[role="button"]');
    expect(th.getAttribute('aria-sort')).toBe('descending');
  });

  // sp-admin-table-actions
  it('table-actions trigger button is visible', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Row actions"]');
    expect(btn).not.toBeNull();
  });

  it('table-actions menu is hidden by default', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
  });

  it('table-actions menu opens on trigger click', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Row actions"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('View');
    expect(fixture.nativeElement.textContent).toContain('Delete');
  });

  it('table-actions emits actionClick when item clicked', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Row actions"]').click();
    fixture.detectChanges();
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    buttons[0].click();
    expect(fixture.componentInstance.last).toBe('View');
  });

  // sp-admin-table-actions — projected content (ng-content) path
  it('table-actions projected content: trigger renders', () => {
    const fixture = TestBed.createComponent(TableActionsProjectedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('button[aria-label="Row actions"]')).not.toBeNull();
  });

  it('table-actions projected content: menu opens on trigger click', () => {
    const fixture = TestBed.createComponent(TableActionsProjectedHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Row actions"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('View');
    expect(fixture.nativeElement.textContent).toContain('Delete');
  });

  it('table-actions projected content: clicking item calls handler', () => {
    const fixture = TestBed.createComponent(TableActionsProjectedHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Row actions"]').click();
    fixture.detectChanges();
    const items: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    items[0].click();
    expect(fixture.componentInstance.last).toBe('view');
  });

  it('table-actions projected content: menu closes on Escape', () => {
    const fixture = TestBed.createComponent(TableActionsProjectedHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Row actions"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
  });

  // sp-admin-theme-toggle
  it('theme toggle renders button', () => {
    const fixture = TestBed.createComponent(SpAdminThemeToggleComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label]');
    expect(btn).not.toBeNull();
  });

  it('theme toggle button click does not throw', () => {
    const fixture = TestBed.createComponent(SpAdminThemeToggleComponent);
    fixture.detectChanges();
    expect(() => {
      fixture.nativeElement.querySelector('button[aria-label]').click();
      fixture.detectChanges();
    }).not.toThrow();
  });

  // sp-admin-header now includes theme toggle
  it('header renders theme toggle button', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('button[aria-label]')).not.toBeNull();
  });

  // sp-admin-filter-bar named slots
  it('filter-bar renders search and actions slots', () => {
    const fixture = TestBed.createComponent(FilterBarHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[placeholder="Search"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Export');
  });

  // sp-admin-pagination
  it('pagination renders page info and prev/next buttons', () => {
    const fixture = TestBed.createComponent(PaginationHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Page 2 of 5');
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    expect(buttons.length).toBeGreaterThanOrEqual(2);
  });

  it('pagination prev button is enabled when page > 1', () => {
    const fixture = TestBed.createComponent(PaginationHostComponent);
    fixture.detectChanges();
    const prev: HTMLButtonElement = fixture.nativeElement.querySelectorAll('button')[0];
    expect(prev.disabled).toBeFalse();
  });
});

// ── Phase 10X-H: form wrapper ControlValueAccessor ──────────────────────────

@Component({
  standalone: true,
  imports: [FormsModule, SpAdminInputComponent],
  template: `<sp-admin-input [(ngModel)]="value" />`,
})
class InputNgModelHostComponent {
  value = 'initial';
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, SpAdminInputComponent],
  template: `<sp-admin-input [formControl]="control" />`,
})
class InputReactiveHostComponent {
  control = new FormControl('start');
}

@Component({
  standalone: true,
  imports: [FormsModule, SpAdminSelectComponent],
  template: `<sp-admin-select [(ngModel)]="value" [options]="options" />`,
})
class SelectNgModelHostComponent {
  value = 'b';
  options = [
    { value: 'a', label: 'Alpha' },
    { value: 'b', label: 'Beta' },
  ];
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, SpAdminSelectComponent],
  template: `<sp-admin-select [formControl]="control" [options]="options" />`,
})
class SelectReactiveHostComponent {
  control = new FormControl('a');
  options = [
    { value: 'a', label: 'Alpha' },
    { value: 'b', label: 'Beta' },
  ];
}

@Component({
  standalone: true,
  imports: [FormsModule, SpAdminTextareaComponent],
  template: `<sp-admin-textarea [(ngModel)]="value" />`,
})
class TextareaNgModelHostComponent {
  value = 'note';
}

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, SpAdminTextareaComponent],
  template: `<sp-admin-textarea [formControl]="control" />`,
})
class TextareaReactiveHostComponent {
  control = new FormControl('hello');
}

@Component({
  standalone: true,
  imports: [SpAdminFormFieldComponent],
  template: `
    <sp-admin-form-field label="Email" hint="We never share it" [required]="true">
      <input class="sp-input" />
    </sp-admin-form-field>
  `,
})
class FormFieldHostComponent {}

describe('admin form wrappers — Phase 10X-H CVA', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  // sp-admin-input
  it('input writes initial ngModel value into the native input', async () => {
    const fixture = TestBed.createComponent(InputNgModelHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.value).toBe('initial');
  });

  it('input propagates typed value back to ngModel', () => {
    const fixture = TestBed.createComponent(InputNgModelHostComponent);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    input.value = 'typed';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBe('typed');
  });

  it('input binds a reactive FormControl value', async () => {
    const fixture = TestBed.createComponent(InputReactiveHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.value).toBe('start');
    input.value = 'changed';
    input.dispatchEvent(new Event('input'));
    expect(fixture.componentInstance.control.value).toBe('changed');
  });

  it('input propagates disabled state from a reactive control', () => {
    const fixture = TestBed.createComponent(InputReactiveHostComponent);
    fixture.componentInstance.control.disable();
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.disabled).toBeTrue();
  });

  it('input marks the control touched on blur', () => {
    const fixture = TestBed.createComponent(InputReactiveHostComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.control.touched).toBeFalse();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    input.dispatchEvent(new Event('blur'));
    expect(fixture.componentInstance.control.touched).toBeTrue();
  });

  // sp-admin-select
  it('select writes initial ngModel value into the native select', async () => {
    const fixture = TestBed.createComponent(SelectNgModelHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('b');
  });

  it('select propagates change back to ngModel', () => {
    const fixture = TestBed.createComponent(SelectNgModelHostComponent);
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.value = 'a';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBe('a');
  });

  it('select binds a reactive FormControl value', async () => {
    const fixture = TestBed.createComponent(SelectReactiveHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('a');
    select.value = 'b';
    select.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.control.value).toBe('b');
  });

  it('select propagates disabled state from a reactive control', () => {
    const fixture = TestBed.createComponent(SelectReactiveHostComponent);
    fixture.componentInstance.control.disable();
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.disabled).toBeTrue();
  });

  it('select marks the control touched on blur', () => {
    const fixture = TestBed.createComponent(SelectReactiveHostComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.control.touched).toBeFalse();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.dispatchEvent(new Event('blur'));
    expect(fixture.componentInstance.control.touched).toBeTrue();
  });

  // sp-admin-textarea
  it('textarea writes initial ngModel value', async () => {
    const fixture = TestBed.createComponent(TextareaNgModelHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const ta: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    expect(ta.value).toBe('note');
  });

  it('textarea propagates typed value back to ngModel', () => {
    const fixture = TestBed.createComponent(TextareaNgModelHostComponent);
    fixture.detectChanges();
    const ta: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    ta.value = 'updated note';
    ta.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(fixture.componentInstance.value).toBe('updated note');
  });

  it('textarea binds a reactive FormControl and marks touched on blur', async () => {
    const fixture = TestBed.createComponent(TextareaReactiveHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const ta: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    expect(ta.value).toBe('hello');
    ta.dispatchEvent(new Event('blur'));
    expect(fixture.componentInstance.control.touched).toBeTrue();
  });

  // sp-admin-form-field
  it('form-field renders label, hint, required marker, and projected control', () => {
    const fixture = TestBed.createComponent(FormFieldHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Email');
    expect(text).toContain('*');
    expect(text).toContain('We never share it');
    expect(fixture.nativeElement.querySelector('input')).not.toBeNull();
  });

  it('form-field shows error instead of hint when error is set', () => {
    const fixture = TestBed.createComponent(SpAdminFormFieldComponent);
    fixture.componentInstance.label = 'Name';
    fixture.componentInstance.hint = 'hint text';
    fixture.componentInstance.error = 'Required';
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Required');
    expect(text).not.toContain('hint text');
  });
});

// ─── Phase 10X-J: Wrapper Variant API tests ───────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminButtonComponent],
  template: `
    <sp-admin-button variant="danger" appearance="solid" size="xs">Delete</sp-admin-button>
    <sp-admin-button variant="success" appearance="outline" size="lg">Save</sp-admin-button>
    <sp-admin-button variant="primary" appearance="soft" size="sm">Draft</sp-admin-button>
    <sp-admin-button variant="secondary" appearance="ghost" size="md">Cancel</sp-admin-button>
    <sp-admin-button variant="neutral" appearance="link" size="md">Link</sp-admin-button>
    <sp-admin-button [fullWidth]="true">Full</sp-admin-button>
    <sp-admin-button [iconOnly]="true" size="sm">X</sp-admin-button>
  `,
})
class ButtonVariantHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminBadgeComponent],
  template: `
    <sp-admin-badge tone="success" appearance="soft" size="sm">Active</sp-admin-badge>
    <sp-admin-badge tone="danger" appearance="solid" size="md">Error</sp-admin-badge>
    <sp-admin-badge tone="warning" appearance="outline" size="sm">Warn</sp-admin-badge>
    <sp-admin-badge tone="purple" [dot]="true">Purple</sp-admin-badge>
  `,
})
class BadgeVariantHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminCardComponent],
  template: `
    <sp-admin-card title="Default" variant="default" padding="md" radius="2xl">Body</sp-admin-card>
    <sp-admin-card title="Elevated" variant="elevated" padding="lg" [headerDivider]="true">Body</sp-admin-card>
    <sp-admin-card title="Flat" variant="flat" padding="none">Body</sp-admin-card>
    <sp-admin-card title="Hover" [hover]="true">Body</sp-admin-card>
  `,
})
class CardVariantHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminTableComponent],
  template: `
    <sp-admin-table variant="basic" density="comfortable" [columns]="cols" [rows]="rows" />
    <sp-admin-table variant="data" density="compact" [columns]="cols" [rows]="rows" />
  `,
})
class TableVariantHostComponent {
  cols = [{ key: 'name', label: 'Name', sortable: true }];
  rows: Record<string, unknown>[] = [{ name: 'Alice' }, { name: 'Bob' }];
}

@Component({
  standalone: true,
  imports: [SpAdminTableComponent],
  template: `<sp-admin-table [columns]="cols" [rows]="rows" (sortChange)="onSort($event)" />`,
})
class TableSortHostComponent {
  cols = [{ key: 'name', label: 'Name', sortable: true }];
  rows: Record<string, unknown>[] = [{ name: 'Alice' }];
  lastSort: { column: string; direction: string } | null = null;
  onSort(e: { column: string; direction: string }): void { this.lastSort = e; }
}

@Component({
  standalone: true,
  imports: [SpAdminFilterBarComponent],
  template: `
    <sp-admin-filter-bar layout="inline" density="compact">
      <input search placeholder="Search" />
      <button actions>Export</button>
    </sp-admin-filter-bar>
    <sp-admin-filter-bar layout="stacked" density="comfortable">
      <input search placeholder="Search" />
    </sp-admin-filter-bar>
  `,
})
class FilterBarVariantHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminFormFieldComponent],
  template: `
    <sp-admin-form-field label="Name" layout="vertical" size="sm"><input /></sp-admin-form-field>
    <sp-admin-form-field label="Email" layout="horizontal" size="md"><input /></sp-admin-form-field>
    <sp-admin-form-field label="Quick" layout="inline" size="lg"><input /></sp-admin-form-field>
  `,
})
class FormFieldLayoutHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminModalComponent],
  template: `
    <sp-admin-modal [open]="true" title="Small" size="sm" variant="default" />
    <sp-admin-modal [open]="true" title="Large" size="lg" variant="form" />
    <sp-admin-modal [open]="true" title="Danger" size="md" variant="danger" />
  `,
})
class ModalVariantHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminDropdownComponent],
  template: `
    <sp-admin-dropdown [(isOpen)]="open">
      <button trigger>Trigger</button>
      <div menu><a>Item</a></div>
    </sp-admin-dropdown>
  `,
})
class DropdownBehaviorHostComponent {
  open = false;
}

@Component({
  standalone: true,
  imports: [SpAdminInputComponent, SpAdminTableComponent, SpAdminBadgeComponent],
  template: `
    <sp-admin-input size="sm" state="error" />
    <sp-admin-table variant="data" density="compact" [columns]="cols" [rows]="rows" />
    <sp-admin-badge tone="success" appearance="soft">Active</sp-admin-badge>
  `,
})
class PageUsageProofHostComponent {
  cols = [{ key: 'name', label: 'Name' }];
  rows: Record<string, unknown>[] = [{ name: 'Alice' }];
}

describe('Phase 10X-J — admin wrapper variant API', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('renders button variants without dropping projected labels', () => {
    const fixture = TestBed.createComponent(ButtonVariantHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Delete');
    expect(text).toContain('Save');
    expect(text).toContain('Draft');
    expect(text).toContain('Cancel');
    expect(text).toContain('Link');
    expect(text).toContain('Full');
    expect(text).toContain('X');
  });

  it('renders badge variants without dropping projected labels', () => {
    const fixture = TestBed.createComponent(BadgeVariantHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Active');
    expect(text).toContain('Error');
    expect(text).toContain('Warn');
    expect(text).toContain('Purple');
  });

  it('renders card variants with titles and projected bodies', () => {
    const fixture = TestBed.createComponent(CardVariantHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Default');
    expect(text).toContain('Elevated');
    expect(text).toContain('Flat');
    expect(text).toContain('Hover');
    expect(text).toContain('Body');
  });

  it('renders table variants with headers and rows', () => {
    const fixture = TestBed.createComponent(TableVariantHostComponent);
    fixture.detectChanges();
    const tables = fixture.nativeElement.querySelectorAll('table');
    expect(tables.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Name');
    expect(fixture.nativeElement.textContent).toContain('Alice');
    expect(fixture.nativeElement.textContent).toContain('Bob');
  });

  it('emits sortChange event when sortable header is clicked', () => {
    const fixture = TestBed.createComponent(TableSortHostComponent);
    fixture.detectChanges();
    const th = fixture.nativeElement.querySelector('th[role="button"]');
    expect(th).not.toBeNull();
    th.click();
    expect(fixture.componentInstance.lastSort).toEqual({ column: 'name', direction: 'asc' });
  });

  it('renders filter bar projected search and actions', () => {
    const fixture = TestBed.createComponent(FilterBarVariantHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('input[placeholder="Search"]').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Export');
  });

  it('renders form-field labels and controls', () => {
    const fixture = TestBed.createComponent(FormFieldLayoutHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Name');
    expect(fixture.nativeElement.textContent).toContain('Email');
    expect(fixture.nativeElement.textContent).toContain('Quick');
    expect(fixture.nativeElement.querySelectorAll('input').length).toBe(3);
  });

  it('sp-admin-input preserves CVA binding with size/state variants applied', async () => {
    const fixture = TestBed.createComponent(InputNgModelHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminInputComponent;
    comp.size = 'sm';
    comp.state = 'error';
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.value).toBe('initial');
  });

  it('sp-admin-select preserves CVA binding with size/state variants applied', () => {
    const fixture = TestBed.createComponent(SelectReactiveHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminSelectComponent;
    comp.size = 'lg';
    comp.state = 'success';
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.value).toBe('a');
  });

  it('sp-admin-textarea preserves CVA binding with size/state variants applied', () => {
    const fixture = TestBed.createComponent(TextareaReactiveHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminTextareaComponent;
    comp.size = 'lg';
    comp.state = 'error';
    fixture.detectChanges();
    const ta: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    expect(ta.value).toBe('hello');
  });

  it('renders modal variants as dialogs', () => {
    const fixture = TestBed.createComponent(ModalVariantHostComponent);
    fixture.detectChanges();
    const dialogs = fixture.nativeElement.querySelectorAll('[role="dialog"]');
    expect(dialogs.length).toBe(3);
    expect(dialogs[0].getAttribute('aria-label')).toBe('Small');
    expect(dialogs[1].getAttribute('aria-label')).toBe('Large');
    expect(dialogs[2].getAttribute('aria-label')).toBe('Danger');
  });

  it('sp-admin-dropdown preserves open/close behavior', () => {
    const fixture = TestBed.createComponent(DropdownBehaviorHostComponent);
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector('button');
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
    trigger.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    trigger.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
  });

  it('page-level use of table/input/badge variants renders without error', () => {
    const fixture = TestBed.createComponent(PageUsageProofHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('table')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Active');
  });
});

// ─── Phase 10X-K-1: sidebar-nav-item, sidebar-section, user-menu ─────────────

@Component({
  standalone: true,
  imports: [SpAdminSidebarNavItemComponent],
  template: `
    <sp-admin-sidebar-nav-item label="Dashboard" route="/admin" [exact]="true" [collapsed]="false">
      <svg viewBox="0 0 24 24" aria-hidden="true"></svg>
    </sp-admin-sidebar-nav-item>
  `,
})
class NavItemExpandedHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminSidebarNavItemComponent],
  template: `
    <sp-admin-sidebar-nav-item label="Students" route="/admin/students" [collapsed]="true">
      <svg viewBox="0 0 24 24" aria-hidden="true"></svg>
    </sp-admin-sidebar-nav-item>
  `,
})
class NavItemCollapsedHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminSidebarSectionComponent],
  template: `<sp-admin-sidebar-section label="Menu" [collapsed]="false" />`,
})
class SidebarSectionExpandedHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminSidebarSectionComponent],
  template: `<sp-admin-sidebar-section label="Menu" [collapsed]="true" />`,
})
class SidebarSectionCollapsedHostComponent {}

@Component({
  standalone: true,
  imports: [SpAdminUserMenuComponent],
  template: `<sp-admin-user-menu email="admin@example.com" initial="A" (signOut)="signedOut = true" />`,
})
class UserMenuHostComponent {
  signedOut = false;
}

describe('Phase 10X-K-1 — shell components', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  // sp-admin-sidebar-nav-item expanded
  it('nav-item renders label when not collapsed', () => {
    const fixture = TestBed.createComponent(NavItemExpandedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Dashboard');
  });

  it('nav-item renders an anchor element', () => {
    const fixture = TestBed.createComponent(NavItemExpandedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a')).not.toBeNull();
  });

  it('nav-item projects icon content', () => {
    const fixture = TestBed.createComponent(NavItemExpandedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).not.toBeNull();
  });

  // sp-admin-sidebar-nav-item collapsed
  it('nav-item hides label text when collapsed', () => {
    const fixture = TestBed.createComponent(NavItemCollapsedHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.querySelector('a')?.textContent?.trim() ?? '';
    expect(text).not.toContain('Students');
  });

  it('nav-item exposes title attribute for tooltip when collapsed', () => {
    const fixture = TestBed.createComponent(NavItemCollapsedHostComponent);
    fixture.detectChanges();
    const a: HTMLAnchorElement = fixture.nativeElement.querySelector('a');
    expect(a.title).toBe('Students');
  });

  // sp-admin-sidebar-section expanded
  it('sidebar-section renders label when not collapsed', () => {
    const fixture = TestBed.createComponent(SidebarSectionExpandedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Menu');
  });

  // sp-admin-sidebar-section collapsed
  it('sidebar-section hides label when collapsed', () => {
    const fixture = TestBed.createComponent(SidebarSectionCollapsedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  // sp-admin-user-menu
  it('user-menu renders profile trigger button', () => {
    const fixture = TestBed.createComponent(UserMenuHostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Profile menu"]');
    expect(btn).not.toBeNull();
  });

  it('user-menu shows avatar initial', () => {
    const fixture = TestBed.createComponent(UserMenuHostComponent);
    fixture.detectChanges();
    const btn: HTMLElement = fixture.nativeElement.querySelector('button[aria-label="Profile menu"]');
    expect(btn.textContent?.trim()).toBe('A');
  });

  it('user-menu opens dropdown on avatar click', () => {
    const fixture = TestBed.createComponent(UserMenuHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
    fixture.nativeElement.querySelector('button[aria-label="Profile menu"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
  });

  it('user-menu shows email in open dropdown', () => {
    const fixture = TestBed.createComponent(UserMenuHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Profile menu"]').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('admin@example.com');
  });

  it('user-menu emits signOut when sign-out button clicked', () => {
    const fixture = TestBed.createComponent(UserMenuHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Profile menu"]').click();
    fixture.detectChanges();
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('[role="menuitem"]'));
    const signOutBtn = buttons.find(b => b.textContent?.includes('Sign out'));
    expect(signOutBtn).not.toBeNull();
    signOutBtn!.click();
    expect(fixture.componentInstance.signedOut).toBeTrue();
  });
});

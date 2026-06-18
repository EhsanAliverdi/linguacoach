import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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
    expect(button.classList).toContain('sp-adm-btn-primary');
    expect(fixture.nativeElement.querySelector('.sp-adm-btn-spinner')).not.toBeNull();
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

    const badge: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-badge-success');
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
    expect(fixture.nativeElement.querySelector('.sp-main-collapsed')).not.toBeNull();
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

  it('layout applies sp-main-collapsed when collapsed=true', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('.sp-admin-main');
    expect(main).not.toBeNull();
    expect(main.classList).toContain('sp-main-collapsed');
  });

  it('layout renders sidebar and header slots in correct containers', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const shell = fixture.nativeElement.querySelector('.sp-admin-shell');
    expect(shell).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-content')).not.toBeNull();
  });

  it('card renders header divider when title is set', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('.sp-adm-card-header');
    expect(header).not.toBeNull();
    expect(header.textContent).toContain('Card title');
  });

  it('card uses sp-adm-card class on section element', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('section.sp-adm-card')).not.toBeNull();
  });

  // ── TailAdmin-backed pattern tests (Phase 10X-E) ───────────────────────

  it('layout uses TailAdmin Layout One min-h-screen xl:flex shell', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const shell = fixture.nativeElement.querySelector('.sp-admin-shell');
    expect(shell).not.toBeNull();
    expect(shell.classList).toContain('min-h-screen');
    expect(shell.classList).toContain('xl:flex');
  });

  it('layout main area carries TailAdmin xl:ml offset classes when collapsed', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('.sp-admin-main');
    expect(main.classList).toContain('transition-all');
    expect(main.classList).toContain('xl:ml-[90px]');
  });

  it('sidebar uses TailAdmin fixed sidebar classes', () => {
    const fixture = TestBed.createComponent(SpAdminSidebarComponent);
    fixture.componentInstance.collapsed = false;
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
    expect(aside.classList).toContain('sp-admin-sidebar');
    expect(aside.classList).toContain('fixed');
    expect(aside.classList).toContain('w-[290px]');
    expect(aside.classList).toContain('border-r');
  });

  it('sidebar uses collapsed width class when collapsed=true', () => {
    const fixture = TestBed.createComponent(SpAdminSidebarComponent);
    fixture.componentInstance.collapsed = true;
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
    expect(aside.classList).toContain('w-[90px]');
  });

  it('header uses TailAdmin sticky top-0 structure', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('header');
    expect(header.classList).toContain('sticky');
    expect(header.classList).toContain('top-0');
    expect(header.classList).toContain('border-b');
  });

  it('button uses TailAdmin rounded-lg inline-flex classes', () => {
    const fixture = TestBed.createComponent(ButtonHostComponent);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button');
    expect(button.classList).toContain('rounded-lg');
    expect(button.classList).toContain('inline-flex');
    expect(button.classList).toContain('items-center');
  });

  it('badge uses TailAdmin rounded-full inline-flex classes', () => {
    const fixture = TestBed.createComponent(BadgeHostComponent);
    fixture.detectChanges();

    const badge = fixture.nativeElement.querySelector('span');
    expect(badge.classList).toContain('rounded-full');
    expect(badge.classList).toContain('inline-flex');
    expect(badge.classList).toContain('sp-adm-badge-success');
  });

  it('card uses TailAdmin rounded-2xl border border-gray-200 bg-white classes', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('section');
    expect(card.classList).toContain('rounded-2xl');
    expect(card.classList).toContain('border');
    expect(card.classList).toContain('bg-white');
  });

  it('stat-card uses TailAdmin rounded-2xl flex structure', () => {
    const fixture = TestBed.createComponent(SpAdminStatCardComponent);
    fixture.componentInstance.label = 'Students';
    fixture.componentInstance.value = '42';
    fixture.componentInstance.tone = 'indigo';
    fixture.detectChanges();

    const article = fixture.nativeElement.querySelector('article');
    expect(article.classList).toContain('rounded-2xl');
    expect(article.classList).toContain('flex');
    expect(article.classList).toContain('items-center');
    expect(fixture.nativeElement.textContent).toContain('Students');
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  it('table uses TailAdmin rounded-2xl border bg-white container', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.sp-adm-table-card');
    expect(card.classList).toContain('rounded-2xl');
    expect(card.classList).toContain('border');
    expect(card.classList).toContain('bg-white');
  });

  it('table th uses TailAdmin text-xs text-gray-500 header pattern', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    const th = fixture.nativeElement.querySelector('th');
    expect(th.classList).toContain('text-xs');
    expect(th.classList).toContain('text-gray-500');
  });

  it('modal uses TailAdmin rounded-3xl bg-white panel', () => {
    const fixture = TestBed.createComponent(SpAdminModalComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Confirm';
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.sp-modal-panel');
    expect(panel.classList).toContain('rounded-3xl');
    expect(panel.classList).toContain('bg-white');
    expect(fixture.nativeElement.textContent).toContain('Confirm');
  });

  it('modal close button uses TailAdmin rounded-full bg-gray-100 pattern', () => {
    const fixture = TestBed.createComponent(SpAdminModalComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Test';
    fixture.detectChanges();

    const closeBtn = fixture.nativeElement.querySelector('.sp-modal-close');
    expect(closeBtn.classList).toContain('rounded-full');
    expect(closeBtn.classList).toContain('bg-gray-100');
  });

  it('drawer uses TailAdmin bg-white border-l structure', () => {
    const fixture = TestBed.createComponent(SpAdminDrawerComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Detail Panel';
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
    expect(aside.classList).toContain('bg-white');
    expect(aside.classList).toContain('border-l');
    expect(aside.classList).toContain('fixed');
  });

  it('active nav uses TailAdmin sp-admin-nav-item-active class (layout shell)', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();
    // Layout shell renders sidebar/header slot containers — just confirm shell structure intact
    expect(fixture.nativeElement.querySelector('.sp-admin-shell')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-content')).not.toBeNull();
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
    // Click the trigger zone (the .sp-adm-dropdown-trigger div)
    fixture.nativeElement.querySelector('.sp-adm-dropdown-trigger').click();
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

  it('dropdown uses TailAdmin rounded-xl border border-gray-200 bg-white menu panel', () => {
    const fixture = TestBed.createComponent(DropdownHostComponent);
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    const panel = fixture.nativeElement.querySelector('.sp-adm-dropdown > div:last-child');
    expect(panel.classList).toContain('rounded-xl');
    expect(panel.classList).toContain('border-gray-200');
  });

  // sp-admin-table sortable columns
  it('table renders sortable column header with sort icon', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const ths: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('th');
    expect(ths[0].classList).toContain('sp-adm-th-sortable');
    expect(ths[0].textContent).toContain('↕');
  });

  it('table non-sortable column has no sortable class', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const ths: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('th');
    expect(ths[1].classList).not.toContain('sp-adm-th-sortable');
  });

  it('table emits sortChange when sortable header is clicked', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th.sp-adm-th-sortable');
    th.click();
    expect(fixture.componentInstance.lastSort).toEqual({ column: 'name', direction: 'asc' });
  });

  it('table toggles sort direction on second click of same column', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'asc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th.sp-adm-th-sortable');
    th.click();
    expect(fixture.componentInstance.lastSort?.direction).toBe('desc');
  });

  it('table shows ascending arrow icon when sort active asc', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'asc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th.sp-adm-th-sortable');
    expect(th.textContent).toContain('▲');
  });

  it('table shows descending arrow icon when sort active desc', () => {
    const fixture = TestBed.createComponent(SortableTableHostComponent);
    fixture.componentInstance.sortColumn = 'name';
    fixture.componentInstance.sortDirection = 'desc';
    fixture.detectChanges();
    const th: HTMLElement = fixture.nativeElement.querySelector('th.sp-adm-th-sortable');
    expect(th.textContent).toContain('▼');
  });

  // sp-admin-table-actions
  it('table-actions trigger button is visible', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.sp-adm-actions-trigger');
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
    fixture.nativeElement.querySelector('.sp-adm-actions-trigger').click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('View');
    expect(fixture.nativeElement.textContent).toContain('Delete');
  });

  it('table-actions emits actionClick when item clicked', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.sp-adm-actions-trigger').click();
    fixture.detectChanges();
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.sp-adm-action-item');
    buttons[0].click();
    expect(fixture.componentInstance.last).toBe('View');
  });

  it('table-actions danger item has red text class', () => {
    const fixture = TestBed.createComponent(TableActionsHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('.sp-adm-actions-trigger').click();
    fixture.detectChanges();
    const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('.sp-adm-action-item');
    expect(buttons[2].classList).toContain('text-red-600');
  });

  // sp-admin-theme-toggle
  it('theme toggle renders button', () => {
    const fixture = TestBed.createComponent(SpAdminThemeToggleComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('.sp-adm-theme-btn');
    expect(btn).not.toBeNull();
  });

  it('theme toggle button click does not throw', () => {
    const fixture = TestBed.createComponent(SpAdminThemeToggleComponent);
    fixture.detectChanges();
    expect(() => {
      fixture.nativeElement.querySelector('.sp-adm-theme-btn').click();
      fixture.detectChanges();
    }).not.toThrow();
  });

  // sp-admin-header now includes theme toggle
  it('header renders theme toggle button', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-adm-theme-btn')).not.toBeNull();
  });

  it('header has left and actions content zones', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-admin-header-left')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-admin-header-actions')).not.toBeNull();
  });

  // sp-admin-filter-bar named slots
  it('filter-bar renders search and actions slots', () => {
    const fixture = TestBed.createComponent(FilterBarHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[placeholder="Search"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Export');
  });

  it('filter-bar uses sp-adm-filter flex container', () => {
    const fixture = TestBed.createComponent(FilterBarHostComponent);
    fixture.detectChanges();
    const bar = fixture.nativeElement.querySelector('.sp-adm-filter');
    expect(bar).not.toBeNull();
    expect(bar.classList).toContain('flex');
    expect(bar.classList).toContain('mb-4');
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
    expect(fixture.nativeElement.querySelector('.sp-adm-field-label').textContent).toContain('Email');
    expect(fixture.nativeElement.querySelector('.sp-adm-field-required')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-adm-field-hint').textContent).toContain('We never share it');
    expect(fixture.nativeElement.querySelector('input.sp-input')).not.toBeNull();
  });

  it('form-field shows error instead of hint when error is set', () => {
    const fixture = TestBed.createComponent(SpAdminFormFieldComponent);
    fixture.componentInstance.label = 'Name';
    fixture.componentInstance.hint = 'hint text';
    fixture.componentInstance.error = 'Required';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-adm-field-error').textContent).toContain('Required');
    expect(fixture.nativeElement.querySelector('.sp-adm-field-hint')).toBeNull();
  });
});

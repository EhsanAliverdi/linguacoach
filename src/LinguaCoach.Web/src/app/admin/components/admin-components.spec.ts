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
    expect(button.classList).toContain('sp-adm-btn-solid-primary');
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

    const badge: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-badge-soft-success');
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
    expect(fixture.nativeElement.querySelector('.flex-1')).not.toBeNull();
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

  it('layout applies xl:ml-[90px] when collapsed=true', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('.flex-1');
    expect(main).not.toBeNull();
    expect(main.classList).toContain('xl:ml-[90px]');
  });

  it('layout renders sidebar and header slots in correct containers', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const shell = fixture.nativeElement.querySelector('.min-h-screen');
    expect(shell).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.flex-1')).not.toBeNull();
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

    const shell = fixture.nativeElement.querySelector('.min-h-screen');
    expect(shell).not.toBeNull();
    expect(shell.classList).toContain('min-h-screen');
  });

  it('layout main area carries TailAdmin xl:ml offset classes when collapsed', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();

    const main = fixture.nativeElement.querySelector('.flex-1');
    expect(main.classList).toContain('transition-all');
    expect(main.classList).toContain('xl:ml-[90px]');
  });

  it('sidebar uses TailAdmin fixed sidebar classes', () => {
    const fixture = TestBed.createComponent(SpAdminSidebarComponent);
    fixture.componentInstance.collapsed = false;
    fixture.detectChanges();

    const aside = fixture.nativeElement.querySelector('aside');
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
    expect(header.classList).toContain('xl:border-b');
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
    expect(badge.classList).toContain('sp-adm-badge-soft-success');
  });

  it('card uses bg-white and variant/radius classes', () => {
    const fixture = TestBed.createComponent(CardHostComponent);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('section');
    expect(card.classList).toContain('sp-adm-card-default');
    expect(card.classList).toContain('sp-adm-card-radius-2xl');
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
    expect(article.classList).toContain('sp-adm-stat-md');
    expect(fixture.nativeElement.textContent).toContain('Students');
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  it('table uses TailAdmin rounded-2xl border bg-white container', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.sp-adm-table-card');
    expect(card).not.toBeNull();
  });

  it('table th uses TailAdmin text-xs text-gray-500 header pattern', () => {
    const fixture = TestBed.createComponent(TableHostComponent);
    fixture.detectChanges();

    const th = fixture.nativeElement.querySelector('th');
    expect(th.classList).toContain('sp-adm-th');
    expect(th.classList).toContain('sp-adm-th-comfortable');
  });

  it('modal uses TailAdmin rounded-3xl bg-white panel', () => {
    const fixture = TestBed.createComponent(SpAdminModalComponent);
    fixture.componentInstance.open = true;
    fixture.componentInstance.title = 'Confirm';
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.sp-modal-panel');
    expect(panel.classList).toContain('sp-modal-panel-default');
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
    expect(aside.classList).toContain('sp-adm-drawer-right');
    expect(aside.classList).toContain('fixed');
  });

  it('active nav uses TailAdmin class (layout shell structure intact)', () => {
    const fixture = TestBed.createComponent(LayoutHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.min-h-screen')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.flex-1')).not.toBeNull();
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

  it('header has grow inner flex container', () => {
    const fixture = TestBed.createComponent(SpAdminHeaderComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.grow')).not.toBeNull();
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
    expect(bar.classList).toContain('sp-adm-filter-comfortable');
    expect(bar.classList).toContain('sp-adm-filter-responsive');
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

  // 1. sp-admin-button size variants
  it('renders button size variants (xs, sm, md, lg)', () => {
    const fixture = TestBed.createComponent(ButtonVariantHostComponent);
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[0].classList).toContain('sp-adm-btn-xs');
    expect(buttons[1].classList).toContain('sp-adm-btn-lg');
    expect(buttons[2].classList).toContain('sp-adm-btn-sm');
    expect(buttons[3].classList).toContain('sp-adm-btn-md');
  });

  // 2. sp-admin-button appearance variants
  it('renders button appearance variants (solid, outline, soft, ghost, link)', () => {
    const fixture = TestBed.createComponent(ButtonVariantHostComponent);
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[0].classList).toContain('sp-adm-btn-solid-danger');
    expect(buttons[1].classList).toContain('sp-adm-btn-outline-success');
    expect(buttons[2].classList).toContain('sp-adm-btn-soft-primary');
    expect(buttons[3].classList).toContain('sp-adm-btn-ghost-secondary');
    expect(buttons[4].classList).toContain('sp-adm-btn-link-neutral');
  });

  // 3. sp-admin-button fullWidth / iconOnly modifiers
  it('renders button fullWidth and iconOnly class modifiers', () => {
    const fixture = TestBed.createComponent(ButtonVariantHostComponent);
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[5].classList).toContain('sp-adm-btn-block');
    expect(buttons[6].classList).toContain('sp-adm-btn-icon-only');
  });

  // 4. sp-admin-badge tones
  it('renders badge tones (success, danger, warning, purple)', () => {
    const fixture = TestBed.createComponent(BadgeVariantHostComponent);
    fixture.detectChanges();
    const badges = fixture.nativeElement.querySelectorAll('.sp-adm-badge');
    expect(badges[0].classList).toContain('sp-adm-badge-soft-success');
    expect(badges[1].classList).toContain('sp-adm-badge-solid-danger');
    expect(badges[2].classList).toContain('sp-adm-badge-outline-warning');
    expect(badges[3].classList).toContain('sp-adm-badge-soft-purple');
  });

  // 5. sp-admin-badge appearances + dot
  it('renders badge appearances and dot indicator', () => {
    const fixture = TestBed.createComponent(BadgeVariantHostComponent);
    fixture.detectChanges();
    const badges = fixture.nativeElement.querySelectorAll('.sp-adm-badge');
    expect(badges[0].classList).toContain('sp-adm-badge-sm');
    expect(badges[1].classList).toContain('sp-adm-badge-md');
    expect(fixture.nativeElement.querySelector('.sp-adm-badge-dot')).not.toBeNull();
  });

  // 6. sp-admin-card padding / variant / headerDivider
  it('renders card padding, variant, and headerDivider options', () => {
    const fixture = TestBed.createComponent(CardVariantHostComponent);
    fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('section.sp-adm-card');
    expect(cards[0].classList).toContain('sp-adm-card-default');
    expect(cards[0].classList).toContain('sp-adm-card-radius-2xl');
    const elevatedHeader = cards[1].querySelector('.sp-adm-card-header');
    expect(elevatedHeader.classList).toContain('sp-adm-card-header-divider');
    expect(cards[2].classList).toContain('sp-adm-card-flat');
    expect(cards[3].classList).toContain('sp-adm-card-hover');
  });

  // 7. sp-admin-table renders basic variant
  it('renders table basic variant with comfortable density', () => {
    const fixture = TestBed.createComponent(TableVariantHostComponent);
    fixture.detectChanges();
    const wrappers = fixture.nativeElement.querySelectorAll('.sp-adm-table-card');
    expect(wrappers.length).toBeGreaterThanOrEqual(1);
    const th = fixture.nativeElement.querySelector('.sp-adm-th-comfortable');
    expect(th).not.toBeNull();
  });

  // 8. sp-admin-table renders data variant
  it('renders table data variant with compact density', () => {
    const fixture = TestBed.createComponent(TableVariantHostComponent);
    fixture.detectChanges();
    const dataWrappers = fixture.nativeElement.querySelectorAll('.sp-adm-table-data');
    expect(dataWrappers.length).toBeGreaterThanOrEqual(1);
    const compactTd = fixture.nativeElement.querySelectorAll('.sp-adm-td-compact');
    expect(compactTd.length).toBeGreaterThan(0);
  });

  // 9. sp-admin-table density classes on th/td
  it('applies correct density classes to th and td', () => {
    const fixture = TestBed.createComponent(TableVariantHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-adm-th-comfortable')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-adm-td-compact')).not.toBeNull();
  });

  // 10. sp-admin-table emits sort events
  it('emits sortChange event when sortable header is clicked', () => {
    const fixture = TestBed.createComponent(TableSortHostComponent);
    fixture.detectChanges();
    const th = fixture.nativeElement.querySelector('.sp-adm-th-sortable');
    expect(th).not.toBeNull();
    th.click();
    expect(fixture.componentInstance.lastSort).toEqual({ column: 'name', direction: 'asc' });
  });

  // 11. sp-admin-filter-bar layout/density options
  it('renders filter bar with inline/compact and stacked/comfortable layouts', () => {
    const fixture = TestBed.createComponent(FilterBarVariantHostComponent);
    fixture.detectChanges();
    const bars = fixture.nativeElement.querySelectorAll('.sp-adm-filter');
    expect(bars[0].classList).toContain('sp-adm-filter-compact');
    expect(bars[0].classList).toContain('sp-adm-filter-inline');
    expect(bars[1].classList).toContain('sp-adm-filter-comfortable');
    expect(bars[1].classList).toContain('sp-adm-filter-stacked');
  });

  // 12. sp-admin-form-field layout options
  it('renders form-field vertical, horizontal, and inline layouts', () => {
    const fixture = TestBed.createComponent(FormFieldLayoutHostComponent);
    fixture.detectChanges();
    const fields = fixture.nativeElement.querySelectorAll('.sp-adm-field');
    expect(fields[0].classList).toContain('sp-adm-field-vertical');
    expect(fields[1].classList).toContain('sp-adm-field-horizontal');
    expect(fields[2].classList).toContain('sp-adm-field-inline');
  });

  // 13. sp-admin-input CVA preserved after variant changes
  it('sp-admin-input preserves CVA binding with size/state variants applied', () => {
    const fixture = TestBed.createComponent(InputNgModelHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminInputComponent;
    comp.size = 'sm';
    comp.state = 'error';
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.classList).toContain('sp-adm-input-sm');
    expect(input.classList).toContain('sp-adm-input-error');
  });

  // 14. sp-admin-select CVA preserved after variant changes
  it('sp-admin-select preserves CVA binding with size/state variants applied', () => {
    const fixture = TestBed.createComponent(SelectReactiveHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminSelectComponent;
    comp.size = 'lg';
    comp.state = 'success';
    fixture.detectChanges();
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    expect(select.classList).toContain('sp-adm-select-lg');
    expect(select.classList).toContain('sp-adm-select-success');
  });

  // 15. sp-admin-textarea CVA preserved after variant changes
  it('sp-admin-textarea preserves CVA binding with size/state variants applied', () => {
    const fixture = TestBed.createComponent(TextareaReactiveHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminTextareaComponent;
    comp.size = 'lg';
    comp.state = 'error';
    fixture.detectChanges();
    const ta: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea');
    expect(ta.classList).toContain('sp-adm-textarea-lg');
    expect(ta.classList).toContain('sp-adm-textarea-error');
  });

  // 16. sp-admin-modal size/variant panel classes
  it('renders modal size and variant panel classes', () => {
    const fixture = TestBed.createComponent(ModalVariantHostComponent);
    fixture.detectChanges();
    const panels = fixture.nativeElement.querySelectorAll('.sp-modal-panel');
    expect(panels.length).toBe(3);
    expect(panels[0].classList).toContain('sp-modal-panel-default');
    expect(panels[1].classList).toContain('sp-modal-panel-form');
    expect(panels[2].classList).toContain('sp-modal-panel-danger');
    expect(panels[2].querySelector('.sp-modal-danger-icon')).not.toBeNull();
  });

  // 17. sp-admin-dropdown open/close behavior preserved
  it('sp-admin-dropdown preserves open/close behavior', () => {
    const fixture = TestBed.createComponent(DropdownBehaviorHostComponent);
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector('.sp-adm-dropdown-trigger');
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
    trigger.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).not.toBeNull();
    trigger.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
  });

  // 18. Page-level usage renders without error
  it('page-level use of table/input/badge variants renders without error', () => {
    const fixture = TestBed.createComponent(PageUsageProofHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-adm-input-sm')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-adm-table-data')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sp-adm-badge-soft-success')).not.toBeNull();
  });
});

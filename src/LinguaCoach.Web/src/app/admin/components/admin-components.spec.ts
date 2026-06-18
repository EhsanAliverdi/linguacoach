import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminDrawerComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminLayoutComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageHeaderComponent,
  SpAdminTableComponent,
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
});

import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SpAdminSlideOverComponent } from './sp-admin-slide-over.component';

// ── host: basic open/closed ───────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over [open]="open" [title]="title" [subtitle]="subtitle" (closed)="onClose()">
      <p>Body content</p>
    </sp-admin-slide-over>
  `,
})
class BasicHostComponent {
  open = true;
  title = 'Student Preferences';
  subtitle = 'Read-only view';
  closed = false;
  onClose() { this.closed = true; }
}

// ── host: header-actions slot ─────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over [open]="true" title="Detail">
      <button slot="header-actions">Edit</button>
      <p>Body</p>
    </sp-admin-slide-over>
  `,
})
class HeaderActionsHostComponent {}

// ── host: footer slot ─────────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over [open]="true" title="Edit">
      <p>Body</p>
      <div slot="footer">
        <button>Save</button>
        <button>Cancel</button>
      </div>
    </sp-admin-slide-over>
  `,
})
class FooterHostComponent {}

// ── host: loading state ───────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over [open]="true" title="Loading" [loading]="true" loadingMessage="Fetching data">
      <p>Should not appear</p>
    </sp-admin-slide-over>
  `,
})
class LoadingHostComponent {}

// ── host: error state ─────────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `
    <sp-admin-slide-over [open]="true" title="Error" errorTitle="Load failed" error="Could not fetch preferences">
    </sp-admin-slide-over>
  `,
})
class ErrorHostComponent {}

// ── host: closed (open=false) ─────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `<sp-admin-slide-over [open]="false" title="Hidden"><p>Hidden body</p></sp-admin-slide-over>`,
})
class ClosedHostComponent {}

// ── host: size input ──────────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminSlideOverComponent],
  template: `<sp-admin-slide-over [open]="true" title="Sized" [size]="size"><p>body</p></sp-admin-slide-over>`,
})
class SizedHostComponent {
  size: 'sm' | 'md' | 'lg' | 'xl' = 'lg';
}

// ── tests ─────────────────────────────────────────────────────────────────────

describe('SpAdminSlideOverComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('renders title and subtitle when open', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Student Preferences');
    expect(text).toContain('Read-only view');
  });

  it('projects body content into the panel', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Body content');
  });

  it('renders a dialog element with aria-label matching the title', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    const dialog: HTMLElement = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog).not.toBeNull();
    expect(dialog.getAttribute('aria-label')).toBe('Student Preferences');
  });

  it('renders close button with aria-label', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-label="Close panel"]');
    expect(btn).not.toBeNull();
  });

  it('emits closed when close button is clicked', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Close panel"]').click();
    expect(fixture.componentInstance.closed).toBeTrue();
  });

  it('emits closed on Escape key', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.closed).toBeFalse();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.closed).toBeTrue();
  });

  it('does not render when open is false', () => {
    const fixture = TestBed.createComponent(ClosedHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('projects header-actions slot content', () => {
    const fixture = TestBed.createComponent(HeaderActionsHostComponent);
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[slot="header-actions"]');
    expect(btn).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });

  it('projects footer slot content', () => {
    const fixture = TestBed.createComponent(FooterHostComponent);
    fixture.detectChanges();
    const footer: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-so-footer');
    expect(footer).not.toBeNull();
    expect(footer.textContent).toContain('Save');
    expect(footer.textContent).toContain('Cancel');
  });

  it('shows loading state and hides body when loading is true', () => {
    const fixture = TestBed.createComponent(LoadingHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fetching data');
    expect(fixture.nativeElement.textContent).not.toContain('Should not appear');
  });

  it('shows error state when error is set', () => {
    const fixture = TestBed.createComponent(ErrorHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Load failed');
    expect(fixture.nativeElement.textContent).toContain('Could not fetch preferences');
  });

  it('hides body content when error is set (error replaces body)', () => {
    const fixture = TestBed.createComponent(ErrorHostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Body still shown after error');
  });

  it('applies size input and reflects in panel inline style', () => {
    const fixture = TestBed.createComponent(SizedHostComponent);
    fixture.detectChanges();
    const aside: HTMLElement = fixture.nativeElement.querySelector('aside');
    expect(aside).not.toBeNull();
    expect(aside.style.width).toBe('600px');
  });

  it('changes panel width when size input changes', () => {
    const fixture = TestBed.createComponent(SizedHostComponent);
    fixture.componentInstance.size = 'xl';
    fixture.detectChanges();
    const aside: HTMLElement = fixture.nativeElement.querySelector('aside');
    expect(aside.style.width).toBe('768px');
  });

  it('does not emit closed on backdrop click when closeOnBackdrop is false', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    const comp = fixture.debugElement.children[0].componentInstance as SpAdminSlideOverComponent;
    comp.closeOnBackdrop = false;
    fixture.detectChanges();
    const backdrop: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-so-backdrop');
    backdrop?.click();
    expect(fixture.componentInstance.closed).toBeFalse();
  });

  it('emits closed on backdrop click when closeOnBackdrop is true (default)', () => {
    const fixture = TestBed.createComponent(BasicHostComponent);
    fixture.detectChanges();
    const backdrop: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-so-backdrop');
    expect(backdrop).not.toBeNull();
    backdrop.click();
    expect(fixture.componentInstance.closed).toBeTrue();
  });
});

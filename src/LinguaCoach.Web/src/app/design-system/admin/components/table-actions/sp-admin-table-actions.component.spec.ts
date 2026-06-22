import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SpAdminTableActionsComponent, SpAdminTableAction } from './sp-admin-table-actions.component';

// ── host: declared actions ────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminTableActionsComponent],
  template: `
    <sp-admin-table-actions [actions]="actions" (actionClick)="onAction($event)">
    </sp-admin-table-actions>
  `,
})
class ActionsHostComponent {
  actions: SpAdminTableAction[] = [
    { label: 'View' },
    { label: 'Edit' },
    { label: 'Delete', danger: true },
  ];
  lastAction: SpAdminTableAction | null = null;
  onAction(a: SpAdminTableAction) { this.lastAction = a; }
}

// ── host: projected content ───────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminTableActionsComponent],
  template: `
    <sp-admin-table-actions>
      <button class="projected-btn" (click)="clicked = true">Custom action</button>
    </sp-admin-table-actions>
  `,
})
class ProjectedHostComponent {
  clicked = false;
}

// ── host: disabled action ─────────────────────────────────────────────────────

@Component({
  standalone: true,
  imports: [SpAdminTableActionsComponent],
  template: `
    <sp-admin-table-actions [actions]="actions" (actionClick)="onAction($event)">
    </sp-admin-table-actions>
  `,
})
class DisabledActionHostComponent {
  actions: SpAdminTableAction[] = [
    { label: 'Disabled action', disabled: true },
    { label: 'Active action' },
  ];
  lastAction: SpAdminTableAction | null = null;
  onAction(a: SpAdminTableAction) { this.lastAction = a; }
}

// ── helpers ───────────────────────────────────────────────────────────────────

function getTrigger(el: HTMLElement): HTMLButtonElement {
  return el.querySelector('[aria-label="Row actions"]') as HTMLButtonElement;
}

function getMenu(el: HTMLElement): HTMLElement | null {
  return el.querySelector('[role="menu"]');
}

// ── tests ─────────────────────────────────────────────────────────────────────

describe('SpAdminTableActionsComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  // ── trigger renders ────────────────────────────────────────────────────────

  it('renders trigger button with aria-label', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    const btn = getTrigger(fixture.nativeElement);
    expect(btn).not.toBeNull();
    expect(btn.getAttribute('aria-label')).toBe('Row actions');
  });

  it('trigger has aria-haspopup="menu"', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    const btn = getTrigger(fixture.nativeElement);
    expect(btn.getAttribute('aria-haspopup')).toBe('menu');
  });

  it('dropdown is NOT visible before trigger is clicked', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).toBeNull();
  });

  // ── open / close ───────────────────────────────────────────────────────────

  it('clicking trigger opens the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).not.toBeNull();
  });

  it('clicking trigger again closes the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    const trigger = getTrigger(fixture.nativeElement);
    trigger.click();
    fixture.detectChanges();
    trigger.click();
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).toBeNull();
  });

  it('Escape key closes the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).not.toBeNull();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).toBeNull();
  });

  it('clicking outside closes the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).not.toBeNull();

    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
    fixture.detectChanges();
    expect(getMenu(fixture.nativeElement)).toBeNull();
  });

  // ── action items render ────────────────────────────────────────────────────

  it('renders all action labels in the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('View');
    expect(text).toContain('Edit');
    expect(text).toContain('Delete');
  });

  it('renders action buttons with role="menuitem"', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    const items = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    expect(items.length).toBe(3);
  });

  // ── actionClick emission ───────────────────────────────────────────────────

  it('clicking an action emits actionClick with the action', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();

    const items: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    items[0].click(); // 'View'
    fixture.detectChanges();

    expect(fixture.componentInstance.lastAction?.label).toBe('View');
  });

  it('clicking an action closes the dropdown', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();

    const items: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    items[1].click(); // 'Edit'
    fixture.detectChanges();

    expect(getMenu(fixture.nativeElement)).toBeNull();
  });

  it('clicking a danger action emits actionClick', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();

    const items: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    items[2].click(); // 'Delete' (danger)
    fixture.detectChanges();

    expect(fixture.componentInstance.lastAction?.label).toBe('Delete');
    expect(fixture.componentInstance.lastAction?.danger).toBeTrue();
  });

  // ── disabled action ────────────────────────────────────────────────────────

  it('disabled action does NOT emit actionClick', () => {
    const fixture = TestBed.createComponent(DisabledActionHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();

    const items: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('[role="menuitem"]');
    // items[0] is the disabled one
    items[0].click();
    fixture.detectChanges();

    expect(fixture.componentInstance.lastAction).toBeNull();
  });

  // ── projected content ──────────────────────────────────────────────────────

  it('renders projected content when no actions are declared', () => {
    const fixture = TestBed.createComponent(ProjectedHostComponent);
    fixture.detectChanges();
    getTrigger(fixture.nativeElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Custom action');
  });

  // ── aria state ─────────────────────────────────────────────────────────────

  it('aria-expanded is false when closed', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    const trigger = getTrigger(fixture.nativeElement);
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });

  it('aria-expanded is true when open', () => {
    const fixture = TestBed.createComponent(ActionsHostComponent);
    fixture.detectChanges();
    const trigger = getTrigger(fixture.nativeElement);
    trigger.click();
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
  });
});

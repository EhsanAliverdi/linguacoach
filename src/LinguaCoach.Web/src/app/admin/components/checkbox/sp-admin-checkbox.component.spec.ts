import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { SpAdminCheckboxComponent } from './sp-admin-checkbox.component';

async function stabilize(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

@Component({
  standalone: true,
  imports: [SpAdminCheckboxComponent, FormsModule],
  template: `<sp-admin-checkbox [(ngModel)]="checked" label="Enable feature" />`,
})
class NgModelHostComponent {
  checked = false;
}

@Component({
  standalone: true,
  imports: [SpAdminCheckboxComponent, FormsModule],
  template: `<sp-admin-checkbox [(ngModel)]="checked" [disabled]="disabled" label="Option" />`,
})
class DisabledHostComponent {
  checked = false;
  disabled = false;
}

@Component({
  standalone: true,
  imports: [SpAdminCheckboxComponent, FormsModule],
  template: `<sp-admin-checkbox [(ngModel)]="checked" label="With helper" helper="Helper text" />`,
})
class HelperHostComponent {
  checked = true;
}

describe('SpAdminCheckboxComponent', () => {
  it('renders with initial unchecked state', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    expect(input).toBeTruthy();
    expect(input.checked).toBeFalse();
  });

  it('renders with initial checked state', async () => {
    const fixture = TestBed.createComponent(HelperHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    expect(input.checked).toBeTrue();
  });

  it('emits true when checked', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);

    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    input.checked = true;
    input.dispatchEvent(new Event('change'));
    await stabilize(fixture);

    expect(fixture.componentInstance.checked).toBeTrue();
  });

  it('emits false when unchecked', async () => {
    const fixture = TestBed.createComponent(HelperHostComponent);
    await stabilize(fixture);

    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    input.checked = false;
    input.dispatchEvent(new Event('change'));
    await stabilize(fixture);

    expect(fixture.componentInstance.checked).toBeFalse();
  });

  it('disables the checkbox when disabled binding is true', async () => {
    const fixture = TestBed.createComponent(DisabledHostComponent);
    fixture.componentInstance.disabled = true;
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    expect(input.disabled).toBeTrue();
  });

  it('enables the checkbox when disabled binding is false', async () => {
    const fixture = TestBed.createComponent(DisabledHostComponent);
    fixture.componentInstance.disabled = false;
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input[type=checkbox]');
    expect(input.disabled).toBeFalse();
  });

  it('renders label text', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Enable feature');
  });

  it('renders helper text when provided', async () => {
    const fixture = TestBed.createComponent(HelperHostComponent);
    await stabilize(fixture);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Helper text');
  });
});

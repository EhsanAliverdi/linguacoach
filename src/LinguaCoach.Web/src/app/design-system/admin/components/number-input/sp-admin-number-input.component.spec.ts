import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { SpAdminNumberInputComponent } from './sp-admin-number-input.component';

async function stabilize(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

@Component({
  standalone: true,
  imports: [SpAdminNumberInputComponent, FormsModule],
  template: `<sp-admin-number-input [(ngModel)]="value" [min]="1" [max]="100" />`,
})
class NgModelHostComponent {
  value: number | null = 42;
}

@Component({
  standalone: true,
  imports: [SpAdminNumberInputComponent, FormsModule],
  template: `<sp-admin-number-input [(ngModel)]="value" [disabled]="disabled" />`,
})
class DisabledHostComponent {
  value: number | null = 5;
  disabled = false;
}

@Component({
  standalone: true,
  imports: [SpAdminNumberInputComponent, FormsModule],
  template: `<sp-admin-number-input [(ngModel)]="value" />`,
})
class NullHostComponent {
  value: number | null = null;
}

describe('SpAdminNumberInputComponent', () => {
  it('renders with initial ngModel value', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input).toBeTruthy();
    expect(input.value).toBe('42');
  });

  it('emits numeric value on user input', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);

    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    input.value = '77';
    input.dispatchEvent(new Event('input'));
    await stabilize(fixture);

    expect(fixture.componentInstance.value).toBe(77);
  });

  it('emits null when field is cleared', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);

    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    input.value = '';
    input.dispatchEvent(new Event('input'));
    await stabilize(fixture);

    expect(fixture.componentInstance.value).toBeNull();
  });

  it('renders with null initial value as empty string', async () => {
    const fixture = TestBed.createComponent(NullHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.value).toBe('');
  });

  it('disables the input when disabled binding is true', async () => {
    const fixture = TestBed.createComponent(DisabledHostComponent);
    fixture.componentInstance.disabled = true;
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.disabled).toBeTrue();
  });

  it('enables the input when disabled binding is false', async () => {
    const fixture = TestBed.createComponent(DisabledHostComponent);
    fixture.componentInstance.disabled = false;
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.disabled).toBeFalse();
  });

  it('sets min attribute', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.getAttribute('min')).toBe('1');
  });

  it('sets max attribute', async () => {
    const fixture = TestBed.createComponent(NgModelHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.getAttribute('max')).toBe('100');
  });
});

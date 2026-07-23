import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { SpAdminMultiSelectComponent, SpAdminMultiSelectOption } from './sp-admin-multi-select.component';

async function stabilize(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const OPTIONS: SpAdminMultiSelectOption[] = [
  { value: 'a', label: 'Present simple', sublabel: 'A1 · grammar' },
  { value: 'b', label: 'Past simple', sublabel: 'A2 · grammar' },
  { value: 'c', label: 'Present perfect', sublabel: 'B1 · grammar' },
];

@Component({
  standalone: true,
  imports: [SpAdminMultiSelectComponent, FormsModule],
  template: `<sp-admin-multi-select [options]="options" [(ngModel)]="selected" (optionPicked)="picked = $event" />`,
})
class AccumulateHostComponent {
  options = OPTIONS;
  selected: string[] = [];
  picked: SpAdminMultiSelectOption | null = null;
}

@Component({
  standalone: true,
  imports: [SpAdminMultiSelectComponent, FormsModule],
  template: `<sp-admin-multi-select [options]="options" [excludeValues]="excludeValues" [accumulate]="false" (optionPicked)="picked = $event" />`,
})
class ImmediateHostComponent {
  options = OPTIONS;
  excludeValues: string[] = [];
  picked: SpAdminMultiSelectOption | null = null;
}

describe('SpAdminMultiSelectComponent', () => {
  it('opens the panel and lists options on focus', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);
    const optionEls = fixture.nativeElement.querySelectorAll('.sp-adm-ms-option');
    expect(optionEls.length).toBe(3);
  });

  it('filters options by search term (label and sublabel)', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.value = 'perfect';
    input.dispatchEvent(new Event('input'));
    await stabilize(fixture);
    const optionEls = fixture.nativeElement.querySelectorAll('.sp-adm-ms-option');
    expect(optionEls.length).toBe(1);
    expect(optionEls[0].textContent).toContain('Present perfect');
  });

  it('accumulate mode: picking an option adds a chip and updates ngModel', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);

    const option: HTMLElement = fixture.nativeElement.querySelector('.sp-adm-ms-option');
    option.click();
    await stabilize(fixture);

    expect(fixture.componentInstance.selected).toEqual(['a']);
    expect(fixture.componentInstance.picked?.value).toBe('a');
    const chips = fixture.nativeElement.querySelectorAll('.sp-adm-ms-chip');
    expect(chips.length).toBe(1);
    expect(chips[0].textContent).toContain('Present simple');
  });

  it('accumulate mode: a picked option no longer appears in the dropdown', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);
    fixture.nativeElement.querySelector('.sp-adm-ms-option').click();
    await stabilize(fixture);

    const remaining: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.sp-adm-ms-option');
    expect(remaining.length).toBe(2);
    const remainingText = Array.from(remaining).map(el => el.textContent).join('|');
    expect(remainingText).not.toContain('Present simple');
  });

  it('accumulate mode: removing a chip updates ngModel', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    fixture.componentInstance.selected = ['a', 'b'];
    await stabilize(fixture);

    const removeButtons = fixture.nativeElement.querySelectorAll('.sp-adm-ms-chip-remove');
    (removeButtons[0] as HTMLElement).click();
    await stabilize(fixture);

    expect(fixture.componentInstance.selected).toEqual(['b']);
  });

  it('accumulate=false mode: picking an option emits optionPicked but renders no chips', async () => {
    const fixture = TestBed.createComponent(ImmediateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);

    fixture.nativeElement.querySelector('.sp-adm-ms-option').click();
    await stabilize(fixture);

    expect(fixture.componentInstance.picked?.value).toBe('a');
    expect(fixture.nativeElement.querySelectorAll('.sp-adm-ms-chip').length).toBe(0);
    // Dropdown keeps showing all options next time since nothing accumulates internally.
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);
    expect(fixture.nativeElement.querySelectorAll('.sp-adm-ms-option').length).toBe(3);
  });

  it('excludeValues hides options regardless of mode', async () => {
    const fixture = TestBed.createComponent(ImmediateHostComponent);
    fixture.componentInstance.excludeValues = ['a', 'b'];
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);

    const optionEls = fixture.nativeElement.querySelectorAll('.sp-adm-ms-option');
    expect(optionEls.length).toBe(1);
    expect(optionEls[0].textContent).toContain('Present perfect');
  });

  it('Enter key picks the highlighted option', async () => {
    const fixture = TestBed.createComponent(AccumulateHostComponent);
    await stabilize(fixture);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.sp-adm-ms-input');
    input.dispatchEvent(new Event('focus'));
    await stabilize(fixture);

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await stabilize(fixture);

    expect(fixture.componentInstance.selected).toEqual(['a']);
  });
});

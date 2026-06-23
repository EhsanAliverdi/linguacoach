import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminToggleComponent } from './sp-admin-toggle.component';

describe('SpAdminToggleComponent', () => {
  let fixture: ComponentFixture<SpAdminToggleComponent>;
  let component: SpAdminToggleComponent;

  async function setup(inputs: Partial<SpAdminToggleComponent> = {}) {
    await TestBed.configureTestingModule({
      imports: [SpAdminToggleComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(SpAdminToggleComponent);
    component = fixture.componentInstance;
    Object.assign(component, inputs);
    fixture.detectChanges();
  }

  it('creates', async () => {
    await setup();
    expect(component).toBeTruthy();
  });

  it('renders unchecked state by default', async () => {
    await setup();
    const btn = fixture.nativeElement.querySelector('[role="switch"]');
    expect(btn.getAttribute('aria-checked')).toBe('false');
  });

  it('renders checked state when checked=true', async () => {
    await setup({ checked: true });
    const btn = fixture.nativeElement.querySelector('[role="switch"]');
    expect(btn.getAttribute('aria-checked')).toBe('true');
  });

  it('renders disabled state', async () => {
    await setup({ disabled: true });
    const root = fixture.nativeElement.querySelector('.sp-tog-root');
    expect(root.classList).toContain('sp-tog-disabled');
  });

  it('renders loading spinner when loading=true', async () => {
    await setup({ loading: true });
    expect(fixture.nativeElement.querySelector('.sp-tog-spinner')).toBeTruthy();
  });

  it('does not render spinner when not loading', async () => {
    await setup({ loading: false });
    expect(fixture.nativeElement.querySelector('.sp-tog-spinner')).toBeFalsy();
  });

  it('renders label when provided', async () => {
    await setup({ label: 'Enable feature' });
    expect(fixture.nativeElement.textContent).toContain('Enable feature');
  });

  it('renders description when provided', async () => {
    await setup({ label: 'Enable', description: 'Enables the feature globally' });
    expect(fixture.nativeElement.textContent).toContain('Enables the feature globally');
  });

  it('does not render label group when no label', async () => {
    await setup();
    expect(fixture.nativeElement.querySelector('.sp-tog-label-group')).toBeFalsy();
  });

  it('emits changed event on toggle click', async () => {
    await setup({ checked: false });
    const emitted: boolean[] = [];
    component.changed.subscribe((v: boolean) => emitted.push(v));
    const btn = fixture.nativeElement.querySelector('[role="switch"]');
    btn.click();
    expect(emitted).toEqual([true]);
  });

  it('emits false when toggling from checked=true', async () => {
    await setup({ checked: true });
    const emitted: boolean[] = [];
    component.changed.subscribe((v: boolean) => emitted.push(v));
    fixture.nativeElement.querySelector('[role="switch"]').click();
    expect(emitted).toEqual([false]);
  });

  it('does not emit when disabled', async () => {
    await setup({ disabled: true });
    const emitted: boolean[] = [];
    component.changed.subscribe((v: boolean) => emitted.push(v));
    component.toggle();
    expect(emitted).toEqual([]);
  });

  it('does not emit when loading', async () => {
    await setup({ loading: true });
    const emitted: boolean[] = [];
    component.changed.subscribe((v: boolean) => emitted.push(v));
    component.toggle();
    expect(emitted).toEqual([]);
  });

  it('implements ControlValueAccessor writeValue', async () => {
    await setup();
    component.writeValue(true);
    expect(component.checked).toBeTrue();
    component.writeValue(false);
    expect(component.checked).toBeFalse();
  });

  it('calls onChange when toggled via CVA', async () => {
    await setup({ checked: false });
    let called = false;
    component.registerOnChange((v: boolean) => { called = true; });
    fixture.nativeElement.querySelector('[role="switch"]').click();
    expect(called).toBeTrue();
  });

  it('setDisabledState sets disabled input', async () => {
    await setup();
    component.setDisabledState(true);
    expect(component.disabled).toBeTrue();
  });

  it('thumb has on class when checked', async () => {
    await setup({ checked: true });
    expect(fixture.nativeElement.querySelector('.sp-tog-thumb--on')).toBeTruthy();
  });

  it('track has on class when checked', async () => {
    await setup({ checked: true });
    expect(fixture.nativeElement.querySelector('.sp-tog-track--on')).toBeTruthy();
  });
});

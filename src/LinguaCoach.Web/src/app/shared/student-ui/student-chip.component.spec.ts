import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StudentChipComponent } from './student-chip.component';

describe('StudentChipComponent', () => {
  let fixture: ComponentFixture<StudentChipComponent>;
  let component: StudentChipComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StudentChipComponent] });
    fixture = TestBed.createComponent(StudentChipComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders a button element', () => {
    const btn = fixture.nativeElement.querySelector('button');
    expect(btn).toBeTruthy();
  });

  it('sets aria-pressed=false when unselected', () => {
    component.selected = false;
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button');
    expect(btn.getAttribute('aria-pressed')).toBe('false');
  });

  it('sets aria-pressed=true when selected', () => {
    component.selected = true;
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button');
    expect(btn.getAttribute('aria-pressed')).toBe('true');
  });

  it('emits toggle on click', () => {
    let emitted = false;
    component.toggle.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('button').click();
    expect(emitted).toBeTrue();
  });

  it('does not emit toggle when disabled', () => {
    component.disabled = true;
    fixture.detectChanges();
    let emitted = false;
    component.toggle.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('button').click();
    expect(emitted).toBeFalse();
  });
});

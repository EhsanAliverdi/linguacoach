import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StudentBadgeComponent } from './student-badge.component';

describe('StudentBadgeComponent', () => {
  let fixture: ComponentFixture<StudentBadgeComponent>;
  let component: StudentBadgeComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [StudentBadgeComponent] });
    fixture = TestBed.createComponent(StudentBadgeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders a span element', () => {
    const span = fixture.nativeElement.querySelector('span');
    expect(span).toBeTruthy();
  });

  it('renders with success variant', () => {
    component.variant = 'success';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('span')).toBeTruthy();
  });

  it('renders with warn variant', () => {
    component.variant = 'warn';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('span')).toBeTruthy();
  });

  it('renders with writing variant', () => {
    component.variant = 'writing';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('span')).toBeTruthy();
  });

  it('defaults to muted variant without throwing', () => {
    expect(fixture.nativeElement.querySelector('span')).toBeTruthy();
  });
});

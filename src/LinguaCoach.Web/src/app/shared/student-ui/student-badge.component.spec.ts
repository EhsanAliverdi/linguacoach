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

  it('renders a span with sp-badge class', () => {
    const span = fixture.nativeElement.querySelector('.sp-badge');
    expect(span).toBeTruthy();
  });

  it('applies variant class for success', () => {
    component.variant = 'success';
    fixture.detectChanges();
    const span = fixture.nativeElement.querySelector('.sp-badge');
    expect(span.classList).toContain('sp-badge--success');
  });

  it('applies variant class for warn', () => {
    component.variant = 'warn';
    fixture.detectChanges();
    const span = fixture.nativeElement.querySelector('.sp-badge');
    expect(span.classList).toContain('sp-badge--warn');
  });

  it('applies variant class for writing', () => {
    component.variant = 'writing';
    fixture.detectChanges();
    const span = fixture.nativeElement.querySelector('.sp-badge');
    expect(span.classList).toContain('sp-badge--writing');
  });

  it('defaults to muted variant', () => {
    const span = fixture.nativeElement.querySelector('.sp-badge');
    expect(span.classList).toContain('sp-badge--muted');
  });
});

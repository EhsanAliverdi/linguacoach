import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminSectionHeaderComponent } from './sp-admin-section-header.component';

describe('SpAdminSectionHeaderComponent', () => {
  let fixture: ComponentFixture<SpAdminSectionHeaderComponent>;
  let component: SpAdminSectionHeaderComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminSectionHeaderComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminSectionHeaderComponent);
    component = fixture.componentInstance;
  });

  it('renders title', () => {
    component.title = 'Ready lesson buffer per student';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ready lesson buffer per student');
  });

  it('renders description when provided', () => {
    component.description = 'Shows per-student counts';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Shows per-student counts');
  });

  it('does not render description element when empty', () => {
    component.description = '';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-sh-desc')).toBeNull();
  });

  it('renders without description by default', () => {
    component.title = 'Section';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-sh-title')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.sp-sh-desc')).toBeNull();
  });
});

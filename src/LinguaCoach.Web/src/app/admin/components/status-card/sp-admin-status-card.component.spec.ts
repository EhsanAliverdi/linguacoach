import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminStatusCardComponent } from './sp-admin-status-card.component';

describe('SpAdminStatusCardComponent', () => {
  let fixture: ComponentFixture<SpAdminStatusCardComponent>;
  let component: SpAdminStatusCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminStatusCardComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminStatusCardComponent);
    component = fixture.componentInstance;
  });

  it('renders label', () => {
    component.label = 'Database';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Database');
  });

  it('renders string value', () => {
    component.value = 'Reachable';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Reachable');
  });

  it('renders numeric value', () => {
    component.value = 42;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('42');
  });

  it('renders helper text when provided', () => {
    component.helper = 'Last checked 2s ago';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Last checked 2s ago');
  });

  it('does not render helper when empty', () => {
    component.helper = '';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-sc-helper')).toBeNull();
  });

  it('shows skeleton when loading', () => {
    component.loading = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-sc-skeleton')).toBeTruthy();
  });

  it('hides skeleton when not loading', () => {
    component.loading = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-sc-skeleton')).toBeNull();
  });

  it('accepts all tone values without error', () => {
    const tones: Array<SpAdminStatusCardComponent['tone']> = ['success', 'warning', 'danger', 'info', 'neutral', 'primary'];
    for (const tone of tones) {
      component.tone = tone;
      expect(() => fixture.detectChanges()).not.toThrow();
    }
  });
});

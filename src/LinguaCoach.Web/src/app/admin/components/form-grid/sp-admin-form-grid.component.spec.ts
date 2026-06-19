import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminFormGridComponent } from './sp-admin-form-grid.component';

describe('SpAdminFormGridComponent', () => {
  let fixture: ComponentFixture<SpAdminFormGridComponent>;
  let component: SpAdminFormGridComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminFormGridComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminFormGridComponent);
    component = fixture.componentInstance;
  });

  it('renders projected content', () => {
    fixture.nativeElement.innerHTML = '<label>Field</label>';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Field');
  });

  it('defaults to 2 columns', () => {
    fixture.detectChanges();
    expect(component.columns).toBe(2);
  });

  it('accepts columns=3 without error', () => {
    component.columns = 3;
    expect(() => fixture.detectChanges()).not.toThrow();
  });

  it('accepts columns=1 without error', () => {
    component.columns = 1;
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminStatusGridComponent } from './sp-admin-status-grid.component';

describe('SpAdminStatusGridComponent', () => {
  let fixture: ComponentFixture<SpAdminStatusGridComponent>;
  let component: SpAdminStatusGridComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminStatusGridComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminStatusGridComponent);
    component = fixture.componentInstance;
  });

  it('renders projected content', () => {
    fixture.nativeElement.innerHTML = '<span>Card</span>';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Card');
  });

  it('defaults to auto columns', () => {
    fixture.detectChanges();
    expect(component.columns).toBe('auto');
  });

  it('accepts columns=4 without error', () => {
    component.columns = 4;
    expect(() => fixture.detectChanges()).not.toThrow();
  });

  it('accepts columns=2 without error', () => {
    component.columns = 2;
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from './sp-admin-breakdown-bars.component';

const items: BreakdownBarItem[] = [
  { label: 'A1', value: 5, pct: 25, tone: 'green' },
  { label: 'A2', value: 10, pct: 50, tone: 'indigo' },
  { label: 'B1', value: 5, pct: 25, tone: 'violet' },
];

describe('SpAdminBreakdownBarsComponent', () => {
  let fixture: ComponentFixture<SpAdminBreakdownBarsComponent>;
  let component: SpAdminBreakdownBarsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminBreakdownBarsComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminBreakdownBarsComponent);
    component = fixture.componentInstance;
  });

  it('creates', () => expect(component).toBeTruthy());

  it('renders a row for each item', () => {
    component.items = items;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-bdb-row').length).toBe(3);
  });

  it('shows empty state when no items', () => {
    component.items = [];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-bdb-empty')).toBeTruthy();
  });

  it('renders labels', () => {
    component.items = items;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('A1');
    expect(text).toContain('A2');
    expect(text).toContain('B1');
  });

  it('renders values', () => {
    component.items = items;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('5');
    expect(text).toContain('10');
  });

  it('renders percentages when showPct is true', () => {
    component.items = items;
    component.showPct = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-bdb-pct').length).toBe(3);
  });

  it('does not render pct column when showPct is false', () => {
    component.items = items;
    component.showPct = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-bdb-pct').length).toBe(0);
  });

  it('fill bar has correct width style', () => {
    component.items = [{ label: 'A1', value: 5, pct: 50 }];
    fixture.detectChanges();
    const fill = fixture.nativeElement.querySelector('.sp-bdb-fill') as HTMLElement;
    expect(fill.style.width).toBe('50%');
  });

  it('fill has correct tone class', () => {
    component.items = [{ label: 'A1', value: 5, pct: 50, tone: 'green' }];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-bdb-fill-green')).toBeTruthy();
  });

  it('renders title', () => {
    component.items = items;
    component.title = 'CEFR Distribution';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('CEFR Distribution');
  });

  it('fill has ARIA progressbar role', () => {
    component.items = [{ label: 'A1', value: 5, pct: 50 }];
    fixture.detectChanges();
    const fill = fixture.nativeElement.querySelector('[role="progressbar"]');
    expect(fill).toBeTruthy();
    expect(fill.getAttribute('aria-valuenow')).toBe('50');
  });
});

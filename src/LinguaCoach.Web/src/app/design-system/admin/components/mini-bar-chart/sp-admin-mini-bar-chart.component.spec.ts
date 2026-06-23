import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminMiniBarChartComponent, MiniBarItem } from './sp-admin-mini-bar-chart.component';

const items: MiniBarItem[] = [
  { label: 'Mon', value: 10 },
  { label: 'Tue', value: 25 },
  { label: 'Wed', value: 5 },
];

describe('SpAdminMiniBarChartComponent', () => {
  let fixture: ComponentFixture<SpAdminMiniBarChartComponent>;
  let component: SpAdminMiniBarChartComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminMiniBarChartComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminMiniBarChartComponent);
    component = fixture.componentInstance;
  });

  it('creates', () => expect(component).toBeTruthy());

  it('renders bars for each item', () => {
    component.items = items;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-mbc-bar').length).toBe(3);
  });

  it('shows empty state when no items', () => {
    component.items = [];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-mbc-empty')).toBeTruthy();
  });

  it('shows empty state when items is null', () => {
    component.items = null as any;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-mbc-empty')).toBeTruthy();
  });

  it('scales tallest bar to 100%', () => {
    component.items = items;
    const bars = component.scaledBars();
    const heights = bars.map(b => b.heightPct);
    expect(Math.max(...heights)).toBe(100);
  });

  it('renders title when provided', () => {
    component.items = items;
    component.title = 'Calls over time';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Calls over time');
  });

  it('renders labels when showLabels is true', () => {
    component.items = items;
    component.showLabels = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-mbc-label').length).toBe(3);
  });

  it('bar has tooltip attribute with label and value', () => {
    component.items = [{ label: 'Mon', value: 10 }];
    const bars = component.scaledBars();
    expect(bars[0].tooltip).toContain('10');
  });

  it('bar uses date in tooltip when provided', () => {
    component.items = [{ label: 'Mon', value: 10, date: '2026-06-01' }];
    const bars = component.scaledBars();
    expect(bars[0].tooltip).toContain('2026-06-01');
  });

  it('bar with zero value gets zero heightPct', () => {
    component.items = [{ label: 'Mon', value: 0 }, { label: 'Tue', value: 10 }];
    const bars = component.scaledBars();
    expect(bars[0].heightPct).toBe(0);
  });

  it('applies tone class to bars', () => {
    component.items = items;
    component.tone = 'green';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-mbc-bar-green')).toBeTruthy();
  });
});

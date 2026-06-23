import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminRingMetricComponent } from './sp-admin-ring-metric.component';

describe('SpAdminRingMetricComponent', () => {
  let fixture: ComponentFixture<SpAdminRingMetricComponent>;
  let component: SpAdminRingMetricComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminRingMetricComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminRingMetricComponent);
    component = fixture.componentInstance;
  });

  it('creates', () => expect(component).toBeTruthy());

  it('renders an SVG ring', () => {
    component.pct = 75;
    component.label = 'Success rate';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).toBeTruthy();
  });

  it('renders the label', () => {
    component.pct = 50;
    component.label = 'Ready';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ready');
  });

  it('renders sub text', () => {
    component.pct = 50;
    component.label = 'Ready';
    component.sub = '4 of 8';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('4 of 8');
  });

  it('renders the fill circle when pct > 0', () => {
    component.pct = 60;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-ring-fill')).toBeTruthy();
  });

  it('does not render fill circle when pct is 0', () => {
    component.pct = 0;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-ring-fill')).toBeFalsy();
  });

  it('applies tone class to fill', () => {
    component.pct = 50;
    component.tone = 'green';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-ring-fill-green')).toBeTruthy();
  });

  it('uses displayValue when provided', () => {
    component.pct = 75;
    component.displayValue = '6/8';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('6/8');
  });

  it('computes circumference correctly', () => {
    component.size = 72;
    const r = (72 - 12) / 2;
    expect(component.circumference).toBeCloseTo(2 * Math.PI * r, 2);
  });

  it('dashOffset is 0 at 100%', () => {
    component.pct = 100;
    expect(component.dashOffset).toBeCloseTo(0, 1);
  });

  it('dashOffset equals circumference at 0%', () => {
    component.pct = 0;
    expect(component.dashOffset).toBeCloseTo(component.circumference, 1);
  });
});

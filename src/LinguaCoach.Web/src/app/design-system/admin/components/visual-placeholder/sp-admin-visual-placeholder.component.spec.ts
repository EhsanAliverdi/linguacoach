import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminVisualPlaceholderComponent } from './sp-admin-visual-placeholder.component';

describe('SpAdminVisualPlaceholderComponent', () => {
  let fixture: ComponentFixture<SpAdminVisualPlaceholderComponent>;
  let component: SpAdminVisualPlaceholderComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminVisualPlaceholderComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminVisualPlaceholderComponent);
    component = fixture.componentInstance;
  });

  it('creates', () => expect(component).toBeTruthy());

  it('shows "Backend not available yet" for not-available state', () => {
    component.state = 'not-available';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
  });

  it('shows "Foundation only" for foundation-only state', () => {
    component.state = 'foundation-only';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Foundation only');
  });

  it('shows "Not implemented" for not-implemented state', () => {
    component.state = 'not-implemented';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Not implemented');
  });

  it('shows "Deferred" for deferred state', () => {
    component.state = 'deferred';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Deferred');
  });

  it('shows custom title when provided', () => {
    component.state = 'not-available';
    component.title = 'Activity trends';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Activity trends');
  });

  it('shows message when provided', () => {
    component.state = 'not-available';
    component.message = 'Requires a backend endpoint';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Requires a backend endpoint');
  });

  it('renders icon SVG', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('svg')).toBeTruthy();
  });

  it('applies correct state CSS class', () => {
    component.state = 'foundation-only';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-foundation-only')).toBeTruthy();
  });

  it('renders no skeleton element when skeleton is none', () => {
    component.skeleton = 'none';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-skeleton')).toBeFalsy();
  });

  it('renders chart skeleton SVG when skeleton is chart', () => {
    component.skeleton = 'chart';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-skeleton')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.sp-vp-sk-svg')).toBeTruthy();
  });

  it('renders ring skeleton when skeleton is ring', () => {
    component.skeleton = 'ring';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-sk-ring-arc')).toBeTruthy();
  });

  it('renders timeline skeleton when skeleton is timeline', () => {
    component.skeleton = 'timeline';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-sk-tl-line')).toBeTruthy();
  });

  it('renders grid skeleton when skeleton is grid', () => {
    component.skeleton = 'grid';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-vp-skeleton')).toBeTruthy();
  });
});

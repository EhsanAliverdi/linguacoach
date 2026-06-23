import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminGraphCardComponent } from './sp-admin-graph-card.component';

describe('SpAdminGraphCardComponent', () => {
  let fixture: ComponentFixture<SpAdminGraphCardComponent>;
  let component: SpAdminGraphCardComponent;

  async function setup(inputs: Partial<SpAdminGraphCardComponent> = {}) {
    await TestBed.configureTestingModule({
      imports: [SpAdminGraphCardComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(SpAdminGraphCardComponent);
    component = fixture.componentInstance;
    Object.assign(component, inputs);
    fixture.detectChanges();
  }

  it('renders title', async () => {
    await setup({ title: 'Daily calls' });
    expect(fixture.nativeElement.textContent).toContain('Daily calls');
  });

  it('renders subtitle when provided', async () => {
    await setup({ title: 'Chart', subtitle: 'Last 30 days' });
    expect(fixture.nativeElement.textContent).toContain('Last 30 days');
  });

  it('does not render subtitle when absent', async () => {
    await setup({ title: 'Chart' });
    const el = fixture.nativeElement.querySelector('.sp-gc-subtitle');
    expect(el).toBeFalsy();
  });

  it('renders live status badge', async () => {
    await setup({ title: 'Chart', status: 'live' });
    expect(fixture.nativeElement.textContent).toContain('Live');
  });

  it('renders unavailable status badge', async () => {
    await setup({ title: 'Chart', status: 'unavailable' });
    expect(fixture.nativeElement.textContent).toContain('Unavailable');
  });

  it('renders partial status badge', async () => {
    await setup({ title: 'Chart', status: 'partial' });
    expect(fixture.nativeElement.textContent).toContain('Partial');
  });

  it('renders action link when actionLabel provided', async () => {
    await setup({ title: 'Chart', actionLabel: 'View all →', actionHref: '/admin/usage' });
    const link = fixture.nativeElement.querySelector('.sp-gc-action');
    expect(link).toBeTruthy();
    expect(link.textContent.trim()).toBe('View all →');
  });

  it('renders footer note', async () => {
    await setup({ title: 'Chart', footerNote: 'Derived from loaded data' });
    expect(fixture.nativeElement.textContent).toContain('Derived from loaded data');
  });

  it('does not render footer when absent', async () => {
    await setup({ title: 'Chart' });
    expect(fixture.nativeElement.querySelector('.sp-gc-footer')).toBeFalsy();
  });

  it('does not render status for loading state', async () => {
    await setup({ title: 'Chart', status: 'loading' });
    expect(fixture.nativeElement.querySelector('.sp-gc-status')).toBeFalsy();
  });
});

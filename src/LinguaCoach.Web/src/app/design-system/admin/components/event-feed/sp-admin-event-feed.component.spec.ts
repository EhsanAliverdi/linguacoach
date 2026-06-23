import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpAdminEventFeedComponent, EventFeedItem } from './sp-admin-event-feed.component';

const items: EventFeedItem[] = [
  { id: '1', timestamp: '2026-06-23T10:00:00Z', title: 'User logged in', level: 'Information', category: 'Auth' },
  { id: '2', timestamp: '2026-06-23T10:01:00Z', title: 'DB timeout', message: 'Connection pool exhausted', level: 'Error', category: 'Database' },
  { id: '3', timestamp: '2026-06-23T10:02:00Z', title: 'Slow query', level: 'Warning', category: 'Database' },
];

describe('SpAdminEventFeedComponent', () => {
  let fixture: ComponentFixture<SpAdminEventFeedComponent>;
  let component: SpAdminEventFeedComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SpAdminEventFeedComponent] }).compileComponents();
    fixture = TestBed.createComponent(SpAdminEventFeedComponent);
    component = fixture.componentInstance;
  });

  it('creates', () => expect(component).toBeTruthy());

  it('renders an item for each event', () => {
    component.items = items;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.sp-ef-item').length).toBe(3);
  });

  it('shows empty state when no items', () => {
    component.items = [];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-ef-empty')).toBeTruthy();
  });

  it('shows loading state', () => {
    component.loading = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Loading');
  });

  it('shows custom empty message', () => {
    component.items = [];
    component.emptyMessage = 'No recent events';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No recent events');
  });

  it('renders event titles', () => {
    component.items = items;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('User logged in');
    expect(text).toContain('DB timeout');
  });

  it('renders Error level badge', () => {
    component.items = [items[1]];
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Error');
  });

  it('renders Warning level badge', () => {
    component.items = [items[2]];
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Warning');
  });

  it('does not render Information level badge (not shown)', () => {
    component.items = [items[0]];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.sp-ef-level')).toBeFalsy();
  });

  it('dotTone maps Error to danger', () => {
    expect(component.dotTone('Error')).toBe('danger');
  });

  it('dotTone maps Warning to warning', () => {
    expect(component.dotTone('Warning')).toBe('warning');
  });

  it('dotTone maps Information to info', () => {
    expect(component.dotTone('Information')).toBe('info');
  });

  it('dotTone maps unknown to neutral', () => {
    expect(component.dotTone(undefined)).toBe('neutral');
  });

  it('renders category text', () => {
    component.items = [items[1]];
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Database');
  });
});

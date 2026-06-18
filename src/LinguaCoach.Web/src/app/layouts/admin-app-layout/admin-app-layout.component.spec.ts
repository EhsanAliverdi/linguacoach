import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminAppLayoutComponent } from './admin-app-layout.component';
import { AuthService } from '../../core/services/auth.service';

// Phase 10X-G: admin header user menu now uses sp-admin-dropdown.
describe('AdminAppLayoutComponent header dropdown', () => {
  function setup() {
    const auth = {
      currentUser: signal({ email: 'admin@speakpath.app' }),
      logout: jasmine.createSpy('logout'),
      isAuthenticated: () => true,
    };
    TestBed.configureTestingModule({
      imports: [AdminAppLayoutComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(AdminAppLayoutComponent);
    fixture.detectChanges();
    return { fixture, auth };
  }

  it('renders the avatar trigger inside an sp-admin-dropdown', () => {
    const { fixture } = setup();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('sp-admin-dropdown')).toBeTruthy();
    expect(host.querySelector('.sp-admin-header-user')).toBeTruthy();
    expect(host.querySelector('.sp-admin-avatar')).toBeTruthy();
  });

  it('shows the avatar initial from the admin email', () => {
    const { fixture } = setup();
    const avatar = fixture.nativeElement.querySelector('.sp-admin-avatar') as HTMLElement;
    expect(avatar.textContent?.trim()).toBe('A');
  });

  it('opens the profile menu when the avatar is clicked', () => {
    const { fixture } = setup();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.sp-admin-profile-menu')).toBeNull();

    (host.querySelector('.sp-admin-avatar') as HTMLElement).click();
    fixture.detectChanges();

    expect(host.querySelector('.sp-admin-profile-menu')).toBeTruthy();
    expect(host.querySelector('.sp-admin-profile-email')?.textContent).toContain('admin@speakpath.app');
  });

  it('signs out from the dropdown menu item', () => {
    const { fixture, auth } = setup();
    const host = fixture.nativeElement as HTMLElement;

    (host.querySelector('.sp-admin-avatar') as HTMLElement).click();
    fixture.detectChanges();

    (host.querySelector('.sp-admin-profile-signout') as HTMLElement).click();
    expect(auth.logout).toHaveBeenCalled();
  });
});

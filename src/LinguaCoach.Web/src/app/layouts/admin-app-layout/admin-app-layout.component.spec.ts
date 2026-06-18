import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminAppLayoutComponent } from './admin-app-layout.component';
import { AuthService } from '../../core/services/auth.service';

function setup() {
  localStorage.removeItem('speakpath.adminSidebarCollapsed');
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
  return { fixture, auth, host: fixture.nativeElement as HTMLElement };
}

describe('AdminAppLayoutComponent (Phase 10X-LAYOUT-BLOCKER)', () => {

  it('sp-admin-layout renders with min-h-screen xl:flex shell', () => {
    const { host } = setup();
    expect(host.querySelector('sp-admin-layout')).toBeTruthy();
    const shell = host.querySelector('.min-h-screen');
    expect(shell).toBeTruthy();
  });

  it('aside in sp-admin-sidebar is fixed and h-screen with border-r', () => {
    const { host } = setup();
    const aside = host.querySelector('sp-admin-sidebar aside');
    expect(aside).toBeTruthy();
    expect(aside!.classList).toContain('fixed');
    expect(aside!.classList).toContain('h-screen');
    expect(aside!.classList).toContain('border-r');
  });

  it('sidebar starts at w-[290px] when expanded', () => {
    const { host } = setup();
    const aside = host.querySelector('sp-admin-sidebar aside');
    expect(aside!.classList).toContain('w-[290px]');
  });

  it('header is sticky top-0', () => {
    const { host } = setup();
    const header = host.querySelector('sp-admin-header header');
    expect(header).toBeTruthy();
    expect(header!.classList).toContain('sticky');
    expect(header!.classList).toContain('top-0');
  });

  it('nav links carry menu-item class', () => {
    const { host } = setup();
    const links = host.querySelectorAll('sp-admin-sidebar a.menu-item');
    expect(links.length).toBeGreaterThan(0);
  });

  it('nav links carry menu-item-inactive class', () => {
    const { host } = setup();
    const links = host.querySelectorAll('sp-admin-sidebar a.menu-item-inactive');
    expect(links.length).toBeGreaterThan(0);
  });

  it('desktop sidebar has Menu and System section headings', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')!.textContent ?? '';
    expect(text).toContain('Menu');
    expect(text).toContain('System');
  });

  it('mobile backdrop is hidden by default', () => {
    const { host } = setup();
    const backdrop = host.querySelector('.fixed.inset-0.bg-gray-900\\/50');
    expect(backdrop).toBeTruthy();
    expect(backdrop!.classList).toContain('hidden');
  });

  it('mobile drawer starts with -translate-x-full', () => {
    const { host } = setup();
    const drawer = host.querySelector('aside.-translate-x-full');
    expect(drawer).toBeTruthy();
  });

  it('hamburger button opens the mobile drawer', () => {
    const { fixture, host } = setup();
    const hamburger = host.querySelector('button[aria-label="Open navigation"]') as HTMLElement;
    expect(hamburger).toBeTruthy();
    hamburger.click();
    fixture.detectChanges();
    const drawer = host.querySelector('aside.translate-x-0');
    expect(drawer).toBeTruthy();
  });

  it('backdrop becomes visible when drawer is open', () => {
    const { fixture, host } = setup();
    (host.querySelector('button[aria-label="Open navigation"]') as HTMLElement).click();
    fixture.detectChanges();
    const backdrop = host.querySelector('.fixed.inset-0.bg-gray-900\\/50');
    expect(backdrop!.classList).not.toContain('hidden');
  });

  it('sidebar collapses to w-[90px] when toggleSidebar is called', () => {
    const { fixture, host } = setup();
    fixture.componentInstance.toggleSidebar();
    fixture.detectChanges();
    const aside = host.querySelector('sp-admin-sidebar aside');
    expect(aside!.classList).toContain('w-[90px]');
  });

  it('desktop toggle button is hidden on mobile (hidden xl:flex)', () => {
    const { host } = setup();
    const toggleBtn = host.querySelector('button[aria-label="Toggle sidebar"]') as HTMLElement;
    expect(toggleBtn).toBeTruthy();
    expect(toggleBtn.classList).toContain('hidden');
    expect(toggleBtn.classList).toContain('xl:flex');
  });

  it('renders the sp-admin-avatar trigger', () => {
    const { host } = setup();
    expect(host.querySelector('.sp-admin-avatar')).toBeTruthy();
  });

  it('shows the avatar initial from the admin email', () => {
    const { host } = setup();
    const avatar = host.querySelector('.sp-admin-avatar') as HTMLElement;
    expect(avatar.textContent?.trim()).toBe('A');
  });

  it('opens the profile dropdown when avatar is clicked', () => {
    const { fixture, host } = setup();
    expect(host.querySelector('.sp-admin-profile-menu')).toBeNull();
    (host.querySelector('.sp-admin-avatar') as HTMLElement).click();
    fixture.detectChanges();
    expect(host.querySelector('.sp-admin-profile-menu')).toBeTruthy();
  });

  it('signs out when dropdown sign-out item is clicked', () => {
    const { fixture, auth, host } = setup();
    (host.querySelector('.sp-admin-avatar') as HTMLElement).click();
    fixture.detectChanges();
    (host.querySelector('.sp-admin-profile-signout') as HTMLElement).click();
    expect(auth.logout).toHaveBeenCalled();
  });

  it('renders sp-admin-theme-toggle inside the header', () => {
    const { host } = setup();
    expect(host.querySelector('sp-admin-theme-toggle')).toBeTruthy();
  });

  it('renders router-outlet for page content', () => {
    const { host } = setup();
    expect(host.querySelector('router-outlet')).toBeTruthy();
  });

  it('adds admin-layout class to body on init and removes on destroy', () => {
    const { fixture } = setup();
    expect(document.body.classList).toContain('admin-layout');
    fixture.destroy();
    expect(document.body.classList).not.toContain('admin-layout');
  });

});

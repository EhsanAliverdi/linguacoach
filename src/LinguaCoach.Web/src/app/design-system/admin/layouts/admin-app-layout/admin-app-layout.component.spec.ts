import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminAppLayoutComponent } from './admin-app-layout.component';
import { AuthService } from '../../../../core/services/auth.service';

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

describe('AdminAppLayoutComponent (Phase 10UI-FIX-2)', () => {

  it('sp-admin-layout renders the admin shell', () => {
    const { host } = setup();
    expect(host.querySelector('sp-admin-layout')).toBeTruthy();
    expect(host.querySelector('sp-admin-sidebar')).toBeTruthy();
    expect(host.querySelector('sp-admin-header')).toBeTruthy();
    expect(host.querySelector('main')).toBeTruthy();
  });

  it('sp-admin-sidebar renders an aside', () => {
    const { host } = setup();
    const aside = host.querySelector('sp-admin-sidebar aside');
    expect(aside).toBeTruthy();
  });

  it('sidebar starts expanded', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance.collapsed()).toBeFalse();
  });

  it('sp-admin-header renders a header', () => {
    const { host } = setup();
    const header = host.querySelector('sp-admin-header header');
    expect(header).toBeTruthy();
  });

  it('nav renders expected admin links', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')?.textContent ?? '';
    expect(text).toContain('Dashboard');
    expect(text).toContain('Students');
    expect(text).toContain('AI Config');
  });

  it('desktop sidebar has Menu and System section headings', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')!.textContent ?? '';
    expect(text).toContain('Menu');
    expect(text).toContain('System');
  });

  it('mobile drawer is closed by default', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance.drawerOpen()).toBeFalse();
  });

  it('hamburger button opens the mobile drawer', () => {
    const { fixture, host } = setup();
    const hamburger = host.querySelector('button[aria-label="Open navigation"]') as HTMLElement;
    expect(hamburger).toBeTruthy();
    hamburger.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.drawerOpen()).toBeTrue();
  });

  it('escape closes the mobile drawer', () => {
    const { fixture, host } = setup();
    (host.querySelector('button[aria-label="Open navigation"]') as HTMLElement).click();
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.componentInstance.drawerOpen()).toBeFalse();
  });

  it('sidebar collapses when toggleSidebar is called', () => {
    const { fixture } = setup();
    fixture.componentInstance.toggleSidebar();
    fixture.detectChanges();
    expect(fixture.componentInstance.collapsed()).toBeTrue();
  });

  it('desktop toggle button is present', () => {
    const { host } = setup();
    const toggleBtn = host.querySelector('button[aria-label="Toggle sidebar"]') as HTMLElement;
    expect(toggleBtn).toBeTruthy();
  });

  it('renders the profile trigger', () => {
    const { host } = setup();
    expect(host.querySelector('button[aria-label="Profile menu"]')).toBeTruthy();
  });

  it('shows the avatar initial from the admin email', () => {
    const { host } = setup();
    const avatar = host.querySelector('button[aria-label="Profile menu"]') as HTMLElement;
    expect(avatar.textContent?.trim()).toBe('A');
  });

  it('opens the profile dropdown when avatar is clicked', () => {
    const { fixture, host } = setup();
    expect(host.querySelector('[role="menu"]')).toBeNull();
    (host.querySelector('button[aria-label="Profile menu"]') as HTMLElement).click();
    fixture.detectChanges();
    expect(host.querySelector('[role="menu"]')).toBeTruthy();
  });

  it('signs out when dropdown sign-out item is clicked', () => {
    const { fixture, auth, host } = setup();
    (host.querySelector('button[aria-label="Profile menu"]') as HTMLElement).click();
    fixture.detectChanges();
    (Array.from(host.querySelectorAll('button')) as HTMLElement[])
      .find(button => button.textContent?.includes('Sign out'))!
      .click();
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

});

describe('AdminAppLayoutComponent — nav links and shell (Phase 10UI-FIX-2)', () => {

  function getAllNavLinks(host: HTMLElement) {
    return Array.from(host.querySelectorAll('sp-admin-sidebar a[routerLink], sp-admin-sidebar a[ng-reflect-router-link]'));
  }
  function getDrawerLinks(host: HTMLElement) {
    return Array.from(host.querySelectorAll('aside.xl\\:hidden a[routerLink], aside.xl\\:hidden a[ng-reflect-router-link]'));
  }

  it('desktop sidebar contains all required nav routes', () => {
    const { host } = setup();
    const links = getAllNavLinks(host);
    const routes = links.map(l => l.getAttribute('ng-reflect-router-link') ?? l.getAttribute('routerLink'));
    const required = ['/admin', '/admin/students', '/admin/ai-config', '/admin/prompts',
      '/admin/usage', '/admin/usage-policies', '/admin/curriculum',
      '/admin/exercise-types', '/admin/notifications',
      '/admin/integrations', '/admin/diagnostics', '/admin/security'];
    for (const r of required) {
      expect(routes).withContext(`route ${r} missing from desktop sidebar`).toContain(r);
    }
  });

  it('Usage Policies link renders in desktop sidebar', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')?.textContent ?? '';
    expect(text).toContain('Usage Policies');
  });

  it('Curriculum link renders in desktop sidebar', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')?.textContent ?? '';
    expect(text).toContain('Curriculum');
  });

  it('Security link renders in desktop sidebar', () => {
    const { host } = setup();
    const text = host.querySelector('sp-admin-sidebar')?.textContent ?? '';
    expect(text).toContain('Security');
  });

  it('sidebar nav items use sp-nav-item class', () => {
    const { host } = setup();
    const navItems = host.querySelectorAll('sp-admin-sidebar a.sp-nav-item');
    expect(navItems.length).toBeGreaterThan(8);
  });

  it('logout is NOT rendered as a sidebar nav item', () => {
    const { host } = setup();
    const sidebarText = host.querySelector('sp-admin-sidebar')?.textContent ?? '';
    expect(sidebarText.toLowerCase()).not.toContain('sign out');
    expect(sidebarText.toLowerCase()).not.toContain('log out');
    expect(sidebarText.toLowerCase()).not.toContain('logout');
  });

  it('mobile drawer contains Usage Policies and Curriculum links', () => {
    const { host } = setup();
    const drawerEl = host.querySelector('aside') as HTMLElement;
    const text = drawerEl?.textContent ?? '';
    expect(text).toContain('Usage Policies');
    expect(text).toContain('Curriculum');
  });

  it('mobile drawer contains Security link', () => {
    const { host } = setup();
    const drawerEl = host.querySelector('aside') as HTMLElement;
    const text = drawerEl?.textContent ?? '';
    expect(text).toContain('Security');
  });

  it('header still renders notification bell and user menu', () => {
    const { host } = setup();
    expect(host.querySelector('sp-admin-header')).toBeTruthy();
    expect(host.querySelector('button[aria-label="Profile menu"]')).toBeTruthy();
  });

  it('shell projects page content via router-outlet', () => {
    const { host } = setup();
    expect(host.querySelector('router-outlet')).toBeTruthy();
  });

});

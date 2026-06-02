import { TokenService } from './token.service';

describe('TokenService', () => {
  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('restores the authenticated user after a page reload', () => {
    const first = new TokenService();
    first.setToken(makeToken(), true);

    const restored = new TokenService();

    expect(restored.isAuthenticated()).toBeTrue();
    expect(restored.getUser()?.email).toBe('student@example.com');
    expect(restored.getUser()?.mustChangePassword).toBeTrue();
  });

  it('clears the persisted session', () => {
    const service = new TokenService();
    service.setToken(makeToken());

    service.clear();

    expect(service.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem('speakpath.auth')).toBeNull();
  });

  function makeToken(): string {
    const header = encode({ alg: 'none', typ: 'JWT' });
    const payload = encode({
      sub: 'student-id',
      email: 'student@example.com',
      role: 'Student',
      exp: Math.floor(Date.now() / 1000) + 3600,
    });
    return `${header}.${payload}.signature`;
  }

  function encode(value: object): string {
    return btoa(JSON.stringify(value))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
  }
});

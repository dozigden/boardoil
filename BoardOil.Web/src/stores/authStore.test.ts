import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from './authStore';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { AuthSession, AuthUser } from '../types/authTypes';

const authApi = {
  registerInitialAdmin: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  changeOwnPassword: vi.fn(),
  getMe: vi.fn(),
  getCsrfToken: vi.fn(),
  getBootstrapStatus: vi.fn()
};

const setCsrfToken = vi.fn();
const setUnauthorizedHandler = vi.fn();
const { router } = vi.hoisted(() => ({
  router: {
    currentRoute: { value: { name: 'boards' as string | null } },
    replace: vi.fn(async () => undefined)
  }
}));

vi.mock('../api/authApi', () => ({
  createAuthApi: () => authApi
}));

vi.mock('../api/http', () => ({
  setCsrfToken: (token: string | null) => setCsrfToken(token),
  setUnauthorizedHandler: (handler: (() => void | Promise<void>) | null) => setUnauthorizedHandler(handler)
}));

vi.mock('../router', () => ({
  router
}));

describe('authStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    authApi.getMe.mockResolvedValue(ok<AuthUser | null>(null));
    authApi.getCsrfToken.mockResolvedValue(ok('csrf-token'));
    authApi.getBootstrapStatus.mockResolvedValue(ok(false));
    authApi.logout.mockResolvedValue(ok(undefined));
    setUnauthorizedHandler.mockClear();
    router.replace.mockClear();
    router.currentRoute.value.name = 'boards';
  });

  it('initialize keeps anonymous state when /me returns no user', async () => {
    const store = useAuthStore();
    await store.initialize();

    expect(store.initialized).toBe(true);
    expect(store.isAuthenticated).toBe(false);
    expect(store.requiresInitialAdminSetup).toBe(false);
    expect(setCsrfToken).toHaveBeenCalledWith(null);
  });

  it('initialize flags initial admin setup when bootstrap status requires it', async () => {
    const store = useAuthStore();
    authApi.getBootstrapStatus.mockResolvedValue(ok(true));

    await store.initialize();

    expect(store.initialized).toBe(true);
    expect(store.isAuthenticated).toBe(false);
    expect(store.requiresInitialAdminSetup).toBe(true);
  });

  it('initialize restores session and csrf when /me returns a user', async () => {
    const store = useAuthStore();
    authApi.getMe.mockResolvedValue(ok<AuthUser | null>({ id: 1, userName: 'admin', role: 'Admin' }));

    await store.initialize();

    expect(store.initialized).toBe(true);
    expect(store.isAuthenticated).toBe(true);
    expect(store.isAdmin).toBe(true);
    expect(store.requiresInitialAdminSetup).toBe(false);
    expect(setCsrfToken).toHaveBeenCalledWith('csrf-token');
  });

  it('login stores user and csrf token on success', async () => {
    const store = useAuthStore();
    const session: AuthSession = {
      user: { id: 2, userName: 'member', role: 'Standard' },
      accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
      refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
      csrfToken: 'csrf-login'
    };
    authApi.login.mockResolvedValue(ok(session));

    const success = await store.login('member', 'Password1234!');

    expect(success).toBe(true);
    expect(store.user?.userName).toBe('member');
    expect(store.isAuthenticated).toBe(true);
    expect(store.requiresInitialAdminSetup).toBe(false);
    expect(setCsrfToken).toHaveBeenCalledWith('csrf-login');
  });

  it('registerInitialAdmin stores user and csrf token on success', async () => {
    const store = useAuthStore();
    const session: AuthSession = {
      user: { id: 1, userName: 'admin', role: 'Admin' },
      accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
      refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
      csrfToken: 'csrf-bootstrap'
    };
    authApi.registerInitialAdmin.mockResolvedValue(ok(session));

    const success = await store.registerInitialAdmin('admin', 'Password1234!');

    expect(success).toBe(true);
    expect(store.user?.userName).toBe('admin');
    expect(store.isAuthenticated).toBe(true);
    expect(store.isAdmin).toBe(true);
    expect(store.requiresInitialAdminSetup).toBe(false);
    expect(setCsrfToken).toHaveBeenCalledWith('csrf-bootstrap');
  });

  it('login exposes API error message on failure', async () => {
    const store = useAuthStore();
    const apiError: AppError = { kind: 'api', message: 'Invalid username or password.' };
    authApi.login.mockResolvedValue(err(apiError));

    const success = await store.login('member', 'bad');

    expect(success).toBe(false);
    expect(store.errorMessage).toBe('Invalid username or password.');
    expect(store.isAuthenticated).toBe(false);
  });

  it('registerInitialAdmin exposes API error message on failure', async () => {
    const store = useAuthStore();
    const apiError: AppError = { kind: 'api', message: 'Initial admin already exists.' };
    authApi.registerInitialAdmin.mockResolvedValue(err(apiError));

    const success = await store.registerInitialAdmin('admin', 'Password1234!');

    expect(success).toBe(false);
    expect(store.errorMessage).toBe('Initial admin already exists.');
    expect(store.isAuthenticated).toBe(false);
  });

  it('logout clears local session even when API succeeds', async () => {
    const store = useAuthStore();
    authApi.login.mockResolvedValue(
      ok<AuthSession>({
        user: { id: 1, userName: 'admin', role: 'Admin' },
        accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
        refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
        csrfToken: 'csrf-login'
      })
    );
    await store.login('admin', 'Password1234!');

    await store.logout();

    expect(store.user).toBeNull();
    expect(store.isAuthenticated).toBe(false);
    expect(setCsrfToken).toHaveBeenLastCalledWith(null);
  });

  it('changeOwnPassword clears local session on success', async () => {
    const store = useAuthStore();
    authApi.login.mockResolvedValue(
      ok<AuthSession>({
        user: { id: 1, userName: 'admin', role: 'Admin' },
        accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
        refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
        csrfToken: 'csrf-login'
      })
    );
    authApi.changeOwnPassword.mockResolvedValue(ok(undefined));
    await store.login('admin', 'Password1234!');

    const success = await store.changeOwnPassword('Password1234!', 'BetterPassword1234!');

    expect(success).toBe(true);
    expect(store.user).toBeNull();
    expect(setCsrfToken).toHaveBeenLastCalledWith(null);
  });

  it('changeOwnPassword exposes API error message on failure', async () => {
    const store = useAuthStore();
    const apiError: AppError = { kind: 'api', message: 'Current password is incorrect.' };
    authApi.changeOwnPassword.mockResolvedValue(err(apiError));

    const success = await store.changeOwnPassword('bad', 'BetterPassword1234!');

    expect(success).toBe(false);
    expect(store.errorMessage).toBe('Current password is incorrect.');
  });

  it('handleUnauthorized clears local session immediately', async () => {
    const store = useAuthStore();
    authApi.login.mockResolvedValue(
      ok<AuthSession>({
        user: { id: 1, userName: 'admin', role: 'Admin' },
        accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
        refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
        csrfToken: 'csrf-login'
      })
    );
    await store.login('admin', 'Password1234!');

    store.handleUnauthorized();

    expect(store.user).toBeNull();
    expect(store.isAuthenticated).toBe(false);
    expect(setCsrfToken).toHaveBeenLastCalledWith(null);
  });

  it('registers unauthorized handler that clears session and routes to unauthorized page', async () => {
    const store = useAuthStore();
    authApi.login.mockResolvedValue(
      ok<AuthSession>({
        user: { id: 1, userName: 'admin', role: 'Admin' },
        accessTokenExpiresAtUtc: '2026-03-16T20:00:00Z',
        refreshTokenExpiresAtUtc: '2026-03-17T20:00:00Z',
        csrfToken: 'csrf-login'
      })
    );

    await store.login('admin', 'Password1234!');
    const registered = setUnauthorizedHandler.mock.calls[setUnauthorizedHandler.mock.calls.length - 1]?.[0] as
      | (() => Promise<void>)
      | undefined;
    expect(typeof registered).toBe('function');

    await registered?.();

    expect(store.user).toBeNull();
    expect(store.isAuthenticated).toBe(false);
    expect(router.replace).toHaveBeenCalledWith({ name: 'unauthorized' });
  });
});

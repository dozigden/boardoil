import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from './authStore';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { AuthSession, AuthUser } from '../types/authTypes';

const authApi = {
  login: vi.fn(),
  logout: vi.fn(),
  getMe: vi.fn(),
  getCsrfToken: vi.fn()
};

const setCsrfToken = vi.fn();

vi.mock('../api/authApi', () => ({
  createAuthApi: () => authApi
}));

vi.mock('../api/http', () => ({
  setCsrfToken: (token: string | null) => setCsrfToken(token)
}));

describe('authStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    authApi.getMe.mockResolvedValue(ok<AuthUser | null>(null));
    authApi.getCsrfToken.mockResolvedValue(ok('csrf-token'));
    authApi.logout.mockResolvedValue(ok(undefined));
  });

  it('initialize keeps anonymous state when /me returns no user', async () => {
    const store = useAuthStore();
    await store.initialize();

    expect(store.initialized).toBe(true);
    expect(store.isAuthenticated).toBe(false);
    expect(setCsrfToken).toHaveBeenCalledWith(null);
  });

  it('initialize restores session and csrf when /me returns a user', async () => {
    const store = useAuthStore();
    authApi.getMe.mockResolvedValue(ok<AuthUser | null>({ id: 1, userName: 'admin', role: 'Admin' }));

    await store.initialize();

    expect(store.initialized).toBe(true);
    expect(store.isAuthenticated).toBe(true);
    expect(store.isAdmin).toBe(true);
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
    expect(setCsrfToken).toHaveBeenCalledWith('csrf-login');
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
});

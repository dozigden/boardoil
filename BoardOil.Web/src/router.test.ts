import { describe, expect, it, vi } from 'vitest';
import { resolveAuthNavigation } from './auth/navigationGuard';

function makeTarget(options?: { name?: string; requiresAuth?: boolean; requiresAdmin?: boolean }) {
  return {
    name: options?.name,
    matched: [
      {
        meta: {
          requiresAuth: options?.requiresAuth,
          requiresAdmin: options?.requiresAdmin
        }
      }
    ]
  };
}

function makeAuthStore(overrides?: Partial<{
  initialized: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  requiresInitialAdminSetup: boolean;
  initialize: () => Promise<void>;
}>) {
  return {
    initialized: true,
    isAuthenticated: false,
    isAdmin: false,
    requiresInitialAdminSetup: false,
    initialize: vi.fn(async () => undefined),
    ...overrides
  };
}

describe('resolveAuthNavigation', () => {
  it('initializes auth store when needed before evaluating route', async () => {
    const authStore = makeAuthStore({ initialized: false });
    const to = makeTarget({ name: 'board', requiresAuth: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(authStore.initialize).toHaveBeenCalledTimes(1);
    expect(result).toEqual({ name: 'login' });
  });

  it('redirects authenticated users away from login', async () => {
    const authStore = makeAuthStore({ isAuthenticated: true });
    const to = makeTarget({ name: 'login', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'board' });
  });

  it('redirects authenticated users away from initial admin setup', async () => {
    const authStore = makeAuthStore({ isAuthenticated: true });
    const to = makeTarget({ name: 'setup-initial-admin', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'board' });
  });

  it('redirects anonymous users from protected routes to login', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false });
    const to = makeTarget({ name: 'board', requiresAuth: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'login' });
  });

  it('redirects anonymous users to setup when initial admin setup is required', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false, requiresInitialAdminSetup: true });
    const to = makeTarget({ name: 'board', requiresAuth: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'setup-initial-admin' });
  });

  it('redirects anonymous users away from login when initial admin setup is required', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false, requiresInitialAdminSetup: true });
    const to = makeTarget({ name: 'login', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'setup-initial-admin' });
  });

  it('redirects anonymous users away from setup when setup is not required', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false, requiresInitialAdminSetup: false });
    const to = makeTarget({ name: 'setup-initial-admin', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'login' });
  });

  it('allows anonymous users into public routes', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false, requiresInitialAdminSetup: false });
    const to = makeTarget({ name: 'licences', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toBe(true);
  });

  it('allows anonymous users into public routes even when initial admin setup is required', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false, requiresInitialAdminSetup: true });
    const to = makeTarget({ name: 'licences', requiresAuth: false });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toBe(true);
  });

  it('redirects non-admin users away from admin routes', async () => {
    const authStore = makeAuthStore({ isAuthenticated: true, isAdmin: false });
    const to = makeTarget({ name: 'users', requiresAuth: true, requiresAdmin: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'board' });
  });

  it('allows admin users into admin routes', async () => {
    const authStore = makeAuthStore({ isAuthenticated: true, isAdmin: true });
    const to = makeTarget({ name: 'users', requiresAuth: true, requiresAdmin: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toBe(true);
  });
});

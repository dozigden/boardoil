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
  initialize: () => Promise<void>;
}>) {
  return {
    initialized: true,
    isAuthenticated: false,
    isAdmin: false,
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

  it('redirects anonymous users from protected routes to login', async () => {
    const authStore = makeAuthStore({ isAuthenticated: false });
    const to = makeTarget({ name: 'board', requiresAuth: true });

    const result = await resolveAuthNavigation(to, authStore);

    expect(result).toEqual({ name: 'login' });
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

import { createPinia, setActivePinia } from 'pinia';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createMemoryHistory, createRouter } from 'vue-router';
import { createAuthNavigation } from './authNavigation';
import { resolveAuthNavigation } from './navigationGuard';

describe('auth bootstrap navigation', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('allows a fresh anonymous login navigation to settle on the login route', async () => {
    vi.resetModules();
    vi.clearAllMocks();
    vi.stubGlobal('window', {
      location: {
        origin: 'http://localhost:5173'
      }
    });
    vi.stubGlobal('fetch', vi.fn());

    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ message: 'Unauthorized' }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      ))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({
          success: true,
          data: { requiresInitialAdminSetup: false },
          statusCode: 200
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      ));

    setActivePinia(createPinia());
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/login', name: 'login', component: {}, meta: { requiresAuth: false } },
        { path: '/unauthorized', name: 'unauthorized', component: {}, meta: { requiresAuth: false } }
      ]
    });
    const replace = vi.spyOn(router, 'replace');
    const { configureAuthNavigation, useAuthStore } = await import('../../shared/stores/authStore');
    configureAuthNavigation(createAuthNavigation(router));
    const authStore = useAuthStore();
    router.beforeEach(to => resolveAuthNavigation(to, authStore));
    await router.push('/login');
    await Promise.resolve();

    expect(router.currentRoute.value.name).toBe('login');
    expect(authStore.initialized).toBe(true);
    expect(authStore.isAuthenticated).toBe(false);
    expect(fetchMock.mock.calls.map(call => call[0])).toEqual([
      'http://localhost:5173/api/auth/me',
      'http://localhost:5173/api/auth/refresh',
      'http://localhost:5173/api/auth/bootstrap-status'
    ]);
    expect(replace).not.toHaveBeenCalled();
  });
});

import { createPinia, setActivePinia } from 'pinia';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { resolveAuthNavigation } from './navigationGuard';

const { router } = vi.hoisted(() => ({
  router: {
    currentRoute: { value: { name: undefined as string | undefined, fullPath: '/' } },
    replace: vi.fn(async () => undefined)
  }
}));

vi.mock('../../router', () => ({
  router
}));

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
    const { useAuthStore } = await import('../../shared/stores/authStore');
    const authStore = useAuthStore();

    const result = await resolveAuthNavigation({
      name: 'login',
      fullPath: '/login',
      matched: [{ meta: { requiresAuth: false } }]
    }, authStore);
    await Promise.resolve();

    expect(result).toBe(true);
    expect(authStore.initialized).toBe(true);
    expect(authStore.isAuthenticated).toBe(false);
    expect(fetchMock.mock.calls.map(call => call[0])).toEqual([
      'http://localhost:5173/api/auth/me',
      'http://localhost:5173/api/auth/refresh',
      'http://localhost:5173/api/auth/bootstrap-status'
    ]);
    expect(router.replace).not.toHaveBeenCalled();
  });
});

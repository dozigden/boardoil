import { describe, expect, it } from 'vitest';
import { createMemoryHistory, createRouter } from 'vue-router';
import { createAuthNavigation } from './authNavigation';

function createTestRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'boards', component: {} },
      { path: '/boards/:boardId/card/:cardId', name: 'board-card', component: {} },
      { path: '/login', name: 'login', component: {} },
      { path: '/setup-initial-admin', name: 'setup-initial-admin', component: {} },
      { path: '/unauthorized', name: 'unauthorized', component: {} }
    ]
  });
}

describe('auth navigation', () => {
  it('preserves the current destination including its query and fragment', async () => {
    const router = createTestRouter();
    const navigation = createAuthNavigation(router);
    await router.push('/boards/3/card/12?mode=edit#comments');

    await navigation.onUnauthorized();

    expect(router.currentRoute.value.name).toBe('unauthorized');
    expect(router.currentRoute.value.query).toEqual({ redirect: '/boards/3/card/12?mode=edit#comments' });
  });

  it.each(['/login?redirect=/boards/3/card/12', '/setup-initial-admin', '/unauthorized?redirect=/'])(
    'keeps an unauthorised response on %s from redirecting again', async path => {
      const router = createTestRouter();
      await router.push(path);
      const currentRoute = router.currentRoute.value;

      await createAuthNavigation(router).onUnauthorized();

      expect(router.currentRoute.value).toBe(currentRoute);
    }
  );

  it('returns to login when admin creation succeeds but sign-in fails', async () => {
    const router = createTestRouter();
    await router.push('/setup-initial-admin');

    await createAuthNavigation(router).onInitialAdminSignInFailed();

    expect(router.currentRoute.value.name).toBe('login');
  });
});

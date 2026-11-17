import type { Router } from 'vue-router';
import type { AuthNavigation } from '../../shared/stores/authStore';
import { buildLoginRedirectQuery } from './redirectTarget';

export function createAuthNavigation(router: Router): AuthNavigation {
  return {
    async onUnauthorized() {
      const { name, fullPath } = router.currentRoute.value;
      if (name !== 'unauthorized' && name !== 'setup-initial-admin' && name !== 'login') {
        await router.replace({ name: 'unauthorized', query: buildLoginRedirectQuery(fullPath) });
      }
    },
    async onInitialAdminSignInFailed() {
      await router.replace({ name: 'login' });
    }
  };
}

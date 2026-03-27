export type GuardTarget = {
  name?: string | symbol | null;
  matched: Array<{ meta: Record<string, unknown> }>;
};

export type GuardAuthStore = {
  initialized: boolean;
  isAuthenticated: boolean;
  isAdmin: boolean;
  requiresInitialAdminSetup: boolean;
  initialize: () => Promise<void>;
};

export async function resolveAuthNavigation(to: GuardTarget, authStore: GuardAuthStore) {
  if (!authStore.initialized) {
    await authStore.initialize();
  }

  const requiresAuth = to.matched.some(record => record.meta.requiresAuth !== false);

  if ((to.name === 'login' || to.name === 'setup-initial-admin') && authStore.isAuthenticated) {
    return { name: 'board' };
  }

  const isSetupRoute = to.name === 'setup-initial-admin';
  const isLoginRoute = to.name === 'login';
  const shouldForceSetupRoute = requiresAuth || isLoginRoute;
  if (
    !authStore.isAuthenticated &&
    authStore.requiresInitialAdminSetup &&
    !isSetupRoute &&
    shouldForceSetupRoute
  ) {
    return { name: 'setup-initial-admin' };
  }

  if (!authStore.isAuthenticated && !authStore.requiresInitialAdminSetup && isSetupRoute) {
    return { name: 'login' };
  }

  if (requiresAuth && !authStore.isAuthenticated) {
    return { name: 'login' };
  }

  const requiresAdmin = to.matched.some(record => record.meta.requiresAdmin === true);
  if (requiresAdmin && !authStore.isAdmin) {
    return { name: 'board' };
  }

  return true;
}

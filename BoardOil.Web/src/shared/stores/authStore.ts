import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createAuthApi } from '../api/authApi';
import { setCsrfToken, setUnauthorizedHandler } from '../api/http';
import { router } from '../../router';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { AuthUser } from '../types/authTypes';
import { buildLoginRedirectQuery } from '../../site/auth/redirectTarget';

export const useAuthStore = defineStore('auth', () => {
  const api = createAuthApi();
  const user = ref<AuthUser | null>(null);
  const busy = ref(false);
  const initialized = ref(false);
  const errorMessage = ref<string | null>(null);
  const requiresInitialAdminSetup = ref(false);

  const isAuthenticated = computed(() => user.value !== null);
  const isAdmin = computed(() => user.value?.role === 'Admin');
  const experimentalFeaturesEnabled = computed(() => user.value?.experimentalFeaturesEnabled === true);
  let initializePromise: Promise<void> | null = null;

  function SET_SESSION(authenticatedUser: AuthUser, csrfToken: string) {
    setCsrfToken(csrfToken);
    user.value = authenticatedUser;
    errorMessage.value = null;
    initialized.value = true;
  }

  function CLEAR_SESSION() {
    user.value = null;
    setCsrfToken(null);
  }

  function UPDATE_OWN_PROFILE(displayName: string, userName: string, role: string, experimentalFeaturesEnabled: boolean) {
    if (!user.value) {
      return;
    }

    user.value = {
      ...user.value,
      userName,
      displayName,
      role,
      experimentalFeaturesEnabled
    };
  }

  setUnauthorizedHandler(async () => {
    const currentPath = router.currentRoute.value.fullPath;
    handleUnauthorized();

    const routeName = router.currentRoute.value.name;
    if (routeName !== 'unauthorized' && routeName !== 'setup-initial-admin' && routeName !== 'login') {
      await router.replace({ name: 'unauthorized', query: buildLoginRedirectQuery(currentPath) });
    }
  });

  function initialize() {
    if (initialized.value) {
      return Promise.resolve();
    }

    initializePromise ??= initializeSession();
    return initializePromise;
  }

  async function initializeSession() {
    busy.value = true;
    try {
      const meResult = await api.getMe();
      if (!meResult.ok || !meResult.data) {
        const bootstrapStatusResult = await api.getBootstrapStatus();
        requiresInitialAdminSetup.value = bootstrapStatusResult.ok && bootstrapStatusResult.data;
        CLEAR_SESSION();
        initialized.value = true;
        return;
      }

      const restored = await restoreSession(meResult.data);
      if (!restored.ok) errorMessage.value = restored.error.message;
      initialized.value = true;
    } finally {
      busy.value = false;
      initializePromise = null;
    }
  }

  async function login(userName: string, password: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.login(userName, password);
      if (!result.ok) return result;

      return await restoreSession(result.data.user);
    } finally {
      busy.value = false;
    }
  }

  async function registerInitialAdmin(userName: string, email: string, password: string, systemTimeZoneId: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.registerInitialAdmin(userName, email, password, systemTimeZoneId);
      if (!result.ok) return result;

      const restored = await restoreSession(result.data.user);
      if (!restored.ok) {
        const message = `Your admin account was created, but sign-in could not be completed. Please sign in. ${restored.error.message}`;
        errorMessage.value = message;
        // Setup succeeded. Do not let navigation retry restoration or offer
        // another registration after a failed follow-up token request.
        initialized.value = true;
        await router.replace({ name: 'login' });
        return err({ ...restored.error, message });
      }
      return restored;
    } finally {
      busy.value = false;
    }
  }

  async function restoreSession(authenticatedUser: AuthUser) {
    CLEAR_SESSION();
    requiresInitialAdminSetup.value = false;
    const csrfResult = await api.getCsrfToken();
    if (!csrfResult.ok) return csrfResult;

    if (csrfResult.data.userId !== authenticatedUser.id) {
      return err<AppError>({ kind: 'api', message: 'The signed-in account changed. Please sign in again.' });
    }

    SET_SESSION(authenticatedUser, csrfResult.data.csrfToken);
    return ok(authenticatedUser);
  }

  async function changeOwnPassword(currentPassword: string, newPassword: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.changeOwnPassword(currentPassword, newPassword);
      if (result.ok) {
        CLEAR_SESSION();
      }
      return result;
    } finally {
      busy.value = false;
    }
  }

  async function logout() {
    busy.value = true;
    try {
      await api.logout();
    } finally {
      CLEAR_SESSION();
      busy.value = false;
    }
  }

  function handleUnauthorized() {
    CLEAR_SESSION();
  }

  function setOwnProfile(displayName: string, userName: string, role: string, experimentalFeaturesEnabled: boolean) {
    UPDATE_OWN_PROFILE(displayName, userName, role, experimentalFeaturesEnabled);
  }

  return {
    user,
    busy,
    initialized,
    errorMessage,
    requiresInitialAdminSetup,
    isAuthenticated,
    isAdmin,
    experimentalFeaturesEnabled,
    initialize,
    login,
    registerInitialAdmin,
    changeOwnPassword,
    logout,
    setOwnProfile,
    handleUnauthorized
  };
});

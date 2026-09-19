import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createAuthApi } from '../api/authApi';
import { setCsrfToken, setUnauthorizedHandler } from '../api/http';
import { router } from '../../router';
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

  setUnauthorizedHandler(async () => {
    const currentPath = router.currentRoute.value.fullPath;
    handleUnauthorized();

    const routeName = router.currentRoute.value.name;
    if (routeName !== 'unauthorized' && routeName !== 'setup-initial-admin' && routeName !== 'login') {
      await router.replace({ name: 'unauthorized', query: buildLoginRedirectQuery(currentPath) });
    }
  });

  async function initialize() {
    if (initialized.value) {
      return;
    }

    busy.value = true;
    try {
      const meResult = await api.getMe();
      if (!meResult.ok || !meResult.data) {
        const bootstrapStatusResult = await api.getBootstrapStatus();
        requiresInitialAdminSetup.value = bootstrapStatusResult.ok && bootstrapStatusResult.data;
        clearSession();
        initialized.value = true;
        return;
      }

      await restoreSession(meResult.data);
      initialized.value = true;
    } finally {
      busy.value = false;
    }
  }

  async function login(userName: string, password: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.login(userName, password);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      return await restoreSession(result.data.user);
    } finally {
      busy.value = false;
    }
  }

  async function registerInitialAdmin(userName: string, email: string, password: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.registerInitialAdmin(userName, email, password);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      const restored = await restoreSession(result.data.user);
      if (!restored) {
        errorMessage.value = `Your admin account was created, but sign-in could not be completed. Please sign in. ${errorMessage.value}`;
        // Setup succeeded. Do not let navigation retry restoration or offer
        // another registration after a failed follow-up token request.
        initialized.value = true;
        await router.replace({ name: 'login' });
      }
      return restored;
    } finally {
      busy.value = false;
    }
  }

  async function restoreSession(authenticatedUser: AuthUser) {
    clearSession();
    requiresInitialAdminSetup.value = false;
    const csrfResult = await api.getCsrfToken();
    if (!csrfResult.ok) {
      errorMessage.value = csrfResult.error.message;
      return false;
    }

    if (csrfResult.data.userId !== authenticatedUser.id) {
      errorMessage.value = 'The signed-in account changed. Please sign in again.';
      return false;
    }

    setCsrfToken(csrfResult.data.csrfToken);
    user.value = authenticatedUser;
    errorMessage.value = null;
    initialized.value = true;
    return true;
  }

  async function changeOwnPassword(currentPassword: string, newPassword: string) {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.changeOwnPassword(currentPassword, newPassword);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      clearSession();
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function logout() {
    busy.value = true;
    try {
      await api.logout();
    } finally {
      clearSession();
      busy.value = false;
    }
  }

  function handleUnauthorized() {
    clearSession();
  }

  function clearSession() {
    user.value = null;
    setCsrfToken(null);
  }

  function setOwnProfile(displayName: string, userName: string, role: string) {
    if (!user.value) {
      return;
    }

    user.value = {
      ...user.value,
      userName,
      displayName,
      role
    };
  }

  return {
    user,
    busy,
    initialized,
    errorMessage,
    requiresInitialAdminSetup,
    isAuthenticated,
    isAdmin,
    initialize,
    login,
    registerInitialAdmin,
    changeOwnPassword,
    logout,
    setOwnProfile,
    handleUnauthorized
  };
});

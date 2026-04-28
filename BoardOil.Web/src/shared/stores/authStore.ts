import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createAuthApi } from '../api/authApi';
import { setCsrfToken, setUnauthorizedHandler } from '../api/http';
import { router } from '../../router';
import type { AuthUser } from '../types/authTypes';

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
    handleUnauthorized();

    const routeName = router.currentRoute.value.name;
    if (routeName !== 'unauthorized' && routeName !== 'setup-initial-admin') {
      await router.replace({ name: 'unauthorized' });
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

      user.value = meResult.data;
      requiresInitialAdminSetup.value = false;
      const csrfResult = await api.getCsrfToken();
      if (csrfResult.ok) {
        setCsrfToken(csrfResult.data);
      }
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

      user.value = result.data.user;
      requiresInitialAdminSetup.value = false;
      setCsrfToken(result.data.csrfToken);
      initialized.value = true;
      return true;
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

      user.value = result.data.user;
      requiresInitialAdminSetup.value = false;
      setCsrfToken(result.data.csrfToken);
      initialized.value = true;
      return true;
    } finally {
      busy.value = false;
    }
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
    handleUnauthorized
  };
});

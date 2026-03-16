import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createAuthApi } from '../api/authApi';
import { setCsrfToken } from '../api/http';
import type { AuthUser } from '../types/authTypes';

export const useAuthStore = defineStore('auth', () => {
  const api = createAuthApi();
  const user = ref<AuthUser | null>(null);
  const busy = ref(false);
  const initialized = ref(false);
  const errorMessage = ref<string | null>(null);

  const isAuthenticated = computed(() => user.value !== null);
  const isAdmin = computed(() => user.value?.role === 'Admin');

  async function initialize() {
    if (initialized.value) {
      return;
    }

    busy.value = true;
    try {
      const meResult = await api.getMe();
      if (!meResult.ok || !meResult.data) {
        clearSession();
        initialized.value = true;
        return;
      }

      user.value = meResult.data;
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
      setCsrfToken(result.data.csrfToken);
      initialized.value = true;
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

  function clearSession() {
    user.value = null;
    setCsrfToken(null);
  }

  return {
    user,
    busy,
    initialized,
    errorMessage,
    isAuthenticated,
    isAdmin,
    initialize,
    login,
    logout
  };
});

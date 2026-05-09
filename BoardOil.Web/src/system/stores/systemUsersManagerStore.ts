import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import type { ManagedUser } from '../../shared/types/authTypes';

export const useSystemUsersManagerStore = defineStore('systemUsersManager', () => {
  const users = ref<ManagedUser[]>([]);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const successMessage = ref<string | null>(null);
  const api = createSystemApi();

  function clearMessages() {
    errorMessage.value = null;
    successMessage.value = null;
  }

  function dispose() {
    users.value = [];
    busy.value = false;
    clearMessages();
  }

  async function loadUsers() {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.getUsers();
      if (!result.ok) {
        errorMessage.value = result.error.message;
        users.value = [];
        return false;
      }

      users.value = result.data;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function createUser(payload: {
    userName: string;
    displayName: string;
    email: string;
    password: string;
    role: 'Admin' | 'Standard';
  }) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.createUser(payload.userName, payload.displayName, payload.email, payload.password, payload.role);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      users.value = [...users.value, result.data].sort((left, right) => left.userName.localeCompare(right.userName));
      successMessage.value = `Created user ${result.data.displayName}.`;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function updateUser(
    userId: number,
    payload: { displayName: string; email: string; role: 'Admin' | 'Standard'; isActive: boolean }
  ) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.updateUser(userId, payload);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      users.value = users.value.map(user => (user.id === userId ? result.data : user));
      successMessage.value = `Updated ${result.data.displayName}.`;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function resetUserPassword(userId: number, displayName: string, newPassword: string) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.resetUserPassword(userId, newPassword);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      successMessage.value = `Password reset for ${displayName}.`;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function deleteUser(userId: number, displayName: string) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.deleteUser(userId);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return false;
      }

      users.value = users.value.filter(entry => entry.id !== userId);
      successMessage.value = `Deleted user ${displayName}.`;
      return true;
    } finally {
      busy.value = false;
    }
  }

  return {
    users,
    busy,
    errorMessage,
    successMessage,
    clearMessages,
    dispose,
    loadUsers,
    createUser,
    updateUser,
    resetUserPassword,
    deleteUser
  };
});

import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type {
  CreateManagedUserRequest,
  ManagedUser,
  UpdateManagedUserRequest
} from '../../shared/types/authTypes';

export const useSystemUsersManagerStore = defineStore('systemUsersManager', () => {
  const users = ref<ManagedUser[]>([]);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const api = createSystemApi();
  const feedback = useUiFeedbackStore();

  function clearMessages() {
    errorMessage.value = null;
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

  async function createUser(payload: CreateManagedUserRequest) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.createUser(payload);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      users.value = [...users.value, result.data].sort((left, right) => left.userName.localeCompare(right.userName));
      feedback.showToast('Created successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function updateUser(userId: number, payload: UpdateManagedUserRequest) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.updateUser(userId, payload);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      users.value = users.value.map(user => (user.id === userId ? result.data : user));
      feedback.showToast('Saved successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function resetUserPassword(userId: number, newPassword: string) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.resetUserPassword(userId, newPassword);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      feedback.showToast('Password reset successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function deleteUser(userId: number) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.deleteUser(userId);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      users.value = users.value.filter(entry => entry.id !== userId);
      feedback.showToast('Deleted successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  return {
    users,
    busy,
    errorMessage,
    clearMessages,
    dispose,
    loadUsers,
    createUser,
    updateUser,
    resetUserPassword,
    deleteUser
  };
});

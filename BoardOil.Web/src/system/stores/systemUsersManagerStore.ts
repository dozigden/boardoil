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

  function SET_USERS(nextUsers: ManagedUser[]) {
    users.value = nextUsers;
  }

  function ADD_USER(user: ManagedUser) {
    SET_USERS([...users.value, user].sort((left, right) => left.userName.localeCompare(right.userName)));
  }

  function UPDATE_USER(userId: number, updatedUser: ManagedUser) {
    SET_USERS(users.value.map(user => (user.id === userId ? updatedUser : user)));
  }

  function REMOVE_USER(userId: number) {
    SET_USERS(users.value.filter(user => user.id !== userId));
  }

  function clearMessages() {
    errorMessage.value = null;
  }

  function dispose() {
    SET_USERS([]);
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
        SET_USERS([]);
        return false;
      }

      SET_USERS(result.data);
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

      ADD_USER(result.data);
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

      UPDATE_USER(userId, result.data);
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

      REMOVE_USER(userId);
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

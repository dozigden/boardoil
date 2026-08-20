import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type {
  ClientAccount,
  CreateClientAccountRequest,
  CreatedClientAccount,
  UpdateClientAccountRequest,
  UserProfileImage
} from '../../shared/types/authTypes';

export const useSystemClientAccountsStore = defineStore('systemClientAccounts', () => {
  const clients = ref<ClientAccount[]>([]);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const api = createSystemApi();
  const feedback = useUiFeedbackStore();

  function clearMessages() {
    errorMessage.value = null;
  }

  function dispose() {
    clients.value = [];
    busy.value = false;
    clearMessages();
  }

  async function loadClients() {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.getClientAccounts();
      if (!result.ok) {
        errorMessage.value = result.error.message;
        clients.value = [];
        return false;
      }

      clients.value = result.data;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function createClientAccount(payload: CreateClientAccountRequest): Promise<CreatedClientAccount | null> {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.createClientAccount(payload);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return null;
      }

      clients.value = [...clients.value, result.data.account].sort((left, right) => left.userName.localeCompare(right.userName));
      feedback.showToast('Created successfully.');
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  async function updateClientAccount(clientId: number, payload: UpdateClientAccountRequest): Promise<ClientAccount | null> {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.updateClientAccount(clientId, payload);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return null;
      }

      clients.value = clients.value.map(client => (client.id === clientId ? result.data : client));
      feedback.showToast('Saved successfully.');
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  async function deleteClientAccount(clientId: number) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.deleteClientAccount(clientId);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      clients.value = clients.value.filter(entry => entry.id !== clientId);
      feedback.showToast('Deleted successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function uploadClientProfileImage(clientId: number, file: File): Promise<UserProfileImage | null> {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.uploadClientAccountProfileImage(clientId, file);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return null;
      }

      clients.value = clients.value.map(client =>
        client.id === clientId
          ? { ...client, profileImageRelativePath: result.data.relativePath }
          : client
      );
      feedback.showToast('Saved successfully.');
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  async function removeClientProfileImage(clientId: number) {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.deleteClientAccountProfileImage(clientId);
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return false;
      }

      clients.value = clients.value.map(entry =>
        entry.id === clientId
          ? { ...entry, profileImageRelativePath: null }
          : entry
      );
      feedback.showToast('Removed successfully.');
      return true;
    } finally {
      busy.value = false;
    }
  }

  return {
    clients,
    busy,
    errorMessage,
    clearMessages,
    dispose,
    loadClients,
    createClientAccount,
    updateClientAccount,
    deleteClientAccount,
    uploadClientProfileImage,
    removeClientProfileImage
  };
});

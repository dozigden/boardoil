import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../api/systemApi';
import type { SystemInfoMessageDto } from '../types/configurationTypes';

export const useSystemInfoMessageStore = defineStore('systemInfoMessage', () => {
  const api = createSystemApi();
  const message = ref<SystemInfoMessageDto | null>(null);
  const loaded = ref(false);
  const busy = ref(false);
  const saving = ref(false);

  function SET_MESSAGE(nextMessage: SystemInfoMessageDto | null) {
    message.value = nextMessage;
    loaded.value = true;
  }

  function CLEAR_MESSAGE() {
    message.value = null;
    loaded.value = false;
  }

  async function load(force = false) {
    if (loaded.value && !force) {
      return true;
    }

    busy.value = true;
    try {
      const result = await api.getSystemInfoMessage();
      if (!result.ok) {
        SET_MESSAGE(null);
        return false;
      }

      SET_MESSAGE(result.data);
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function save(nextMessage: SystemInfoMessageDto | null) {
    saving.value = true;
    try {
      const result = await api.updateSystemInfoMessage(nextMessage);
      if (result.ok) {
        SET_MESSAGE(result.data);
      }
      return result;
    } finally {
      saving.value = false;
    }
  }

  function setMessage(nextMessage: SystemInfoMessageDto | null) {
    SET_MESSAGE(nextMessage);
  }

  function clear() {
    CLEAR_MESSAGE();
  }

  return {
    message,
    loaded,
    busy,
    saving,
    load,
    save,
    setMessage,
    clear
  };
});

import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../api/systemApi';
import type { SystemInfoMessageDto } from '../types/configurationTypes';

export const useSystemInfoMessageStore = defineStore('systemInfoMessage', () => {
  const api = createSystemApi();
  const message = ref<SystemInfoMessageDto | null>(null);
  const loaded = ref(false);
  const busy = ref(false);

  async function load(force = false) {
    if (loaded.value && !force) {
      return true;
    }

    busy.value = true;
    try {
      const result = await api.getSystemInfoMessage();
      if (!result.ok) {
        message.value = null;
        loaded.value = true;
        return false;
      }

      message.value = result.data;
      loaded.value = true;
      return true;
    } finally {
      busy.value = false;
    }
  }

  function setMessage(nextMessage: SystemInfoMessageDto | null) {
    message.value = nextMessage;
    loaded.value = true;
  }

  function clear() {
    message.value = null;
    loaded.value = false;
  }

  return {
    message,
    loaded,
    busy,
    load,
    setMessage,
    clear
  };
});

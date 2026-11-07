import { defineStore } from 'pinia';
import { ref } from 'vue';
import { hasPendingWrites } from '../api/http';
import { getBackendBuildInfo, getFrontendBuildInfo } from '../api/versionApi';

const checkIntervalMs = 5 * 60 * 1000;
const checkTimeoutMs = 10 * 1000;

export const useAppUpdateStore = defineStore('appUpdate', () => {
  const updateAvailable = ref(false);
  const refreshMessage = ref('');
  let interval: ReturnType<typeof setInterval> | null = null;
  let activeCheck: AbortController | null = null;

  function SET_UPDATE_AVAILABLE(available: boolean) {
    updateAvailable.value = available;
  }

  function SET_REFRESH_MESSAGE(message: string) {
    refreshMessage.value = message;
  }

  async function check() {
    if (interval === null || document.visibilityState === 'hidden' || activeCheck) {
      return;
    }

    const controller = new AbortController();
    activeCheck = controller;
    const timeout = setTimeout(() => controller.abort(), checkTimeoutMs);
    try {
      const result = await getBackendBuildInfo(controller.signal);
      if (!result.ok || controller.signal.aborted) {
        return;
      }

      const frontend = getFrontendBuildInfo();
      const backend = result.data;
      SET_UPDATE_AVAILABLE(frontend.version !== backend.version
        || frontend.channel !== backend.channel
        || frontend.build !== backend.build
        || frontend.commit !== backend.commit);
    } finally {
      clearTimeout(timeout);
      if (activeCheck === controller) {
        activeCheck = null;
      }
    }
  }

  function onVisibilityChange() {
    void check();
  }

  function initialize() {
    if (interval !== null) {
      return;
    }

    interval = setInterval(() => { void check(); }, checkIntervalMs);
    document.addEventListener('visibilitychange', onVisibilityChange);
    void check();
  }

  function dispose() {
    if (interval !== null) {
      clearInterval(interval);
      interval = null;
    }
    document.removeEventListener('visibilitychange', onVisibilityChange);
    activeCheck?.abort();
    activeCheck = null;
    SET_UPDATE_AVAILABLE(false);
    SET_REFRESH_MESSAGE('');
  }

  function blockPendingWrites() {
    if (!hasPendingWrites()) {
      return false;
    }

    SET_REFRESH_MESSAGE('A save or upload is still in progress. Try again when it finishes.');
    return true;
  }

  function refresh() {
    if (!updateAvailable.value || interval === null) {
      return;
    }

    SET_REFRESH_MESSAGE('');
    if (blockPendingWrites()) {
      return;
    }

    window.location.reload();
  }

  return { updateAvailable, refreshMessage, initialize, dispose, refresh };
});

import { defineStore } from 'pinia';
import { ref } from 'vue';

export type UiToastTone = 'success' | 'error';

export const useUiFeedbackStore = defineStore('uiFeedback', () => {
  const errorMessage = ref('');
  const warningMessage = ref('');
  const toastMessage = ref('');
  const toastTone = ref<UiToastTone>('success');
  let toastTimeout: ReturnType<typeof setTimeout> | null = null;

  function setError(message: string) {
    errorMessage.value = message;
  }

  function clearError() {
    errorMessage.value = '';
  }

  function setWarning(message: string) {
    warningMessage.value = message;
  }

  function clearWarning() {
    warningMessage.value = '';
  }

  function showToast(message: string, tone: UiToastTone = 'success') {
    clearToastTimeout();
    toastMessage.value = message;
    toastTone.value = tone;
    toastTimeout = setTimeout(() => {
      toastMessage.value = '';
      toastTimeout = null;
    }, 3000);
  }

  function clearToast() {
    clearToastTimeout();
    toastMessage.value = '';
  }

  function clearToastTimeout() {
    if (toastTimeout === null) {
      return;
    }

    clearTimeout(toastTimeout);
    toastTimeout = null;
  }

  return {
    errorMessage,
    warningMessage,
    toastMessage,
    toastTone,
    setError,
    clearError,
    setWarning,
    clearWarning,
    showToast,
    clearToast
  };
});

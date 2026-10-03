import { defineStore } from 'pinia';
import { ref } from 'vue';

export type UiToastTone = 'success' | 'error';

export const useUiFeedbackStore = defineStore('uiFeedback', () => {
  const errorMessage = ref('');
  const errorSource = ref<string | null>(null);
  const warningMessage = ref('');
  const toastMessage = ref('');
  const toastTone = ref<UiToastTone>('success');
  let toastTimeout: ReturnType<typeof setTimeout> | null = null;

  function SET_ERROR(message: string, source: string | null) {
    errorSource.value = source;
    errorMessage.value = message;
  }

  function SET_WARNING(message: string) {
    warningMessage.value = message;
  }

  function SET_TOAST(message: string, tone: UiToastTone) {
    toastMessage.value = message;
    toastTone.value = tone;
  }

  function CLEAR_TOAST() {
    toastMessage.value = '';
  }

  function setError(message: string, source: string) {
    SET_ERROR(message, source);
  }

  function clearError(source: string) {
    if (errorSource.value === source) {
      resetError();
    }
  }

  function resetError() {
    SET_ERROR('', null);
  }

  function setWarning(message: string) {
    SET_WARNING(message);
  }

  function clearWarning() {
    SET_WARNING('');
  }

  function showToast(message: string, tone: UiToastTone = 'success') {
    clearToastTimeout();
    SET_TOAST(message, tone);
    toastTimeout = setTimeout(() => {
      CLEAR_TOAST();
      toastTimeout = null;
    }, 3000);
  }

  function clearToast() {
    clearToastTimeout();
    CLEAR_TOAST();
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
    errorSource,
    warningMessage,
    toastMessage,
    toastTone,
    setError,
    clearError,
    resetError,
    setWarning,
    clearWarning,
    showToast,
    clearToast
  };
});

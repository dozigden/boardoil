import { defineStore } from 'pinia';
import { ref } from 'vue';

export type UiToastTone = 'success' | 'error';

export const useUiFeedbackStore = defineStore('uiFeedback', () => {
  const errorMessage = ref('');
  const warningMessage = ref('');
  const toastMessage = ref('');
  const toastTone = ref<UiToastTone>('success');
  let toastTimeout: ReturnType<typeof setTimeout> | null = null;

  function SET_ERROR(message: string) {
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

  function setError(message: string) {
    SET_ERROR(message);
  }

  function clearError() {
    SET_ERROR('');
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

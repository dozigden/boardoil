import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useUiFeedbackStore = defineStore('uiFeedback', () => {
  const errorMessage = ref('');
  const warningMessage = ref('');

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

  return {
    errorMessage,
    warningMessage,
    setError,
    clearError,
    setWarning,
    clearWarning
  };
});

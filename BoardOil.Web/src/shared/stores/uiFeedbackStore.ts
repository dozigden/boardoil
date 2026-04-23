import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useUiFeedbackStore = defineStore('uiFeedback', () => {
  const errorMessage = ref('');

  function setError(message: string) {
    errorMessage.value = message;
  }

  function clearError() {
    errorMessage.value = '';
  }

  return {
    errorMessage,
    setError,
    clearError
  };
});

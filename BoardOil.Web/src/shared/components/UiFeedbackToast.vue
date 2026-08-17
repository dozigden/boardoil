<template>
  <aside
    v-if="displayMessage"
    class="ui-feedback-toast"
    :class="`ui-feedback-toast--${displayTone}`"
    :role="displayTone === 'error' ? 'alert' : 'status'"
    :aria-live="displayTone === 'error' ? 'assertive' : 'polite'"
  >
    <span class="ui-feedback-toast-icon" aria-hidden="true">{{ displayIcon }}</span>
    <p class="ui-feedback-toast-message">{{ displayMessage }}</p>
  </aside>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';

const feedbackStore = useUiFeedbackStore();
const { toastMessage, toastTone, warningMessage } = storeToRefs(feedbackStore);
const displayMessage = computed(() => toastMessage.value || warningMessage.value);
const displayTone = computed(() => toastMessage.value ? toastTone.value : 'warning');
const displayIcon = computed(() => {
  if (displayTone.value === 'success') {
    return '\u2713';
  }

  if (displayTone.value === 'error') {
    return '!';
  }

  return '\u26a0';
});
</script>

<style scoped>
.ui-feedback-toast {
  position: fixed;
  top: calc(0.75rem + env(safe-area-inset-top));
  right: calc(0.75rem + env(safe-area-inset-right));
  max-width: min(28rem, calc(100vw - 1.5rem));
  display: flex;
  align-items: center;
  gap: 0.65rem;
  margin: 0;
  padding: 0.65rem 0.75rem;
  border-radius: 0.65rem;
  box-shadow: var(--bo-toast-warning-shadow);
  z-index: 1200;
}

.ui-feedback-toast--warning {
  border: 1px solid var(--bo-toast-warning-border);
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
}

.ui-feedback-toast--success {
  border: 1px solid color-mix(in srgb, var(--bo-colour-success) 70%, var(--bo-border-soft));
  background: color-mix(in srgb, var(--bo-colour-success) 18%, var(--bo-surface-base));
  color: var(--bo-colour-success-ink);
}

.ui-feedback-toast--error {
  border: 1px solid color-mix(in srgb, var(--bo-colour-danger) 70%, var(--bo-border-soft));
  background: color-mix(in srgb, var(--bo-colour-danger) 18%, var(--bo-surface-base));
  color: var(--bo-colour-danger-ink);
}

.ui-feedback-toast-icon {
  font-size: 1rem;
  line-height: 1;
}

.ui-feedback-toast-message {
  margin: 0;
  font-weight: 600;
  font-size: 0.9rem;
  line-height: 1.3;
}

@media (max-width: 767px) {
  .ui-feedback-toast {
    top: calc(0.5rem + env(safe-area-inset-top));
    right: calc(0.5rem + env(safe-area-inset-right));
    left: calc(0.5rem + env(safe-area-inset-left));
    max-width: none;
  }
}
</style>

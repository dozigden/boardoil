<template>
  <aside v-if="warningMessage" class="ui-feedback-toast" role="status" aria-live="polite">
    <span class="ui-feedback-toast-icon" aria-hidden="true">&#9888;</span>
    <p class="ui-feedback-toast-message">{{ warningMessage }}</p>
  </aside>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';

const feedbackStore = useUiFeedbackStore();
const { warningMessage } = storeToRefs(feedbackStore);
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
  border: 1px solid var(--bo-toast-warning-border);
  background: var(--bo-toast-warning-bg);
  color: var(--bo-toast-warning-text);
  box-shadow: var(--bo-toast-warning-shadow);
  z-index: 1200;
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

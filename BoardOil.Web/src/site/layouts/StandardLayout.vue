<template>
  <section class="app-layout app-layout--standard app-layout-with-header">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-layout-standard-content">
      <slot />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import AppHeader from '../components/AppHeader.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);
</script>

<style scoped>
.app-layout--standard {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.app-layout-standard-content {
  flex: 1;
  min-height: 0;
  min-width: 0;
  padding-inline: 1rem;
  position: relative;
}

@media (min-width: 1024px) {
  .app-layout-standard-content {
    padding-inline: 1.5rem;
  }
}
</style>

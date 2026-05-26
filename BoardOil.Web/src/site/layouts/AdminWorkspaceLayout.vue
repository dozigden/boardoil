<template>
  <section class="app-layout app-layout--admin app-layout-with-header">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-layout-admin-content">
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
.app-layout--admin {
  height: 100vh;
  height: 100dvh;
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
  --bo-header-margin-active: 0;
  --bo-admin-scroll-inline-padding: 1rem;
  --bo-admin-scroll-block-start-padding: 1rem;
  --bo-admin-scroll-block-end-padding: 1rem;
}

.app-layout-admin-content {
  flex: 1;
  height: 0;
  min-height: 0;
  min-width: 0;
  display: flex;
  flex-direction: column;
  padding: 0;
  overflow: hidden;
  position: relative;
}

.app-layout-admin-content :deep(.admin-content > *) {
  margin-top: 0;
  max-width: none;
}

@media (max-width: 720px) {
  .app-layout--admin {
    --bo-admin-scroll-inline-padding: 0.75rem;
    --bo-admin-scroll-block-start-padding: 0.75rem;
    --bo-admin-scroll-block-end-padding: 0.75rem;
  }
}
</style>

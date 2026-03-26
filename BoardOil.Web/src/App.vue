<template>
  <main class="app-shell">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-content">
      <RouterView />
    </section>
    <RouterView name="dialog" />
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted, watch } from 'vue';
import { RouterView } from 'vue-router';
import AppHeader from './components/AppHeader.vue';
import { useBoardStore } from './stores/boardStore';
import { useTagStore } from './stores/tagStore';
import { useAuthStore } from './stores/authStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';

const boardStore = useBoardStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);

onMounted(async () => {
  await authStore.initialize();
  if (authStore.isAuthenticated) {
    await boardStore.initialize();
    await tagStore.initialize();
  }
});

onUnmounted(async () => {
  await boardStore.dispose();
  tagStore.dispose();
});

watch(
  () => authStore.isAuthenticated,
  async authenticated => {
    if (authenticated) {
      await boardStore.initialize();
      await tagStore.initialize();
      return;
    }

    await boardStore.dispose();
    tagStore.dispose();
  }
);
</script>

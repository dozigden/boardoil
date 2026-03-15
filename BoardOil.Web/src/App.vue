<template>
  <main class="app-shell">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <RouterView />
    <RouterView name="dialog" />
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onMounted, onUnmounted } from 'vue';
import { RouterView } from 'vue-router';
import AppHeader from './components/AppHeader.vue';
import { useBoardStore } from './stores/boardStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';

const boardStore = useBoardStore();
const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);

onMounted(async () => {
  await boardStore.initialize();
});

onUnmounted(async () => {
  await boardStore.dispose();
});
</script>

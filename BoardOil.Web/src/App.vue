<template>
  <main :class="['app-shell', `app-shell--${layoutMode}`]">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <component :is="layoutComponent" class="app-content">
      <RouterView />
    </component>
    <RouterView name="dialog" />
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, onUnmounted, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import AppHeader from './components/AppHeader.vue';
import { useBoardStore } from './stores/boardStore';
import { useTagStore } from './stores/tagStore';
import { useAuthStore } from './stores/authStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';
import BoardWorkspaceLayout from './layouts/BoardWorkspaceLayout.vue';
import PageScrollLayout from './layouts/PageScrollLayout.vue';
import { resolveAppLayout } from './layouts/appLayout';

const boardStore = useBoardStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const feedbackStore = useUiFeedbackStore();
const route = useRoute();
const { errorMessage } = storeToRefs(feedbackStore);
const layoutMode = computed(() => resolveAppLayout(route.meta.layout));
const layoutComponent = computed(() => (layoutMode.value === 'board' ? BoardWorkspaceLayout : PageScrollLayout));

onMounted(async () => {
  await authStore.initialize();
  if (authStore.isAuthenticated) {
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
      await tagStore.initialize();
      return;
    }

    await boardStore.dispose();
    tagStore.dispose();
  }
);
</script>

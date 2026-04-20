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
import { useBoardCatalogueStore } from './stores/boardCatalogueStore';
import { useBoardStore } from './stores/boardStore';
import { useTagStore } from './stores/tagStore';
import { useAuthStore } from './stores/authStore';
import { useUiFeedbackStore } from './stores/uiFeedbackStore';
import BoardWorkspaceLayout from './layouts/BoardWorkspaceLayout.vue';
import AdminWorkspaceLayout from './layouts/AdminWorkspaceLayout.vue';
import FullHeightLayout from './layouts/FullHeightLayout.vue';
import PageScrollLayout from './layouts/PageScrollLayout.vue';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD, APP_LAYOUT_FULL_HEIGHT, resolveAppLayout } from './layouts/appLayout';
import { getPageTitle } from './components/appHeaderNavigation';

const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const feedbackStore = useUiFeedbackStore();
const route = useRoute();
const { errorMessage } = storeToRefs(feedbackStore);
const { boards } = storeToRefs(boardCatalogueStore);
const { board, currentBoardId } = storeToRefs(boardStore);
const layoutMode = computed(() => resolveAppLayout(route.meta.layout));
const layoutComponent = computed(() => {
  if (layoutMode.value === APP_LAYOUT_BOARD) {
    return BoardWorkspaceLayout;
  }

  if (layoutMode.value === APP_LAYOUT_ADMIN) {
    return AdminWorkspaceLayout;
  }

  if (layoutMode.value === APP_LAYOUT_FULL_HEIGHT) {
    return FullHeightLayout;
  }

  return PageScrollLayout;
});
const routeBoardId = computed(() => {
  const boardId = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(boardId) ? boardId : null;
});
const pageTitle = computed(() => getPageTitle(board.value, boards.value, currentBoardId.value, routeBoardId.value));

onMounted(async () => {
  await authStore.initialize();
});

onUnmounted(async () => {
  await boardStore.dispose();
  boardCatalogueStore.dispose();
  tagStore.dispose();
});

watch(
  () => authStore.isAuthenticated,
  async authenticated => {
    if (authenticated) {
      await Promise.all([tagStore.initialize(), boardCatalogueStore.loadBoards()]);
      return;
    }

    await boardStore.dispose();
    boardCatalogueStore.dispose();
    tagStore.dispose();
  }
);

watch(
  pageTitle,
  nextTitle => {
    document.title = nextTitle;
  },
  { immediate: true }
);
</script>

<style scoped>
.app-shell {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  min-width: 0;
  padding: 0;
}

.app-shell--board {
  height: 100vh;
  height: 100dvh;
  overflow: hidden;
}

.app-shell--admin {
  height: 100vh;
  height: 100dvh;
  overflow: hidden;
}

.app-shell--full-height {
  height: 100vh;
  height: 100dvh;
  overflow: hidden;
}

.app-shell--page {
  overflow-x: hidden;
}

.app-content {
  flex: 1;
  min-height: 0;
  min-width: 0;
  display: flex;
  flex-direction: column;
  width: 100%;
}

.app-shell--board .app-content {
  overflow: hidden;
}

.app-shell--admin .app-content {
  overflow: hidden;
}

.app-shell--full-height .app-content {
  overflow: hidden;
}

.app-shell--page .app-content {
  overflow: visible;
}

.app-content > * {
  min-height: 0;
  min-width: 0;
}
</style>

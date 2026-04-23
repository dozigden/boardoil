<template>
  <main :class="['app-shell', `app-shell--${layoutMode}`]">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-content-stage">
      <RouterView v-slot="{ Component, route: viewRoute }">
        <Transition :name="pageTransitionName">
          <component :is="layoutComponent" :key="getViewKey(viewRoute)" class="app-content">
            <component :is="Component" />
          </component>
        </Transition>
      </RouterView>
    </section>
    <RouterView name="dialog" />
  </main>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterView, useRoute, type RouteLocationNormalizedLoaded } from 'vue-router';
import AppHeader from './site/components/AppHeader.vue';
import { useBoardCatalogueStore } from './shared/stores/boardCatalogueStore';
import { useBoardStore } from './board/stores/boardStore';
import { useTagStore } from './board/stores/tagStore';
import { useAuthStore } from './shared/stores/authStore';
import { useUiFeedbackStore } from './shared/stores/uiFeedbackStore';
import BoardWorkspaceLayout from './site/layouts/BoardWorkspaceLayout.vue';
import AdminWorkspaceLayout from './site/layouts/AdminWorkspaceLayout.vue';
import FullHeightLayout from './site/layouts/FullHeightLayout.vue';
import PageScrollLayout from './site/layouts/PageScrollLayout.vue';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD, APP_LAYOUT_FULL_HEIGHT, resolveAppLayout } from './site/layouts/appLayout';
import { getPageTitle } from './site/components/appHeaderNavigation';

const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const feedbackStore = useUiFeedbackStore();
const route = useRoute();
const { errorMessage } = storeToRefs(feedbackStore);
const { boards } = storeToRefs(boardCatalogueStore);
const { board, currentBoardId } = storeToRefs(boardStore);
const pageTransitionName = ref('route-none');
const previousRouteSnapshot = ref<RouteSnapshot | null>(null);
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

watch(
  () => ({ name: route.name, boardId: route.params.boardId }),
  () => {
    const current = toRouteSnapshot(route);
    pageTransitionName.value = resolvePageTransition(previousRouteSnapshot.value, current);
    previousRouteSnapshot.value = current;
  },
  { immediate: true }
);

function getViewKey(viewRoute: RouteLocationNormalizedLoaded) {
  const routeName = typeof viewRoute.name === 'string' ? viewRoute.name : 'route';
  return `${routeName}:${JSON.stringify(viewRoute.params ?? {})}`;
}

function toRouteSnapshot(activeRoute: ReturnType<typeof useRoute>): RouteSnapshot {
  const boardIdParam = activeRoute.params.boardId;
  const boardId = Array.isArray(boardIdParam)
    ? (boardIdParam[0] ? String(boardIdParam[0]) : null)
    : (boardIdParam ? String(boardIdParam) : null);

  return {
    name: typeof activeRoute.name === 'string' ? activeRoute.name : '',
    boardId
  };
}

function resolvePageTransition(previous: RouteSnapshot | null, current: RouteSnapshot) {
  if (!previous) {
    return 'route-none';
  }

  const isSameBoard = previous.boardId !== null && previous.boardId === current.boardId;
  if (isSameBoard && previous.name === 'board' && current.name === 'board-archived') {
    return 'conveyor-slide-left';
  }

  if (isSameBoard && previous.name === 'board-archived' && current.name === 'board') {
    return 'conveyor-slide-right';
  }

  return 'route-none';
}

type RouteSnapshot = {
  name: string;
  boardId: string | null;
};
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

.app-content-stage {
  flex: 1;
  min-height: 0;
  min-width: 0;
  position: relative;
  display: flex;
  flex-direction: column;
}

.app-shell--board .app-content-stage {
  overflow: hidden;
}

.app-shell--board :deep(.app-header) {
  margin-bottom: 0.5rem;
}

.app-shell--admin .app-content-stage {
  overflow: hidden;
}

.app-shell--full-height .app-content-stage {
  overflow: hidden;
}

.app-shell--page .app-content-stage {
  overflow: visible;
}

.app-content > * {
  min-height: 0;
  min-width: 0;
}

.conveyor-slide-left-enter-active,
.conveyor-slide-left-leave-active,
.conveyor-slide-right-enter-active,
.conveyor-slide-right-leave-active {
  transition: transform 320ms cubic-bezier(0.22, 1, 0.36, 1);
  will-change: transform;
  position: absolute;
  inset: 0;
}

.conveyor-slide-left-enter-from {
  transform: translate3d(100%, 0, 0);
}

.conveyor-slide-left-enter-to {
  transform: translate3d(0, 0, 0);
}

.conveyor-slide-left-leave-from {
  transform: translate3d(0, 0, 0);
}

.conveyor-slide-left-leave-to {
  transform: translate3d(-100%, 0, 0);
}

.conveyor-slide-right-enter-from {
  transform: translate3d(-100%, 0, 0);
}

.conveyor-slide-right-enter-to {
  transform: translate3d(0, 0, 0);
}

.conveyor-slide-right-leave-from {
  transform: translate3d(0, 0, 0);
}

.conveyor-slide-right-leave-to {
  transform: translate3d(100%, 0, 0);
}
</style>

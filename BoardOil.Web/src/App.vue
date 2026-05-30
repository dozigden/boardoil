<template>
  <RouterView v-slot="{ Component, route: viewRoute }">
    <component :is="layoutComponent" :key="layoutMode" class="app-layout-host">
      <Transition :name="pageTransitionName">
        <component :is="Component" :key="getViewKey(viewRoute)" />
      </Transition>
    </component>
  </RouterView>
  <RouterView v-if="!hideRootDialogView" name="dialog" />
  <ConfirmDialogHost />
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { RouterView, useRoute, type RouteLocationNormalizedLoaded } from 'vue-router';
import ConfirmDialogHost from './shared/components/ConfirmDialogHost.vue';
import { useBoardCatalogueStore } from './shared/stores/boardCatalogueStore';
import { useBoardStore } from './board/stores/boardStore';
import { useTagStore } from './board/stores/tagStore';
import { useAuthStore } from './shared/stores/authStore';
import { useUserProfileImageStore } from './shared/stores/userProfileImageStore';
import StandardLayout from './site/layouts/StandardLayout.vue';
import BoardWithConveyorLayout from './site/layouts/BoardWithConveyorLayout.vue';
import SystemAdminLayout from './site/layouts/SystemAdminLayout.vue';
import BoardAdminWorkspaceLayout from './site/layouts/BoardAdminWorkspaceLayout.vue';
import {
  APP_LAYOUT_ADMIN,
  APP_LAYOUT_BOARD_ADMIN,
  APP_LAYOUT_BOARD_WITH_CONVEYOR,
  resolveAppLayout
} from './site/layouts/appLayout';
import { getPageTitle } from './site/components/appHeaderNavigation';

const boardStore = useBoardStore();
const boardCatalogueStore = useBoardCatalogueStore();
const tagStore = useTagStore();
const authStore = useAuthStore();
const userProfileImageStore = useUserProfileImageStore();
const route = useRoute();
const { boards } = storeToRefs(boardCatalogueStore);
const { board, currentBoardId, isLoadingBoard } = storeToRefs(boardStore);
const pageTransitionName = ref('route-none');
const previousRouteSnapshot = ref<RouteSnapshot | null>(null);
const layoutMode = computed(() => resolveAppLayout(route.meta.layout));
const layoutComponent = computed(() => {
  if (layoutMode.value === APP_LAYOUT_BOARD_WITH_CONVEYOR) {
    return BoardWithConveyorLayout;
  }

  if (layoutMode.value === APP_LAYOUT_ADMIN) {
    return SystemAdminLayout;
  }

  if (layoutMode.value === APP_LAYOUT_BOARD_ADMIN) {
    return BoardAdminWorkspaceLayout;
  }

  return StandardLayout;
});
const routeBoardId = computed(() => {
  const boardId = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(boardId) ? boardId : null;
});
const pageTitle = computed(() => getPageTitle(board.value, boards.value, currentBoardId.value, routeBoardId.value));
const routeRequiresBoardContext = computed(() =>
  route.matched.some(matchedRoute => matchedRoute.meta.requiresBoardContext === true)
);
const hasBoardRouteContext = computed(() => {
  if (!routeRequiresBoardContext.value) {
    return true;
  }

  if (routeBoardId.value === null) {
    return false;
  }

  return (
    !isLoadingBoard.value &&
    currentBoardId.value === routeBoardId.value &&
    board.value?.id === routeBoardId.value
  );
});
const hideRootDialogView = computed(() => !hasBoardRouteContext.value);

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
      await Promise.all([
        boardCatalogueStore.loadBoards(),
        userProfileImageStore.loadOwnProfileImage()
      ]);
      return;
    }

    await boardStore.dispose();
    boardCatalogueStore.dispose();
    tagStore.dispose();
    userProfileImageStore.reset();
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
  const previousIsBoardWorkspace = isBoardWorkspaceRoute(previous.name);
  const currentIsBoardWorkspace = isBoardWorkspaceRoute(current.name);

  if (isSameBoard && previousIsBoardWorkspace && current.name === 'board-archived') {
    return 'conveyor-slide-left';
  }

  if (isSameBoard && previous.name === 'board-archived' && currentIsBoardWorkspace) {
    return 'conveyor-slide-right';
  }

  return 'route-none';
}

function isBoardWorkspaceRoute(routeName: string) {
  return routeName === 'board' || routeName === 'board-card';
}

type RouteSnapshot = {
  name: string;
  boardId: string | null;
};
</script>

<style scoped>
.app-layout-host {
  min-height: 100vh;
  min-height: 100dvh;
  min-width: 0;
  position: relative;
  overflow: hidden;
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

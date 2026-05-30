<template>
  <section class="app-layout app-layout--admin app-layout-with-header">
    <AppHeader />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <section class="app-layout-admin-content">
      <section v-if="!hasBoardContext" class="board-context-loading" aria-live="polite">
        <span class="board-context-loading-indicator" aria-hidden="true" />
        <p class="board-context-loading-label">Loading board...</p>
      </section>
      <slot v-else />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppHeader from '../components/AppHeader.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useBoardStore } from '../../board/stores/boardStore';

const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);
const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const { board, currentBoardId, isLoadingBoard } = storeToRefs(boardStore);
const routeBoardId = computed(() => parseRouteIntParam(route.params.boardId));
const hasBoardContext = computed(() => {
  if (routeBoardId.value === null) {
    return false;
  }

  return (
    !isLoadingBoard.value &&
    currentBoardId.value === routeBoardId.value &&
    board.value?.id === routeBoardId.value
  );
});
let boardContextRequestVersion = 0;

watch(
  routeBoardId,
  async boardId => {
    const requestVersion = ++boardContextRequestVersion;
    if (boardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    if (currentBoardId.value === boardId && board.value?.id === boardId) {
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (requestVersion !== boardContextRequestVersion) {
      return;
    }

    if (!loaded) {
      await router.replace({ name: 'boards' });
    }
  },
  { immediate: true }
);

function parseRouteIntParam(value: unknown) {
  const raw = Array.isArray(value) ? value[0] : value;
  const parsed = Number.parseInt(String(raw ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}
</script>

<style scoped>
.app-layout--admin {
  position: fixed;
  inset: 0;
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

.board-context-loading {
  flex: 1;
  min-height: 0;
  display: grid;
  place-items: center;
  align-content: center;
  justify-items: center;
  gap: 0.75rem;
  padding: 1.5rem;
}

.board-context-loading-indicator {
  width: 2rem;
  height: 2rem;
  border-radius: 999px;
  border: 3px solid var(--bo-border-soft);
  border-top-color: var(--bo-link);
  animation: bo-admin-layout-spin 0.9s linear infinite;
}

.board-context-loading-label {
  margin: 0;
  color: var(--bo-ink-muted);
}

@keyframes bo-admin-layout-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 720px) {
  .app-layout--admin {
    --bo-admin-scroll-inline-padding: 0.75rem;
    --bo-admin-scroll-block-start-padding: 0.75rem;
    --bo-admin-scroll-block-end-padding: 0.75rem;
  }
}
</style>

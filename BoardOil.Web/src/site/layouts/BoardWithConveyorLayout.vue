<template>
  <section class="app-layout app-layout--board-with-conveyor app-layout-with-header">
    <AppHeader compact-mobile />
    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <section class="board-layout-conveyor-region">
      <BoardConveyor
        :highlighted="conveyorConfig.highlighted"
        :left-label="conveyorConfig.leftLabel"
        :left-aria-label="conveyorConfig.leftAriaLabel"
        :left-disabled="conveyorConfig.leftDisabled"
        :right-label="conveyorConfig.rightLabel"
        :right-aria-label="conveyorConfig.rightAriaLabel"
        :right-disabled="conveyorConfig.rightDisabled"
        @left-click="handleLeftClick"
        @right-click="handleRightClick"
      >
        <div :id="registry.conveyorContentTargetId" class="board-layout-conveyor-content-target" />
      </BoardConveyor>
    </section>

    <section class="board-layout-content">
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
import BoardConveyor from '../../board/components/BoardConveyor.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { useBoardStore } from '../../board/stores/boardStore';
import { provideBoardLayoutRegistry } from './boardLayoutRegistry';

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
const registry = provideBoardLayoutRegistry();
const conveyorConfig = registry.conveyorConfig;

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

function handleLeftClick() {
  void registry.conveyorConfig.value.onLeftClick?.();
}

function handleRightClick() {
  void registry.conveyorConfig.value.onRightClick?.();
}

function parseRouteIntParam(value: unknown) {
  const raw = Array.isArray(value) ? value[0] : value;
  const parsed = Number.parseInt(String(raw ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}
</script>

<style scoped>
.app-layout--board-with-conveyor {
  height: 100vh;
  height: 100dvh;
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
  --bo-board-layout-inline-gutter: 1.5rem;
  --bo-board-scroll-inline-padding: 1.5rem;
  --bo-board-layout-gap: var(--bo-standard-gap);
}

.board-layout-conveyor-region {
  --bo-board-conveyor-slot-min-height: 3.2rem;
  --bo-conveyor-min-height: var(--bo-board-conveyor-slot-min-height);
  margin-inline: var(--bo-board-layout-inline-gutter);
}

.board-layout-content {
  display: flex;
  flex-direction: column;
  flex: 1;
  height: 0;
  min-height: 0;
  min-width: 0;
  margin-top: var(--bo-board-layout-gap);
  margin-inline: 0;
  overflow: hidden;
  position: relative;
}

.board-layout-conveyor-content-target {
  min-width: 0;
  width: 100%;
  min-height: var(--bo-board-conveyor-slot-min-height);
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
  animation: bo-layout-spin 0.9s linear infinite;
}

.board-context-loading-label {
  margin: 0;
  color: var(--bo-ink-muted);
}

@keyframes bo-layout-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 767px) {
  .app-layout--board-with-conveyor {
    --bo-board-layout-inline-gutter: 0;
    --bo-board-scroll-inline-padding: 0.375rem;
    --bo-board-layout-gap: 0.3rem;
    --bo-header-margin-active: var(--bo-header-margin-conveyor-mobile);
  }

  .board-layout-conveyor-region {
    --bo-board-conveyor-slot-min-height: 2.5rem;
  }
}
</style>

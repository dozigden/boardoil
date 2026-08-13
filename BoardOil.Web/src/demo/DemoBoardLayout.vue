<template>
  <section class="demo-layout">
    <DemoHeader />
    <p v-if="errorMessage" class="error demo-error">{{ errorMessage }}</p>

    <section class="demo-conveyor-region">
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
        <div :id="registry.conveyorContentTargetId" class="demo-conveyor-content" />
      </BoardConveyor>
    </section>

    <section class="demo-content">
      <section v-if="!hasBoardContext" class="demo-loading" aria-live="polite">
        <span class="demo-loading-indicator" aria-hidden="true" />
        <p>Loading preview...</p>
      </section>
      <slot v-else />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardConveyor from '../board/components/BoardConveyor.vue';
import { useBoardStore } from '../board/stores/boardStore';
import { useUiFeedbackStore } from '../shared/stores/uiFeedbackStore';
import { provideBoardLayoutRegistry } from '../site/layouts/boardLayoutRegistry';
import DemoHeader from './DemoHeader.vue';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const feedbackStore = useUiFeedbackStore();
const { board, currentBoardId, isLoadingBoard } = storeToRefs(boardStore);
const { errorMessage } = storeToRefs(feedbackStore);
const registry = provideBoardLayoutRegistry();
const conveyorConfig = registry.conveyorConfig;
const routeBoardId = computed(() => parseRouteIntParam(route.params.boardId));
const hasBoardContext = computed(() => {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    return false;
  }

  return !isLoadingBoard.value
    && currentBoardId.value === boardId
    && board.value?.id === boardId;
});
let requestVersion = 0;

watch(
  routeBoardId,
  async boardId => {
    const activeRequest = ++requestVersion;
    if (boardId === null) {
      await router.replace({ name: 'board', params: { boardId: 1 } });
      return;
    }

    if (currentBoardId.value === boardId && board.value?.id === boardId) {
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (activeRequest !== requestVersion || loaded) {
      return;
    }

    await router.replace({ name: 'board', params: { boardId: 1 } });
  },
  { immediate: true }
);

function handleLeftClick() {
  void conveyorConfig.value.onLeftClick?.();
}

function handleRightClick() {
  void conveyorConfig.value.onRightClick?.();
}

function parseRouteIntParam(value: unknown) {
  const raw = Array.isArray(value) ? value[0] : value;
  const parsed = Number.parseInt(String(raw ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}
</script>

<style scoped>
.demo-layout {
  --bo-board-layout-inline-gutter: 1.5rem;
  --bo-board-scroll-inline-padding: 1.5rem;
  --bo-board-layout-gap: var(--bo-standard-gap);
  height: 100vh;
  height: 100dvh;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.demo-error {
  margin: 0.5rem 1.5rem 0;
}

.demo-conveyor-region {
  --bo-board-conveyor-slot-min-height: 3.2rem;
  --bo-conveyor-min-height: var(--bo-board-conveyor-slot-min-height);
  margin-top: 0.75rem;
  margin-inline: var(--bo-board-layout-inline-gutter);
}

.demo-conveyor-content {
  width: 100%;
  min-width: 0;
  min-height: var(--bo-board-conveyor-slot-min-height);
}

.demo-content {
  height: 0;
  min-width: 0;
  min-height: 0;
  margin-top: var(--bo-board-layout-gap);
  display: flex;
  flex: 1;
  flex-direction: column;
  overflow: hidden;
  position: relative;
}

.demo-loading {
  min-height: 0;
  display: grid;
  flex: 1;
  place-content: center;
  place-items: center;
  gap: 0.75rem;
  color: var(--bo-ink-muted);
}

.demo-loading-indicator {
  width: 2rem;
  height: 2rem;
  border: 3px solid var(--bo-border-soft);
  border-top-color: var(--bo-link);
  border-radius: 999px;
  animation: demo-spin 0.9s linear infinite;
}

@keyframes demo-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 767px) {
  .demo-layout {
    --bo-board-layout-inline-gutter: 0;
    --bo-board-scroll-inline-padding: 0.375rem;
    --bo-board-layout-gap: 0.3rem;
  }

  .demo-conveyor-region {
    --bo-board-conveyor-slot-min-height: 2.5rem;
    margin-top: 0.4rem;
  }
}
</style>

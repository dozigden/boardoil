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
      <slot />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import AppHeader from '../components/AppHeader.vue';
import BoardConveyor from '../../board/components/BoardConveyor.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { provideBoardLayoutRegistry } from './boardLayoutRegistry';

const feedbackStore = useUiFeedbackStore();
const { errorMessage } = storeToRefs(feedbackStore);
const registry = provideBoardLayoutRegistry();
const conveyorConfig = registry.conveyorConfig;

function handleLeftClick() {
  void registry.conveyorConfig.value.onLeftClick?.();
}

function handleRightClick() {
  void registry.conveyorConfig.value.onRightClick?.();
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
}

@media (max-width: 767px) {
  .app-layout--board-with-conveyor {
    --bo-board-layout-inline-gutter: 0;
    --bo-board-scroll-inline-padding: 0.375rem;
    --bo-board-layout-gap: 0.3rem;
    --bo-header-margin-active: var(--bo-header-margin-conveyor-mobile);
  }
}
</style>

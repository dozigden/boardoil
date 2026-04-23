<template>
  <div class="board-conveyor-tip" :class="tipClass">
    <svg class="board-conveyor-tip-shape" :class="shapeClass" viewBox="0 0 22 44" preserveAspectRatio="none" aria-hidden="true">
      <path class="board-conveyor-tip-fill" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5 Z" />
      <path class="board-conveyor-tip-edge" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5" vector-effect="non-scaling-stroke" />
    </svg>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(defineProps<{
  side?: 'left' | 'right';
}>(), {
  side: 'left'
});

const tipClass = computed(() => (props.side === 'right' ? 'board-conveyor-tip--right' : 'board-conveyor-tip--left'));
const shapeClass = computed(() => (props.side === 'right' ? 'board-conveyor-tip-shape--right' : ''));
</script>

<style scoped>
.board-conveyor-tip {
  position: relative;
  flex: 0 0 var(--bo-conveyor-tip-width);
  width: var(--bo-conveyor-tip-width);
  height: 100%;
  min-height: 100%;
  z-index: 2;
}

.board-conveyor-tip--left {
  margin-right: -2px;
}

.board-conveyor-tip--right {
  margin-left: -2px;
}

.board-conveyor-tip-shape {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.board-conveyor-tip-shape--right {
  transform: scaleX(-1);
  transform-origin: center;
}

.board-conveyor-tip-fill {
  fill: var(--bo-conveyor-end-fill);
}

.board-conveyor-tip-edge {
  fill: none;
  stroke: var(--bo-conveyor-end-stroke);
  stroke-width: 1;
  stroke-linejoin: round;
  stroke-linecap: butt;
}
</style>

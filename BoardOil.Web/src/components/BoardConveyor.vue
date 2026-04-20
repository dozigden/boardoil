<template>
  <header class="board-conveyor">
    <button
      v-if="hasLeftAction"
      type="button"
      class="board-conveyor-end board-conveyor-end--left"
      :aria-label="leftAriaLabel ?? leftLabel ?? undefined"
      :disabled="leftDisabled"
      @click="emit('leftClick')"
    >
      <svg class="board-conveyor-end-shape" viewBox="0 0 140 44" preserveAspectRatio="none" aria-hidden="true">
        <path
          class="board-conveyor-end-shape-path"
          d="M22 1.5 H128.5 Q138.5 1.5 138.5 11.5 V32.5 Q138.5 42.5 128.5 42.5 H22 Q13.5 35.5 1.5 22 Q13.5 8.5 22 1.5 Z"
          vector-effect="non-scaling-stroke"
        />
      </svg>
      <span class="board-conveyor-end-content">
        <ChevronLeft :size="16" aria-hidden="true" />
        <span class="board-conveyor-end-label">{{ leftLabel }}</span>
      </span>
    </button>

    <section class="board-conveyor-main">
      <slot />
    </section>

    <button
      v-if="hasRightAction"
      type="button"
      class="board-conveyor-end board-conveyor-end--right"
      :aria-label="rightAriaLabel ?? rightLabel ?? undefined"
      :disabled="rightDisabled"
      @click="emit('rightClick')"
    >
      <svg class="board-conveyor-end-shape board-conveyor-end-shape--mirrored" viewBox="0 0 140 44" preserveAspectRatio="none" aria-hidden="true">
        <path
          class="board-conveyor-end-shape-path"
          d="M22 1.5 H128.5 Q138.5 1.5 138.5 11.5 V32.5 Q138.5 42.5 128.5 42.5 H22 Q13.5 35.5 1.5 22 Q13.5 8.5 22 1.5 Z"
          vector-effect="non-scaling-stroke"
        />
      </svg>
      <span class="board-conveyor-end-content">
        <span class="board-conveyor-end-label">{{ rightLabel }}</span>
        <ChevronRight :size="16" aria-hidden="true" />
      </span>
    </button>
  </header>
</template>

<script setup lang="ts">
import { ChevronLeft, ChevronRight } from 'lucide-vue-next';
import { computed } from 'vue';

const props = withDefaults(defineProps<{
  leftLabel?: string | null;
  leftAriaLabel?: string | null;
  rightLabel?: string | null;
  rightAriaLabel?: string | null;
  leftDisabled?: boolean;
  rightDisabled?: boolean;
}>(), {
  leftLabel: null,
  leftAriaLabel: null,
  rightLabel: null,
  rightAriaLabel: null,
  leftDisabled: false,
  rightDisabled: false
});

const emit = defineEmits<{
  leftClick: [];
  rightClick: [];
}>();

const hasLeftAction = computed(() => (props.leftLabel ?? '').trim().length > 0);
const hasRightAction = computed(() => (props.rightLabel ?? '').trim().length > 0);
</script>

<style scoped>
.board-conveyor {
  display: flex;
  align-items: stretch;
  gap: 0.55rem;
  margin-inline: 0;
}

.board-conveyor-main {
  flex: 1 1 auto;
  min-width: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
}

.board-conveyor-end {
  --bo-conveyor-end-fill: var(--bo-surface-panel);
  --bo-conveyor-end-stroke: var(--bo-border-soft);
  min-height: 2.55rem;
  min-width: 6.75rem;
  border: none;
  background: transparent;
  color: var(--bo-link);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  position: relative;
  cursor: pointer;
}

.board-conveyor-end-shape {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.board-conveyor-end-shape--mirrored {
  transform: scaleX(-1);
  transform-origin: center;
}

.board-conveyor-end-shape-path {
  fill: var(--bo-conveyor-end-fill);
  stroke: var(--bo-conveyor-end-stroke);
  stroke-width: 1;
  stroke-linejoin: round;
}

.board-conveyor-end-content {
  position: relative;
  z-index: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.3rem;
  padding-inline: 0.8rem;
}

.board-conveyor-end-label {
  font-weight: 600;
  white-space: nowrap;
}

.board-conveyor-end--left {
  border-radius: 12px;
}

.board-conveyor-end--left .board-conveyor-end-content {
  padding-left: 0.95rem;
}

.board-conveyor-end--right {
  border-radius: 12px;
}

.board-conveyor-end--right .board-conveyor-end-content {
  padding-right: 0.95rem;
}

.board-conveyor-end:is(:hover, :focus-visible):not(:disabled) {
  --bo-conveyor-end-fill: color-mix(in srgb, var(--bo-surface-energy) 74%, var(--bo-surface-panel));
  --bo-conveyor-end-stroke: var(--bo-colour-energy);
  color: var(--bo-colour-energy);
}

.board-conveyor-end:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 2px;
}

.board-conveyor-end:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

@media (max-width: 720px) {
  .board-conveyor {
    gap: 0.35rem;
  }

  .board-conveyor-end {
    min-height: 2.35rem;
    min-width: 2.65rem;
    font-size: 0.82rem;
  }

  .board-conveyor-end-label {
    display: none;
  }

  .board-conveyor-end-content {
    padding-inline: 0.55rem;
  }
}
</style>

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
      <div class="board-conveyor-end-tip-wrapper board-conveyor-end-tip-wrapper--left">
        <svg class="board-conveyor-end-shape-tip board-conveyor-end-shape-tip--left" viewBox="0 0 22 44" preserveAspectRatio="none" aria-hidden="true">
          <path class="board-conveyor-end-shape-fill" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5 Z" />
          <path class="board-conveyor-end-shape-edge board-conveyor-end-shape-edge--tip" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5" vector-effect="non-scaling-stroke" />
        </svg>
      </div>
      <div class="board-conveyor-end-body-wrapper">
        <svg class="board-conveyor-end-shape-body board-conveyor-end-shape-body--left" viewBox="0 0 120 44" preserveAspectRatio="none" aria-hidden="true">
          <path class="board-conveyor-end-shape-fill" d="M0 1.5 H108.5 Q118.5 1.5 118.5 11.5 V32.5 Q118.5 42.5 108.5 42.5 H0 Z" />
          <path class="board-conveyor-end-shape-edge board-conveyor-end-shape-edge--body" d="M0 1.5 H108.5 Q118.5 1.5 118.5 11.5 V32.5 Q118.5 42.5 108.5 42.5 H0" vector-effect="non-scaling-stroke" />
        </svg>
        <span class="board-conveyor-end-content">
          <ChevronLeft :size="16" aria-hidden="true" />
          <span class="board-conveyor-end-label">{{ leftLabel }}</span>
        </span>
      </div>
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
      <div class="board-conveyor-end-body-wrapper">
        <svg class="board-conveyor-end-shape-body board-conveyor-end-shape-body--right" viewBox="0 0 120 44" preserveAspectRatio="none" aria-hidden="true">
          <path class="board-conveyor-end-shape-fill" d="M0 1.5 H108.5 Q118.5 1.5 118.5 11.5 V32.5 Q118.5 42.5 108.5 42.5 H0 Z" />
          <path class="board-conveyor-end-shape-edge board-conveyor-end-shape-edge--body" d="M0 1.5 H108.5 Q118.5 1.5 118.5 11.5 V32.5 Q118.5 42.5 108.5 42.5 H0" vector-effect="non-scaling-stroke" />
        </svg>
        <span class="board-conveyor-end-content">
          <span class="board-conveyor-end-label">{{ rightLabel }}</span>
          <ChevronRight :size="16" aria-hidden="true" />
        </span>
      </div>
      <div class="board-conveyor-end-tip-wrapper board-conveyor-end-tip-wrapper--right">
        <svg class="board-conveyor-end-shape-tip board-conveyor-end-shape-tip--right" viewBox="0 0 22 44" preserveAspectRatio="none" aria-hidden="true">
          <path class="board-conveyor-end-shape-fill" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5 Z" />
          <path class="board-conveyor-end-shape-edge board-conveyor-end-shape-edge--tip" d="M22 1.5 Q12 8.5 1.5 22 Q12 35.5 22 42.5" vector-effect="non-scaling-stroke" />
        </svg>
      </div>
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
  --bo-conveyor-min-height: 44px;
  --bo-conveyor-tip-width: 22px;
  display: flex;
  align-items: stretch;
  gap: 0.55rem;
  margin-inline: 0;
}

.board-conveyor-main {
  flex: 1 1 auto;
  min-width: 0;
  min-height: var(--bo-conveyor-min-height);
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  background: var(--bo-surface-panel);
  display: flex;
  align-items: stretch;
}

.board-conveyor-end {
  --bo-conveyor-end-fill: var(--bo-surface-panel);
  --bo-conveyor-end-stroke: var(--bo-border-soft);
  min-height: var(--bo-conveyor-min-height);
  min-width: 6.75rem;
  border: none;
  background: transparent;
  color: var(--bo-link);
  display: inline-flex;
  align-items: stretch;
  justify-content: flex-start;
  padding: 0;
  cursor: pointer;
}

.board-conveyor-end-body-wrapper {
  position: relative;
  display: inline-flex;
  flex: 1 1 auto;
  align-items: center;
  justify-content: center;
  min-width: 0;
  min-height: 100%;
  z-index: 1;
}

.board-conveyor-end-tip-wrapper {
  position: relative;
  flex: 0 0 var(--bo-conveyor-tip-width);
  width: var(--bo-conveyor-tip-width);
  height: 100%;
  min-height: 100%;
  z-index: 2;
}

.board-conveyor-end-shape-body--left {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.board-conveyor-end-shape-body--right {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
  transform: scaleX(-1);
  transform-origin: center;
}

.board-conveyor-end-shape-tip {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.board-conveyor-end-tip-wrapper--left {
  margin-right: -2px;
}

.board-conveyor-end-tip-wrapper--right {
  margin-left: -2px;
}

.board-conveyor-end-shape-tip--right {
  transform: scaleX(-1);
  transform-origin: center;
}

.board-conveyor-end-shape-fill {
  fill: var(--bo-conveyor-end-fill);
}

.board-conveyor-end-shape-edge {
  fill: none;
  stroke: var(--bo-conveyor-end-stroke);
  stroke-width: 1;
  stroke-linejoin: round;
}

.board-conveyor-end-shape-edge--body {
  stroke-linecap: butt;
}

.board-conveyor-end-shape-edge--tip {
  stroke-linecap: butt;
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

.board-conveyor-end--left .board-conveyor-end-content {
  padding-left: 0;
}

.board-conveyor-end--right .board-conveyor-end-content {
  padding-right: 0;
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
    --bo-conveyor-min-height: 40px;
    --bo-conveyor-tip-width: 20px;
    gap: 0.35rem;
  }

  .board-conveyor-end {
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

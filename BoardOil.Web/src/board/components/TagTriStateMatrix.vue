<template>
  <section
    class="tag-tri-state"
    :class="{
      'tag-tri-state--fluid': fluid,
      'tag-tri-state--directional-cursor': showDirectionalCursor,
      'tag-tri-state--parent-scroll': scrollMode === 'parent'
    }"
    :style="rootStyle"
    :aria-label="ariaLabel"
  >
    <div class="tag-tri-state-grid tag-tri-state-grid--header">
      <span class="tag-tri-state-grid-cell tag-tri-state-grid-cell--state">{{ labels.left }}</span>
      <span class="tag-tri-state-grid-cell tag-tri-state-grid-cell--state">{{ labels.middle }}</span>
      <span class="tag-tri-state-grid-cell tag-tri-state-grid-cell--state">{{ labels.right }}</span>
    </div>

    <div v-for="tagName in availableTagNames" :key="`tag-tri-${tagName}`" class="tag-tri-state-grid">
      <button
        type="button"
        class="btn btn--tab tag-tri-state-btn tag-tri-state-btn--left"
        :class="{ 'is-active': getState(tagName) === leftState, 'tag-tri-state-btn--empty': getState(tagName) !== leftState }"
        :disabled="disabled"
        :aria-label="`${leftActionPrefix} ${tagName}`"
        @mouseenter="setHoverTarget(tagName, leftState)"
        @mouseleave="clearHoverTarget(tagName)"
        @click="setState(tagName, leftState)"
      >
        <Tag
          v-if="getState(tagName) === leftState && !shouldRenderStyledItem(tagName)"
          :tag-name="tagName"
          :class="getTagNudgeClass(tagName, leftState)"
        />
        <span
          v-else-if="getState(tagName) === leftState"
          class="tag tag-tri-state-styled-item"
          :class="[
            { 'tag-tri-state-styled-item--card': props.semanticScope === 'card' },
            { 'tag-tri-state-styled-item--slick': props.semanticScope === 'slick' },
            ...getStyledItemClasses(tagName),
            getTagNudgeClass(tagName, leftState)
          ]"
          :style="getStyledItemStyle(tagName)"
        >
          <span class="tag-label">{{ tagName }}</span>
        </span>
        <span v-else class="tag-tri-state-placeholder" aria-hidden="true"></span>
      </button>

      <button
        type="button"
        class="btn btn--tab tag-tri-state-btn tag-tri-state-btn--middle"
        :class="{ 'is-active': getState(tagName) === 'none', 'tag-tri-state-btn--empty': getState(tagName) !== 'none' }"
        :disabled="disabled"
        :aria-label="`${middleActionPrefix} ${tagName}`"
        @mouseenter="setHoverTarget(tagName, 'none')"
        @mouseleave="clearHoverTarget(tagName)"
        @click="setState(tagName, 'none')"
      >
        <Tag
          v-if="getState(tagName) === 'none' && !shouldRenderStyledItem(tagName)"
          :tag-name="tagName"
          :class="getTagNudgeClass(tagName, 'none')"
        />
        <span
          v-else-if="getState(tagName) === 'none'"
          class="tag tag-tri-state-styled-item"
          :class="[
            { 'tag-tri-state-styled-item--card': props.semanticScope === 'card' },
            { 'tag-tri-state-styled-item--slick': props.semanticScope === 'slick' },
            ...getStyledItemClasses(tagName),
            getTagNudgeClass(tagName, 'none')
          ]"
          :style="getStyledItemStyle(tagName)"
        >
          <span class="tag-label">{{ tagName }}</span>
        </span>
        <span v-else class="tag-tri-state-placeholder" aria-hidden="true"></span>
      </button>

      <button
        type="button"
        class="btn btn--tab tag-tri-state-btn tag-tri-state-btn--right"
        :class="{ 'is-active': getState(tagName) === rightState, 'tag-tri-state-btn--empty': getState(tagName) !== rightState }"
        :disabled="disabled"
        :aria-label="`${rightActionPrefix} ${tagName}`"
        @mouseenter="setHoverTarget(tagName, rightState)"
        @mouseleave="clearHoverTarget(tagName)"
        @click="setState(tagName, rightState)"
      >
        <Tag
          v-if="getState(tagName) === rightState && !shouldRenderStyledItem(tagName)"
          :tag-name="tagName"
          :class="getTagNudgeClass(tagName, rightState)"
        />
        <span
          v-else-if="getState(tagName) === rightState"
          class="tag tag-tri-state-styled-item"
          :class="[
            { 'tag-tri-state-styled-item--card': props.semanticScope === 'card' },
            { 'tag-tri-state-styled-item--slick': props.semanticScope === 'slick' },
            ...getStyledItemClasses(tagName),
            getTagNudgeClass(tagName, rightState)
          ]"
          :style="getStyledItemStyle(tagName)"
        >
          <span class="tag-label">{{ tagName }}</span>
        </span>
        <span v-else class="tag-tri-state-placeholder" aria-hidden="true"></span>
      </button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import type { TagFilterState, TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import type { StylePresentation } from '../../shared/utils/styleTypes';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import Tag from './Tag.vue';

const props = withDefaults(defineProps<{
  availableTagNames: string[];
  states: TagFilterStateMap;
  labels: { left: string; middle: string; right: string };
  ariaLabel: string;
  leftState?: TagFilterState;
  rightState?: TagFilterState;
  leftActionPrefix: string;
  middleActionPrefix: string;
  rightActionPrefix: string;
  disabled?: boolean;
  fluid?: boolean;
  showDirectionalCursor?: boolean;
  enableBounce?: boolean;
  scrollMode?: 'self' | 'parent';
  stickyTopOffsetPx?: number;
  stateKeysByName?: Record<string, string>;
  styledItemsByName?: Record<string, StylePresentation>;
  semanticScope?: 'tag' | 'card' | 'slick';
}>(), {
  leftState: 'exclude',
  rightState: 'include',
  disabled: false,
  fluid: false,
  showDirectionalCursor: true,
  enableBounce: true,
  scrollMode: 'self',
  stickyTopOffsetPx: 0,
  stateKeysByName: () => ({}),
  styledItemsByName: () => ({}),
  semanticScope: 'tag'
});

const emit = defineEmits<{
  'update:states': [value: TagFilterStateMap];
}>();

const hoverTargetStates = ref<Record<string, TagFilterState | null>>({});
const rootStyle = computed(() => ({
  '--bo-tag-tri-header-top': `${props.stickyTopOffsetPx}px`
}));

function shouldRenderStyledItem(tagName: string) {
  return resolveStyledItem(tagName) !== null;
}

function getStyledItemClasses(tagName: string) {
  const styledItem = resolveStyledItem(tagName);
  if (!styledItem) {
    return [];
  }

  return getSemanticStyleClasses(styledItem, props.semanticScope);
}

function getStyledItemStyle(tagName: string) {
  const styledItem = resolveStyledItem(tagName);
  if (!styledItem) {
    return {};
  }

  const surfaceStyle = getSurfaceStyle(styledItem, {
    fallbackBackground: 'var(--bo-surface-base)',
    fallbackColor: 'var(--bo-ink-strong)',
    fallbackBorderColor: 'var(--bo-border-soft)'
  });

  if (props.semanticScope !== 'slick') {
    return surfaceStyle;
  }

  const slickBorderColor = surfaceStyle.background ?? surfaceStyle.borderColor;
  return typeof slickBorderColor === 'string' && slickBorderColor.trim().length > 0
    ? { borderColor: slickBorderColor }
    : {};
}

function getState(tagName: string): TagFilterState {
  const stateKey = resolveStateKey(tagName);
  if (!stateKey) {
    return 'none';
  }

  return props.states[stateKey] ?? 'none';
}

function setState(tagName: string, state: TagFilterState) {
  const stateKey = resolveStateKey(tagName);
  if (!stateKey) {
    return;
  }

  const next = { ...props.states };
  if (state === 'none') {
    delete next[stateKey];
  } else {
    next[stateKey] = state;
  }

  emit('update:states', next);
  clearHoverTarget(tagName);
}

function setHoverTarget(tagName: string, targetState: TagFilterState) {
  if (!props.enableBounce) {
    return;
  }

  const stateKey = resolveStateKey(tagName);
  if (!stateKey) {
    return;
  }

  if (getState(tagName) === targetState) {
    clearHoverTarget(tagName);
    return;
  }

  hoverTargetStates.value = {
    ...hoverTargetStates.value,
    [stateKey]: targetState
  };
}

function clearHoverTarget(tagName: string) {
  const stateKey = resolveStateKey(tagName);
  if (!stateKey || hoverTargetStates.value[stateKey] === undefined) {
    return;
  }

  const next = { ...hoverTargetStates.value };
  delete next[stateKey];
  hoverTargetStates.value = next;
}

function getTagNudgeClass(tagName: string, currentState: TagFilterState) {
  if (!props.enableBounce || getState(tagName) !== currentState) {
    return '';
  }

  const stateKey = resolveStateKey(tagName);
  const targetState = stateKey ? hoverTargetStates.value[stateKey] ?? null : null;
  if (targetState === null || targetState === currentState) {
    return '';
  }

  return getOrder(targetState) > getOrder(currentState)
    ? 'tag-tri-nudge-right'
    : 'tag-tri-nudge-left';
}

function getOrder(state: TagFilterState) {
  if (state === props.leftState) {
    return 0;
  }

  if (state === 'none') {
    return 1;
  }

  return 2;
}

function normaliseTagName(tagName: string) {
  return tagName.trim().toLocaleLowerCase();
}

function resolveStateKey(tagName: string) {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return '';
  }

  return props.stateKeysByName[normalisedTagName] ?? normalisedTagName;
}

function resolveStyledItem(tagName: string): StylePresentation | null {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return null;
  }

  return props.styledItemsByName[normalisedTagName] ?? null;
}
</script>

<style scoped>
.tag-tri-state {
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  overflow: auto;
}

.tag-tri-state--parent-scroll {
  overflow: visible;
}

.tag-tri-state-grid {
  display: grid;
  grid-template-columns: 146px 146px 146px;
  gap: 0;
  align-items: stretch;
  justify-content: start;
}

.tag-tri-state--fluid .tag-tri-state-grid {
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(0, 1fr);
  justify-content: stretch;
}

.tag-tri-state-grid--header {
  position: sticky;
  top: var(--bo-tag-tri-header-top, 0px);
  z-index: 1;
  background: var(--bo-surface-panel-strong);
  border-bottom: 1px solid var(--bo-border-soft);
}

.tag-tri-state-grid-cell {
  min-width: 0;
  font-size: 0.78rem;
  padding: 0.45rem 0.5rem 0.4rem;
}

.tag-tri-state-grid-cell--state {
  text-align: center;
  color: var(--bo-ink-muted);
}

.tag-tri-state-btn {
  width: 100%;
  min-width: 0;
  justify-content: center;
  padding: 0.16rem 0.5rem;
  min-height: 1.68rem;
  border-radius: 0;
  background: var(--bo-filter-col-bg, transparent);
  --bo-btn-bg: transparent;
  --bo-btn-border: transparent;
  --bo-btn-ink: inherit;
  --bo-btn-bg-hover: transparent;
  --bo-btn-border-hover: transparent;
  --bo-btn-ink-hover: inherit;
}

.tag-tri-state-btn--empty {
  --bo-btn-ink: transparent;
  --bo-btn-ink-hover: transparent;
}

.tag-tri-state-btn:is(:hover, :focus-visible):not(:disabled) {
  background: var(--bo-filter-col-bg-hover, var(--bo-filter-col-bg, transparent));
  border-color: transparent;
  box-shadow: none;
  outline: none;
}

.tag-tri-state-grid:hover .tag-tri-state-btn {
  background: var(--bo-filter-col-bg-hover, var(--bo-filter-col-bg, transparent));
}

.tag-tri-state-btn--left {
  --bo-filter-col-bg: color-mix(in oklab, var(--bo-colour-danger) 14%, var(--bo-surface-base));
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-colour-danger) 22%, var(--bo-surface-base));
}

.tag-tri-state-btn--middle {
  --bo-filter-col-bg: var(--bo-surface-base);
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-surface-muted) 36%, var(--bo-surface-base));
  cursor: pointer;
}

.tag-tri-state-btn--right {
  --bo-filter-col-bg: color-mix(in oklab, var(--bo-colour-success) 18%, var(--bo-surface-base));
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-colour-success) 28%, var(--bo-surface-base));
}

.tag-tri-state-btn:disabled {
  border-color: transparent;
  box-shadow: none;
  opacity: 1;
}

.tag-tri-state-btn--left :deep(.tag),
.tag-tri-state-btn--middle :deep(.tag),
.tag-tri-state-btn--right :deep(.tag) {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tag-tri-state-styled-item {
  max-width: 100%;
}

.tag-tri-state-styled-item--card {
  border-radius: 7px;
  min-height: 1.32rem;
  padding-inline: 0.55rem;
}

.tag-tri-state-styled-item--slick {
  border-width: 4px;
  border-radius: 12px;
  min-height: 1.62rem;
  padding: 0.12rem 0.75rem;
  background: color-mix(in srgb, var(--bo-surface-base) 90%, transparent);
  color: var(--bo-ink-strong);
}

.tag-tri-state-placeholder {
  display: inline-block;
  width: 100%;
  min-height: 0.92rem;
}

@keyframes tag-tri-nudge-left {
  0% { transform: translateX(0); }
  12% { transform: translateX(-2px); }
  34% { transform: translateX(-5px); }
  56% { transform: translateX(-1.4px); }
  78% { transform: translateX(0); }
  100% { transform: translateX(0); }
}

@keyframes tag-tri-nudge-right {
  0% { transform: translateX(0); }
  12% { transform: translateX(2px); }
  34% { transform: translateX(5px); }
  56% { transform: translateX(1.4px); }
  78% { transform: translateX(0); }
  100% { transform: translateX(0); }
}

.tag-tri-nudge-left {
  animation: tag-tri-nudge-left 0.95s ease-in-out infinite;
}

.tag-tri-nudge-right {
  animation: tag-tri-nudge-right 0.95s ease-in-out infinite;
}

.tag-tri-state--directional-cursor .tag-tri-state-btn--left {
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'%3E%3Cpath fill='%231f2937' d='M10.8 2.7 4.6 8l6.2 5.3v-3h4V5.7h-4z'/%3E%3C/svg%3E") 8 8, w-resize;
}

.tag-tri-state--directional-cursor .tag-tri-state-btn--right {
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'%3E%3Cpath fill='%231f2937' d='M5.2 2.7v3h-4v4.6h4v3L11.4 8z'/%3E%3C/svg%3E") 8 8, e-resize;
}

@media (max-width: 720px) {
  .tag-tri-state-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    width: min(21rem, calc(100vw - 1.5rem));
  }

  .tag-tri-state--fluid .tag-tri-state-grid {
    width: auto;
  }
}
</style>

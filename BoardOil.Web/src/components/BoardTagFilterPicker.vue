<template>
  <div ref="dropdownRoot" class="board-tag-filter-dropdown">
    <div class="board-tag-filter-trigger-row">
      <div class="board-tag-filter-button-wrap">
        <button
          type="button"
          class="btn btn--secondary board-tag-filter-toggle"
          :aria-controls="menuId"
          :aria-expanded="open"
          @click="emit('update:open', !open)"
        >
          <Filter :size="14" aria-hidden="true" />
          <span>Tags</span>
        </button>

        <section v-if="open" :id="menuId" class="panel panel--compact board-tag-filter-menu" aria-label="Tag filter matrix">
          <div class="board-tag-filter-grid board-tag-filter-grid--header">
            <span class="board-tag-filter-grid-cell board-tag-filter-grid-cell--state">Exclude</span>
            <span class="board-tag-filter-grid-cell board-tag-filter-grid-cell--state">Tag</span>
            <span class="board-tag-filter-grid-cell board-tag-filter-grid-cell--state">Include</span>
          </div>

          <div v-for="tagName in availableTagNames" :key="`tag-filter-${tagName}`" class="board-tag-filter-grid">
        <button
          type="button"
          class="btn btn--tab board-tag-filter-state board-tag-filter-state--exclude"
          :class="{
            'is-active': getTagFilterState(tagName) === 'exclude',
            'board-tag-filter-state--empty': getTagFilterState(tagName) !== 'exclude'
          }"
          :aria-label="`Move ${tagName} to exclude`"
          @mouseenter="setHoverTarget(tagName, 'exclude')"
          @mouseleave="clearHoverTarget(tagName)"
          @click="setTagFilterState(tagName, 'exclude')"
        >
          <Tag v-if="getTagFilterState(tagName) === 'exclude'" :tag-name="tagName" :class="getTagNudgeClass(tagName, 'exclude')" />
          <span v-else class="board-tag-filter-placeholder" aria-hidden="true"></span>
        </button>
        <button
          type="button"
          class="btn btn--tab board-tag-filter-state board-tag-filter-state--tag"
          :class="{
            'is-active': getTagFilterState(tagName) === 'none',
            'board-tag-filter-state--empty': getTagFilterState(tagName) !== 'none'
          }"
          :aria-label="`Move ${tagName} to tag`"
          @mouseenter="setHoverTarget(tagName, 'none')"
          @mouseleave="clearHoverTarget(tagName)"
          @click="setTagFilterState(tagName, 'none')"
        >
          <Tag v-if="getTagFilterState(tagName) === 'none'" :tag-name="tagName" :class="getTagNudgeClass(tagName, 'none')" />
          <span v-else class="board-tag-filter-placeholder" aria-hidden="true"></span>
        </button>
        <button
          type="button"
          class="btn btn--tab board-tag-filter-state board-tag-filter-state--include"
          :class="{
            'is-active': getTagFilterState(tagName) === 'include',
            'board-tag-filter-state--empty': getTagFilterState(tagName) !== 'include'
          }"
          :aria-label="`Move ${tagName} to include`"
          @mouseenter="setHoverTarget(tagName, 'include')"
          @mouseleave="clearHoverTarget(tagName)"
          @click="setTagFilterState(tagName, 'include')"
        >
          <Tag v-if="getTagFilterState(tagName) === 'include'" :tag-name="tagName" :class="getTagNudgeClass(tagName, 'include')" />
          <span v-else class="board-tag-filter-placeholder" aria-hidden="true"></span>
        </button>
      </div>
    </section>
  </div>

      <div class="board-tag-filter-counts">
        <span
          v-if="excludedTagCount > 0"
          class="badge board-tag-filter-count-badge"
        >
          {{ excludedTagCount }} exclude
        </span>
        <span
          v-if="includedTagCount > 0"
          class="badge board-tag-filter-count-badge"
        >
          {{ includedTagCount }} include
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Filter } from 'lucide-vue-next';
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import type { TagFilterState, TagFilterStateMap } from '../types/tagFilterTypes';
import Tag from './Tag.vue';

const props = defineProps<{
  availableTagNames: string[];
  filterStates: TagFilterStateMap;
  open: boolean;
}>();

const emit = defineEmits<{
  'update:filterStates': [next: TagFilterStateMap];
  'update:open': [open: boolean];
}>();

const menuId = 'board-tag-filter-menu';
const dropdownRoot = ref<HTMLElement | null>(null);

const includedTagCount = computed(() =>
  props.availableTagNames.filter(tagName => getTagFilterState(tagName) === 'include').length
);

const excludedTagCount = computed(() =>
  props.availableTagNames.filter(tagName => getTagFilterState(tagName) === 'exclude').length
);
const hoverTargetStates = ref<Record<string, TagFilterState | null>>({});

onMounted(() => {
  document.addEventListener('pointerdown', handleDocumentPointerDown);
});

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', handleDocumentPointerDown);
});

function getTagFilterState(tagName: string): TagFilterState {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return 'none';
  }

  return props.filterStates[normalisedTagName] ?? 'none';
}

function setTagFilterState(tagName: string, state: TagFilterState) {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return;
  }

  const next = { ...props.filterStates };
  if (state === 'none') {
    delete next[normalisedTagName];
  } else {
    next[normalisedTagName] = state;
  }

  emit('update:filterStates', next);
  clearHoverTarget(tagName);
}

function setHoverTarget(tagName: string, targetState: TagFilterState) {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return;
  }

  if (getTagFilterState(tagName) === targetState) {
    clearHoverTarget(tagName);
    return;
  }

  hoverTargetStates.value = {
    ...hoverTargetStates.value,
    [normalisedTagName]: targetState
  };
}

function clearHoverTarget(tagName: string) {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName || hoverTargetStates.value[normalisedTagName] === undefined) {
    return;
  }

  const next = { ...hoverTargetStates.value };
  delete next[normalisedTagName];
  hoverTargetStates.value = next;
}

function getTagNudgeClass(tagName: string, currentState: TagFilterState) {
  if (getTagFilterState(tagName) !== currentState) {
    return '';
  }

  const targetState = hoverTargetStates.value[normaliseTagName(tagName)] ?? null;
  if (targetState === null || targetState === currentState) {
    return '';
  }

  return getTagFilterStateOrder(targetState) > getTagFilterStateOrder(currentState)
    ? 'board-tag-nudge-right'
    : 'board-tag-nudge-left';
}

function getTagFilterStateOrder(state: TagFilterState) {
  if (state === 'exclude') {
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

function handleDocumentPointerDown(event: PointerEvent) {
  if (!props.open) {
    return;
  }

  const target = event.target;
  if (!(target instanceof Node)) {
    return;
  }

  if (dropdownRoot.value?.contains(target)) {
    return;
  }

  emit('update:open', false);
}
</script>

<style scoped>
.board-tag-filter-dropdown {
  position: relative;
  flex: 0 0 auto;
  width: fit-content;
  max-width: 100%;
}

.board-tag-filter-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.32rem;
  padding: 0.3rem 0.55rem;
}

.board-tag-filter-trigger-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.board-tag-filter-button-wrap {
  position: relative;
}

.board-tag-filter-counts {
  display: inline-flex;
  align-items: center;
  justify-content: flex-start;
  gap: 0.35rem;
  min-width: 12rem;
}

.board-tag-filter-count-badge {
  justify-content: flex-start;
}

.board-tag-filter-menu {
  position: absolute;
  top: calc(100% + 0.35rem);
  left: 50%;
  transform: translateX(-50%);
  z-index: 12;
  background: var(--bo-surface-base);
  padding: 0;
  width: fit-content;
  max-width: calc(100vw - 3.5rem);
  max-height: min(56vh, 420px);
  overflow: auto;
  box-shadow: var(--bo-shadow-pop);
  display: grid;
  gap: 0;
}

.board-tag-filter-grid {
  display: grid;
  grid-template-columns: 146px 146px 146px;
  gap: 0;
  align-items: stretch;
  justify-content: start;
  padding: 0;
}

.board-tag-filter-grid--header {
  position: sticky;
  top: 0;
  background: var(--bo-surface-panel-strong);
  border-bottom: 1px solid var(--bo-border-soft);
  z-index: 1;
}

.board-tag-filter-grid-cell {
  min-width: 0;
  font-size: 0.78rem;
  padding: 0.45rem 0.5rem 0.4rem;
}

.board-tag-filter-grid-cell--state {
  text-align: center;
  color: var(--bo-ink-muted);
}

.board-tag-filter-state {
  width: 100%;
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

.board-tag-filter-state--empty {
  --bo-btn-ink: transparent;
  --bo-btn-ink-hover: transparent;
}

.board-tag-filter-state:is(:hover, :focus-visible):not(:disabled) {
  background: var(--bo-filter-col-bg-hover, var(--bo-filter-col-bg, transparent));
  border-color: transparent;
  box-shadow: none;
  outline: none;
}

.board-tag-filter-grid:hover .board-tag-filter-state {
  background: var(--bo-filter-col-bg-hover, var(--bo-filter-col-bg, transparent));
}

.board-tag-filter-state--exclude {
  --bo-filter-col-bg: color-mix(in oklab, var(--bo-colour-danger) 14%, var(--bo-surface-base));
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-colour-danger) 22%, var(--bo-surface-base));
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'%3E%3Cpath fill='%231f2937' d='M10.8 2.7 4.6 8l6.2 5.3v-3h4V5.7h-4z'/%3E%3C/svg%3E")
      8 8,
    w-resize;
}

.board-tag-filter-state--tag {
  --bo-filter-col-bg: var(--bo-surface-base);
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-surface-muted) 36%, var(--bo-surface-base));
  cursor: pointer;
}

.board-tag-filter-state--include {
  --bo-filter-col-bg: color-mix(in oklab, var(--bo-colour-success) 18%, var(--bo-surface-base));
  --bo-filter-col-bg-hover: color-mix(in oklab, var(--bo-colour-success) 28%, var(--bo-surface-base));
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 16 16'%3E%3Cpath fill='%231f2937' d='M5.2 2.7v3h-4v4.6h4v3L11.4 8z'/%3E%3C/svg%3E")
      8 8,
    e-resize;
}

.board-tag-filter-state :deep(.tag) {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@keyframes bo-tag-nudge-left {
  0% {
    transform: translateX(0);
  }

  12% {
    transform: translateX(-2px);
  }

  34% {
    transform: translateX(-5px);
  }

  56% {
    transform: translateX(-1.4px);
  }

  78% {
    transform: translateX(0);
  }

  100% {
    transform: translateX(0);
  }
}

@keyframes bo-tag-nudge-right {
  0% {
    transform: translateX(0);
  }

  12% {
    transform: translateX(2px);
  }

  34% {
    transform: translateX(5px);
  }

  56% {
    transform: translateX(1.4px);
  }

  78% {
    transform: translateX(0);
  }

  100% {
    transform: translateX(0);
  }
}

.board-tag-nudge-left {
  animation: bo-tag-nudge-left 0.95s ease-in-out infinite;
}

.board-tag-nudge-right {
  animation: bo-tag-nudge-right 0.95s ease-in-out infinite;
}

.board-tag-filter-placeholder {
  display: inline-block;
  width: 100%;
  min-height: 0.92rem;
}

@media (max-width: 720px) {
  .board-tag-filter-menu {
    width: fit-content;
    max-width: calc(100vw - 1.5rem);
  }

  .board-tag-filter-grid {
    grid-template-columns: 124px 124px 124px;
    gap: 0;
  }
}
</style>

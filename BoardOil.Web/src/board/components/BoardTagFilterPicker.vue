<template>
  <div ref="dropdownRoot" class="board-tag-filter-dropdown">
    <div class="board-tag-filter-trigger-row">
      <div class="board-tag-filter-button-wrap">
        <button
          type="button"
          class="btn btn--secondary board-tag-filter-toggle"
          :class="{ 'board-tag-filter-toggle--active': hasActiveTagFilters }"
          aria-label="Tag filters"
          title="Tag filters"
          :aria-controls="menuId"
          :aria-expanded="open"
          @click="emit('update:open', !open)"
        >
          <Filter :size="14" aria-hidden="true" />
          <span class="board-tag-filter-toggle-label">Tags</span>
        </button>

        <section v-if="open" :id="menuId" class="panel panel--compact board-tag-filter-menu" aria-label="Tag filter matrix">
          <TagTriStateMatrix
            :available-tag-names="availableTagNames"
            :states="filterStates"
            :labels="{ left: 'Exclude', middle: 'Tag', right: 'Include' }"
            :ariaLabel="'Tag filter matrix'"
            left-action-prefix="Move to exclude"
            middle-action-prefix="Move to tag"
            right-action-prefix="Move to include"
            :show-directional-cursor="true"
            :enable-bounce="true"
            @update:states="emit('update:filterStates', $event)"
          />
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Filter } from 'lucide-vue-next';
import { ref } from 'vue';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { useClickOutside } from '../../shared/composables/useClickOutside';
import TagTriStateMatrix from './TagTriStateMatrix.vue';

const props = defineProps<{
  availableTagNames: string[];
  filterStates: TagFilterStateMap;
  hasActiveTagFilters: boolean;
  open: boolean;
}>();

const emit = defineEmits<{
  'update:filterStates': [next: TagFilterStateMap];
  'update:open': [open: boolean];
}>();

const menuId = 'board-tag-filter-menu';
const dropdownRoot = ref<HTMLElement | null>(null);

useClickOutside(dropdownRoot, () => {
  emit('update:open', false);
}, () => props.open);
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
  min-height: var(--bo-board-filter-control-height, 2.3rem);
  padding: 0 0.65rem;
  transition: border-color 140ms ease, background-color 140ms ease, color 140ms ease;
}

.board-tag-filter-toggle--active {
  --bo-btn-bg: var(--bo-colour-energy);
  --bo-btn-border: var(--bo-surface-energy);
  --bo-btn-ink: var(--bo-surface-energy);
  --bo-btn-bg-hover: var(--bo-colour-energy-strong);
  --bo-btn-border-hover: var(--bo-surface-energy);
  --bo-btn-ink-hover: var(--bo-surface-energy);
}

.board-tag-filter-trigger-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.board-tag-filter-button-wrap {
  position: relative;
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
}

@media (max-width: 720px) {
  .board-tag-filter-dropdown {
    min-width: 0;
  }

  .board-tag-filter-toggle {
    min-height: var(--bo-board-filter-control-height, 2.3rem);
    padding: 0 0.5rem;
    min-width: 2rem;
    justify-content: center;
  }

  .board-tag-filter-toggle-label {
    display: none;
  }

  .board-tag-filter-menu {
    left: 0;
    right: auto;
    transform: none;
    width: min(21rem, calc(100vw - 1.5rem));
    max-width: calc(100vw - 1.5rem);
  }
}
</style>

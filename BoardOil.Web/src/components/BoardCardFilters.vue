<template>
  <header :class="rootClasses">
    <div class="board-main-controls">
      <div class="board-search-pane">
        <label class="board-search-field">
          <input
            :value="searchText"
            type="search"
            placeholder="Search"
            maxlength="200"
            @input="emit('update:searchText', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </div>

      <div class="board-controls-pane">
        <BoardTagFilterPicker
          v-if="availableTagNames.length > 0"
          :available-tag-names="availableTagNames"
          :filter-states="filterStates"
          :open="pickerOpen"
          @update:filter-states="emit('update:filterStates', $event)"
          @update:open="emit('update:pickerOpen', $event)"
        />

        <div class="board-filters-summary">
          <button
            type="button"
            class="btn btn--secondary board-clear-filters"
            aria-label="Clear card filters"
            title="Clear filters"
            :disabled="!hasActiveFilters"
            @click="emit('clear')"
          >
            <X :size="16" aria-hidden="true" />
            <span class="board-clear-filters-label">Clear</span>
          </button>
        </div>
      </div>
    </div>

    <div class="board-selection-pane">
      <button
        type="button"
        class="btn btn--secondary board-selection-toggle"
        :aria-label="selectionMode ? 'Done selecting cards' : 'Select cards'"
        :title="selectionMode ? 'Done selecting cards' : 'Select cards'"
        @click="emit('toggleSelectionMode')"
      >
        <span class="board-selection-toggle-label">
          {{ selectionMode ? 'Done' : 'Select' }}
        </span>
        <span v-if="selectionMode && selectedCount > 0" class="board-selection-toggle-count">
          {{ selectedCount }}
        </span>
      </button>
    </div>
  </header>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next';
import { computed } from 'vue';
import type { TagFilterStateMap } from '../types/tagFilterTypes';
import BoardTagFilterPicker from './BoardTagFilterPicker.vue';

const props = withDefaults(defineProps<{
  searchText: string;
  availableTagNames: string[];
  filterStates: TagFilterStateMap;
  pickerOpen: boolean;
  hasActiveFilters: boolean;
  selectionMode?: boolean;
  selectedCount?: number;
  embedded?: boolean;
}>(), {
  selectionMode: false,
  selectedCount: 0,
  embedded: false
});

const emit = defineEmits<{
  'update:searchText': [value: string];
  'update:filterStates': [value: TagFilterStateMap];
  'update:pickerOpen': [value: boolean];
  clear: [];
  toggleSelectionMode: [];
}>();

const rootClasses = computed(() => (
  props.embedded
    ? ['board-filters', 'board-filters--embedded']
    : ['panel', 'panel--compact', 'board-filters']
));
</script>

<style scoped>
.board-filters {
  --bo-board-filter-control-height: 2.3rem;
  margin-top: 0;
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.board-filters:not(.board-filters--embedded) {
  margin-inline: 1.5rem;
}

.board-filters--embedded {
  margin-inline: 0;
  padding: 0.45rem 0.6rem;
}

.board-main-controls {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-width: 0;
  flex: 1 1 auto;
}

.board-search-pane {
  display: flex;
  align-items: center;
  width: min(100%, 22rem);
  flex: 0 1 22rem;
}

.board-search-field {
  flex: 0 1 auto;
  width: 100%;
  min-width: 0;
}

.board-controls-pane {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 0.6rem;
  flex: 0 1 auto;
}

.board-filters-summary {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.board-clear-filters {
  min-height: var(--bo-board-filter-control-height);
  padding: 0 0.65rem;
}

.board-selection-toggle {
  min-height: var(--bo-board-filter-control-height);
  min-width: 4.85rem;
  padding: 0 0.65rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
}

.board-selection-pane {
  margin-left: auto;
  flex: 0 0 auto;
}

.board-selection-toggle-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.25rem;
  height: 1.25rem;
  padding: 0 0.34rem;
  border-radius: 999px;
  background: var(--bo-surface-energy);
  color: var(--bo-text-default);
  font-size: 0.76rem;
  line-height: 1;
}

.board-search-field input {
  min-height: var(--bo-board-filter-control-height);
}

@media (max-width: 720px) {
  .board-filters:not(.board-filters--embedded) {
    margin-inline: 0;
    gap: 0.5rem;
    align-items: center;
    border-top: none;
    border-left: none;
    border-right: none;
    border-radius: 0 0 10px 10px;
    padding: 0.5rem 0.75rem;
  }

  .board-filters--embedded {
    padding: 0.32rem 0.45rem;
    gap: 0.45rem;
  }

  .board-main-controls {
    gap: 0.35rem;
  }

  .board-search-pane {
    width: 100%;
    flex-basis: auto;
  }

  .board-controls-pane {
    align-items: center;
    justify-content: flex-start;
    gap: 0.35rem;
    min-width: 0;
    flex-wrap: nowrap;
  }

  .board-filters-summary {
    flex: 0 0 auto;
  }

  .board-clear-filters {
    padding: 0 0.5rem;
  }
}
</style>

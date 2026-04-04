<template>
  <header class="panel panel--compact board-filters">
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
  </header>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next';
import type { TagFilterStateMap } from '../types/tagFilterTypes';
import BoardTagFilterPicker from './BoardTagFilterPicker.vue';

defineProps<{
  searchText: string;
  availableTagNames: string[];
  filterStates: TagFilterStateMap;
  pickerOpen: boolean;
  hasActiveFilters: boolean;
}>();

const emit = defineEmits<{
  'update:searchText': [value: string];
  'update:filterStates': [value: TagFilterStateMap];
  'update:pickerOpen': [value: boolean];
  clear: [];
}>();
</script>

<style scoped>
.board-filters {
  margin-inline: 1.5rem;
  margin-top: 0;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0.6rem 0.9rem;
  align-items: start;
}

.board-search-pane {
  display: flex;
  align-items: center;
}

.board-search-field {
  flex: 1 1 auto;
  min-width: 0;
}

.board-controls-pane {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.6rem;
}

.board-filters-summary {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.board-clear-filters {
  padding: 0.35rem 0.55rem;
}

@media (max-width: 720px) {
  .board-filters {
    margin-inline: 0;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 0.5rem;
    align-items: center;
    border-top: none;
    border-left: none;
    border-right: none;
    border-radius: 0 0 10px 10px;
    padding: 0.5rem 0.75rem;
  }

  .board-search-pane {
    width: 100%;
  }

  .board-controls-pane {
    align-items: center;
    justify-content: flex-end;
    gap: 0.35rem;
    min-width: 0;
    flex-wrap: nowrap;
  }

  .board-filters-summary {
    margin-left: 0;
    flex: 0 0 auto;
  }

  .board-clear-filters {
    padding: 0.3rem 0.45rem;
  }
}
</style>
